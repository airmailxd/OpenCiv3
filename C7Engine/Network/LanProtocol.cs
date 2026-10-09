using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using C7GameData;
using C7GameData.Save;

namespace C7Engine.Network;

// The messages a LAN host and its clients exchange. Each frame on the wire is
// a 4-byte little-endian length, a FrameKind byte, and the payload, which is
// JSON (see NetSerialization) except for snapshots.
public enum FrameKind : byte {
	// Client to host.
	Hello = 1,
	ClaimSeat = 2,
	Command = 3,
	// Watch the game without playing: the host sends snapshots and the
	// messages for everyone, and ignores anything the spectator sends.
	Watch = 4,
	// A seated guest's civilization for a game not created yet.
	ChooseCivilization = 5,
	// Gives back a seat taken before the game started.
	LeaveSeat = 6,
	// Asks for the whole game in the next snapshot, from a client that
	// couldn't apply a SnapshotDelta.
	RequestSnapshot = 7,
	// A Command the client has already carried out on its own game (see
	// MovePrediction), which the host answers with a snapshot even if its
	// game is unchanged, to put the client's right.
	PredictedCommand = 8,

	// Host to client.
	Lobby = 10,
	Rejected = 11,
	Start = 12,
	Snapshot = 13,
	UiMessage = 14,
	TurnClock = 15,
	// A snapshot as a patch to the one sent before it (see EncodedSnapshot).
	SnapshotDelta = 16,
}

public static class LanProtocol {
	// Bump when the frames or the messages in them change incompatibly.
	public const int Version = 10;

	public const int DefaultPort = 47_777;
	public const int DiscoveryPort = 47_778;

	// Frames larger than this are treated as a broken connection.
	public const int MaxFrameBytes = 64 * 1024 * 1024;

	// Snapshots that decompress to more than this are treated as broken,
	// rather than read until memory runs out.
	public const int MaxSnapshotJsonBytes = 1024 * 1024 * 1024;

	public const string DiscoveryRequest = "C7-LAN-DISCOVER";

	// The whole game, compressed, for a client to show.
	public static byte[] EncodeSnapshot(GameData gameData) {
		return EncodeSnapshot(SnapshotOf(gameData)).Compressed;
	}

	// The game as it stands, to be encoded by EncodeSnapshot. This must be
	// called on the thread that runs the game, but what it returns shares
	// nothing the game goes on to change, so it can be encoded on another
	// thread while the game carries on.
	public static SaveGame SnapshotOf(GameData gameData) {
		SaveGame save = SaveGame.FromGameData(gameData);
		SnapshotDetacher.Detach(save);
		return save;
	}

	// Encodes a snapshot from SnapshotOf, on any thread. When it's identical
	// to the previous one, the previous one's encoding is returned rather
	// than encoding it all over again. Otherwise the patch from the previous
	// one is made straight away, since that's what most connections need.
	public static EncodedSnapshot EncodeSnapshot(SaveGame snapshot, EncodedSnapshot previous = null) {
		byte[] json = snapshot.ToCompactJSON();
		byte[] hash = SHA256.HashData(json);
		if (previous != null && previous.Hash.AsSpan().SequenceEqual(hash)) {
			return previous;
		}
		EncodedSnapshot encoded = new(json, hash);
		if (previous != null) {
			encoded.PatchFrom(previous);
		}
		return encoded;
	}

	// Reads a Snapshot frame.
	public static SaveGame DecodeSnapshot(byte[] snapshot) {
		return SaveGame.FromJSON(ReadSnapshot(FrameKind.Snapshot, snapshot, null).Json);
	}

	// The game in a Snapshot or SnapshotDelta frame, as JSON. A delta needs
	// the snapshot it patches, which is the last one read from the same
	// connection. Throws InvalidDataException if the frame is broken or
	// patches some other snapshot.
	public static ReceivedSnapshot ReadSnapshot(FrameKind kind, byte[] payload, ReceivedSnapshot previous) {
		if (kind == FrameKind.Snapshot) {
			byte[] whole = SnapshotCompression.Decompress(payload, MaxSnapshotJsonBytes);
			return new ReceivedSnapshot(whole, SHA256.HashData(whole));
		}
		if (kind != FrameKind.SnapshotDelta) {
			throw new ArgumentException($"A {kind} frame isn't a snapshot");
		}
		if (payload.Length < 2 * HashBytes) {
			throw new InvalidDataException("The snapshot delta is too short");
		}
		ReadOnlySpan<byte> baseHash = payload.AsSpan(0, HashBytes);
		ReadOnlySpan<byte> hash = payload.AsSpan(HashBytes, HashBytes);
		if (previous == null || !baseHash.SequenceEqual(previous.Hash)) {
			throw new InvalidDataException("The snapshot delta patches a snapshot we don't have");
		}
		byte[] json = SnapshotCompression.Decompress(payload.AsSpan(2 * HashBytes), MaxSnapshotJsonBytes, previous.Json);
		byte[] actual = SHA256.HashData(json);
		if (!hash.SequenceEqual(actual)) {
			throw new InvalidDataException("The snapshot delta doesn't give the snapshot it should");
		}
		return new ReceivedSnapshot(json, actual);
	}

	internal const int HashBytes = 32;
}

// A snapshot as encoded for sending: the game's JSON, its hash, which is the
// same for two snapshots exactly when they hold the same game, and the ways
// it can be sent. A connection that hasn't been sent a snapshot yet gets the
// whole game compressed, in a Snapshot frame; after that, it gets a
// SnapshotDelta frame with the hash of the snapshot it was sent last, the
// hash of this one, and this one compressed as a patch to that one. The
// connection writes frames in order and TCP delivers them in order, so the
// last snapshot written to a connection is the one its client has.
//
// Both are made the first time a connection asks for them, on that
// connection's thread, and kept for the other connections, which mostly want
// the same: everyone is usually sent the same snapshots.
public sealed class EncodedSnapshot {
	public byte[] Json { get; }
	public byte[] Hash { get; }

	private readonly Lazy<byte[]> compressed;
	private readonly ConcurrentDictionary<string, Lazy<byte[]>> patches = new();

	public EncodedSnapshot(byte[] json, byte[] hash) {
		Json = json;
		Hash = hash;
		compressed = new(() => SnapshotCompression.Compress(Json));
	}

	// The whole game, compressed, for a Snapshot frame.
	public byte[] Compressed => compressed.Value;

	// A SnapshotDelta frame's payload, for a client that has the earlier
	// snapshot; or null when sending the whole game is about as small.
	public byte[] PatchFrom(EncodedSnapshot earlier) {
		Lazy<byte[]> patch = patches.GetOrAdd(Convert.ToHexString(earlier.Hash), _ => new(() => MakePatch(earlier)));
		return patch.Value;
	}

	private byte[] MakePatch(EncodedSnapshot earlier) {
		// A small change patches to well under a fiftieth of the JSON, which
		// is a fraction of the whole game compressed, without compressing it
		// to compare. A larger patch is only worth it if it's clearly smaller
		// than the whole game.
		byte[] patch = SnapshotCompression.CompressPatch(Json, earlier.Json, Json.Length / 64)
			?? SnapshotCompression.CompressPatch(Json, earlier.Json, Compressed.Length * 3 / 4);
		if (patch == null) {
			return null;
		}
		byte[] payload = new byte[2 * LanProtocol.HashBytes + patch.Length];
		earlier.Hash.CopyTo(payload, 0);
		Hash.CopyTo(payload, LanProtocol.HashBytes);
		patch.CopyTo(payload, 2 * LanProtocol.HashBytes);
		return payload;
	}
}

// A snapshot as a client read it: the game's JSON and its hash.
public sealed record ReceivedSnapshot(byte[] Json, byte[] Hash);

// reconnectToken is the token the host gave this player when they took
// their seats, to have them back after losing the connection; null to join
// afresh.
public record HelloInfo(int version, string playerName, string reconnectToken = null);

// A guest may take several seats, for players taking turns at their machine,
// so each seat can have its own player's name; null for the guest's name.
public record ClaimSeatInfo(ID playerID, string playerName = null);

// A human player's place in the game, and who has taken it. In a new game
// whose guests choose their civilizations, a guest's civilization is null
// until they choose one, which means a random one. A disconnected seat is
// held for the guest who lost their connection to it, and nobody else can
// take it; an away seat's turns end by themselves, since the host chose to
// go on without its player until they are back.
public record SeatInfo(ID playerID, string civilization, string playerName, bool isHost, string takenBy,
	bool disconnected = false, bool away = false);

// A civilization a guest can choose, with what the lobby shows about it.
public record CivilizationChoice(string name, string leader, string noun, string leaderArtFile, List<string> traits);

// civilizations is what guests can choose from, or null when the game's
// civilizations are already set (a saved game, or one already created).
// creatingGame is true while the host creates the world, when choices are
// closed. yourSeats are the seats this guest has taken, in turn order.
// simultaneousTurns is whether the humans will play their turns at once.
// reconnectToken is this guest's, to say hello with to have its seats back
// if the connection is lost. hideUnseen is whether each guest is sent only
// what its players may know of the game.
public record LobbyInfo(string hostName, List<SeatInfo> seats, List<ID> yourSeats, List<string> spectators = null,
	List<CivilizationChoice> civilizations = null, bool creatingGame = false, bool started = false,
	bool simultaneousTurns = false, string reconnectToken = null, bool hideUnseen = false);

// The civilization's name, or null for a random one, for one of the guest's
// seats; null for their first.
public record ChooseCivilizationInfo(string civilization, ID playerID = null);

// The players this guest plays, in turn order; empty for a spectator. The
// token is as in LobbyInfo.
public record StartInfo(List<ID> yourPlayerIDs, string reconnectToken = null);

// A host's answer to a discovery broadcast.
public record DiscoveryReply(string hostName, int port, int openSeats, bool started);

// Whose turn it is and how long they have had it, for the scoreboard, and
// which players are at their machines. secondsAllowed is null when turns have
// no time limit. playersToMove are the humans yet to finish their turn, in
// turn order: with simultaneous turns, everyone the clock is running for, and
// otherwise at most the active player. awayPlayers are those the host went on
// without, whose turns end by themselves until they are back.
public record TurnClockInfo(ID activePlayerID, int turn, double secondsElapsed, double? secondsAllowed, List<ID> connectedPlayers,
	List<ID> playersToMove = null, List<ID> awayPlayers = null);

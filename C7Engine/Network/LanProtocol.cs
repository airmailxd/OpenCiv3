using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

	// Host to client.
	Lobby = 10,
	Rejected = 11,
	Start = 12,
	Snapshot = 13,
	UiMessage = 14,
	TurnClock = 15,
}

public static class LanProtocol {
	// Bump when the frames or the messages in them change incompatibly.
	public const int Version = 7;

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
	// than compressing it all over again.
	public static EncodedSnapshot EncodeSnapshot(SaveGame snapshot, EncodedSnapshot previous = null) {
		byte[] json = snapshot.ToCompactJSON();
		byte[] hash = SHA256.HashData(json);
		if (previous != null && previous.Hash.AsSpan().SequenceEqual(hash)) {
			return previous;
		}
		MemoryStream compressed = new();
		using (GZipStream gzip = new(compressed, CompressionLevel.Fastest, leaveOpen: true)) {
			gzip.Write(json);
		}
		return new EncodedSnapshot(compressed.ToArray(), hash);
	}

	public static SaveGame DecodeSnapshot(byte[] snapshot) {
		using GZipStream gzip = new(new MemoryStream(snapshot), CompressionMode.Decompress);
		MemoryStream json = new();
		byte[] buffer = new byte[81920];
		int read;
		while ((read = gzip.Read(buffer)) > 0) {
			if (json.Length + read > MaxSnapshotJsonBytes) {
				throw new InvalidDataException($"The snapshot is larger than {MaxSnapshotJsonBytes} bytes");
			}
			json.Write(buffer, 0, read);
		}
		return SaveGame.FromJSON(json.ToArray());
	}
}

// A snapshot as sent: the compressed game, and a hash of the game it holds,
// which is the same for two snapshots exactly when they hold the same game.
public sealed class EncodedSnapshot {
	public byte[] Compressed { get; }
	public byte[] Hash { get; }

	public EncodedSnapshot(byte[] compressed, byte[] hash) {
		Compressed = compressed;
		Hash = hash;
	}
}

public record HelloInfo(int version, string playerName);

// A guest may take several seats, for players taking turns at their machine,
// so each seat can have its own player's name; null for the guest's name.
public record ClaimSeatInfo(ID playerID, string playerName = null);

// A human player's place in the game, and who has taken it. In a new game
// whose guests choose their civilizations, a guest's civilization is null
// until they choose one, which means a random one.
public record SeatInfo(ID playerID, string civilization, string playerName, bool isHost, string takenBy);

// A civilization a guest can choose, with what the lobby shows about it.
public record CivilizationChoice(string name, string leader, string noun, string leaderArtFile, List<string> traits);

// civilizations is what guests can choose from, or null when the game's
// civilizations are already set (a saved game, or one already created).
// creatingGame is true while the host creates the world, when choices are
// closed. yourSeats are the seats this guest has taken, in turn order.
// simultaneousTurns is whether the humans will play their turns at once.
public record LobbyInfo(string hostName, List<SeatInfo> seats, List<ID> yourSeats, List<string> spectators = null,
	List<CivilizationChoice> civilizations = null, bool creatingGame = false, bool started = false,
	bool simultaneousTurns = false);

// The civilization's name, or null for a random one, for one of the guest's
// seats; null for their first.
public record ChooseCivilizationInfo(string civilization, ID playerID = null);

// The players this guest plays, in turn order; empty for a spectator.
public record StartInfo(List<ID> yourPlayerIDs);

// A host's answer to a discovery broadcast.
public record DiscoveryReply(string hostName, int port, int openSeats, bool started);

// Whose turn it is and how long they have had it, for the scoreboard, and
// which players are at their machines. secondsAllowed is null when turns have
// no time limit. playersToMove are the humans yet to finish their turn, in
// turn order: with simultaneous turns, everyone the clock is running for, and
// otherwise at most the active player.
public record TurnClockInfo(ID activePlayerID, int turn, double secondsElapsed, double? secondsAllowed, List<ID> connectedPlayers,
	List<ID> playersToMove = null);

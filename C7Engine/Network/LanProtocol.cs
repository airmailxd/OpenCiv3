using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
	public const int Version = 4;

	public const int DefaultPort = 47_777;
	public const int DiscoveryPort = 47_778;

	// Frames larger than this are treated as a broken connection.
	public const int MaxFrameBytes = 64 * 1024 * 1024;

	public const string DiscoveryRequest = "C7-LAN-DISCOVER";

	// The whole game, compressed, for a client to show.
	public static byte[] EncodeSnapshot(GameData gameData) {
		byte[] json = SaveGame.FromGameData(gameData).ToCompactJSON();
		MemoryStream compressed = new();
		using (GZipStream gzip = new(compressed, CompressionLevel.Fastest, leaveOpen: true)) {
			gzip.Write(json);
		}
		return compressed.ToArray();
	}

	public static SaveGame DecodeSnapshot(byte[] snapshot) {
		using GZipStream gzip = new(new MemoryStream(snapshot), CompressionMode.Decompress);
		MemoryStream json = new();
		gzip.CopyTo(json);
		return SaveGame.FromJSON(json.ToArray());
	}
}

public record HelloInfo(int version, string playerName);

public record ClaimSeatInfo(ID playerID);

// A human player's place in the game, and who has taken it. In a new game
// whose guests choose their civilizations, a guest's civilization is null
// until they choose one, which means a random one.
public record SeatInfo(ID playerID, string civilization, string playerName, bool isHost, string takenBy);

// A civilization a guest can choose, with what the lobby shows about it.
public record CivilizationChoice(string name, string leader, string noun, string leaderArtFile, List<string> traits);

// civilizations is what guests can choose from, or null when the game's
// civilizations are already set (a saved game, or one already created).
// creatingGame is true while the host creates the world, when choices are
// closed.
public record LobbyInfo(string hostName, List<SeatInfo> seats, ID yourSeat, List<string> spectators = null,
	List<CivilizationChoice> civilizations = null, bool creatingGame = false);

// The civilization's name, or null for a random one.
public record ChooseCivilizationInfo(string civilization);

// A spectator's yourPlayerID is null.
public record StartInfo(ID yourPlayerID);

// A host's answer to a discovery broadcast.
public record DiscoveryReply(string hostName, int port, int openSeats, bool started);

// Whose turn it is and how long they have had it, for the scoreboard, and
// which players are at their machines. secondsAllowed is null when turns have
// no time limit.
public record TurnClockInfo(ID activePlayerID, int turn, double secondsElapsed, double? secondsAllowed, List<ID> connectedPlayers);

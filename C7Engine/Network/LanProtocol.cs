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

	// Host to client.
	Lobby = 10,
	Rejected = 11,
	Start = 12,
	Snapshot = 13,
	UiMessage = 14,
}

public static class LanProtocol {
	// Bump when the frames or the messages in them change incompatibly.
	public const int Version = 1;

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

// A human player's place in the game, and who has taken it.
public record SeatInfo(ID playerID, string civilization, string playerName, bool isHost, string takenBy);

public record LobbyInfo(string hostName, List<SeatInfo> seats, ID yourSeat);

public record StartInfo(ID yourPlayerID);

// A host's answer to a discovery broadcast.
public record DiscoveryReply(string hostName, int port, int openSeats, bool started);

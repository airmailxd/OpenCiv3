using System;
using System.Net.Sockets;
using System.Text;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine.Network;

// A connection to a LAN host. The client's engine doesn't run: it sends its
// player's messages to the host, and shows the snapshots and UI messages the
// host sends back.
//
// Frames are handled in Poll(), which the lobby and then the game call every
// frame on the main thread.
public class LanClient : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanClient>();

	private readonly LanConnection connection;

	public string HostAddress { get; }
	public LobbyInfo Lobby { get; private set; }
	public string RejectedReason { get; private set; }

	// The player this client plays, once the game has started.
	public ID PlayerID { get; private set; }

	// The game as the host first sent it, until the game screen takes over.
	public SaveGame StartingGame { get; private set; }

	public bool IsConnected => !connection.IsClosed;

	public event Action LobbyChanged;

	// Set by the game screen. Snapshots are applied before the UI messages
	// that follow them, which refer to the snapshot's units and cities.
	public Action<SaveGame> SnapshotReceived;
	public Action<byte[]> UiMessageReceived;

	private LanClient(LanConnection connection, string hostAddress) {
		this.connection = connection;
		HostAddress = hostAddress;
	}

	public static LanClient Connect(string address, int port, string playerName) {
		TcpClient tcp = new();
		tcp.Connect(address, port);
		LanClient client = new(new LanConnection(tcp), $"{address}:{port}");
		client.connection.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, playerName));
		return client;
	}

	public void ClaimSeat(ID playerID) {
		connection.Send(FrameKind.ClaimSeat, new ClaimSeatInfo(playerID));
	}

	public void SendCommand(MessageToEngine msg) {
		connection.Send(FrameKind.Command, NetSerialization.Serialize(msg));
	}

	public void Poll() {
		while (connection.TryPeek(out Frame frame)) {
			// Once the game has started, wait for the game screen before
			// handling anything more.
			if (StartingGame != null && SnapshotReceived == null) {
				return;
			}
			connection.TryReceive(out frame);
			Handle(frame);
		}
	}

	private void Handle(Frame frame) {
		switch (frame.kind) {
			case FrameKind.Lobby:
				Lobby = NetSerialization.DeserializeData<LobbyInfo>(frame.payload);
				LobbyChanged?.Invoke();
				break;
			case FrameKind.Rejected:
				RejectedReason = Encoding.UTF8.GetString(frame.payload);
				log.Information("The host turned us away: {Reason}", RejectedReason);
				LobbyChanged?.Invoke();
				break;
			case FrameKind.Start:
				PlayerID = NetSerialization.DeserializeData<StartInfo>(frame.payload).yourPlayerID;
				break;
			case FrameKind.Snapshot:
				SaveGame save = LanProtocol.DecodeSnapshot(frame.payload);
				if (SnapshotReceived != null) {
					SnapshotReceived(save);
				} else {
					StartingGame = save;
					LobbyChanged?.Invoke();
				}
				break;
			case FrameKind.UiMessage:
				UiMessageReceived?.Invoke(frame.payload);
				break;
			default:
				log.Warning("Ignoring unexpected {Kind} frame from the host", frame.kind);
				break;
		}
	}

	public void Dispose() {
		connection.Dispose();
	}
}

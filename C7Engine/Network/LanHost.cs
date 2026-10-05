using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine.Network;

// Hosts a LAN game. The host's engine is the only one that runs: each client
// sends its player's messages here, and after the game changes the host sends
// every client a snapshot of the whole game, followed by the UI messages for
// that client's player.
//
// Everything except accepting connections and answering discovery happens in
// Poll(), which the game calls every frame on its main thread.
public class LanHost : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanHost>();

	// Snapshots wait this long after the last change, so that a burst of
	// messages (like a unit moving along a path) is sent as one.
	private static readonly TimeSpan SnapshotDelay = TimeSpan.FromMilliseconds(50);

	// But a steady stream of changes still sends a snapshot this often.
	private static readonly TimeSpan MaxSnapshotDelay = TimeSpan.FromMilliseconds(300);

	private class Seat {
		public SeatInfo info;
		public LanConnection connection;
		public string takenBy;
		public readonly List<byte[]> pendingUiMessages = new();
	}

	private readonly string hostName;
	private readonly List<Seat> seats;
	private readonly ID hostPlayerID;
	private readonly string hostCivilization;
	private readonly TcpListener listener;
	private readonly UdpClient discovery;
	private readonly ConcurrentQueue<TcpClient> accepted = new();

	// Connections that haven't taken a seat yet.
	private readonly List<(LanConnection connection, string name)> unseated = new();

	private long lastProcessedMessageCount = -1;
	private readonly Stopwatch sinceChange = Stopwatch.StartNew();
	private readonly Stopwatch sinceSnapshot = Stopwatch.StartNew();
	private bool snapshotPending;
	private volatile bool disposed;

	public bool Started { get; private set; }
	public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

	// Raised on Poll() when players join, leave, or take seats.
	public event Action LobbyChanged;

	// Seats for the human players other than the host's, in turn order.
	public IReadOnlyList<SeatInfo> Seats => seats.Select(s => s.info with { takenBy = s.takenBy }).ToList();
	public bool AllSeatsTaken => seats.All(s => s.connection != null && !s.connection.IsClosed);

	// The human player the host plays: the first one who isn't defeated.
	public static SavePlayer HostPlayer(SaveGame save) {
		return save.Players.FirstOrDefault(p => p.human && !p.defeated);
	}

	public LanHost(string hostName, SaveGame save, int port = LanProtocol.DefaultPort, bool answerDiscovery = true) {
		this.hostName = hostName;
		SavePlayer host = HostPlayer(save) ?? throw new ArgumentException("The game has no human players");
		hostPlayerID = host.id;
		hostCivilization = host.civilization;
		seats = save.Players
			.Where(p => p.human && !p.defeated && p.id != hostPlayerID)
			.Select(p => new Seat { info = new SeatInfo(p.id, p.civilization, p.name, false, null) })
			.ToList();

		listener = new TcpListener(IPAddress.Any, port);
		listener.Start();
		Thread acceptThread = new(AcceptLoop) { IsBackground = true, Name = "LAN accept" };
		acceptThread.Start();

		if (answerDiscovery) {
			try {
				discovery = new UdpClient(LanProtocol.DiscoveryPort) { EnableBroadcast = true };
				Thread discoveryThread = new(DiscoveryLoop) { IsBackground = true, Name = "LAN discovery" };
				discoveryThread.Start();
			} catch (SocketException e) {
				// Another host on this machine is answering; players can still
				// join by address.
				log.Warning("Not answering LAN discovery: {Error}", e.Message);
			}
		}
		log.Information("Hosting LAN game on port {Port} with {Seats} open seats", Port, seats.Count);
	}

	private void AcceptLoop() {
		while (!disposed) {
			try {
				accepted.Enqueue(listener.AcceptTcpClient());
			} catch (Exception e) when (e is SocketException or ObjectDisposedException or InvalidOperationException) {
				return;
			}
		}
	}

	private void DiscoveryLoop() {
		while (!disposed) {
			try {
				IPEndPoint from = new(IPAddress.Any, 0);
				byte[] request = discovery.Receive(ref from);
				if (Encoding.UTF8.GetString(request) != LanProtocol.DiscoveryRequest) {
					continue;
				}
				int openSeats = seats.Count(s => s.connection == null || s.connection.IsClosed);
				byte[] reply = NetSerialization.SerializeData(new DiscoveryReply(hostName, Port, openSeats, Started));
				discovery.Send(reply, reply.Length, from);
			} catch (Exception e) when (e is SocketException or ObjectDisposedException) {
				if (disposed) return;
			}
		}
	}

	// Starts the game once the host's engine has loaded it: from now on
	// clients' messages go to the engine, and the engine's messages to them.
	public void StartGame() {
		Started = true;
		EngineStorage.uiFollowsActivePlayer = false;
		EngineStorage.uiControllerID = hostPlayerID;
		EngineStorage.activePlayerID = TurnHandling.FirstHumanToPlay(EngineStorage.gameData).id;
		EngineStorage.animationsEnabled = false;
		EngineStorage.uiMessageRouter = RouteMessageToUI;

		byte[] snapshot = LanProtocol.EncodeSnapshot(EngineStorage.gameData);
		foreach (Seat seat in seats.Where(s => s.connection != null)) {
			SendStart(seat, snapshot);
		}
		lastProcessedMessageCount = EngineStorage.processedMessageCount;
	}

	private void SendStart(Seat seat, byte[] snapshot) {
		seat.pendingUiMessages.Clear();
		seat.connection.Send(FrameKind.Start, new StartInfo(seat.info.playerID));
		seat.connection.Send(FrameKind.Snapshot, snapshot);
	}

	public void Poll() {
		while (accepted.TryDequeue(out TcpClient client)) {
			unseated.Add((new LanConnection(client), null));
		}

		foreach ((LanConnection connection, string name) in unseated.ToList()) {
			PollUnseated(connection, name);
		}
		foreach (Seat seat in seats) {
			PollSeat(seat);
		}

		if (Started) {
			MaybeSendSnapshot();
		}
	}

	private void PollUnseated(LanConnection connection, string name) {
		while (connection.TryReceive(out Frame frame)) {
			try {
				switch (frame.kind) {
					case FrameKind.Hello:
						HelloInfo hello = NetSerialization.DeserializeData<HelloInfo>(frame.payload);
						if (hello.version != LanProtocol.Version) {
							Reject(connection, $"The host is running a different version of the game (protocol {LanProtocol.Version}, yours is {hello.version}).");
							return;
						}
						name = string.IsNullOrWhiteSpace(hello.playerName) ? connection.RemoteAddress : hello.playerName.Trim();
						SetUnseatedName(connection, name);
						SendLobby(connection, null);
						break;
					case FrameKind.ClaimSeat:
						if (name == null) {
							Reject(connection, "Say hello before claiming a seat.");
							return;
						}
						ClaimSeatInfo claim = NetSerialization.DeserializeData<ClaimSeatInfo>(frame.payload);
						Seat seat = seats.Find(s => s.info.playerID == claim.playerID);
						if (seat == null || (seat.connection != null && !seat.connection.IsClosed)) {
							SendLobby(connection, null);
							break;
						}
						unseated.RemoveAll(u => u.connection == connection);
						seat.connection?.Dispose();
						seat.connection = connection;
						seat.takenBy = name;
						log.Information("{Name} took the seat of {Player}", name, seat.info.playerID);
						if (Started) {
							// A player rejoining a game in progress.
							SendStart(seat, LanProtocol.EncodeSnapshot(EngineStorage.gameData));
						}
						BroadcastLobby();
						return;
					default:
						log.Warning("Ignoring {Kind} frame from unseated {Address}", frame.kind, connection.RemoteAddress);
						break;
				}
			} catch (JsonException e) {
				log.Warning("Bad {Kind} frame from {Address}: {Error}", frame.kind, connection.RemoteAddress, e.Message);
			}
		}
		if (connection.IsClosed) {
			unseated.RemoveAll(u => u.connection == connection);
		}
	}

	private void SetUnseatedName(LanConnection connection, string name) {
		int index = unseated.FindIndex(u => u.connection == connection);
		if (index >= 0) {
			unseated[index] = (connection, name);
		}
	}

	private void Reject(LanConnection connection, string reason) {
		connection.Send(FrameKind.Rejected, Encoding.UTF8.GetBytes(reason));
		connection.Dispose();
		unseated.RemoveAll(u => u.connection == connection);
	}

	private void PollSeat(Seat seat) {
		if (seat.connection == null) {
			return;
		}
		while (seat.connection.TryReceive(out Frame frame)) {
			if (frame.kind != FrameKind.Command || !Started) {
				continue;
			}
			MessageToEngine msg;
			try {
				msg = NetSerialization.DeserializeMessageToEngine(frame.payload);
			} catch (Exception e) when (e is JsonException or NotSupportedException or FormatException) {
				log.Warning("Bad command from {Player}: {Error}", seat.info.playerID, e.Message);
				continue;
			}
			if (msg == null || msg.IsLocal) {
				continue;
			}
			// Clients act only as their own player.
			msg.playerID = seat.info.playerID;
			msg.DistrustRemoteSender();
			EngineStorage.ReceiveFromRemote(msg);
		}
		if (seat.connection.IsClosed) {
			log.Information("{Name} left the seat of {Player}", seat.takenBy, seat.info.playerID);
			seat.connection = null;
			seat.takenBy = null;
			seat.pendingUiMessages.Clear();
			BroadcastLobby();
		}
	}

	private void RouteMessageToUI(MessageToUI msg) {
		if (msg.IsForEveryone) {
			EngineStorage.SendToLocalUI(msg);
			byte[] json = NetSerialization.Serialize(msg);
			foreach (Seat seat in seats) {
				QueueUiMessage(seat, json);
			}
			return;
		}

		Player to = msg.NetworkRecipient;
		Seat recipientSeat = to == null ? null : seats.Find(s => s.info.playerID == to.id);
		if (recipientSeat == null) {
			EngineStorage.SendToLocalUI(msg);
		} else {
			QueueUiMessage(recipientSeat, NetSerialization.Serialize(msg));
		}
	}

	private void QueueUiMessage(Seat seat, byte[] json) {
		if (seat.connection != null && !seat.connection.IsClosed) {
			seat.pendingUiMessages.Add(json);
			snapshotPending = true;
		}
	}

	// Sends a snapshot once the game has changed and the engine has caught up
	// with its messages, then the UI messages, which refer to the snapshot's
	// units and cities.
	private void MaybeSendSnapshot() {
		if (EngineStorage.processedMessageCount != lastProcessedMessageCount) {
			lastProcessedMessageCount = EngineStorage.processedMessageCount;
			snapshotPending = true;
			sinceChange.Restart();
		}
		bool settled = !EngineStorage.HasPendingMessagesToEngine() && sinceChange.Elapsed >= SnapshotDelay;
		if (!snapshotPending || !(settled || sinceSnapshot.Elapsed >= MaxSnapshotDelay)) {
			return;
		}
		snapshotPending = false;
		sinceSnapshot.Restart();

		List<Seat> connected = seats.Where(s => s.connection != null && !s.connection.IsClosed).ToList();
		if (connected.Count == 0) {
			return;
		}
		byte[] snapshot = LanProtocol.EncodeSnapshot(EngineStorage.gameData);
		foreach (Seat seat in connected) {
			seat.connection.Send(FrameKind.Snapshot, snapshot);
			foreach (byte[] json in seat.pendingUiMessages) {
				seat.connection.Send(FrameKind.UiMessage, json);
			}
			seat.pendingUiMessages.Clear();
		}
	}

	private void SendLobby(LanConnection connection, ID yourSeat) {
		List<SeatInfo> seatInfos = [
			new SeatInfo(hostPlayerID, hostCivilization, hostName, true, hostName),
			.. Seats,
		];
		connection.Send(FrameKind.Lobby, new LobbyInfo(hostName, seatInfos, yourSeat));
	}

	private void BroadcastLobby() {
		foreach ((LanConnection connection, string name) in unseated) {
			if (name != null) {
				SendLobby(connection, null);
			}
		}
		foreach (Seat seat in seats.Where(s => s.connection != null)) {
			SendLobby(seat.connection, seat.info.playerID);
		}
		LobbyChanged?.Invoke();
	}

	public void Dispose() {
		if (disposed) {
			return;
		}
		disposed = true;
		listener.Stop();
		discovery?.Dispose();
		foreach ((LanConnection connection, _) in unseated) {
			connection.Dispose();
		}
		foreach (Seat seat in seats) {
			seat.connection?.Dispose();
		}
		if (Started) {
			EngineStorage.ResetNetworking();
		}
	}
}

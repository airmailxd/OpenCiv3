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
using System.Threading.Tasks;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine.Network;

// Hosts a LAN game. The host's engine is the only one that runs: each client
// sends its player's messages here, and after the game changes the host sends
// every client a snapshot of the whole game, followed by the UI messages for
// that client's player. Spectators get the snapshots and the messages for
// everyone, and play no part.
//
// A host can also seat guests before its game exists, for a new game whose
// guests choose their own civilizations: the host creates the game once
// everyone has chosen, then starts it as usual.
//
// Everything except accepting connections, answering discovery, encoding
// snapshots and writing to the network happens in Poll(), which the game
// calls every frame on its main thread.
//
// A snapshot is taken from the game on the main thread, but encoded on a
// worker thread, and each connection writes it when it's ready, in its place
// among that connection's frames. While one is being encoded, changes wait
// for the next, so a burst of changes is sent as one snapshot of the latest
// game rather than a queue of stale ones; and a snapshot identical to the
// last one a connection was sent isn't sent again.
public class LanHost : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanHost>();

	// Snapshots wait this long after the last change, so that a burst of
	// messages (like a unit moving along a path) is sent as one.
	private static readonly TimeSpan SnapshotDelay = TimeSpan.FromMilliseconds(50);

	// But a steady stream of changes still sends a snapshot this often.
	private static readonly TimeSpan MaxSnapshotDelay = TimeSpan.FromMilliseconds(300);

	// Spectators don't need every move, and redrawing the whole game for
	// each snapshot is slow, so they get one this often at most.
	private static readonly TimeSpan SpectatorSnapshotInterval = TimeSpan.FromSeconds(1);

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

	// For a new game not created yet, the civilizations guests can choose
	// from; null once the game exists.
	private List<Civilization> choosable;
	private bool creatingGame;
	private readonly TcpListener listener;
	private readonly UdpClient discovery;
	private readonly ConcurrentQueue<TcpClient> accepted = new();

	// Connections that haven't taken a seat yet.
	private readonly List<(LanConnection connection, string name)> unseated = new();

	// Connections watching the game rather than playing in it.
	private class Spectator {
		public LanConnection connection;
		public string name;
		public readonly List<byte[]> pendingUiMessages = new();
	}
	private readonly List<Spectator> spectators = new();

	// The last snapshot handed to be encoded. Each is encoded after the one
	// before, so it can reuse that one's encoding when nothing changed.
	private Task<EncodedSnapshot> lastEncoding;

	private long lastProcessedMessageCount = -1;
	private readonly Stopwatch sinceChange = Stopwatch.StartNew();
	private readonly Stopwatch sinceSnapshot = Stopwatch.StartNew();
	private bool snapshotPending;
	private readonly Stopwatch sinceSpectatorSnapshot = Stopwatch.StartNew();
	private bool spectatorSnapshotPending;
	private volatile bool disposed;

	// How long each human has to play their turn before the host ends it for
	// them; null for no limit.
	public TimeSpan? TurnTimeLimit { get; set; }

	// The turn being timed: whose it is, and since when.
	private ID clockPlayerID;
	private int clockTurn = -1;
	private readonly Stopwatch turnClock = new();
	private bool turnTimedOut;

	public bool Started { get; private set; }
	public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

	// Raised on Poll() when players join, leave, or take seats.
	public event Action LobbyChanged;

	// Seats for the human players other than the host's, in turn order.
	public IReadOnlyList<SeatInfo> Seats => seats.Select(s => s.info with { takenBy = s.takenBy }).ToList();
	public bool AllSeatsTaken => seats.All(s => s.connection != null && !s.connection.IsClosed);
	public IReadOnlyList<string> Spectators => spectators.Select(s => s.name).ToList();
	public string HostCivilization => hostCivilization;

	// Whether guests are choosing their civilizations for a game the host
	// hasn't created yet.
	public bool GuestsChooseCivilizations => choosable != null;

	// The human player the host plays: the first one who isn't defeated.
	public static SavePlayer HostPlayer(SaveGame save) {
		return save.Players.FirstOrDefault(p => p.human && !p.defeated);
	}

	// Hosts a game that already exists, such as a saved one.
	public LanHost(string hostName, SaveGame save, int port = LanProtocol.DefaultPort, bool answerDiscovery = true)
		: this(hostName, RequireHostPlayer(save).id, RequireHostPlayer(save).civilization, SeatsFor(save), null, port, answerDiscovery) {
	}

	// Hosts a new game whose guests choose their civilizations: one seat for
	// each guest, with the IDs GameSetup will give them, and the host playing
	// hostCivilization. Call BeginCreatingGame and GameCreated to create it.
	public LanHost(string hostName, string hostCivilization, int guestSeats, IEnumerable<Civilization> playable,
		int port = LanProtocol.DefaultPort, bool answerDiscovery = true)
		: this(hostName, GameSetup.HumanPlayerIDs(guestSeats + 1)[0], hostCivilization,
			GameSetup.HumanPlayerIDs(guestSeats + 1).Skip(1)
				.Select(id => new Seat { info = new SeatInfo(id, null, null, false, null) })
				.ToList(),
			playable.Where(c => !c.isBarbarian).ToList(), port, answerDiscovery) {
	}

	private static SavePlayer RequireHostPlayer(SaveGame save) {
		return HostPlayer(save) ?? throw new ArgumentException("The game has no human players");
	}

	private static List<Seat> SeatsFor(SaveGame save) {
		ID hostPlayerID = RequireHostPlayer(save).id;
		return save.Players
			.Where(p => p.human && !p.defeated && p.id != hostPlayerID)
			.Select(p => new Seat { info = new SeatInfo(p.id, p.civilization, p.name, false, null) })
			.ToList();
	}

	private LanHost(string hostName, ID hostPlayerID, string hostCivilization, List<Seat> seats,
		List<Civilization> choosable, int port, bool answerDiscovery) {
		this.hostName = hostName;
		this.hostPlayerID = hostPlayerID;
		this.hostCivilization = hostCivilization;
		this.seats = seats;
		this.choosable = choosable;

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

	// Takes a snapshot of the game as it stands, and encodes it on a worker
	// thread.
	private Task<EncodedSnapshot> EncodeSnapshot() {
		SaveGame snapshot = LanProtocol.SnapshotOf(EngineStorage.gameData);
		Task<EncodedSnapshot> previous = lastEncoding;
		lastEncoding = Task.Run(async () => {
			EncodedSnapshot before = null;
			if (previous != null) {
				try {
					before = await previous;
				} catch (Exception) {
					// The connections waiting on it have logged why.
				}
			}
			return LanProtocol.EncodeSnapshot(snapshot, before);
		});
		return lastEncoding;
	}

	private bool EncodingSnapshot => lastEncoding != null && !lastEncoding.IsCompleted;

	// Starts the game once the host's engine has loaded it: from now on
	// clients' messages go to the engine, and the engine's messages to them.
	public void StartGame() {
		if (GuestsChooseCivilizations) {
			throw new InvalidOperationException("Create the game before starting it");
		}
		Started = true;
		EngineStorage.uiFollowsActivePlayer = false;
		EngineStorage.uiControllerID = hostPlayerID;
		EngineStorage.activePlayerID = TurnHandling.FirstHumanToPlay(EngineStorage.gameData).id;
		EngineStorage.animationsEnabled = false;
		EngineStorage.uiMessageRouter = RouteMessageToUI;

		Task<EncodedSnapshot> snapshot = EncodeSnapshot();
		foreach (Seat seat in seats.Where(s => s.connection != null)) {
			SendStart(seat, snapshot);
		}
		foreach (Spectator spectator in spectators) {
			SendStart(spectator, snapshot);
		}
		lastProcessedMessageCount = EngineStorage.processedMessageCount;
	}

	private static void SendStart(Seat seat, Task<EncodedSnapshot> snapshot) {
		seat.pendingUiMessages.Clear();
		seat.connection.Send(FrameKind.Start, new StartInfo(seat.info.playerID));
		seat.connection.SendSnapshot(snapshot);
	}

	private static void SendStart(Spectator spectator, Task<EncodedSnapshot> snapshot) {
		spectator.pendingUiMessages.Clear();
		spectator.connection.Send(FrameKind.Start, new StartInfo(null));
		spectator.connection.SendSnapshot(snapshot);
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
		foreach (Spectator spectator in spectators.ToList()) {
			PollSpectator(spectator);
		}

		if (Started) {
			UpdateTurnClock();
			MaybeSendSnapshot();
		}
	}

	// Restarts the clock when a new turn begins, and ends a human's turn for
	// them once they have run out of time.
	private void UpdateTurnClock() {
		GameData gameData = EngineStorage.gameData;
		ID active = EngineStorage.activePlayerID;
		if (active != clockPlayerID || gameData.turn != clockTurn) {
			clockPlayerID = active;
			clockTurn = gameData.turn;
			turnClock.Restart();
			turnTimedOut = false;
			BroadcastTurnClock();
		}

		if (TurnTimeLimit is not TimeSpan limit || turnTimedOut || turnClock.Elapsed < limit) {
			return;
		}
		Player player = gameData.GetPlayer(active);
		if (player != null && player.isHuman && !player.hasPlayedThisTurn) {
			turnTimedOut = true;
			log.Information("{Player} ran out of time, ending their turn", player);
			EngineStorage.ReceiveFromRemote(new MsgEndTurn { playerID = active });
		}
	}

	// The clock as it stands now, or null before the game starts.
	public TurnClockInfo CurrentClock() {
		if (!Started || clockPlayerID == null) {
			return null;
		}
		return new TurnClockInfo(clockPlayerID, clockTurn, turnClock.Elapsed.TotalSeconds,
			TurnTimeLimit?.TotalSeconds, ConnectedPlayers());
	}

	private List<ID> ConnectedPlayers() {
		return [
			hostPlayerID,
			.. seats.Where(s => s.connection != null && !s.connection.IsClosed).Select(s => s.info.playerID),
		];
	}

	private void BroadcastTurnClock() {
		TurnClockInfo clock = CurrentClock();
		if (clock == null) {
			return;
		}
		foreach (Seat seat in seats.Where(s => s.connection != null && !s.connection.IsClosed)) {
			seat.connection.Send(FrameKind.TurnClock, clock);
		}
		foreach (Spectator spectator in spectators.Where(s => !s.connection.IsClosed)) {
			spectator.connection.Send(FrameKind.TurnClock, clock);
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
							SendStart(seat, EncodeSnapshot());
						}
						BroadcastLobby();
						return;
					case FrameKind.Watch:
						if (name == null) {
							Reject(connection, "Say hello before watching.");
							return;
						}
						unseated.RemoveAll(u => u.connection == connection);
						Spectator spectator = new() { connection = connection, name = name };
						spectators.Add(spectator);
						log.Information("{Name} is watching", name);
						if (Started) {
							SendStart(spectator, EncodeSnapshot());
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
			if (frame.kind == FrameKind.ChooseCivilization) {
				ChooseCivilization(seat, frame);
				continue;
			}
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
			if (GuestsChooseCivilizations && !creatingGame) {
				// Whoever takes the seat next chooses afresh.
				seat.info = seat.info with { civilization = null };
			}
			BroadcastLobby();
		}
	}

	// A guest's choice of civilization: one nobody else has chosen, or null
	// for a random one. Anything else leaves their seat as it was.
	private void ChooseCivilization(Seat seat, Frame frame) {
		if (!GuestsChooseCivilizations || creatingGame) {
			return;
		}
		string civilization;
		try {
			civilization = NetSerialization.DeserializeData<ChooseCivilizationInfo>(frame.payload).civilization;
		} catch (JsonException e) {
			log.Warning("Bad civilization choice from {Player}: {Error}", seat.info.playerID, e.Message);
			return;
		}
		bool available = civilization == null
			|| (choosable.Any(c => c.name == civilization)
				&& civilization != hostCivilization
				&& !seats.Any(s => s != seat && s.info.civilization == civilization));
		if (available) {
			seat.info = seat.info with { civilization = civilization };
			log.Information("{Name} chose {Civilization}", seat.takenBy, civilization ?? "a random civilization");
		}
		// Either way, everyone hears how things stand.
		BroadcastLobby();
	}

	// Closes the guests' choices, and returns the guests as GameSetup's
	// hotseatPlayers: each with the civilization they chose (null for a
	// random one) and their name.
	public List<HotseatPlayer> BeginCreatingGame() {
		if (!GuestsChooseCivilizations) {
			throw new InvalidOperationException("The game already exists");
		}
		creatingGame = true;
		BroadcastLobby();
		return seats.Select(s => new HotseatPlayer {
			civilization = choosable.Find(c => c.name == s.info.civilization),
			name = s.takenBy,
		}).ToList();
	}

	// Reopens the choices if creating the game failed.
	public void CancelCreatingGame() {
		creatingGame = false;
		BroadcastLobby();
	}

	// Takes the game GameSetup created from BeginCreatingGame's players, and
	// fixes each seat to its player.
	public void GameCreated(SaveGame save) {
		if (RequireHostPlayer(save).id != hostPlayerID) {
			throw new InvalidOperationException("The host's player isn't the game's first human player");
		}
		foreach (Seat seat in seats) {
			SavePlayer player = save.Players.Find(p => p.id == seat.info.playerID);
			if (player == null || !player.human) {
				throw new InvalidOperationException($"The game has no human player for the seat {seat.info.playerID}");
			}
			seat.info = seat.info with { civilization = player.civilization, playerName = player.name };
		}
		choosable = null;
		creatingGame = false;
		BroadcastLobby();
	}

	// Spectators only listen: whatever they send is dropped.
	private void PollSpectator(Spectator spectator) {
		while (spectator.connection.TryReceive(out _)) { }
		if (spectator.connection.IsClosed) {
			log.Information("{Name} stopped watching", spectator.name);
			spectators.Remove(spectator);
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
			foreach (Spectator spectator in spectators.Where(s => !s.connection.IsClosed)) {
				spectator.pendingUiMessages.Add(json);
				spectatorSnapshotPending = true;
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
	// units and cities. While the last snapshot is still being encoded, the
	// next one waits for it, taking in the changes made meanwhile.
	private void MaybeSendSnapshot() {
		if (EngineStorage.processedMessageCount != lastProcessedMessageCount) {
			lastProcessedMessageCount = EngineStorage.processedMessageCount;
			snapshotPending = true;
			spectatorSnapshotPending = true;
			sinceChange.Restart();
		}
		if (EncodingSnapshot) {
			return;
		}
		bool settled = !EngineStorage.HasPendingMessagesToEngine() && sinceChange.Elapsed >= SnapshotDelay;
		Task<EncodedSnapshot> snapshot = null;

		if (snapshotPending && (settled || sinceSnapshot.Elapsed >= MaxSnapshotDelay)) {
			snapshotPending = false;
			sinceSnapshot.Restart();
			List<Seat> connected = seats.Where(s => s.connection != null && !s.connection.IsClosed).ToList();
			if (connected.Count > 0) {
				snapshot = EncodeSnapshot();
				foreach (Seat seat in connected) {
					SendSnapshot(seat.connection, snapshot, seat.pendingUiMessages);
				}
			}
		}

		if (spectatorSnapshotPending && sinceSpectatorSnapshot.Elapsed >= SpectatorSnapshotInterval) {
			spectatorSnapshotPending = false;
			sinceSpectatorSnapshot.Restart();
			List<Spectator> watching = spectators.Where(s => !s.connection.IsClosed).ToList();
			if (watching.Count > 0) {
				snapshot ??= EncodeSnapshot();
				foreach (Spectator spectator in watching) {
					SendSnapshot(spectator.connection, snapshot, spectator.pendingUiMessages);
				}
			}
		}
	}

	private static void SendSnapshot(LanConnection connection, Task<EncodedSnapshot> snapshot, List<byte[]> pendingUiMessages) {
		connection.SendSnapshot(snapshot);
		foreach (byte[] json in pendingUiMessages) {
			connection.Send(FrameKind.UiMessage, json);
		}
		pendingUiMessages.Clear();
	}

	private void SendLobby(LanConnection connection, ID yourSeat) {
		List<SeatInfo> seatInfos = [
			new SeatInfo(hostPlayerID, hostCivilization, hostName, true, hostName),
			.. Seats,
		];
		List<CivilizationChoice> civilizations = choosable?.Select(c => new CivilizationChoice(
			c.name, c.leader, c.noun, c.leaderArtFile, c.traits.Select(t => t.ToString()).ToList())).ToList();
		connection.Send(FrameKind.Lobby, new LobbyInfo(hostName, seatInfos, yourSeat, [.. Spectators], civilizations, creatingGame));
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
		foreach (Spectator spectator in spectators) {
			SendLobby(spectator.connection, null);
		}
		// Who is connected has changed, and anyone who just came in needs
		// the clock.
		BroadcastTurnClock();
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
		foreach (Spectator spectator in spectators) {
			spectator.connection.Dispose();
		}
		if (Started) {
			EngineStorage.ResetNetworking();
		}
	}
}

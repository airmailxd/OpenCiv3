using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine.Network;

// Hosts a LAN game. The host's engine is the only one that runs: each client
// sends its player's messages here, and after the game changes the host sends
// every client a snapshot of the whole game, followed by the UI messages for
// that client's players. Spectators get the snapshots and the messages for
// everyone, and play no part.
//
// A guest may take more than one seat, when several people take turns at
// their machine as in a hotseat game; it then plays each of those players.
//
// A host can also seat guests before its game exists, for a new game whose
// guests choose their own civilizations: the host creates the game once
// everyone has chosen, then starts it as usual.
//
// Once the game has started, a guest who loses their connection keeps their
// seats until they say hello again with the token they were given. The host
// saves the game as each turn begins, with those tokens, and can resume it
// from that save, holding the seats for the guests to come back to.
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

	// Messages for a disconnected seat's player are kept for them until the
	// turn is over, but no more than this many.
	private const int MaxHeldUiMessages = 200;

	private class Seat {
		public SeatInfo info;
		// The guest playing this seat, and the name of the player in it.
		public Guest guest;
		public string takenBy;
		// The token of the guest who took the seat. Once the game is under
		// way, a guest who loses their connection keeps their seats, and
		// saying hello with this token has them back.
		public string token;
		// The host chose to go on without the player, so their turns end
		// by themselves until someone is in the seat again.
		public bool away;
		// The messages for the player while they're disconnected, and the
		// turn they're from.
		public readonly List<byte[]> heldUiMessages = new();
		public int heldTurn = -1;
		public bool IsTaken => guest != null && !guest.connection.IsClosed;
		// Kept for a guest who has lost their connection.
		public bool IsHeld => !IsTaken && token != null;
	}

	// A machine that has joined to play, with as many seats as it has taken
	// (none at first).
	private class Guest {
		public LanConnection connection;
		// Null until it says hello.
		public string name;
		// Given to the guest when it says hello, or the one it says hello
		// with when it comes back to its seats.
		public string token;
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

	// Everyone joined to play, whether or not they have taken a seat yet.
	private readonly List<Guest> guests = new();

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

	// What discovery answers, published whole for the discovery thread.
	private volatile DiscoveryReply discoveryReply;

	private long lastProcessedMessageCount = -1;
	private readonly Stopwatch sinceChange = Stopwatch.StartNew();
	private readonly Stopwatch sinceSnapshot = Stopwatch.StartNew();
	private bool snapshotPending;
	private readonly Stopwatch sinceSpectatorSnapshot = Stopwatch.StartNew();
	private bool spectatorSnapshotPending;
	private volatile bool disposed;

	// How long each human has to play their turn before the host ends it for
	// them; null for no limit. With simultaneous turns, it is how long the
	// humans have for the round, all at once.
	public TimeSpan? TurnTimeLimit { get; set; }

	// Whether the humans play their turns at the same time rather than one
	// after another, which the game takes on when it starts.
	public bool SimultaneousTurns {
		get => simultaneousTurns;
		set {
			if (simultaneousTurns != value) {
				simultaneousTurns = value;
				BroadcastLobby();
			}
		}
	}
	private bool simultaneousTurns;

	// The turn being timed: whose it is, who is yet to finish it, and since
	// when.
	private ID clockPlayerID;
	private List<ID> clockPlayersToMove = [];
	private int clockTurn = -1;
	private readonly Stopwatch turnClock = new();
	private bool turnTimedOut;

	// The turn each away player's turn was last ended for them.
	private readonly Dictionary<ID, int> awayTurnsEnded = new();

	// A game resumed from an autosave holds its guests' seats for them from
	// the lobby on, not just once it has started.
	private bool resumed;

	// Where the game is saved as each turn begins, so that the host can
	// resume it if their game ends; null not to save it.
	public string AutosaveDirectory { get; set; }
	private int autosavedTurn = -1;

	// The last autosave, written on a worker thread after the one before.
	internal Task LastAutosave { get; private set; } = Task.CompletedTask;

	public bool Started { get; private set; }

	// Whether a guest who loses their connection keeps their seats.
	private bool HoldsSeats => Started || resumed;
	public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

	// Raised on Poll() when players join, leave, or take seats.
	public event Action LobbyChanged;

	// Seats for the human players other than the host's, in turn order.
	public IReadOnlyList<SeatInfo> Seats =>
		seats.Select(s => s.info with { takenBy = s.takenBy, disconnected = s.IsHeld, away = s.away }).ToList();
	public bool AllSeatsTaken => seats.All(s => s.IsTaken);

	// Whether every seat is taken or held for a guest who lost their
	// connection, as in a resumed game whose guests aren't all back yet.
	public bool AllSeatsTakenOrHeld => seats.All(s => s.IsTaken || s.IsHeld);
	public IReadOnlyList<string> Spectators => spectators.Select(s => s.name).ToList();
	public string HostCivilization => hostCivilization;
	public ID HostPlayerID => hostPlayerID;

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

	// Hosts a game again from its autosave, with the settings it had, and
	// with each guest's seats held for them until they come back with their
	// token. The host starts it from the lobby as usual.
	public static LanHost Resume(string hostName, SaveGame save, LanResumeInfo info, int? port = null, bool answerDiscovery = true) {
		LanHost host = new(hostName, save, port ?? info.port, answerDiscovery) {
			TurnTimeLimit = info.turnSeconds is double seconds ? TimeSpan.FromSeconds(seconds) : null,
			SimultaneousTurns = info.simultaneousTurns,
			resumed = true,
		};
		foreach (LanResumeSeat saved in info.seats ?? []) {
			Seat seat = host.seats.Find(s => s.info.playerID == saved.playerID);
			if (seat == null || string.IsNullOrEmpty(saved.reconnectToken)) {
				continue;
			}
			seat.token = saved.reconnectToken;
			seat.takenBy = saved.playerName;
		}
		host.PublishDiscoveryReply();
		return host;
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

		PublishDiscoveryReply();
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
				// The seats belong to the main thread, which publishes what
				// to answer as they change.
				byte[] reply = NetSerialization.SerializeData(discoveryReply);
				discovery.Send(reply, reply.Length, from);
			} catch (Exception e) when (e is SocketException or ObjectDisposedException) {
				if (disposed) return;
			} catch (Exception e) {
				// Anything else would take the whole program down with this
				// thread.
				if (disposed) return;
				log.Error(e, "Couldn't answer a LAN discovery request");
			}
		}
	}

	// Called on the main thread whenever the seats or the game's state may
	// have changed.
	private void PublishDiscoveryReply() {
		int openSeats = seats.Count(s => !s.IsTaken && !s.IsHeld);
		DiscoveryReply current = discoveryReply;
		if (current == null || current.openSeats != openSeats || current.started != Started) {
			discoveryReply = new DiscoveryReply(hostName, Port, openSeats, Started);
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
		EngineStorage.gameData.simultaneousTurns = SimultaneousTurns;
		EngineStorage.uiFollowsActivePlayer = false;
		EngineStorage.uiControllerID = hostPlayerID;
		EngineStorage.activePlayerID = TurnHandling.FirstHumanToPlay(EngineStorage.gameData).id;
		EngineStorage.animationsEnabled = false;
		EngineStorage.uiMessageRouter = RouteMessageToUI;
		EngineStorage.playerReachable = IsReachable;

		Task<EncodedSnapshot> snapshot = EncodeSnapshot();
		foreach (Guest guest in SeatedGuests()) {
			SendStart(guest, snapshot);
		}
		foreach (Spectator spectator in spectators) {
			SendStart(spectator, snapshot);
		}
		lastProcessedMessageCount = EngineStorage.processedMessageCount;
		PublishDiscoveryReply();
	}

	private IEnumerable<Seat> SeatsOf(Guest guest) => seats.Where(s => s.guest == guest);

	// The guests with a seat, whose connections are still open.
	private List<Guest> SeatedGuests() {
		return guests.Where(g => !g.connection.IsClosed && SeatsOf(g).Any()).ToList();
	}

	// Tells a guest which players are theirs, now or after taking another
	// seat in a game in progress, and shows them the game.
	private void SendStart(Guest guest, Task<EncodedSnapshot> snapshot) {
		guest.connection.Send(FrameKind.Start, new StartInfo(SeatsOf(guest).Select(s => s.info.playerID).ToList(), guest.token));
		SendSnapshot(guest.connection, snapshot, guest.pendingUiMessages);
	}

	// The engine asks a player some things only as their turn starts, and
	// those prompts went with their old connection, so a player rejoining
	// during their turn is asked again.
	private static void ResendTurnPrompts(Seat seat) {
		GameData gameData = EngineStorage.gameData;
		Player player = gameData?.GetPlayer(seat.info.playerID);
		if (player == null || !TurnHandling.IsPlayersTurn(gameData, player.id)) {
			return;
		}
		UnitedNations.AskHumanToVote(gameData, player);
	}

	private static void SendStart(Spectator spectator, Task<EncodedSnapshot> snapshot) {
		spectator.pendingUiMessages.Clear();
		spectator.connection.Send(FrameKind.Start, new StartInfo([]));
		spectator.connection.SendSnapshot(snapshot);
	}

	public void Poll() {
		while (accepted.TryDequeue(out TcpClient client)) {
			guests.Add(new Guest { connection = new LanConnection(client) });
		}

		foreach (Guest guest in guests.ToList()) {
			PollGuest(guest);
		}
		foreach (Spectator spectator in spectators.ToList()) {
			PollSpectator(spectator);
		}

		if (Started) {
			UpdateTurnClock();
			MaybeSendSnapshot();
		}
		PublishDiscoveryReply();
	}

	// Restarts the clock when a new turn begins, and ends the humans' turns
	// for them once they have run out of time. With simultaneous turns, one
	// clock times the whole round, from when the humans start moving.
	private void UpdateTurnClock() {
		GameData gameData = EngineStorage.gameData;
		ID active = EngineStorage.activePlayerID;
		List<ID> toMove = TurnHandling.PlayersToMove(gameData).Select(p => p.id).ToList();
		bool newTurn = gameData.turn != clockTurn || (!gameData.simultaneousTurns && active != clockPlayerID);
		if (newTurn) {
			clockTurn = gameData.turn;
			turnClock.Restart();
			turnTimedOut = false;
		}
		// Everyone hears when someone finishes, too.
		if (newTurn || active != clockPlayerID || !toMove.SequenceEqual(clockPlayersToMove)) {
			clockPlayerID = active;
			clockPlayersToMove = toMove;
			BroadcastTurnClock();
		}

		EndAwayPlayersTurns(gameData, toMove);

		// A new turn's humans are about to move: save it.
		if (toMove.Count > 0 && gameData.turn != autosavedTurn && AutosaveDirectory != null) {
			autosavedTurn = gameData.turn;
			Autosave();
		}

		if (TurnTimeLimit is not TimeSpan limit || turnTimedOut || turnClock.Elapsed < limit || toMove.Count == 0) {
			return;
		}
		turnTimedOut = true;
		foreach (ID id in toMove) {
			log.Information("{Player} ran out of time, ending their turn", gameData.GetPlayer(id));
			EngineStorage.ReceiveFromRemote(new MsgEndTurn { playerID = id, turn = gameData.turn });
		}
	}

	// Ends the turn of each away player still to move, once a turn, the way
	// running out of time does.
	private void EndAwayPlayersTurns(GameData gameData, List<ID> toMove) {
		foreach (ID id in toMove) {
			Seat seat = seats.Find(s => s.info.playerID == id);
			if (seat == null || !seat.away || seat.IsTaken
				|| (awayTurnsEnded.TryGetValue(id, out int ended) && ended == gameData.turn)) {
				continue;
			}
			awayTurnsEnded[id] = gameData.turn;
			log.Information("{Player} is away, ending their turn", gameData.GetPlayer(id));
			EngineStorage.ReceiveFromRemote(new MsgEndTurn { playerID = id, turn = gameData.turn });
		}
	}

	// Saves the game and what resuming it needs. The game is taken as it
	// stands, and written on a worker thread.
	private void Autosave() {
		SaveGame save = LanProtocol.SnapshotOf(EngineStorage.gameData);
		LanResumeInfo info = ResumeInfo();
		string directory = AutosaveDirectory;
		LastAutosave = LastAutosave.ContinueWith(_ => LanAutosave.Write(directory, save, info));
	}

	// How to host this game again, with the seats as they are now.
	public LanResumeInfo ResumeInfo() {
		return new LanResumeInfo(hostName, Port, TurnTimeLimit?.TotalSeconds, SimultaneousTurns,
			seats.Where(s => s.token != null).Select(s => new LanResumeSeat(s.info.playerID, s.takenBy, s.token)).ToList());
	}

	// The players with nobody at their machine whom the game would wait on:
	// their guests lost the connection, or nobody has taken their seats, and
	// the host hasn't chosen to go on without them.
	public IReadOnlyList<ID> AbsentPlayers => seats.Where(s => !s.IsTaken && !s.away).Select(s => s.info.playerID).ToList();

	// Goes on without the absent players: from now on their turns end by
	// themselves, until a guest is in their seat again.
	public void ContinueWithoutAbsentPlayers() {
		foreach (Seat seat in seats.Where(s => !s.IsTaken && !s.away)) {
			log.Information("Going on without {Player}", seat.takenBy ?? seat.info.playerName ?? seat.info.playerID.ToString());
			seat.away = true;
		}
		BroadcastLobby();
	}

	// Gives up the seat held for a guest who lost their connection, so that
	// anyone can take it. A seat the game went on without stays away.
	public bool ReleaseSeat(ID playerID) {
		Seat seat = seats.Find(s => s.info.playerID == playerID);
		if (seat == null || !seat.IsHeld) {
			return false;
		}
		log.Information("Releasing the seat of {Player}, held for {Name}", playerID, seat.takenBy);
		FreeSeat(seat);
		BroadcastLobby();
		return true;
	}

	// The clock as it stands now, or null before the game starts.
	public TurnClockInfo CurrentClock() {
		if (!Started || clockPlayerID == null) {
			return null;
		}
		return new TurnClockInfo(clockPlayerID, clockTurn, turnClock.Elapsed.TotalSeconds,
			TurnTimeLimit?.TotalSeconds, ConnectedPlayers(), clockPlayersToMove,
			seats.Where(s => s.away).Select(s => s.info.playerID).ToList());
	}

	private List<ID> ConnectedPlayers() {
		return [
			hostPlayerID,
			.. seats.Where(s => s.IsTaken).Select(s => s.info.playerID),
		];
	}

	private void BroadcastTurnClock() {
		TurnClockInfo clock = CurrentClock();
		if (clock == null) {
			return;
		}
		foreach (Guest guest in SeatedGuests()) {
			guest.connection.Send(FrameKind.TurnClock, clock);
		}
		foreach (Spectator spectator in spectators.Where(s => !s.connection.IsClosed)) {
			spectator.connection.Send(FrameKind.TurnClock, clock);
		}
	}

	// A frame that can't be read or handled, from anyone, is logged and
	// ignored, and a peer that sends nothing else is dropped, so that a bad
	// frame never takes the host's Poll (and everyone else's frames, and the
	// engine) down with it.
	private static bool BadFrame(LanConnection connection, string from, Frame frame, Exception e) {
		log.Warning("Bad {Kind} frame from {From}: {Error}", frame.kind, from, e.Message);
		return connection.NoteBadFrame();
	}

	private void PollGuest(Guest guest) {
		while (guest.connection.TryReceive(out Frame frame)) {
			try {
				if (!HandleGuestFrame(guest, frame)) {
					// Turned away, or watching from now on.
					return;
				}
				guest.connection.NoteGoodFrame();
			} catch (Exception e) {
				if (BadFrame(guest.connection, guest.name ?? guest.connection.RemoteAddress, frame, e)) {
					break;
				}
				if (!guests.Contains(guest)) {
					return;
				}
			}
		}
		if (guest.connection.IsClosed) {
			DropGuest(guest);
		}
	}

	// Handles a frame from a guest, and returns whether they are still one.
	private bool HandleGuestFrame(Guest guest, Frame frame) {
		switch (frame.kind) {
			case FrameKind.Hello:
				HelloInfo hello = NetSerialization.DeserializeRequired<HelloInfo>(frame.payload);
				if (hello.version != LanProtocol.Version) {
					Reject(guest, $"The host is running a different version of the game (protocol {LanProtocol.Version}, yours is {hello.version}).");
					return false;
				}
				guest.name = string.IsNullOrWhiteSpace(hello.playerName) ? guest.connection.RemoteAddress : hello.playerName.Trim();
				if (hello.reconnectToken != null) {
					if (!Reattach(guest, hello.reconnectToken)) {
						Reject(guest, "The host is no longer keeping a seat for you.");
						return false;
					}
					return true;
				}
				guest.token = NewToken();
				SendLobby(guest);
				return true;
			case FrameKind.ClaimSeat:
				if (guest.name == null) {
					Reject(guest, "Say hello before claiming a seat.");
					return false;
				}
				ClaimSeat(guest, NetSerialization.DeserializeRequired<ClaimSeatInfo>(frame.payload));
				return true;
			case FrameKind.LeaveSeat:
				LeaveSeat(guest, NetSerialization.DeserializeRequired<ClaimSeatInfo>(frame.payload));
				return true;
			case FrameKind.Watch:
				if (guest.name == null) {
					Reject(guest, "Say hello before watching.");
					return false;
				}
				if (SeatsOf(guest).Any()) {
					log.Warning("Ignoring a request to watch from {Name}, who has a seat", guest.name);
					return true;
				}
				guests.Remove(guest);
				Spectator spectator = new() { connection = guest.connection, name = guest.name };
				spectators.Add(spectator);
				log.Information("{Name} is watching", guest.name);
				if (Started) {
					SendStart(spectator, EncodeSnapshot());
				}
				BroadcastLobby();
				return false;
			case FrameKind.ChooseCivilization:
				ChooseCivilization(guest, frame);
				return true;
			case FrameKind.Command:
				HandleCommand(guest, frame);
				return true;
			default:
				log.Warning("Ignoring {Kind} frame from {Address}", frame.kind, guest.connection.RemoteAddress);
				return true;
		}
	}

	// A guest takes an open seat, in addition to any they have already, or
	// renames the player in one of theirs before the game starts.
	private void ClaimSeat(Guest guest, ClaimSeatInfo claim) {
		Seat seat = seats.Find(s => s.info.playerID == claim.playerID);
		if (seat != null && seat.guest == guest && seat.IsTaken) {
			if (!Started && !creatingGame) {
				seat.takenBy = SeatPlayerName(guest, claim.playerName);
			}
			BroadcastLobby();
			return;
		}
		if (seat != null && !seat.IsTaken && seat.guest != null && !HoldsSeats) {
			// The last guest closed without PollGuest seeing it go.
			FreeSeat(seat);
		}
		if (seat == null || seat.IsTaken || seat.IsHeld) {
			SendLobby(guest);
			return;
		}
		seat.guest = guest;
		seat.token = guest.token;
		seat.away = false;
		seat.takenBy = SeatPlayerName(guest, claim.playerName);
		log.Information("{Name} took the seat of {Player} for {SeatName}", guest.name, seat.info.playerID, seat.takenBy);
		if (Started) {
			// A player rejoining a game in progress.
			SendStart(guest, EncodeSnapshot());
			ResendTurnPrompts(seat);
		}
		BroadcastLobby();
	}

	// The name of the player in a seat a guest takes: the one given, or the
	// guest's own.
	private static string SeatPlayerName(Guest guest, string playerName) {
		if (string.IsNullOrWhiteSpace(playerName)) {
			return guest.name;
		}
		playerName = playerName.Trim();
		return playerName.Length > 40 ? playerName[..40] : playerName;
	}

	// A guest gives back a seat before the game starts. Once it has, they
	// leave a seat only by leaving the game.
	private void LeaveSeat(Guest guest, ClaimSeatInfo leave) {
		Seat seat = SeatsOf(guest).FirstOrDefault(s => s.info.playerID == leave.playerID);
		if (seat == null || Started || creatingGame) {
			SendLobby(guest);
			return;
		}
		log.Information("{Name} left the seat of {Player}", seat.takenBy, seat.info.playerID);
		FreeSeat(seat);
		BroadcastLobby();
	}

	private void FreeSeat(Seat seat) {
		seat.guest = null;
		seat.takenBy = null;
		seat.token = null;
		seat.heldUiMessages.Clear();
		ReleaseDiplomacy(seat);
		if (GuestsChooseCivilizations && !creatingGame) {
			// Whoever takes the seat next chooses afresh.
			seat.info = seat.info with { civilization = null };
		}
	}

	private void DropGuest(Guest guest) {
		guests.Remove(guest);
		guest.connection.Dispose();
		List<Seat> left = SeatsOf(guest).ToList();
		foreach (Seat seat in left) {
			if (HoldsSeats) {
				// They may well be back: the seat waits for them.
				log.Information("{Name} lost the connection to the seat of {Player}, which is held for them", seat.takenBy, seat.info.playerID);
				seat.guest = null;
				ReleaseDiplomacy(seat);
			} else {
				log.Information("{Name} left the seat of {Player}", seat.takenBy, seat.info.playerID);
				FreeSeat(seat);
			}
		}
		if (left.Count > 0) {
			BroadcastLobby();
		}
	}

	private static string NewToken() {
		return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
	}

	// A guest says hello with the token of seats it had: it has them all
	// back, and the game as it stands now. Returns false if no seat is held
	// for the token.
	private bool Reattach(Guest guest, string token) {
		List<Seat> theirs = seats.Where(s => s.token == token).ToList();
		if (theirs.Count == 0) {
			log.Information("{Name} came back, but no seat is held for them", guest.name);
			return false;
		}
		// Their old connection may not have noticed it has gone.
		foreach (Guest old in theirs.Select(s => s.guest).Where(g => g != null && g != guest).Distinct().ToList()) {
			guests.Remove(old);
			old.connection.Dispose();
			guest.pendingUiMessages.AddRange(old.pendingUiMessages);
		}
		guest.token = token;
		int turn = EngineStorage.gameData?.turn ?? -1;
		foreach (Seat seat in theirs) {
			seat.guest = guest;
			seat.away = false;
			if (Started && seat.heldTurn == turn) {
				guest.pendingUiMessages.AddRange(seat.heldUiMessages);
			}
			seat.heldUiMessages.Clear();
		}
		log.Information("{Name} is back, in the seats of {Players}", guest.name, string.Join(", ", theirs.Select(s => s.info.playerID)));

		if (Started) {
			// The clock first, so that the guest knows whose turn it is once
			// it has the game.
			TurnClockInfo clock = CurrentClock();
			if (clock != null) {
				guest.connection.Send(FrameKind.TurnClock, clock);
			}
			SendStart(guest, EncodeSnapshot());
			foreach (Seat seat in theirs) {
				ResendTurnPrompts(seat);
			}
		}
		BroadcastLobby();
		return true;
	}

	private void Reject(Guest guest, string reason) {
		guest.connection.Send(FrameKind.Rejected, Encoding.UTF8.GetBytes(reason));
		guest.connection.Dispose();
		guests.Remove(guest);
	}

	// Players at this machine are always here; a guest's are while the guest
	// is connected.
	private bool IsReachable(ID player) {
		Seat seat = seats.Find(s => s.info.playerID == player);
		return seat == null || seat.IsTaken;
	}

	// The engine may be waiting on the seat's player to answer an AI, who
	// has left or, rejoining, will never see the question. They are taken
	// to have closed it unanswered, so the game goes on.
	private void ReleaseDiplomacy(Seat seat) {
		if (!Started) {
			return;
		}
		if (EngineStorage.diplomacyPlayerID == seat.info.playerID) {
			log.Information("Closing the diplomacy {Player} was asked to answer", seat.info.playerID);
			EngineStorage.ReceiveFromRemote(new MsgDiplomacyCompleted { playerID = seat.info.playerID });
		}
		// Likewise a deal another human proposed to them is turned down.
		if (EngineStorage.pendingDeal?.opponent?.id == seat.info.playerID) {
			log.Information("Turning down the deal {Player} was asked to answer", seat.info.playerID);
			EngineStorage.ReceiveFromRemote(new MsgRespondToDeal(false) { playerID = seat.info.playerID });
		}
	}

	private void HandleCommand(Guest guest, Frame frame) {
		if (!Started) {
			return;
		}
		MessageToEngine msg = NetSerialization.DeserializeRequired<MessageToEngine>(frame.payload);
		if (msg.IsLocal) {
			return;
		}
		// Guests act only as their own players. One with a single seat always
		// acts as it; one with several says which.
		List<Seat> theirs = SeatsOf(guest).ToList();
		if (theirs.Count == 1) {
			msg.playerID = theirs[0].info.playerID;
		} else if (!theirs.Any(s => s.info.playerID == msg.playerID)) {
			log.Warning("Ignoring {Message} from {Name} for {Player}, who isn't theirs", msg.GetType().Name, guest.name, msg.playerID);
			return;
		}
		msg.DistrustRemoteSender(id => theirs.Any(s => s.info.playerID == id));
		EngineStorage.ReceiveFromRemote(msg);
	}

	// A guest's choice of civilization: one nobody else has chosen, or null
	// for a random one. Anything else leaves their seat as it was.
	private void ChooseCivilization(Guest guest, Frame frame) {
		if (!GuestsChooseCivilizations || creatingGame) {
			return;
		}
		ChooseCivilizationInfo choice = NetSerialization.DeserializeRequired<ChooseCivilizationInfo>(frame.payload);
		Seat seat = choice.playerID == null
			? SeatsOf(guest).FirstOrDefault()
			: SeatsOf(guest).FirstOrDefault(s => s.info.playerID == choice.playerID);
		if (seat == null) {
			return;
		}
		string civilization = choice.civilization;
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
		if (msg.IsForSpectatorsOnly) {
			byte[] spectatorJson = NetSerialization.Serialize(msg);
			foreach (Spectator spectator in spectators.Where(s => !s.connection.IsClosed)) {
				spectator.pendingUiMessages.Add(spectatorJson);
				spectatorSnapshotPending = true;
			}
			return;
		}

		if (msg.IsForEveryone) {
			EngineStorage.SendToLocalUI(msg);
			byte[] json = NetSerialization.Serialize(msg);
			foreach (Guest guest in SeatedGuests()) {
				QueueUiMessage(guest, json);
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
		} else if (recipientSeat.IsTaken) {
			QueueUiMessage(recipientSeat.guest, NetSerialization.Serialize(msg));
		} else if (recipientSeat.IsHeld) {
			HoldUiMessage(recipientSeat, NetSerialization.Serialize(msg));
		}
	}

	// Keeps a message for a disconnected player, to give them if they're
	// back before the turn is over; after that it's out of date.
	private static void HoldUiMessage(Seat seat, byte[] json) {
		int turn = EngineStorage.gameData?.turn ?? -1;
		if (seat.heldTurn != turn) {
			seat.heldTurn = turn;
			seat.heldUiMessages.Clear();
		}
		if (seat.heldUiMessages.Count < MaxHeldUiMessages) {
			seat.heldUiMessages.Add(json);
		}
	}

	private void QueueUiMessage(Guest guest, byte[] json) {
		if (!guest.connection.IsClosed) {
			guest.pendingUiMessages.Add(json);
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
			List<Guest> connected = SeatedGuests();
			if (connected.Count > 0) {
				snapshot = EncodeSnapshot();
				foreach (Guest guest in connected) {
					SendSnapshot(guest.connection, snapshot, guest.pendingUiMessages);
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

	private void SendLobby(Guest guest) {
		SendLobby(guest.connection, SeatsOf(guest).Select(s => s.info.playerID).ToList(), guest.token);
	}

	private void SendLobby(LanConnection connection, List<ID> yourSeats, string reconnectToken = null) {
		List<SeatInfo> seatInfos = [
			new SeatInfo(hostPlayerID, hostCivilization, hostName, true, hostName),
			.. Seats,
		];
		List<CivilizationChoice> civilizations = choosable?.Select(c => new CivilizationChoice(
			c.name, c.leader, c.noun, c.leaderArtFile, c.traits.Select(t => t.ToString()).ToList())).ToList();
		connection.Send(FrameKind.Lobby, new LobbyInfo(hostName, seatInfos, yourSeats, [.. Spectators], civilizations, creatingGame, Started,
			SimultaneousTurns, reconnectToken));
	}

	private void BroadcastLobby() {
		foreach (Guest guest in guests.Where(g => g.name != null)) {
			SendLobby(guest);
		}
		foreach (Spectator spectator in spectators) {
			SendLobby(spectator.connection, []);
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
		foreach (Guest guest in guests) {
			guest.connection.Dispose();
		}
		foreach (Spectator spectator in spectators) {
			spectator.connection.Dispose();
		}
		if (Started) {
			EngineStorage.ResetNetworking();
		}
	}
}

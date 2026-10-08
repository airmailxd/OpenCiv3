using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine.Network;

// A connection to a LAN host. The client's engine doesn't run: it sends its
// players' messages to the host, and shows the snapshots and UI messages the
// host sends back.
//
// Frames are handled in Poll(), which the lobby and then the game call every
// frame on the main thread, in the order they arrived. A snapshot is
// decompressed and read on a worker thread once it's the next frame to
// handle, and Poll hands it over (and goes on to the frames after it) once
// it's ready. Only one snapshot is read at a time: one followed straight
// away by a newer one is dropped for it without being read, and one
// identical to the snapshot last shown is skipped.
//
// A frame that can't be read or handled is logged and skipped. The host's
// frames are the only source of the game, so hanging up wouldn't get a
// better one.
//
// Once in the game, a client that loses the host keeps trying to connect
// again in the background, and says hello with the token the host gave it,
// to have its seats back where it left them.
public class LanClient : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanClient>();

	private LanConnection connection;
	private readonly string playerName;

	// The snapshot frame being read, and its reading.
	private Frame readingFrame;
	private Task<SaveGame> reading;

	// The compressed bytes of the snapshot last shown. The host doesn't send
	// a connection the same snapshot twice in a row, so this rarely matches,
	// but comparing costs little next to reading a snapshot and redrawing.
	private byte[] lastShownSnapshot;

	public string HostAddress { get; }
	public string Address { get; }
	public int Port { get; }
	public LobbyInfo Lobby { get; private set; }
	public string RejectedReason { get; private set; }

	// The players this client plays, in turn order, once the game has
	// started: more than one when they take turns at this machine. Empty for
	// a spectator.
	public IReadOnlyList<ID> PlayerIDs { get; private set; } = [];

	// The seats this client has taken in the lobby.
	public IReadOnlyList<ID> YourSeats => (IReadOnlyList<ID>)Lobby?.yourSeats ?? [];

	// Whether this client only watches the game.
	public bool IsSpectator { get; private set; }

	// The game as the host first sent it, until the game screen takes over.
	public SaveGame StartingGame { get; private set; }

	public bool IsConnected => !connection.IsClosed;

	// What the host gave us to say hello with to have our seats back.
	public string ReconnectToken { get; private set; }

	// Whether to try connecting again when the connection to the host is
	// lost, which the game screen turns on.
	public bool ReconnectAutomatically { get; set; }

	// Whether we're trying to connect again, until the host answers, and
	// which try this is.
	public bool Reconnecting { get; private set; }
	public int ReconnectAttempt => Volatile.Read(ref reconnectAttempt);
	private int reconnectAttempt;

	// The waits between tries start at the first and double up to the
	// longest.
	internal TimeSpan FirstReconnectDelay = TimeSpan.FromSeconds(1);
	internal TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

	// The connection a try made, for Poll to take over; the tries being
	// made; and whether we have said hello on a new connection and wait
	// for the host's answer.
	private TcpClient reconnected;
	private CancellationTokenSource reconnecting;
	private bool awaitingAnswer;
	private volatile bool disposed;

	// True when we're back with a host that has our game in its lobby,
	// having started hosting it again, until it starts it.
	public bool HostIsResuming { get; private set; }

	// The host's turn clock as last sent, and how long ago that was.
	private TurnClockInfo clock;
	private readonly System.Diagnostics.Stopwatch sinceClock = new();

	public event Action LobbyChanged;

	// Set by the game screen. Snapshots are applied before the UI messages
	// that follow them, which refer to the snapshot's units and cities.
	public Action<SaveGame> SnapshotReceived;
	public Action<byte[]> UiMessageReceived;

	// Set by the game screen: called when the host says again which players
	// are ours, after we take another seat in the game in progress or come
	// back after losing the connection.
	public Action PlayersChanged;

	private LanClient(TcpClient tcp, string address, int port, string playerName, string reconnectToken) {
		connection = new LanConnection(tcp);
		Address = address;
		Port = port;
		HostAddress = $"{address}:{port}";
		this.playerName = playerName;
		ReconnectToken = reconnectToken;
	}

	// Joins the host there. With the token from a game we were in, the host
	// gives back the seats we had in it.
	public static LanClient Connect(string address, int port, string playerName, string reconnectToken = null) {
		TcpClient tcp = new();
		tcp.Connect(address, port);
		LanClient client = new(tcp, address, port, playerName, reconnectToken);
		client.SayHello();
		return client;
	}

	private void SayHello() {
		connection.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, playerName, ReconnectToken));
	}

	// Takes a seat, alongside any taken already, for the player named (or
	// this client's own name when null). Claiming one of our own seats again
	// renames its player, before the game starts.
	public void ClaimSeat(ID playerID, string playerName = null) {
		connection.Send(FrameKind.ClaimSeat, new ClaimSeatInfo(playerID, playerName));
	}

	// Gives back a seat before the game starts.
	public void LeaveSeat(ID playerID) {
		connection.Send(FrameKind.LeaveSeat, new ClaimSeatInfo(playerID));
	}

	// Chooses the civilization for one of our seats (null for the first) to
	// play in a game not created yet; null for a random one.
	public void ChooseCivilization(string civilization, ID playerID = null) {
		connection.Send(FrameKind.ChooseCivilization, new ChooseCivilizationInfo(civilization, playerID));
	}

	// Watches the game instead of taking a seat.
	public void Watch() {
		IsSpectator = true;
		connection.Send(FrameKind.Watch, []);
	}

	public void SendCommand(MessageToEngine msg) {
		if (IsSpectator) {
			return;
		}
		connection.Send(FrameKind.Command, NetSerialization.Serialize(msg));
	}

	// The turn clock as it stands now, or null until the host sends it.
	public TurnClockInfo CurrentClock() {
		return clock == null ? null : clock with { secondsElapsed = clock.secondsElapsed + sinceClock.Elapsed.TotalSeconds };
	}

	public void Poll() {
		PollFrames();
		PollReconnecting();
	}

	private void PollFrames() {
		while (connection.TryPeek(out Frame frame)) {
			// Once the game has started, wait for the game screen before
			// handling anything more.
			if (StartingGame != null && SnapshotReceived == null) {
				return;
			}
			if (frame.kind == FrameKind.Snapshot && !ReferenceEquals(frame, readingFrame)) {
				// A snapshot of the game already shown, or one that a newer
				// snapshot straight after it replaces, isn't read at all.
				bool superseded = connection.TryPeekAt(1, out Frame next) && next.kind == FrameKind.Snapshot;
				if (superseded || IsShown(frame.payload)) {
					connection.TryReceive(out _);
					continue;
				}
				readingFrame = frame;
				reading = Task.Run(() => LanProtocol.DecodeSnapshot(frame.payload));
			}
			if (ReferenceEquals(frame, readingFrame) && !reading.IsCompleted) {
				// Wait for it, so frames stay in order.
				return;
			}
			connection.TryReceive(out frame);
			try {
				Handle(frame);
			} catch (Exception e) {
				log.Error(e, "Couldn't handle a {Kind} frame from the host", frame.kind);
			}
		}
	}

	// Once the connection is lost and everything that came over it has been
	// handled, starts trying to connect again; and takes over the new
	// connection once there is one.
	private void PollReconnecting() {
		if (Interlocked.Exchange(ref reconnected, null) is TcpClient tcp) {
			try {
				connection = new LanConnection(tcp);
			} catch (Exception e) when (e is SocketException or InvalidOperationException or ObjectDisposedException) {
				log.Information("Lost the new connection to the host straight away: {Error}", e.Message);
				tcp.Dispose();
				StartReconnecting();
				return;
			}
			log.Information("Connected to the host again, saying hello");
			readingFrame = null;
			reading = null;
			// Show the host's game afresh, even if it's what we showed last.
			lastShownSnapshot = null;
			awaitingAnswer = true;
			SayHello();
			if (IsSpectator) {
				connection.Send(FrameKind.Watch, []);
			}
			return;
		}
		bool trying = Reconnecting && !awaitingAnswer;
		if (trying || disposed || !ReconnectAutomatically || RejectedReason != null
			|| !connection.IsClosed || connection.TryPeek(out _)) {
			return;
		}
		StartReconnecting();
	}

	private void StartReconnecting() {
		if (!Reconnecting) {
			log.Information("Lost the connection to the host, trying to connect again");
			Volatile.Write(ref reconnectAttempt, 0);
		}
		Reconnecting = true;
		awaitingAnswer = false;
		reconnecting?.Cancel();
		CancellationTokenSource cancel = new();
		reconnecting = cancel;
		Task.Run(() => TryConnecting(cancel.Token));
	}

	// Runs on a worker thread until a connection is made or the tries are
	// called off.
	private async Task TryConnecting(CancellationToken cancel) {
		TimeSpan delay = FirstReconnectDelay;
		while (!cancel.IsCancellationRequested) {
			try {
				await Task.Delay(delay, cancel);
			} catch (OperationCanceledException) {
				return;
			}
			delay = delay * 2 < MaxReconnectDelay ? delay * 2 : MaxReconnectDelay;
			int attempt = Interlocked.Increment(ref reconnectAttempt);
			TcpClient tcp = new();
			try {
				using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
				timeout.CancelAfter(ConnectTimeout);
				await tcp.ConnectAsync(Address, Port, timeout.Token);
				if (cancel.IsCancellationRequested) {
					tcp.Dispose();
					return;
				}
				Interlocked.Exchange(ref reconnected, tcp)?.Dispose();
				return;
			} catch (Exception e) when (e is SocketException or OperationCanceledException or ObjectDisposedException) {
				tcp.Dispose();
				log.Debug("Couldn't reach the host on try {Attempt}: {Error}", attempt, e.Message);
			}
		}
	}

	// The host has answered our hello on a new connection.
	private void NoteAnswer() {
		if (!awaitingAnswer) {
			return;
		}
		awaitingAnswer = false;
		Reconnecting = false;
		log.Information("The host has us back");
	}

	// Stops trying to connect again.
	public void StopReconnecting() {
		reconnecting?.Cancel();
		Reconnecting = false;
		awaitingAnswer = false;
	}

	private bool IsShown(byte[] snapshot) {
		return lastShownSnapshot != null && (StartingGame != null || SnapshotReceived != null)
			&& snapshot.AsSpan().SequenceEqual(lastShownSnapshot);
	}

	private void Handle(Frame frame) {
		switch (frame.kind) {
			case FrameKind.Lobby:
				Lobby = NetSerialization.DeserializeRequired<LobbyInfo>(frame.payload);
				ReconnectToken = Lobby.reconnectToken ?? ReconnectToken;
				// A host with the game we're in back in its lobby is resuming
				// it.
				HostIsResuming = !Lobby.started && (StartingGame != null || SnapshotReceived != null);
				NoteAnswer();
				LobbyChanged?.Invoke();
				break;
			case FrameKind.Rejected:
				RejectedReason = Encoding.UTF8.GetString(frame.payload);
				log.Information("The host turned us away: {Reason}", RejectedReason);
				StopReconnecting();
				LobbyChanged?.Invoke();
				break;
			case FrameKind.Start:
				StartInfo start = NetSerialization.DeserializeRequired<StartInfo>(frame.payload);
				PlayerIDs = start.yourPlayerIDs ?? [];
				ReconnectToken = start.reconnectToken ?? ReconnectToken;
				HostIsResuming = false;
				NoteAnswer();
				PlayersChanged?.Invoke();
				break;
			case FrameKind.Snapshot:
				Task<SaveGame> read = ReferenceEquals(frame, readingFrame) ? reading : null;
				readingFrame = null;
				reading = null;
				SaveGame save = read != null ? read.GetAwaiter().GetResult() : LanProtocol.DecodeSnapshot(frame.payload);
				if (save == null) {
					throw new System.IO.InvalidDataException("The snapshot holds no game");
				}
				lastShownSnapshot = frame.payload;
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
			case FrameKind.TurnClock:
				clock = NetSerialization.DeserializeRequired<TurnClockInfo>(frame.payload);
				sinceClock.Restart();
				break;
			default:
				log.Warning("Ignoring unexpected {Kind} frame from the host", frame.kind);
				break;
		}
	}

	public void Dispose() {
		disposed = true;
		StopReconnecting();
		Interlocked.Exchange(ref reconnected, null)?.Dispose();
		connection.Dispose();
	}
}

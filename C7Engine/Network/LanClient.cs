using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using C7GameData;
using C7GameData.Save;
using C7Relay;
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
// Most snapshots are patches to the one before (see EncodedSnapshot), so
// every snapshot is decompressed, in order, even those not read. One that
// can't be applied leaves us without the game the next ones patch, so we ask
// the host for the whole game, and skip the patches until it comes.
//
// A frame that can't be read or handled is logged and skipped. The host's
// frames are the only source of the game, so hanging up wouldn't get a
// better one.
//
// With PredictMoves on, our players' simple orders are carried out on our
// game as they're sent (see MovePrediction), and the host answers them with
// a snapshot even if they change nothing there, which puts our game right.
//
// Once in the game, a client that loses the host keeps trying to connect
// again in the background, and says hello with the token the host gave it,
// to have its seats back where it left them.
//
// A host with a password asks for it before letting us in, unless we're back
// with our token: the password given when joining answers it, or the player
// is asked for it (see PasswordRequest), and it's kept to answer the host
// again after losing the connection.
public class LanClient : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanClient>();

	private LanConnection connection;
	private readonly string playerName;

	// The snapshot frame being read, and its reading: what was decompressed,
	// or null if it couldn't be, and the game, or null if it's the one shown
	// already.
	private Frame readingFrame;
	private Task<(ReceivedSnapshot, SaveGame)> reading;

	// The last snapshot decompressed, which the next one patches; null once
	// one couldn't be, until the host sends the whole game again. Each is
	// decompressed on a worker thread once the one before it is.
	private Task<ReceivedSnapshot> decompressed = Task.FromResult<ReceivedSnapshot>(null);

	// Whether we have asked for the whole game and not had it yet.
	private bool askedForWholeSnapshot;

	// The hash of the snapshot last shown. The host doesn't send a
	// connection the same snapshot twice in a row, so this rarely matches,
	// but comparing costs little next to reading a snapshot and redrawing.
	private byte[] lastShownSnapshot;

	// The hash of the last snapshot decompressed, and how many came whole
	// and as patches, for tests.
	internal byte[] ReceivedSnapshotHash => decompressed.IsCompletedSuccessfully ? decompressed.Result?.Hash : null;
	internal int WholeSnapshotsReceived { get; private set; }
	internal byte[] ShownSnapshotHash => lastShownSnapshot;
	internal int SnapshotDeltasReceived { get; private set; }

	// Where the host is, which is where we connect again after losing it.
	// A host online that moves its game to another join code is followed
	// there.
	public LanEndpoint Endpoint {
		get => endpoint;
		private set => endpoint = value;
	}
	private volatile LanEndpoint endpoint;

	// Trying to connect again stops once the relay has said for this long
	// that there's no game with the join code: the host has stopped hosting
	// online, or moved to a code we weren't told.
	internal static TimeSpan GiveUpOnUnknownCode = TimeSpan.FromMinutes(3);
	public string HostAddress => Endpoint.Description;

	// The host's address on the network, or null for one joined online.
	public string Address => (Endpoint as LanAddressEndpoint)?.Address;
	public int Port => (Endpoint as LanAddressEndpoint)?.Port ?? 0;
	public LobbyInfo Lobby { get; private set; }
	public string RejectedReason { get; private set; }

	// The host asks for the game's password, which the player hasn't given
	// or got wrong: answer with SendPassword. Null otherwise.
	public PasswordChallengeInfo PasswordRequest { get; private set; }

	// The password to answer the host with, once given.
	private string password;

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

	// Whether our players' simple orders, like moving a unit, are carried
	// out on our game as they're sent, rather than shown once the host's
	// snapshot comes back (see MovePrediction).
	public bool PredictMoves { get; set; }

	// Whether to try connecting again when the connection to the host is
	// lost, which the game screen turns on.
	public bool ReconnectAutomatically { get; set; }

	// Whether we're trying to connect again, until the host answers, and
	// which try this is.
	public bool Reconnecting { get; private set; }
	public int ReconnectAttempt => Volatile.Read(ref reconnectAttempt);
	private int reconnectAttempt;

	// Why the last try to connect again failed, or null.
	public string LastReconnectError => lastReconnectError;
	private volatile string lastReconnectError;

	// Why trying to connect again can't work, as when the relay says we're
	// banned; Poll turns us away with it.
	private volatile string reconnectFailedForGood;

	// The waits between tries start at the first and double up to the
	// longest.
	internal TimeSpan FirstReconnectDelay = TimeSpan.FromSeconds(1);
	internal TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

	// How long joining waits for the host to answer at first, which may be
	// some way away.
	public static readonly TimeSpan InitialConnectTimeout = TimeSpan.FromSeconds(10);

	// The connection a try made, for Poll to take over; the tries being
	// made; and whether we have said hello on a new connection and wait
	// for the host's answer.
	private LanTransport reconnected;
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

	// Set by the game screen: called when the host says it sends this
	// spectator the game another way, ahead of the snapshots of it.
	public Action SpectatorViewChanged;

	// Set by the game screen: called when one of our orders has been
	// carried out on our game ahead of the host (see PredictMoves).
	public Action OrderPredicted;

	private LanClient(LanTransport transport, LanEndpoint endpoint, string playerName, string reconnectToken, string password) {
		connection = new LanConnection(transport);
		Endpoint = endpoint;
		this.playerName = playerName;
		ReconnectToken = reconnectToken;
		this.password = string.IsNullOrEmpty(password) ? null : password;
	}

	// Joins the host there, waiting until connected. With the token from a
	// game we were in, the host gives back the seats we had in it.
	public static LanClient Connect(string address, int port, string playerName, string reconnectToken = null) {
		return ConnectAsync(new LanAddressEndpoint(address, port), playerName, reconnectToken).GetAwaiter().GetResult();
	}

	// Joins the host there without holding up the caller, giving up with a
	// TimeoutException if it doesn't answer in time. The password, if
	// given, is the game's, for a host that asks for one.
	public static async Task<LanClient> ConnectAsync(LanEndpoint endpoint, string playerName, string reconnectToken = null,
		TimeSpan? timeout = null, CancellationToken cancel = default, string password = null) {
		LanTransport transport = await endpoint.ConnectAsync(timeout ?? InitialConnectTimeout, cancel).ConfigureAwait(false);
		if (cancel.IsCancellationRequested) {
			transport.Dispose();
			cancel.ThrowIfCancellationRequested();
		}
		LanClient client = new(transport, endpoint, playerName, reconnectToken, password);
		client.SayHello();
		return client;
	}

	private void SayHello() {
		connection.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, playerName, ReconnectToken));
	}

	// Answers the host's PasswordRequest with the game's password, which is
	// kept to answer the host again after losing the connection.
	public void SendPassword(string typed) {
		password = typed ?? "";
		if (PasswordRequest is PasswordChallengeInfo challenge) {
			PasswordRequest = null;
			AnswerPassword(challenge);
		}
	}

	private void AnswerPassword(PasswordChallengeInfo challenge) {
		string verifier = GamePassword.Verifier(password, challenge.salt);
		connection.Send(FrameKind.Password, new PasswordInfo(GamePassword.Proof(verifier, challenge.nonce)));
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

	// How the host sends this spectator the game now, or null if it hasn't
	// said.
	public SpectatorViewInfo SpectatorView => IsSpectator ? Lobby?.yourSpectatorView : null;

	// The ways the host lets spectators see the game.
	public SpectatorViews AllowedSpectatorViews => Lobby?.spectatorViews ?? SpectatorViews.None;

	// Asks the host to send this spectator the game another way, which it
	// does if it allows it; the lobby it answers with says how it's sent.
	// The choice is asked for again after losing the connection.
	public void ChooseSpectatorView(SpectatorViewInfo view) {
		if (!IsSpectator || view == null) {
			return;
		}
		chosenSpectatorView = view;
		connection.Send(FrameKind.ChooseSpectatorView, view);
	}
	private SpectatorViewInfo chosenSpectatorView;

	public void SendCommand(MessageToEngine msg) {
		if (IsSpectator) {
			return;
		}
		byte[] payload = NetSerialization.Serialize(msg);
		if (PredictMoves && MovePrediction.Predict(msg)) {
			// Our game is no longer the one the host last sent, so the
			// host's answer is shown even if it's that one again.
			lastShownSnapshot = null;
			connection.Send(FrameKind.PredictedCommand, payload);
			OrderPredicted?.Invoke();
		} else {
			connection.Send(FrameKind.Command, payload);
		}
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
			if (IsSnapshot(frame.kind) && !ReferenceEquals(frame, readingFrame)) {
				Task<ReceivedSnapshot> snapshot = Decompress(frame);
				// A snapshot that a newer snapshot straight after it replaces
				// is only decompressed, for the next one to patch.
				if (connection.TryPeekAt(1, out Frame next) && IsSnapshot(next.kind)) {
					connection.TryReceive(out _);
					continue;
				}
				readingFrame = frame;
				byte[] shown = StartingGame != null || SnapshotReceived != null ? lastShownSnapshot : null;
				reading = snapshot.ContinueWith(t => {
					ReceivedSnapshot received = t.Result;
					// The game already shown isn't read again.
					if (received == null || (shown != null && received.Hash.AsSpan().SequenceEqual(shown))) {
						return (received, (SaveGame)null);
					}
					try {
						return (received, SaveGame.FromJSON(received.Json));
					} catch (Exception e) {
						// Taken as one that couldn't be decompressed: the
						// host sends the whole game.
						log.Warning("Couldn't read the game the host sent: {Error}", e.Message);
						return ((ReceivedSnapshot)null, (SaveGame)null);
					}
				}, TaskScheduler.Default);
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

	private static bool IsSnapshot(FrameKind kind) => kind is FrameKind.Snapshot or FrameKind.SnapshotDelta;

	// Decompresses a snapshot frame on a worker thread once the snapshot
	// before it is, applying it to that one if it's a patch. A frame that
	// can't be read leaves null, and the patches after it fail too, until
	// the next whole snapshot.
	private Task<ReceivedSnapshot> Decompress(Frame frame) {
		if (frame.kind == FrameKind.Snapshot) {
			WholeSnapshotsReceived++;
			askedForWholeSnapshot = false;
		} else {
			SnapshotDeltasReceived++;
		}
		// Once we have asked for the whole game, the patches before it are
		// expected to fail.
		bool quiet = askedForWholeSnapshot;
		decompressed = decompressed.ContinueWith(previous => {
			try {
				return LanProtocol.ReadSnapshot(frame.kind, frame.payload, previous.Result);
			} catch (Exception e) {
				if (!quiet) {
					log.Warning("Couldn't read a {Kind} frame from the host: {Error}", frame.kind, e.Message);
				}
				return null;
			}
		}, TaskScheduler.Default);
		return decompressed;
	}

	// Once the connection is lost and everything that came over it has been
	// handled, starts trying to connect again; and takes over the new
	// connection once there is one.
	private void PollReconnecting() {
		if (Interlocked.Exchange(ref reconnected, null) is LanTransport transport) {
			try {
				connection = new LanConnection(transport);
			} catch (Exception e) when (e is SocketException or InvalidOperationException or ObjectDisposedException) {
				log.Information("Lost the new connection to the host straight away: {Error}", e.Message);
				transport.Dispose();
				StartReconnecting();
				return;
			}
			log.Information("Connected to the host again, saying hello");
			readingFrame = null;
			reading = null;
			// The host starts the new connection with the whole game, which
			// we show afresh, even if it's what we showed last.
			decompressed = Task.FromResult<ReceivedSnapshot>(null);
			askedForWholeSnapshot = false;
			lastShownSnapshot = null;
			awaitingAnswer = true;
			sinceHello.Restart();
			SayHello();
			if (IsSpectator) {
				connection.Send(FrameKind.Watch, []);
				if (chosenSpectatorView != null) {
					connection.Send(FrameKind.ChooseSpectatorView, chosenSpectatorView);
				}
			}
			return;
		}
		if (reconnectFailedForGood is string why && RejectedReason == null) {
			RejectedReason = why;
			log.Information("Can't connect to the host again: {Reason}", why);
			StopReconnecting();
			LobbyChanged?.Invoke();
			return;
		}
		if (awaitingAnswer && !connection.IsClosed && sinceHello.Elapsed > AnswerTimeout) {
			log.Information("The host didn't answer within {Seconds} seconds, trying again", AnswerTimeout.TotalSeconds);
			connection.Dispose();
			StartReconnecting();
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
		System.Diagnostics.Stopwatch unknownCode = null;
		while (!cancel.IsCancellationRequested) {
			try {
				await Task.Delay(delay, cancel);
			} catch (OperationCanceledException) {
				return;
			}
			delay = delay * 2 < MaxReconnectDelay ? delay * 2 : MaxReconnectDelay;
			int attempt = Interlocked.Increment(ref reconnectAttempt);
			try {
				LanTransport transport = await Endpoint.ConnectAsync(ConnectTimeout, cancel);
				if (cancel.IsCancellationRequested) {
					transport.Dispose();
					return;
				}
				Interlocked.Exchange(ref reconnected, transport)?.Dispose();
				return;
			} catch (RelayException e) when (e.MovedTo is string moved && Endpoint is RelayEndpoint online) {
				log.Information("The host moved the game to {Code}, following it", RelayProtocol.FormatCode(moved));
				Endpoint = online with { Code = moved };
				lastReconnectError = e.Message;
				delay = FirstReconnectDelay;
			} catch (RelayException e) when (e.IsPermanent) {
				reconnectFailedForGood = e.Message;
				return;
			} catch (RelayException e) when (e.CloseCode == RelayCloseCodes.UnknownRoom) {
				lastReconnectError = e.Message;
				unknownCode ??= System.Diagnostics.Stopwatch.StartNew();
				if (unknownCode.Elapsed >= GiveUpOnUnknownCode) {
					reconnectFailedForGood = "The game's join code no longer exists: the host stopped hosting it online, or moved it "
						+ "to a new code. Ask the host for the code to join again.";
					return;
				}
			} catch (Exception e) when (e is SocketException or OperationCanceledException or ObjectDisposedException
				or TimeoutException or IOException or WebSocketException) {
				lastReconnectError = e.Message;
				unknownCode = null;
				log.Debug("Couldn't reach the host on try {Attempt}: {Error}", attempt, e.Message);
			}
		}
	}

	// A host that takes the new connection but doesn't answer our hello in
	// this long is given up on, and we try again.
	internal static TimeSpan AnswerTimeout = TimeSpan.FromSeconds(15);
	private readonly System.Diagnostics.Stopwatch sinceHello = new();

	// The host has answered our hello on a new connection.
	private void NoteAnswer() {
		if (!awaitingAnswer) {
			return;
		}
		awaitingAnswer = false;
		Reconnecting = false;
		lastReconnectError = null;
		log.Information("The host has us back");
	}

	// Stops trying to connect again.
	public void StopReconnecting() {
		reconnecting?.Cancel();
		Reconnecting = false;
		awaitingAnswer = false;
	}

	private void Handle(Frame frame) {
		switch (frame.kind) {
			case FrameKind.PasswordRequired:
				PasswordChallengeInfo challenge = NetSerialization.DeserializeRequired<PasswordChallengeInfo>(frame.payload);
				NoteAnswer();
				if (password != null && !challenge.wrong) {
					AnswerPassword(challenge);
				} else {
					PasswordRequest = challenge;
					LobbyChanged?.Invoke();
				}
				break;
			case FrameKind.Lobby:
				PasswordRequest = null;
				SpectatorViewInfo viewBefore = SpectatorView;
				Lobby = WithSafeArt(NetSerialization.DeserializeRequired<LobbyInfo>(frame.payload));
				if (SpectatorView != viewBefore) {
					SpectatorViewChanged?.Invoke();
				}

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
			case FrameKind.SnapshotDelta:
				Task<(ReceivedSnapshot, SaveGame)> read = reading;
				readingFrame = null;
				reading = null;
				(ReceivedSnapshot received, SaveGame save) = read.GetAwaiter().GetResult();
				if (received == null) {
					AskForWholeSnapshot();
					break;
				}
				if (save == null) {
					// The game shown already.
					break;
				}
				lastShownSnapshot = received.Hash;
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

	// The lobby without any leader art the host named that isn't one of the
	// game's own files (see LanProtocol.IsSafeArtPath).
	private static LobbyInfo WithSafeArt(LobbyInfo lobby) {
		if (lobby.civilizations == null || lobby.civilizations.All(c => c == null || c.leaderArtFile == null || LanProtocol.IsSafeArtPath(c.leaderArtFile))) {
			return lobby;
		}
		log.Warning("Ignoring leader art the host named outside the game's art");
		return lobby with {
			civilizations = lobby.civilizations
				.Select(c => c == null || LanProtocol.IsSafeArtPath(c.leaderArtFile) ? c : c with { leaderArtFile = null })
				.ToList(),
		};
	}

	// A snapshot couldn't be read, so we don't have the game the next ones
	// patch: the host sends the whole of it next.
	private void AskForWholeSnapshot() {
		if (askedForWholeSnapshot) {
			return;
		}
		askedForWholeSnapshot = true;
		log.Information("Asking the host for the whole game");
		connection.Send(FrameKind.RequestSnapshot, []);
	}

	public void Dispose() {
		disposed = true;
		StopReconnecting();
		Interlocked.Exchange(ref reconnected, null)?.Dispose();
		connection.Dispose();
	}
}

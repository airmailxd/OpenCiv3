using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine.Network;

// A connection to a LAN host. The client's engine doesn't run: it sends its
// player's messages to the host, and shows the snapshots and UI messages the
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
public class LanClient : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanClient>();

	private readonly LanConnection connection;

	// The snapshot frame being read, and its reading.
	private Frame readingFrame;
	private Task<SaveGame> reading;

	// The compressed bytes of the snapshot last shown. The host doesn't send
	// a connection the same snapshot twice in a row, so this rarely matches,
	// but comparing costs little next to reading a snapshot and redrawing.
	private byte[] lastShownSnapshot;

	public string HostAddress { get; }
	public LobbyInfo Lobby { get; private set; }
	public string RejectedReason { get; private set; }

	// The player this client plays, once the game has started. Null for a
	// spectator.
	public ID PlayerID { get; private set; }

	// Whether this client only watches the game.
	public bool IsSpectator { get; private set; }

	// The game as the host first sent it, until the game screen takes over.
	public SaveGame StartingGame { get; private set; }

	public bool IsConnected => !connection.IsClosed;

	// The host's turn clock as last sent, and how long ago that was.
	private TurnClockInfo clock;
	private readonly System.Diagnostics.Stopwatch sinceClock = new();

	public event Action LobbyChanged;

	// Set by the game screen. Snapshots are applied before the UI messages
	// that follow them, which refer to the snapshot's units and cities.
	public Action<SaveGame> SnapshotReceived;
	public Action<byte[]> UiMessageReceived;

	private LanClient(TcpClient tcp, string hostAddress) {
		connection = new LanConnection(tcp);
		HostAddress = hostAddress;
	}

	public static LanClient Connect(string address, int port, string playerName) {
		TcpClient tcp = new();
		tcp.Connect(address, port);
		LanClient client = new(tcp, $"{address}:{port}");
		client.connection.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, playerName));
		return client;
	}

	public void ClaimSeat(ID playerID) {
		connection.Send(FrameKind.ClaimSeat, new ClaimSeatInfo(playerID));
	}

	// Chooses the civilization to play in a game not created yet; null for
	// a random one.
	public void ChooseCivilization(string civilization) {
		connection.Send(FrameKind.ChooseCivilization, new ChooseCivilizationInfo(civilization));
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

	private bool IsShown(byte[] snapshot) {
		return lastShownSnapshot != null && (StartingGame != null || SnapshotReceived != null)
			&& snapshot.AsSpan().SequenceEqual(lastShownSnapshot);
	}

	private void Handle(Frame frame) {
		switch (frame.kind) {
			case FrameKind.Lobby:
				Lobby = NetSerialization.DeserializeRequired<LobbyInfo>(frame.payload);
				LobbyChanged?.Invoke();
				break;
			case FrameKind.Rejected:
				RejectedReason = Encoding.UTF8.GetString(frame.payload);
				log.Information("The host turned us away: {Reason}", RejectedReason);
				LobbyChanged?.Invoke();
				break;
			case FrameKind.Start:
				PlayerID = NetSerialization.DeserializeRequired<StartInfo>(frame.payload).yourPlayerID;
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
		connection.Dispose();
	}
}

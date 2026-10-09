using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using C7GameData;
using C7GameData.Save;
using C7Relay;
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
// saves the game as each turn begins, with those tokens (and saves them
// again as they change), and can resume it from that save, holding the
// seats for the guests to come back to.
//
// Guests join over TCP, or, once the host is also hosting online, through an
// online relay with a join code (see RelayHostLink); either way they're the
// same to the host.
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
// last one a connection was sent isn't sent again. Each connection is sent
// the whole game once, and after that patches to the last snapshot it was
// sent (see EncodedSnapshot), until its client asks for the whole game again.
//
// Unless the host turns HideUnseen off, each guest's snapshots hold only what
// the players at its machine may know (see SnapshotFilter): the game is taken
// once, and filtered and encoded for each machine on worker threads. A
// machine's snapshots are patches to its own last one, as before.
//
// The host may set a password (see GamePassword), which guests give before
// they're let in, unless they're coming back to their seats with their
// token. It can remove a guest from the game, which frees its seats for
// others, unlike losing the connection; or ban it, which also turns away its
// token, and its address: on the network here, or through the relay.
//
// Hosting online, the host can also list its game in the relay's public
// list (ListPublicly), which it keeps up to date as the game goes on.
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

	// A guest has this long to say hello once it connects, and this long to
	// give the password once asked, which a player types; one that takes
	// longer is dropped, rather than hold its connection open for nothing.
	internal static TimeSpan HelloTimeout = TimeSpan.FromSeconds(15);
	internal static TimeSpan PasswordTimeout = TimeSpan.FromMinutes(2);

	// Guests without a seat, at most: in all, and from one address on the
	// network. Past the first, the one waiting longest makes way for the
	// newcomer, so that guests coming back to their seats always get in;
	// past the second, the newcomer is turned away.
	private const int MaxUnseatedGuests = 24;
	private const int MaxUnseatedGuestsPerAddress = 4;

	// How many frames a guest or spectator may have handled a second, on
	// average, and at once; the rest wait their turn, and a peer whose
	// frames pile up past the most a connection holds here is dropped.
	private const double FramesPerSecond = 50;
	private const double FrameBurst = 200;
	private const int MaxReceivedFramesPerPeer = 5_000;

	// A peer that asks for the whole game again is sent it no more often
	// than this, since taking and encoding it is costly.
	private static readonly TimeSpan WholeSnapshotInterval = TimeSpan.FromSeconds(5);

	// The frames a peer may have handled now (see FramesPerSecond).
	private sealed class FrameBudget {
		private double frames = FrameBurst;
		private long refilledAt = Stopwatch.GetTimestamp();

		public bool Take() {
			long now = Stopwatch.GetTimestamp();
			frames = Math.Min(FrameBurst, frames + Stopwatch.GetElapsedTime(refilledAt, now).TotalSeconds * FramesPerSecond);
			refilledAt = now;
			if (frames < 1) {
				return false;
			}
			frames -= 1;
			return true;
		}
	}

	// A peer's asking for the whole game: whether it's waiting for it, and
	// when it was last sent it.
	private sealed class WholeSnapshotRequest {
		public bool pending;
		public Stopwatch sinceSent;

		// Has the peer sent the whole game next if it's asked and not too
		// soon after the last time; returns whether it was.
		public bool TakeDue(LanConnection connection) {
			if (!pending || (sinceSent != null && sinceSent.Elapsed < WholeSnapshotInterval)) {
				return false;
			}
			pending = false;
			(sinceSent ??= new Stopwatch()).Restart();
			RequestWholeSnapshot(connection);
			return true;
		}
	}

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
		// Bans the address of the guest who took the seat, even once it has
		// gone; null when that can't be done.
		public Action banAddress;
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
		// The guest guessed at what its orders do (see MovePrediction), so
		// its next snapshot is sent even if the game hasn't changed, which
		// only a snapshot would show it otherwise.
		public bool awaitsAnswer;
		// Let in, having said hello, and given the password if the game has
		// one. Until then, the nonce it was asked to sign, how many wrong
		// answers it gave, and whether it asked to watch meanwhile.
		public bool admitted;
		public string nonce;
		public int wrongPasswords;
		public bool watchOnceAdmitted;
		// Since it connected, or was last asked for the password.
		public readonly Stopwatch waiting = Stopwatch.StartNew();
		public readonly FrameBudget frameBudget = new();
		public readonly WholeSnapshotRequest wholeSnapshot = new();
	}

	private string hostName;
	private readonly List<Seat> seats;
	private readonly ID hostPlayerID;
	private readonly string hostCivilization;

	// The game's turn while in the lobby, for its public listing.
	private readonly int lobbyTurn;

	// The game's password as its salt and verifier (see GamePassword), or
	// null when anyone may join.
	private string passwordSalt;
	private string passwordVerifier;

	// The wrong passwords from each address (see WrongPasswords).
	private sealed class PasswordFailures {
		public int count;
		public int lockouts;
		public long lockedUntil;
	}
	private readonly Dictionary<string, PasswordFailures> passwordFailures = new();
	private const int MaxPasswordFailureAddresses = 1024;

	// The guests the host banned: their tokens, and the addresses of those
	// that joined on the network. Those through a relay, the relay turns
	// away (see RelayHostLink.Bans), with these keys when resuming.
	private readonly HashSet<string> bannedTokens = new();
	private readonly HashSet<string> bannedAddresses = new();
	private List<string> resumedRelayBans;

	// The secret the relay makes this game's ban keys with, the same in
	// every room the game has there (see RelayProtocol).
	private string relayBanScope = NewToken();

	public const string KickedReason = "The host removed you from the game.";
	public const string BannedReason = "The host has banned you from this game.";

	// For a new game not created yet, the civilizations guests can choose
	// from; null once the game exists.
	private List<Civilization> choosable;
	private bool creatingGame;
	private readonly TcpListener listener;
	private readonly UdpClient discovery;
	private readonly ConcurrentQueue<LanTransport> accepted = new();

	// Everyone joined to play, whether or not they have taken a seat yet.
	private readonly List<Guest> guests = new();

	// Connections watching the game rather than playing in it.
	// Each has an ID of its own, for the host to tell them apart by, and the
	// token it was let in with, to say hello with to watch again after
	// losing the connection.
	private class Spectator {
		public int id;
		public LanConnection connection;
		public string name;
		public string token;
		public readonly List<byte[]> pendingUiMessages = new();
		public FrameBudget frameBudget = new();
		public readonly WholeSnapshotRequest wholeSnapshot = new();
	}
	private readonly List<Spectator> spectators = new();
	private int nextSpectatorID = 1;

	// The most spectators watching at once.
	public const int MaxSpectators = 8;

	// The tokens of the spectators let in lately, who may come back to
	// watch without the password, as players come back to their seats.
	private readonly List<string> spectatorTokens = new();
	private const int MaxSpectatorTokens = 256;

	// Whether anyone may watch the game rather than play. Spectators see
	// the whole game, so unless the host says otherwise they may watch only
	// when guests see the whole game too (HideUnseen off). Not allowing them
	// any more stops those watching.
	public bool AllowSpectators {
		get => allowSpectators ?? !hideUnseen;
		set {
			allowSpectators = value;
			if (!value) {
				StopSpectators();
			}
		}
	}
	private bool? allowSpectators;

	// Whether the game is in the relay's public list while hosting online,
	// under this name and description (see CurrentListing); and its map's
	// size, as the list shows it.
	public bool ListPublicly { get; set; }
	public string PublicName { get; set; }
	public string PublicDescription { get; set; }
	public string MapSize { get; set; }
	public string DefaultPublicName => $"{hostName}'s game";

	// The name the host goes by, which everyone sees.
	public string HostName {
		get => hostName;
		set {
			string name = TidyName(value);
			if (name != null && name != hostName) {
				hostName = name;
				BroadcastLobby();
			}
		}
	}

	// The last snapshot handed to be encoded for each view of the game (see
	// ViewKey). Each is encoded after the one before it for the same view,
	// so it can reuse that one's encoding when nothing changed.
	private readonly Dictionary<string, Task<EncodedSnapshot>> lastEncodings = new();

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

	// Whether each guest's machine is sent only what its players may know of
	// the game (see SnapshotFilter), rather than the whole of it. Spectators
	// always see everything.
	public bool HideUnseen {
		get => hideUnseen;
		set {
			if (hideUnseen != value) {
				hideUnseen = value;
				if (!AllowSpectators) {
					// They would see what the guests are no longer sent.
					StopSpectators();
				}
				BroadcastLobby();
			}
		}
	}
	private bool hideUnseen = true;

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

	// The host's link to an online relay, for guests joining with a join
	// code; null unless hosting online.
	public RelayHostLink Online { get; private set; }

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

	// The spectators with their IDs, which tell apart two with the same name.
	public IReadOnlyList<SpectatorInfo> SpectatorList => spectators.Select(s => new SpectatorInfo(s.id, s.name)).ToList();
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
		: this(hostName, RequireHostPlayer(save).id, RequireHostPlayer(save).civilization, SeatsFor(save), null, port, answerDiscovery,
			save.TurnNumber) {
		if (save.Map?.tilesWide > 0) {
			MapSize = $"{save.Map.tilesWide}x{save.Map.tilesTall}";
		}
	}

	// Hosts a game again from its autosave, with the settings it had, and
	// with each guest's seats held for them until they come back with their
	// token. The host starts it from the lobby as usual.
	public static LanHost Resume(string hostName, SaveGame save, LanResumeInfo info, int? port = null, bool answerDiscovery = true) {
		LanHost host = new(hostName, save, port ?? info.port, answerDiscovery) {
			TurnTimeLimit = info.turnSeconds is double seconds ? TimeSpan.FromSeconds(seconds) : null,
			SimultaneousTurns = info.simultaneousTurns,
			HideUnseen = info.hideUnseen,
			resumed = true,
			passwordSalt = info.passwordVerifier == null ? null : info.passwordSalt,
			passwordVerifier = info.passwordSalt == null ? null : info.passwordVerifier,
			resumedRelayBans = info.relayBans,
			// A game saved before ban scopes has its bans made with its
			// room's code, and keeps them so.
			relayBanScope = RelayProtocol.IsBanScope(info.relayBanScope) ? info.relayBanScope
				: info.relayBans?.Count > 0 ? null : NewToken(),
			allowSpectators = info.allowSpectators,
			ListPublicly = info.listPublicly,
			PublicName = info.publicName,
			PublicDescription = info.publicDescription,
		};
		host.bannedTokens.UnionWith(info.bannedTokens ?? []);
		host.bannedAddresses.UnionWith(info.bannedAddresses ?? []);
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
			playable.Where(c => !c.isBarbarian).ToList(), port, answerDiscovery, 0) {
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
		List<Civilization> choosable, int port, bool answerDiscovery, int lobbyTurn) {
		this.hostName = TidyName(hostName) ?? UnnamedGuest;
		this.lobbyTurn = lobbyTurn;
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

	// Takes guests through an online relay too, alongside those on the LAN.
	// With the code and key of a room this host had, as in a resumed game,
	// it claims that room again, so its guests find it where they left it.
	//
	// Hosting online again in a new room, as when the old one couldn't be
	// claimed again, the guests banned before stay banned, and the old
	// room's guests are told the new code, to follow the game there.
	public RelayHostLink HostOnline(string relayUrl, string code = null, string key = null) {
		RelayHostLink previous = Online;
		previous?.Dispose();
		List<string> bans = previous == null ? resumedRelayBans : [.. previous.Bans];
		bool moving = code == null && previous?.Code != null && OnlineRelay.SameRelay(previous.RelayUrl, relayUrl);
		Online = new RelayHostLink(relayUrl, accepted.Enqueue, code, key, bans, relayBanScope,
			moving ? previous.Code : null, moving ? previous.Key : null);
		Online.Start();
		return Online;
	}

	// Whether guests need the password to join.
	public bool HasPassword => passwordVerifier != null;

	// Sets the game's password, or takes it away for a blank one. Guests
	// already in keep their seats, and come back to them without it.
	public void SetPassword(string password) {
		if (string.IsNullOrEmpty(password)) {
			passwordSalt = null;
			passwordVerifier = null;
		} else {
			passwordSalt = GamePassword.NewSalt();
			passwordVerifier = GamePassword.Verifier(password, passwordSalt);
		}
		PublishDiscoveryReply();
	}

	// The game as the relay's public list shows it.
	public GameListing CurrentListing() {
		string name = string.IsNullOrWhiteSpace(PublicName) ? DefaultPublicName : PublicName.Trim();
		string description = string.IsNullOrWhiteSpace(PublicDescription) ? null : PublicDescription.Trim();
		int turn = Started && EngineStorage.gameData != null ? EngineStorage.gameData.turn : lobbyTurn;
		string locale = System.Globalization.CultureInfo.CurrentCulture.Name;
		return new GameListing(name, description, HasPassword,
			seats.Count + 1, seats.Count(s => s.IsTaken || s.IsHeld) + 1, AllowSpectators, turn, Started,
			MapSize, SimultaneousTurns, TurnTimeLimit?.TotalSeconds, HideUnseen, hostName, locale == "" ? null : locale);
	}

	private void AcceptLoop() {
		while (!disposed) {
			try {
				accepted.Enqueue(LanTransport.Tcp(listener.AcceptTcpClient()));
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
		if (current == null || current.openSeats != openSeats || current.started != Started || current.hasPassword != HasPassword
			|| current.hostName != hostName) {
			discoveryReply = new DiscoveryReply(hostName, Port, openSeats, Started, HasPassword);
		}
	}

	// The game as it stands, taken once and encoded for each view of it that
	// is asked for: the whole game, or what a guest's players may know of it.
	private sealed class SnapshotRound {
		public readonly SaveGame snapshot = LanProtocol.SnapshotForPeers(EngineStorage.gameData);
		public readonly Dictionary<string, Task<EncodedSnapshot>> encodings = new();
		// Shared by the views of the round.
		public Dictionary<ID, string> ownTerritory;
	}

	// What a guest is sent: "" for the whole game, as spectators are, or the
	// IDs of the guest's players, whose view is filtered to what they know.
	private string ViewKey(Guest guest) {
		return hideUnseen ? string.Join(",", SeatsOf(guest).Select(s => s.info.playerID.ToString()).Order()) : "";
	}

	// The round's snapshot as the guest may see it, or whole for null,
	// encoded on a worker thread. What the guest's players know is worked
	// out here, on the main thread, and the snapshot filtered to it on the
	// worker thread.
	private Task<EncodedSnapshot> EncodeSnapshot(SnapshotRound round, Guest guest = null) {
		string key = guest == null ? "" : ViewKey(guest);
		if (round.encodings.TryGetValue(key, out Task<EncodedSnapshot> encoding)) {
			return encoding;
		}
		SnapshotFilter.View view = key == ""
			? null
			: SnapshotFilter.ViewOf(EngineStorage.gameData, SeatsOf(guest).Select(s => s.info.playerID),
				round.ownTerritory ??= SnapshotFilter.OwnTerritory(EngineStorage.gameData));
		SaveGame snapshot = round.snapshot;
		Task<EncodedSnapshot> previous = lastEncodings.GetValueOrDefault(key);
		encoding = Task.Run(async () => {
			EncodedSnapshot before = null;
			if (previous != null) {
				try {
					before = await previous;
				} catch (Exception) {
					// The connections waiting on it have logged why.
				}
			}
			return LanProtocol.EncodeSnapshot(view == null ? snapshot : SnapshotFilter.Filter(snapshot, view), before);
		});
		round.encodings[key] = encoding;
		lastEncodings[key] = encoding;
		return encoding;
	}

	// Forgets the views nobody is sent any more, such as a departed guest's.
	private void ForgetUnusedViews() {
		HashSet<string> used = [.. SeatedGuests().Select(ViewKey), ""];
		foreach (string key in lastEncodings.Keys.Where(k => !used.Contains(k)).ToList()) {
			lastEncodings.Remove(key);
		}
	}

	private bool EncodingSnapshot => lastEncodings.Values.Any(e => !e.IsCompleted);

	// The hash of the snapshot a guest with these players would be sent of
	// the game as it stands (the whole game for null), for tests.
	internal static byte[] SnapshotHashFor(IEnumerable<ID> playerIDs) {
		SaveGame snapshot = LanProtocol.SnapshotForPeers(EngineStorage.gameData);
		if (playerIDs != null) {
			snapshot = SnapshotFilter.Filter(snapshot, SnapshotFilter.ViewOf(EngineStorage.gameData, playerIDs));
		}
		return SHA256.HashData(snapshot.ToCompactJSON());
	}

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

		SnapshotRound round = new();
		foreach (Guest guest in SeatedGuests()) {
			SendStart(guest, round);
		}
		foreach (Spectator spectator in spectators) {
			SendStart(spectator, EncodeSnapshot(round));
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
	private void SendStart(Guest guest, SnapshotRound round) {
		guest.connection.Send(FrameKind.Start, new StartInfo(SeatsOf(guest).Select(s => s.info.playerID).ToList(), guest.token));
		SendSnapshot(guest.connection, EncodeSnapshot(round, guest), guest.pendingUiMessages);
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
		spectator.connection.Send(FrameKind.Start, new StartInfo([], spectator.token));
		spectator.connection.SendSnapshot(snapshot);
	}

	public void Poll() {
		// What guests do here tells everyone of the lobby once, at the end.
		deferLobby = true;
		try {
			PollPeers();
		} finally {
			deferLobby = false;
		}
		if (lobbyChanged) {
			BroadcastLobby();
		}

		if (Started) {
			UpdateTurnClock();
			MaybeSendSnapshot();
		}
		PublishDiscoveryReply();
		Online?.SetListing(ListPublicly ? CurrentListing() : null);
		SaveResumeInfoIfChanged();
	}

	private void PollPeers() {
		while (accepted.TryDequeue(out LanTransport transport)) {
			if (!MakeRoomFor(transport)) {
				transport.Dispose();
				continue;
			}
			LanConnection connection = new(transport, maxFrameBytes: LanProtocol.MaxFrameBytesBeforeAdmission) {
				maxReceivedFrames = MaxReceivedFramesPerPeer,
			};
			if (transport.RemoteHost != null && bannedAddresses.Contains(transport.RemoteHost)) {
				log.Information("Turned away {Address}, which is banned", transport.RemoteAddress);
				connection.Send(FrameKind.Rejected, Encoding.UTF8.GetBytes(BannedReason));
				connection.Dispose();
				continue;
			}
			guests.Add(new Guest { connection = connection });
		}

		foreach (Guest guest in guests.ToList()) {
			PollGuest(guest);
		}
		DropGuestsTooSlowToJoin();
		foreach (Spectator spectator in spectators.ToList()) {
			PollSpectator(spectator);
		}
	}

	// Whether a new connection may join the guests without a seat: there's
	// room for it, or the one among them waiting longest makes room, unless
	// its address on the network has as many as it may.
	private bool MakeRoomFor(LanTransport transport) {
		List<Guest> unseated = guests.Where(g => !g.connection.IsClosed && !SeatsOf(g).Any()).ToList();
		string address = transport.RemoteHost;
		if (address != null && !IsLoopback(address)
			&& unseated.Count(g => g.connection.Transport.RemoteHost == address) >= MaxUnseatedGuestsPerAddress) {
			log.Information("Turned away {Address}, which has too many connections waiting", transport.RemoteAddress);
			return false;
		}
		if (unseated.Count >= MaxUnseatedGuests) {
			// Those not yet let in go first.
			Guest longest = unseated.OrderBy(g => g.admitted).ThenByDescending(g => g.waiting.Elapsed).First();
			log.Information("Too many guests are waiting, dropping {Name}", longest.name ?? longest.connection.RemoteAddress);
			Reject(longest, "The host has too many players waiting to join. Try again in a moment.");
		}
		return true;
	}

	// Drops the guests that haven't said hello, or given the password, in
	// time.
	private void DropGuestsTooSlowToJoin() {
		foreach (Guest guest in guests.Where(g => !g.admitted).ToList()) {
			TimeSpan allowed = guest.nonce != null ? PasswordTimeout : HelloTimeout;
			if (guest.waiting.Elapsed > allowed) {
				log.Information("{Name} took too long to join, dropping them", guest.name ?? guest.connection.RemoteAddress);
				Reject(guest, guest.nonce != null ? "Took too long to give the password." : "Took too long to say hello.");
			}
		}
	}

	private static bool IsLoopback(string address) {
		return IPAddress.TryParse(address, out IPAddress ip) && IPAddress.IsLoopback(ip);
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
		savedResumeInfo = NetSerialization.SerializeData(info);
		string directory = AutosaveDirectory;
		LastAutosave = LastAutosave.ContinueWith(_ => LanAutosave.Write(directory, save, info));
	}

	// Between autosaves, what resuming needs is saved again whenever it
	// changes (seats taken, guests banned, the password, the join code and
	// so on), at most this often, so that the host doesn't lose it if its
	// game ends mid-turn.
	private static readonly TimeSpan ResumeInfoSaveInterval = TimeSpan.FromSeconds(1);
	private readonly Stopwatch sinceResumeInfoChecked = Stopwatch.StartNew();
	private byte[] savedResumeInfo;

	private void SaveResumeInfoIfChanged() {
		// Only beside the game it's for: one this host autosaved, or the
		// one it resumed.
		if (AutosaveDirectory == null || (autosavedTurn < 0 && !resumed) || sinceResumeInfoChecked.Elapsed < ResumeInfoSaveInterval) {
			return;
		}
		sinceResumeInfoChecked.Restart();
		LanResumeInfo info = ResumeInfo();
		byte[] json = NetSerialization.SerializeData(info);
		if (savedResumeInfo != null && json.AsSpan().SequenceEqual(savedResumeInfo)) {
			return;
		}
		savedResumeInfo = json;
		string directory = AutosaveDirectory;
		LastAutosave = LastAutosave.ContinueWith(_ => LanAutosave.WriteResumeInfo(directory, info));
	}

	// How to host this game again, with the seats as they are now.
	public LanResumeInfo ResumeInfo() {
		return new LanResumeInfo(hostName, Port, TurnTimeLimit?.TotalSeconds, SimultaneousTurns,
			seats.Where(s => s.token != null).Select(s => new LanResumeSeat(s.info.playerID, s.takenBy, s.token)).ToList(),
			Online?.RelayUrl, Online?.Code, Online?.Key, HideUnseen,
			passwordSalt, passwordVerifier, [.. bannedTokens], [.. bannedAddresses], Online == null ? resumedRelayBans : [.. Online.Bans],
			ListPublicly, PublicName, PublicDescription, allowSpectators, relayBanScope);
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

	// Removes the guest in the seat from the game: its connection is closed,
	// and its seats freed for anyone to take, with its token no longer good
	// for them. The guest may join again as anyone may, unless it's banned,
	// which also turns its token away, and its address on the network or
	// through the relay. Works for a guest whose seat is held for it, too.
	//
	// occupant, if given, is the seat's OccupantOf when the host chose to do
	// this; if someone else has the seat now, nothing is done.
	public bool Kick(ID playerID, bool ban = false, string occupant = null) {
		Seat seat = seats.Find(s => s.info.playerID == playerID);
		if (seat == null || (seat.guest == null && seat.token == null)) {
			return false;
		}
		if (occupant != null && OccupantOf(playerID) != occupant) {
			log.Information("Not removing whoever is in the seat of {Player} now, who isn't the one the host meant", playerID);
			return false;
		}
		Guest guest = seat.guest != null && guests.Contains(seat.guest) ? seat.guest : null;
		string token = seat.token ?? guest?.token;
		log.Information(ban ? "Banning {Name}" : "Removing {Name} from the game", seat.takenBy);
		if (ban) {
			if (token != null) {
				bannedTokens.Add(token);
			}
			(guest == null ? seat.banAddress : BanAddress(guest))?.Invoke();
		}
		foreach (Seat theirs in seats.Where(s => s == seat || (guest != null && s.guest == guest) || (token != null && s.token == token)).ToList()) {
			FreeSeat(theirs);
		}
		if (guest != null) {
			Reject(guest, ban ? BannedReason : KickedReason);
		}
		BroadcastLobby();
		return true;
	}

	// Who is in the seat, or holds it, as something only this host can tell
	// apart from anyone else in it later; null for nobody.
	public string OccupantOf(ID playerID) {
		Seat seat = seats.Find(s => s.info.playerID == playerID);
		string token = seat?.token ?? seat?.guest?.token;
		return token == null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
	}

	// Stops a spectator watching, and with ban, turns away its token and
	// address.
	public bool KickSpectator(int id, bool ban = false) {
		Spectator spectator = spectators.Find(s => s.id == id);
		if (spectator == null) {
			return false;
		}
		log.Information(ban ? "Banning {Name}" : "Removing {Name} from the game", spectator.name);
		spectatorTokens.Remove(spectator.token);
		if (ban) {
			if (spectator.token != null) {
				bannedTokens.Add(spectator.token);
			}
			BanAddress(spectator.connection)?.Invoke();
		}
		StopWatching(spectator, ban ? BannedReason : KickedReason);
		BroadcastLobby();
		return true;
	}

	// The same for the first spectator with the name.
	public bool KickSpectator(string name, bool ban = false) {
		Spectator spectator = spectators.Find(s => s.name == name);
		return spectator != null && KickSpectator(spectator.id, ban);
	}

	private void StopWatching(Spectator spectator, string reason) {
		spectator.connection.Send(FrameKind.Rejected, Encoding.UTF8.GetBytes(reason));
		spectator.connection.Dispose();
		spectators.Remove(spectator);
	}

	// Stops everyone watching, once they may not.
	private void StopSpectators() {
		if (spectators.Count == 0) {
			return;
		}
		log.Information("Spectators may no longer watch, stopping {Count} watching", spectators.Count);
		foreach (Spectator spectator in spectators.ToList()) {
			StopWatching(spectator, NoSpectatorsReason);
		}
		BroadcastLobby();
	}

	public const string NoSpectatorsReason = "The host doesn't let anyone watch this game.";

	private Action BanAddress(Guest guest) => BanAddress(guest.connection);

	// What bans the address a connection comes from: here, for one on the
	// network, or at the relay for one through it. A connection from this
	// machine is never banned by address, which would ban everyone else on
	// it too; its token still is.
	private Action BanAddress(LanConnection connection) {
		LanTransport transport = connection.Transport;
		if (transport.RemoteHost == null) {
			return transport.BanAtRelay;
		}
		string address = transport.RemoteHost;
		if (IsLoopback(address)) {
			return null;
		}
		return () => bannedAddresses.Add(address);
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
		while (guest.connection.TryPeek(out _) && guest.frameBudget.Take() && guest.connection.TryReceive(out Frame frame)) {
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
				if (guest.name != null) {
					log.Warning("Ignoring another hello from {Name}", guest.name);
					return true;
				}
				HelloInfo hello = NetSerialization.DeserializeRequired<HelloInfo>(frame.payload);
				if (hello.version != LanProtocol.Version) {
					Reject(guest, $"The host is running a different version of the game (protocol {LanProtocol.Version}, yours is {hello.version}).");
					return false;
				}
				guest.name = TidyName(hello.playerName) ?? UnnamedGuest;
				if (hello.reconnectToken != null) {
					if (bannedTokens.Contains(hello.reconnectToken)) {
						log.Information("Turned away {Name}, who is banned", guest.name);
						Reject(guest, BannedReason);
						return false;
					}
					// Back to their seats, which needs no password, or to
					// watch, which they ask for next.
					if (spectatorTokens.Contains(hello.reconnectToken) && !seats.Any(s => s.token == hello.reconnectToken)) {
						return Admit(guest, hello.reconnectToken);
					}
					if (!Reattach(guest, hello.reconnectToken)) {
						Reject(guest, "The host is no longer keeping a seat for you.");
						return false;
					}
					return true;
				}
				if (HasPassword) {
					if (PasswordLockout(guest) is TimeSpan wait) {
						Reject(guest, $"Too many wrong passwords from your address. Try again in {Math.Ceiling(wait.TotalMinutes):0} "
							+ $"{(wait.TotalMinutes <= 1 ? "minute" : "minutes")}.");
						return false;
					}
					AskForPassword(guest, false);
					return true;
				}
				return Admit(guest);
			case FrameKind.Password:
				if (guest.admitted || guest.nonce == null) {
					return true;
				}
				PasswordInfo answer = NetSerialization.DeserializeRequired<PasswordInfo>(frame.payload);
				// The host may have taken the password away meanwhile.
				if (!HasPassword || GamePassword.Check(passwordVerifier, guest.nonce, answer.proof)) {
					if (guest.connection.Transport.AddressKey is string key) {
						passwordFailures.Remove(key);
					}
					return Admit(guest);
				}
				log.Information("{Name} gave the wrong password", guest.name);
				if (NoteWrongPassword(guest)) {
					Reject(guest, "Too many wrong passwords.");
					return false;
				}
				AskForPassword(guest, true);
				return true;
			case FrameKind.ClaimSeat:
				if (!guest.admitted) {
					Reject(guest, "Say hello before claiming a seat.");
					return false;
				}
				ClaimSeat(guest, NetSerialization.DeserializeRequired<ClaimSeatInfo>(frame.payload));
				return true;
			case FrameKind.LeaveSeat:
				LeaveSeat(guest, NetSerialization.DeserializeRequired<ClaimSeatInfo>(frame.payload));
				return true;
			case FrameKind.Watch:
				if (!guest.admitted && guest.nonce != null) {
					// Once they have given the password.
					guest.watchOnceAdmitted = true;
					return true;
				}
				if (!guest.admitted) {
					Reject(guest, "Say hello before watching.");
					return false;
				}
				return !StartWatching(guest);
			case FrameKind.ChooseCivilization:
				ChooseCivilization(guest, frame);
				return true;
			case FrameKind.Command:
			case FrameKind.PredictedCommand:
				HandleCommand(guest, frame);
				return true;
			case FrameKind.RequestSnapshot:
				if (Started && SeatsOf(guest).Any()) {
					guest.wholeSnapshot.pending = true;
				}
				return true;
			default:
				// Counted as a frame that can't be read.
				throw new InvalidDataException($"A guest doesn't send {frame.kind} frames");
		}
	}

	// Asks the guest for the game's password, with a new nonce to sign.
	private void AskForPassword(Guest guest, bool wrong) {
		guest.nonce = GamePassword.NewNonce();
		guest.waiting.Restart();
		guest.connection.Send(FrameKind.PasswordRequired, new PasswordChallengeInfo(passwordSalt, guest.nonce, wrong,
			GamePassword.MaxWrongAttempts - WrongPasswords(guest)));
	}

	// The wrong passwords given lately from where the guest is: its address
	// on the network, or the key the relay gave for it; or failing that, on
	// its connection.
	private int WrongPasswords(Guest guest) {
		string key = guest.connection.Transport.AddressKey;
		return key == null ? guest.wrongPasswords : passwordFailures.GetValueOrDefault(key)?.count ?? 0;
	}

	// Counts a wrong password, and returns whether that was one too many,
	// which keeps its address from trying again for a while, longer each
	// time; so connecting again doesn't give anyone more tries.
	private bool NoteWrongPassword(Guest guest) {
		string key = guest.connection.Transport.AddressKey;
		if (key == null) {
			return ++guest.wrongPasswords >= GamePassword.MaxWrongAttempts;
		}
		if (!passwordFailures.TryGetValue(key, out PasswordFailures failures)) {
			if (passwordFailures.Count >= MaxPasswordFailureAddresses) {
				// Forget those that may try again.
				long now = Environment.TickCount64;
				foreach (string old in passwordFailures.Where(f => f.Value.lockedUntil <= now).Select(f => f.Key).ToList()) {
					passwordFailures.Remove(old);
				}
			}
			failures = passwordFailures[key] = new PasswordFailures();
		}
		if (++failures.count < GamePassword.MaxWrongAttempts) {
			return false;
		}
		failures.count = 0;
		failures.lockouts++;
		double seconds = Math.Min(GamePassword.FirstLockout.TotalSeconds * Math.Pow(2, failures.lockouts - 1),
			GamePassword.MaxLockout.TotalSeconds);
		failures.lockedUntil = Environment.TickCount64 + (long)(seconds * 1000);
		log.Information("Too many wrong passwords from {Name}, turning their address away for {Seconds} seconds", guest.name, seconds);
		return true;
	}

	// How long the guest's address is still kept from trying the password,
	// or null if it isn't.
	private TimeSpan? PasswordLockout(Guest guest) {
		string key = guest.connection.Transport.AddressKey;
		if (key == null || !passwordFailures.TryGetValue(key, out PasswordFailures failures)) {
			return null;
		}
		long left = failures.lockedUntil - Environment.TickCount64;
		return left > 0 ? TimeSpan.FromMilliseconds(left) : null;
	}

	// Lets the guest in, with a token for the seats it takes, and shows it
	// the lobby; or has it watch, if it asked to meanwhile. Returns whether
	// it's still a guest.
	// A spectator coming back keeps its token.
	private bool Admit(Guest guest, string token = null) {
		guest.admitted = true;
		guest.connection.MaxFrameBytes = LanProtocol.MaxGuestFrameBytes;
		guest.connection.Transport.AdmittedAtRelay?.Invoke();
		guest.nonce = null;
		guest.token = token ?? NewToken();
		if (guest.watchOnceAdmitted) {
			return !StartWatching(guest);
		}
		SendLobby(guest);
		return true;
	}

	// The guest watches the game from now on, rather than playing. Returns
	// false if it can't, being seated.
	private bool StartWatching(Guest guest) {
		if (SeatsOf(guest).Any()) {
			log.Warning("Ignoring a request to watch from {Name}, who has a seat", guest.name);
			return false;
		}
		if (!AllowSpectators) {
			Reject(guest, NoSpectatorsReason);
			return true;
		}
		// One coming back may not have been noticed to have gone.
		foreach (Spectator old in spectators.Where(s => s.token == guest.token).ToList()) {
			old.connection.Dispose();
			spectators.Remove(old);
		}
		if (spectators.Count(s => !s.connection.IsClosed) >= MaxSpectators) {
			Reject(guest, "The game has as many spectators as it takes.");
			return true;
		}
		guests.Remove(guest);
		Spectator spectator = new() {
			id = nextSpectatorID++, connection = guest.connection, name = guest.name, token = guest.token, frameBudget = guest.frameBudget,
		};
		spectators.Add(spectator);
		spectatorTokens.Remove(guest.token);
		spectatorTokens.Add(guest.token);
		if (spectatorTokens.Count > MaxSpectatorTokens) {
			spectatorTokens.RemoveAt(0);
		}
		log.Information("{Name} is watching", guest.name);
		if (Started) {
			SendStart(spectator, EncodeSnapshot(new SnapshotRound()));
		}
		BroadcastLobby();
		return true;
	}

	// A guest takes an open seat, in addition to any they have already, or
	// renames the player in one of theirs before the game starts.
	private void ClaimSeat(Guest guest, ClaimSeatInfo claim) {
		Seat seat = seats.Find(s => s.info.playerID == claim.playerID);
		if (seat != null && seat.guest == guest && seat.IsTaken) {
			string renamed = Started || creatingGame ? seat.takenBy : SeatPlayerName(guest, claim.playerName);
			if (renamed == seat.takenBy) {
				SendLobby(guest);
				return;
			}
			seat.takenBy = renamed;
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
		seat.banAddress = BanAddress(guest);
		log.Information("{Name} took the seat of {Player} for {SeatName}", guest.name, seat.info.playerID, seat.takenBy);
		if (Started) {
			// A player rejoining a game in progress, or taking another seat,
			// which changes what they may see.
			SendStart(guest, new SnapshotRound());
			ResendTurnPrompts(seat);
		}
		BroadcastLobby();
	}

	// The name of the player in a seat a guest takes: the one given, or the
	// guest's own.
	private static string SeatPlayerName(Guest guest, string playerName) {
		return TidyName(playerName) ?? guest.name;
	}

	// A name a guest gave, as everyone is shown it: without control or
	// formatting characters, which could hide or reorder what's shown, and
	// no longer than MaxPlayerNameLength; or null if nothing is left.
	internal static string TidyName(string name) => RelayText.Tidy(name, MaxPlayerNameLength);

	public const int MaxPlayerNameLength = 40;

	// What a guest that gave no name is called. Never its address, which
	// everyone else would see.
	public const string UnnamedGuest = "Guest";

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
		seat.banAddress = null;
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
		guest.admitted = true;
		guest.connection.MaxFrameBytes = LanProtocol.MaxGuestFrameBytes;
		guest.connection.Transport.AdmittedAtRelay?.Invoke();
		int turn = EngineStorage.gameData?.turn ?? -1;
		foreach (Seat seat in theirs) {
			seat.guest = guest;
			seat.away = false;
			seat.banAddress = BanAddress(guest);
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
			SendStart(guest, new SnapshotRound());
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
		// Guests act only as their own players, so one without a seat has
		// nothing to say, and isn't listened to.
		List<Seat> theirs = SeatsOf(guest).ToList();
		if (!Started || theirs.Count == 0) {
			return;
		}
		MessageToEngine msg = NetSerialization.DeserializeRequired<MessageToEngine>(frame.payload);
		if (msg.IsLocal) {
			return;
		}
		// One with a single seat always acts as it; one with several says
		// which.
		if (theirs.Count == 1) {
			msg.playerID = theirs[0].info.playerID;
		} else if (!theirs.Any(s => s.info.playerID == msg.playerID)) {
			log.Warning("Ignoring {Message} from {Name} for {Player}, who isn't theirs", msg.GetType().Name, guest.name, msg.playerID);
			return;
		}
		msg.DistrustRemoteSender(id => theirs.Any(s => s.info.playerID == id));
		EngineStorage.ReceiveFromRemote(msg);
		guest.awaitsAnswer |= frame.kind == FrameKind.PredictedCommand;
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
		if (available && seat.info.civilization != civilization) {
			seat.info = seat.info with { civilization = civilization };
			log.Information("{Name} chose {Civilization}", seat.takenBy, civilization ?? "a random civilization");
			BroadcastLobby();
		} else {
			// The guest hears how things stand.
			SendLobby(guest);
		}
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

	// Spectators only listen: whatever they send is dropped, except asking
	// for the whole game.
	private void PollSpectator(Spectator spectator) {
		while (spectator.connection.TryPeek(out _) && spectator.frameBudget.Take() && spectator.connection.TryReceive(out Frame frame)) {
			if (frame.kind == FrameKind.RequestSnapshot && Started) {
				spectator.wholeSnapshot.pending = true;
			}
		}
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
		foreach (Guest guest in SeatedGuests()) {
			snapshotPending |= guest.wholeSnapshot.TakeDue(guest.connection);
		}
		foreach (Spectator spectator in spectators.Where(s => !s.connection.IsClosed)) {
			spectatorSnapshotPending |= spectator.wholeSnapshot.TakeDue(spectator.connection);
		}
		if (EncodingSnapshot) {
			return;
		}
		bool settled = !EngineStorage.HasPendingMessagesToEngine() && sinceChange.Elapsed >= SnapshotDelay;
		SnapshotRound round = null;

		if (snapshotPending && (settled || sinceSnapshot.Elapsed >= MaxSnapshotDelay)) {
			snapshotPending = false;
			sinceSnapshot.Restart();
			List<Guest> connected = SeatedGuests();
			if (connected.Count > 0) {
				round = new SnapshotRound();
				// Orders the engine is yet to carry out are answered by a
				// later snapshot.
				bool answered = !EngineStorage.HasPendingMessagesToEngine();
				foreach (Guest guest in connected) {
					SendSnapshot(guest.connection, EncodeSnapshot(round, guest), guest.pendingUiMessages, guest.awaitsAnswer && answered);
					guest.awaitsAnswer &= !answered;
				}
				ForgetUnusedViews();
			}
		}

		if (spectatorSnapshotPending && sinceSpectatorSnapshot.Elapsed >= SpectatorSnapshotInterval) {
			spectatorSnapshotPending = false;
			sinceSpectatorSnapshot.Restart();
			List<Spectator> watching = spectators.Where(s => !s.connection.IsClosed).ToList();
			if (watching.Count > 0) {
				round ??= new SnapshotRound();
				Task<EncodedSnapshot> snapshot = EncodeSnapshot(round);
				foreach (Spectator spectator in watching) {
					SendSnapshot(spectator.connection, snapshot, spectator.pendingUiMessages);
				}
			}
		}
	}

	// A client couldn't apply a patch, and has lost track of the game until
	// it's sent the whole of it.
	private static void RequestWholeSnapshot(LanConnection connection) {
		log.Information("{Address} asked for the whole game", connection.RemoteAddress);
		connection.SendWholeSnapshotNext();
	}

	private static void SendSnapshot(LanConnection connection, Task<EncodedSnapshot> snapshot, List<byte[]> pendingUiMessages,
		bool evenIfSame = false) {
		connection.SendSnapshot(snapshot, evenIfSame);
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
			SimultaneousTurns, reconnectToken, HideUnseen));
	}

	// While Poll handles what guests send, the lobby is sent once at the
	// end rather than for each thing they do.
	private bool deferLobby;
	private bool lobbyChanged;

	private void BroadcastLobby() {
		if (deferLobby) {
			lobbyChanged = true;
			return;
		}
		lobbyChanged = false;
		foreach (Guest guest in guests.Where(g => g.admitted)) {
			SendLobby(guest);
		}
		foreach (Spectator spectator in spectators) {
			SendLobby(spectator.connection, [], spectator.token);
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
		Online?.Dispose();
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

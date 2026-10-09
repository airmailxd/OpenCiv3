using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using C7Relay;
using Serilog;

namespace C7Engine.Network;

// A LAN host's connection to an online relay, through which guests anywhere
// join it with a join code (see RelayProtocol). Each guest that joins becomes
// a LanTransport for the host to take in like one that connected over TCP.
//
// Everything runs on worker threads. A host that loses the relay keeps
// trying to connect again, claiming its room with its code and key, and its
// guests, whose connections went with it, connect again by themselves.
//
// Each guest's connection queues what it sends here, and the link sends the
// oldest first; when other guests have the very same bytes next, as they
// usually do for snapshots and turn clocks, it sends them once, addressed to
// all of them. It waits a moment for the others' copies before sending, since
// each connection writes on its own thread.
//
// The link also lists the host's game in the relay's public list, when the
// host asks it to (see SetListing), and asks the relay to ban guests,
// keeping the keys the relay gives for them to ban them again whenever it
// connects.
public sealed class RelayHostLink : IDisposable {
	private static readonly ILogger log = Log.ForContext<RelayHostLink>();

	public enum LinkState { Connecting, Online, Reconnecting, Failed }

	public string RelayUrl { get; }

	// The room's join code, once the relay has given it, and the key for
	// claiming it again.
	public string Code => code;
	public string Key => key;
	public string FormattedCode => RelayProtocol.FormatCode(code);
	private volatile string code;
	private volatile string key;

	public LinkState State => state;
	private volatile LinkState state = LinkState.Connecting;

	// Why the last try to reach the relay failed, or why the link gave up;
	// null while online.
	public string Error => error;
	private volatile string error;

	// The game as last listed publicly, or null when it isn't; and whether
	// the relay has listed it, or why it wouldn't.
	public GameListing Listing {
		get {
			lock (sync) {
				return listing;
			}
		}
	}
	public bool IsListed => Listing != null && listedByRelay;
	public string ListingError => listingError;
	private GameListing listing;
	private long listingSentAt;
	private volatile bool listedByRelay;
	private volatile string listingError;

	// The keys of the guests' addresses banned from the room (see
	// RelayProtocol), to give the relay whenever the link connects.
	public IReadOnlyList<string> Bans {
		get {
			lock (sync) {
				return [.. bans];
			}
		}
	}
	private readonly List<string> bans = [];

	// How many guests are connected through the relay.
	public int GuestCount {
		get {
			lock (sync) {
				return guests.Count;
			}
		}
	}

	// For tests: the messages sent with data for guests, and those of them
	// that went to more than one.
	internal long DataMessagesSent => Interlocked.Read(ref dataMessagesSent);
	internal long FannedOutMessages => Interlocked.Read(ref fannedOutMessages);
	private long dataMessagesSent;
	private long fannedOutMessages;

	internal TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(1);
	internal TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);
	public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

	// How long the oldest message waits for the other guests' copies.
	internal TimeSpan FanOutWindow = TimeSpan.FromMilliseconds(5);

	// A guest's connection waits to queue more while this much is queued for
	// it, which leaves its LanConnection to merge the snapshots waiting.
	private const long MaxQueuedBytesPerGuest = 1024 * 1024;

	// What a guest sent that its connection hasn't read yet. A guest's
	// frames are small (see LanProtocol.MaxGuestFrameBytes) and read as they
	// come, so a guest with more than this waiting is dropped.
	private const long MaxIncomingBytesPerGuest = 4 * LanProtocol.MaxGuestFrameBytes;

	private readonly Action<LanTransport> guestArrived;
	private readonly CancellationTokenSource disposed = new();

	// Guards the guests, what's queued for them and the relay, and socket.
	private readonly object sync = new();
	private readonly Dictionary<uint, GuestStream> guests = new();
	private readonly Queue<(byte[] message, WebSocketMessageType type)> control = new();

	// The keys the relay gave for guests about to join (see RelayProtocol).
	private readonly Dictionary<uint, string> joiningKeys = new();
	private ClientWebSocket socket;
	private long nextSequence;

	// Released whenever there may be something new to send.
	private readonly SemaphoreSlim wake = new(0);

	// The secret the relay makes this game's ban keys with, and the room
	// the game moved from (see RelayProtocol).
	private readonly string banScope;
	private readonly string movedFromCode;
	private readonly string movedFromKey;

	// guestArrived is called on a worker thread for each guest that joins.
	// With the code and key of a room this host had, it claims that room
	// again, as when resuming a game, and with the keys of the guests it
	// banned, bans them again. Its ban keys are made with banScope, if
	// given, so that they're good in any room this game has; and a new room
	// tells the guests of the room the game had before, if given, where it
	// went.
	public RelayHostLink(string relayUrl, Action<LanTransport> guestArrived, string code = null, string key = null,
		IEnumerable<string> bans = null, string banScope = null, string movedFromCode = null, string movedFromKey = null) {
		RelayUrl = relayUrl;
		this.guestArrived = guestArrived;
		this.code = code;
		this.key = key;
		this.bans.AddRange(bans ?? []);
		this.banScope = banScope;
		this.movedFromCode = movedFromCode;
		this.movedFromKey = movedFromKey;
	}

	// Lists the game publicly as it is now, or takes it off the list for
	// null. The game is listed again only when it changes, and every so
	// often to keep it listed, so this can be called as often as the host
	// likes.
	public void SetListing(GameListing current) {
		lock (sync) {
			bool due = current != null
				&& Stopwatch.GetElapsedTime(listingSentAt) >= TimeSpan.FromSeconds(RelayProtocol.ListingRefreshSeconds);
			if (Equals(current, listing) && !due) {
				return;
			}
			bool wasListed = listing != null;
			listing = current;
			if (current == null) {
				listedByRelay = false;
				listingError = null;
				if (!wasListed) {
					return;
				}
			}
		}
		SendListing();
	}

	// Sends the listing, or takes it down.
	private void SendListing() {
		GameListing current;
		lock (sync) {
			current = listing;
			listingSentAt = Stopwatch.GetTimestamp();
		}
		RelayControl control = current == null ? new RelayControl(RelayControl.Unlist) : new RelayControl(RelayControl.List, listing: current);
		SendControl(control.ToBytes(), WebSocketMessageType.Text);
	}

	// Asks the relay to turn away the guest's address from now on, as for a
	// guest that has gone. With the key the relay gave for it, the ban is
	// kept to give the relay whenever the link connects, so it isn't lost
	// while the link is down.
	private void Ban(GuestStream guest) {
		log.Information("Banning guest {Guest} at the relay", guest.id);
		KeepBan(guest.key);
		SendControl(BanMessage(guest), WebSocketMessageType.Text);
	}

	private static byte[] BanMessage(GuestStream guest) {
		return new RelayControl(RelayControl.Ban, guest: guest.id, bans: guest.key == null ? null : [guest.key]).ToBytes();
	}

	private void KeepBan(string key) {
		if (key == null) {
			return;
		}
		lock (sync) {
			if (!bans.Contains(key)) {
				bans.Add(key);
			}
		}
	}

	// What the relay needs to hear on each new connection: the guests
	// banned, and the game's listing.
	private void Connected() {
		List<string> banned;
		bool listed;
		lock (sync) {
			banned = [.. bans];
			listed = listing != null;
		}
		if (banned.Count > 0) {
			SendControl(new RelayControl(RelayControl.Bans, bans: banned.TakeLast(RelayProtocol.MaxBans).ToList()).ToBytes(),
				WebSocketMessageType.Text);
		}
		listedByRelay = false;
		if (listed) {
			SendListing();
		}
	}

	public void Start() {
		Task.Run(Run);
	}

	private async Task Run() {
		CancellationToken cancel = disposed.Token;
		TimeSpan delay = FirstRetryDelay;
		while (!cancel.IsCancellationRequested) {
			try {
				(ClientWebSocket connected, RelayControl welcome) = await RelayConnection.ConnectAsync(
					RelayConnection.HostUri(RelayUrl, code, key, banScope, movedFromCode, movedFromKey), ConnectTimeout, cancel);
				code = welcome.code;
				key = welcome.key;
				error = null;
				state = LinkState.Online;
				delay = FirstRetryDelay;
				log.Information("Hosting online with the join code {Code}", FormattedCode);
				await RunConnection(connected, RelayConnection.QuietLimit(welcome.pingSeconds), cancel);
				if (!cancel.IsCancellationRequested) {
					error ??= "Lost the connection to the relay.";
					log.Information("Lost the connection to the relay: {Error}", error);
				}
			} catch (RelayException e) when (e.IsPermanent) {
				log.Warning("The relay won't have this host: {Error}", e.Message);
				error = e.Message;
				state = LinkState.Failed;
				return;
			} catch (OperationCanceledException) when (cancel.IsCancellationRequested) {
				return;
			} catch (Exception e) {
				error = e.Message;
				log.Debug("Couldn't reach the relay: {Error}", e.Message);
			}
			if (cancel.IsCancellationRequested) {
				return;
			}
			state = code == null ? LinkState.Connecting : LinkState.Reconnecting;
			try {
				await Task.Delay(delay, cancel);
			} catch (OperationCanceledException) {
				return;
			}
			delay = delay * 2 < MaxRetryDelay ? delay * 2 : MaxRetryDelay;
		}
	}

	// Passes messages both ways until the connection is lost or the link is
	// closed. Its guests go with it.
	private async Task RunConnection(ClientWebSocket connected, TimeSpan quietLimit, CancellationToken cancel) {
		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		lock (sync) {
			socket = connected;
		}
		Task sending = SendLoop(connected, stop.Token);
		Connected();
		try {
			await ReceiveLoop(connected, quietLimit, stop.Token);
		} catch (RelayException e) when (!e.IsPermanent) {
			error = e.Message;
		} catch (Exception e) when (e is WebSocketException or IOException or OperationCanceledException) {
			error = cancel.IsCancellationRequested ? null : e is OperationCanceledException ? "The relay stopped answering." : e.Message;
		} finally {
			stop.Cancel();
			List<GuestStream> gone;
			lock (sync) {
				socket = null;
				control.Clear();
				gone = [.. guests.Values];
				guests.Clear();
				Monitor.PulseAll(sync);
			}
			foreach (GuestStream guest in gone) {
				guest.RemoteClosed();
			}
			try {
				await sending;
			} catch (Exception) {
				// Gone with the connection.
			}
			connected.Abort();
			connected.Dispose();
		}
	}

	private async Task ReceiveLoop(ClientWebSocket connected, TimeSpan quietLimit, CancellationToken cancel) {
		WebSocketMessageReader reader = new();
		while (true) {
			WebSocketMessageType type;
			ReadOnlyMemory<byte> message;
			using (CancellationTokenSource quiet = CancellationTokenSource.CreateLinkedTokenSource(cancel)) {
				quiet.CancelAfter(quietLimit);
				(type, message) = await reader.ReadAsync(connected, LanProtocol.MaxFrameBytes + RelayProtocol.HeaderLength(1), quiet.Token);
			}
			if (type == WebSocketMessageType.Close) {
				throw RelayConnection.ClosedBy(connected);
			}
			if (type == WebSocketMessageType.Text) {
				FromRelay(RelayControl.Parse(message.Span));
				continue;
			}
			if (!RelayProtocol.TryReadHeader(message.Span, out byte kind, out int count)) {
				log.Warning("Ignoring a message from the relay that can't be read");
				continue;
			}
			for (int i = 0; i < count; ++i) {
				uint id = RelayProtocol.GuestAt(message.Span, i);
				switch (kind) {
					case RelayProtocol.Open:
						GuestJoined(id);
						break;
					case RelayProtocol.Data:
						GuestStream to;
						lock (sync) {
							guests.TryGetValue(id, out to);
						}
						to?.Deliver(RelayProtocol.Payload(message.Span, count).ToArray());
						break;
					case RelayProtocol.Close:
						GuestStream gone;
						lock (sync) {
							guests.Remove(id, out gone);
							Monitor.PulseAll(sync);
						}
						gone?.RemoteClosed();
						break;
				}
			}
		}
	}

	// What the relay says in text: its pings, and its answers about the
	// listing and bans.
	private void FromRelay(RelayControl control) {
		switch (control?.type) {
			case RelayControl.Ping:
				SendControl(new RelayControl(RelayControl.Pong).ToBytes(), WebSocketMessageType.Text);
				break;
			case RelayControl.Listed:
				listingError = control.error;
				listedByRelay = control.error == null && Listing != null;
				if (control.error != null) {
					log.Information("The relay didn't list the game: {Error}", control.error);
				}
				break;
			case RelayControl.Banned:
				foreach (string ban in control.bans ?? []) {
					KeepBan(ban);
				}
				break;
			case RelayControl.Guest:
				if (control.key is { Length: > 0 and <= 64 }) {
					lock (sync) {
						// Only the next few to join; anything more is stale.
						if (joiningKeys.Count > 64) {
							joiningKeys.Clear();
						}
						joiningKeys[control.guest] = control.key;
					}
				}
				break;
		}
	}

	private void GuestJoined(uint id) {
		GuestStream guest = new(this, id);
		lock (sync) {
			joiningKeys.Remove(id, out guest.key);
			guests[id] = guest;
		}
		log.Information("Guest {Guest} joined through the relay", id);
		guestArrived(new LanTransport(guest, $"online guest {id}") {
			BanAtRelay = guest.BanWhenClosed,
			AdmittedAtRelay = guest.Admitted,
			AddressKey = guest.key == null ? null : $"relay:{guest.key}",
		});
	}

	private void SendControl(byte[] message, WebSocketMessageType type) {
		lock (sync) {
			if (socket == null) {
				return;
			}
			control.Enqueue((message, type));
		}
		wake.Release();
	}

	// A message waiting to go to a guest; a null message closes the guest's
	// connection once what it was sent before has gone, or with ban, bans
	// the guest first.
	private sealed class Pending {
		public byte[] message;
		public bool ban;
		public long sequence;
		public long queuedAt;
	}

	// Queues what a guest's connection sends, once there's room for it.
	private void Queue(GuestStream guest, byte[] message) {
		lock (sync) {
			while (!guest.closed && socket != null && guest.queuedBytes > MaxQueuedBytesPerGuest) {
				Monitor.Wait(sync);
			}
			if (guest.closed || socket == null) {
				throw new IOException("The connection through the relay is closed");
			}
			guest.outgoing.Enqueue(new Pending { message = message, sequence = nextSequence++, queuedAt = Stopwatch.GetTimestamp() });
			guest.queuedBytes += message.Length;
		}
		wake.Release();
	}

	// The guest's connection is closing: the relay closes it once what was
	// queued for it has gone, banning it first if the host asked.
	private void Close(GuestStream guest) {
		bool banNow = false;
		lock (sync) {
			if (guest.closed) {
				return;
			}
			guest.closed = true;
			if (socket == null || !guests.ContainsKey(guest.id)) {
				guests.Remove(guest.id);
				banNow = guest.banOnClose;
			} else {
				guest.outgoing.Enqueue(new Pending {
					ban = guest.banOnClose, sequence = nextSequence++, queuedAt = Stopwatch.GetTimestamp(),
				});
				Monitor.PulseAll(sync);
			}
		}
		if (banNow) {
			Ban(guest);
		}
		wake.Release();
	}

	// Bans the guest once its connection has sent what it has, or now if
	// it's closed already.
	private void BanWhenClosed(GuestStream guest) {
		lock (sync) {
			if (!guest.closed) {
				guest.banOnClose = true;
				// Kept now, in case the link goes before it's sent.
				if (guest.key != null && !bans.Contains(guest.key)) {
					bans.Add(guest.key);
				}
				return;
			}
		}
		Ban(guest);
	}

	private async Task SendLoop(ClientWebSocket connected, CancellationToken cancel) {
		while (true) {
			await wake.WaitAsync(cancel);
			while (true) {
				(byte[] message, WebSocketMessageType type, TimeSpan wait) = NextMessage();
				if (wait > TimeSpan.Zero) {
					await Task.Delay(wait, cancel);
					continue;
				}
				if (message == null) {
					break;
				}
				await connected.SendAsync(message, type, true, cancel);
			}
		}
	}

	// What to send next, or how long to wait for the other guests' copies of
	// it, or nothing.
	private (byte[], WebSocketMessageType, TimeSpan) NextMessage() {
		lock (sync) {
			if (control.Count > 0) {
				(byte[] message, WebSocketMessageType type) = control.Dequeue();
				return (message, type, TimeSpan.Zero);
			}
			GuestStream first = null;
			foreach (GuestStream guest in guests.Values) {
				if (guest.outgoing.Count > 0 && (first == null || guest.outgoing.Peek().sequence < first.outgoing.Peek().sequence)) {
					first = guest;
				}
			}
			if (first == null) {
				return (null, default, TimeSpan.Zero);
			}
			Pending head = first.outgoing.Peek();
			if (head.message == null && head.ban) {
				// The relay closes the guest's connection as it bans it.
				head.ban = false;
				return (BanMessage(first), WebSocketMessageType.Text, TimeSpan.Zero);
			}
			if (head.message == null) {
				first.outgoing.Dequeue();
				guests.Remove(first.id);
				return (RelayProtocol.Encode(RelayProtocol.Close, first.id), WebSocketMessageType.Binary, TimeSpan.Zero);
			}

			List<GuestStream> same = [first];
			int open = 0;
			foreach (GuestStream guest in guests.Values) {
				if (!guest.closed) {
					++open;
				}
				if (guest != first && guest.outgoing.Count > 0 && guest.outgoing.Peek().message is byte[] next
					&& next.AsSpan().SequenceEqual(head.message)) {
					same.Add(guest);
				}
			}
			TimeSpan age = Stopwatch.GetElapsedTime(head.queuedAt);
			if (same.Count < open && age < FanOutWindow) {
				return (null, default, FanOutWindow - age);
			}

			foreach (GuestStream guest in same) {
				guest.outgoing.Dequeue();
				guest.queuedBytes -= head.message.Length;
			}
			Monitor.PulseAll(sync);
			Interlocked.Increment(ref dataMessagesSent);
			if (same.Count > 1) {
				Interlocked.Increment(ref fannedOutMessages);
			}
			return (RelayProtocol.Encode(RelayProtocol.Data, same.Select(g => g.id).ToList(), head.message),
				WebSocketMessageType.Binary, TimeSpan.Zero);
		}
	}

	// Closes the link, and with it the guests' connections through it. The
	// room waits on the relay for a while, for this host to claim again.
	public void Dispose() {
		if (disposed.IsCancellationRequested) {
			return;
		}
		disposed.Cancel();
		ClientWebSocket open;
		lock (sync) {
			open = socket;
		}
		open?.Abort();
	}

	// A guest's connection through the relay, as a stream for a
	// LanConnection: what the relay passes on from the guest is read from it,
	// and each flush sends what was written since to the guest.
	private sealed class GuestStream : Stream {
		private readonly RelayHostLink link;
		public readonly uint id;

		// The key the relay gave for the guest's address, or null if it
		// didn't (see RelayProtocol).
		public string key;

		// Guarded by the link.
		public readonly Queue<Pending> outgoing = new();
		public long queuedBytes;
		public bool closed;
		public bool banOnClose;

		// Guarded by itself.
		private readonly Queue<byte[]> incoming = new();
		private long incomingBytes;
		private byte[] current = [];
		private int read;
		private bool ended;

		private readonly MemoryStream unsent = new();

		public GuestStream(RelayHostLink link, uint id) {
			this.link = link;
			this.id = id;
		}

		public void BanWhenClosed() => link.BanWhenClosed(this);

		// The host has let the guest in (see RelayProtocol).
		public void Admitted() => link.SendControl(new RelayControl(RelayControl.Admit, guest: id).ToBytes(), WebSocketMessageType.Text);

		public void Deliver(byte[] bytes) {
			lock (incoming) {
				if (ended) {
					return;
				}
				if (incomingBytes + bytes.Length <= MaxIncomingBytesPerGuest) {
					incoming.Enqueue(bytes);
					incomingBytes += bytes.Length;
					Monitor.Pulse(incoming);
					return;
				}
				ended = true;
				incoming.Clear();
				incomingBytes = 0;
				Monitor.PulseAll(incoming);
			}
			log.Warning("Guest {Guest} sent more than its connection can take, dropping it", id);
			link.Close(this);
		}

		// The guest has gone, or the relay with it.
		public void RemoteClosed() {
			lock (link.sync) {
				closed = true;
				outgoing.Clear();
				queuedBytes = 0;
				Monitor.PulseAll(link.sync);
			}
			lock (incoming) {
				ended = true;
				Monitor.PulseAll(incoming);
			}
		}

		public override int Read(Span<byte> buffer) {
			lock (incoming) {
				while (read == current.Length) {
					if (incoming.TryDequeue(out byte[] next)) {
						current = next;
						read = 0;
						incomingBytes -= next.Length;
					} else if (ended) {
						return 0;
					} else {
						Monitor.Wait(incoming);
					}
				}
				int count = Math.Min(buffer.Length, current.Length - read);
				current.AsSpan(read, count).CopyTo(buffer);
				read += count;
				return count;
			}
		}

		public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

		public override void Write(ReadOnlySpan<byte> buffer) => unsent.Write(buffer);

		public override void Write(byte[] buffer, int offset, int count) => unsent.Write(buffer, offset, count);

		public override void Flush() {
			if (unsent.Length == 0) {
				return;
			}
			byte[] message = unsent.ToArray();
			unsent.SetLength(0);
			link.Queue(this, message);
		}

		protected override void Dispose(bool disposing) {
			link.Close(this);
			lock (incoming) {
				ended = true;
				Monitor.PulseAll(incoming);
			}
			base.Dispose(disposing);
		}

		public override bool CanRead => true;
		public override bool CanWrite => true;
		public override bool CanSeek => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
	}
}

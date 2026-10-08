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

	private readonly Action<LanTransport> guestArrived;
	private readonly CancellationTokenSource disposed = new();

	// Guards the guests, what's queued for them and the relay, and socket.
	private readonly object sync = new();
	private readonly Dictionary<uint, GuestStream> guests = new();
	private readonly Queue<(byte[] message, WebSocketMessageType type)> control = new();
	private ClientWebSocket socket;
	private long nextSequence;

	// Released whenever there may be something new to send.
	private readonly SemaphoreSlim wake = new(0);

	// guestArrived is called on a worker thread for each guest that joins.
	// With the code and key of a room this host had, it claims that room
	// again, as when resuming a game.
	public RelayHostLink(string relayUrl, Action<LanTransport> guestArrived, string code = null, string key = null) {
		RelayUrl = relayUrl;
		this.guestArrived = guestArrived;
		this.code = code;
		this.key = key;
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
					RelayConnection.HostUri(RelayUrl, code, key), ConnectTimeout, cancel);
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
				if (RelayControl.Parse(message.Span)?.type == RelayControl.Ping) {
					SendControl(new RelayControl(RelayControl.Pong).ToBytes(), WebSocketMessageType.Text);
				}
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

	private void GuestJoined(uint id) {
		GuestStream guest = new(this, id);
		lock (sync) {
			guests[id] = guest;
		}
		log.Information("Guest {Guest} joined through the relay", id);
		guestArrived(new LanTransport(guest, $"online guest {id}"));
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
	// connection once what it was sent before has gone.
	private sealed class Pending {
		public byte[] message;
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
	// queued for it has gone.
	private void Close(GuestStream guest) {
		lock (sync) {
			if (guest.closed) {
				return;
			}
			guest.closed = true;
			if (socket == null || !guests.ContainsKey(guest.id)) {
				guests.Remove(guest.id);
				return;
			}
			guest.outgoing.Enqueue(new Pending { message = null, sequence = nextSequence++, queuedAt = Stopwatch.GetTimestamp() });
			Monitor.PulseAll(sync);
		}
		wake.Release();
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

		// Guarded by the link.
		public readonly Queue<Pending> outgoing = new();
		public long queuedBytes;
		public bool closed;

		// Guarded by itself.
		private readonly Queue<byte[]> incoming = new();
		private byte[] current = [];
		private int read;
		private bool ended;

		private readonly MemoryStream unsent = new();

		public GuestStream(RelayHostLink link, uint id) {
			this.link = link;
			this.id = id;
		}

		public void Deliver(byte[] bytes) {
			lock (incoming) {
				incoming.Enqueue(bytes);
				Monitor.Pulse(incoming);
			}
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

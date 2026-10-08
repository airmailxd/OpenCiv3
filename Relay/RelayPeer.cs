using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace C7Relay;

// One WebSocket connected to the relay, a host's or a guest's. What's sent to
// it waits in a queue and is written one message at a time, so any thread
// can send without waiting for the network. The relay pings it, and drops it
// once it has gone quiet for too long or isn't keeping up with what it's
// sent.
internal sealed class RelayPeer {
	private readonly WebSocket socket;
	private readonly RelayOptions options;
	private readonly Channel<(ReadOnlyMemory<byte> data, WebSocketMessageType type)> outgoing =
		Channel.CreateUnbounded<(ReadOnlyMemory<byte>, WebSocketMessageType)>(new UnboundedChannelOptions { SingleReader = true });
	private long queuedBytes;
	private long lastHeard = Environment.TickCount64;

	// Set once to close the connection, with why.
	private int closing;
	private WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure;
	private string closeReason = "";

	// Cancelled to give up on the connection without a goodbye.
	private readonly CancellationTokenSource abort = new();

	public string Address { get; }
	public long BytesReceived { get; private set; }
	public long BytesSent => Interlocked.Read(ref bytesSent);
	private long bytesSent;

	public bool IsClosing => Volatile.Read(ref closing) != 0;

	public RelayPeer(WebSocket socket, string address, RelayOptions options) {
		this.socket = socket;
		this.options = options;
		Address = address;
	}

	// Queues a message. False if the connection is closing, or was just
	// dropped for falling too far behind.
	public bool Send(ReadOnlyMemory<byte> data, WebSocketMessageType type = WebSocketMessageType.Binary) {
		if (IsClosing) {
			return false;
		}
		if (Interlocked.Add(ref queuedBytes, data.Length) > options.MaxQueuedBytes) {
			Close(RelayCloseCodes.TooSlow, "The connection isn't keeping up with the game.");
			return false;
		}
		return outgoing.Writer.TryWrite((data, type));
	}

	public bool Send(RelayControl control) {
		return Send(control.ToBytes(), WebSocketMessageType.Text);
	}

	// Closes the connection once what's queued has been sent, saying why.
	public void Close(int status, string reason) {
		if (Interlocked.Exchange(ref closing, 1) != 0) {
			return;
		}
		closeStatus = (WebSocketCloseStatus)status;
		// A close reason can be at most 123 bytes.
		closeReason = reason.Length > 120 ? reason[..120] : reason;
		outgoing.Writer.TryComplete();
	}

	// Runs the connection until it closes: hands each message received to
	// onMessage (whose data is only valid until it returns), sends what's
	// queued, and pings.
	public async Task RunAsync(Action<ReadOnlyMemory<byte>, WebSocketMessageType> onMessage, CancellationToken shutdown) {
		using CancellationTokenRegistration onShutdown = shutdown.Register(() => Close((int)WebSocketCloseStatus.EndpointUnavailable, "The relay is shutting down."));
		Task sending = SendLoop();
		Task pinging = PingLoop();
		try {
			await ReceiveLoop(onMessage);
		} finally {
			Close((int)WebSocketCloseStatus.NormalClosure, "");
			// Give the goodbye a moment to go out.
			await Task.WhenAny(sending, Task.Delay(TimeSpan.FromSeconds(5)));
			abort.Cancel();
			await Task.WhenAll(sending.ContinueWith(_ => { }), pinging.ContinueWith(_ => { }));
			socket.Abort();
		}
	}

	private async Task ReceiveLoop(Action<ReadOnlyMemory<byte>, WebSocketMessageType> onMessage) {
		byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
		try {
			while (true) {
				int length = 0;
				ValueWebSocketReceiveResult result = default;
				do {
					if (length == Math.Min(buffer.Length, options.MaxMessageBytes)) {
						if (length >= options.MaxMessageBytes) {
							// The rest of it is read and dropped, until the
							// peer answers the goodbye: closing with it unread
							// would cut the goodbye off.
							Close((int)WebSocketCloseStatus.MessageTooBig, "A message was too large for the relay.");
							length = 0;
							continue;
						}
						byte[] larger = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length * 2, options.MaxMessageBytes));
						buffer.AsSpan(0, length).CopyTo(larger);
						ArrayPool<byte>.Shared.Return(buffer);
						buffer = larger;
					}
					int room = Math.Min(buffer.Length, options.MaxMessageBytes) - length;
					result = await socket.ReceiveAsync(buffer.AsMemory(length, room), abort.Token);
					length += result.Count;
				} while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

				if (result.MessageType == WebSocketMessageType.Close) {
					return;
				}
				Volatile.Write(ref lastHeard, Environment.TickCount64);
				BytesReceived += length;
				if (IsClosing) {
					// Waiting for the goodbye to go out.
					continue;
				}
				onMessage(buffer.AsMemory(0, length), result.MessageType);
			}
		} catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException) {
			// The connection broke, or we gave up on it.
		} finally {
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	private async Task SendLoop() {
		try {
			await foreach ((ReadOnlyMemory<byte> data, WebSocketMessageType type) in outgoing.Reader.ReadAllAsync(abort.Token)) {
				await socket.SendAsync(data, type, true, abort.Token);
				Interlocked.Add(ref queuedBytes, -data.Length);
				Interlocked.Add(ref bytesSent, data.Length);
			}
			if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived) {
				await socket.CloseOutputAsync(closeStatus, closeReason, abort.Token);
			}
		} catch (Exception e) when (e is WebSocketException or OperationCanceledException or ObjectDisposedException) {
			// The connection broke, or we gave up on it.
		}
		// The receive loop ends once the peer answers the goodbye, but a peer
		// that sends no answer is given up on.
		_ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => abort.Cancel());
	}

	private async Task PingLoop() {
		try {
			while (!IsClosing) {
				await Task.Delay(options.PingInterval, abort.Token);
				long quiet = Environment.TickCount64 - Volatile.Read(ref lastHeard);
				if (quiet > options.IdleTimeout.TotalMilliseconds) {
					Close(RelayCloseCodes.TimedOut, "The relay heard nothing from the game for too long.");
					return;
				}
				Send(new RelayControl(RelayControl.Ping));
			}
		} catch (OperationCanceledException) {
		}
	}
}

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using C7Relay;

namespace C7Engine.Network;

// A host that guests join through an online relay, by its join code.
public sealed record RelayEndpoint(string RelayUrl, string Code) : LanEndpoint {
	public override string Description => $"the game {RelayProtocol.FormatCode(Code)}";

	public override async Task<LanTransport> ConnectAsync(TimeSpan timeout, CancellationToken cancel = default) {
		(ClientWebSocket socket, RelayControl welcome) =
			await RelayConnection.ConnectAsync(RelayConnection.JoinUri(RelayUrl, Code), timeout, cancel).ConfigureAwait(false);
		RelayGuestStream stream = new(socket, RelayConnection.QuietLimit(welcome.pingSeconds),
			welcome.maxMessageBytes > 0 ? welcome.maxMessageBytes : RelayProtocol.DefaultMaxGuestMessageBytes);
		return new LanTransport(stream, $"the host of {RelayProtocol.FormatCode(Code)}");
	}
}

// A guest's connection to its host through the relay, as a stream for a
// LanConnection. Each binary message from the relay is bytes from the host,
// and each flush sends what was written since to it, as one message or, if
// it's larger than the relay takes, in pieces. The
// relay's pings are answered on the way, and a relay that stops pinging is
// taken to have gone.
internal sealed class RelayGuestStream : Stream {
	private readonly ClientWebSocket socket;
	private readonly TimeSpan quietLimit;
	private readonly int maxMessageBytes;
	private readonly WebSocketMessageReader reader = new();
	private readonly SemaphoreSlim sending = new(1, 1);
	private readonly MemoryStream unsent = new();

	// What's left of the last message read.
	private ReadOnlyMemory<byte> received;
	private int disposed;

	public RelayGuestStream(ClientWebSocket socket, TimeSpan quietLimit, int maxMessageBytes = RelayProtocol.DefaultMaxGuestMessageBytes) {
		this.socket = socket;
		this.quietLimit = quietLimit;
		this.maxMessageBytes = maxMessageBytes;
	}

	public override int Read(Span<byte> buffer) {
		while (received.IsEmpty) {
			if (!ReceiveAsync().GetAwaiter().GetResult()) {
				return 0;
			}
		}
		int count = Math.Min(buffer.Length, received.Length);
		received.Span[..count].CopyTo(buffer);
		received = received[count..];
		return count;
	}

	public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

	// Reads the next message with bytes from the host; false once the relay
	// has closed the connection.
	private async Task<bool> ReceiveAsync() {
		while (true) {
			WebSocketMessageType type;
			ReadOnlyMemory<byte> message;
			using (CancellationTokenSource quiet = new(quietLimit)) {
				try {
					(type, message) = await reader.ReadAsync(socket, LanProtocol.MaxFrameBytes, quiet.Token).ConfigureAwait(false);
				} catch (OperationCanceledException) {
					throw new IOException("The relay stopped answering");
				} catch (WebSocketException e) {
					throw new IOException(e.Message, e);
				}
			}
			if (type == WebSocketMessageType.Close) {
				return false;
			}
			if (type == WebSocketMessageType.Binary) {
				if (message.Length > 0) {
					// The reader reuses its buffer for the next message.
					received = message.ToArray();
					return true;
				}
				continue;
			}
			if (RelayControl.Parse(message.Span)?.type == RelayControl.Ping) {
				await SendAsync(new RelayControl(RelayControl.Pong).ToBytes(), WebSocketMessageType.Text).ConfigureAwait(false);
			}
		}
	}

	public override void Write(ReadOnlySpan<byte> buffer) => unsent.Write(buffer);

	public override void Write(byte[] buffer, int offset, int count) => unsent.Write(buffer, offset, count);

	// Sends what was written since the last flush as one message.
	public override void Flush() {
		if (unsent.Length == 0) {
			return;
		}
		ReadOnlyMemory<byte> written = unsent.GetBuffer().AsMemory(0, (int)unsent.Length);
		for (int start = 0; start < written.Length; start += maxMessageBytes) {
			ReadOnlyMemory<byte> piece = written.Slice(start, Math.Min(maxMessageBytes, written.Length - start));
			SendAsync(piece, WebSocketMessageType.Binary).GetAwaiter().GetResult();
		}
		unsent.SetLength(0);
	}

	private async Task SendAsync(ReadOnlyMemory<byte> message, WebSocketMessageType type) {
		await sending.WaitAsync().ConfigureAwait(false);
		try {
			await socket.SendAsync(message, type, true, CancellationToken.None).ConfigureAwait(false);
		} catch (WebSocketException e) {
			throw new IOException(e.Message, e);
		} finally {
			sending.Release();
		}
	}

	protected override void Dispose(bool disposing) {
		if (Interlocked.Exchange(ref disposed, 1) != 0) {
			return;
		}
		// Say goodbye, so that the host hears we've gone straight away, but
		// don't wait for it.
		_ = Task.Run(async () => {
			try {
				using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
				await sending.WaitAsync(timeout.Token).ConfigureAwait(false);
				await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", timeout.Token).ConfigureAwait(false);
			} catch (Exception) {
				// It's going anyway.
			}
			socket.Dispose();
		});
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

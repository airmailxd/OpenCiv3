using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Serilog;

namespace C7Engine.Network;

public record Frame(FrameKind kind, byte[] payload);

// One TCP connection between a LAN host and a client. A background thread
// reads frames into a queue that the game drains on its own thread, so the
// game's state is only ever touched from one thread.
public class LanConnection : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanConnection>();

	private readonly TcpClient client;
	private readonly NetworkStream stream;
	private readonly object writeLock = new();
	private readonly ConcurrentQueue<Frame> received = new();
	private volatile bool closed;

	public string RemoteAddress { get; }

	// True once either side has closed the connection or it broke.
	public bool IsClosed => closed;

	public LanConnection(TcpClient client) {
		this.client = client;
		client.NoDelay = true;
		stream = client.GetStream();
		RemoteAddress = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

		Thread reader = new(ReadLoop) { IsBackground = true, Name = $"LAN reader {RemoteAddress}" };
		reader.Start();
	}

	public bool TryReceive(out Frame frame) {
		return received.TryDequeue(out frame);
	}

	public bool TryPeek(out Frame frame) {
		return received.TryPeek(out frame);
	}

	public void Send(FrameKind kind, byte[] payload) {
		if (closed) {
			return;
		}
		byte[] header = new byte[5];
		BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
		header[4] = (byte)kind;
		try {
			lock (writeLock) {
				stream.Write(header);
				stream.Write(payload);
			}
		} catch (Exception e) when (e is IOException or ObjectDisposedException or SocketException) {
			log.Information("Lost connection to {Address} while sending: {Error}", RemoteAddress, e.Message);
			Dispose();
		}
	}

	public void Send<T>(FrameKind kind, T value) {
		Send(kind, NetSerialization.SerializeData(value));
	}

	private void ReadLoop() {
		byte[] header = new byte[5];
		try {
			while (!closed) {
				stream.ReadExactly(header);
				int length = BinaryPrimitives.ReadInt32LittleEndian(header);
				if (length < 0 || length > LanProtocol.MaxFrameBytes) {
					throw new IOException($"Frame of {length} bytes is too large");
				}
				byte[] payload = new byte[length];
				stream.ReadExactly(payload);
				received.Enqueue(new Frame((FrameKind)header[4], payload));
			}
		} catch (Exception e) when (e is IOException or ObjectDisposedException or SocketException or EndOfStreamException) {
			if (!closed) {
				log.Information("Connection to {Address} closed: {Error}", RemoteAddress, e.Message);
			}
		} finally {
			Dispose();
		}
	}

	public void Dispose() {
		closed = true;
		client.Dispose();
	}
}

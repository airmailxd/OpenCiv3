using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace C7Engine.Network;

// A frame as received. Prepared is whatever the connection's owner worked out
// from it on the reader thread (see LanConnection's constructor), or null.
public record Frame(FrameKind kind, byte[] payload) {
	public object Prepared { get; init; }
}

// One TCP connection between a LAN host and a client. A background thread
// reads frames into a queue that the game drains on its own thread, so the
// game's state is only ever touched from one thread. Another background
// thread writes the frames the game sends, in the order it sent them, so a
// slow or stalled peer never holds up the game or the other connections.
public class LanConnection : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanConnection>();

	// A peer that falls this far behind is dropped rather than have its
	// frames pile up without limit. Snapshots count only as frames, since
	// every connection shares the same snapshot bytes.
	public const int MaxQueuedFrames = 10_000;
	public const long MaxQueuedBytes = 256L * 1024 * 1024;

	// How long a closed connection may spend sending what was queued before
	// it closed, like a rejection, before giving up on the peer.
	private static readonly TimeSpan LingerTime = TimeSpan.FromSeconds(5);

	// A frame waiting to be written. A snapshot's payload may still be being
	// encoded on another thread.
	private class Outgoing {
		public FrameKind kind;
		public byte[] payload;
		public Task<EncodedSnapshot> snapshot;
	}

	private readonly TcpClient client;
	private readonly NetworkStream stream;
	private readonly Func<Frame, Frame> prepareReceived;
	private readonly ConcurrentQueue<Frame> received = new();

	// The frames waiting to be written, which also serves as their lock.
	private readonly Queue<Outgoing> outgoing = new();
	private Outgoing lastQueued;
	private long queuedBytes;

	// Closing: no more frames are taken, and the writer finishes those
	// queued. Aborted: the socket is closed and anything queued is dropped.
	private volatile bool closing;
	private volatile bool aborted;

	// The hash of the last snapshot written, so an identical one isn't sent
	// again. Only the writer thread uses it.
	private byte[] lastSnapshotHash;

	public string RemoteAddress { get; }

	// True once either side has closed the connection or it broke.
	public bool IsClosed => closing || aborted;

	// prepareReceived, if given, is called on the reader thread with each
	// frame as it arrives, and the frame it returns is the one queued.
	public LanConnection(TcpClient client, Func<Frame, Frame> prepareReceived = null) {
		this.client = client;
		this.prepareReceived = prepareReceived;
		client.NoDelay = true;
		stream = client.GetStream();
		RemoteAddress = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

		Thread reader = new(ReadLoop) { IsBackground = true, Name = $"LAN reader {RemoteAddress}" };
		reader.Start();
		Thread writer = new(WriteLoop) { IsBackground = true, Name = $"LAN writer {RemoteAddress}" };
		writer.Start();
	}

	public bool TryReceive(out Frame frame) {
		return received.TryDequeue(out frame);
	}

	public bool TryPeek(out Frame frame) {
		return received.TryPeek(out frame);
	}

	// Looks at the frame this many places behind the next one, without
	// taking it.
	public bool TryPeekAt(int index, out Frame frame) {
		if (index == 0) {
			return received.TryPeek(out frame);
		}
		// Enumerating a ConcurrentQueue sees it as it was at one moment.
		frame = received.Skip(index).FirstOrDefault();
		return frame != null;
	}

	// Queues a frame to be sent. This never waits for the network.
	public void Send(FrameKind kind, byte[] payload) {
		Enqueue(new Outgoing { kind = kind, payload = payload }, supersede: false);
	}

	public void Send<T>(FrameKind kind, T value) {
		Send(kind, NetSerialization.SerializeData(value));
	}

	// Queues a snapshot, which may still be being encoded on another thread.
	// It is written once encoding finishes, after the frames queued before
	// it and before those queued after it. It isn't written at all if it's
	// identical to the last snapshot written, and it replaces the snapshot
	// queued just before it if that hasn't been written yet and nothing was
	// queued in between, since the newer one shows everything the older
	// one would have.
	public void SendSnapshot(Task<EncodedSnapshot> snapshot) {
		Enqueue(new Outgoing { kind = FrameKind.Snapshot, snapshot = snapshot }, supersede: true);
	}

	private void Enqueue(Outgoing frame, bool supersede) {
		long bytes = frame.payload?.Length ?? 0;
		lock (outgoing) {
			if (closing) {
				return;
			}
			if (supersede && lastQueued?.snapshot != null) {
				lastQueued.snapshot = frame.snapshot;
				return;
			}
			if (outgoing.Count < MaxQueuedFrames && queuedBytes + bytes <= MaxQueuedBytes) {
				outgoing.Enqueue(frame);
				lastQueued = frame;
				queuedBytes += bytes;
				Monitor.Pulse(outgoing);
				return;
			}
		}
		log.Warning("{Address} isn't keeping up with the game, dropping the connection", RemoteAddress);
		Abort();
	}

	// Takes the next frame to write, waiting for one. Null once the
	// connection has closed and everything queued has been taken.
	private Outgoing NextOutgoing() {
		lock (outgoing) {
			while (outgoing.Count == 0 && !closing) {
				Monitor.Wait(outgoing);
			}
			if (outgoing.Count == 0 || aborted) {
				return null;
			}
			Outgoing next = outgoing.Dequeue();
			if (outgoing.Count == 0) {
				lastQueued = null;
			}
			queuedBytes -= next.payload?.Length ?? 0;
			return next;
		}
	}

	private void WriteLoop() {
		byte[] header = new byte[5];
		try {
			while (NextOutgoing() is Outgoing next) {
				byte[] payload = next.payload;
				if (next.snapshot != null) {
					EncodedSnapshot snapshot = WaitForSnapshot(next.snapshot);
					if (snapshot == null || (lastSnapshotHash != null && lastSnapshotHash.AsSpan().SequenceEqual(snapshot.Hash))) {
						continue;
					}
					payload = snapshot.Compressed;
					lastSnapshotHash = snapshot.Hash;
				}
				BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
				header[4] = (byte)next.kind;
				stream.Write(header);
				stream.Write(payload);
			}
		} catch (Exception e) when (e is IOException or ObjectDisposedException or SocketException or InvalidOperationException) {
			if (!aborted) {
				log.Information("Lost connection to {Address} while sending: {Error}", RemoteAddress, e.Message);
			}
		} finally {
			Abort();
		}
	}

	// The snapshot once it's encoded, or null if encoding failed or the
	// connection broke while waiting.
	private EncodedSnapshot WaitForSnapshot(Task<EncodedSnapshot> snapshot) {
		try {
			while (!snapshot.Wait(TimeSpan.FromMilliseconds(100))) {
				if (aborted) {
					return null;
				}
			}
			return snapshot.Result;
		} catch (AggregateException e) {
			log.Error(e.InnerException, "Couldn't encode a snapshot for {Address}", RemoteAddress);
			return null;
		}
	}

	private void ReadLoop() {
		byte[] header = new byte[5];
		try {
			while (!aborted) {
				stream.ReadExactly(header);
				int length = BinaryPrimitives.ReadInt32LittleEndian(header);
				if (length < 0 || length > LanProtocol.MaxFrameBytes) {
					throw new IOException($"Frame of {length} bytes is too large");
				}
				byte[] payload = new byte[length];
				stream.ReadExactly(payload);
				Frame frame = new((FrameKind)header[4], payload);
				received.Enqueue(prepareReceived == null ? frame : prepareReceived(frame));
			}
		} catch (Exception e) when (e is IOException or ObjectDisposedException or SocketException or EndOfStreamException) {
			if (!IsClosed) {
				log.Information("Connection to {Address} closed: {Error}", RemoteAddress, e.Message);
			}
		} finally {
			Abort();
		}
	}

	// Closes the connection once the frames already queued have been sent,
	// without waiting for that.
	public void Dispose() {
		lock (outgoing) {
			if (closing) {
				return;
			}
			closing = true;
			Monitor.PulseAll(outgoing);
		}
		// A peer that stops reading can't keep the connection open.
		Task.Delay(LingerTime).ContinueWith(_ => Abort());
	}

	// Closes the connection now, dropping anything not yet sent.
	private void Abort() {
		lock (outgoing) {
			aborted = true;
			closing = true;
			outgoing.Clear();
			lastQueued = null;
			queuedBytes = 0;
			Monitor.PulseAll(outgoing);
		}
		client.Dispose();
	}
}

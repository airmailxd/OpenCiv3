using System;
using System.IO;
using System.Net.Sockets;
using Serilog;

namespace C7Engine.Network;

// What a LanConnection sends its frames over: a duplex byte stream to the
// peer, like a TCP connection, or a guest's connection through an online
// relay. The connection writes each frame and then flushes the stream, so a
// stream that sends messages rather than bytes can send each frame as one.
public sealed class LanTransport : IDisposable {
	private static readonly ILogger log = Log.ForContext<LanTransport>();

	public Stream Stream { get; }
	public string RemoteAddress { get; }

	// Disposed along with the stream, like the TcpClient a NetworkStream
	// came from.
	private readonly IDisposable owner;

	public LanTransport(Stream stream, string remoteAddress, IDisposable owner = null) {
		Stream = stream;
		RemoteAddress = remoteAddress;
		this.owner = owner;
	}

	// A TCP connection, sending each frame as soon as it's written, and
	// noticing a peer that vanishes.
	public static LanTransport Tcp(TcpClient client) {
		client.NoDelay = true;
		KeepAlive(client.Client);
		return new LanTransport(client.GetStream(), client.Client.RemoteEndPoint?.ToString() ?? "unknown", client);
	}

	// A peer that vanishes without closing the connection, like one whose
	// network went down, would otherwise look connected for a long time,
	// since a side that has nothing to send never finds out.
	private static void KeepAlive(Socket socket) {
		try {
			socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 2);
			socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
		} catch (Exception e) when (e is SocketException or PlatformNotSupportedException or NotSupportedException) {
			log.Debug("Couldn't set keep-alive on a LAN connection: {Error}", e.Message);
		}
	}

	public void Dispose() {
		Stream.Dispose();
		owner?.Dispose();
	}
}

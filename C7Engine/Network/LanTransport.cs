using System;
using System.IO;
using System.Net;

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

	// The peer's IP address, without the port, for a host to ban; null when
	// it isn't known, as for a guest through a relay.
	public string RemoteHost { get; init; }

	// What stands for the peer's address, to count what it does (like wrong
	// passwords) across its connections: its IP address, or what the relay
	// gave for it; null when neither is known.
	public string AddressKey {
		get => addressKey ?? RemoteHost;
		init => addressKey = value;
	}
	private readonly string addressKey;

	// Asks the relay a guest came through to turn its address away from the
	// host's room from now on, once the connection has sent what it has;
	// null for a connection that isn't through a relay.
	public Action BanAtRelay { get; init; }

	// Tells the relay a guest came through that the host has let it in, so
	// that it isn't made to give way to guests joining after it; null for a
	// connection that isn't through a relay.
	public Action AdmittedAtRelay { get; init; }

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
		IPEndPoint remote = client.Client.RemoteEndPoint as IPEndPoint;
		return new LanTransport(client.GetStream(), remote?.ToString() ?? "unknown", client) {
			RemoteHost = remote == null ? null : HostOf(remote.Address),
		};
	}

	// An address as a host bans it: an IPv4 address mapped into IPv6 the way
	// it would come over IPv4.
	internal static string HostOf(IPAddress address) {
		return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
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

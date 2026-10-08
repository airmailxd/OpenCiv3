using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace C7Engine.Network;

// Where a client finds its host, and how to connect to it again after
// losing it.
public abstract record LanEndpoint {
	// What to call the host's whereabouts to the player.
	public abstract string Description { get; }

	// Connects to the host, giving up after the timeout with a
	// TimeoutException.
	public abstract Task<LanTransport> ConnectAsync(TimeSpan timeout, CancellationToken cancel = default);
}

// A host at an address on the network.
public sealed record LanAddressEndpoint(string Address, int Port) : LanEndpoint {
	public override string Description => Port == LanProtocol.DefaultPort ? Address : $"{Address}:{Port}";

	public override async Task<LanTransport> ConnectAsync(TimeSpan timeout, CancellationToken cancel = default) {
		TcpClient tcp = new();
		using CancellationTokenSource timer = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		timer.CancelAfter(timeout);
		try {
			await tcp.ConnectAsync(Address, Port, timer.Token);
			return LanTransport.Tcp(tcp);
		} catch (OperationCanceledException) when (!cancel.IsCancellationRequested) {
			tcp.Dispose();
			throw new TimeoutException($"{Description} didn't answer within {timeout.TotalSeconds:0} seconds.");
		} catch {
			tcp.Dispose();
			throw;
		}
	}
}

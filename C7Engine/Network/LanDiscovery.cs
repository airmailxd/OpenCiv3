using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace C7Engine.Network;

public record FoundHost(string address, DiscoveryReply reply);

// Finds LAN games by broadcasting on the local networks and collecting the
// hosts' replies.
public static class LanDiscovery {
	public static async Task<List<FoundHost>> FindHosts(TimeSpan timeout) {
		using UdpClient udp = new(0) { EnableBroadcast = true };
		byte[] request = Encoding.UTF8.GetBytes(LanProtocol.DiscoveryRequest);
		foreach (IPAddress target in BroadcastAddresses()) {
			try {
				await udp.SendAsync(request, request.Length, new IPEndPoint(target, LanProtocol.DiscoveryPort));
			} catch (SocketException) {
				// Some interfaces don't allow broadcasts; the others may.
			}
		}

		Dictionary<string, FoundHost> found = new();
		using CancellationTokenSource cancel = new(timeout);
		try {
			while (true) {
				UdpReceiveResult result = await udp.ReceiveAsync(cancel.Token);
				try {
					DiscoveryReply reply = NetSerialization.DeserializeData<DiscoveryReply>(result.Buffer);
					string address = result.RemoteEndPoint.Address.ToString();
					found[$"{address}:{reply.port}"] = new FoundHost(address, reply);
				} catch (JsonException) {
					// Not one of ours.
				}
			}
		} catch (OperationCanceledException) {
		}
		return found.Values.OrderBy(h => h.reply.hostName).ToList();
	}

	// The broadcast address of each local IPv4 network, plus this machine for
	// a host running alongside.
	private static IEnumerable<IPAddress> BroadcastAddresses() {
		HashSet<IPAddress> addresses = [IPAddress.Broadcast, IPAddress.Loopback];
		foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces()) {
			if (nic.OperationalStatus != OperationalStatus.Up) {
				continue;
			}
			foreach (UnicastIPAddressInformation unicast in nic.GetIPProperties().UnicastAddresses) {
				if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask == null) {
					continue;
				}
				byte[] ip = unicast.Address.GetAddressBytes();
				byte[] mask = unicast.IPv4Mask.GetAddressBytes();
				byte[] broadcast = ip.Select((b, i) => (byte)(b | ~mask[i])).ToArray();
				addresses.Add(new IPAddress(broadcast));
			}
		}
		return addresses;
	}

	// This machine's IPv4 addresses on its local networks, for the host to
	// tell players who join by address.
	public static List<string> LocalAddresses() {
		return NetworkInterface.GetAllNetworkInterfaces()
			.Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
			.SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
			.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
			.Select(u => u.Address.ToString())
			.Distinct()
			.ToList();
	}
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace C7Engine.Network;

// A host that answered, at the address it answered from, and how long its
// answer took to come, in milliseconds (null if not timed).
public record FoundHost(string address, DiscoveryReply reply, int? latencyMs = null) {
	// The virtual network it was found on, such as "Tailscale", or null for
	// an ordinary local network.
	public string network => LanDiscovery.NetworkOf(address);
}

// Finds LAN games by broadcasting on the local networks and collecting the
// hosts' replies. Virtual networks such as Tailscale carry no broadcasts, so
// the machines on this player's tailnet and the hosts joined before are also
// asked directly.
public static class LanDiscovery {
	private static readonly ILogger log = Log.ForContext(typeof(LanDiscovery));

	// How long Tailscale and name lookups get to say whom to ask.
	private static readonly TimeSpan DirectLookupLimit = TimeSpan.FromSeconds(2);
	// How long to listen after asking them: a tailnet peer asked for the
	// first time may take a moment while Tailscale finds a path to it.
	private static readonly TimeSpan DirectReplyWait = TimeSpan.FromSeconds(1.5);

	// knownHosts are addresses or names of hosts to ask directly, such as
	// the ones joined before. Stopping returns the hosts found so far.
	//
	// Each host's latency is the time from asking to its answer. The awaits
	// don't come back to the caller's thread, which in Godot would wait for
	// the next frame and add it to the time.
	public static async Task<List<FoundHost>> FindHosts(TimeSpan timeout, IEnumerable<string> knownHosts = null,
		CancellationToken stop = default) {
		using UdpClient udp = new(0) { EnableBroadcast = true };
		byte[] request = Encoding.UTF8.GetBytes(LanProtocol.DiscoveryRequest);
		long broadcastSent = Stopwatch.GetTimestamp();
		foreach (IPAddress target in BroadcastAddresses()) {
			try {
				await udp.SendAsync(request, request.Length, new IPEndPoint(target, LanProtocol.DiscoveryPort)).ConfigureAwait(false);
			} catch (SocketException) {
				// Some interfaces don't allow broadcasts; the others may.
			}
		}

		// Ends the listening below once the direct requests have had their
		// time to be answered, or when stopped.
		using CancellationTokenSource cancel = new();
		using CancellationTokenRegistration stopping = stop.Register(cancel.Cancel);
		ConcurrentDictionary<IPAddress, long> directSent = new();
		Task askingDirectly = AskDirectly(udp, request, knownHosts?.ToList() ?? [], timeout, cancel, directSent);

		Dictionary<string, FoundHost> found = new();
		try {
			while (true) {
				UdpReceiveResult result;
				try {
					result = await udp.ReceiveAsync(cancel.Token).ConfigureAwait(false);
				} catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset) {
					// Windows reports a request nobody was listening for (such
					// as the one to this machine) on the next receive.
					continue;
				} catch (SocketException) {
					// What was found so far is all there is.
					break;
				}
				long received = Stopwatch.GetTimestamp();
				try {
					DiscoveryReply reply = NetSerialization.DeserializeRequired<DiscoveryReply>(result.Buffer);
					IPAddress from = result.RemoteEndPoint.Address;
					string address = from.ToString();
					string key = $"{address}:{reply.port}";
					int latency = LatencyMs(broadcastSent, directSent.TryGetValue(from, out long sent) ? sent : null, received);
					// A host asked both ways answers twice; the quicker
					// answer is the truer time.
					if (found.TryGetValue(key, out FoundHost before) && before.latencyMs < latency) {
						latency = before.latencyMs.Value;
					}
					found[key] = new FoundHost(address, reply, latency);
				} catch (JsonException) {
					// Not one of ours.
				}
			}
		} catch (OperationCanceledException) {
		}
		cancel.Cancel();
		await askingDirectly.ConfigureAwait(false);
		return Merge(found.Values);
	}

	// The time an answer took, from the request to that host if one was sent
	// to it directly before the answer came, or else from the broadcast.
	internal static int LatencyMs(long broadcastSent, long? directSent, long received) {
		long sent = directSent is long direct && direct <= received ? direct : broadcastSent;
		return (int)Math.Max(0, Stopwatch.GetElapsedTime(sent, received).TotalMilliseconds);
	}

	// Sends the request to each host to ask directly, then waits out the
	// time to listen for answers before ending the search.
	private static async Task AskDirectly(UdpClient udp, byte[] request, List<string> knownHosts, TimeSpan timeout,
		CancellationTokenSource cancel, ConcurrentDictionary<IPAddress, long> sentAt) {
		Stopwatch clock = Stopwatch.StartNew();
		try {
			// Off the main thread: asking Tailscale runs a program.
			List<IPAddress> targets = await Task.Run(() => DirectTargets(knownHosts));
			foreach (IPAddress target in targets) {
				if (cancel.IsCancellationRequested) {
					return;
				}
				try {
					sentAt[target] = Stopwatch.GetTimestamp();
					await udp.SendAsync(request, request.Length, new IPEndPoint(target, LanProtocol.DiscoveryPort));
				} catch (SocketException) {
					// Unreachable from here; the others may not be.
				}
			}
			TimeSpan wait = timeout - clock.Elapsed;
			if (targets.Count > 0 && wait < DirectReplyWait) {
				wait = DirectReplyWait;
			}
			if (wait > TimeSpan.Zero) {
				await Task.Delay(wait, cancel.Token);
			}
		} catch (OperationCanceledException) {
		} catch (Exception e) when (e is SocketException or ObjectDisposedException) {
			log.Information("Couldn't ask hosts directly for their games: {Error}", e.Message);
		} finally {
			cancel.Cancel();
		}
	}

	// The IPv4 addresses to ask directly: the known hosts, and this
	// player's tailnet peers when this machine is on Tailscale.
	private static async Task<List<IPAddress>> DirectTargets(List<string> knownHosts) {
		Task<List<IPAddress>> tailnet = LocalAddressesByNetwork().Any(a => a.network == "Tailscale")
			? TailnetPeers.FindAsync(DirectLookupLimit)
			: Task.FromResult(new List<IPAddress>());
		List<IPAddress> targets = [];
		using CancellationTokenSource limit = new(DirectLookupLimit);
		foreach (string host in knownHosts) {
			if (IPAddress.TryParse(host, out IPAddress address)) {
				targets.Add(address);
				continue;
			}
			// A name, such as a tailnet machine's.
			try {
				targets.AddRange(await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, limit.Token));
			} catch (Exception e) when (e is SocketException or OperationCanceledException or ArgumentException) {
				// Not known any more.
			}
		}
		targets.AddRange(await tailnet);
		return DirectTargetsFrom(targets);
	}

	// The host only answers machines on its own networks, and this one is
	// always asked by broadcast, so only other machines' IPv4 addresses on
	// local or virtual networks are worth asking.
	internal static List<IPAddress> DirectTargetsFrom(IEnumerable<IPAddress> addresses) {
		return addresses
			.Select(a => a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a)
			.Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) && LanHost.IsLocalNetwork(a))
			.Distinct()
			.ToList();
	}

	// The hosts found, each once: a host on both the local network and
	// Tailscale answers on each, and is listed by its local address.
	public static List<FoundHost> Merge(IEnumerable<FoundHost> found) {
		List<FoundHost> all = found.ToList();
		HashSet<DiscoveryReply> local = all.Where(h => h.network == null).Select(h => h.reply).ToHashSet();
		return all.Where(h => h.network == null || !local.Contains(h.reply))
			.OrderBy(h => h.reply.hostName)
			.ToList();
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
		return LocalAddressesByNetwork().Select(a => a.address).ToList();
	}

	// This machine's addresses, with the virtual network each is on, such as
	// "Tailscale", or null for an ordinary local network. Players on the
	// internet can reach a host through a virtual network app.
	public static List<(string address, string network)> LocalAddressesByNetwork() {
		return NetworkInterface.GetAllNetworkInterfaces()
			.Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
			.SelectMany(nic => nic.GetIPProperties().UnicastAddresses
				.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork && !IsLinkLocal(u.Address))
				.Select(u => (address: u.Address.ToString(), network: VirtualNetwork(nic, u.Address))))
			.DistinctBy(a => a.address)
			.ToList();
	}

	// 169.254.x.x: what Windows gives an adapter that got no address, such
	// as Tailscale's while Tailscale is stopped. No one can join by it.
	public static bool IsLinkLocal(IPAddress address) {
		byte[] bytes = address.GetAddressBytes();
		return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
	}

	private static string VirtualNetwork(NetworkInterface nic, IPAddress address) {
		string name = $"{nic.Name} {nic.Description}";
		if (name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) || IsTailscaleAddress(address)) {
			return "Tailscale";
		}
		if (name.Contains("ZeroTier", StringComparison.OrdinalIgnoreCase)) {
			return "ZeroTier";
		}
		if (name.Contains("Radmin", StringComparison.OrdinalIgnoreCase)) {
			return "Radmin VPN";
		}
		return null;
	}

	// Tailscale gives out addresses from 100.64.0.0/10.
	public static bool IsTailscaleAddress(IPAddress address) {
		if (address.IsIPv4MappedToIPv6) {
			address = address.MapToIPv4();
		}
		if (address.AddressFamily != AddressFamily.InterNetwork) {
			return false;
		}
		byte[] bytes = address.GetAddressBytes();
		return bytes[0] == 100 && (bytes[1] & 0xC0) == 64;
	}

	// The virtual network a found host's address is on, such as "Tailscale",
	// or null when that can't be told from the address alone.
	public static string NetworkOf(string address) {
		return IPAddress.TryParse(address, out IPAddress ip) && IsTailscaleAddress(ip) ? "Tailscale" : null;
	}
}

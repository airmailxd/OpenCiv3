using System.Collections.Generic;
using System.Linq;
using System.Net;
using C7Engine.Network;
using Xunit;

namespace EngineTests;

// Finding games over Tailscale, which carries no broadcasts: whom to ask
// directly. Kept out of EngineTests.Network since nothing here opens a socket.
public class TailnetDiscoveryTest {
	// Trimmed from what "tailscale status --json" prints (Tailscale 1.7x):
	// one online Windows peer, one online Linux peer, one offline phone, a
	// peer with no addresses yet, and this machine under Self.
	private const string StatusJson = """
		{
		  "Version": "1.76.1-t2b5a1e3a1-g3c0a24ea5",
		  "TUN": true,
		  "BackendState": "Running",
		  "AuthURL": "",
		  "TailscaleIPs": ["100.101.102.103", "fd7a:115c:a1e0::1"],
		  "Self": {
		    "ID": "nSelf1CNTRL",
		    "PublicKey": "nodekey:1111",
		    "HostName": "owner-desktop",
		    "DNSName": "owner-desktop.tail1234.ts.net.",
		    "OS": "windows",
		    "TailscaleIPs": ["100.101.102.103", "fd7a:115c:a1e0::1"],
		    "Online": true,
		    "Active": true
		  },
		  "Peer": {
		    "nodekey:2222": {
		      "ID": "nPeer2CNTRL",
		      "PublicKey": "nodekey:2222",
		      "HostName": "friend-laptop",
		      "DNSName": "friend-laptop.tail1234.ts.net.",
		      "OS": "windows",
		      "UserID": 123,
		      "TailscaleIPs": ["100.88.17.4", "fd7a:115c:a1e0::2"],
		      "Relay": "fra",
		      "Online": true,
		      "Active": false,
		      "LastSeen": "0001-01-01T00:00:00Z"
		    },
		    "nodekey:3333": {
		      "ID": "nPeer3CNTRL",
		      "HostName": "friend-linux",
		      "OS": "linux",
		      "TailscaleIPs": ["fd7a:115c:a1e0::3", "100.127.255.254"],
		      "Online": true
		    },
		    "nodekey:4444": {
		      "ID": "nPeer4CNTRL",
		      "HostName": "friends-phone",
		      "OS": "iOS",
		      "TailscaleIPs": ["100.70.1.9", "fd7a:115c:a1e0::4"],
		      "Online": false,
		      "LastSeen": "2026-10-27T21:14:03Z"
		    },
		    "nodekey:5555": {
		      "ID": "nPeer5CNTRL",
		      "HostName": "new-machine",
		      "OS": "macOS",
		      "TailscaleIPs": null,
		      "Online": true
		    },
		    "nodekey:6666": {
		      "ID": "nPeer6CNTRL",
		      "HostName": "odd-one",
		      "OS": "linux",
		      "TailscaleIPs": ["192.168.1.50"],
		      "Online": true
		    }
		  },
		  "User": {
		    "123": { "ID": 123, "LoginName": "friend@example.com", "DisplayName": "Friend" }
		  },
		  "CurrentTailnet": { "Name": "example.com", "MagicDNSSuffix": "tail1234.ts.net", "MagicDNSEnabled": true },
		  "MagicDNSSuffix": "tail1234.ts.net"
		}
		""";

	[Fact]
	public void OnlinePeersIPv4AddressesAreAsked() {
		List<IPAddress> peers = TailnetPeers.ParseStatus(StatusJson);
		Assert.Equal(["100.88.17.4", "100.127.255.254"], peers.Select(p => p.ToString()));
	}

	[Fact]
	public void NothingIsAskedWhenTailscaleIsntRunning() {
		string stopped = StatusJson.Replace("\"BackendState\": \"Running\"", "\"BackendState\": \"Stopped\"");
		Assert.Empty(TailnetPeers.ParseStatus(stopped));
	}

	[Theory]
	[InlineData("")]
	[InlineData("not json")]
	[InlineData("[]")]
	[InlineData("{\"BackendState\": \"Running\", \"Peer\": null}")]
	[InlineData("{\"BackendState\": \"NeedsLogin\"}")]
	public void OddOutputGivesNoPeers(string json) {
		Assert.Empty(TailnetPeers.ParseStatus(json));
	}

	[Theory]
	[InlineData("100.64.0.0", true)]
	[InlineData("100.101.102.103", true)]
	[InlineData("100.127.255.255", true)]
	[InlineData("100.63.255.255", false)]
	[InlineData("100.128.0.0", false)]
	[InlineData("192.168.1.20", false)]
	[InlineData("fd7a:115c:a1e0::1", false)]
	public void TailscaleAddresses(string address, bool tailscale) {
		Assert.Equal(tailscale, LanDiscovery.IsTailscaleAddress(IPAddress.Parse(address)));
		Assert.Equal(tailscale ? "Tailscale" : null, LanDiscovery.NetworkOf(address));
	}

	[Fact]
	public void OnlyOtherMachinesOnLocalOrVirtualNetworksAreAskedDirectly() {
		List<IPAddress> targets = LanDiscovery.DirectTargetsFrom(
			new[] { "100.88.17.4", "127.0.0.1", "8.8.8.8", "192.168.1.20", "100.88.17.4", "::1", "::ffff:10.0.0.7" }
				.Select(IPAddress.Parse));
		Assert.Equal(["100.88.17.4", "192.168.1.20", "10.0.0.7"], targets.Select(t => t.ToString()));
	}

	[Fact]
	public void HostOnBothNetworksIsListedOnceByItsLocalAddress() {
		DiscoveryReply game = new("Owner", LanProtocol.DefaultPort, 3, false);
		DiscoveryReply other = new("Friend", LanProtocol.DefaultPort, 1, false);
		List<FoundHost> merged = LanDiscovery.Merge([
			new FoundHost("100.101.102.103", game),
			new FoundHost("192.168.1.20", game),
			new FoundHost("100.88.17.4", other),
		]);
		Assert.Equal(2, merged.Count);
		Assert.Contains(merged, h => h.address == "192.168.1.20" && h.network == null);
		Assert.Contains(merged, h => h.address == "100.88.17.4" && h.network == "Tailscale");
	}

	[Fact]
	public void RecentHostsKeepTheLatestFirstWithoutRepeats() {
		List<string> recent = RecentLanHosts.Parse("100.88.17.4, 192.168.1.20,,100.88.17.4");
		Assert.Equal(["100.88.17.4", "192.168.1.20"], recent);

		recent = RecentLanHosts.Add(recent, "192.168.1.20");
		Assert.Equal(["192.168.1.20", "100.88.17.4"], recent);

		recent = RecentLanHosts.Add(recent, " friend-laptop ");
		Assert.Equal("friend-laptop,192.168.1.20,100.88.17.4", RecentLanHosts.Format(recent));
		Assert.Equal(recent, RecentLanHosts.Parse(RecentLanHosts.Format(recent)));
	}

	[Theory]
	[InlineData("127.0.0.1")]
	[InlineData("localhost")]
	[InlineData("")]
	[InlineData("a,b")]
	public void RecentHostsSkipThisMachineAndBadAddresses(string address) {
		Assert.Equal(["192.168.1.20"], RecentLanHosts.Add(["192.168.1.20"], address));
	}

	[Fact]
	public void RecentHostsAreLimited() {
		List<string> recent = [];
		for (int i = 1; i <= RecentLanHosts.MaxAddresses + 3; i++) {
			recent = RecentLanHosts.Add(recent, $"10.0.0.{i}");
		}
		Assert.Equal(RecentLanHosts.MaxAddresses, recent.Count);
		Assert.Equal($"10.0.0.{RecentLanHosts.MaxAddresses + 3}", recent[0]);
	}
}

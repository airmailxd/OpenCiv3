using System.Net;
using C7Engine.Network;
using Xunit;

namespace EngineTests.GameData;

public class LanAddressTest {
	// A stopped Tailscale's adapter has a 169.254 address, which the host
	// must not tell players to join by.
	[Theory]
	[InlineData("169.254.83.107", true)]
	[InlineData("100.101.88.2", false)]
	[InlineData("192.168.7.87", false)]
	public void LinkLocalAddressesAreNotOffered(string address, bool linkLocal) {
		Assert.Equal(linkLocal, LanDiscovery.IsLinkLocal(IPAddress.Parse(address)));
	}
}

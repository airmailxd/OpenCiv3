using System.Threading.Tasks;
using C7Engine.Network;
using Xunit;

namespace Relay.Tests;

// Choosing a relay, and telling players which one a game is on.
public class OnlineRelayTests {
	[Fact]
	public void AGameOnTheDefaultRelayIsJoinedWithItsCodeAlone() {
		Assert.Equal("KQ7-4MZ", OnlineRelay.Invite("KQ7-4MZ", OnlineRelay.DefaultUrl));
	}

	[Theory]
	[InlineData("wss://relay.example.org", "KQ7-4MZ@relay.example.org")]
	[InlineData("relay.example.org", "KQ7-4MZ@relay.example.org")]
	[InlineData("https://relay.example.org/", "KQ7-4MZ@relay.example.org")]
	[InlineData("wss://relay.example.org:8443/c7", "KQ7-4MZ@relay.example.org:8443/c7")]
	[InlineData("ws://localhost:5080", "KQ7-4MZ@ws://localhost:5080")]
	public void AnotherRelayIsNamedInTheInvite(string relayUrl, string invite) {
		Assert.Equal(invite, OnlineRelay.Invite("KQ7-4MZ", relayUrl));

		// And reading the invite back finds the same game.
		Assert.True(OnlineRelay.TryParseInvite(invite, out string code, out string parsedRelay));
		Assert.Equal("KQ74MZ", code);
		Assert.True(OnlineRelay.SameRelay(relayUrl, parsedRelay));
	}

	[Theory]
	[InlineData("kq7-4mz")]
	[InlineData(" KQ7 4MZ ")]
	[InlineData("KQ74MZ@")]
	public void ACodeAloneUsesTheChosenRelay(string typed) {
		Assert.True(OnlineRelay.TryParseInvite(typed, out string code, out string relayUrl));
		Assert.Equal("KQ74MZ", code);
		Assert.Equal(OnlineRelay.Url, relayUrl);
	}

	[Theory]
	[InlineData("")]
	[InlineData("KQ7")]
	[InlineData("KQ7-4MZ0@relay.example.org")]
	[InlineData("hello@relay.example.org")]
	public void WhatIsntACodeIsRefused(string typed) {
		Assert.False(OnlineRelay.TryParseInvite(typed, out _, out _));
	}

	[Fact]
	public async Task ARunningRelayAnswersTheCheck() {
		await using TestRelay relay = await TestRelay.Start();
		Assert.Null(await OnlineRelay.CheckAsync(relay.Url));
	}

	[Fact]
	public async Task AnAddressWithNoRelaySaysSo() {
		Assert.NotNull(await OnlineRelay.CheckAsync("ws://127.0.0.1:1"));
		Assert.NotNull(await OnlineRelay.CheckAsync(OnlineRelay.DefaultUrl));
		Assert.NotNull(await OnlineRelay.CheckAsync("ftp://relay.example.org"));
	}
}

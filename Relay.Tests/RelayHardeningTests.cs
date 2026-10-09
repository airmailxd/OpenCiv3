extern alias relay;

using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;
using C7Engine.Network;
using C7Relay;
using Xunit;
using RateLimits = relay::C7Relay.RateLimits;

namespace Relay.Tests;

// What keeps the relay's rooms usable: guests nobody let in giving way,
// guests sending too much, bans by key, and rooms that moved.
public class RelayHardeningTests {
	private static Uri HostUri(TestRelay relay, string banScope = null, string movedFrom = null, string movedFromKey = null) {
		return RelayConnection.HostUri(relay.Url, banScope: banScope, movedFromCode: movedFrom, movedFromKey: movedFromKey);
	}

	[Fact]
	public async Task AFullRoomMakesWayForNewcomersOverGuestsNobodyLetIn() {
		await using TestRelay relay = await TestRelay.Start(options => options.MaxGuestsPerRoom = 2);
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay, admits: true));
		string code = (await host.Welcome()).code;

		await using RawClient ann = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await ann.Welcome();
		uint annID = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);
		await host.SendText(new RelayControl(RelayControl.Admit, guest: annID));

		await using RawClient idle = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await idle.Welcome();
		uint idleID = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);

		// The one not let in gives way; the one let in stays.
		await using RawClient newcomer = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await newcomer.Welcome();
		Assert.Equal(RelayCloseCodes.RoomFull, (await idle.Closed()).status);
		byte[] closed = await host.ReceiveBinary();
		Assert.Equal(RelayProtocol.Close, closed[0]);
		Assert.Equal(idleID, RelayProtocol.GuestAt(closed, 0));
		Assert.Equal(WebSocketState.Open, ann.Socket.State);
	}

	[Fact]
	public async Task AGuestSendingTooMuchIsDroppedNotItsHost() {
		await using TestRelay relay = await TestRelay.Start(options => {
			options.GuestMessagesPerSecond = 1;
			options.GuestBurstMessages = 5;
		});
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await guest.Welcome();
		for (int i = 0; i < 10; ++i) {
			await guest.Send([1, 2, 3]);
		}
		Assert.Equal(RelayCloseCodes.TooMuch, (await guest.Closed()).status);
		Assert.Equal(WebSocketState.Open, host.Socket.State);
	}

	[Fact]
	public async Task AHostBansByTheKeyWhateverTheIDStandsFor() {
		await using TestRelay relay = await TestRelay.Start();
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await guest.Welcome();
		uint id = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);
		string key = Assert.Contains(id, host.GuestKeys);

		// An ID that isn't the guest's still bans its key, and closes nobody.
		await host.SendText(new RelayControl(RelayControl.Ban, guest: id + 100, bans: [key]));
		await RelayServerTests.WaitUntil(() => {
			lock (relay.RoomFor(code)) {
				return relay.RoomFor(code).Bans.Contains(key);
			}
		});
		Assert.Equal(WebSocketState.Open, guest.Socket.State);
		await using RawClient again = await RawClient.Connect(RawClient.JoinUri(relay, code));
		Assert.Equal(RelayCloseCodes.Banned, (await again.Closed()).status);
	}

	[Fact]
	public async Task BansHoldInAnotherRoomWithTheSameScope() {
		await using TestRelay relay = await TestRelay.Start();
		const string scope = "0123456789abcdef0123456789abcdef";
		string key;
		await using (RawClient first = await RawClient.Connect(HostUri(relay, scope))) {
			string code = (await first.Welcome()).code;
			await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, code));
			await guest.Welcome();
			uint id = RelayProtocol.GuestAt(await first.ReceiveBinary(), 0);
			key = first.GuestKeys[id];
		}

		await using RawClient second = await RawClient.Connect(HostUri(relay, scope));
		string newCode = (await second.Welcome()).code;
		await second.SendText(new RelayControl(RelayControl.Bans, bans: [key]));
		await RelayServerTests.WaitUntil(() => {
			lock (relay.RoomFor(newCode)) {
				return relay.RoomFor(newCode).Bans.Contains(key);
			}
		});
		await using RawClient banned = await RawClient.Connect(RawClient.JoinUri(relay, newCode));
		Assert.Equal(RelayCloseCodes.Banned, (await banned.Closed()).status);
	}

	[Fact]
	public async Task GuestsOfARoomTheHostMovedFromAreToldTheNewCode() {
		await using TestRelay relay = await TestRelay.Start();
		RelayControl old;
		await using (RawClient first = await RawClient.Connect(HostUri(relay))) {
			old = await first.Welcome();
		}
		await RelayServerTests.WaitUntil(() => {
			lock (relay.RoomFor(old.code)) {
				return relay.RoomFor(old.code).Host == null;
			}
		});

		await using RawClient second = await RawClient.Connect(HostUri(relay, movedFrom: old.code, movedFromKey: old.key));
		RelayControl moved = await second.Welcome();
		Assert.NotEqual(old.code, moved.code);
		Assert.Equal(RelayProtocol.DefaultMaxHostMessageBytes, moved.maxMessageBytes);

		RelayException e = await Assert.ThrowsAsync<RelayException>(
			() => RelayConnection.ConnectAsync(RawClient.JoinUri(relay, old.code), RawClient.Timeout, default));
		Assert.Equal(RelayCloseCodes.Moved, e.CloseCode);
		Assert.Equal(moved.code, e.MovedTo);
	}

	[Fact]
	public void IPv6AddressesAreCountedByTheirSlash64() {
		Assert.Equal("2001:db8:1:2::/64", RateLimits.KeyOf(IPAddress.Parse("2001:db8:1:2:3:4:5:6")));
		Assert.Equal(RateLimits.KeyOf(IPAddress.Parse("2001:db8:1:2::1")), RateLimits.KeyOf(IPAddress.Parse("2001:db8:1:2:ffff::9")));
		Assert.Equal("192.0.2.7", RateLimits.KeyOf(IPAddress.Parse("::ffff:192.0.2.7")));
		Assert.Equal("127.0.0.1", RateLimits.KeyOf(IPAddress.Loopback));
	}
}

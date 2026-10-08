extern alias relay;

using System;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using C7Engine.Network;
using C7Relay;
using Xunit;
using RateLimits = relay::C7Relay.RateLimits;
using RelayOptions = relay::C7Relay.RelayOptions;
using RoomRegistry = relay::C7Relay.RoomRegistry;

namespace Relay.Tests;

// The relay on its own: rooms and codes, passing bytes between hosts and
// guests, and turning away what it should.
public class RelayServerTests {
	private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

	[Theory]
	[InlineData("KQ7-4MZ", "KQ74MZ")]
	[InlineData("kq74mz", "KQ74MZ")]
	[InlineData(" kq7 4mz ", "KQ74MZ")]
	[InlineData("KQ7-4M", null)]
	[InlineData("KQ7-4MZA", null)]
	[InlineData("KQ0-4MZ", null)]
	[InlineData("KQI-4MZ", null)]
	[InlineData("", null)]
	[InlineData(null, null)]
	public void JoinCodesAreReadHoweverTheyAreTyped(string typed, string code) {
		Assert.Equal(code, RelayProtocol.NormalizeCode(typed));
	}

	[Fact]
	public void MessagesWithTheHostCarryTheirGuests() {
		byte[] message = RelayProtocol.Encode(RelayProtocol.Data, [3u, 70000u], Bytes("hello"));
		Assert.True(RelayProtocol.TryReadHeader(message, out byte kind, out int count));
		Assert.Equal(RelayProtocol.Data, kind);
		Assert.Equal(2, count);
		Assert.Equal(3u, RelayProtocol.GuestAt(message, 0));
		Assert.Equal(70000u, RelayProtocol.GuestAt(message, 1));
		Assert.Equal("hello", Encoding.UTF8.GetString(RelayProtocol.Payload(message, count)));

		Assert.False(RelayProtocol.TryReadHeader([RelayProtocol.Data, 2, 0, 1, 0, 0, 0], out _, out _));
		Assert.False(RelayProtocol.TryReadHeader([RelayProtocol.Data, 0, 0], out _, out _));
	}

	[Fact]
	public void CodesAreRandomAndOnlyTheRelayCanMakeTheirKeys() {
		RelayOptions options = new() { KeySecret = "secret" };
		RoomRegistry rooms = new(options);
		string a = rooms.Create("9").Code;
		string b = rooms.Create("9").Code;
		Assert.NotEqual(a, b);
		Assert.Equal(a, RelayProtocol.NormalizeCode(RelayProtocol.FormatCode(a)));
		Assert.True(rooms.IsKeyFor(a, rooms.KeyFor(a)));
		Assert.False(rooms.IsKeyFor(a, rooms.KeyFor(b)));
		Assert.False(rooms.IsKeyFor(a, null));

		// The same secret makes the same keys, as after a restart.
		Assert.Equal(rooms.KeyFor(a), new RoomRegistry(options).KeyFor(a));
		Assert.NotEqual(rooms.KeyFor(a), new RoomRegistry(new RelayOptions { KeySecret = "other" }).KeyFor(a));
	}

	[Fact]
	public void RateLimitsCountEachAddress() {
		RateLimits limits = new(new RelayOptions { ConnectionsPerMinute = 2, FailedJoinsPerTenMinutes = 1 });
		Assert.True(limits.AllowConnection("a"));
		Assert.True(limits.AllowConnection("a"));
		Assert.False(limits.AllowConnection("a"));
		Assert.True(limits.AllowConnection("b"));
		Assert.False(limits.IsLockedOut("a"));
		limits.NoteFailedJoin("a");
		Assert.True(limits.IsLockedOut("a"));
		Assert.False(limits.IsLockedOut("b"));
	}

	[Fact]
	public async Task AHostGetsACodeThatGuestsJoinWith() {
		await using TestRelay relay = await TestRelay.Start();
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		RelayControl welcome = await host.Welcome();
		Assert.NotNull(RelayProtocol.NormalizeCode(welcome.code));
		Assert.False(string.IsNullOrEmpty(welcome.key));
		Assert.True(welcome.pingSeconds > 0);

		// With the code as players see it, in any case.
		await using RawClient ann = await RawClient.Connect(RawClient.JoinUri(relay, RelayProtocol.FormatCode(welcome.code).ToLowerInvariant()));
		await ann.Welcome();
		byte[] open = await host.ReceiveBinary();
		Assert.True(RelayProtocol.TryReadHeader(open, out byte kind, out int count));
		Assert.Equal(RelayProtocol.Open, kind);
		uint annID = RelayProtocol.GuestAt(open, 0);

		await using RawClient bob = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		await bob.Welcome();
		uint bobID = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);
		Assert.NotEqual(annID, bobID);

		// Each guest's bytes reach the host marked with who sent them.
		await ann.Send(Bytes("from ann"));
		await bob.Send(Bytes("from bob"));
		for (int i = 0; i < 2; ++i) {
			byte[] data = await host.ReceiveBinary();
			Assert.True(RelayProtocol.TryReadHeader(data, out kind, out count));
			Assert.Equal(RelayProtocol.Data, kind);
			string text = Encoding.UTF8.GetString(RelayProtocol.Payload(data, count));
			Assert.Equal(RelayProtocol.GuestAt(data, 0) == annID ? "from ann" : "from bob", text);
		}

		// The host's reach just the guests they're for.
		await host.Send(RelayProtocol.Encode(RelayProtocol.Data, bobID, Bytes("for bob")));
		await host.Send(RelayProtocol.Encode(RelayProtocol.Data, annID, Bytes("for ann")));
		Assert.Equal("for bob", Encoding.UTF8.GetString(await bob.ReceiveBinary()));
		Assert.Equal("for ann", Encoding.UTF8.GetString(await ann.ReceiveBinary()));

		// And bytes for everyone are sent once, for all of them.
		await host.Send(RelayProtocol.Encode(RelayProtocol.Data, [annID, bobID], Bytes("for everyone")));
		Assert.Equal("for everyone", Encoding.UTF8.GetString(await ann.ReceiveBinary()));
		Assert.Equal("for everyone", Encoding.UTF8.GetString(await bob.ReceiveBinary()));

		using HttpClient http = new();
		using JsonDocument health = JsonDocument.Parse(await http.GetStringAsync(relay.HttpUrl + "/health"));
		Assert.Equal("ok", health.RootElement.GetProperty("status").GetString());
		Assert.Equal(1, health.RootElement.GetProperty("rooms").GetInt32());
		Assert.Equal(2, health.RootElement.GetProperty("guests").GetInt32());
	}

	[Fact]
	public async Task TheHostHearsWhenAGuestGoesAndCanSendOneAway() {
		await using TestRelay relay = await TestRelay.Start();
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		RawClient ann = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await ann.Welcome();
		uint annID = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);
		await using RawClient bob = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await bob.Welcome();
		uint bobID = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);

		await ann.DisposeAsync();
		byte[] close = await host.ReceiveBinary();
		Assert.Equal(RelayProtocol.Close, close[0]);
		Assert.Equal(annID, RelayProtocol.GuestAt(close, 0));

		await host.Send(RelayProtocol.Encode(RelayProtocol.Close, bobID));
		(int? status, string reason) = await bob.Closed();
		Assert.Equal(RelayCloseCodes.ClosedByHost, status);
		Assert.False(string.IsNullOrEmpty(reason));
	}

	[Fact]
	public async Task AHostThatDropsClaimsItsCodeAgainWithItsKey() {
		await using TestRelay relay = await TestRelay.Start();
		RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		RelayControl welcome = await host.Welcome();
		await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		await guest.Welcome();

		// The guests go with the host.
		await host.DisposeAsync();
		Assert.Equal(RelayCloseCodes.HostAway, (await guest.Closed()).status);

		// Guests can't join until it's back.
		await using RawClient early = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		Assert.Equal(RelayCloseCodes.HostAway, (await early.Closed()).status);

		// Nobody else can claim the code.
		await using RawClient impostor = await RawClient.Connect(RawClient.HostUri(relay, welcome.code, "not the key"));
		Assert.Equal(RelayCloseCodes.WrongKey, (await impostor.Closed()).status);

		await using RawClient back = await RawClient.Connect(RawClient.HostUri(relay, welcome.code, welcome.key));
		RelayControl again = await back.Welcome();
		Assert.Equal(welcome.code, again.code);
		Assert.Equal(welcome.key, again.key);
		await using RawClient late = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		await late.Welcome();
		Assert.Equal(RelayProtocol.Open, (await back.ReceiveBinary())[0]);
	}

	[Fact]
	public async Task AHostClaimsItsCodeEvenAfterItsRoomHasGone() {
		await using TestRelay relay = await TestRelay.Start(options => {
			options.RoomTtlSeconds = 0.1;
			options.SweepIntervalSeconds = 0.1;
		});
		RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		RelayControl welcome = await host.Welcome();
		await host.DisposeAsync();

		await WaitUntil(() => relay.Hub.Rooms.Count == 0);
		await using RawClient gone = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		Assert.Equal(RelayCloseCodes.UnknownRoom, (await gone.Closed()).status);

		await using RawClient back = await RawClient.Connect(RawClient.HostUri(relay, welcome.code, welcome.key));
		Assert.Equal(welcome.code, (await back.Welcome()).code);
		await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		await guest.Welcome();
	}

	[Fact]
	public async Task AHostBackBeforeTheRelayNoticedReplacesItsOldConnection() {
		await using TestRelay relay = await TestRelay.Start();
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		RelayControl welcome = await host.Welcome();
		await using RawClient back = await RawClient.Connect(RawClient.HostUri(relay, welcome.code, welcome.key));
		await back.Welcome();
		Assert.Equal(RelayCloseCodes.Replaced, (await host.Closed()).status);
	}

	[Fact]
	public async Task WrongCodesAndVersionsAreTurnedAwayWithAReason() {
		await using TestRelay relay = await TestRelay.Start();
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;

		await using RawClient lost = await RawClient.Connect(RawClient.JoinUri(relay, code == "AAAAAA" ? "BBBBBB" : "AAAAAA"));
		(int? status, string reason) = await lost.Closed();
		Assert.Equal(RelayCloseCodes.UnknownRoom, status);
		Assert.Contains("code", reason);

		await using RawClient garbled = await RawClient.Connect(RawClient.JoinUri(relay, "not a code"));
		Assert.Equal(RelayCloseCodes.UnknownRoom, (await garbled.Closed()).status);

		Uri oldRelay = new($"{relay.Url}/join/{code}?v=999&game={LanProtocol.Version}");
		await using RawClient newer = await RawClient.Connect(oldRelay);
		Assert.Equal(RelayCloseCodes.UnsupportedVersion, (await newer.Closed()).status);

		Uri otherGame = new($"{relay.Url}/join/{code}?v={RelayProtocol.Version}&game=other");
		await using RawClient other = await RawClient.Connect(otherGame);
		(status, reason) = await other.Closed();
		Assert.Equal(RelayCloseCodes.GameVersionMismatch, status);
		Assert.Contains("version", reason);

		// What isn't a WebSocket is told what the relay is.
		using HttpClient http = new();
		HttpResponseMessage response = await http.GetAsync(relay.HttpUrl + "/host");
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task AnAddressTryingTooManyCodesIsLockedOut() {
		await using TestRelay relay = await TestRelay.Start(options => options.FailedJoinsPerTenMinutes = 3);
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		string wrong = code == "AAAAAA" ? "BBBBBB" : "AAAAAA";
		for (int i = 0; i < 3; ++i) {
			await using RawClient guess = await RawClient.Connect(RawClient.JoinUri(relay, wrong));
			Assert.Equal(RelayCloseCodes.UnknownRoom, (await guess.Closed()).status);
		}
		// Even the right code, for a while.
		await Assert.ThrowsAsync<WebSocketException>(() => RawClient.Connect(RawClient.JoinUri(relay, code)));
		RelayException relayError = await Assert.ThrowsAsync<RelayException>(
			() => RelayConnection.ConnectAsync(RawClient.JoinUri(relay, code), RawClient.Timeout, default));
		Assert.Equal(RelayCloseCodes.TooManyAttempts, relayError.CloseCode);
	}

	[Fact]
	public async Task ConnectionsAndRoomsAreLimited() {
		await using TestRelay relay = await TestRelay.Start(options => {
			options.MaxGuestsPerRoom = 1;
			options.MaxRooms = 2;
			options.RoomsPerHour = 100;
		});
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await guest.Welcome();
		await using RawClient extra = await RawClient.Connect(RawClient.JoinUri(relay, code));
		Assert.Equal(RelayCloseCodes.RoomFull, (await extra.Closed()).status);

		await using RawClient second = await RawClient.Connect(RawClient.HostUri(relay));
		await second.Welcome();
		await using RawClient third = await RawClient.Connect(RawClient.HostUri(relay));
		Assert.Equal(RelayCloseCodes.RelayFull, (await third.Closed()).status);

		await using TestRelay strict = await TestRelay.Start(options => options.RoomsPerHour = 1);
		await using RawClient first = await RawClient.Connect(RawClient.HostUri(strict));
		await first.Welcome();
		await using RawClient another = await RawClient.Connect(RawClient.HostUri(strict));
		Assert.Equal(RelayCloseCodes.TooManyAttempts, (await another.Closed()).status);

		await using TestRelay busy = await TestRelay.Start(options => options.ConnectionsPerMinute = 1);
		await using RawClient only = await RawClient.Connect(RawClient.HostUri(busy));
		await only.Welcome();
		await Assert.ThrowsAsync<WebSocketException>(() => RawClient.Connect(RawClient.HostUri(busy)));
	}

	[Fact]
	public async Task GuestsMayOnlySendSmallMessages() {
		await using TestRelay relay = await TestRelay.Start();
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await guest.Welcome();
		uint id = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);

		// The host may send more than a guest may, like a whole game.
		await host.Send(RelayProtocol.Encode(RelayProtocol.Data, id, new byte[512 * 1024]));
		Assert.Equal(512 * 1024, (await guest.ReceiveBinary()).Length);

		int limit = RelayProtocol.DefaultMaxGuestMessageBytes;
		await guest.Send(new byte[limit]);
		Assert.Equal(limit, RelayProtocol.Payload(await host.ReceiveBinary(), 1).Length);
		await guest.Send(new byte[limit + 1]);
		Assert.Equal((int)WebSocketCloseStatus.MessageTooBig, (await guest.Closed()).status);
		Assert.Equal(RelayProtocol.Close, (await host.ReceiveBinary())[0]);
		await WaitUntil(() => relay.Hub.ReceiveBuffers.Used == 0);
	}

	[Fact]
	public async Task TooLargeAMessageFromTheHostClosesItsConnection() {
		await using TestRelay relay = await TestRelay.Start(options => options.MaxHostMessageBytes = 64 * 1024);
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		await host.Welcome();
		await host.Send(new byte[64 * 1024 + 1]);
		Assert.Equal((int)WebSocketCloseStatus.MessageTooBig, (await host.Closed()).status);
	}

	[Fact]
	public async Task LargeMessagesShareABudget() {
		await using TestRelay relay = await TestRelay.Start(options => options.MaxReceiveBufferBytes = 64 * 1024);
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await guest.Welcome();
		await host.ReceiveBinary();
		await guest.Send(new byte[200 * 1024]);
		(int? status, string reason) = await guest.Closed();
		Assert.Equal(RelayCloseCodes.RelayFull, status);
		Assert.Contains("busy", reason);
		await WaitUntil(() => relay.Hub.ReceiveBuffers.Used == 0);
	}

	[Fact]
	public async Task ConnectionsOpenAtOnceAreLimited() {
		await using TestRelay relay = await TestRelay.Start(options => options.MaxConnectionsPerAddress = 2);
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await guest.Welcome();
		RelayException e = await Assert.ThrowsAsync<RelayException>(
			() => RelayConnection.ConnectAsync(RawClient.JoinUri(relay, code), RawClient.Timeout, default));
		Assert.Equal(RelayCloseCodes.TooManyAttempts, e.CloseCode);

		// Once one closes, there's room again.
		await guest.DisposeAsync();
		await WaitUntil(() => relay.Hub.Limits.OpenFrom("127.0.0.1") < 2);
		await using RawClient again = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await again.Welcome();

		await using TestRelay full = await TestRelay.Start(options => options.MaxConnections = 1);
		await using RawClient only = await RawClient.Connect(RawClient.HostUri(full));
		await only.Welcome();
		e = await Assert.ThrowsAsync<RelayException>(
			() => RelayConnection.ConnectAsync(RawClient.HostUri(full), RawClient.Timeout, default));
		Assert.Equal(RelayCloseCodes.RelayFull, e.CloseCode);
		Assert.Contains("busy", e.Message);
	}

	[Fact]
	public async Task AConnectionThatGoesQuietIsDropped() {
		await using TestRelay relay = await TestRelay.Start(options => {
			options.PingIntervalSeconds = 0.2;
			options.IdleTimeoutSeconds = 0.5;
		});
		await using RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		string code = (await host.Welcome()).code;
		await using RawClient quiet = await RawClient.Connect(RawClient.JoinUri(relay, code));
		quiet.AnswerPings = false;
		await quiet.Welcome();
		Assert.Equal(RelayCloseCodes.TimedOut, (await quiet.Closed()).status);

		// One that answers the pings stays.
		await using RawClient awake = await RawClient.Connect(RawClient.JoinUri(relay, code));
		await awake.Welcome();
		Task<(int?, string)> closed = awake.Closed();
		await Task.WhenAny(closed, Task.Delay(TimeSpan.FromSeconds(1.5)));
		Assert.False(closed.IsCompleted);
	}

	internal static async Task WaitUntil(Func<bool> condition) {
		DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(10);
		while (!condition()) {
			Assert.True(DateTime.UtcNow < giveUp, "Timed out waiting");
			await Task.Delay(20);
		}
	}
}

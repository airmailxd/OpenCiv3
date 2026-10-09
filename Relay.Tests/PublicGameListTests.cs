extern alias relay;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using C7Relay;
using Xunit;
using GameList = relay::C7Relay.GameList;
using RelayListing = relay::C7Relay.GameListing;


namespace Relay.Tests;

// The relay's public list of games: hosts list their games, and players
// find them at /games; and hosts ban guests through the relay.
public class PublicGameListTests {
	private static GameListing Listing(string name = "Ann's game", int seatsTotal = 4, int seatsTaken = 1, bool hasPassword = false) {
		return new GameListing(name, "Come and play", hasPassword, seatsTotal, seatsTaken, true, 0, false, "60x60", true, 300, true, "Ann", "en-US");
	}

	// A raw host, welcomed, with its code.
	private static async Task<(RawClient host, RelayControl welcome)> Host(TestRelay relay) {
		RawClient host = await RawClient.Connect(RawClient.HostUri(relay));
		return (host, await host.Welcome());
	}

	// Lists the game and waits for the relay's answer: why it wasn't
	// listed, or null.
	private static async Task<string> List(RawClient host, GameListing listing) {
		await host.SendText(new RelayControl(RelayControl.List, listing: listing));
		return (await Control(host, RelayControl.Listed)).error;
	}

	// The next text message of the type, skipping anything else.
	private static async Task<RelayControl> Control(RawClient client, string type) {
		while (true) {
			(WebSocketMessageType kind, byte[] data) = await client.Receive();
			Assert.True(kind != WebSocketMessageType.Close, $"Closed waiting for {type}: {client.Socket.CloseStatusDescription}");
			if (kind == WebSocketMessageType.Text && RelayControl.Parse(data) is RelayControl control && control.type == type) {
				return control;
			}
		}
	}

	private static async Task<PublicGameList> Games(TestRelay relay, string query = "") {
		using HttpClient http = new();
		string json = await http.GetStringAsync(relay.HttpUrl + RelayProtocol.GamesPath + query);
		return JsonSerializer.Deserialize<PublicGameList>(json);
	}

	[Theory]
	[InlineData("  Ann's   game  ", "Ann's game")]
	[InlineData("Tabs\tand\nnew\r\nlines", "Tabs and new lines")]
	[InlineData("Bell\u0007 and ‮reversed​", "Bell and reversed")]
	[InlineData("\u0000\u0001  \t", null)]
	[InlineData(null, null)]
	public void TextInAListingIsTidied(string text, string tidy) {
		Assert.Equal(tidy, GameList.TidyText(text, RelayProtocol.MaxGameNameLength));
	}

	[Fact]
	public void AListingIsCutToSizeAndKeptWithinReason() {
		RelayListing tidy = GameList.Tidy(new RelayListing(new string('x', 500), new string('y', 1000),
			seatsTotal: 1000, seatsTaken: -5, turnSeconds: double.NaN));
		Assert.Equal(RelayProtocol.MaxGameNameLength, tidy.name.Length);
		Assert.Equal(RelayProtocol.MaxGameDescriptionLength, tidy.description.Length);
		Assert.Equal(64, tidy.seatsTotal);
		Assert.Equal(0, tidy.seatsTaken);
		Assert.Null(tidy.turnSeconds);
		Assert.Null(GameList.Tidy(new RelayListing(" \u0001 ")));

		// Half an emoji isn't left at the end.
		string cut = GameList.TidyText(new string('a', RelayProtocol.MaxGameNameLength - 1) + "\U0001F600", RelayProtocol.MaxGameNameLength);
		Assert.False(char.IsHighSurrogate(cut[^1]));
	}

	[Fact]
	public async Task AListedGameIsInTheListUntilItsHostUnlistsItOrLeaves() {
		await using TestRelay relay = await TestRelay.Start();
		(RawClient host, RelayControl welcome) = await Host(relay);
		Assert.Empty((await Games(relay)).games);

		Assert.Null(await List(host, Listing("  Ann's\tgame ")));
		PublicGame game = Assert.Single((await Games(relay)).games);
		Assert.Equal(welcome.code, game.code);
		Assert.Equal(LanVersion, game.gameVersion);
		Assert.Equal("Ann's game", game.game.name);
		Assert.Equal("Come and play", game.game.description);
		Assert.Equal(3, game.seatsOpen);
		Assert.Equal("60x60", game.game.mapSize);
		Assert.Equal(300, game.game.turnSeconds);

		// Listed again as the game changes.
		Assert.Null(await List(host, Listing() with { seatsTaken = 4, started = true, turn = 12 }));
		game = Assert.Single((await Games(relay)).games);
		Assert.Equal(0, game.seatsOpen);
		Assert.True(game.game.started);
		Assert.Equal(12, game.game.turn);

		// A game without a name isn't listed, and the listing stays as it was.
		Assert.NotNull(await List(host, Listing("\t")));
		Assert.Equal(12, Assert.Single((await Games(relay)).games).game.turn);

		await host.SendText(new RelayControl(RelayControl.Unlist));
		await Control(host, RelayControl.Listed);
		Assert.Empty((await Games(relay)).games);

		// A host that leaves takes its listing with it, though its room
		// waits for it.
		Assert.Null(await List(host, Listing()));
		Assert.Single((await Games(relay)).games);
		await host.DisposeAsync();
		await RelayServerTests.WaitUntil(() => {
			lock (relay.RoomFor(welcome.code)) {
				return relay.RoomFor(welcome.code).Listing == null;
			}
		});
		Assert.Empty((await Games(relay)).games);
		Assert.True(relay.Hub.Rooms.TryGet(welcome.code, out _));

		// And lists it again once back.
		await using RawClient back = await RawClient.Connect(RawClient.HostUri(relay, welcome.code, welcome.key));
		await back.Welcome();
		Assert.Empty((await Games(relay)).games);
		Assert.Null(await List(back, Listing()));
		Assert.Equal(welcome.code, Assert.Single((await Games(relay)).games).code);
	}

	private static readonly string LanVersion = C7Engine.Network.LanProtocol.Version.ToString();

	[Fact]
	public async Task AListingThatIsntRefreshedGoes() {
		await using TestRelay relay = await TestRelay.Start(options => {
			options.ListingTtlSeconds = 0.5;
			options.SweepIntervalSeconds = 0.1;
		});
		(RawClient host, RelayControl welcome) = await Host(relay);
		await using RawClient hostConnection = host;
		Assert.Null(await List(host, Listing()));
		Assert.Single((await Games(relay)).games);
		await RelayServerTests.WaitUntil(() => {
			lock (relay.RoomFor(welcome.code)) {
				return relay.RoomFor(welcome.code).Listing == null;
			}
		});
		Assert.Empty((await Games(relay)).games);

		// Listing it again brings it back.
		Assert.Null(await List(host, Listing()));
		Assert.Single((await Games(relay)).games);
	}

	[Fact]
	public async Task TheListCanBeFilteredAndIsCapped() {
		await using TestRelay relay = await TestRelay.Start(options => {
			options.MaxPublicGames = 3;
			options.MaxListingsPerAddress = 10;
		});
		List<RawClient> hosts = [];
		try {
			async Task Add(GameListing listing) {
				(RawClient host, _) = await Host(relay);
				hosts.Add(host);
				Assert.Null(await List(host, listing));
			}
			await Add(Listing("Open"));
			await Add(Listing("Full", seatsTotal: 2, seatsTaken: 2));
			await Add(Listing("Locked", hasPassword: true));
			await Add(Listing("Open too"));

			PublicGameList all = await Games(relay);
			Assert.Equal(4, all.total);
			Assert.Equal(3, all.games.Count);
			// Games with room come first.
			Assert.DoesNotContain(all.games, g => g.game.name == "Full");

			Assert.Equal(["Locked", "Open", "Open too"], (await Games(relay, "?notFull=true")).games.Select(g => g.game.name).Order());
			Assert.Equal(["Full", "Open", "Open too"], (await Games(relay, "?noPassword=1")).games.Select(g => g.game.name).Order());
			Assert.Equal(["Open", "Open too"], (await Games(relay, "?noPassword=true&notFull=true")).games.Select(g => g.game.name).Order());
			Assert.Single((await Games(relay, "?limit=1")).games);
			Assert.Equal(3, (await Games(relay, "?limit=500")).games.Count);
			Assert.Equal(4, (await Games(relay, $"?version={LanVersion}")).total);
			Assert.Empty((await Games(relay, "?version=0")).games);
		} finally {
			foreach (RawClient host in hosts) {
				await host.DisposeAsync();
			}
		}
	}

	[Fact]
	public async Task AnAddressListsOnlySoManyGames() {
		await using TestRelay relay = await TestRelay.Start(options => options.MaxListingsPerAddress = 2);
		(RawClient a, _) = await Host(relay);
		(RawClient b, _) = await Host(relay);
		(RawClient c, _) = await Host(relay);
		await using RawClient a_ = a, b_ = b, c_ = c;
		Assert.Null(await List(a, Listing("A")));
		Assert.Null(await List(b, Listing("B")));
		string error = await List(c, Listing("C"));
		Assert.Contains("already lists 2 games", error);
		Assert.Equal(2, (await Games(relay)).total);

		// Listing one already listed again is fine, and once one goes,
		// another may be listed.
		Assert.Null(await List(a, Listing("A again")));
		await b.SendText(new RelayControl(RelayControl.Unlist));
		await Control(b, RelayControl.Listed);
		Assert.Null(await List(c, Listing("C")));
		Assert.Equal(["A again", "C"], (await Games(relay)).games.Select(g => g.game.name).Order());
	}

	[Fact]
	public async Task AskingForTheListTooOftenIsTurnedAway() {
		await using TestRelay relay = await TestRelay.Start(options => options.GameListRequestsPerMinute = 2);
		using HttpClient http = new();
		Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(relay.HttpUrl + "/games")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(relay.HttpUrl + "/games")).StatusCode);
		Assert.Equal(HttpStatusCode.TooManyRequests, (await http.GetAsync(relay.HttpUrl + "/games")).StatusCode);
	}

	[Fact]
	public async Task TheListShowsHowQuicklyTheHostAnswers() {
		await using TestRelay relay = await TestRelay.Start(options => {
			options.PingIntervalSeconds = 0.1;
			options.GameListRequestsPerMinute = 100_000;
		});
		(RawClient host, _) = await Host(relay);
		await using RawClient hostConnection = host;
		Assert.Null(await List(host, Listing()));
		// The raw client answers pings while it waits for anything else.
		Task<(WebSocketMessageType, byte[])> reading = host.Receive();
		PublicGame game = null;
		await RelayServerTests.WaitUntil(() => {
			game = Games(relay).GetAwaiter().GetResult().games.Single();
			return game.hostPingMs != null;
		});
		Assert.InRange(game.hostPingMs.Value, 0, 5000);
		Assert.False(reading.IsCompleted);

	}

	[Fact]
	public async Task ABannedGuestsAddressIsTurnedAwayFromTheRoom() {
		await using TestRelay relay = await TestRelay.Start();
		(RawClient host, RelayControl welcome) = await Host(relay);
		await using RawClient hostConnection = host;
		(RawClient other, RelayControl otherWelcome) = await Host(relay);
		await using RawClient otherConnection = other;

		await using RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		await guest.Welcome();
		uint id = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);

		await host.SendText(new RelayControl(RelayControl.Ban, guest: id));
		(int? status, string reason) = await guest.Closed();
		Assert.Equal(RelayCloseCodes.Banned, status);
		Assert.Contains("banned", reason);
		RelayControl banned = await Control(host, RelayControl.Banned);
		Assert.Equal(id, banned.guest);
		string key = Assert.Single(banned.bans);

		// The guest can't come back to this room from its address.
		await using RawClient again = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		Assert.Equal(RelayCloseCodes.Banned, (await again.Closed()).status);

		// But can join other games.
		await using RawClient elsewhere = await RawClient.Connect(RawClient.JoinUri(relay, otherWelcome.code));
		await elsewhere.Welcome();

		// A relay that restarted, with the same secret, has the ban back
		// once the host gives it its key.
		await using TestRelay restarted = await TestRelay.Start();
		await using RawClient back = await RawClient.Connect(RawClient.HostUri(restarted, welcome.code, welcome.key));
		await back.Welcome();
		await using RawClient before = await RawClient.Connect(RawClient.JoinUri(restarted, welcome.code));
		await before.Welcome();
		await back.SendText(new RelayControl(RelayControl.Bans, bans: [key]));
		// Waits for the relay to have read it.
		Assert.Null(await List(back, Listing()));
		await using RawClient after = await RawClient.Connect(RawClient.JoinUri(restarted, welcome.code));
		Assert.Equal(RelayCloseCodes.Banned, (await after.Closed()).status);
	}

	[Fact]
	public async Task AGuestThatLeftCanStillBeBanned() {
		await using TestRelay relay = await TestRelay.Start();
		(RawClient host, RelayControl welcome) = await Host(relay);
		await using RawClient hostConnection = host;
		RawClient guest = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		await guest.Welcome();
		uint id = RelayProtocol.GuestAt(await host.ReceiveBinary(), 0);
		await guest.DisposeAsync();
		Assert.Equal(RelayProtocol.Close, (await host.ReceiveBinary())[0]);

		await host.SendText(new RelayControl(RelayControl.Ban, guest: id));
		Assert.Equal(id, (await Control(host, RelayControl.Banned)).guest);
		await using RawClient again = await RawClient.Connect(RawClient.JoinUri(relay, welcome.code));
		Assert.Equal(RelayCloseCodes.Banned, (await again.Closed()).status);
	}

	[Fact]
	public void ControlMessagesOnlyCarryWhatTheySay() {
		string json = Encoding.UTF8.GetString(new RelayControl(RelayControl.Ping).ToBytes());
		Assert.DoesNotContain("listing", json);
		Assert.DoesNotContain("bans", json);
		Assert.Null(RelayControl.Parse(Encoding.UTF8.GetBytes(new string(' ', RelayProtocol.MaxControlBytes + 1))));
	}
}

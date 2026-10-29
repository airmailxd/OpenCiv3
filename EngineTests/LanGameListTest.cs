using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using C7Engine;
using C7Engine.Network;
using C7GameData.Save;
using Xunit;

namespace EngineTests;

// The LAN game list laid out like Steam's server browser: what the hosts'
// discovery replies carry, and the list's tabs, filters, sorting, cells and
// favorites. Kept out of EngineTests.Network since nothing here opens a
// socket.
public class LanGameListTest {
	private static readonly int Version = LanProtocol.Version;

	private static FoundHost Host(string address, string name, int open = 1, bool started = false, bool password = false,
		int? version = null, int port = LanProtocol.DefaultPort, int? latency = 10, int total = 4, string gameName = null) {
		return new FoundHost(address, new DiscoveryReply(name, port, open, started, password, version ?? Version, gameName, total,
			total - open, "80x80", started ? 50 : 1, "Standard"), latency);
	}

	// ---- The reply ----

	[Fact]
	public void ReplyRoundTripsWithTheListsFields() {
		DiscoveryReply reply = new("Stefan", 47777, 2, true, true, Version, "Friday Night Civ", 6, 4, "100x100", 57, "Eternal War", false);
		byte[] json = NetSerialization.SerializeData(reply);
		Assert.Equal(reply, NetSerialization.DeserializeRequired<DiscoveryReply>(json));
	}

	[Fact]
	public void AnOlderHostsReplyStillReadsWithBlanks() {
		// As hosts sent it before the list's fields were added.
		byte[] json = Encoding.UTF8.GetBytes("""{"hostName":"Old","port":47777,"openSeats":2,"started":false,"hasPassword":true,"version":14}""");
		DiscoveryReply reply = NetSerialization.DeserializeRequired<DiscoveryReply>(json);
		Assert.Equal("Old", reply.hostName);
		Assert.True(reply.hasPassword);
		Assert.Equal(14, reply.version);
		Assert.Null(reply.gameName);
		Assert.Equal(0, reply.seatsTotal);
		Assert.Null(reply.mapSize);
		Assert.Null(reply.gameType);
		Assert.True(reply.spectatorsAllowed);
		Assert.Equal("2 open", LanGameList.PlayersText(reply));
		Assert.Equal("Old's game", LanGameList.GameText(new LanGameRow("10.0.0.2", 47777, new FoundHost("10.0.0.2", reply), false, false)));

		// And before passwords and versions.
		byte[] oldest = Encoding.UTF8.GetBytes("""{"hostName":"Older","port":47777,"openSeats":1,"started":true}""");
		DiscoveryReply first = NetSerialization.DeserializeRequired<DiscoveryReply>(oldest);
		Assert.Equal(0, first.version);
		Assert.Equal("old", LanGameList.VersionText(first));
		Assert.Equal("Started", LanGameList.TurnText(first));
	}

	[Fact]
	public void LatencyIsFromTheRequestThatHostAnswered() {
		long second = Stopwatch.Frequency;
		// Asked directly at 1 s, answered at 1.05 s: 50 ms, not 1050.
		Assert.Equal(50, LanDiscovery.LatencyMs(0, second, second + second / 20));
		// Answered before being asked directly: the broadcast's answer.
		Assert.Equal(20, LanDiscovery.LatencyMs(0, second, second / 50));
		Assert.Equal(20, LanDiscovery.LatencyMs(0, null, second / 50));
	}

	// ---- Tabs ----

	[Fact]
	public void TabsSplitTheHostsByWhereTheyWereFound() {
		List<FoundHost> found = [Host("192.168.1.20", "Lan"), Host("100.88.12.3", "Tail")];
		List<string> none = [];
		Assert.Equal(2, LanGameList.Rows(LanGameTab.All, found, none, none).Count);
		Assert.Equal("Lan", Assert.Single(LanGameList.Rows(LanGameTab.Lan, found, none, none)).reply.hostName);
		Assert.Equal("Tail", Assert.Single(LanGameList.Rows(LanGameTab.Tailscale, found, none, none)).reply.hostName);
		Assert.Empty(LanGameList.Rows(LanGameTab.Favorites, found, none, none));
	}

	[Fact]
	public void FavoritesAndHistoryListHostsThatDidntAnswerToo() {
		List<FoundHost> found = [Host("192.168.1.20", "Lan"), Host("100.88.12.3", "Tail", port: 47780)];
		List<string> favorites = ["192.168.1.20", "10.0.0.7:47781"];
		// Recent hosts are kept without ports; "mybox" resolved to the tailnet host.
		List<string> recent = ["mybox", "192.168.1.77"];
		Dictionary<string, List<string>> resolved = new() { ["mybox"] = ["100.88.12.3"] };

		List<LanGameRow> starred = LanGameList.Rows(LanGameTab.Favorites, found, favorites, recent, resolved);
		Assert.Equal(["192.168.1.20", "10.0.0.7:47781"], starred.Select(r => r.entry));
		Assert.True(starred[0].answered);
		Assert.False(starred[1].answered);
		Assert.Equal(47781, starred[1].port);

		List<LanGameRow> history = LanGameList.Rows(LanGameTab.History, found, favorites, recent, resolved);
		Assert.Equal(["100.88.12.3", "192.168.1.77"], history.Select(r => r.address));
		Assert.True(history[0].answered);
		Assert.True(history[0].recent);
		Assert.False(history[1].answered);

		// A favorite at another port is another host.
		Assert.False(LanGameList.Matches("192.168.1.20:47790", found[0]));
		Assert.True(LanGameList.Matches("192.168.1.20:47790", found[0], anyPort: true));
	}

	// ---- Filters and sorting ----

	[Fact]
	public void FiltersHideWhatTheyNameButNotHostsThatDidntAnswer() {
		List<LanGameRow> rows = [
			Row(Host("192.168.1.1", "Open")),
			Row(Host("192.168.1.2", "Full", open: 0)),
			Row(Host("192.168.1.3", "Going", started: true)),
			Row(Host("192.168.1.4", "Locked", password: true)),
			Row(Host("192.168.1.5", "Newer", version: Version + 1)),
			new LanGameRow("10.0.0.7", LanProtocol.DefaultPort, null, true, false),
		];
		Assert.Equal(6, LanGameList.Filter(rows, new LanGameFilter()).Count);
		Assert.Equal(["Open", null], LanGameList.Filter(rows, new LanGameFilter("", true, true, true, true)).Select(r => r.reply?.hostName));
		Assert.Equal(["Locked"], LanGameList.Filter(rows, new LanGameFilter("lock")).Select(r => r.reply?.hostName));
		Assert.Equal(["10.0.0.7"], LanGameList.Filter(rows, new LanGameFilter("10.0.0")).Select(r => r.address));
	}

	[Fact]
	public void SortingKeepsHostsThatAnsweredFirst() {
		List<LanGameRow> rows = [
			new LanGameRow("10.0.0.7", LanProtocol.DefaultPort, null, true, false),
			Row(Host("192.168.1.10", "B", latency: 40)),
			Row(Host("192.168.1.9", "A", latency: 5)),
			Row(Host("192.168.1.11", "C", latency: null)),
		];
		Assert.Equal(["A", "B", "C", null], LanGameList.Sort(rows, LanGameColumn.Latency, true).Select(r => r.reply?.hostName));
		Assert.Equal(["C", "B", "A", null], LanGameList.Sort(rows, LanGameColumn.Latency, false).Select(r => r.reply?.hostName));
		// Addresses by their numbers: .9 before .10.
		Assert.Equal(["A", "B", "C", null], LanGameList.Sort(rows, LanGameColumn.Address, true).Select(r => r.reply?.hostName));
	}

	[Fact]
	public void CellsSayWhatSteamsWould() {
		FoundHost host = Host("100.88.12.3", "Ana", open: 1, total: 5, port: 47780, gameName: "Kings & Nomads");
		LanGameRow row = Row(host);
		Assert.Equal("Kings & Nomads", LanGameList.GameText(row));
		Assert.Equal("4 / 5", LanGameList.PlayersText(host.reply));
		Assert.Equal("Lobby", LanGameList.TurnText(host.reply));
		Assert.Equal("100.88.12.3:47780 (Tailscale)", LanGameList.AddressText(row));
		Assert.Equal("10", LanGameList.LatencyText(host));
		Assert.Equal("50", LanGameList.TurnText(Host("192.168.1.2", "B", started: true).reply));
	}

	private static LanGameRow Row(FoundHost host) => new(host.address, host.reply.port, host, false, false);

	// ---- Game types ----

	[Fact]
	public void GameTypesAreToldByTheirRules() {
		Assert.Equal("Standard", LanGameList.GameTypeOf(new SaveGame()));
		SaveGame tide = new();
		tide.Rules.RisingTide = true;
		tide.Rules.GovernmentLocked = true;
		Assert.Equal("Rising Tide", LanGameList.GameTypeOf(tide));
		SaveGame war = new();
		war.Rules.PermanentWar = true;
		Assert.Equal("Eternal War", LanGameList.GameTypeOf(war));
		SaveGame kings = new();
		kings.Rules.SettlersOnlyInConqueredCities = true;
		Assert.Equal("Kings & Nomads", LanGameList.GameTypeOf(kings));
		SaveGame mdj = new();
		mdj.Rules.OwnTechTree = true;
		Assert.Equal("MDJ", LanGameList.GameTypeOf(mdj));
		SaveGame conquest = new() { ScenarioName = "1 Mesopotamia" };
		Assert.Equal("Mesopotamia", LanGameList.GameTypeOf(conquest));
		Assert.Equal("Kings & Nomads", LanGameList.GameTypeName(GameScenario.KingsAndNomads));
	}

	// ---- Favorites ----

	[Fact]
	public void FavoritesToggleAndKeepTheirPortsOnlyWhenNotTheDefault() {
		List<string> list = FavoriteLanHosts.Toggle([], $"192.168.1.20:{LanProtocol.DefaultPort}");
		Assert.Equal(["192.168.1.20"], list);
		list = FavoriteLanHosts.Toggle(list, " mybox:47780 ");
		Assert.Equal(["192.168.1.20", "mybox:47780"], list);
		Assert.Equal(["mybox:47780"], FavoriteLanHosts.Toggle(list, "192.168.1.20"));
		Assert.Equal(list, FavoriteLanHosts.Toggle(list, "a,b"));
		Assert.Equal(list, FavoriteLanHosts.Parse(FavoriteLanHosts.Format(list)));
		Assert.Equal(["x", "y"], FavoriteLanHosts.Parse("x, y,,X"));
	}
}

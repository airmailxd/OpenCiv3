using System.Collections.Generic;
using System.Linq;
using C7Engine.Network;
using C7Relay;
using Xunit;

namespace EngineTests.Network;

// The server browser's list of public games: what it hides, how it sorts,
// and the games joined lately.
public class PublicGamesTest {
	private static readonly string Version = LanProtocol.Version.ToString();

	private static PublicGame Game(string name, int seatsTotal = 4, int seatsTaken = 1, bool hasPassword = false,
		string version = null, int turn = 0, bool started = false, string mapSize = null, double? turnSeconds = null, int? hostPingMs = null) {
		GameListing listing = new(name, null, hasPassword, seatsTotal, seatsTaken, true, turn, started, mapSize, false, turnSeconds,
			true, $"{name} host");
		return new PublicGame($"CODE{name.Length:00}", version ?? Version, listing, seatsTotal - seatsTaken, hostPingMs);
	}

	private static readonly List<PublicGame> Games = [
		Game("Bravo", seatsTotal: 2, seatsTaken: 2, turn: 40, started: true, mapSize: "100x100", turnSeconds: 120, hostPingMs: 30),
		Game("alpha", hasPassword: true, mapSize: "60x60", hostPingMs: 80),
		Game("Charlie", version: "1", turn: 3, started: true, mapSize: "80x80"),
		Game("Delta", seatsTotal: 8, seatsTaken: 3, mapSize: "40x40", turnSeconds: 600, hostPingMs: 10),
	];

	private static List<string> Names(IEnumerable<PublicGame> games) => games.Select(g => g.game.name).ToList();

	[Fact]
	public void TheBrowserHidesWhatThePlayerAsks() {
		Assert.Equal(4, PublicGames.Filter(Games, new BrowserFilter()).Count);
		Assert.Equal(["alpha", "Charlie", "Delta"], Names(PublicGames.Filter(Games, new BrowserFilter(hideFull: true))));
		Assert.Equal(["Bravo", "Charlie", "Delta"], Names(PublicGames.Filter(Games, new BrowserFilter(hidePassword: true))));
		Assert.Equal(["Bravo", "alpha", "Delta"], Names(PublicGames.Filter(Games, new BrowserFilter(hideIncompatible: true))));
		Assert.Equal(["Delta"], Names(PublicGames.Filter(Games, new BrowserFilter(true, true, true))));
		Assert.False(PublicGames.IsCompatible(Games[2]));
	}

	[Fact]
	public void TheBrowserSortsByAnyColumnWithGamesItCanJoinFirst() {
		// Charlie's game is another version, so it's last whichever way.
		Assert.Equal(["alpha", "Bravo", "Delta", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.Name, true)));
		Assert.Equal(["Delta", "Bravo", "alpha", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.Name, false)));
		Assert.Equal(["Bravo", "alpha", "Delta", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.OpenSeats, true)));
		Assert.Equal(["Delta", "Bravo", "alpha", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.Players, false)));
		// Games in the lobby before those under way.
		Assert.Equal(["alpha", "Delta", "Bravo", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.Turn, true)));
		Assert.Equal(["Delta", "alpha", "Bravo", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.MapSize, true)));
		// No limit sorts as the longest.
		Assert.Equal(["Bravo", "Delta", "alpha", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.TurnTimer, true)));
		Assert.Equal(["Bravo", "Delta", "alpha", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.Password, true)));
		// Pings add the relay's own; unknown ones go last.
		Assert.Equal(["Delta", "Bravo", "alpha", "Charlie"], Names(PublicGames.Sort(Games, BrowserColumn.Ping, true, 25)));
		Assert.Equal(35, PublicGames.PingOf(Games[3], 25));
		Assert.Null(PublicGames.PingOf(Games[2], 25));
		Assert.Null(PublicGames.PingOf(Games[3], null));
	}

	[Fact]
	public void RecentGamesKeepTheLatestOfEachAtTheTop() {
		RecentOnlineGames.RecentGame ann = new("Ann's game", "KQ74MZ", "wss://relay.example.org");
		RecentOnlineGames.RecentGame bob = new("Bob's game", "ABCDEF", "wss://relay.example.org");
		List<RecentOnlineGames.RecentGame> games = RecentOnlineGames.With([], ann);
		games = RecentOnlineGames.With(games, bob);
		Assert.Equal([bob, ann], games);
		// Joining Ann's again, on the same relay as typed another way.
		RecentOnlineGames.RecentGame annAgain = ann with { relayUrl = "relay.example.org" };
		Assert.Equal([annAgain, bob], RecentOnlineGames.With(games, annAgain));
		// The same code on another relay is another game.
		Assert.Equal(3, RecentOnlineGames.With(games, ann with { relayUrl = "other.example.org" }).Count);

		for (int i = 0; i < 20; ++i) {
			games = RecentOnlineGames.With(games, new($"Game {i}", RelayProtocol.CodeAlphabet.Substring(i, 6), "relay.example.org"));
		}
		Assert.Equal(RecentOnlineGames.MaxGames, games.Count);

		Assert.Equal(ann, RecentOnlineGames.Parse(RecentOnlineGames.Format(ann)));
		Assert.Null(RecentOnlineGames.Parse("not a code wss://relay"));
		Assert.Null(RecentOnlineGames.Parse(null));
	}
}

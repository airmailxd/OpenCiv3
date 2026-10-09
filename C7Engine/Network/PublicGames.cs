using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using C7Relay;

namespace C7Engine.Network;

// The games hosts list publicly on an online relay, for the server browser:
// fetching them, and filtering and sorting them as the player asks. See
// RelayProtocol for how hosts list them.
public static class PublicGames {
	// How often the browser fetches the list again while it's open.
	public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(20);

	private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };

	// Whether this game can join the listed one: the host must run the same
	// version of the game's protocol.
	public static bool IsCompatible(PublicGame game) => game.gameVersion == LanProtocol.Version.ToString();

	// How long a message takes to reach the host and come back, through the
	// relay, in milliseconds: the time to the relay, and the host's own to
	// it. Null when either isn't known.
	public static int? PingOf(PublicGame game, int? relayPingMs) {
		return game.hostPingMs is int host && relayPingMs is int relay ? host + relay : null;
	}

	// The relay's address for plain HTTP requests, at the path.
	public static Uri HttpUri(string relayUrl, string path, string query = "") {
		Uri relay = RelayConnection.BaseUri(relayUrl) ?? throw new RelayException($"\"{relayUrl}\" isn't the address of a relay.");
		UriBuilder builder = new(relay) {
			Scheme = relay.Scheme == "ws" ? "http" : "https",
			// Changing the scheme drops the port.
			Port = relay.IsDefaultPort ? -1 : relay.Port,
			Query = query,
		};
		builder.Path = builder.Path.TrimEnd('/') + path;
		return builder.Uri;
	}

	// The games listed on the relay. Throws a RelayException saying what
	// went wrong if they can't be had.
	public static async Task<PublicGameList> FetchAsync(string relayUrl, CancellationToken cancel = default) {
		if (OnlineRelay.Problem(relayUrl) is string problem) {
			throw new RelayException(problem);
		}
		Uri uri = HttpUri(relayUrl, RelayProtocol.GamesPath);
		try {
			using HttpResponseMessage response = await http.GetAsync(uri, cancel).ConfigureAwait(false);
			if (response.StatusCode == HttpStatusCode.NotFound) {
				throw new RelayException("This relay doesn't keep a list of public games. It may need updating.");
			}
			if (response.StatusCode == HttpStatusCode.TooManyRequests) {
				throw new RelayException("The relay has had too many requests from your address. Wait a minute, then refresh.");
			}
			if (!response.IsSuccessStatusCode) {
				throw new RelayException($"The relay couldn't list its games ({(int)response.StatusCode}).");
			}
			byte[] json = await response.Content.ReadAsByteArrayAsync(cancel).ConfigureAwait(false);
			PublicGameList list = JsonSerializer.Deserialize<PublicGameList>(json);
			return list with { games = (list.games ?? []).Where(g => g?.game != null && g.code != null).ToList() };
		} catch (OperationCanceledException) when (!cancel.IsCancellationRequested) {
			throw new RelayException("The relay didn't answer in time.");
		} catch (HttpRequestException e) {
			throw new RelayException($"Couldn't reach the relay: {e.Message}", null, e);
		} catch (JsonException e) {
			throw new RelayException("The relay's list of games couldn't be read.", null, e);
		}
	}

	// How long the relay takes to answer, in milliseconds, or null if it
	// doesn't. The connection is made first, so that only the answer is
	// timed.
	public static async Task<int?> PingRelayAsync(string relayUrl, CancellationToken cancel = default) {
		if (OnlineRelay.Problem(relayUrl) != null) {
			return null;
		}
		Uri uri = HttpUri(relayUrl, RelayProtocol.HealthPath);
		try {
			(await http.GetAsync(uri, cancel).ConfigureAwait(false)).Dispose();
			Stopwatch timer = Stopwatch.StartNew();
			using HttpResponseMessage response = await http.GetAsync(uri, cancel).ConfigureAwait(false);
			return response.IsSuccessStatusCode ? (int)timer.ElapsedMilliseconds : null;
		} catch (Exception e) when (e is HttpRequestException or OperationCanceledException) {
			return null;
		}
	}

	// The games to show: without full games, those with a password, or
	// those this game can't join, as asked.
	public static List<PublicGame> Filter(IEnumerable<PublicGame> games, BrowserFilter filter) {
		return games
			.Where(g => !filter.hideFull || g.seatsOpen > 0)
			.Where(g => !filter.hidePassword || !g.game.hasPassword)
			.Where(g => !filter.hideIncompatible || IsCompatible(g))
			.ToList();
	}

	// The games in the column's order, with the games this game can join
	// first either way. Ties are by name.
	public static List<PublicGame> Sort(IEnumerable<PublicGame> games, BrowserColumn column, bool ascending, int? relayPingMs = null) {
		Func<PublicGame, IComparable> key = column switch {
			BrowserColumn.Host => g => g.game.hostName ?? "",
			BrowserColumn.Players => g => g.game.seatsTaken,
			BrowserColumn.OpenSeats => g => g.seatsOpen,
			// Games in the lobby come before those under way.
			BrowserColumn.Turn => g => g.game.started ? g.game.turn + 1 : 0,
			BrowserColumn.MapSize => g => MapArea(g.game.mapSize),
			BrowserColumn.Simultaneous => g => g.game.simultaneousTurns,
			// No time limit sorts as the longest.
			BrowserColumn.TurnTimer => g => g.game.turnSeconds ?? double.MaxValue,
			BrowserColumn.Password => g => g.game.hasPassword,
			BrowserColumn.Fog => g => g.game.hideUnseen,
			BrowserColumn.Ping => g => PingOf(g, relayPingMs) ?? int.MaxValue,
			_ => g => g.game.name ?? "",
		};
		IOrderedEnumerable<PublicGame> compatibleFirst = games.OrderByDescending(IsCompatible);
		IOrderedEnumerable<PublicGame> sorted = ascending
			? compatibleFirst.ThenBy(key, Comparer<IComparable>.Create(Compare))
			: compatibleFirst.ThenByDescending(key, Comparer<IComparable>.Create(Compare));
		return sorted.ThenBy(g => g.game.name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	private static int Compare(IComparable a, IComparable b) {
		return a is string x && b is string y ? StringComparer.CurrentCultureIgnoreCase.Compare(x, y) : a.CompareTo(b);
	}

	// A map size like "100x100" as its number of tiles, for sorting; 0 if
	// it isn't one.
	private static int MapArea(string size) {
		string[] parts = size?.Split('x') ?? [];
		return parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) ? w * h : 0;
	}
}

// What the browser hides.
public sealed record BrowserFilter(bool hideFull = false, bool hidePassword = false, bool hideIncompatible = false);

// The browser's columns, which it sorts by.
public enum BrowserColumn { Name, Host, Players, OpenSeats, Turn, MapSize, Simultaneous, TurnTimer, Password, Fog, Ping }

// The public games this player joined lately, newest first, to find again
// in the browser: their names, codes and relays, kept in C7.ini.
public static class RecentOnlineGames {
	public sealed record RecentGame(string name, string code, string relayUrl);

	public const int MaxGames = 10;
	private const string Section = "RecentOnlineGames";

	public static List<RecentGame> Load() {
		List<RecentGame> games = [];
		for (int i = 0; i < MaxGames; ++i) {
			string entry = C7Settings.GetSettingsValueOrDefault(Section, $"Game{i}", null);
			if (Parse(entry) is RecentGame game) {
				games.Add(game);
			}
		}
		return games;
	}

	// Remembers a game joined, at the top of the list.
	public static void Remember(RecentGame game) {
		List<RecentGame> games = With(Load(), game);
		for (int i = 0; i < MaxGames; ++i) {
			if (i < games.Count) {
				C7Settings.SetValue(Section, $"Game{i}", Format(games[i]));
			} else {
				C7Settings.RemoveValue(Section, $"Game{i}");
			}
		}
		C7Settings.SaveSettings();
	}

	// The list with the game at its top, and no other with its code on the
	// same relay, cut to length.
	public static List<RecentGame> With(IEnumerable<RecentGame> games, RecentGame game) {
		return games
			.Where(g => !(g.code == game.code && OnlineRelay.SameRelay(g.relayUrl, game.relayUrl)))
			.Prepend(game)
			.Take(MaxGames)
			.ToList();
	}

	// Kept as "code relay name": the code and the relay have no spaces.
	public static string Format(RecentGame game) => $"{game.code} {game.relayUrl} {game.name}";

	public static RecentGame Parse(string entry) {
		string[] parts = entry?.Split(' ', 3) ?? [];
		if (parts.Length < 2 || RelayProtocol.NormalizeCode(parts[0]) is not string code || RelayConnection.BaseUri(parts[1]) == null) {
			return null;
		}
		return new RecentGame(parts.Length == 3 ? parts[2] : code, code, parts[1]);
	}
}

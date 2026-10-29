using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using C7GameData.Save;

namespace C7Engine.Network;

// The tabs of the LAN game list (owner's request, 2026-10-28: laid out like
// Steam's server browser): every game found, those on the local network,
// those on the player's Tailscale network, and the hosts the player starred
// or joined before, answering or not.
public enum LanGameTab { All, Lan, Tailscale, Favorites, History }

// The list's columns, which it sorts by.
public enum LanGameColumn { Favorite, Password, Game, Players, GameType, Map, Turn, Latency, Address, Version }

// What the list hides: games whose name, host, address or type doesn't
// have the text in it, and full, started, password-protected games and
// those this game can't join, as asked.
public sealed record LanGameFilter(string text = "", bool hideFull = false, bool hideStarted = false, bool hidePassword = false,
	bool hideOtherVersions = false);

// A row of the list: a host that answered (found), or a starred or recent
// host that didn't (found null), at its address and port.
public sealed record LanGameRow(string address, int port, FoundHost found, bool favorite, bool recent) {
	public DiscoveryReply reply => found?.reply;
	public bool answered => found != null;

	// How the favorites and the join-by-address box write it.
	public string entry => LanGameList.Entry(address, port);
}

// The LAN game list's rows: which hosts each tab shows, filtered and
// sorted, and the text of their cells.
public static class LanGameList {
	public static string Entry(string address, int port) => port == LanProtocol.DefaultPort ? address : $"{address}:{port}";

	// An entry such as "192.168.1.20" or "mybox:47778" split into its host
	// and port, which is the default when not given.
	public static (string host, int port) ParseEntry(string entry) {
		string text = entry?.Trim() ?? "";
		int colon = text.LastIndexOf(':');
		if (colon > 0 && int.TryParse(text[(colon + 1)..], out int port) && port > 0 && port <= 65535) {
			return (text[..colon], port);
		}
		return (text, LanProtocol.DefaultPort);
	}

	// Whether a stored entry is that found host: the same address, or a
	// name that resolved to it, and the same port if the entry gives one.
	// Recent hosts are kept without their ports, so any port matches them.
	public static bool Matches(string entry, FoundHost host, IReadOnlyDictionary<string, List<string>> resolved = null,
		bool anyPort = false) {
		(string name, int port) = ParseEntry(entry);
		if (!anyPort && port != host.reply.port) {
			return false;
		}
		if (name.Equals(host.address, StringComparison.OrdinalIgnoreCase)) {
			return true;
		}
		return resolved != null && resolved.TryGetValue(name, out List<string> addresses) && addresses.Contains(host.address);
	}

	// The rows a tab shows. The Favorites and History tabs also show the
	// hosts that didn't answer, which can still be joined by address.
	public static List<LanGameRow> Rows(LanGameTab tab, IEnumerable<FoundHost> found, IReadOnlyList<string> favorites,
		IReadOnlyList<string> recent, IReadOnlyDictionary<string, List<string>> resolved = null) {
		List<FoundHost> hosts = found.ToList();
		List<LanGameRow> rows = hosts
			.Select(h => new LanGameRow(h.address, h.reply.port, h, favorites.Any(e => Matches(e, h, resolved)),
				recent.Any(e => Matches(e, h, resolved, anyPort: true))))
			.Where(r => tab switch {
				LanGameTab.Lan => r.found.network == null,
				LanGameTab.Tailscale => r.found.network == "Tailscale",
				LanGameTab.Favorites => r.favorite,
				LanGameTab.History => r.recent,
				_ => true,
			})
			.ToList();
		IReadOnlyList<string> stored = tab switch {
			LanGameTab.Favorites => favorites,
			LanGameTab.History => recent,
			_ => [],
		};
		foreach (string entry in stored) {
			if (hosts.Any(h => Matches(entry, h, resolved, anyPort: tab == LanGameTab.History))) {
				continue;
			}
			(string host, int port) = ParseEntry(entry);
			rows.Add(new LanGameRow(host, port, null, favorites.Any(f => SameEntry(f, entry)),
				recent.Any(r => ParseEntry(r).host.Equals(host, StringComparison.OrdinalIgnoreCase))));
		}
		return rows;
	}

	public static bool SameEntry(string a, string b) {
		(string hostA, int portA) = ParseEntry(a);
		(string hostB, int portB) = ParseEntry(b);
		return portA == portB && hostA.Equals(hostB, StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsCompatible(DiscoveryReply reply) => reply.version == LanProtocol.Version;

	public static List<LanGameRow> Filter(IEnumerable<LanGameRow> rows, LanGameFilter filter) {
		string text = filter.text?.Trim() ?? "";
		return rows.Where(r => {
			if (text != "" && !SearchText(r).Contains(text, StringComparison.CurrentCultureIgnoreCase)) {
				return false;
			}
			// A host that didn't answer is kept: nothing is known against it.
			if (r.reply is not DiscoveryReply reply) {
				return true;
			}
			return !(filter.hideFull && reply.openSeats == 0)
				&& !(filter.hideStarted && reply.started)
				&& !(filter.hidePassword && reply.hasPassword)
				&& !(filter.hideOtherVersions && !IsCompatible(reply));
		}).ToList();
	}

	private static string SearchText(LanGameRow row) {
		return string.Join("\n", GameText(row), row.reply?.hostName, row.address, row.reply?.gameType);
	}

	// The rows in the column's order. The hosts that answered come first
	// either way, as Steam lists the servers that don't answer last; ties
	// are by the game's name.
	public static List<LanGameRow> Sort(IEnumerable<LanGameRow> rows, LanGameColumn column, bool ascending) {
		Func<LanGameRow, IComparable> key = column switch {
			LanGameColumn.Favorite => r => r.favorite,
			LanGameColumn.Password => r => r.reply?.hasPassword ?? false,
			LanGameColumn.Players => r => r.reply == null ? -1 : r.reply.seatsTotal > 0 ? r.reply.seatsTaken : 0,
			LanGameColumn.GameType => r => r.reply?.gameType ?? "",
			LanGameColumn.Map => r => MapArea(r.reply?.mapSize),
			// Games in the lobby come before those under way.
			LanGameColumn.Turn => r => r.reply == null ? -1 : r.reply.started ? r.reply.turn + 1 : 0,
			LanGameColumn.Latency => r => r.found?.latencyMs ?? int.MaxValue,
			LanGameColumn.Address => r => AddressKey(r.address),
			LanGameColumn.Version => r => r.reply?.version ?? -1,
			_ => r => GameText(r),
		};
		Comparer<IComparable> comparer = Comparer<IComparable>.Create(Compare);
		IOrderedEnumerable<LanGameRow> answeredFirst = rows.OrderByDescending(r => r.answered);
		IOrderedEnumerable<LanGameRow> sorted = ascending
			? answeredFirst.ThenBy(key, comparer)
			: answeredFirst.ThenByDescending(key, comparer);
		return sorted.ThenBy(GameText, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	private static int Compare(IComparable a, IComparable b) {
		return a is string x && b is string y ? StringComparer.CurrentCultureIgnoreCase.Compare(x, y) : a.CompareTo(b);
	}

	// An address sorts by its numbers, so that 192.168.1.9 comes before
	// 192.168.1.10; names sort after addresses.
	private static string AddressKey(string address) {
		if (IPAddress.TryParse(address, out IPAddress ip) && ip.AddressFamily == AddressFamily.InterNetwork) {
			return string.Concat(ip.GetAddressBytes().Select(b => b.ToString("D3")));
		}
		return "~" + address;
	}

	// A map size like "100x100" as its number of tiles; 0 if it isn't one.
	public static int MapArea(string size) {
		string[] parts = size?.Split('x') ?? [];
		return parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) ? w * h : 0;
	}

	// ---- The cells' text ----

	// The game's name, or the host's game when it has none; a host that
	// didn't answer by its address.
	public static string GameText(LanGameRow row) {
		if (row.reply is not DiscoveryReply reply) {
			return row.entry;
		}
		return reply.gameName ?? $"{reply.hostName}'s game";
	}

	// Seats taken of all of them, as Steam shows players, or the open seats
	// alone from a host too old to say more.
	public static string PlayersText(DiscoveryReply reply) {
		if (reply == null) {
			return "";
		}
		return reply.seatsTotal > 0 ? $"{reply.seatsTaken} / {reply.seatsTotal}" : $"{reply.openSeats} open";
	}

	public static string TurnText(DiscoveryReply reply) {
		if (reply == null) {
			return "";
		}
		if (!reply.started) {
			return "Lobby";
		}
		return reply.turn > 0 ? reply.turn.ToString() : "Started";
	}

	public static string LatencyText(FoundHost found) => found?.latencyMs is int ms ? ms.ToString() : "";

	public static string AddressText(LanGameRow row) {
		string network = row.found?.network ?? (IPAddress.TryParse(row.address, out IPAddress ip) && LanDiscovery.IsTailscaleAddress(ip)
			? "Tailscale" : null);
		return row.entry + (network != null ? $" ({network})" : "");
	}

	public static string VersionText(DiscoveryReply reply) {
		if (reply == null) {
			return "";
		}
		return reply.version == 0 ? "old" : reply.version.ToString();
	}

	// ---- Game types ----

	public static string GameTypeName(GameScenario scenario) {
		return scenario switch {
			GameScenario.EternalWar => "Eternal War",
			GameScenario.RisingTide => "Rising Tide",
			GameScenario.KingsAndNomads => "Kings & Nomads",
			GameScenario.Mdj => "MDJ",
			_ => "Standard",
		};
	}

	// The type of a game already set up, told by the rules each scenario
	// sets (see GameSetup.*.cs), or by the Conquests scenario it began from.
	public static string GameTypeOf(SaveGame save) {
		if (save?.Rules is not C7GameData.Rules rules) {
			return null;
		}
		// Before MDJ: Rising Tide is played under MDJ's rules.
		if (rules.RisingTide) {
			return GameTypeName(GameScenario.RisingTide);
		}
		if (rules.SettlersOnlyInConqueredCities) {
			return GameTypeName(GameScenario.KingsAndNomads);
		}
		if (rules.PermanentWar) {
			return GameTypeName(GameScenario.EternalWar);
		}
		if (rules.GovernmentLocked || rules.OwnTechTree) {
			return GameTypeName(GameScenario.Mdj);
		}
		if (!string.IsNullOrWhiteSpace(save.ScenarioName)) {
			// "1 Mesopotamia" is Conquests' file name for Mesopotamia.
			string name = save.ScenarioName.Trim();
			int space = name.IndexOf(' ');
			return space > 0 && name[..space].All(char.IsDigit) ? name[(space + 1)..] : name;
		}
		return GameTypeName(GameScenario.None);
	}

	// The addresses the names among the entries stand for, such as a
	// tailnet machine's, to match them to the hosts found. Names that don't
	// resolve in time are left out.
	public static async Task<Dictionary<string, List<string>>> ResolveAsync(IEnumerable<string> entries, TimeSpan timeout) {
		Dictionary<string, List<string>> resolved = new(StringComparer.OrdinalIgnoreCase);
		using CancellationTokenSource limit = new(timeout);
		foreach (string name in entries.Select(e => ParseEntry(e).host).Distinct(StringComparer.OrdinalIgnoreCase)) {
			if (name == "" || IPAddress.TryParse(name, out _)) {
				continue;
			}
			try {
				IPAddress[] addresses = await Dns.GetHostAddressesAsync(name, AddressFamily.InterNetwork, limit.Token).ConfigureAwait(false);
				resolved[name] = addresses.Select(a => a.ToString()).ToList();
			} catch (Exception e) when (e is SocketException or OperationCanceledException or ArgumentException) {
				// Not known now.
			}
		}
		return resolved;
	}
}

// The LAN hosts the player starred in the game list, kept in the settings
// like RecentLanHosts, as "address" or "address:port".
public static class FavoriteLanHosts {
	public const string SettingsSection = "FavoriteLanHosts";
	private const string SettingsKey = "Addresses";
	public const int MaxAddresses = 32;

	public static List<string> Load() {
		return Parse(C7Settings.GetSettingsValueOrDefault(SettingsSection, SettingsKey, ""));
	}

	// Stars the entry, or unstars it if it was starred. Returns whether it
	// is starred now.
	public static bool Toggle(string entry) {
		List<string> after = Toggle(Load(), entry);
		C7Settings.SetValue(SettingsSection, SettingsKey, Format(after));
		C7Settings.SaveSettings();
		return after.Any(e => LanGameList.SameEntry(e, entry));
	}

	public static void Add(string entry) {
		List<string> before = Load();
		if (before.Any(e => LanGameList.SameEntry(e, entry))) {
			return;
		}
		Toggle(entry);
	}

	public static List<string> Parse(string value) {
		return (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(Tidy)
			.Where(e => e != null)
			.DistinctBy(e => e.ToLowerInvariant())
			.Take(MaxAddresses)
			.ToList();
	}

	public static string Format(IEnumerable<string> entries) => string.Join(",", entries);

	// The list with the entry added at its end, or taken out if it was in.
	public static List<string> Toggle(IEnumerable<string> entries, string entry) {
		List<string> list = entries.ToList();
		string tidy = Tidy(entry);
		if (tidy == null) {
			return list;
		}
		if (list.RemoveAll(e => LanGameList.SameEntry(e, tidy)) > 0) {
			return list;
		}
		return list.Append(tidy).Take(MaxAddresses).ToList();
	}

	// The entry as kept: without the default port, or null when it can't be
	// kept (empty, or with the list's comma in it).
	public static string Tidy(string entry) {
		(string host, int port) = LanGameList.ParseEntry(entry);
		if (host == "" || host.Contains(',') || host.Contains(' ')) {
			return null;
		}
		return LanGameList.Entry(host, port);
	}
}

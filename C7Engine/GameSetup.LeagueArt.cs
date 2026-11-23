using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine;

// The League modes' own Civilopedia keys and art in standalone mode.
//
// The League's mods (GameSetup.BundledRules.cs, GameSetup.Mdj.cs) give many
// of their items a stock item's Civilopedia entry: Modern's "Wartime
// (Draft)" is TECH_Chivalry, QC's Palace BLDG_Pyramids. Icons, Civilopedia
// pictures and articles are all looked up by entry, so such an item showed
// the stock item's, and items sharing an entry had one Civilopedia page
// between them (the art audit of 2026-11-22). In standalone mode, which
// draws with our own art and text only, each such item gets a key of its
// own instead (TECH_MODERN_Wartime), or a pointer back to the stock key of
// the thing it is (Modern's Currency is TECH_Currency again), and units the
// art they are to be drawn with. Civ3 mode keeps the mods' own entries,
// which Civ3's files and the mods' have.
//
// The table is C7/Lua/standalone/league_art.json, shared with ArtGen
// (civilopedia_icons/league.py indexes the keys in PediaIcons.txt and
// icon_files.lua) and EngineTests (LeagueArtTest): per mode (the rules'
// folder) and section, by the item's exact name, its key, its picture
// ("art": a file stem; for a unit the art folder) and that picture's
// status: "new" until it is drawn, then "done", or "stock" for an existing
// picture. A unit's art and a resource's map cell only change once their
// picture is there (not "new"): until then the item is drawn as before.
// Leader heads by leader are in standalone/textures.lua.
public partial class GameSetup {
	private static readonly ILogger leagueLog = Log.ForContext<GameSetup>();

	public const string LeagueArtFile = "league_art.json";

	public sealed class LeagueArtEntry {
		public string from { get; set; }
		public string key { get; set; }
		public string leader { get; set; }
		public string art { get; set; }
		public string status { get; set; }
		public int? cell { get; set; }
		public string priority { get; set; }
		public string note { get; set; }

		public bool Drawn => status != "new";
	}

	public sealed class LeagueArtTable {
		public Dictionary<string, Dictionary<string, Dictionary<string, LeagueArtEntry>>> modes { get; set; } = new();

		// A mode's entries of a section (techs, buildings, governments,
		// resources, civs, units, leaders, terrain), by item name.
		public IReadOnlyDictionary<string, LeagueArtEntry> Section(string mode, string section) {
			if (mode != null && modes.TryGetValue(mode, out var sections) && sections.TryGetValue(section, out var items)) {
				return items;
			}
			return new Dictionary<string, LeagueArtEntry>();
		}
	}

	// The table in the standalone addon's folder of gameModesDir, or an
	// empty one without it.
	public static LeagueArtTable LoadLeagueArt(string gameModesDir) {
		string path = Path.Join(gameModesDir, "standalone", LeagueArtFile);
		if (!File.Exists(path)) {
			leagueLog.Warning("No {file} at {path}: the League modes keep their borrowed entries", LeagueArtFile, path);
			return new LeagueArtTable();
		}
		return JsonSerializer.Deserialize<LeagueArtTable>(File.ReadAllText(path)) ?? new LeagueArtTable();
	}

	// Whether a League mode gives the civilization (by name) this leader,
	// with a portrait of its own: stem is the portrait's file stem
	// (Art/Leaderheads/<stem>.png), or null while it is still to be made.
	// standalone/textures.lua has the same table for the leader heads the
	// texture config maps; this is for the screens that show portraits
	// themselves (the end of the game's).
	public static bool LeagueLeaderPortrait(LeagueArtTable table, string civ, string leader, out string stem) {
		stem = null;
		if (table == null || civ == null || leader == null) {
			return false;
		}
		foreach (string mode in table.modes.Keys) {
			if (table.Section(mode, "leaders").TryGetValue(civ, out LeagueArtEntry entry) && entry.leader == leader) {
				stem = entry.Drawn ? entry.art : null;
				return true;
			}
		}
		return false;
	}

	// Gives the items of the mode's rules (by its rules folder, like
	// "modern") their own keys and art, in standalone mode.
	internal static void UseLeagueArt(SaveGame save, LeagueArtTable table, string mode) {
		static void Each<T>(IEnumerable<T> items, IReadOnlyDictionary<string, LeagueArtEntry> entries, System.Func<T, string> name, System.Action<T, LeagueArtEntry> use) {
			foreach (T item in items) {
				if (item != null && name(item) is string n && entries.TryGetValue(n, out LeagueArtEntry entry)) {
					use(item, entry);
				}
			}
		}

		Each(save.Techs, table.Section(mode, "techs"), t => t.Name, (t, e) => t.CivilopediaEntry = e.key ?? t.CivilopediaEntry);
		Each(save.Buildings, table.Section(mode, "buildings"), b => b.name, (b, e) => b.civilopediaEntry = e.key ?? b.civilopediaEntry);
		Each(save.Governments, table.Section(mode, "governments"), g => g.name, (g, e) => g.civilopediaEntry = e.key ?? g.civilopediaEntry);
		Each(save.Civilizations, table.Section(mode, "civs"), c => c.name, (c, e) => c.civilopediaEntry = e.key ?? c.civilopediaEntry);
		Each(save.Resources, table.Section(mode, "resources"), r => r.Name, (r, e) => {
			r.CivilopediaEntry = e.key ?? r.CivilopediaEntry;
			if (e.cell is int cell && e.Drawn) {
				r.Icon = cell;
			}
		});

		// Units: a unit drawn with another art name takes the Civilopedia
		// icon made for that art: the one a unit of the same rules drawn
		// with it has, or the icon named after the art (as Modern's units',
		// GameSetup.Modern.cs).
		List<SaveUnitPrototype> units = save.UnitPrototypes;
		Each(units, table.Section(mode, "units"), u => u.name, (u, e) => {
			u.civilopediaEntry = e.key ?? u.civilopediaEntry;
			if (e.art == null || !e.Drawn || u.art.mainArt.defaultName == e.art) {
				return;
			}
			SaveUnitPrototype other = units.FirstOrDefault(o => o != u && o.art.mainArt.defaultName == e.art && o.art.pediaArt.large != null);
			string stem = System.Text.RegularExpressions.Regex.Replace(e.art, "[^A-Za-z0-9]+", "_").Trim('_');
			Art art = u.art;
			art.mainArt = new MainArt { defaultName = e.art };
			art.pediaArt = other != null
				? new PediaArt { large = other.art.pediaArt.large, small = other.art.pediaArt.small }
				: new PediaArt { large = $"Art/Civilopedia/Icons/Units/{stem}_large.png", small = $"Art/Civilopedia/Icons/Units/{stem}_small.png" };
			u.art = art;
		});
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The League modes in standalone mode show each item with its own picture
// and its own Civilopedia article, not another item's (the art audit of
// 2026-11-22; C7Engine/GameSetup.LeagueArt.cs and
// C7/Lua/standalone/league_art.json).
//
// What isn't there yet (art still being drawn, articles still being
// written) is listed in LeagueArtPending.txt, one "mode|section|item|what"
// a line: those items are expected to fail, and the test fails when one
// on the list passes (take it off) as much as when one not on it fails.
public class LeagueArtTest {
	private static string C7Folder => PathUtils.getBasePath("../C7/");
	private static string CivilopediaFolder => Path.Combine(C7Folder, "Civilopedia");
	private static string ModernArtFolder => Path.Combine(C7Folder, "ModernArt");
	private static string PendingPath => PathUtils.getBasePath("../EngineTests/GameData/LeagueArtPending.txt");

	private static readonly (string Mode, GameScenario Scenario)[] Modes = [
		("mptournament", GameScenario.MpTournament), ("qc", GameScenario.Qc), ("modern", GameScenario.Modern),
		("future", GameScenario.Future), ("mdj", GameScenario.Mdj),
	];

	private static readonly GameMode.Config Standalone = new("civ3", ["standalone"]);

	private static readonly Lazy<GameSetup.LeagueArtTable> table = new(() => GameSetup.LoadLeagueArt(PathUtils.GameModesDir));
	private static readonly Lazy<PediaIcons> icons = new(() => new PediaIcons(Path.Combine(CivilopediaFolder, "Text", "PediaIcons.txt")));
	private static readonly Lazy<CivilopediaText> text = new(() => CivilopediaText.Load(Path.Combine(CivilopediaFolder, "Text", "Civilopedia.txt")));
	private static readonly Dictionary<string, SaveGame> rules = new();

	private static SaveGame Rules(string mode) {
		lock (rules) {
			if (!rules.TryGetValue(mode, out SaveGame save)) {
				save = GameSetup.LoadRulesFor(Modes.First(m => m.Mode == mode).Scenario, PathUtils.GameModesDir, Standalone);
				rules[mode] = save;
			}
			return save;
		}
	}

	// ArtGen's file stems (civilopedia_icons/common.file_stem).
	private static string FileStem(string name) => Regex.Replace(name, "[^A-Za-z0-9]+", "_").Trim('_');
	private static string KeyStem(string key) => FileStem(key[(key.IndexOf('_') + 1)..]);
	// A name without the mod's note in brackets: "Wartime (Draft)".
	private static string Bare(string name) => Regex.Replace(name ?? "", @"\s*\(.*\)\s*$", "").Trim();
	// Names compared: letters and digits, any case, without "The".
	private static string Norm(string name) => Regex.Replace(Regex.Replace(Bare(name).ToLowerInvariant(), "^the ", ""), "[^a-z0-9]", "");
	private static string IconStem(string path) => path == null ? null : Regex.Replace(Path.GetFileNameWithoutExtension(path), "_(large|small)$", "");
	private static bool PediaFileExists(string path) => path != null && File.Exists(Path.Combine(CivilopediaFolder, path));

	private sealed record Item(string Section, string Name, string Key, object Rule);

	private static List<Item> Items(SaveGame save) {
		List<Item> items = new();
		items.AddRange(save.Techs.Where(t => t.EraCivilopediaName != EraUtils.HIDDEN_CVLPD)
			.Select(t => new Item("techs", t.Name, t.CivilopediaEntry ?? CivilopediaCatalog.KeyFromName("TECH_", t.Name), t)));
		items.AddRange(save.Buildings.Select(b => new Item("buildings", b.name, b.civilopediaEntry ?? CivilopediaCatalog.KeyFromName("BLDG_", b.name), b)));
		items.AddRange(save.Governments.Select(g => new Item("governments", g.name, g.civilopediaEntry ?? CivilopediaCatalog.KeyFromName("GOVT_", g.name), g)));
		items.AddRange(save.Resources.Where(r => r != null && r.Name != null)
			.Select(r => new Item("resources", r.Name, r.CivilopediaEntry ?? CivilopediaCatalog.KeyFromName("GOOD_", r.Name), r)));
		items.AddRange(save.Civilizations.Where(c => c != null && !c.IsIndependent && c.civilopediaEntry != null)
			.Select(c => new Item("civs", c.name, c.civilopediaEntry, c)));
		items.AddRange(save.UnitPrototypes
			.Select(u => new Item("units", u.name, u.civilopediaEntry ?? CivilopediaCatalog.KeyFromName("PRTO_", u.name), u)));
		// QC's "(Unused)" items are left over in its BIQ: nothing builds them.
		return items.Where(i => !i.Name.StartsWith("(Unused)")).ToList();
	}

	// The stock civilizations' leaders, whose portraits are by civ name.
	private static readonly Lazy<Dictionary<string, string>> stockLeaders = new(() =>
		GameMode.Load(PathUtils.GameModesDir, Standalone).GetSave().Civilizations
			.Where(c => c.name != null).GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First().leader));

	// The names of the items of the stock rules (Civ3's, and MDJ's own
	// entries) by section and key: the things their articles are about.
	private static readonly Lazy<Dictionary<(string, string), HashSet<string>>> stockNames = new(() => {
		Dictionary<(string, string), HashSet<string>> names = new();
		SaveGame civ3 = GameMode.Load(PathUtils.GameModesDir, Standalone).GetSave();
		SaveGame mdj = GameMode.Load(PathUtils.GameModesDir, GameSetup.MdjGameMode(Standalone)).GetSave();
		List<Item> stock = Items(civ3);
		HashSet<(string, string)> stockKeys = stock.Select(i => (i.Section, i.Key)).ToHashSet();
		foreach (Item item in stock.Concat(Items(mdj).Where(i => !stockKeys.Contains((i.Section, i.Key))))) {
			if (!names.TryGetValue((item.Section, item.Key), out HashSet<string> set)) {
				names[(item.Section, item.Key)] = set = new();
			}
			set.Add(Norm(item.Name));
		}
		return names;
	});

	// What doesn't show as itself, as "mode|section|item|what".
	private static List<string> Problems() {
		List<string> problems = new();
		foreach ((string mode, _) in Modes) {
			SaveGame save = Rules(mode);
			List<Item> items = Items(save);
			void Problem(string section, string name, string what) => problems.Add($"{mode}|{section}|{name}|{what}");

			Dictionary<(string, string), Item> firstByKey = new();
			foreach (Item item in items) {
				GameSetup.LeagueArtEntry declared = table.Value.Section(mode, item.Section).GetValueOrDefault(item.Name);

				// A page of its own: the Civilopedia has one page per key.
				if (firstByKey.TryGetValue((item.Section, item.Key), out Item first)) {
					if (Norm(first.Name) != Norm(item.Name)) {
						Problem(item.Section, item.Name, $"page (shares {item.Key} with {first.Name})");
					}
				} else {
					firstByKey[(item.Section, item.Key)] = item;
				}

				// Its own article: a stock article of another name only if
				// the table says it's the same thing.
				CivilopediaArticle article = text.Value.Get(item.Key);
				if (article == null) {
					Problem(item.Section, item.Name, $"article (none for {item.Key})");
				} else if (mode == "mdj" && IsLeagueKey(item.Key) && !article.HasDescription) {
					// MDJ's articles all have a history (MdjScenarioTest).
					Problem(item.Section, item.Name, $"history (none for {item.Key})");
				} else if (declared == null && stockNames.Value.TryGetValue((item.Section, item.Key), out HashSet<string> names)
					&& !names.Contains(Norm(item.Name))) {
					Problem(item.Section, item.Name, $"article (is {names.First()}'s)");
				}

				// Known to be wrong still, and left for later.
				if (declared?.pending != null) {
					Problem(item.Section, item.Name, $"{declared.pending} (later: {declared.note})");
				}

				// Its own picture.
				if (item.Rule is SaveUnitPrototype unit) {
					string pedia = unit.art.pediaArt.large;
					HashSet<string> expected = new() { KeyStem(item.Key) };
					if (declared?.art != null) {
						expected = new() { FileStem(declared.art) };
						// Drawn as before: the icon of the entry it had.
						if (declared.status == "stock") {
							expected.Add(KeyStem(declared.from));
						}
						foreach (SaveUnitPrototype other in save.UnitPrototypes.Where(o => o != unit && o.art.mainArt.defaultName == declared.art)) {
							expected.Add(IconStem(other.art.pediaArt.large));
						}
						if (unit.art.mainArt.defaultName != declared.art) {
							Problem("units", item.Name, $"map art (is {unit.art.mainArt.defaultName}, not {declared.art})");
						}
						if (declared.status == "new" && Directory.Exists(Path.Combine(ModernArtFolder, "Art", "Units", declared.art))) {
							Problem("units", item.Name, "status (its art is there: mark it done)");
						}
					} else if (save.ScenarioName == GameSetup.ModernRules.ScenarioName && GameSetup.ModernUnitArt.ContainsKey(Bare(item.Name))) {
						expected = new() { FileStem(GameSetup.ModernUnitArt[Bare(item.Name)]) };
					}
					if (!expected.Contains(IconStem(pedia)) || !PediaFileExists(pedia)) {
						Problem("units", item.Name, $"picture (is {pedia})");
					}
					continue;
				}
				string icon = icons.Value.GetCivilopediaIcon(item.Key, true);
				string want = declared?.art ?? KeyStem(item.Key);
				if (icon == null || !PediaFileExists(icon)) {
					Problem(item.Section, item.Name, $"picture (none for {item.Key})");
				} else if (!string.Equals(IconStem(icon), want, StringComparison.OrdinalIgnoreCase)) {
					Problem(item.Section, item.Name, $"picture (is {IconStem(icon)}, not {want})");
				}
				string folder = item.Section switch {
					"techs" => "Advances",
					"buildings" => "Buildings",
					"governments" => "Governments",
					"resources" => "Resources",
					_ => "Races",
				};
				if (declared?.status == "new" && declared.art != null && IconStem(icon) != declared.art
					&& PediaFileExists($"Art/Civilopedia/Icons/{folder}/{declared.art}_large.png")) {
					Problem(item.Section, item.Name, "status (its picture is there: mark it done and rewrite the indexes)");
				}
				if (item.Rule is Resource resource && declared?.cell is int cell && resource.Icon != cell) {
					Problem("resources", item.Name, $"map cell (is {resource.Icon}, not {cell})");
				}
			}

			// Terrain: an icon and an article by the key the Civilopedia gives it.
			foreach (TerrainType terrain in save.TerrainTypes.Where(t => t?.DisplayName != null)) {
				string key = CivilopediaCatalog.TerrainKey(terrain.DisplayName, text.Value);
				if (!PediaFileExists(icons.Value.GetCivilopediaIcon(key, true))) {
					Problem("terrain", terrain.DisplayName, $"picture (none for {key})");
				}
				if (!text.Value.Has(key)) {
					Problem("terrain", terrain.DisplayName, $"article (none for {key})");
				}
			}

			// Leader heads: a civ of a stock name led by another leader has a
			// portrait of its own, by leader.
			foreach (Civilization civ in save.Civilizations.Where(c => c != null && !c.IsIndependent && c.name != null)) {
				bool league = GameSetup.LeagueLeaderPortrait(table.Value, civ.name, civ.leader, out string stem);
				if (league) {
					GameSetup.LeagueArtEntry entry = table.Value.Section(mode, "leaders")[civ.name];
					string file = Path.Combine(ModernArtFolder, "Art", "Leaderheads", entry.art + ".png");
					if (stem == null) {
						Problem("leaders", civ.name, File.Exists(file) ? "status (the portrait is there: mark it done)" : $"portrait ({entry.art} to make)");
					} else if (!File.Exists(file)) {
						Problem("leaders", civ.name, $"portrait ({stem}.png is missing)");
					}
				} else if (stockLeaders.Value.TryGetValue(civ.name, out string stockLeader) && stockLeader != civ.leader) {
					Problem("leaders", civ.name, $"portrait (shows {stockLeader}, not {civ.leader})");
				}
			}
		}
		return problems;
	}

	// A League mode's own key whose article is still to be written: the
	// tests that every entry has an article leave it to this test, which
	// lists it as pending.
	public static bool AwaitsArticle(string key, Func<string, bool> hasArticle) {
		return key != null && !hasArticle(key) && LeagueKeys.Value.Contains(key);
	}

	public static bool IsLeagueKey(string key) => key != null && LeagueKeys.Value.Contains(key);

	private static readonly Lazy<HashSet<string>> LeagueKeys = new(() => table.Value.modes.Values
		.SelectMany(s => s.Values).SelectMany(e => e.Values).Where(e => e.key != null && e.key != e.from)
		.Select(e => e.key).ToHashSet());

	private static HashSet<string> Pending() => File.ReadAllLines(PendingPath)
		.Select(l => l.TrimEnd()).Where(l => l.Length > 0 && !l.StartsWith('#')).ToHashSet();

	// Strips the detail in brackets after the "what", which may change as
	// files come and go: "picture (is Chivalry, not MODERN_Wartime)".
	// Item names may have brackets of their own: only the last field's go.
	private static string Kind(string problem) {
		int what = problem.LastIndexOf('|') + 1;
		return problem[..what] + Regex.Replace(problem[what..], @" \(.*$", "");
	}

	[Fact]
	public void EveryItemShowsItsOwnPictureAndArticleOrIsPending() {
		List<string> problems = Problems();
		HashSet<string> found = problems.Select(Kind).ToHashSet();
		HashSet<string> pending = Pending().Select(Kind).ToHashSet();
		List<string> unexpected = problems.Where(p => !pending.Contains(Kind(p))).ToList();
		List<string> done = pending.Where(p => !found.Contains(p)).ToList();
		Assert.True(unexpected.Count == 0 && done.Count == 0,
			$"Not as themselves ({unexpected.Count}, not pending):\n  {string.Join("\n  ", unexpected)}\n"
			+ $"Pending but fine now ({done.Count}: take them off {Path.GetFileName(PendingPath)}):\n  {string.Join("\n  ", done)}");
	}

	// Every item the table names is one of the mode's (a renamed item
	// would silently keep its borrowed entry).
	[Fact]
	public void TheTableNamesItemsOfTheModes() {
		List<string> missing = new();
		foreach ((string mode, var sections) in table.Value.modes) {
			SaveGame save = Rules(mode);
			HashSet<(string, string)> names = Items(save).Select(i => (i.Section, i.Name)).ToHashSet();
			foreach ((string section, var entries) in sections) {
				foreach (string name in entries.Keys) {
					bool there = section switch {
						"leaders" => save.Civilizations.Any(c => c.name == name && c.leader == entries[name].leader),
						"terrain" => save.TerrainTypes.Any(t => t?.DisplayName == name),
						_ => names.Contains((section, name)),
					};
					if (!there) {
						missing.Add($"{mode}|{section}|{name}");
					}
				}
			}
		}
		Assert.Empty(missing);
	}

	// The league's keys are their own: no two items of a mode that are
	// different things share one, and none is another mode's.
	[Fact]
	public void TheNewKeysAreOneModesEach() {
		Dictionary<string, string> modeOf = new();
		List<string> clashes = new();
		foreach ((string mode, var sections) in table.Value.modes) {
			foreach ((string section, var entries) in sections) {
				foreach ((string name, GameSetup.LeagueArtEntry e) in entries) {
					if (e.key == null || e.key == e.from || !Regex.IsMatch(e.key, "^[A-Z]+_(QC|MODERN|FUTURE|MDJ)_")) {
						continue;
					}
					if (modeOf.TryGetValue(e.key, out string other) && other != mode) {
						clashes.Add($"{e.key}: {other} and {mode}");
					}
					modeOf[e.key] = mode;
					Assert.Contains($"_{mode.ToUpperInvariant()}_", e.key);
				}
			}
		}
		Assert.Empty(clashes);
	}

	// standalone/textures.lua's leader heads by leader are the table's.
	[Fact]
	public void TheTexturesLeaderTableIsTheJsons() {
		string lua = File.ReadAllText(Path.Combine(C7Folder, "Lua", "standalone", "textures.lua"));
		Match block = Regex.Match(lua, @"local league_leader_heads = \{(.*?)\n\}", RegexOptions.Singleline);
		Assert.True(block.Success);
		Dictionary<string, string> inLua = Regex.Matches(block.Groups[1].Value, @"\[""([^""]+)""\] = (false|""([^""]+)"")")
			.ToDictionary(m => m.Groups[1].Value, m => m.Groups[3].Success ? m.Groups[3].Value : null);
		Dictionary<string, string> inJson = table.Value.modes.Values
			.SelectMany(s => s.GetValueOrDefault("leaders") ?? new())
			.ToDictionary(e => $"{e.Key}|{e.Value.leader}", e => e.Value.Drawn ? e.Value.art : null);
		Assert.Equal(inJson.OrderBy(e => e.Key), inLua.OrderBy(e => e.Key));
	}

	// Civ3 mode reads the mods' own entries: the rules without the
	// standalone addon keep them.
	[Fact]
	public void CivThreeModeKeepsTheModsEntries() {
		SaveGame civ3 = GameSetup.LoadBundledRules(PathUtils.GameModesDir, new GameMode.Config("civ3"), GameSetup.ModernRules);
		Assert.Equal("TECH_Chivalry", civ3.Techs.Single(t => t.Name == "Wartime (Draft)").CivilopediaEntry);
		Assert.Equal("RACE_GERMANS", civ3.Civilizations.Single(c => c.name == "European Union").civilopediaEntry);
		SaveGame standalone = Rules("modern");
		Assert.Equal("TECH_MODERN_Wartime", standalone.Techs.Single(t => t.Name == "Wartime (Draft)").CivilopediaEntry);
		Assert.Equal("TECH_Currency", standalone.Techs.Single(t => t.Name == "Currency").CivilopediaEntry);
		Assert.Equal("Bradley", Rules("future").UnitPrototypes.Single(u => u.name == "Bradley").art.mainArt.defaultName);
	}
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7GameData;
using C7GameData.Save;
using QueryCiv3;

namespace C7Engine;

// Game types played under the rules of a multiplayer ladder mod of the Civ
// Players League (user's request, 2026-11-22), on a generated map, as MDJ's
// are (see GameSetup.Mdj.cs). Each mod is a Conquests BIQ; its rules are
// bundled, in C7/Lua/<RulesetDir>/ruleset.json, made from the BIQ by
// _Console/RegenerateRuleset, so the game never needs the BIQ, nor a Civ3
// install; the base mode's textures, behaviors and audio go with them, and
// its addons (standalone's art) apply as to Civ3's rules. A mod's own art
// isn't bundled: standalone mode draws a unit with no art of its own as a
// stock unit like it.
//
// MP Tournament: the League's MPTournament.biq, the multiplayer tournament
// ruleset (its units, buildings and advances are the stock
// MPTournament.biq's): accelerated production, no scientific leaders or
// culture flips, domination and elimination victories, and a 90-turn limit.
//
// QC: QC_1.74.biq, the League's quick game, after the stock Quick Civ: food,
// costs, culture and worker jobs scaled as there (citizens eat 4, units and
// buildings cost half, Settlers take 3 citizens), a shorter tech tree, its
// own units, world sizes by player count, and a 64-turn limit.
//
// Modern: Modern_2.4.biq, everyone starting in the Industrial Age under a
// placeholder government to leave for one of its four (Equality, Tradition,
// Freedom, Order), with their own buildings and wonders, its own units and
// advances, Regicide, and a 64-turn limit.
//
// Future: newfuture3.3.biq, everyone starting in the Modern Era knowing all
// but two advances, with the mod's own civilizations, traits and unique
// units, Regicide, and a 90-turn limit.
public partial class GameSetup {
	// A mod's bundled rules: the game type, the BIQ they are made from, the
	// folder under C7/Lua they are bundled in, the scenario name the rules
	// are known by (saved with the game), and the League's ladder settings
	// for a new game.
	public sealed record BundledRules(GameScenario Scenario, string BiqName, string RulesetDir, string ScenarioName, LadderSettings Ladder);

	// The League's settings for a ladder game of a mod ("Settings for ladder
	// mods"): the world's age, landform (null for any) and ocean, and the
	// world size, by its place in the mod's list (the League plays the
	// fifth, Civ3's Huge, with 8 players, the fourth with 7, the third with
	// 6).
	public sealed record LadderSettings(WorldCharacteristics.Age Age, WorldCharacteristics.Landform? Landform,
		WorldCharacteristics.OceanCoverage Ocean, int WorldSizeIndex);

	public static readonly BundledRules MpTournamentRules = new(GameScenario.MpTournament, "MPTournament.biq", "mptournament", "MPTournament",
		new(WorldCharacteristics.Age.Billion_5, WorldCharacteristics.Landform.Pangaea, WorldCharacteristics.OceanCoverage.Percent_60, 0));
	public static readonly BundledRules QcRules = new(GameScenario.Qc, "QC_1.74.biq", "qc", "QC_1.74",
		new(WorldCharacteristics.Age.Billion_5, WorldCharacteristics.Landform.Pangaea, WorldCharacteristics.OceanCoverage.Percent_60, 2));
	public static readonly BundledRules ModernRules = new(GameScenario.Modern, "Modern_2.4.biq", "modern", "Modern_2.4",
		new(WorldCharacteristics.Age.Billion_5, null, WorldCharacteristics.OceanCoverage.Percent_70, 2));
	public static readonly BundledRules FutureRules = new(GameScenario.Future, "newfuture3.3.biq", "future", "newfuture3.3",
		new(WorldCharacteristics.Age.Billion_5, null, WorldCharacteristics.OceanCoverage.Percent_70, 4));

	public static readonly BundledRules[] AllBundledRules = [MpTournamentRules, QcRules, ModernRules, FutureRules];

	// The bundled rules the game type is played under, or null for the
	// usual ones (or MDJ's, which GameSetup.Mdj.cs loads).
	public static BundledRules BundledRulesFor(GameScenario scenario) {
		return AllBundledRules.FirstOrDefault(r => r.Scenario == scenario);
	}

	// The bundled rules a game is played under, by the scenario name saved
	// with it, or null.
	public static BundledRules BundledRulesOf(SaveGame save) {
		return AllBundledRules.FirstOrDefault(r => r.ScenarioName == save?.ScenarioName);
	}

	// The folder of the rules the game type is played under, as
	// GameMode.Config.rulesetDir: MDJ's for MDJ and Rising Tide (see
	// GameSetup.Mdj.cs), a mod's, or null for the usual ones.
	public static string RulesetDirFor(GameScenario scenario) {
		return scenario is GameScenario.Mdj or GameScenario.RisingTide ? MdjRulesetDir : BundledRulesFor(scenario)?.RulesetDir;
	}

	// The rules the game type is played under over the given game mode, or
	// null for the game mode's own.
	public static SaveGame LoadRulesFor(GameScenario scenario, string gameModesDir, C7Engine.Lua.GameMode.Config over) {
		if (scenario is GameScenario.Mdj or GameScenario.RisingTide) {
			return LoadMdjRules(gameModesDir, over);
		}
		if (BundledRulesFor(scenario) is not BundledRules rules) {
			return null;
		}
		return LoadBundledRules(gameModesDir, over, rules);
	}

	// The game mode of the mod's rules over the given one.
	public static C7Engine.Lua.GameMode.Config BundledGameMode(C7Engine.Lua.GameMode.Config over, BundledRules rules) {
		return new(over.baseModeDir, [.. over.addonPaths], rules.RulesetDir);
	}

	// The mod's bundled rules, ready for Populate to make a map and players
	// for.
	public static SaveGame LoadBundledRules(string gameModesDir, C7Engine.Lua.GameMode.Config over, BundledRules rules) {
		SaveGame save = C7Engine.Lua.GameMode.Load(gameModesDir, BundledGameMode(over, rules)).GetSave();
		// Standalone mode draws with C7's own art only.
		if (over.addonPaths.Contains("standalone")) {
			save.ScenarioSearchPath = null;
			// Modern's new units with art of their own (GameSetup.Modern.cs).
			if (rules == ModernRules) {
				UseModernUnitArt(save);
			}
		}
		save.ScenarioName = rules.ScenarioName;
		return save;
	}

	// The mod's rules imported from its BIQ, wherever it is, as
	// RegenerateRuleset bundles them, or null if there is no such file.
	// The Civ3 install gives the default rules the BIQ leaves out, and the
	// Civilopedia icons.
	public static SaveGame ImportBundledBiq(string biqPath, string civ3Path, BundledRules rules) {
		if (biqPath == null || !File.Exists(biqPath)) {
			return null;
		}
		string scenarios = Path.GetDirectoryName(Path.GetFullPath(biqPath));
		string defaultBiq = Path.Join(civ3Path, "Conquests", "conquests.biq");
		SaveGame save = ImportCiv3.ImportBiq(biqPath, defaultBiq, searchFolders => PediaIconsFor(civ3Path, scenarios, searchFolders));
		save.ScenarioName = rules.ScenarioName;
		BiqData biq = new(QueryCiv3.Util.ReadFile(biqPath));
		save.Rules.ScenarioStart = ScenarioStartOf(biq, save);
		// Modern's buildings named as the mod names them (GameSetup.Modern.cs).
		if (rules == ModernRules) {
			RenameModernBuildings(save, biq);
		}
		// The mods' tech trees aren't Civ3's (MP Tournament's has Radio,
		// the others' are their own): the science advisor draws the arrows
		// itself.
		save.Rules.OwnTechTree = true;
		return save;
	}

	// The start the BIQ's first LEAD gives a player, for every civ of a game
	// on a generated map, or null if the BIQ has none: its era, gold and
	// government, the advances it lists, and its units. (Civ3's editor lists
	// a LEAD's advances itself, those of the eras before its own among them:
	// Future's lists all but its Modern Era's two; Modern's, in the
	// Industrial Age, lists none, so its players start with only their
	// civilization's own.) The League's mods have a LEAD for each of 8
	// players, all alike but for Future's last two, which list no advances,
	// an oversight it seems: the first is taken for all.
	public static ScenarioStart ScenarioStartOf(BiqData biq, SaveGame save) {
		if (biq.Lead == null || biq.Lead.Length == 0) {
			return null;
		}
		QueryCiv3.Biq.LEAD lead = biq.Lead[0];
		List<string> techs = (biq.LeadTech?[0] ?? []).Where(i => i >= 0 && i < save.Techs.Count).Select(i => save.Techs[i].Name).ToList();
		List<string> units = (biq.LeadPrto?[0] ?? [])
			.Where(u => u.UnitType >= 0 && u.UnitType < save.UnitPrototypes.Count)
			.SelectMany(u => Enumerable.Repeat(save.UnitPrototypes[u.UnitType].name, u.NumberOfStartUnits)).ToList();
		var eras = biq.Eras;
		return new ScenarioStart {
			Era = eras != null && lead.InitialEra >= 0 && lead.InitialEra < eras.Length ? eras[lead.InitialEra].CivilopediaEntry : null,
			Gold = lead.StartCash,
			Government = lead.Government >= 0 && lead.Government < save.Governments.Count ? save.Governments[lead.Government].name : null,
			// Null rather than empty: an empty list wouldn't survive the
			// rules' trip through Lua as a list.
			Techs = techs.Count > 0 ? techs : null,
			Units = units.Count > 0 ? units : null,
		};
	}

	// Gives the player the scenario's start (see Rules.ScenarioStart) but
	// its units, which it returns for AddPlayer to place; null for the
	// usual start.
	private static List<string> ApplyScenarioStart(SaveGame save, SavePlayer player) {
		if (save.Rules.ScenarioStart is not ScenarioStart start) {
			return null;
		}
		if (!string.IsNullOrEmpty(start.Era)) {
			player.eraCivilopediaName = start.Era;
		}
		player.gold = start.Gold;
		if (start.Government != null && save.Governments.Find(g => g.name == start.Government) is Government government) {
			player.governmentId = government.id;
		}
		foreach (string name in start.Techs ?? []) {
			if (save.Techs.Find(t => t.Name == name) is SaveTech tech) {
				player.knownTechs.Add(tech.id);
			}
		}
		// And every advance of the eras before its own, as Civ3 does: "setting
		// this value to any era later than the initial era will automatically
		// grant that player all the technologies in earlier eras" (the
		// Conquests editor's help, Initial Era). Modern is built on it: its
		// players start in the Industrial Age under a placeholder government,
		// its four governments need Mysticism, Currency, The Republic and
		// Democracy, and those are its only advances of earlier eras, with
		// nothing needed before them.
		int era = EraUtils.GetEraIndex(start.Era);
		foreach (SaveTech tech in save.Techs) {
			int techEra = EraUtils.GetEraIndex(tech.EraCivilopediaName);
			if (techEra >= 0 && techEra < era) {
				player.knownTechs.Add(tech.id);
			}
		}
		return start.Units?.Where(name => save.UnitPrototypes.Any(p => p.name == name)).ToList();
	}

	public static bool IsMpTournament(SaveGame save) => BundledRulesOf(save) == MpTournamentRules;
}

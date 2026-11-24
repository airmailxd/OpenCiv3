using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// The Civ Players League's ladder mods, MP Tournament, QC, Modern and
// Future, played under their own rules, bundled from their BIQs (see
// GameSetup.BundledRules.cs).
public class BundledScenariosTest {
	private static GameMode.Config Mode(bool standalone) => new("civ3", standalone ? ["standalone"] : []);

	// Where the mods' BIQs are, to check the bundled rules against: the
	// folder C7_LEAGUE_MODS names, or the user's Downloads/Scenarios. The
	// game never reads them.
	private static string ModsFolder => Environment.GetEnvironmentVariable("C7_LEAGUE_MODS")
		?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Scenarios");

	private static SaveGame Rules(GameScenario scenario, bool standalone = true) {
		return GameSetup.LoadBundledRules(PathUtils.GameModesDir, Mode(standalone), GameSetup.BundledRulesFor(scenario));
	}

	private static C7GameData.GameData NewGame(GameScenario scenario, int seed) {
		SaveGame save = Rules(scenario);
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_5,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = 80, height = 80, numberOfCivs = 4, distanceBetweenCivs = 12, techRate = 240, optimalNumberOfCities = 16 },
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.IsIndependent && !c.isBarbarian),
			difficulty = save.Difficulties[2],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 3).ToList(),
			victoryConditions = save.VictoryConditions,
			scenario = scenario,
		}.Populate(save);
		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(save);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		return gameData;
	}

	public static TheoryData<GameScenario> Mods => [GameScenario.MpTournament, GameScenario.Qc, GameScenario.Modern, GameScenario.Future];

	// Each mod's rules need no BIQ and no Civ3 install, and standalone
	// mode draws them with its own art; each has its own victory conditions
	// and turn limit, and the League's ladder settings.
	[Theory]
	[InlineData(GameScenario.MpTournament, 90)]
	[InlineData(GameScenario.Qc, 64)]
	[InlineData(GameScenario.Modern, 64)]
	[InlineData(GameScenario.Future, 90)]
	public void EachModHasItsRules(GameScenario scenario, int turnLimit) {
		SaveGame save = Rules(scenario);
		Assert.Null(save.ScenarioSearchPath);
		Assert.Equal(GameSetup.BundledRulesFor(scenario), GameSetup.BundledRulesOf(save));
		Assert.True(save.VictoryConditions.UseTurnLimit);
		Assert.Equal(turnLimit, save.TimeOptions.turnLimit);
		Assert.True(save.VictoryConditions.AllowDominationVictory);
		Assert.True(save.Rules.AcceleratedProduction);
		Assert.False(save.Rules.ScientificLeaders);
		Assert.False(save.Rules.AllowCultureFlips);
		Assert.All(save.UnitPrototypes, u => Assert.StartsWith("Art/Civilopedia/Icons/Units/", u.art.pediaArt.large));
		GameSetup.LadderSettings ladder = GameSetup.BundledRulesFor(scenario).Ladder;
		Assert.InRange(ladder.WorldSizeIndex, 0, save.WorldSizes.Count - 1);
		// Every advance a terraform or civ needs is one of these rules' own.
		HashSet<ID> techs = save.Techs.Select(t => t.id).ToHashSet();
		Assert.All(save.TerraForms.Where(t => t.RequiredTech != null), t => Assert.Contains(t.RequiredTech, techs));
		Assert.All(save.Civilizations.SelectMany(c => c.startingTechs), id => Assert.Contains(id, techs));
	}

	// MP Tournament: the League's changes to the tournament units (the
	// Hwach'a a cheap early bombard), and Map Making's map trading.
	[Fact]
	public void MpTournamentHasItsUnits() {
		SaveGame save = Rules(GameScenario.MpTournament);
		Assert.Equal(10, save.UnitPrototypes.Single(u => u.name == "Enkidu Warrior").shieldCost);
		Assert.Equal((20, 4), save.UnitPrototypes.Where(u => u.name == "Hwach'a").Select(u => (u.shieldCost, u.bombard)).Single());
		Assert.Contains(SaveTech.Flag.EnablesMapTrading, save.Techs.Single(t => t.Name == "Map Making").flags);
		Assert.True(save.VictoryConditions.CityElimination);
		Assert.Null(save.Rules.ScenarioStart);
	}

	// QC: citizens eat 4, Settlers take 3, Despotism has no tile penalty.
	[Fact]
	public void QcHasItsRules() {
		SaveGame save = Rules(GameScenario.Qc);
		Assert.Equal(4, save.Rules.FoodConsumptionPerCitizen);
		Assert.Equal(3, save.UnitPrototypes.Single(u => u.name == "Settler").populationCost);
		Assert.False(save.Governments.Single(g => g.name == "Despotism").hasTilePenalty);
		Assert.Equal(["4 Player", "5 Player", "6 Player", "7 Player", "8 Player"], save.WorldSizes.Select(w => w.name));
	}

	// A QC game's citizens eat 4 food each; MP Tournament's 2.
	[Fact]
	public void QcCitizensEatFour() {
		foreach ((GameScenario scenario, int food) in new[] { (GameScenario.Qc, 4), (GameScenario.MpTournament, 2) }) {
			C7GameData.GameData gameData = NewGame(scenario, 4);
			Player civ = gameData.players.First(p => !p.IsIndependent);
			City city = CityInteractions.BuildCity(civ.units.First(u => u.unitType.actions.Contains(UnitAction.BuildCity)).location, civ, "First");
			Assert.Equal(food * city.residents.Count, city.FoodConsumedPerTurn());
		}
	}

	// Modern: every civ starts in the Industrial Age with two Workers and
	// two Settlers, under the placeholder government it is to leave for
	// one of Modern's four, knowing its civilization's own advances and
	// those of the earlier eras, which are what the four need.
	[Fact]
	public void ModernStartsInTheIndustrialAge() {
		C7GameData.GameData gameData = NewGame(GameScenario.Modern, 1);
		Assert.Contains(gameData.governments, g => g.name == "Equality");
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			Assert.Equal(EraUtils.INDUSTRIAL_AGE_CVLPD, civ.eraCivilopediaName);
			Assert.Equal("!Switch Governments!", civ.government.name);
			Assert.Equal(10, civ.gold);
			Assert.Equal(2, civ.units.Count(u => u.unitType.name == "Settler"));
			Assert.Equal(2, civ.units.Count(u => u.unitType.name == "Worker"));
			HashSet<string> known = civ.knownTechs.Select(id => gameData.techs.Single(t => t.id == id).Name).ToHashSet();
			Assert.Superset(new HashSet<string> { "Mysticism", "Currency", "The Republic", "Democracy" }, known);
			Assert.Equal(civ.civilization.startingTechs.Count + 4, civ.knownTechs.Count);
			Assert.All(gameData.governments.Where(g => g.name is "Equality" or "Tradition" or "Freedom" or "Order"),
				g => Assert.Contains(g.prerequisiteTech, civ.knownTechs));
		}
	}

	// Future: every civ starts in the Modern Era, under Despotism with a
	// Settler and a Worker, knowing every advance but ICBM, Integrated
	// Defense and the civilizations' own (which it has if its civilization
	// starts with them).
	[Fact]
	public void FutureStartsKnowingAlmostEverything() {
		C7GameData.GameData gameData = NewGame(GameScenario.Future, 1);
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			Assert.Equal(EraUtils.MODERN_ERA_CVLPD, civ.eraCivilopediaName);
			Assert.Equal("Despotism", civ.government.name);
			Assert.Single(civ.units, u => u.unitType.name == "Settler");
			Assert.Single(civ.units, u => u.unitType.name == "Worker");
			HashSet<string> known = civ.knownTechs.Select(id => gameData.techs.Single(t => t.id == id).Name).ToHashSet();
			Assert.Equal(82, known.Count(name => !name.StartsWith("Being ")));
			Assert.DoesNotContain("ICBM", known);
			Assert.DoesNotContain("Integrated Defense", known);
		}
	}

	// Each sets up a game of its own type, as the game list names it, and
	// its save remembers the rules it is played under.
	[Theory]
	[InlineData(GameScenario.MpTournament, "MP Tournament")]
	[InlineData(GameScenario.Qc, "QC")]
	[InlineData(GameScenario.Modern, "Modern")]
	[InlineData(GameScenario.Future, "Future")]
	public void EachModSetsUpAGame(GameScenario scenario, string name) {
		C7GameData.GameData gameData = NewGame(scenario, 2);
		SaveGame saved = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.Equal(name, LanGameList.GameTypeOf(saved));
		Assert.Equal(GameSetup.BundledRulesFor(scenario).RulesetDir, saved.GameModeConfig.rulesetDir);
		Assert.Equal(GameSetup.BundledRulesFor(scenario).ScenarioName, saved.ScenarioName);
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			Assert.Contains(civ.units, u => u.unitType.actions.Contains(UnitAction.BuildCity));
		}
	}

	// Standalone mode draws a unit with no art of its own as a stock unit
	// like it (AnimationManager.FallbackArtFor): every mod unit has its own
	// art in C7/ModernArt, or one of the stock units it falls back to has.
	[Theory]
	[MemberData(nameof(Mods))]
	public void EveryUnitCanBeDrawnInStandalone(GameScenario scenario) {
		C7GameData.GameData gameData = NewGame(scenario, 3);
		string units = PathUtils.getBasePath("../C7/ModernArt/Art/Units");
		bool Has(string name) => !string.IsNullOrEmpty(name) && Directory.Exists(Path.Combine(units, name));
		List<string> problems = [];
		foreach (UnitPrototype unit in gameData.unitPrototypes) {
			if (Has(unit.art.mainArt.defaultName)) {
				continue;
			}
			string[] fallbacks = unit.isSettler ? ["Settler"]
				: unit.isWorker ? ["Worker"]
				: unit.IsSeaUnit() ? ["Galley", "Caravel"]
				: unit.movement > 1 && unit.IsLandUnit() ? ["Horseman"]
				: unit.bombard > 0 && unit.defense <= 1 ? ["Catapult"]
				: [];
			if (!fallbacks.Append("Spearman").Append("Warrior").Any(Has)) {
				problems.Add(unit.name);
			}
		}
		Assert.True(problems.Count == 0, string.Join(", ", problems));
	}

	// The bundled rules are what RegenerateRuleset makes of the mods' BIQs
	// today (skipped without them, or without the Civ3 install whose
	// conquests.biq the import fills in from).
	[SkippableTheory]
	[MemberData(nameof(Mods))]
	public void TheBundledRulesAreTheBiqs(GameScenario scenario) {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		GameSetup.BundledRules rules = GameSetup.BundledRulesFor(scenario);
		string path = Path.Combine(ModsFolder, rules.BiqName);
		Skip.If(!File.Exists(path), $"No {path}");
		SaveGame biq = GameSetup.ImportBundledBiq(path, Civ3Location.GetCiv3Path(), rules);
		SaveGame bundled = Rules(scenario, standalone: false);
		Assert.Equal(biq.UnitPrototypes.Select(u => (u.name, u.attack, u.defense, u.movement, u.shieldCost, u.populationCost)),
			bundled.UnitPrototypes.Select(u => (u.name, u.attack, u.defense, u.movement, u.shieldCost, u.populationCost)));
		Assert.Equal(biq.Techs.Select(t => (t.Name, t.Cost)), bundled.Techs.Select(t => (t.Name, t.Cost)));
		Assert.Equal(biq.Buildings.Select(b => (b.name, b.shieldCost, b.culturePerTurn)), bundled.Buildings.Select(b => (b.name, b.shieldCost, b.culturePerTurn)));
		Assert.Equal(biq.Governments.Select(g => (g.name, g.hasTilePenalty)), bundled.Governments.Select(g => (g.name, g.hasTilePenalty)));
		Assert.Equal(biq.Civilizations.Select(c => c.name), bundled.Civilizations.Select(c => c.name));
		Assert.Equal(biq.WorldSizes.Select(w => w.name), bundled.WorldSizes.Select(w => w.name));
		Assert.Equal((biq.Rules.AcceleratedProduction, biq.Rules.ScientificLeaders, biq.Rules.FoodConsumptionPerCitizen),
			(bundled.Rules.AcceleratedProduction, bundled.Rules.ScientificLeaders, bundled.Rules.FoodConsumptionPerCitizen));
		Assert.Equal((biq.TimeOptions.turnLimit, biq.VictoryConditions.UseTurnLimit), (bundled.TimeOptions.turnLimit, bundled.VictoryConditions.UseTurnLimit));
		Assert.Equal(biq.Rules.ScenarioStart?.Era, bundled.Rules.ScenarioStart?.Era);
		Assert.Equal(biq.Rules.ScenarioStart?.Units ?? [], bundled.Rules.ScenarioStart?.Units ?? []);
		Assert.Equal(biq.Rules.ScenarioStart?.Techs ?? [], bundled.Rules.ScenarioStart?.Techs ?? []);
		Assert.Equal((biq.Rules.MultiplayerTurnTimerBase, biq.Rules.MultiplayerTurnTimerPerCity, biq.Rules.MultiplayerTurnTimerPerUnit),
			(bundled.Rules.MultiplayerTurnTimerBase, bundled.Rules.MultiplayerTurnTimerPerCity, bundled.Rules.MultiplayerTurnTimerPerUnit));
		Assert.Equal(biq.UnitPrototypes.Select(u => (u.name, string.Join("+", u.requiredResources.Order()), string.Join(",", u.producibleBy.Order()))),
			bundled.UnitPrototypes.Select(u => (u.name, string.Join("+", u.requiredResources.Order()), string.Join(",", u.producibleBy.Order()))));
		Assert.Equal(biq.Buildings.Select(b => (b.name, b.requiredBuilding, b.doublesHappinessOf, b.greatWonderProperties?.buildingGainedInEveryCity,
				b.greatWonderProperties?.buildingGainedInEveryCityOnContinent)),
			bundled.Buildings.Select(b => (b.name, b.requiredBuilding, b.doublesHappinessOf, b.greatWonderProperties?.buildingGainedInEveryCity,
				b.greatWonderProperties?.buildingGainedInEveryCityOnContinent)));
	}

	// Only the civilizations the BIQ lets play (its GAME section's list) are
	// in the rules, so no player or random AI can have another: MP
	// Tournament's 13 of Civ3's 31, QC's 16 (not its Spectators), Modern's
	// 17 (not its Spectators, Rome or Vietnam) and Future's 21; and no unit
	// is producible by any other.
	[Theory]
	[InlineData(GameScenario.MpTournament, 13)]
	[InlineData(GameScenario.Qc, 16)]
	[InlineData(GameScenario.Modern, 17)]
	[InlineData(GameScenario.Future, 21)]
	public void OnlyThePlayableCivilizationsAreThere(GameScenario scenario, int civs) {
		SaveGame save = Rules(scenario);
		List<string> names = save.Civilizations.Where(c => !c.isBarbarian).Select(c => c.name).ToList();
		Assert.Equal(civs, names.Count);
		Assert.Single(save.Civilizations, c => c.isBarbarian);
		Assert.DoesNotContain("Spectators", names);
		// The barbarians' units are theirs.
		Assert.All(save.UnitPrototypes, u => Assert.Subset(save.Civilizations.Select(c => c.name).ToHashSet(), u.producibleBy));
		// A game of eight civs, all but one picked at random, has only these.
		for (int seed = 6; seed < 8; ++seed) {
			SaveGame game = Rules(scenario);
			WorldCharacteristics wc = new(game) {
				landform = WorldCharacteristics.Landform.Pangaea,
				oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
				age = WorldCharacteristics.Age.Billion_5,
				climate = WorldCharacteristics.Climate.Normal,
				temperature = WorldCharacteristics.Temperature.Temperate,
				barbarianActivity = BarbarianActivity.Roaming,
				worldSize = new WorldSize() { width = 100, height = 100, numberOfCivs = 8, distanceBetweenCivs = 10, techRate = 240, optimalNumberOfCities = 16 },
				mapSeed = seed,
			};
			new GameSetup {
				playerCivilization = game.Civilizations.First(c => !c.isBarbarian),
				difficulty = game.Difficulties[2],
				worldCharacteristics = wc,
				opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 7).ToList(),
				victoryConditions = game.VictoryConditions,
				scenario = scenario,
			}.Populate(game);
			Assert.All(game.Players.Where(p => !p.isBarbarian), p => Assert.Contains(p.civilization, names));
		}
	}

	// The playable civilizations are the BIQ's list, by its RACE indexes
	// (skipped without the BIQs, as TheBundledRulesAreTheBiqs is).
	[SkippableTheory]
	[MemberData(nameof(Mods))]
	public void ThePlayableCivilizationsAreTheBiqs(GameScenario scenario) {
		GameSetup.BundledRules rules = GameSetup.BundledRulesFor(scenario);
		string path = Path.Combine(ModsFolder, rules.BiqName);
		Skip.If(!File.Exists(path), $"No {path}");
		BiqData biq = new(QueryCiv3.Util.ReadFile(path));
		int[] playable = biq.GameCiv[0];
		Assert.NotEmpty(playable);
		Assert.Equal(playable.Select(i => biq.Race[i].Name).Prepend(biq.Race[0].Name),
			Rules(scenario).Civilizations.Select(c => c.name));
	}

	// A building refers to another by the BIQ's index, so to the right one
	// of several named alike (Modern's Sun Tzu's to the Barracks of no
	// government, not to Tradition's): each reference is to the building
	// made of the BLDG the BIQ names (skipped without the BIQs or a Civ3
	// install).
	[SkippableTheory]
	[MemberData(nameof(Mods))]
	public void BuildingsReferToTheBiqsBuildings(GameScenario scenario) {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		GameSetup.BundledRules rules = GameSetup.BundledRulesFor(scenario);
		string path = Path.Combine(ModsFolder, rules.BiqName);
		Skip.If(!File.Exists(path), $"No {path}");
		BiqData biq = new(QueryCiv3.Util.ReadFile(path));
		SaveGame save = GameSetup.ImportBundledBiq(path, Civ3Location.GetCiv3Path(), rules);
		// Wealth (and QC's second, like it) is an inflow, not a building.
		List<int> indexes = Enumerable.Range(0, biq.Bldg.Length).Where(i => !biq.Bldg[i].Capitalization).ToList();
		Assert.Equal(indexes.Count, save.Buildings.Count);
		string Named(int index) => index < 0 ? null : save.Buildings[indexes.IndexOf(index)].name;
		for (int i = 0; i < indexes.Count; ++i) {
			QueryCiv3.Biq.BLDG bldg = biq.Bldg[indexes[i]];
			SaveBuilding building = save.Buildings[i];
			Assert.Equal(bldg.CivilopediaEntry?.Trim(), building.civilopediaEntry);
			Assert.Equal(Named(bldg.RequiredBuilding), building.requiredBuilding);
			Assert.Equal(Named(bldg.DoublesHappiness), building.doublesHappinessOf);
			Assert.Equal(Named(bldg.GainInEveryCity), building.greatWonderProperties?.buildingGainedInEveryCity);
			Assert.Equal(Named(bldg.GainInEveryCityOnContinent), building.greatWonderProperties?.buildingGainedInEveryCityOnContinent);
		}
	}

	// Each bundled ruleset carries its BIQ's multiplayer turn timer: the
	// seconds a turn lasts, and those more for each city and each unit
	// (Civ3's own: 24, 3 and 1).
	[Theory]
	[InlineData(GameScenario.None, 24, 3, 1)]
	[InlineData(GameScenario.MpTournament, 16, 2, 1)]
	[InlineData(GameScenario.Qc, 42, 3, 0)]
	[InlineData(GameScenario.Modern, 37, 2, 1)]
	[InlineData(GameScenario.Future, 29, 3, 1)]
	[InlineData(GameScenario.Mdj, 22, 2, 1)]
	public void EachHasItsTurnTimer(GameScenario scenario, int seconds, int perCity, int perUnit) {
		SaveGame save = scenario == GameScenario.None ? GameMode.Load(PathUtils.GameModesDir, Mode(true)).GetSave()
			: GameSetup.LoadRulesFor(scenario, PathUtils.GameModesDir, Mode(true));
		Assert.Equal((seconds, perCity, perUnit),
			(save.Rules.MultiplayerTurnTimerBase, save.Rules.MultiplayerTurnTimerPerCity, save.Rules.MultiplayerTurnTimerPerUnit));
	}

	// The units that need three resources need all three (the import once
	// read only two): Modern Armor, and Modern's own.
	[Theory]
	[InlineData(GameScenario.None, "Modern Armor", "Aluminum+Oil+Rubber")]
	[InlineData(GameScenario.MpTournament, "Modern Armor", "Aluminum+Oil+Rubber")]
	[InlineData(GameScenario.Future, "Modern Armor", "Aluminum+Oil+Rubber")]
	[InlineData(GameScenario.Modern, "ICBM", "Aluminum+Coal+Uranium")]
	[InlineData(GameScenario.Modern, "Stealth Bomber", "Aluminum+Oil+Uranium")]
	public void UnitsNeedTheirThirdResource(GameScenario scenario, string unit, string resources) {
		SaveGame save = scenario == GameScenario.None ? GameMode.Load(PathUtils.GameModesDir, Mode(true)).GetSave() : Rules(scenario);
		Assert.Equal(resources, string.Join("+", save.UnitPrototypes.Single(u => u.name == unit).requiredResources.Order()));
	}

	// C7 knows buildings, units, advances, governments and civilizations by
	// name, so each mod's are all named apart (Modern's buildings repeated
	// for each government are told apart by it; see
	// ImportCiv3.MakeBuildingNamesUnique, and named as the mod does by
	// GameSetup.RenameModernBuildings).
	[Theory]
	[MemberData(nameof(Mods))]
	public void EverythingHasANameOfItsOwn(GameScenario scenario) {
		SaveGame save = Rules(scenario);
		foreach (IEnumerable<string> names in new[] { save.Buildings.Select(b => b.name), save.UnitPrototypes.Select(u => u.name),
				save.Techs.Select(t => t.Name), save.Governments.Select(g => g.name), save.Civilizations.Select(c => c.name) }) {
			Assert.Empty(names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key));
		}
		if (scenario == GameScenario.Modern) {
			Assert.Contains(save.Buildings, b => b.name == "T Barracks");
			Assert.Contains(save.Buildings, b => b.name == "Barracks");
		}
	}

	// Under a mod with "retain culture" (none of the League's four has it),
	// a captured city's culture is its captor's (Rules.RetainCultureOnCapture).
	[Fact]
	public void ACapturedCitysCultureIsKeptUnderTheRule() {
		foreach (bool rule in new[] { true, false }) {
			C7GameData.GameData gameData = NewGame(GameScenario.MpTournament, 5);
			gameData.rules.RetainCultureOnCapture = rule;
			Player[] civs = gameData.players.Where(p => !p.IsIndependent).Take(2).ToArray();
			City first = CityInteractions.BuildCity(civs[0].units.First(u => u.unitType.actions.Contains(UnitAction.BuildCity)).location, civs[0], "First");
			CityInteractions.BuildCity(civs[0].units.First(u => u.unitType.isWorker).location.neighbors.Values.First(t => t.IsLand() && !t.HasCity()), civs[0], "Second");
			first.perPlayerCulture[civs[0]] = 500;
			CityInteractions.CaptureCity(first, civs[1]);
			Assert.Equal(civs[1], first.owner);
			Assert.Equal(rule ? 500 : 0, first.GetCulture());
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.GameData;

// QC (the League's QC_1.74.biq) and MP Tournament (MPTournament.biq), see
// GameSetup.BundledRules.cs: their rules honoured in play, not only
// imported, and the AI playing them to their last turn.
public class QcAndMpTournamentTest {
	private readonly ITestOutputHelper output;

	public QcAndMpTournamentTest(ITestOutputHelper output) {
		this.output = output;
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	private static GameMode.Config Mode => new("civ3", ["standalone"]);

	// A new game of the mod on the League's ladder world (5 billion years,
	// a pangaea, 60% water), with the rules' world size of the given index.
	private static SaveGame NewSave(GameScenario scenario, int seed, int civs, int worldSize) {
		GameSetup.BundledRules rules = GameSetup.BundledRulesFor(scenario);
		SaveGame save = GameSetup.LoadBundledRules(PathUtils.GameModesDir, Mode, rules);
		WorldSize size = save.WorldSizes[worldSize];
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_5,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = size,
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.IsIndependent && !c.isBarbarian),
			difficulty = save.Difficulties[2],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, civs - 1).ToList(),
			victoryConditions = save.VictoryConditions,
			scenario = scenario,
		}.Populate(save);
		return save;
	}

	private static C7GameData.GameData NewGame(GameScenario scenario, int seed = 3) {
		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(NewSave(scenario, seed, 4, 0));
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		return gameData;
	}

	private static Player[] Civs(C7GameData.GameData gameData) => gameData.players.Where(p => !p.IsIndependent && !p.isBarbarians).ToArray();

	private static City FoundCity(Player civ, string name) {
		MapUnit settler = civ.units.First(u => u.unitType.actions.Contains(UnitAction.BuildCity));
		return CityInteractions.BuildCity(settler.location, civ, name);
	}

	private static void GrowTo(City city, int size) {
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident { city = city, nationality = city.owner.civilization, citizenType = city.residents[0].citizenType });
		}
	}

	private static Tech TechNamed(C7GameData.GameData gameData, string name) => gameData.techs.Single(t => t.Name == name);

	// The League's ladder settings ("Settings for ladder mods"): 5 billion
	// years, a pangaea and 60% water for both, MP Tournament on a tiny world
	// and QC on its 6-player one (the League's standard size).
	[Theory]
	[InlineData(GameScenario.Qc, "6 Player")]
	[InlineData(GameScenario.MpTournament, "Tiny")]
	public void TheLaddersSettings(GameScenario scenario, string worldSize) {
		GameSetup.BundledRules rules = GameSetup.BundledRulesFor(scenario);
		SaveGame save = GameSetup.LoadBundledRules(PathUtils.GameModesDir, Mode, rules);
		Assert.Equal(WorldCharacteristics.Age.Billion_5, rules.Ladder.Age);
		Assert.Equal(WorldCharacteristics.Landform.Pangaea, rules.Ladder.Landform);
		Assert.Equal(WorldCharacteristics.OceanCoverage.Percent_60, rules.Ladder.Ocean);
		Assert.Equal(worldSize, save.WorldSizes[rules.Ladder.WorldSizeIndex].name);
	}

	// Both mods keep Civ3's difficulties (their BIQs' are Civ3's own), so
	// they need none of their own.
	[Theory]
	[InlineData(GameScenario.Qc)]
	[InlineData(GameScenario.MpTournament)]
	public void TheDifficultiesAreCiv3s(GameScenario scenario) {
		SaveGame civ3 = GameMode.Load(PathUtils.GameModesDir, Mode).GetSave();
		SaveGame mod = GameSetup.LoadBundledRules(PathUtils.GameModesDir, Mode, GameSetup.BundledRulesFor(scenario));
		static object Of(Difficulty d) => (d.Name, d.NumberOfCitizensBornContent, d.NumberOfAIDefensiveStartingUnits, d.NumberOfAIOffensiveStartingUnits,
			d.ExtraStartUnit1, d.ExtraStartUnit2, d.AdditionalFreeUnitSupport, d.UnitSupportBonusForEachSettlement, d.AttackBonusAgainstBarbarians,
			d.HumanCostFactor, d.AiCostFactor);
		Assert.Equal(civ3.Difficulties.Select(Of), mod.Difficulties.Select(Of));
	}

	// QC's Settlers take three citizens: a city of three can't finish one
	// (it waits with its shields, as Civ3's does), a city of four can and
	// is left with one citizen. MP Tournament's take two, as Civ3's do.
	[Theory]
	[InlineData(GameScenario.Qc, 3)]
	[InlineData(GameScenario.MpTournament, 2)]
	public void SettlersTakeTheirCitizens(GameScenario scenario, int citizens) {
		C7GameData.GameData gameData = NewGame(scenario);
		City city = FoundCity(Civs(gameData)[0], "First");
		UnitPrototype settler = gameData.unitPrototypes.Single(u => u.name == "Settler");
		Assert.Equal(citizens, settler.populationCost);
		city.SetItemBeingProduced(settler);

		GrowTo(city, citizens);
		city.AddForestShields(city.owner.ShieldCost(settler));
		Assert.Null(city.ComputeTurnProduction());
		Assert.Equal(citizens, city.residents.Count);

		GrowTo(city, citizens + 1);
		city.AddForestShields(city.owner.ShieldCost(settler));
		Assert.Same(settler, city.ComputeTurnProduction());
		Assert.Single(city.residents);
	}

	// QC halves the stock costs in its rules (a Settler 15 shields, a
	// Warrior 5) and Accelerated Production halves the box again; both
	// mods' food boxes are halved too.
	[Theory]
	[InlineData(GameScenario.Qc, "Settler", 15)]
	[InlineData(GameScenario.Qc, "Warrior", 5)]
	[InlineData(GameScenario.MpTournament, "Settler", 30)]
	[InlineData(GameScenario.MpTournament, "Warrior", 10)]
	public void AcceleratedProductionHalvesTheBoxes(GameScenario scenario, string unit, int listed) {
		C7GameData.GameData gameData = NewGame(scenario);
		Assert.True(gameData.rules.AcceleratedProduction);
		City city = FoundCity(Civs(gameData)[0], "First");
		UnitPrototype prototype = gameData.unitPrototypes.Single(u => u.name == unit);
		Assert.Equal(listed, prototype.shieldCost);
		int halved = city.owner.ShieldCost(prototype);
		int food = city.FoodNeededToGrow();
		gameData.rules.AcceleratedProduction = false;
		Assert.Equal(city.owner.ShieldCost(prototype) / 2, halved);
		Assert.Equal(city.FoodNeededToGrow() / 2, food);
	}

	// QC's Despotism and Anarchy have no tile penalty: irrigated grassland
	// (2 food, and QC's irrigation adds 2) gives all 4. Under MP
	// Tournament's (irrigation adds 1) the 3 are cut to 2, as in Civ3.
	[Theory]
	[InlineData(GameScenario.Qc, "Despotism", 6)]
	[InlineData(GameScenario.Qc, "Anarchy", 6)]
	[InlineData(GameScenario.Qc, "Monarchy", 6)]
	[InlineData(GameScenario.MpTournament, "Despotism", 2)]
	[InlineData(GameScenario.MpTournament, "Anarchy", 2)]
	[InlineData(GameScenario.MpTournament, "Monarchy", 3)]
	public void DespotismsTilePenalty(GameScenario scenario, string government, int food) {
		C7GameData.GameData gameData = NewGame(scenario);
		Player civ = Civs(gameData)[0];
		civ.government = gameData.governments.Single(g => g.name == government);
		Tile grassland = gameData.map.tiles.First(t => t.baseTerrainType.Key == "grassland" && t.overlayTerrainType == t.baseTerrainType
			&& t.Resource == Resource.NONE && !t.BordersRiver() && !t.HasCity());
		grassland.overlays.Add(gameData.terrainImprovements.Single(ti => ti.key == Tile.TileOverlays.IRRIGATION));
		Assert.Equal(food, grassland.FoodYield(civ).yield);
	}

	// MP Tournament's irrigation gives desert 2 food (the League's change),
	// so irrigated desert makes 2 even under Despotism; other land gets 1.
	[Fact]
	public void MpTournamentIrrigatesDesertForTwo() {
		C7GameData.GameData gameData = NewGame(GameScenario.MpTournament);
		Player civ = Civs(gameData)[0];
		Tile desert = gameData.map.tiles.First(t => t.baseTerrainType.Key == "desert" && t.overlayTerrainType == t.baseTerrainType
			&& t.Resource == Resource.NONE && !t.BordersRiver() && !t.HasCity());
		Assert.Equal(0, desert.FoodYield(civ).yield);
		desert.overlays.Add(gameData.terrainImprovements.Single(ti => ti.key == Tile.TileOverlays.IRRIGATION));
		Assert.Equal(2, desert.FoodYield(civ).yield);
	}

	// Neither mod has scientific leaders or culture flips, in the game as
	// set up (the setup keeps the rules' choice): being first to an advance
	// brings no leader, however often it is rolled.
	[Theory]
	[InlineData(GameScenario.Qc)]
	[InlineData(GameScenario.MpTournament)]
	public void NoScientificLeadersOrCultureFlips(GameScenario scenario) {
		C7GameData.GameData gameData = NewGame(scenario);
		Assert.False(gameData.rules.ScientificLeaders);
		Assert.False(gameData.rules.AllowCultureFlips);
		Player civ = Civs(gameData)[0];
		FoundCity(civ, "First");
		Tech tech = civ.GetAvailableTechsToResearch(gameData.techs).First();
		for (int i = 0; i < 100; ++i) {
			Assert.Null(MapUnit.RollForScientificLeader(gameData, civ, tech));
		}
	}

	// Elimination: a QC civ is out once it has lost two cities to enemies,
	// an MP Tournament civ once it has lost one.
	[Theory]
	[InlineData(GameScenario.Qc, 2)]
	[InlineData(GameScenario.MpTournament, 1)]
	public void EliminationTakesTheRulesCities(GameScenario scenario, int cities) {
		C7GameData.GameData gameData = NewGame(scenario);
		Assert.True(gameData.victoryConditions.CityElimination);
		Assert.Equal(cities, gameData.victoryConditions.CityEliminationCount);
		Assert.Contains(gameData.victories, v => v is EliminationCondition);
		Player[] civs = Civs(gameData);
		for (int lost = 1; lost <= cities; ++lost) {
			City city = FoundCity(civs[0], $"City {lost}");
			// Its settler is used up; the next city needs another.
			if (lost < cities) {
				gameData.SpawnUnit(civs[0], gameData.unitPrototypes.Single(u => u.name == "Settler"),
					civs[0].units.First(u => u.unitType.isWorker).location.neighbors.Values.First(t => t.IsLand() && !t.HasCity()));
			}
			GrowTo(city, 3);
			CityInteractions.CaptureCity(city, civs[1]);
			Assert.Same(civs[1], city.owner);
			Assert.Equal(lost == cities, InstantElimination.IsEliminated(civs[0], gameData.victoryConditions));
		}
	}

	// QC's hidden advances (of no era): Satellites, which the BIQ gives
	// only the Spectators, who don't play (they aren't among its playable
	// civilizations, so not among the rules'), and which alone allows
	// ICBMs, SETI and the thrusters, so no one has them; and two that civs
	// start with, "(Unused)" (an offshore platform for England, Korea and
	// Portugal) and "Seafaring Civ (Not Dutch)". They can't be researched,
	// traded, stolen or learned from huts or the Great Library.
	[Fact]
	public void QcsHiddenAdvancesAreOnlyGiven() {
		C7GameData.GameData gameData = NewGame(GameScenario.Qc);
		string[] hidden = ["Satellites", "(Unused)", "Seafaring Civ (Not Dutch)"];
		Assert.Equal(hidden, gameData.techs.Where(t => t.EraCivilopediaName == EraUtils.HIDDEN_CVLPD).Select(t => t.Name));
		Assert.DoesNotContain(gameData.civilizations, c => c.name == "Spectators");
		Assert.DoesNotContain(gameData.civilizations, c => c.startingTechs.Contains(TechNamed(gameData, "Satellites").id));
		Assert.Equal(["ICBM"], gameData.unitPrototypes.Where(u => u.requiredTech?.Name == "Satellites").Select(u => u.name));

		Player[] civs = Civs(gameData);
		Player knows = civs[0], learner = civs[1];
		foreach (string name in hidden) {
			knows.knownTechs.Add(TechNamed(gameData, name).id);
		}
		// Every advance there is to learn otherwise, so only the hidden
		// ones are left.
		foreach (Tech tech in gameData.techs.Where(t => !hidden.Contains(t.Name))) {
			learner.knownTechs.Add(tech.id);
		}
		Assert.Empty(learner.GetAvailableTechsToResearch(gameData.techs));
		Assert.Empty(knows.GetTechsTradableTo(learner, gameData.techs));
		Assert.Empty(Espionage.StealableTechs(gameData, learner, knows));
		Assert.Empty(GoodyHuts.TechsToLearn(gameData, learner));
	}

	// QC's tree ends in "End Of Game", which allows nothing but "Game Ends,
	// Higher Score Wins", turning a city's shields into gold as Wealth does.
	// It can't be built before. With it, nothing is left to research but
	// future techs.
	[Fact]
	public void QcsTreeEndsInEndOfGame() {
		C7GameData.GameData gameData = NewGame(GameScenario.Qc);
		Tech end = TechNamed(gameData, "End Of Game");
		Assert.DoesNotContain(gameData.techs, t => t.Prerequisites.Contains(end));
		Inflow gameEnds = gameData.Inflows.Single(i => i.name == "Game Ends, Higher Score Wins");
		Assert.Same(end, gameEnds.requiredTech);

		Player civ = Civs(gameData)[0];
		City city = FoundCity(civ, "First");
		Assert.Contains(city.ListProductionOptions(gameData), p => p.name == "Wealth");
		Assert.DoesNotContain(city.ListProductionOptions(gameData), p => p == gameEnds);
		foreach (Tech tech in gameData.techs.Where(t => t.EraCivilopediaName != EraUtils.HIDDEN_CVLPD)) {
			civ.knownTechs.Add(tech.id);
		}
		Assert.Contains(city.ListProductionOptions(gameData), p => p == gameEnds);
		Assert.Empty(civ.GetAvailableTechsToResearch(gameData.techs));
		Assert.True(civ.KnowsEveryTech(gameData));
		// Wealth's commerce.
		city.SetItemBeingProduced(gameEnds);
		Assert.True(gameEnds.GetInflowYieldFunc(InflowYield.commerce)(new ScriptContext(civ, city)) >= 1);
	}

	// The bundled inflows, with what they need, are what the import makes of
	// the BIQs (skipped without them or a Civ3 install, as
	// BundledScenariosTest.TheBundledRulesAreTheBiqs is).
	[SkippableTheory]
	[InlineData(GameScenario.Qc)]
	[InlineData(GameScenario.MpTournament)]
	public void TheBundledInflowsAreTheBiqs(GameScenario scenario) {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		GameSetup.BundledRules rules = GameSetup.BundledRulesFor(scenario);
		string mods = Environment.GetEnvironmentVariable("C7_LEAGUE_MODS")
			?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Scenarios");
		string path = System.IO.Path.Combine(mods, rules.BiqName);
		Skip.If(!System.IO.File.Exists(path), $"No {path}");
		SaveGame biq = GameSetup.ImportBundledBiq(path, QueryCiv3.Civ3Location.GetCiv3Path(), rules);
		SaveGame bundled = GameSetup.LoadBundledRules(PathUtils.GameModesDir, new GameMode.Config("civ3", []), rules);
		Assert.Equal(biq.Inflows.Select(i => (i.name, i.iconRowIndex, i.requiredTech?.ToString())),
			bundled.Inflows.Select(i => (i.name, i.iconRowIndex, i.requiredTech?.ToString())));
	}

	// A game of each saves and loads (as a LAN host sends it), its Wealth
	// and QC's "Game Ends, Higher Score Wins" among its production.
	[Theory]
	[InlineData(GameScenario.Qc)]
	[InlineData(GameScenario.MpTournament)]
	public void AGameSavesAndLoads(GameScenario scenario) {
		C7GameData.GameData gameData = NewGame(scenario);
		City city = FoundCity(Civs(gameData)[0], "First");
		city.SetItemBeingProduced(gameData.Inflows.Last());
		SaveGame saved = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		C7GameData.GameData loaded = SaveGameFixture.HydrateSaveGame(saved);
		Assert.Equal(gameData.Inflows.Select(i => (i.name, i.requiredTech?.Name)), loaded.Inflows.Select(i => (i.name, i.requiredTech?.Name)));
		Assert.Equal(gameData.Inflows.Last().name, loaded.cities.Single().itemBeingProduced.name);
		Assert.All(loaded.Inflows, i => Assert.NotNull(i.GetInflowYieldFunc(InflowYield.commerce)));
	}

	// The AI plays every civ of a game on the League's ladder world to the
	// turn limit: each founds cities, builds and grows, and nothing goes
	// wrong on the way. The AI goes to war by itself (see
	// C7Engine.AI.OpportunityWar), and the civs fight: units are lost in
	// war in the second half of the game. (Before, left alone, the AI
	// seldom went to war in so few turns, so halfway the biggest civ was
	// made to go to war with its nearest neighbour; it no longer is.)
	// Under Elimination a civ that has lost its
	// cities' worth (QC's two, MP Tournament's one) is gone. At the limit
	// (QC's 64th turn, MP Tournament's 90th) the game ends, the best score
	// winning, as Civ3's does.
	[Theory]
	[InlineData(GameScenario.Qc, 11)]
	[InlineData(GameScenario.Qc, 21)]
	[InlineData(GameScenario.MpTournament, 12)]
	[InlineData(GameScenario.MpTournament, 21)]
	public async Task TheAiPlaysToTheTurnLimit(GameScenario scenario, int seed) {
		GameSetup.BundledRules rules = GameSetup.BundledRulesFor(scenario);
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		BehaviorEngine behaviors = GameMode.Load(PathUtils.GameModesDir, GameSetup.BundledGameMode(Mode, rules)).behaviors;
		await CreateGame.createGame(NewSave(scenario, seed, 4, 0), _ => behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;
		int limit = gameData.timeOptions.turnLimit;
		Assert.Contains(gameData.victories, v => v is TimeLimitVictory t && t.TurnLimit == limit);
		gameData.observerMode = true;
		foreach (Player player in gameData.players) {
			player.isHuman = false;
		}
		Dictionary<Player, int> mostCities = [];
		// The wars the civs declared, and the units killed by halfway.
		List<string> wars = [];
		Action<Player, Player> previous = C7Engine.AI.AiExperiments.onWarDeclared;
		C7Engine.AI.AiExperiments.onWarDeclared = (a, d) => {
			if (!a.IsIndependent && !d.IsIndependent) {
				wars.Add($"turn {gameData.turn}: {a.civilization.name} on {d.civilization.name}");
			}
		};
		int killedBefore = -1;
		try {
			TurnHandling.OnBeginTurn();
			TurnHandling.InitTurnData();
			Task firstTurn = TurnHandling.AdvanceTurn();
			EngineWaits.WaitForStartTurnMessage(_ => { });
			await firstTurn;
			while (!gameData.gameOver && gameData.turn <= limit) {
				if (gameData.turn == limit / 2) {
					killedBefore = Civs(gameData).Sum(p => p.unitsKilled);
				}
				new MsgEndTurn { turn = gameData.turn }.send();
				EngineWaits.WaitForStartTurnMessage(_ => { });
				foreach (Player p in Civs(gameData)) {
					mostCities[p] = Math.Max(mostCities.GetValueOrDefault(p), p.cities.Count);
				}
				if (gameData.turn % 16 == 0 || gameData.gameOver) {
					foreach (Player p in Civs(gameData)) {
						output.WriteLine($"turn {gameData.turn} {p.civilization.name}{(p.defeated ? " (out)" : "")}: {p.government.name} cities={p.cities.Count} pop={p.cities.Sum(c => c.residents.Count)} units={p.units.Count} techs={p.knownTechs.Count} killed={p.unitsKilled} lost={p.citiesLost}");
					}
				}
			}
		} finally {
			gameData.observerMode = false;
			C7Engine.AI.AiExperiments.onWarDeclared = previous;
		}
		output.WriteLine($"over at turn {gameData.turn}: {gameData.winner?.civilization.name} wins; wars: {string.Join("; ", wars)}");
		Assert.True(gameData.gameOver);
		Assert.True(gameData.turn <= limit);
		Assert.All(mostCities, kv => Assert.True(kv.Value >= 3, $"{kv.Key.civilization.name} had at most {kv.Value} cities"));
		Assert.All(Civs(gameData).Where(p => !p.defeated),
			p => Assert.True(p.cities.Sum(c => c.residents.Count) > p.cities.Count, $"{p.civilization.name}'s cities haven't grown"));
		Assert.NotEmpty(wars);
		Assert.True(killedBefore >= 0 && Civs(gameData).Sum(p => p.unitsKilled) > killedBefore, "no fighting in the second half");
		Assert.All(Civs(gameData).Where(p => p.citiesLost >= gameData.victoryConditions.CityEliminationCount), p => Assert.True(p.defeated));
		if (gameData.turn == limit) {
			// Ended by the limit: the best score among the civs left won.
			Player best = Civs(gameData).Where(p => !p.defeated)
				.OrderByDescending(p => gameData.history[p.id.ToString()].LastOrDefault()?.Score ?? 0).First();
			Assert.Same(best, gameData.winner);
		}
	}
}

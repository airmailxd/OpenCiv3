using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.UnitAI;
using C7GameData;
using C7GameData.AIData;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.AI;

// The AI playing the League's mods and stock rules (see LeagueAiGames).
public class LeagueAiTest {
	private readonly ITestOutputHelper output;

	public LeagueAiTest(ITestOutputHelper output) {
		this.output = output;
	}

	private sealed class LineSink(StreamWriter writer) : Serilog.Core.ILogEventSink {
		public void Emit(Serilog.Events.LogEvent e) {
			lock (writer) {
				writer.WriteLine($"t{C7Engine.EngineStorage.gameData?.turn} {e.RenderMessage()}");
			}
		}
	}

	// A table of AI games, for measuring an AI change: nothing unless
	// C7_LEAGUE_AI_OUT names a file to append the results to.
	//   C7_LEAGUE_AI_MODS   scenarios, comma separated (default
	//                       None,MpTournament,Qc,Modern,Future,Mdj)
	//   C7_LEAGUE_AI_SEEDS  seeds, comma separated (default 1,2,3)
	//   C7_LEAGUE_AI_CIVS   civs per game (default 4)
	//   C7_LEAGUE_AI_TURNS  turns (default each mod's limit, 90 for stock and MDJ)
	//   C7_LEAGUE_AI_TYPE   Standard or Advanced (default Standard)
	//   C7_LEAGUE_AI_LABEL  a label for the lines (default "run")
	[Fact]
	public async Task Measure() {
		string path = Environment.GetEnvironmentVariable("C7_LEAGUE_AI_OUT");
		if (string.IsNullOrEmpty(path)) {
			return;
		}
		GameScenario[] mods = (Environment.GetEnvironmentVariable("C7_LEAGUE_AI_MODS") ?? "None,MpTournament,Qc,Modern,Future,Mdj")
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Enum.Parse<GameScenario>).ToArray();
		int[] seeds = (Environment.GetEnvironmentVariable("C7_LEAGUE_AI_SEEDS") ?? "1,2,3")
			.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
		int civs = int.TryParse(Environment.GetEnvironmentVariable("C7_LEAGUE_AI_CIVS"), out int c) ? c : 4;
		int? turns = int.TryParse(Environment.GetEnvironmentVariable("C7_LEAGUE_AI_TURNS"), out int t) ? t : null;
		AiType aiType = Enum.TryParse(Environment.GetEnvironmentVariable("C7_LEAGUE_AI_TYPE"), out AiType a) ? a : AiType.Standard;
		string label = Environment.GetEnvironmentVariable("C7_LEAGUE_AI_LABEL") ?? "run";
		// C7_LEAGUE_AI_LOG: the engine's log, to a file.
		string logPath = Environment.GetEnvironmentVariable("C7_LEAGUE_AI_LOG");
		if (!string.IsNullOrEmpty(logPath)) {
			StreamWriter writer = new(logPath) { AutoFlush = true };
			Serilog.Log.Logger = new Serilog.LoggerConfiguration().MinimumLevel.Information()
				.WriteTo.Sink(new LineSink(writer)).CreateLogger();
		}
		foreach (GameScenario mod in mods) {
			foreach (int seed in seeds) {
				string line;
				try {
					// C7_LEAGUE_AI_TRACE=n: each civ's cities, production,
					// units and gold every n turns.
					int every = int.TryParse(Environment.GetEnvironmentVariable("C7_LEAGUE_AI_TRACE"), out int e) ? e : 0;
					Action<C7GameData.GameData> trace = every <= 0 ? null : g => {
						if (g.turn % every != 0) {
							return;
						}
						foreach (Player p in LeagueAiGames.Civs(g).Where(p => !p.defeated)) {
							string units = string.Join(",", p.units.GroupBy(u => u.unitType.name).Select(x => $"{x.Key}x{x.Count()}"));
							string cities = string.Join(" ", p.cities.Select(c => $"{c.name}({c.residents.Count}):{c.itemBeingProduced?.name}"));
							File.AppendAllText(path, $"  t{g.turn} {p.civilization.name} {p.government?.name} gold={p.gold} cities=[{cities}] units=[{units}]\n");
						}
					};
					LeagueAiGames.Result r = await LeagueAiGames.Play(mod, seed, civs, turns, aiType, trace);
					line = $"{label} {r.Summary()}\n    wars: {string.Join("; ", r.wars)}\n    captures: {string.Join("; ", r.captures)}"
						+ $"\n    first govs: {string.Join(", ", r.firstGovernment.Select(kv => kv.Key + "=" + kv.Value))}"
						+ $"\n    maxGold: {string.Join(", ", r.maxGold.Select(kv => kv.Key + "=" + kv.Value))}";
				} catch (Exception e) {
					line = $"{label} {mod} seed={seed} FAILED: {e.GetType().Name}: {e.Message}\n{e.StackTrace?.Split('\n').FirstOrDefault()}";
				}
				output.WriteLine(line);
				File.AppendAllText(path, line + "\n");
			}
		}
	}

	private static C7GameData.GameData NewGame(GameScenario scenario, int seed = 3) {
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(LeagueAiGames.NewSave(scenario, seed, 4));
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		// Every civ the AI's.
		foreach (Player p in gameData.players) {
			p.isHuman = false;
		}
		return gameData;
	}

	private static Tile StartOf(Player civ) => civ.units.First(u => u.unitType.actions.Contains(UnitAction.BuildCity)).location;

	// A free land tile for a city, on the start's landmass, so far from it.
	private static Tile SiteNear(C7GameData.GameData gameData, Tile start, int min, int max) {
		return gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& t.continent == start.continent && t.DistanceTo(start) >= min && t.DistanceTo(start) <= max
			&& !gameData.cities.Any(c => c.location.DistanceTo(t) < 4));
	}

	private static void Spawn(C7GameData.GameData gameData, Player civ, string unit, Tile tile, int count) {
		UnitPrototype type = gameData.unitPrototypes.First(u => u.name == unit);
		for (int i = 0; i < count; ++i) {
			gameData.SpawnUnit(civ, type, tile);
		}
	}

	// Two civs of a QC game, ours with three cities and theirs with one
	// within reach of ours, at peace, past a third of the game.
	private static (C7GameData.GameData gameData, Player us, Player them) TwoNeighbours() {
		C7GameData.GameData gameData = NewGame(GameScenario.Qc);
		Player[] civs = LeagueAiGames.Civs(gameData);
		Player us = civs[0], them = civs[1];
		Tile start = StartOf(us);
		CityInteractions.BuildCity(start, us, "Ours 1");
		CityInteractions.BuildCity(SiteNear(gameData, start, 4, 6), us, "Ours 2");
		CityInteractions.BuildCity(SiteNear(gameData, start, 4, 7), us, "Ours 3");
		CityInteractions.BuildCity(SiteNear(gameData, start, 8, 11), them, "Theirs");
		us.EnsureRelationshipExists(them);
		gameData.turn = 40;
		OpportunityWar.Forget(us);
		return (gameData, us, them);
	}

	// A civ much stronger than a neighbour with a city within reach picks
	// it for a war of opportunity once the game is far enough on.
	[Fact]
	public void AStrongCivPicksAWeakNeighbour() {
		(C7GameData.GameData gameData, Player us, Player them) = TwoNeighbours();
		Spawn(gameData, us, "Swordsman", us.cities[0].location, 8);
		Assert.True(OpportunityWar.Ready(us, gameData));
		Assert.True(OpportunityWar.Score(us, them, gameData, OpportunityWar.Needed(gameData)) > 0,
			$"distance {OpportunityWar.Distance(us, them)} power {us.CalculateMilitaryStrength()} against {OpportunityWar.DefendingPower(us, them, gameData)} needed {OpportunityWar.Needed(gameData)} declare {WarCampaign.CanDeclareWarOn(us, them)}");
		Assert.Same(them, OpportunityWar.Victim(us, gameData));
		Assert.Same(them, PreyWar.Prey(us, gameData));
	}

	// A civ weaker than its neighbour keeps the peace.
	[Fact]
	public void AWeakCivKeepsThePeace() {
		(C7GameData.GameData gameData, Player us, Player them) = TwoNeighbours();
		Spawn(gameData, us, "Swordsman", us.cities[0].location, 4);
		Spawn(gameData, them, "Swordsman", them.cities[0].location, 8);
		Assert.Null(OpportunityWar.Victim(us, gameData));
	}

	// Too early in a limited game, too near its end, or with the switch
	// off, no war of opportunity either.
	[Fact]
	public void NoWarTooEarlyTooLateOrSwitchedOff() {
		(C7GameData.GameData gameData, Player us, Player _) = TwoNeighbours();
		Spawn(gameData, us, "Swordsman", us.cities[0].location, 8);
		int limit = OpportunityWar.TurnLimit(gameData).Value;
		gameData.turn = 5;
		OpportunityWar.Forget(us);
		Assert.Null(OpportunityWar.Victim(us, gameData));
		gameData.turn = limit - OpportunityWar.MinTurnsLeft + 1;
		OpportunityWar.Forget(us);
		Assert.Null(OpportunityWar.Victim(us, gameData));
		gameData.turn = 40;
		OpportunityWar.Forget(us);
		AiExperiments.Enable(us, OpportunityWar.Off);
		Assert.Null(OpportunityWar.Victim(us, gameData));
	}

	// As the turns of a limited game run out, less of an edge is needed.
	[Fact]
	public void LessEdgeIsNeededAsTheTurnsRunOut() {
		C7GameData.GameData gameData = NewGame(GameScenario.Qc);
		gameData.turn = 10;
		double early = OpportunityWar.Needed(gameData);
		gameData.turn = 60;
		double late = OpportunityWar.Needed(gameData);
		Assert.Equal(OpportunityWar.RequiredRatio, early, 6);
		Assert.True(late < early);
		Assert.True(late >= OpportunityWar.LateRequiredRatio);
	}

	private static C7GameData.Save.SaveGame ModernSave(string civ) {
		C7GameData.Save.SaveGame save = GameSetup.LoadRulesFor(GameScenario.Modern, PathUtils.GameModesDir, new C7Engine.Lua.GameMode.Config("civ3", ["standalone"]));
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea, oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_5, climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate, barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize { width = 60, height = 60, numberOfCivs = 4, distanceBetweenCivs = 10, techRate = 240, optimalNumberOfCities = 16 },
			mapSeed = 11,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.Single(c => c.name == civ),
			difficulty = save.Difficulties[2],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 3).ToList(),
			victoryConditions = save.VictoryConditions,
			scenario = GameScenario.Modern,
		}.Populate(save);
		return save;
	}

	private static C7GameData.GameData ModernGame(string civ) {
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(ModernSave(civ));
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		foreach (Player p in gameData.players) {
			p.isHuman = false;
		}
		return gameData;
	}

	// Under Modern's rules a civ picks its favourite government when it
	// scores near the best (before, every AI chose Freedom), and each
	// government's own buildings count for it.
	[Theory]
	[InlineData("Nigeria", "Order")]
	[InlineData("Japan", "Tradition")]
	[InlineData("Canada", "Equality")]
	[InlineData("European Union", "Freedom")]
	public void ModernCivsLeanToTheirFavouriteGovernment(string civ, string government) {
		C7GameData.GameData gameData = ModernGame(civ);
		Player player = gameData.players.Single(p => p.civilization.name == civ);
		CityInteractions.BuildCity(StartOf(player), player, "Capital");
		Assert.Equal(government, GovernmentAI.BestGovernment(player, gameData).name);
		foreach (string name in new[] { "Tradition", "Freedom", "Order", "Equality" }) {
			Assert.True(GovernmentAI.OwnBuildingsWorth(player, gameData.governments.Single(g => g.name == name)) > 0, name);
		}
	}

	// A city builds its government's own buildings: a T Temple scores more
	// under Tradition for being Tradition's; another's gets nothing.
	[Fact]
	public void TheGovernmentsOwnBuildingsAreWanted() {
		C7GameData.GameData gameData = ModernGame("Japan");
		Player japan = gameData.players.Single(p => p.civilization.name == "Japan");
		City city = CityInteractions.BuildCity(StartOf(japan), japan, "Kyoto");
		japan.AdoptGovernment(gameData.governments.Single(g => g.name == "Tradition"));
		ChooseProducible.ProducibleStats stats = ChooseProducible.CalculateStats(city, japan, city.ListProductionOptions(gameData).ToList());
		Building temple = gameData.Buildings.First(b => b.name.StartsWith("T Temple"));
		Assert.True(ChooseProducible.GovernmentBuildingBonus(stats, japan, temple) > 0);
		Building bank = gameData.Buildings.First(b => b.name.StartsWith("F Bank"));
		Assert.Equal(0, ChooseProducible.GovernmentBuildingBonus(stats, japan, bank));
	}

	// A Modern Settler in a city with an airport, its site far off, is
	// airdropped onto or next to the site.
	[Fact]
	public void ASettlerIsAirdroppedToADistantSite() {
		C7GameData.GameData gameData = ModernGame("Japan");
		Player japan = gameData.players.Single(p => p.civilization.name == "Japan");
		Tile start = StartOf(japan);
		City city = CityInteractions.BuildCity(start, japan, "Kyoto");
		japan.AdoptGovernment(gameData.governments.Single(g => g.name == "Tradition"));
		city.AddBuilding(gameData.Buildings.First(b => b.providesVeteranAirUnits && b.requiredGovernment?.name == "Tradition"));
		Assert.NotNull(city.Airport());
		UnitPrototype settlerType = gameData.unitPrototypes.First(u => u.name == "Settler");
		Assert.True(AirdropAI.CanAirdrop(settlerType));
		MapUnit settler = gameData.SpawnUnit(japan, settlerType, start);
		Tile site = SiteNear(gameData, start, 6, Math.Min(8, settler.OperationalRange()));
		settler.currentAI = new SettlerAI(new SettlerAIData { goal = SettlerAIData.SettlerGoal.BUILD_CITY, destination = site });
		Assert.True(settler.CanAirdropFrom());
		Assert.True(settler.CanAirdropTo(site), $"to {site} from {start}");
		Assert.False(AIThreat.Assess(japan).EnemyCanReach(site));
		int drops = 0;
		AirdropAI.onAirdrop = (_, _) => ++drops;
		try {
			AirdropAI.PlayTurn(japan, gameData);
		} finally {
			AirdropAI.onAirdrop = null;
		}
		Assert.Equal(1, drops);
		Assert.True(settler.location.DistanceTo(site) <= 1);
	}

	// A lone city with land to settle doesn't start a great wonder (under
	// Future's rules every Standard AI started the Pyramids on its first
	// turn, and had one city at turn 60 in half the games).
	[Fact]
	public void ALoneCityExpandsBeforeAWonder() {
		C7GameData.GameData gameData = NewGame(GameScenario.Future, 1);
		Player civ = LeagueAiGames.Civs(gameData)[0];
		City city = CityInteractions.BuildCity(StartOf(civ), civ, "First");
		List<IProducible> options = city.ListProductionOptions(gameData).ToList();
		Assert.Contains(options, o => o is Building b && b.IsGreatWonder());
		ChooseProducible.ProducibleStats stats = ChooseProducible.CalculateStats(city, civ, options);
		Assert.True(ChooseProducible.ExpandsFirst(stats, civ));
		for (int i = 0; i < 20; ++i) {
			Assert.False(ChooseProducible.Choose(city, civ) is Building b && b.IsGreatWonder());
		}
	}

	// In a whole game, the AI goes to war by itself and takes a city (under
	// MP Tournament's Elimination, a civ that loses one is out, its cities
	// gone with it), within the mod's turn limit (90 turns for stock
	// rules). Before OpportunityWar, in 8 of 9 such games nobody declared
	// war at all.
	[Theory]
	[InlineData(GameScenario.Qc, 3)]
	[InlineData(GameScenario.MpTournament, 4)]
	[InlineData(GameScenario.Modern, 1)]
	[InlineData(GameScenario.Future, 1)]
	[InlineData(GameScenario.None, 5)]
	public async Task TheAiGoesToWarAndTakesACity(GameScenario scenario, int seed) {
		LeagueAiGames.Result r = await LeagueAiGames.Play(scenario, seed);
		output.WriteLine(r.Summary());
		output.WriteLine($"wars: {string.Join("; ", r.wars)}");
		output.WriteLine($"captures: {string.Join("; ", r.captures)}");
		Assert.NotEmpty(r.wars);
		Assert.True(r.captures.Count > 0 || r.eliminated.Count > 0, "no city taken");
	}

	// Modern's AIs don't all take the same government (before, every one
	// chose Freedom).
	[Fact]
	public async Task ModernAisChooseDifferentGovernments() {
		LeagueAiGames.Result r = await LeagueAiGames.Play(GameScenario.Modern, 3, turns: 12);
		output.WriteLine(string.Join(", ", r.firstGovernment.Select(kv => kv.Key + "=" + kv.Value)));
		Assert.Equal(r.civs, r.firstGovernment.Count);
		Assert.True(r.firstGovernment.Values.Distinct().Count() >= 2);
	}
}

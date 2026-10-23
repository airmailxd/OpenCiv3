using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7Engine.Lua;
using C7GameData;
using C7GameData.AIData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// How the AI plays Rising Tide (see TideAI).
public class RisingTideAITest {
	private static C7GameData.GameData NewGame(int seed) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Continents,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = 80, height = 80, numberOfCivs = 4, distanceBetweenCivs = 12, techRate = 240, optimalNumberOfCities = 16 },
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(d => d.Name == "Regent"),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 3).ToList(),
			victoryConditions = new VictoryConditions(),
			scenario = GameScenario.RisingTide,
		}.Populate(save);
		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(save);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
		gameData.map.recomputeContinents();
		return gameData;
	}

	private static Player FirstCiv(C7GameData.GameData gameData) {
		Player civ = gameData.players.First(p => !p.isBarbarians);
		civ.isHuman = false;
		foreach (Tile t in gameData.map.tiles) {
			civ.tileKnowledge.knownTiles.Add(t);
		}
		return civ;
	}

	private static UnitPrototype Prototype(C7GameData.GameData gameData, string name) {
		return gameData.unitPrototypes.First(p => p.name == name);
	}

	// A tile d steps from the sea drowns at the d-th wave.
	[Fact]
	public void EachTileDrownsAtTheWaveItsDepthSays() {
		C7GameData.GameData gameData = NewGame(11);
		Dictionary<Tile, int> waves = gameData.map.tiles.Where(t => t.IsLand()).ToDictionary(t => t, TideAI.WavesUntilDrowned);
		Assert.Contains(waves.Values, w => w == 1);
		Assert.True(TideAI.DeepestLand() >= GameSetup.RisingTideStartDepth);
		for (int wave = 1; wave <= 3; ++wave) {
			gameData.turn = gameData.risingTide.nextWaveTurn;
			RisingTide.BeginTurn(gameData);
			foreach ((Tile t, int w) in waves) {
				Assert.Equal(w > wave, t.IsLand());
			}
		}
	}

	// The turns until each wave follow the timetable, 21, 13, 8, ... apart.
	[Fact]
	public void TheTurnsUntilEachWaveFollowTheTimetable() {
		C7GameData.GameData gameData = NewGame(12);
		Assert.Equal(21, TideAI.TurnsUntilWave(1));
		Assert.Equal(34, TideAI.TurnsUntilWave(2));
		Assert.Equal(42, TideAI.TurnsUntilWave(3));
		gameData.turn = 30;
		Assert.Equal(TideAI.Never, TideAI.TurnsUntilWave(TideAI.Never));
		Tile shore = RisingTide.Shore(gameData).First();
		Assert.Equal(1, TideAI.WavesUntilDrowned(shore));
		Assert.True(TideAI.DrownsWithin(shore, 21 - 0));
	}

	// Outside Rising Tide every tile lasts for ever, and none of this
	// changes anything.
	[Fact]
	public void WithoutTheTideNothingDrowns() {
		C7GameData.GameData gameData = NewGame(13);
		gameData.rules.RisingTide = false;
		Player civ = FirstCiv(gameData);
		Tile shore = RisingTide.Shore(gameData).First(t => t.IsAllowCities() && !t.HasCity());
		Assert.Equal(TideAI.Never, TideAI.WavesUntilDrowned(shore));
		Assert.Equal(1f, TideAI.SiteSurvivalFactor(shore));
		City city = CityInteractions.BuildCity(shore, civ, "Ys");
		Assert.False(TideAI.TooLateToBuild(city, gameData.Buildings.First(b => b.name == "Temple")));
		MapUnit archer = gameData.SpawnUnit(civ, Prototype(gameData, "Archer"), shore);
		Assert.False(TideAI.MustFlee(archer));
	}

	// Settlers only choose sites that last at least a few waves, and the
	// longer a site lasts the more it is worth.
	[Fact]
	public void SettlersChooseSitesThatLast() {
		C7GameData.GameData gameData = NewGame(14);
		Player civ = FirstCiv(gameData);
		MapUnit settler = civ.units.First(u => u.unitType.isSettler);
		int least = System.Math.Min(TideAI.MinSiteWaves, TideAI.DeepestLand());

		Dictionary<Tile, float> sites = SettlerLocationAI.GetScoredSettlerCandidates(settler.location, civ);
		Assert.NotEmpty(sites);
		Assert.All(sites.Keys, t => Assert.True(TideAI.WavesUntilDrowned(t) >= least));
		double deepAll = sites.Keys.Average(t => TideAI.WavesUntilDrowned(t));
		double deepBest = sites.OrderByDescending(s => s.Value).Take(5).Average(s => TideAI.WavesUntilDrowned(s.Key));
		Assert.True(deepBest > deepAll, $"the best sites last {deepBest} waves, all {deepAll}");
		Assert.True(TideAI.SiteSurvivalFactor(sites.MaxBy(s => TideAI.WavesUntilDrowned(s.Key)).Key)
			> TideAI.SiteSurvivalFactor(sites.MinBy(s => TideAI.WavesUntilDrowned(s.Key)).Key));

		// The first city goes a few tiles inland at most, where the land
		// lasts at least as long as where the settler starts.
		SettlerAIData plan = SettlerAI.MakeAiData(settler, civ);
		Assert.Equal(SettlerAIData.SettlerGoal.BUILD_CITY, plan.goal);
		Assert.True(plan.destination.DistanceTo(settler.location) <= TideAI.FirstCityMaxWalk);
		Assert.True(TideAI.WavesUntilDrowned(plan.destination) >= TideAI.WavesUntilDrowned(settler.location));
	}

	// Under the tide cities may stand with only a tile between them.
	[Fact]
	public void CitiesStandCloserUnderTheTide() {
		C7GameData.GameData gameData = NewGame(15);
		Player civ = FirstCiv(gameData);
		MapUnit settler = civ.units.First(u => u.unitType.isSettler);
		City city = CityInteractions.BuildCity(settler.location, civ, "Atlantis");
		Tile near = city.location.GetTilesWithinTileSquare(2).First(t => t != Tile.NONE && t.DistanceTo(city.location) == 2
			&& t.IsAllowCities() && TideAI.LastsLongEnough(t, TideAI.MinKeptSiteWaves) && (t.OwningPlayer() == null || t.OwningPlayer() == civ));
		Assert.True(SettlerLocationAI.IsStillValidSite(near, civ));
		Assert.False(SettlerLocationAI.IsStillValidSite(city.location.neighbors.Values.First(t => t.IsLand()), civ));
	}

	// A city the sea will soon take stops building improvements, and wants
	// settlers more than a city that will stand.
	[Fact]
	public void ADoomedCityBuildsSettlersNotImprovements() {
		C7GameData.GameData gameData = NewGame(16);
		Player civ = FirstCiv(gameData);
		gameData.turn = 10;
		Tile deep = gameData.map.tiles.Where(t => t.IsAllowCities() && t.unitsOnTile.Count == 0).MaxBy(TideAI.WavesUntilDrowned);
		City safe = CityInteractions.BuildCity(deep, civ, "Safe");
		Tile shore = RisingTide.Shore(gameData).First(t => t.IsAllowCities() && t.unitsOnTile.Count == 0 && t.DistanceTo(deep) > 6
			&& !SettlerLocationAI.IsInvalidCityLocation(t));
		City doomed = CityInteractions.BuildCity(shore, civ, "Doomed");
		foreach (City c in new[] { safe, doomed }) {
			gameData.SpawnUnit(civ, Prototype(gameData, "Archer"), c.location);
		}
		Assert.True(TideAI.CityTurnsLeft(doomed) <= TideAI.DoomedCityTurns);

		Building temple = gameData.Buildings.First(b => b.name == "Temple");
		// Both nearly done: the doomed city wouldn't keep it long enough.
		safe.SetStoredShields(civ.ShieldCost(temple) - 1);
		doomed.SetStoredShields(civ.ShieldCost(temple) - 1);
		Assert.True(TideAI.TooLateToBuild(doomed, temple));
		Assert.False(TideAI.TooLateToBuild(safe, temple), $"temple in {safe.TurnsToProduce(temple)} turns, {TideAI.CityTurnsLeft(safe)} left, size {safe.residents.Count}");
		Assert.Equal(-1000f, ChooseProducible.ScoreBuilding(ChooseProducible.CalculateStats(doomed, civ, [temple]), doomed, civ, temple));

		// A settler both can finish in time.
		UnitPrototype settler = Prototype(gameData, "Settler");
		safe.SetStoredShields(civ.ShieldCost(settler) - 1);
		doomed.SetStoredShields(civ.ShieldCost(settler) - 1);
		float SettlerScore(City c) => ChooseProducible.ScoreUnit(ChooseProducible.CalculateStats(c, civ, [settler]), c, civ, settler);
		Assert.True(SettlerScore(doomed) > SettlerScore(safe) + TideAI.DoomedSettlerBonus / 2,
			$"settler scores: doomed {SettlerScore(doomed)}, safe {SettlerScore(safe)}");
	}

	// A unit the next wave would drown makes for higher ground, and none
	// steps onto the shore just before the wave.
	[Fact]
	public async Task UnitsGetOutOfTheWayOfTheWave() {
		C7GameData.GameData gameData = NewGame(17);
		Player civ = FirstCiv(gameData);
		Tile shore = RisingTide.Shore(gameData).First(t => t.unitsOnTile.Count == 0 && t.OwningPlayer() == null
			&& t.neighbors.Values.Any(n => n.IsLand() && TideAI.WavesUntilDrowned(n) == 2 && n.unitsOnTile.Count == 0 && n.OwningPlayer() == null));
		MapUnit archer = gameData.SpawnUnit(civ, Prototype(gameData, "Archer"), shore);

		// With the wave far off, it stays.
		gameData.turn = 10;
		Assert.False(TideAI.MustFlee(archer));
		Assert.False(await TideAI.EvacuateIfNeeded(archer, civ));
		Assert.Equal(shore, archer.location);

		// The turn before it strikes, it goes.
		gameData.turn = 20;
		Assert.True(TideAI.MustFlee(archer));
		Assert.True(await TideAI.EvacuateIfNeeded(archer, civ));
		Assert.True(TideAI.WavesUntilDrowned(archer.location) >= 2);

		// And won't step back onto the shore now, though it may later.
		Assert.True(TideAI.StepWouldDrown(archer, shore));
		gameData.turn = 10;
		Assert.False(TideAI.StepWouldDrown(archer, shore));

		gameData.turn = 21;
		RisingTide.BeginTurn(gameData);
		Assert.Contains(archer, gameData.mapUnits);
	}

	// Workers don't improve land the sea will soon take.
	[Fact]
	public async Task WorkersLeaveTheShoreAlone() {
		C7GameData.GameData gameData = NewGame(18);
		Player civ = FirstCiv(gameData);
		// A city of five on the shore, working the land about it.
		Tile shore = RisingTide.Shore(gameData).First(t => t.IsAllowCities() && t.unitsOnTile.Count == 0 && t.OwningPlayer() == null
			&& t.GetLandNeighbors().Count >= 4);
		MapUnit settler = gameData.SpawnUnit(civ, Prototype(gameData, "Settler"), shore);
		City city = await settler.BuildCity("Ys");
		MapUnit worker = gameData.SpawnUnit(civ, Prototype(gameData, "Worker"), shore);
		gameData.turn = 18;

		// Without the tide the worker improves the land nearest it, on the
		// shore.
		gameData.rules.RisingTide = false;
		WorkerAIData plan = WorkerAI.MakeAiData(worker, civ);
		gameData.rules.RisingTide = true;
		Assert.True(plan?.destination != null && TideAI.DrownsWithin(plan.destination, TideAI.MinImprovedTileTurns),
			$"without the tide, the worker went to {plan?.destination} of {city.residents.Count} citizens' tiles");

		// With it, land that lasts.
		plan = WorkerAI.MakeAiData(worker, civ);
		Assert.True(plan?.destination == null || !TideAI.DrownsWithin(plan.destination, TideAI.MinImprovedTileTurns), $"worker sent to {plan?.destination}");
	}
}

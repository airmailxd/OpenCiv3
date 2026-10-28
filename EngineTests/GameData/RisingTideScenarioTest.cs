using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7Engine.Lua;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class RisingTideScenarioTest {
	private static (SaveGame, C7GameData.GameData) NewGame(int seed) {
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
		Civilization player = save.Civilizations.First(c => !c.isBarbarian && c.traits.Contains(Civilization.Trait.Expansionist));
		new GameSetup {
			playerCivilization = player,
			difficulty = save.Difficulties.First(d => d.Name == "Regent"),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 3).ToList(),
			victoryConditions = new VictoryConditions(),
			scenario = GameScenario.RisingTide,
		}.Populate(save);
		return (save, SaveGameFixture.HydrateSaveGame(save));
	}

	// One landmass; every civ on its shore with a capital of size 5, three
	// workers and two archers, and a scout if expansionist.
	[Fact]
	public void SetsUpTheScenario() {
		(_, C7GameData.GameData gameData) = NewGame(1);
		EngineStorage.InitializeGameDataForTests(gameData);
		Assert.True(gameData.rules.RisingTide);
		Assert.Equal(GameSetup.RisingTideCitySize, gameData.rules.NewCitySize);
		Assert.Equal(GameSetup.RisingTideCityCenterFood, gameData.rules.CityCenterFood);
		Assert.Equal(21, RisingTide.TurnsUntilNextWave(gameData));
		Assert.Contains(gameData.victories, v => v is RisingTideVictory);
		Assert.DoesNotContain(gameData.victories, v => v is ConquestVictory);
		gameData.map.recomputeContinents();
		Assert.Single(gameData.map.continents, c => c.First().IsLand());

		foreach (Player civ in gameData.players.Where(p => !p.isBarbarians)) {
			City capital = Assert.Single(civ.cities);
			Assert.True(capital.IsCapital());
			Assert.Equal(GameSetup.RisingTideCitySize, capital.residents.Count);
			Assert.Equal(0, civ.units.Count(u => u.unitType.isSettler));
			Assert.Equal(3, civ.units.Count(u => u.unitType.name == "Worker"));
			Assert.Equal(2, civ.units.Count(u => u.unitType.name == "Archer"));
			bool expansionist = civ.civilization.traits.Contains(Civilization.Trait.Expansionist);
			Assert.Equal(expansionist ? 1 : 0, civ.units.Count(u => u.unitType.name == gameData.rules.ScoutUnitType));
			// On the shore.
			Tile start = capital.location;
			Assert.True(start.NeighborsOcean());
			// Its center feeds two citizens even under Despotism, and an
			// agricultural civ's a third by fresh water.
			bool agricultural = civ.civilization.traits.Contains(Civilization.Trait.Agricultural);
			bool freshWater = start.BordersRiver() || start.NeighborsFreshWater();
			Assert.Equal(agricultural && freshWater ? 5 : 4, start.FoodYield(capital).yield);
			int toSea = gameData.map.tiles.Where(t => t.baseTerrainType.IsWater && !t.isFreshWater).Min(t => t.DistanceTo(start));
			Assert.Equal(GameSetup.RisingTideStartDepth, toSea);
		}
	}

	// The waves come 21, 13, 8, 5, 5, 3, 3, 2, 2, 1 turns apart, then every
	// turn.
	[Fact]
	public void TheWavesComeEverFaster() {
		Assert.Equal([21, 13, 8, 5, 5, 3, 3, 2, 2, 1, 1, 1], Enumerable.Range(0, 12).Select(RisingTide.Interval));
	}

	// A wave turns the shore to Coast, destroying the cities and drowning
	// the land units on it, and the next is set.
	[Fact]
	public async System.Threading.Tasks.Task AWaveFloodsTheShore() {
		(_, C7GameData.GameData gameData) = NewGame(2);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player civ = gameData.players.First(p => !p.isBarbarians);
		Tile inland = gameData.map.tiles.Where(t => t.IsAllowCities() && t.unitsOnTile.Count == 0 && !t.HasCity()).MaxBy(TideAI.WavesUntilDrowned);
		MapUnit settler = gameData.SpawnUnit(civ, gameData.unitPrototypes.First(p => p.isSettler), inland);
		City city = await settler.BuildCity("Atlantis");
		Assert.Equal(GameSetup.RisingTideCitySize, city.residents.Count);

		Tile shore = RisingTide.Shore(gameData).First(t => t.unitsOnTile.Count == 0 && !t.HasCity() && t.IsAllowCities());
		City doomed = CityInteractions.BuildCity(shore, civ, "Ys");
		MapUnit archer = gameData.SpawnUnit(civ, gameData.unitPrototypes.First(p => p.name == "Archer"), shore);
		int land = gameData.map.tiles.Count(t => t.IsLand());
		int shoreTiles = RisingTide.Shore(gameData).Count;
		// The art of each tile, which the flooded tiles' must change with.
		Dictionary<Tile, Civ3ExtraInfo> art = gameData.map.tiles.ToDictionary(t => t, t => t.ExtraInfo);
		HashSet<Tile> redrawn = RisingTide.Shore(gameData).ToHashSet();
		foreach (Tile t in redrawn.ToList()) {
			foreach (TileDirection d in new[] { TileDirection.SOUTH, TileDirection.SOUTHEAST, TileDirection.SOUTHWEST }) {
				if (t.neighbors.TryGetValue(d, out Tile n) && n != Tile.NONE) {
					redrawn.Add(n);
				}
			}
		}

		gameData.turn = 21;
		RisingTide.BeginTurn(gameData);
		Assert.True(shore.baseTerrainType.IsCoast);
		// The flooded tiles, and those whose art shows their corners, are
		// drawn anew; the rest are as they were.
		Assert.All(gameData.map.tiles, t => Assert.Equal(redrawn.Contains(t), !ReferenceEquals(art[t], t.ExtraInfo)));
		Assert.DoesNotContain(doomed, gameData.cities);
		Assert.DoesNotContain(archer, gameData.mapUnits);
		Assert.Equal(land - shoreTiles, gameData.map.tiles.Count(t => t.IsLand()));
		Assert.Equal(13, RisingTide.TurnsUntilNextWave(gameData));
		Assert.Equal(1, gameData.risingTide.waves);
	}

	// A wave washes away the barbarian camps and goody huts on the shore,
	// and drowns the barbarians there.
	[Fact]
	public void AWaveWashesAwayCampsAndHuts() {
		(_, C7GameData.GameData gameData) = NewGame(2);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		List<Tile> shore = RisingTide.Shore(gameData).Where(t => t.unitsOnTile.Count == 0 && !t.HasCity()).ToList();
		Tile camp = shore[0], hut = shore[1];
		camp.hasBarbarianCamp = true;
		gameData.map.barbarianCamps.Add(camp);
		MapUnit barbarian = gameData.SpawnUnit(barbarians, gameData.unitPrototypes.First(p => p.name == "Warrior"), camp);
		hut.hasGoodyHut = true;

		gameData.turn = 21;
		RisingTide.BeginTurn(gameData);
		Assert.False(camp.hasBarbarianCamp);
		Assert.DoesNotContain(camp, gameData.map.barbarianCamps);
		Assert.DoesNotContain(barbarian, gameData.mapUnits);
		Assert.False(hut.hasGoodyHut);
	}

	// The score: each turn survived is worth 10 points, each enemy unit
	// defeated 1 and each city taken 5, times the difficulty's factor (3
	// for Regent), on top of the usual score. A fallen civ's score stands.
	[Fact]
	public void SurvivingCountsMostInTheScore() {
		(_, C7GameData.GameData gameData) = NewGame(6);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player civ = gameData.players.First(p => !p.isBarbarians);
		civ.unitsKilled = 4;
		civ.citiesCaptured = 1;
		civ.UpdateHistory(gameData);
		civ.UpdateHistory(gameData);
		Assert.Equal(2, civ.turnsSurvived);
		Assert.Equal(3 * (2 * 10 + 4 * 1 + 1 * 5), RisingTide.ScoreBonus(civ, gameData));
		int usual = (int)System.Math.Round(ScoreVictory.ComputeTurnScore(civ, gameData), System.MidpointRounding.AwayFromZero);
		int score = HallOfFame.ScoreOf(gameData, civ);
		Assert.Equal(usual + RisingTide.ScoreBonus(civ, gameData), score);

		civ.defeated = true;
		civ.UpdateHistory(gameData);
		Assert.Equal(2, civ.turnsSurvived);
		Assert.Equal(score, HallOfFame.ScoreOf(gameData, civ));
	}

	// The game ends when one civ is left, and the best score wins, though
	// that civ has fallen.
	[Fact]
	public void TheBestScoreWinsWhenOneCivIsLeft() {
		(_, C7GameData.GameData gameData) = NewGame(7);
		EngineStorage.InitializeGameDataForTests(gameData);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		foreach (Player civ in civs) {
			gameData.history[civ.id.ToString()].Add(new HistTurnRecord { Score = civ == civs[1] ? 500 : 100 });
		}
		foreach (Player civ in civs.Skip(2)) {
			civ.defeated = true;
		}
		TurnHandling.CheckVictory(gameData);
		Assert.False(gameData.gameOver);

		civs[1].defeated = true;
		TurnHandling.CheckVictory(gameData);
		Assert.True(gameData.gameOver);
		Assert.Equal(civs[1], gameData.winner);
		Assert.Equal(0, gameData.victoryBonus);
	}

	// The game also ends, and is scored, when the tide takes everyone at
	// once.
	[Fact]
	public void TheBestScoreWinsWhenNoCivIsLeft() {
		(_, C7GameData.GameData gameData) = NewGame(8);
		EngineStorage.InitializeGameDataForTests(gameData);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		foreach (Player civ in civs) {
			gameData.history[civ.id.ToString()].Add(new HistTurnRecord { Score = civ == civs[2] ? 500 : 100 });
			civ.defeated = true;
		}
		TurnHandling.CheckVictory(gameData);
		Assert.True(gameData.gameOver);
		Assert.Equal(civs[2], gameData.winner);
	}

	// The sea is no refuge: no ships can be built, and a ship that sails
	// out of port sinks at once with what it carries.
	[Fact]
	public async System.Threading.Tasks.Task ShipsSinkAtSea() {
		(_, C7GameData.GameData gameData) = NewGame(4);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player civ = gameData.players.First(p => !p.isBarbarians);
		Tile shore = RisingTide.Shore(gameData).First(t => t.unitsOnTile.Count == 0 && !t.HasCity() && t.IsAllowCities());
		City port = CityInteractions.BuildCity(shore, civ, "Port");
		Assert.DoesNotContain(port.ListProductionOptions(gameData), p => p is UnitPrototype u && u.IsSeaUnit());

		UnitPrototype galley = gameData.unitPrototypes.First(p => p.IsSeaUnit() && p.capacity > 0);
		MapUnit ship = gameData.SpawnUnit(civ, galley, shore);
		MapUnit settler = gameData.SpawnUnit(civ, gameData.unitPrototypes.First(p => p.isSettler), shore);
		ship.Board(settler);
		(TileDirection dir, Tile sea) = shore.neighbors.First(n => n.Value != Tile.NONE && n.Value.baseTerrainType.IsWater && !n.Value.isFreshWater && n.Value.unitsOnTile.Count == 0);

		Assert.False(await ship.Move(dir));
		Assert.DoesNotContain(ship, gameData.mapUnits);
		Assert.DoesNotContain(settler, gameData.mapUnits);
	}

	// A resource deal the sea breaks ends with the wave, and no one is
	// blamed for breaking it.
	[Fact]
	public void TheSeaBreaksDealsWithoutBlame() {
		(_, C7GameData.GameData gameData) = NewGame(3);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		Player exporter = civs[0], importer = civs[1];
		// Cities inland, so both outlive their start cities on the shore.
		foreach (Player civ in new[] { exporter, importer }) {
			Tile deep = gameData.map.tiles.Where(t => t.IsAllowCities() && t.unitsOnTile.Count == 0 && !t.HasCity()
				&& t.GetTilesWithinRankDistance(1).All(n => n == Tile.NONE || !n.HasCity())).MaxBy(TideAI.WavesUntilDrowned);
			CityInteractions.BuildCity(deep, civ, civ.civilization.name + " Keep");
		}
		exporter.EnsureRelationshipExists(importer);
		importer.EnsureRelationshipExists(exporter);
		// A deal for a resource the exporter doesn't have, which can't be
		// kept.
		Resource luxury = gameData.Resources.First(r => r.Category == ResourceCategory.LUXURY);
		PlayerRelationship.RegisterMultiTurnDeal(exporter, importer, new MultiTurnDeal(DealType.Luxury, DealSubType.LuxuryPerTurn,
			DealDetails.Outbound, 0, luxury.Key, 20, 0));

		gameData.turn = 21;
		RisingTide.BeginTurn(gameData);
		Assert.DoesNotContain(exporter.playerRelationships[importer.id].multiTurnDeals, d => d.dealSubType == DealSubType.LuxuryPerTurn);
		Assert.False(importer.playerRelationships[exporter.id].brokeTradeDeal);
	}

	// A civ needs a city to live: one left with only a settler falls.
	[Fact]
	public void ASettlerAloneDoesNotKeepACivAlive() {
		(_, C7GameData.GameData gameData) = NewGame(5);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player civ = gameData.players.First(p => !p.isBarbarians);
		Tile inland = gameData.map.tiles.Where(t => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity()).MaxBy(TideAI.WavesUntilDrowned);
		gameData.SpawnUnit(civ, gameData.unitPrototypes.First(p => p.isSettler), inland);
		CityInteractions.DestroyCity(civ.cities[0]);
		gameData.CheckForCivDestructionAndNotifyUi(civ);
		Assert.True(civ.defeated);
	}

	// The tide survives a save.
	[Fact]
	public void TheTideSurvivesASave() {
		(_, C7GameData.GameData gameData) = NewGame(3);
		gameData.risingTide.waves = 4;
		gameData.risingTide.nextWaveTurn = 50;
		SaveGame loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.Equal(4, loaded.RisingTide.waves);
		Assert.Equal(50, loaded.RisingTide.nextWaveTurn);
	}

	// So do what the score counts.
	[Fact]
	public void TheScoreSurvivesASave() {
		(_, C7GameData.GameData gameData) = NewGame(3);
		Player civ = gameData.players.First(p => !p.isBarbarians);
		civ.turnsSurvived = 30;
		civ.unitsKilled = 7;
		civ.citiesCaptured = 2;
		SaveGame loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		SavePlayer saved = loaded.Players.First(p => p.id == civ.id);
		Assert.Equal((30, 7, 2), (saved.turnsSurvived, saved.unitsKilled, saved.citiesCaptured));
	}
}

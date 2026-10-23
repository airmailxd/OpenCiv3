using System.Collections.Generic;
using System.Linq;
using C7Engine;
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

	// One landmass; every civ near its edge with a settler, three workers
	// and two archers, and a scout if expansionist.
	[Fact]
	public void SetsUpTheScenario() {
		(_, C7GameData.GameData gameData) = NewGame(1);
		EngineStorage.InitializeGameDataForTests(gameData);
		Assert.True(gameData.rules.RisingTide);
		Assert.Equal(GameSetup.RisingTideCitySize, gameData.rules.NewCitySize);
		Assert.Equal(21, RisingTide.TurnsUntilNextWave(gameData));
		Assert.Contains(gameData.victories, v => v is ConquestVictory);
		gameData.map.recomputeContinents();
		Assert.Single(gameData.map.continents, c => c.First().IsLand());

		foreach (Player civ in gameData.players.Where(p => !p.isBarbarians)) {
			Assert.Empty(civ.cities);
			Assert.Equal(1, civ.units.Count(u => u.unitType.isSettler));
			Assert.Equal(3, civ.units.Count(u => u.unitType.name == "Worker"));
			Assert.Equal(2, civ.units.Count(u => u.unitType.name == "Archer"));
			bool expansionist = civ.civilization.traits.Contains(Civilization.Trait.Expansionist);
			Assert.Equal(expansionist ? 1 : 0, civ.units.Count(u => u.unitType.name == gameData.rules.ScoutUnitType));
			// Near the edge, but not on the shore.
			Tile start = civ.units[0].location;
			Assert.False(start.NeighborsOcean());
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
		MapUnit settler = civ.units.First(u => u.unitType.isSettler);
		City city = await settler.BuildCity("Atlantis");
		Assert.Equal(GameSetup.RisingTideCitySize, city.residents.Count);

		Tile shore = RisingTide.Shore(gameData).First(t => t.unitsOnTile.Count == 0 && !t.HasCity() && t.IsAllowCities());
		City doomed = CityInteractions.BuildCity(shore, civ, "Ys");
		MapUnit archer = gameData.SpawnUnit(civ, gameData.unitPrototypes.First(p => p.name == "Archer"), shore);
		int land = gameData.map.tiles.Count(t => t.IsLand());
		int shoreTiles = RisingTide.Shore(gameData).Count;

		gameData.turn = 21;
		RisingTide.BeginTurn(gameData);
		Assert.True(shore.baseTerrainType.IsCoast);
		Assert.DoesNotContain(doomed, gameData.cities);
		Assert.DoesNotContain(archer, gameData.mapUnits);
		Assert.Equal(land - shoreTiles, gameData.map.tiles.Count(t => t.IsLand()));
		Assert.Equal(13, RisingTide.TurnsUntilNextWave(gameData));
		Assert.Equal(1, gameData.risingTide.waves);
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
}

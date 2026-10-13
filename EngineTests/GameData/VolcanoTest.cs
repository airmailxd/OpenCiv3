using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Volcanic eruptions (see Volcano).
public class VolcanoTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;
	private readonly Tile volcano;

	public VolcanoTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		(gameData, player, city, volcano) = Setup(fixture);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	// A generated game with a city founded where the human's settler stands
	// and a volcano next to it.
	private static (C7GameData.GameData, Player, City, Tile) Setup(SaveGameFixture fixture) {
		C7GameData.GameData data = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(data);
		EngineStorage.animationsEnabled = false;
		Player p = data.players.First(p => p.isHuman);
		Tile tile = p.units.First(u => u.unitType.isSettler).location;
		City c = CityInteractions.BuildCity(tile, p, p.GetNextCityName());
		Tile v = tile.neighbors.Values.First(t => t != Tile.NONE && !t.IsWater() && !t.HasCity());
		v.overlayTerrainType = data.terrainTypes.Single(t => t.Key == "volcano");
		return (data, p, c, v);
	}

	private List<string> Popups() {
		List<string> result = new();
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			if (msg is MsgShowTemporaryPopup popup) {
				result.Add(popup.message);
			}
		}
		return result;
	}

	[Fact]
	public void TheRulesetHasConquestsEruptionPeriod() {
		Assert.Equal(5000, gameData.rules.VolcanoEruptionPeriod);
	}

	[Fact]
	public void LavaReachesOnlyTheLandAroundTheVolcano() {
		List<Tile> reach = Volcano.TilesInReach(volcano);
		Assert.NotEmpty(reach);
		Assert.All(reach, t => {
			Assert.False(t.IsWater());
			Assert.Contains(t, volcano.neighbors.Values);
		});
		Assert.DoesNotContain(volcano, reach);
	}

	[Fact]
	public void AnEruptionDestroysTheCityUnitsAndImprovementsItHitsAndPollutes() {
		Tile cityTile = city.location;
		Tile field = Volcano.TilesInReach(volcano).First(t => !t.HasCity());
		TerrainImprovement road = gameData.terrainImprovements.First(i => i.key == "road");
		field.overlays.Add(road);
		MapUnit warrior = gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Warrior"), field);
		MapUnit defender = gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Warrior"), cityTile);
		int unitsThere = cityTile.unitsOnTile.Count + field.unitsOnTile.Count;
		Popups();

		Volcano.EruptionResult result = Volcano.Erupt(gameData, volcano, new List<Tile> { field, cityTile });

		Assert.Equal(new[] { city.name }, result.citiesDestroyed);
		Assert.Equal(unitsThere, result.unitsDestroyed);
		Assert.Empty(cityTile.unitsOnTile);
		Assert.Empty(field.unitsOnTile);
		Assert.False(cityTile.HasCity());
		Assert.DoesNotContain(city, gameData.cities);
		Assert.DoesNotContain(warrior, field.unitsOnTile);
		Assert.DoesNotContain(warrior, player.units);
		Assert.DoesNotContain(defender, player.units);
		Assert.False(field.HasRoad());
		Assert.True(field.HasPollution());
		Assert.True(cityTile.HasPollution());
		Assert.True(cityTile.HasRuins());

		List<string> popups = Popups();
		Assert.Contains("Volcano has erupted!", popups);
		Assert.Contains($"{city.name} is torched by the volcanic eruption!", popups);
		Assert.Contains("Our units were burned to a crisp by a lava flow!", popups);
	}

	[Fact]
	public void TilesTheLavaMissesAreUntouched() {
		Tile cityTile = city.location;
		Tile field = Volcano.TilesInReach(volcano).First(t => !t.HasCity());
		Volcano.Erupt(gameData, volcano, new List<Tile> { field });
		Assert.True(cityTile.HasCity());
		Assert.False(cityTile.HasPollution());
		Assert.True(field.HasPollution());
	}

	[Fact]
	public void AVolcanoThatEruptsHitsFromOneToAllOfTheLandAroundIt() {
		int reach = Volcano.TilesInReach(volcano).Count;
		gameData.rules.VolcanoEruptionPeriod = 1;
		gameData.turn = 1;
		C7GameData.GameData.rng = new GameRandom(42);

		List<Volcano.EruptionResult> results = Volcano.ProcessEndOfRound(gameData);

		Volcano.EruptionResult ours = Assert.Single(results, r => r.volcano == volcano);
		Assert.InRange(ours.tilesHit.Count, 1, reach);
		Assert.Equal(ours.tilesHit.Count, ours.tilesHit.Distinct().Count());
		Assert.All(ours.tilesHit, t => Assert.True(t.HasPollution()));
	}

	[Fact]
	public void EruptionsAreTheSameWithTheSameSeed() {
		List<(int, int)> Run() {
			(C7GameData.GameData data, _, _, Tile v) = Setup(fixture);
			data.rules.VolcanoEruptionPeriod = 1;
			data.turn = 1;
			C7GameData.GameData.rng = new GameRandom(99);
			List<Volcano.EruptionResult> results = Volcano.ProcessEndOfRound(data);
			return results.SelectMany(r => r.tilesHit).Select(t => (t.XCoordinate, t.YCoordinate)).ToList();
		}
		List<(int, int)> first = Run();
		Assert.NotEmpty(first);
		Assert.Equal(first, Run());
	}

	[Fact]
	public void NoEruptionPeriodMeansNoEruptionsAndNoRandomNumbers() {
		gameData.rules.VolcanoEruptionPeriod = 0;
		C7GameData.GameData.rng = new Random(7);
		int expected = new Random(7).Next();
		Assert.Empty(Volcano.ProcessEndOfRound(gameData));
		Assert.Equal(expected, C7GameData.GameData.rng.Next());
	}

	// On a small map of its own: a volcano in grassland, with no calendar,
	// so a year a turn.
	[Fact]
	public void AVolcanoEruptsOnAverageOnceAnEruptionPeriod() {
		TerrainType grassland = new() { Key = "grassland" };
		TerrainType volcanoType = new() { Key = "volcano" };
		C7GameData.GameData data = new() { gameDifficulty = new Difficulty() };
		data.rules = new Rules { VolcanoEruptionPeriod = 100 };
		EngineStorage.InitializeGameDataForTests(data);
		GameMap map = new() { numTilesWide = 6, numTilesTall = 6, tiles = new List<Tile>() };
		for (int i = 0; i < 18; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y, baseTerrainType = grassland, overlayTerrainType = grassland });
		}
		map.computeNeighbors();
		data.map = map;
		Tile v = map.tileAt(2, 2);
		v.overlayTerrainType = volcanoType;
		C7GameData.GameData.rng = new GameRandom(5);

		int eruptions = 0;
		for (int turn = 0; turn < 5000; ++turn) {
			eruptions += Volcano.ProcessEndOfRound(data).Count;
		}
		// 1% of 5000 turns is 50.
		Assert.InRange(eruptions, 30, 70);
	}
}

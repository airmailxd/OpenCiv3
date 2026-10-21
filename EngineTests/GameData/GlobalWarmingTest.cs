using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Global warming (see GlobalWarming), as Civ3 FUN_004fd670 does it.
public class GlobalWarmingTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;
	// A land tile next to the human's city, which the human sees.
	private readonly Tile field;

	public GlobalWarmingTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => p.isHuman);
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		field = tile.neighbors.Values.First(t => t != Tile.NONE && !t.IsWater() && !t.HasCity());
		field.riverNorth = field.riverNortheast = field.riverEast = field.riverSoutheast = false;
		field.riverSouth = field.riverSouthwest = field.riverWest = field.riverNorthwest = false;
		Popups();
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private TerrainType Terrain(string key) => gameData.terrainTypes.Single(t => t.Key == key);

	private void SetTerrain(Tile t, string baseKey, string overlayKey = null) {
		t.baseTerrainType = Terrain(baseKey);
		t.overlayTerrainType = Terrain(overlayKey ?? baseKey);
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

	// conquests.biq's TERR "Pollution Effect": Grassland 1 (Plains),
	// Plains 0 (Desert), Forest and Jungle 14 (base terrain), Marsh 11
	// (Coast), the rest -1.
	[Fact]
	public void TheRulesetHasConquestsPollutionEffects() {
		Assert.Equal("plains", Terrain("grassland").pollutionEffect);
		Assert.Equal("desert", Terrain("plains").pollutionEffect);
		Assert.Equal(TerrainType.PollutionEffectBaseTerrain, Terrain("forest").pollutionEffect);
		Assert.Equal(TerrainType.PollutionEffectBaseTerrain, Terrain("jungle").pollutionEffect);
		Assert.Equal("coast", Terrain("marsh").pollutionEffect);
		foreach (string key in new[] { "desert", "tundra", "flood plain", "hills", "mountains", "volcano", "coast", "sea", "ocean" }) {
			Assert.Equal(TerrainType.PollutionEffectNone, Terrain(key).pollutionEffect);
		}
	}

	[Fact]
	public void SavesWithoutPollutionEffectsUseConquests() {
		Assert.Equal("plains", new TerrainType { Key = "grassland" }.PollutionEffect());
		Assert.Equal("desert", new TerrainType { Key = "plains" }.PollutionEffect());
		Assert.Equal(TerrainType.PollutionEffectBaseTerrain, new TerrainType { Key = "jungle" }.PollutionEffect());
		Assert.Equal(TerrainType.PollutionEffectNone, new TerrainType { Key = "hills" }.PollutionEffect());
		Assert.Equal(TerrainType.PollutionEffectNone, new TerrainType { Key = "hills", pollutionEffect = "none" }.PollutionEffect());
	}

	// World pollution is the cities' pollution icons plus the square of the
	// nuclear explosions and meltdowns.
	[Fact]
	public void WorldPollutionIsCityIconsPlusNuclearEventsSquared() {
		int icons = gameData.cities.Sum(c => c.PollutionIcons(gameData.rules));
		gameData.nuclearEvents = 3;
		Assert.Equal(icons + 9, GlobalWarming.WorldPollution(gameData));
	}

	[Fact]
	public void AMeltdownCountsAsANuclearEvent() {
		Meltdown.MeltDown(gameData, city);
		Assert.Equal(1, gameData.nuclearEvents);
	}

	[Fact]
	public void TheSunBrightensAtAQuarterAndAHalfOfTheTiles() {
		Assert.Equal(0, GlobalWarming.SunLevel(0, 1000));
		Assert.Equal(1, GlobalWarming.SunLevel(1, 1000));
		Assert.Equal(1, GlobalWarming.SunLevel(250, 1000));
		Assert.Equal(2, GlobalWarming.SunLevel(251, 1000));
		Assert.Equal(2, GlobalWarming.SunLevel(500, 1000));
		Assert.Equal(3, GlobalWarming.SunLevel(501, 1000));
	}

	[Fact]
	public void GrasslandTurnsToPlainsLosingItsIrrigationAndMine() {
		SetTerrain(field, "grassland");
		field.overlays.Add(gameData.terrainImprovements.First(i => i.key == "irrigation"));
		field.overlays.Add(gameData.terrainImprovements.First(i => i.key == "road"));

		GlobalWarming.Change change = GlobalWarming.Strike(gameData, field);

		Assert.NotNull(change);
		Assert.True(field.overlayTerrainType.IsPlains);
		Assert.True(field.baseTerrainType.IsPlains);
		Assert.DoesNotContain(field.overlays.GetImprovements(), i => i.key == "irrigation");
		Assert.True(field.HasRoad());
		Assert.Contains("Global Warming has turned Grassland into Plains!", Popups());
	}

	[Fact]
	public void AForestBecomesTheTerrainUnderIt() {
		SetTerrain(field, "plains", "forest");

		Assert.NotNull(GlobalWarming.Strike(gameData, field));

		Assert.True(field.overlayTerrainType.IsPlains);
		Assert.True(field.baseTerrainType.IsPlains);
		Assert.Contains("Global Warming has turned Forest into Plains!", Popups());
	}

	// Marsh's effect is Coast, and warming never turns land into water.
	[Fact]
	public void MarshHillsAndWaterAreLeftAlone() {
		foreach ((string b, string o) in new[] { ("grassland", "marsh"), ("grassland", "hills"), ("desert", "desert") }) {
			SetTerrain(field, b, o);
			Assert.Null(GlobalWarming.Strike(gameData, field));
			Assert.Equal(o, field.overlayTerrainType.Key);
		}
		Tile water = gameData.map.tiles.First(t => t.IsWater());
		TerrainType before = water.overlayTerrainType;
		Assert.Null(GlobalWarming.Strike(gameData, water));
		Assert.Equal(before, water.overlayTerrainType);
		Assert.Empty(Popups());
	}

	// Desert set by a river comes out as flood plain, which isn't what was
	// asked for, so Civ3 neither tells anyone nor clears the improvements.
	[Fact]
	public void PlainsByARiverBecomeFloodPlainSilently() {
		SetTerrain(field, "plains");
		field.riverNorth = true;
		field.overlays.Add(gameData.terrainImprovements.First(i => i.key == "mine"));

		Assert.Null(GlobalWarming.Strike(gameData, field));

		Assert.True(field.overlayTerrainType.IsFloodPlain);
		Assert.True(field.baseTerrainType.IsDesert);
		Assert.Contains(field.overlays.GetImprovements(), i => i.key == "mine");
		Assert.Empty(Popups());
	}

	// A terrain becoming something other than desert, plains, grassland or
	// tundra gets GLOBALWARMING_FOREST_JUNGLE, over grassland.
	[Fact]
	public void BecomingAnotherTerrainSaysTheOldOneWasDestroyed() {
		SetTerrain(field, "plains", "hills");
		Terrain("hills").pollutionEffect = "jungle";

		Assert.NotNull(GlobalWarming.Strike(gameData, field));

		Assert.True(field.overlayTerrainType.IsJungle);
		Assert.True(field.baseTerrainType.IsGrassland);
		Assert.Contains("Global Warming\" has destroyed Hills!", Popups());
	}

	[Fact]
	public void NoOneIsToldOfATileTheyCantSee() {
		Tile unseen = gameData.map.tiles.First(t => !t.IsWater() && gameData.players.All(p => p.tileKnowledge == null || !p.tileKnowledge.isActiveTile(t)));
		SetTerrain(unseen, "grassland");

		Assert.NotNull(GlobalWarming.Strike(gameData, unseen));

		Assert.Empty(Popups());
	}

	[Fact]
	public void NoPollutionNoWarming() {
		foreach (City c in gameData.cities) {
			Assert.Equal(0, c.PollutionIcons(gameData.rules));
		}
		Assert.Empty(GlobalWarming.ProcessEndOfRound(gameData));
	}

	[Fact]
	public void WarmingIsTheSameWithTheSameSeed() {
		List<(int, int, string)> Run() {
			C7GameData.GameData data = fixture.saveGame.ToGameData(fixture.behaviors);
			EngineStorage.InitializeGameDataForTests(data);
			data.nuclearEvents = 60;
			C7GameData.GameData.rng = new GameRandom(77);
			return GlobalWarming.ProcessEndOfRound(data)
				.Select(c => (c.tile.XCoordinate, c.tile.YCoordinate, c.newTerrain.Key)).ToList();
		}
		List<(int, int, string)> first = Run();
		Assert.NotEmpty(first);
		Assert.Equal(first, Run());
	}

	// On a small map of its own: with world pollution above the tile count,
	// every try strikes, and grassland and plains always change.
	[Fact]
	public void TriesAreWorldPollutionOverTenPlusOne() {
		TerrainType grassland = new() { Key = "grassland" };
		TerrainType plains = new() { Key = "plains" };
		TerrainType desert = new() { Key = "desert" };
		C7GameData.GameData data = new() { gameDifficulty = new Difficulty() };
		data.rules = new Rules();
		data.terrainTypes = new List<TerrainType> { desert, plains, grassland };
		EngineStorage.InitializeGameDataForTests(data);
		GameMap map = new() { numTilesWide = 6, numTilesTall = 6, tiles = new List<Tile>() };
		for (int i = 0; i < 18; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y, baseTerrainType = grassland, overlayTerrainType = grassland });
		}
		map.computeNeighbors();
		data.map = map;
		// 25 world pollution: 3 tries, each a sure strike.
		data.nuclearEvents = 5;
		C7GameData.GameData.rng = new GameRandom(3);

		Assert.Equal(3, GlobalWarming.ProcessEndOfRound(data).Count);
	}
}

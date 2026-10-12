using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// When there are at least as many islands that can hold a civ as there are
// civs, each civ starts on an island of its own, rather than two sharing one
// while another is left empty (owner's decision, 2026-10-12). Seed 632203657
// is the owner's archipelago game, where the English and French shared an
// island, as did the Maya and Hittites, with six big islands empty.
public class IslandStartsTest {
	// More than this many land tiles makes an island one a civ can start on
	// in the first placement passes (MapGenerator.MIN_TILES_PER_PLAYER_ISLAND).
	private const int IslandForACiv = 81;

	private static GameMap Generate(WorldCharacteristics.OceanCoverage ocean, int seed) {
		SaveGame save = GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Archipelago,
			oceanCoverage = ocean,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = save.WorldSizes.First(w => w.name == "Standard"),
			mapSeed = seed,
		};
		return MapGenerator.GenerateMap(wc);
	}

	[Theory]
	[InlineData(WorldCharacteristics.OceanCoverage.Percent_70, 632203657)]
	[InlineData(WorldCharacteristics.OceanCoverage.Percent_60, 632203657)]
	[InlineData(WorldCharacteristics.OceanCoverage.Percent_70, 1)]
	[InlineData(WorldCharacteristics.OceanCoverage.Percent_70, 2)]
	[InlineData(WorldCharacteristics.OceanCoverage.Percent_60, 3)]
	public void EachCivGetsAnIslandOfItsOwnWhenThereAreEnough(WorldCharacteristics.OceanCoverage ocean, int seed) {
		GameMap map = Generate(ocean, seed);
		List<int> bigIslands = map.continents.Where(c => c.First().IsLand() && c.Count > IslandForACiv).Select(c => c.First().continent).ToList();
		Assert.True(bigIslands.Count >= map.startingLocations.Count, $"only {bigIslands.Count} big islands for {map.startingLocations.Count} civs");

		Dictionary<int, int> startsPerIsland = map.startingLocations.GroupBy(s => s.continent).ToDictionary(g => g.Key, g => g.Count());
		Assert.All(startsPerIsland, kv => Assert.True(kv.Value == 1, $"{kv.Value} civs start on island {kv.Key}"));
	}
}

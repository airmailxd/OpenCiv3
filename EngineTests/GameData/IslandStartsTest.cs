using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The starts on an archipelago. Civ3's own generator places them (Civ3
// 0x5fe9c0), so how they share the islands is Civ3's: the owner's rules
// for that (2026-10-12: a civ to an island where there are enough, big
// islands for several civs, empty ones to settle) gave way to Civ3's.
public class IslandStartsTest {
	private static List<HashSet<Tile>> Islands(GameMap map) {
		return map.continents.Where(c => c.First().IsLand()).ToList();
	}

	private static GameMap Generate(string worldSize, WorldCharacteristics.OceanCoverage ocean, int seed, int coastalStartsNeeded = 0) {
		SaveGame save = GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Archipelago,
			oceanCoverage = ocean,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = save.WorldSizes.First(w => w.name == worldSize),
			mapSeed = seed,
			coastalStartsNeeded = coastalStartsNeeded,
		};
		return MapGenerator.GenerateMap(wc);
	}

	private static string Describe(GameMap map) {
		Dictionary<int, int> starts = map.startingLocations.GroupBy(s => s.continent).ToDictionary(g => g.Key, g => g.Count());
		return string.Join(" ", Islands(map).OrderByDescending(c => c.Count).Where(c => c.Count >= 10)
			.Select(c => c.Count + (starts.TryGetValue(c.First().continent, out int n) ? $"({n})" : "")));
	}

	[Theory]
	[InlineData("Huge", WorldCharacteristics.OceanCoverage.Percent_70, 405802261)]
	[InlineData("Huge", WorldCharacteristics.OceanCoverage.Percent_60, 405802261)]
	[InlineData("Huge", WorldCharacteristics.OceanCoverage.Percent_80, 405802261)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_70, 632203657)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_60, 632203657)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_70, 1)]
	[InlineData("Small", WorldCharacteristics.OceanCoverage.Percent_80, 3)]
	[InlineData("Tiny", WorldCharacteristics.OceanCoverage.Percent_80, 2)]
	[InlineData("Tiny", WorldCharacteristics.OceanCoverage.Percent_80, 4)]
	[InlineData("Large", WorldCharacteristics.OceanCoverage.Percent_60, 4)]
	// Only the seafaring civs must start on the coast (owner's decision,
	// 2026-10-12): the map has a coastal start for each of them, however
	// many there are.
	public void ThereIsACoastalStartForEachSeafaringCiv(string worldSize, WorldCharacteristics.OceanCoverage ocean, int seed) {
		int civs = GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3")).GetSave().WorldSizes.First(w => w.name == worldSize).numberOfCivs;
		foreach (int needed in new[] { 1, civs / 2, civs }) {
			GameMap map = Generate(worldSize, ocean, seed, needed);
			Assert.NotEmpty(map.startingLocations);
			Assert.True(map.startingLocations.Count(s => s.NeighborsOcean()) >= needed, $"{needed} coastal starts needed: {map.startingLocations.Count(s => s.NeighborsOcean())}");
		}
	}

	[Fact]
	public void SameSeedMakesTheSameIslandsAndStarts() {
		GameMap a = Generate("Huge", WorldCharacteristics.OceanCoverage.Percent_70, 405802261);
		GameMap b = Generate("Huge", WorldCharacteristics.OceanCoverage.Percent_70, 405802261);
		Assert.Equal(Describe(a), Describe(b));
		Assert.Equal(a.startingLocations.Select(t => (t.XCoordinate, t.YCoordinate)), b.startingLocations.Select(t => (t.XCoordinate, t.YCoordinate)));
	}
}

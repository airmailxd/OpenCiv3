using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// How civs are spread over an archipelago's islands. Each civ gets an
// island of its own where there are enough, rather than two sharing one
// while another is left empty (owner's decision, 2026-10-12); seed
// 632203657 is the owner's archipelago game where the English and French
// shared an island, as did the Maya and Hittites, with six big islands
// empty. Since then the islands come in different sizes, so a big island
// holds two or more civs (any number: owner's decision, 2026-10-12) and
// the islands left empty are the smaller ones meant for settling (owner's report on
// seed 405802261, a Huge archipelago where every island was 105 to 201
// tiles, each civ had one, and every start was 2 to 4 tiles from water).
public class IslandStartsTest {
	// A civ's room on an archipelago: half its share of the land
	// (MapGenerator.ArchipelagoTilesPerPlayer).
	private static int TilesPerCiv(GameMap map, int civs) {
		return Islands(map).Sum(c => c.Count) / (2 * civs);
	}

	// A civ shares an island only if its share of it is at least this
	// share of every empty island (MapGenerator.ISLAND_JOIN_SHARE, with
	// a little slack).
	private const double JoinShare = 0.6;

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

	// No island holds more civs than it has room for, and civs share an
	// island only when no island about as big as each one's share of it is
	// left empty.
	[Theory]
	[InlineData("Huge", WorldCharacteristics.OceanCoverage.Percent_70, 405802261)]
	[InlineData("Huge", WorldCharacteristics.OceanCoverage.Percent_60, 405802261)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_70, 632203657)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_60, 632203657)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_70, 1)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_70, 2)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_60, 3)]
	[InlineData("Small", WorldCharacteristics.OceanCoverage.Percent_80, 3)]
	[InlineData("Large", WorldCharacteristics.OceanCoverage.Percent_80, 2)]
	public void CivsShareAnIslandOnlyWhenNoComparableIslandIsLeftEmpty(string worldSize, WorldCharacteristics.OceanCoverage ocean, int seed) {
		GameMap map = Generate(worldSize, ocean, seed);
		int civs = map.startingLocations.Count;
		int tilesPerCiv = TilesPerCiv(map, civs);
		string description = Describe(map);

		Dictionary<int, int> startsPerIsland = map.startingLocations.GroupBy(s => s.continent).ToDictionary(g => g.Key, g => g.Count());
		List<int> emptyIslands = Islands(map).Where(c => !startsPerIsland.ContainsKey(c.First().continent)).Select(c => c.Count).ToList();
		foreach (HashSet<Tile> island in Islands(map).Where(c => startsPerIsland.ContainsKey(c.First().continent))) {
			int starts = startsPerIsland[island.First().continent];
			if (starts == 1) {
				continue;
			}
			Assert.True(starts <= island.Count / tilesPerCiv, $"{starts} civs on an island of {island.Count} tiles: {description}");
			double share = island.Count / (double)starts;
			Assert.All(emptyIslands, empty => Assert.True(share >= empty * JoinShare, $"civs share an island of {island.Count} tiles while one of {empty} is empty: {description}"));
		}
	}

	// The islands come in different sizes: big ones that two or more civs
	// share, middling ones and small ones left empty for settling. Not every
	// map has an island two civs share, as a big island may not have room
	// for two starts the world size's distance between civs apart.
	[Theory]
	[InlineData("Huge", WorldCharacteristics.OceanCoverage.Percent_70)]
	[InlineData("Huge", WorldCharacteristics.OceanCoverage.Percent_60)]
	[InlineData("Large", WorldCharacteristics.OceanCoverage.Percent_70)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_70)]
	[InlineData("Standard", WorldCharacteristics.OceanCoverage.Percent_80)]
	public void IslandsVaryInSizeAndSomeAreLeftToSettle(string worldSize, WorldCharacteristics.OceanCoverage ocean) {
		int big = 0, middling = 0, small = 0, shared = 0;
		foreach (int seed in new[] { 405802261, 1, 2 }) {
			GameMap map = Generate(worldSize, ocean, seed);
			int civs = map.startingLocations.Count;
			int share = 2 * TilesPerCiv(map, civs);
			string description = $"seed {seed}: {Describe(map)}";
			List<int> sizes = Islands(map).Select(c => c.Count).ToList();
			big += sizes.Count(s => s >= share);
			middling += sizes.Count(s => s >= share * .4 && s < share);
			small += sizes.Count(s => s >= share * .1 && s < share * .4);

			Dictionary<int, int> startsPerIsland = map.startingLocations.GroupBy(s => s.continent).ToDictionary(g => g.Key, g => g.Count());
			shared += startsPerIsland.Values.Count(n => n >= 2);
			Assert.True(Islands(map).Count(c => c.Count >= share * .1 && !startsPerIsland.ContainsKey(c.First().continent)) >= civs / 4,
				$"too few islands left to settle, {description}");
		}
		Assert.True(big >= 3, $"{big} big islands");
		Assert.True(middling >= 3, $"{middling} middling islands");
		Assert.True(small >= 3, $"{small} small islands");
		Assert.True(shared >= 3, $"{shared} islands shared by two or more civs");
	}

	// On an archipelago, starts that needn't be coastal are as likely as
	// not to be (owner's decision, 2026-10-12): left to the scores, almost
	// none were.
	[Fact]
	public void OtherStartsOnAnArchipelagoAreOftenCoastal() {
		int coastal = 0, starts = 0;
		foreach (int seed in new[] { 1426963693, 1, 2, 3 }) {
			GameMap map = Generate("Huge", WorldCharacteristics.OceanCoverage.Percent_70, seed);
			coastal += map.startingLocations.Count(s => s.NeighborsOcean());
			starts += map.startingLocations.Count;
		}
		Assert.InRange(coastal / (double)starts, .3, .75);
	}

	[Fact]
	public void SameSeedMakesTheSameIslandsAndStarts() {
		GameMap a = Generate("Huge", WorldCharacteristics.OceanCoverage.Percent_70, 405802261);
		GameMap b = Generate("Huge", WorldCharacteristics.OceanCoverage.Percent_70, 405802261);
		Assert.Equal(Describe(a), Describe(b));
		Assert.Equal(a.startingLocations.Select(t => (t.XCoordinate, t.YCoordinate)), b.startingLocations.Select(t => (t.XCoordinate, t.YCoordinate)));
	}
}

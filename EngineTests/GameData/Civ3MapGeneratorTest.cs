using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// A few facts of Civ3's own map generator (Civ3MapGenerator, its port),
// taken from Civ3: the numbers of a map Civ3 made, and rules read from its
// code. MapGenExactTest checks every tile against a corpus of Civ3's maps.
public class Civ3MapGeneratorTest {
	private static GameMap Civ3Map(int seed, WorldCharacteristics.Landform landform, WorldCharacteristics.OceanCoverage ocean, int coastalStarts) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		return MapGenerator.GenerateMap(new WorldCharacteristics(save) {
			landform = landform,
			oceanCoverage = ocean,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = save.WorldSizes.Single(w => w.name == "Standard"),
			mapSeed = seed,
			coastalStartsNeeded = coastalStarts,
		});
	}

	// Civ3's own Standard pangaea of seed 1, 70% water, normal and
	// temperate, 4 billion years, with one seafaring civ: the numbers of
	// its 4000 BC save (STD_Standard_Pan70_Nrm-Tmp-4B_s000001.SAV).
	[Fact]
	public void Civ3sPangaeaOfSeedOne() {
		GameMap map = Civ3Map(1, WorldCharacteristics.Landform.Pangaea, WorldCharacteristics.OceanCoverage.Percent_70, 1);
		Assert.Equal(1347, map.tiles.Count(t => t.IsLand()));
		Assert.Equal(477, map.tiles.Count(t => t.BordersRiver()));
		Assert.Equal(25, map.tiles.Count(t => t.hasGoodyHut));
		Assert.Equal(246, map.tiles.Count(t => t.isBonusShield));
		Assert.Equal(
			new[] { (22, 26), (30, 64), (33, 43), (38, 22), (46, 76), (51, 55), (63, 25), (63, 81) },
			map.startingLocations.Select(t => (t.XCoordinate, t.YCoordinate)).OrderBy(p => p));

		Dictionary<string, int> resources = map.tiles.Where(t => t.Resource != null && t.Resource != Resource.NONE).GroupBy(t => t.Resource.Key).ToDictionary(g => g.Key, g => g.Count());
		Dictionary<string, int> civ3 = new() {
			["Horses"] = 9,
			["Iron"] = 9,
			["Saltpeter"] = 6,
			["Coal"] = 6,
			["Oil"] = 6,
			["Rubber"] = 6,
			["Aluminum"] = 6,
			["Uranium"] = 6,
			["Wines"] = 5,
			["Furs"] = 4,
			["Dyes"] = 3,
			["Incense"] = 5,
			["Spices"] = 3,
			["Ivory"] = 3,
			["Silks"] = 4,
			["Gems"] = 4,
			["Whales"] = 18,
			["Game"] = 10,
			["Fish"] = 18,
			["Cattle"] = 7,
			["Wheat"] = 8,
			["Gold"] = 11,
			["Sugar"] = 13,
			["Tropical Fruit"] = 7,
			["Oasis"] = 6,
			["Tobacco"] = 10,
		};
		Assert.Equal(civ3.OrderBy(p => p.Key), resources.OrderBy(p => p.Key));
	}

	// How many of a strategic resource Civ3 places (Civ3 0x6020d0): the
	// number of civs times the appearance ratio, a quarter less for a
	// resource two or three terrains may have. So with 8 civs, ratios of
	// 160, 120 and 100 on two or three terrains make 9, 6 and 6, on every
	// map with the room.
	[Theory]
	[InlineData(WorldCharacteristics.Landform.Continents, 3)]
	[InlineData(WorldCharacteristics.Landform.Pangaea, 7)]
	[InlineData(WorldCharacteristics.Landform.Archipelago, 11)]
	public void EightCivsGetNineSixAndSixOfAStrategicResource(WorldCharacteristics.Landform landform, int seed) {
		GameMap map = Civ3Map(seed, landform, WorldCharacteristics.OceanCoverage.Percent_70, 0);
		int Count(string key) => map.tiles.Count(t => t.Resource?.Key == key);
		Assert.Equal(9, Count("Horses"));
		Assert.Equal(6, Count("Saltpeter"));
		Assert.Equal(6, Count("Uranium"));
	}

	// Civ3's random numbers (Civ3 0x6263f0, 0x626440): the state goes
	// state * 0x41C64E6D + 0x3039, and rand(n) is n times 15 bits of it.
	[Fact]
	public void TheRandomNumbersAreCiv3s() {
		Civ3MapGenerator.Rng rng = new(1);
		// 1 * 0x41C64E6D + 0x3039 = 0x41C67EA6, whose bits 16..30 are 16838.
		Assert.Equal(16838 * 100 >> 15, rng.Rand(100));
		Assert.Equal(0x41C67EA6u, rng.state);
		// n is cut to 16 bits.
		Civ3MapGenerator.Rng a = new(12345), b = new(12345);
		Assert.Equal(a.Rand(0x10000 + 7), b.Rand(7));
	}

	// Civ3 makes an archipelago again until it has three islands or more,
	// the largest less than twice the third, and keeps its tenth try
	// whatever it is (Civ3 0x5fc7e0).
	[Fact]
	public void AnArchipelagoIsThreeIslandsOrMoreOfLikeSize() {
		int accepted = 0;
		for (int seed = 1; seed <= 10; ++seed) {
			Civ3MapGenerator g = new(new Civ3MapGenerator.Settings {
				width = 100,
				height = 100,
				seed = seed,
				landform = 0,
				oceanCoverage = 1,
				climate = 1,
				temperature = 1,
				age = 1,
				barbarians = 1,
				numberOfCivs = 8,
				civDistance = 12,
			}, new Civ3MapGenerator.Rules());
			g.Generate();
			List<int> islands = g.bodies.Where(b => b.isLand).Select(b => b.size).ToList();
			if (g.shapeAttempts < 10) {
				Assert.True(islands.Count >= 3 && islands[0] < 2 * islands[2], $"seed {seed}: islands {string.Join("/", islands)}");
				++accepted;
			}
		}
		Assert.True(accepted >= 5, $"{accepted} of 10 archipelagos had the shape");
	}
}

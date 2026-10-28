using System;
using System.Security.Cryptography;
using System.Text;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.GameData;

// Guards map generation against accidental changes: the same seed must keep
// producing exactly the same map. The expected hashes were recorded with the
// map generator before it was optimized, and updated when luxury clustering
// stopped placing luxuries on tiles that already had a resource, again
// when deserts along rivers became flood plains, and again after the
// Civ3-like terrain work (biomes, highlands, drainage-basin rivers, water
// share, resource counts and start positions, 1992cb90..00fdc2a2), and
// again when starts came to be kept the world size's distance between civs
// apart in map coordinates, never relaxed, and again (the archipelago only)
// when archipelagos came to be made of planned islands, and again (the
// pangaea only) when the shape check came to count the extra water, and
// again when starts came to be spread over the land rather than packed
// into the best of it, and again (the archipelago only) when each civ
// came to get an island of its own where there are enough, and again when
// starts came to lean to the coast and to fresh water (and, on an
// archipelago, to be on the coast), and again (the archipelago only) when
// its islands came in different sizes and a big island came to hold
// several civs.
// And again when starts stopped leaning to the coast, with only the
// seafaring civs' starts having to be coastal.
// And again when an archipelago's islands could be any size, with no
// most civs on one island.
// And again when the islands came as big ones for two or three civs,
// home islands, and empty ones to settle, and other starts on an
// archipelago got an even chance of the coast.
// And again when the poles came to be sunk to deep water, so no land
// runs along the map's top or bottom edge.
//
// Re-baselining: when a map generator change is MEANT to change the maps,
// run only this test
//   dotnet test EngineTests --filter "FullyQualifiedName~PerfAiStrategyMapDeterminismTest"
// and copy each "<landform> <seed>: <hash>" line from the test output into
// the matching InlineData below. The hashes are the same on every platform,
// so a hash that differs between machines is a determinism bug, not a
// reason to re-baseline. Say in the commit which change moved them.
public class PerfAiStrategyMapDeterminismTest {
	private readonly ITestOutputHelper output;

	public PerfAiStrategyMapDeterminismTest(ITestOutputHelper output) {
		this.output = output;
	}

	private static string HashMap(GameMap m) {
		StringBuilder sb = new();
		foreach (Tile t in m.tiles) {
			sb.Append(t.XCoordinate).Append(',').Append(t.YCoordinate).Append(':')
				.Append(t.baseTerrainType?.Key).Append('/').Append(t.overlayTerrainType?.Key).Append('/')
				.Append(t.Resource?.Key).Append('/').Append(t.ResourceKey).Append('/')
				.Append(t.continent).Append('/').Append(t.biomeRegion).Append('/')
				.Append(t.riverNorth).Append(t.riverNortheast).Append(t.riverEast).Append(t.riverSoutheast)
				.Append(t.riverSouth).Append(t.riverSouthwest).Append(t.riverWest).Append(t.riverNorthwest).Append('/')
				.Append(t.isBonusShield).Append(t.hasBarbarianCamp).Append('/')
				.Append(t.ExtraInfo?.BaseTerrainFileID).Append('.').Append(t.ExtraInfo?.BaseTerrainImageID)
				.Append(';');
		}
		sb.Append("|starts:");
		foreach (Tile t in m.startingLocations) {
			sb.Append(t.XCoordinate).Append(',').Append(t.YCoordinate).Append(';');
		}
		sb.Append("|camps:");
		foreach (Tile t in m.barbarianCamps) {
			sb.Append(t.XCoordinate).Append(',').Append(t.YCoordinate).Append(';');
		}
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
	}

	private static GameMap Generate(WorldCharacteristics.Landform landform, int seed) {
		SaveGame save = GameMode.Load(PathUtils.GameModesDir, new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = landform,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() {
				width = 100,
				height = 100,
				numberOfCivs = 8,
				distanceBetweenCivs = 12,
				techRate = 240,
				optimalNumberOfCities = 20,
			},
			mapSeed = seed,
		};
		return MapGenerator.GenerateMap(wc);
	}

	[Theory]
	[InlineData(WorldCharacteristics.Landform.Pangaea, 123456, "6CF0E57A676BA1070BFF796EE1257F712A92E5626C1C794F0BF76EE27E7B3E3E")]
	[InlineData(WorldCharacteristics.Landform.Continents, 4242, "AE8004019D4F17D629BE8B869198310B788E64F14242982C53F6D0DEC0BE2B16")]
	[InlineData(WorldCharacteristics.Landform.Archipelago, 777, "0E12373F10E341B2A12FDE1B6847DDF4FD8F4E1FB8BF2F9F1F56626E9EE36AB7")]
	public void SameSeedProducesSameMap(WorldCharacteristics.Landform landform, int seed, string expectedHash) {
		string hash = HashMap(Generate(landform, seed));
		output.WriteLine($"{landform} {seed}: {hash}");
		Assert.Equal(expectedHash, hash);
	}
}

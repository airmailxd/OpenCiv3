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
// map generator before it was optimized.
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
	[InlineData(WorldCharacteristics.Landform.Pangaea, 123456, "D80FDD98C9095A360A94C4F6467E1B060F39DF92AB09612A7BDAAABB43B481F0")]
	[InlineData(WorldCharacteristics.Landform.Continents, 4242, "2DE13FEF33E1ACE5C4F8938D00AB89DC01147A1645C6CA2D56297E8C369D15C9")]
	[InlineData(WorldCharacteristics.Landform.Archipelago, 777, "D58900D361B39283F7B1BF80B04A64722DDBD5189B4DCDC85646DA8E62FA3EA7")]
	public void SameSeedProducesSameMap(WorldCharacteristics.Landform landform, int seed, string expectedHash) {
		string hash = HashMap(Generate(landform, seed));
		output.WriteLine($"{landform} {seed}: {hash}");
		Assert.Equal(expectedHash, hash);
	}
}

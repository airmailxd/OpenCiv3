using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace EngineTests.GameData;

public class GameMapGeneratorTest {
	// Set C7_DEBUG_IMAGE_DIR to see the maps.
	public static void SaveMapsAsWaterLandPng(List<List<GameMap>> maps, string fileName) {
		int mapWidthPx = maps[0][0].numTilesWide * 2 + 1;
		int mapHeightPx = maps[0][0].numTilesTall + 1;

		Bgra32 grass = new((byte)60,(byte)190,(byte)110);
		Bgra32 coastBlue = new((byte)62, (byte)164, (byte)240);
		Bgra32 seaBlue = new((byte)88, (byte)141, (byte)195);
		Bgra32 oceanBlue = new((byte)0, (byte)105, (byte)148);
		Bgra32 hillsGreen = new((byte)2, (byte)90, (byte)60);
		Bgra32 mountainBrown = new((byte)70, (byte)65, (byte)40);
		Bgra32 tundra = new((byte)241, (byte)245, (byte)241);
		Bgra32 desert = new((byte)231, (byte)161, (byte)112);
		Bgra32 plains = new((byte)88, (byte)57, (byte)39);
		Bgra32 jungle = new((byte)0, (byte)60, (byte)0);
		Bgra32 forest = new((byte)107,(byte)142,(byte)35);
		Bgra32 marsh = new((byte)46,(byte)139,(byte)87);
		Bgra32 floodPlain = new((byte)173,(byte)216,(byte)230);

		using (Image<Bgra32> image = new(mapWidthPx * maps.Count, mapHeightPx * maps[0].Count)) {
			for (int i = 0; i < maps.Count; ++i) {
				for (int k = 0; k < maps[i].Count; ++k) {
					int xOffset = i * mapWidthPx;
					int yOffset = k * mapHeightPx;

					GameMap m = maps[i][k];
					foreach (Tile t in m.tiles) {
						Bgra32 color;
						if (t.overlayTerrainType.Key == "mountains" || t.overlayTerrainType.Key == "volcano") {
							color = mountainBrown;
						} else if (t.overlayTerrainType.Key == "hills") {
							color = hillsGreen;
						} else if (t.overlayTerrainType.Key == "tundra") {
							color = tundra;
						} else if (t.overlayTerrainType.Key == "desert") {
							color = desert;
						} else if (t.overlayTerrainType.Key == "plains") {
							color = plains;
						} else if (t.overlayTerrainType.Key == "forest") {
							color = forest;
						} else if (t.overlayTerrainType.Key == "jungle") {
							color = jungle;
						} else if (t.overlayTerrainType.Key == "marsh") {
							color = marsh;
						} else if (t.overlayTerrainType.Key == "ocean") {
							color = oceanBlue;
						} else if (t.overlayTerrainType.Key == "sea") {
							color = seaBlue;
						} else if (t.overlayTerrainType.Key == "coast") {
							color = coastBlue;
						} else {
							color = grass;
						}

						image[t.XCoordinate * 2 + xOffset, t.YCoordinate + yOffset] = color;
						image[t.XCoordinate * 2 + 1 + xOffset, t.YCoordinate + yOffset] = color;

						if (t.XCoordinate > 0) {
							image[t.XCoordinate * 2 - 1 + xOffset, t.YCoordinate + yOffset] = color;
						}
						if (t.XCoordinate + 1 < m.numTilesWide) {
							image[t.XCoordinate * 2 + 2 + xOffset, t.YCoordinate + yOffset] = color;
						}

						if (t.YCoordinate > 0) {
							Bgra32 old = image[t.XCoordinate * 2 + xOffset, t.YCoordinate + yOffset - 1];

							image[t.XCoordinate * 2 + xOffset, t.YCoordinate + yOffset - 1] = Average(old, color);
							image[t.XCoordinate * 2 + 1 + xOffset, t.YCoordinate + yOffset - 1] = Average(old, color);
						}
					}
				}
			}

			DebugImages.Save(image, fileName);
		}
	}

	private static Bgra32 Average(Bgra32 a, Bgra32 c) {
		int r = (int)Math.Sqrt((Math.Pow(a.R, 2) + Math.Pow(c.R, 2)) / 2);
		int g = (int)Math.Sqrt((Math.Pow(a.G, 2) + Math.Pow(c.G, 2)) / 2);
		int b = (int)Math.Sqrt((Math.Pow(a.B, 2) + Math.Pow(c.B, 2)) / 2);

		return new Bgra32((byte)r, (byte)g, (byte)b);
	}

	private static readonly HashSet<string> WaterTerrains = ["coast", "sea", "ocean"];

	private static readonly WorldCharacteristics.OceanCoverage[] Oceans = {
		WorldCharacteristics.OceanCoverage.Percent_60,
		WorldCharacteristics.OceanCoverage.Percent_70,
		WorldCharacteristics.OceanCoverage.Percent_80,
	};

	private static List<TerrainType> TerrainTypes() {
		List<TerrainType> terrainTypes = new();
		terrainTypes.Add(new TerrainType() { Key = "grassland" });
		terrainTypes.Add(new TerrainType() { Key = "plains" });
		terrainTypes.Add(new TerrainType() { Key = "desert" });
		terrainTypes.Add(new TerrainType() { Key = "tundra" });
		terrainTypes.Add(new TerrainType() { Key = "flood plain" });
		terrainTypes.Add(new TerrainType() { Key = "coast" });
		terrainTypes.Add(new TerrainType() { Key = "sea" });
		terrainTypes.Add(new TerrainType() { Key = "ocean" });
		terrainTypes.Add(new TerrainType() { Key = "forest" });
		terrainTypes.Add(new TerrainType() { Key = "jungle" });
		terrainTypes.Add(new TerrainType() { Key = "marsh" });
		terrainTypes.Add(new TerrainType() { Key = "hills" });
		terrainTypes.Add(new TerrainType() { Key = "volcano" });
		terrainTypes.Add(new TerrainType() { Key = "mountains" });
		return terrainTypes;
	}

	private static GameMap Generate(WorldCharacteristics.Landform landform, WorldCharacteristics.OceanCoverage ocean, int seed) {
		return MapGenerator.GenerateMap(new WorldCharacteristics() {
			landform = landform,
			oceanCoverage = ocean,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			worldSize = new WorldSize() { width = 100, height = 100 },
			terrainTypes = TerrainTypes(),
			mapSeed = seed,
		});
	}

	private static double LandShare(GameMap map) {
		return (double)map.tiles.Count(t => !WaterTerrains.Contains(t.baseTerrainType.Key)) / map.tiles.Count;
	}

	private static string Describe(GameMap map) {
		return string.Join(",", map.tiles.Select(t => $"{t.XCoordinate}:{t.YCoordinate}:{t.baseTerrainType.Key}:{t.overlayTerrainType.Key}"));
	}

	[Theory]
	[InlineData(WorldCharacteristics.Landform.Archipelago)]
	[InlineData(WorldCharacteristics.Landform.Continents)]
	[InlineData(WorldCharacteristics.Landform.Pangaea)]
	public void GameMapGeneration(WorldCharacteristics.Landform landform) {
		// Fixed seeds, so a failure can be reproduced.
		const int seed = 20240501;

		// One map for each ocean coverage.
		List<List<GameMap>> maps = Oceans.Select(ocean => new List<GameMap> { Generate(landform, ocean, seed) }).ToList();

		List<double> landShares = [];
		for (int i = 0; i < Oceans.Length; ++i) {
			GameMap map = maps[i][0];
			Assert.Equal(100, map.numTilesWide);
			Assert.Equal(100, map.numTilesTall);
			Assert.Equal(100 * 100 / 2, map.tiles.Count);
			Assert.All(map.tiles, t => {
				Assert.InRange(t.XCoordinate, 0, map.numTilesWide - 1);
				Assert.InRange(t.YCoordinate, 0, map.numTilesTall - 1);
				Assert.NotNull(t.baseTerrainType);
				Assert.NotNull(t.overlayTerrainType);
			});

			// About as much land as the ocean coverage leaves.
			double land = LandShare(map);
			double expected = 1 - (int)Oceans[i] / 100.0;
			Assert.True(Math.Abs(land - expected) <= 0.15, $"{landform} with {Oceans[i]}: {land:P0} land, expected about {expected:P0}");
			landShares.Add(land);
		}

		// More ocean, less land.
		for (int i = 0; i + 1 < landShares.Count; ++i) {
			Assert.True(landShares[i] > landShares[i + 1], $"{landform}: land shares {string.Join(", ", landShares.Select(l => l.ToString("P1")))}");
		}

		// The same seed makes the same map.
		Assert.Equal(Describe(maps[1][0]), Describe(Generate(landform, Oceans[1], seed)));

		SaveMapsAsWaterLandPng(maps, $"debug_{landform.ToString().ToLowerInvariant()}_maps.png");
	}

	[Theory]
	[InlineData(WorldCharacteristics.Landform.Archipelago)]
	[InlineData(WorldCharacteristics.Landform.Continents)]
	[InlineData(WorldCharacteristics.Landform.Pangaea)]
	public void DesertsAlongRiversAreFloodPlains(WorldCharacteristics.Landform landform) {
		GameMap map = Generate(landform, WorldCharacteristics.OceanCoverage.Percent_60, seed: 20240501);

		Assert.DoesNotContain(map.tiles, t => t.overlayTerrainType.IsDesert && t.BordersRiver());
		Assert.All(map.tiles.Where(t => t.overlayTerrainType.IsFloodPlain), t => {
			Assert.True(t.BordersRiver(), $"Flood plain at {t.XCoordinate},{t.YCoordinate} doesn't border a river");
			Assert.True(t.baseTerrainType.IsDesert, $"Flood plain at {t.XCoordinate},{t.YCoordinate} has base {t.baseTerrainType.Key}");
		});
		Assert.Contains(map.tiles, t => t.overlayTerrainType.IsFloodPlain);
	}

	[Theory]
	[InlineData(WorldCharacteristics.Landform.Archipelago)]
	[InlineData(WorldCharacteristics.Landform.Continents)]
	[InlineData(WorldCharacteristics.Landform.Pangaea)]
	public void GoodyHutsArePlacedApartOnLand(WorldCharacteristics.Landform landform) {
		GameMap map = Generate(landform, WorldCharacteristics.OceanCoverage.Percent_60, seed: 20240501);

		List<Tile> huts = map.tiles.Where(t => t.hasGoodyHut).ToList();
		Assert.NotEmpty(huts);
		Assert.All(huts, t => {
			Assert.True(t.IsLand());
			Assert.False(t.hasBarbarianCamp);
			Assert.DoesNotContain(t.GetTilesWithinTileSquare(2), n => n != t && (n.hasGoodyHut || n.hasBarbarianCamp));
			Assert.DoesNotContain(map.startingLocations, s => t.DistanceTo(s) < 3);
		});
	}
}

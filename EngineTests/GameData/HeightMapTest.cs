using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using EngineTests.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace EngineTests.GameData;

public class HeightMapTest {
	private const int Width = 100;
	private const int Height = 100;

	// Roughly archipelago, continents and pangaea shaped.
	private static readonly double[] Scales = [0.2, 0.05, 0.02];

	// Set C7_DEBUG_IMAGE_DIR to see the maps, which makes it easier to
	// understand how the scale parameter (and other height map parameters)
	// change things.
	public static void SaveNoiseMapAsPng(List<HeightMap> heightMaps, string fileName) {
		using Image<Bgra32> image = new(heightMaps[0].mapWidth, heightMaps[0].mapHeight * heightMaps.Count);
		for (int i = 0; i < heightMaps.Count; ++i) {
			for (int x = 0; x < heightMaps[i].mapWidth; x++) {
				for (int y = 0; y < heightMaps[i].mapHeight; y++) {
					byte grayScale = (byte)(heightMaps[i].GetHeight(x, y));
					image[x, y + i * heightMaps[0].mapHeight] = new Bgra32(grayScale, grayScale, grayScale, (byte)255);
				}
			}
		}
		DebugImages.Save(image, fileName);
	}

	private static int[] Heights(HeightMap hm) {
		List<int> heights = [];
		for (int x = 0; x < hm.mapWidth; x++) {
			for (int y = 0; y < hm.mapHeight; y++) {
				heights.Add(hm.GetHeight(x, y));
			}
		}
		return heights.ToArray();
	}

	// The average height difference between horizontally neighboring tiles:
	// lower for smoother maps.
	private static double Roughness(HeightMap hm) {
		long total = 0;
		int count = 0;
		for (int x = 0; x + 1 < hm.mapWidth; x++) {
			for (int y = 0; y < hm.mapHeight; y++) {
				total += Math.Abs(hm.GetHeight(x, y) - hm.GetHeight(x + 1, y));
				++count;
			}
		}
		return (double)total / count;
	}

	[Fact]
	public void HeightMapGeneration() {
		List<HeightMap> hms = [];
		Dictionary<double, double> roughness = [];

		foreach (double scale in Scales) {
			List<HeightMap> ofScale = [];
			for (int seed = 1; seed <= 4; ++seed) {
				HeightMap hm = new(seed: seed, width: Width, height: Height, scale: scale);
				ofScale.Add(hm);

				int[] heights = Heights(hm);
				Assert.All(heights, h => Assert.InRange(h, 0, 255));
				// Heights are normalized to fill the range.
				Assert.True(heights.Max() - heights.Min() > 128, $"scale {scale}, seed {seed}: heights only span {heights.Min()}..{heights.Max()}");

				// The same seed gives the same map.
				Assert.Equal(heights, Heights(new HeightMap(seed: seed, width: Width, height: Height, scale: scale)));

				// The sea level for a share of water gives about that share.
				foreach (int percentWater in new[] { 60, 70, 80 }) {
					int seaLevel = hm.FindSeaLevel(percentWater);
					double water = 100.0 * heights.Count(h => h < seaLevel) / heights.Length;
					Assert.InRange(water, percentWater - 5, percentWater + 5);
				}
			}

			// Different seeds give different maps.
			Assert.Equal(ofScale.Count, ofScale.Select(hm => string.Join(",", Heights(hm))).Distinct().Count());

			roughness[scale] = ofScale.Average(Roughness);
			hms.AddRange(ofScale);
		}

		// The smaller the scale, the smoother (larger featured) the map.
		for (int i = 0; i + 1 < Scales.Length; ++i) {
			Assert.True(roughness[Scales[i]] > roughness[Scales[i + 1]],
				$"scale {Scales[i]} has roughness {roughness[Scales[i]]:0.00}, scale {Scales[i + 1]} has {roughness[Scales[i + 1]]:0.00}");
		}

		SaveNoiseMapAsPng(hms, "debug_noise_map.png");
	}
}

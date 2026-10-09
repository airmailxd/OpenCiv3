using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData.Save;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class StartLocationTest {
	// Every civ must start where its capital can grow to size 3 working only
	// the tiles next to it, which its first borders cover: a city
	// center makes 2 food and each citizen eats 2, so the two best food
	// tiles in reach must make at least 3 food between them.
	[Theory]
	[InlineData(WorldCharacteristics.Landform.Archipelago)]
	[InlineData(WorldCharacteristics.Landform.Continents)]
	[InlineData(WorldCharacteristics.Landform.Pangaea)]
	public void EveryStartCanGrowToSizeThree(WorldCharacteristics.Landform landform) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		foreach (WorldCharacteristics.Temperature temperature in new[] { WorldCharacteristics.Temperature.Cool, WorldCharacteristics.Temperature.Temperate }) {
			for (int seed = 1; seed <= 4; ++seed) {
				WorldCharacteristics wc = new(save) {
					landform = landform,
					oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
					age = WorldCharacteristics.Age.Billion_4,
					climate = WorldCharacteristics.Climate.Arid,
					temperature = temperature,
					worldSize = new WorldSize() { width = 100, height = 100, numberOfCivs = 8, distanceBetweenCivs = 12 },
					mapSeed = seed,
				};
				GameMap map = MapGenerator.GenerateMap(wc);
				Player player = new() { government = wc.defaultGovernment };

				Assert.Equal(wc.worldSize.numberOfCivs, map.startingLocations.Count);
				foreach (Tile start in map.startingLocations) {
					List<int> food = start.GetTilesWithinRankDistance(1)
						.Where(t => t != start)
						.Select(t => t.FoodYield(player).yield)
						.OrderByDescending(f => f)
						.ToList();
					Assert.True(food[0] >= 1 && food[0] + food[1] >= 3,
						$"{landform} {temperature} seed {seed}: start at {start.XCoordinate},{start.YCoordinate} has best food tiles {string.Join(",", food.Take(4))}");
				}
			}
		}
	}
}

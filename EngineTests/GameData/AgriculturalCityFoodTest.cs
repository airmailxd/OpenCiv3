using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// An agricultural civ's city center makes a third food, which Despotism's
// tile penalty takes away unless the city is by fresh water
// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/).
public class AgriculturalCityFoodTest : IClassFixture<SaveGameFixture> {
	private readonly City city;
	private readonly Tile tile;

	public AgriculturalCityFoodTest(SaveGameFixture fixture) {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		player.civilization.traits.Add(Civilization.Trait.Agricultural);
		tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());

		// Away from fresh water, and a city of size 7.
		tile.riverNorth = tile.riverNortheast = tile.riverEast = tile.riverSoutheast = false;
		tile.riverSouth = tile.riverSouthwest = tile.riverWest = tile.riverNorthwest = false;
		foreach (Tile n in tile.neighbors.Values) {
			n.isFreshWater = false;
		}
		CitizenType laborer = gameData.citizenTypes.FirstOrDefault() ?? new CitizenType();
		while (city.residents.Count < 7) {
			city.residents.Add(new CityResident() { citizenType = laborer, tileWorked = Tile.NONE });
		}
		Assert.Equal("Despotism", player.government.name);
	}

	[Fact]
	public void DespotismTakesTheBonusFromACityAwayFromFreshWater() {
		Assert.Equal(2, tile.FoodYield(city).yield);
	}

	[Fact]
	public void DespotismSparesTheBonusOfACityOnARiver() {
		tile.riverNorth = true;
		Assert.Equal(3, tile.FoodYield(city).yield);
	}
}

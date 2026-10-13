using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Selling a city's improvements (see City.SellBuilding).
public class SellBuildingTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public SellBuildingTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
	}

	private Building Named(string name) {
		return gameData.Buildings.First(b => b.name == name);
	}

	[Fact]
	public void SellingRemovesTheBuildingAndPaysItsShieldCost() {
		Building temple = Named("Temple");
		city.AddBuilding(temple);
		int gold = player.gold;

		int price = CityInteractions.SellBuilding(city, temple);

		Assert.Equal(player.ShieldCost(temple), price);
		Assert.Equal(gold + price, player.gold);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == temple);
	}

	[Fact]
	public void OnlyOneSalePerCityPerTurn() {
		city.AddBuilding(Named("Temple"));
		city.AddBuilding(Named("Barracks"));
		Assert.True(city.SellBuilding(Named("Temple")) >= 0);
		Assert.NotNull(city.WhyCannotSell(Named("Barracks")));
		Assert.Equal(-1, city.SellBuilding(Named("Barracks")));

		city.HandleCityProduction(gameData);
		Assert.Null(city.WhyCannotSell(Named("Barracks")));
	}

	[Fact]
	public void ThePalaceAndWondersCantBeSold() {
		Building palace = gameData.Buildings.First(b => b.isCenterOfEmpire);
		Assert.Contains(city.constructed_buildings, cb => cb.building == palace);
		Assert.NotNull(city.WhyCannotSell(palace));

		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder());
		city.AddBuilding(wonder);
		Assert.NotNull(city.WhyCannotSell(wonder));
	}

	[Fact]
	public void ABuildingAWonderAlsoProvidesCantBeSold() {
		// The Temple of Artemis gives a temple in every city on its
		// continent, so even a temple the city built can't be sold.
		Building artemis = gameData.Buildings.First(b => b.name == "The Temple of Artemis");
		Building temple = Named("Temple");
		city.AddBuilding(temple);
		Assert.Null(city.WhyCannotSell(temple));
		city.AddBuilding(artemis);
		Assert.True(city.IsProvidedByWonders(temple));
		Assert.NotNull(city.WhyCannotSell(temple));
	}

	[Fact]
	public void AqueductsCantBeSold() {
		Building aqueduct = gameData.Buildings.First(b => b.allowsCitySize2);
		city.AddBuilding(aqueduct);
		Assert.NotNull(city.WhyCannotSell(aqueduct));
	}

	[Fact]
	public void TheSaleIsSaved() {
		city.soldBuildingThisTurn = true;
		City loaded = new SaveCity(city).ToCity(gameData.map, gameData.players, gameData.unitPrototypes,
			gameData.civilizations, gameData.Buildings, gameData.citizenTypes, gameData.Inflows);
		Assert.True(loaded.soldBuildingThisTurn);
	}
}

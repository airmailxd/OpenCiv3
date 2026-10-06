using System.Linq;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Luxuries and strategic resources that come from other civs through deals
// reach the cities connected to the capital, and exported ones leave them.
public class TradedLuxuriesTest {
	private readonly Resource wines = new() { Key = "Wines", Name = "Wines", Category = ResourceCategory.LUXURY };
	private readonly Resource furs = new() { Key = "Furs", Name = "Furs", Category = ResourceCategory.LUXURY };
	private readonly Resource iron = new() { Key = "Iron", Name = "Iron", Category = ResourceCategory.STRATEGIC };

	private C7GameData.GameData gameData;
	private Player us, them;
	private City city;

	public TradedLuxuriesTest() {
		gameData = new C7GameData.GameData() {
			gameDifficulty = new Difficulty() { NumberOfCitizensBornContent = 2 },
		};
		gameData.Resources.AddRange(new[] { wines, furs, iron });
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);

		us = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1"), government = new Government() };
		them = new() { civilization = new Civilization("Greece"), id = ID.FromString("player-2"), government = new Government() };
		gameData.players.Add(us);
		gameData.players.Add(them);
		us.playerRelationships[them.id] = new PlayerRelationship { multiTurnDeals = { MultiTurnDeal.DEFAULT_PEACE } };
		them.playerRelationships[us.id] = new PlayerRelationship { multiTurnDeals = { MultiTurnDeal.DEFAULT_PEACE } };

		city = new(new Tile(ID.None("city tile")), us, "Rome", ID.None("city"));
		us.cities.Add(city);
		CitizenType laborer = new() { IsDefaultCitizen = true };
		for (int i = 0; i < 4; ++i) {
			city.residents.Add(new CityResident() { citizenType = laborer, tileWorked = Tile.NONE });
		}
	}

	private void Trade(Resource resource, DealDetails direction) {
		DealType type = resource.Category == ResourceCategory.LUXURY ? DealType.Luxury : DealType.Resource;
		DealSubType subType = resource.Category == ResourceCategory.LUXURY ? DealSubType.LuxuryPerTurn : DealSubType.ResourcePerTurn;
		PlayerRelationship.RegisterMultiTurnDeal(us, them,
			new MultiTurnDeal(type, subType, direction, resourcePerTurn: resource.Key, turnStartDeal: gameData.turn));
	}

	private int Count(CityResident.Mood mood) => city.residents.Count(r => r.mood == mood);

	[Fact]
	public void ImportedLuxuriesReachTheCity() {
		Assert.Empty(city.GetLuxuries(gameData));

		Trade(wines, DealDetails.Inbound);

		Assert.Equal(new[] { wines }, city.GetLuxuries(gameData).Keys);
		Assert.Empty(city.GetStrategicResources(gameData));
	}

	[Fact]
	public void ImportedStrategicResourcesReachTheCity() {
		Trade(iron, DealDetails.Inbound);

		Assert.Equal(new[] { iron }, city.GetStrategicResources(gameData).Keys);
		Assert.Empty(city.GetLuxuries(gameData));
	}

	[Fact]
	public void ExportedLuxuriesAreNoLongerImportedBack() {
		Trade(wines, DealDetails.Inbound);
		Trade(wines, DealDetails.Outbound);

		Assert.Empty(city.GetLuxuries(gameData));
	}

	[Fact]
	public void ImportedLuxuriesMakeCitizensHappy() {
		city.RecalculateCitizenMoods(gameData);
		Assert.Equal(0, Count(CityResident.Mood.Happy));
		Assert.Equal(2, Count(CityResident.Mood.Content));
		Assert.Equal(2, Count(CityResident.Mood.Unhappy));

		// Each luxury makes one content citizen happy.
		Trade(wines, DealDetails.Inbound);
		Trade(furs, DealDetails.Inbound);
		city.RecalculateCitizenMoods(gameData);

		Assert.Equal(2, Count(CityResident.Mood.Happy));
		Assert.Equal(0, Count(CityResident.Mood.Content));
		Assert.Equal(2, Count(CityResident.Mood.Unhappy));
	}

	[Fact]
	public void ImportedLuxuriesOnlyReachCitiesConnectedToTheCapital() {
		// The first city counts as the capital when there's no palace, so a
		// second city with no road to it is cut off.
		City cutOff = new(new Tile(ID.None("cut off tile")), us, "Antium", ID.None("cut off"));
		us.cities.Add(cutOff);
		Trade(wines, DealDetails.Inbound);

		Assert.Equal(new[] { wines }, city.GetLuxuries(gameData).Keys);
		Assert.Empty(cutOff.GetLuxuries(gameData));
	}
}

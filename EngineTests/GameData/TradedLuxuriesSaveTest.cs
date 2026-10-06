using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Checks traded luxuries against a real Civ3 save, where England (player-3)
// gets spices from America (player-2).
public class TradedLuxuriesSaveTest : RemoteSaveLoader, IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public TradedLuxuriesSaveTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private static int HappyFaces(City city) => city.residents.Count(r => r.mood == CityResident.Mood.Happy);

	[SkippableFact]
	public async Task ImportedSpicesMakeConnectedCitiesHappier() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		(SaveGame save, Exception ex, _) = await LoadGameAndData(RemoteSaves.MultiTurnDealB);
		Assert.Null(ex);
		C7GameData.GameData gameData = save.ToGameData(fixture.behaviors);
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);

		Player england = gameData.GetPlayer(ID.FromString("player-3"));
		Player america = gameData.GetPlayer(ID.FromString("player-2"));
		Resource spices = gameData.Resources.Find(r => r.Key == "Spices");
		PlayerRelationship relationship = england.playerRelationships[america.id];
		MultiTurnDeal spiceDeal = relationship.multiTurnDeals.Single(d => d.resourcePerTurn == "Spices");

		List<City> connected = england.cities
			.Where(c => gameData.GetTradeNetwork().ConnectedToCapital(england, c)).ToList();
		Assert.NotEmpty(connected);

		Dictionary<City, int> happyWithSpices = new();
		foreach (City city in connected) {
			Assert.Contains(spices, city.GetLuxuries(gameData).Keys);
			city.RecalculateCitizenMoods(gameData);
			happyWithSpices[city] = HappyFaces(city);
		}

		relationship.multiTurnDeals.Remove(spiceDeal);
		bool anyLessHappy = false;
		foreach (City city in connected) {
			Assert.DoesNotContain(spices, city.GetLuxuries(gameData).Keys);
			city.RecalculateCitizenMoods(gameData);
			Assert.True(HappyFaces(city) <= happyWithSpices[city]);
			anyLessHappy |= HappyFaces(city) < happyWithSpices[city];
		}
		Assert.True(anyLessHappy, "Losing the spices should cost at least one city a happy face.");
	}
}

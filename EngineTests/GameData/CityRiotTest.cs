using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class CityRiotTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;

	public CityRiotTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	// A city riots when happy - unhappy < 0. Specialists take no part, even
	// one left with an unhappy mood from when it was a laborer.
	[Fact]
	public void SpecialistsDontCountTowardsRiots() {
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());

		// Specialists that give no luxuries, so the laborer stays content.
		CitizenType specialistType = gameData.citizenTypes.First(x => !x.IsDefaultCitizen && x.Luxuries == 0);
		for (int i = 0; i < 2; ++i) {
			city.AddCitizen(new CityResident {
				city = city,
				citizenType = specialistType,
				mood = CityResident.Mood.Unhappy,
			});
		}

		Assert.Equal(City.Mood.Happy, city.RecalculateCitizenMoods(gameData));
		Assert.All(city.residents.Where(r => r.citizenType.IsDefaultCitizen),
			r => Assert.Equal(CityResident.Mood.Content, r.mood));

		player.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);
		Assert.False(city.isInCivilDisorder);
	}
}

using System.Linq;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// The order in which faces change citizens' moods: born moods, then the
// buildings' and wonders' content faces, then war weariness, then happy
// faces, which make content citizens happy first and take two faces for an
// unhappy one. After justanick's worked example of a size 12 Republic at war
// (https://forums.civfanatics.com/threads/mechanics-of-happy-content-faces.701169/ #2).
public class FaceOrderTest {
	private readonly C7GameData.GameData gameData;
	private readonly Player us, enemy;
	private readonly City city;
	private readonly CitizenType laborer = new() { IsDefaultCitizen = true };

	public FaceOrderTest() {
		// "You start at 4 content and 8 unhappy citizens" (Chieftain).
		gameData = new C7GameData.GameData() {
			gameDifficulty = new Difficulty() { NumberOfCitizensBornContent = 4 },
		};
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);

		// A government with low war weariness, as a Republic's.
		us = new() {
			civilization = new Civilization("Rome"), id = ID.FromString("player-1"), isHuman = true,
			government = new Government() { warWeariness = 1 },
			scienceRate = 0, taxRate = 0, luxuryRate = 10,
		};
		enemy = new() { civilization = new Civilization("Greece"), id = ID.FromString("player-2"), government = new Government() };
		gameData.players.Add(us);
		gameData.players.Add(enemy);
		// No deals: at war.
		us.playerRelationships[enemy.id] = new PlayerRelationship();
		enemy.playerRelationships[us.id] = new PlayerRelationship();

		city = new(new Tile(ID.None("city tile")), us, "Rome", ID.None("city"));
		us.cities.Add(city);
		for (int i = 0; i < 12; ++i) {
			city.residents.Add(new CityResident() { citizenType = laborer, tileWorked = Tile.NONE });
		}
	}

	// All the city's commerce goes to luxuries, one happy face a point.
	private void SetLuxuries(int luxuries) {
		Tile tile = new(ID.None("commerce tile")) {
			overlayTerrainType = new TerrainType() { baseCommerceProduction = luxuries },
		};
		city.residents[0].tileWorked = tile;
		Assert.Equal(luxuries, city.CurrentCommerceYieldRaw().happiness);
	}

	private (int happy, int content, int unhappy) Moods() {
		city.RecalculateCitizenMoods(gameData);
		return (city.residents.Count(r => r.mood == CityResident.Mood.Happy),
			city.residents.Count(r => r.mood == CityResident.Mood.Content),
			city.residents.Count(r => r.mood == CityResident.Mood.Unhappy));
	}

	private void TempleAndMausoleum() {
		// "Temple 1 + Mausoleum 3": four content faces.
		SaveBuilding sb = new() { name = "Temple and Mausoleum", contentFacesInCity = 4 };
		city.AddBuilding(new Building(sb, new C7GameData.GameData()));
	}

	[Fact]
	public void WarWearinessComesAfterTheContentFaces() {
		TempleAndMausoleum();
		// 61-90 points is level 2, half the citizens: 6 unhappy faces.
		us.AddWarWeariness(enemy, 70);
		Assert.Equal(6, us.WarWearinessUnhappiness(city));

		// "4 content faces and 6 unhappy faces turn it into 2 content and 10
		// unhappy citizens."
		Assert.Equal((0, 2, 10), Moods());
	}

	[Fact]
	public void HappyFacesComeAfterWarWeariness() {
		TempleAndMausoleum();
		us.AddWarWeariness(enemy, 70);
		SetLuxuries(6);

		// Two faces make the two content citizens happy; the other four
		// make two unhappy citizens happy, two faces each. War weariness
		// doesn't cancel happy faces one for one (which would leave 8
		// content, 4 unhappy and none happy).
		Assert.Equal((4, 0, 8), Moods());
	}

	[Fact]
	public void AHappyFaceMakesAnUnhappyCitizenContentOnlyWhenNoneIsContent() {
		SetLuxuries(5);
		// 4 content, 8 unhappy: four faces make the content happy, and the
		// fifth an unhappy one content.
		Assert.Equal((4, 1, 7), Moods());
	}
}

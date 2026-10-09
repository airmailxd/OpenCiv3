using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class CelebrationTest {
	private readonly Rules rules = new() { MinimumPopulationForWeLoveTheKing = 3 };

	private City MakeCity(Government government, params CityResident.Mood[] moods) {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData() { rules = rules });
		Player player = new() { civilization = new Civilization(), government = government, rules = rules };
		City city = new(Tile.NONE, player, "Partyville", ID.None("city"));
		CitizenType laborer = new() { IsDefaultCitizen = true };
		foreach (CityResident.Mood mood in moods) {
			Tile farm = new(ID.None("farm")) { overlayTerrainType = new TerrainType() { baseFoodProduction = 2 } };
			city.residents.Add(new CityResident() { citizenType = laborer, mood = mood, tileWorked = farm });
		}
		return city;
	}

	[Fact]
	public void HappyCityWithNoUnhappyCitizensCelebrates() {
		City city = MakeCity(new Government(), CityResident.Mood.Happy, CityResident.Mood.Happy, CityResident.Mood.Content);
		Assert.True(city.QualifiesForCelebration(rules));
	}

	[Fact]
	public void AnUnhappyCitizenStopsTheCelebration() {
		City city = MakeCity(new Government(), CityResident.Mood.Happy, CityResident.Mood.Happy, CityResident.Mood.Unhappy);
		Assert.False(city.QualifiesForCelebration(rules));
	}

	[Fact]
	public void StarvingCitiesDoNotCelebrate() {
		City city = MakeCity(new Government(), CityResident.Mood.Happy, CityResident.Mood.Happy, CityResident.Mood.Content);
		city.residents[0].tileWorked.overlayTerrainType.baseFoodProduction = 0;
		city.residents[1].tileWorked.overlayTerrainType.baseFoodProduction = 0;
		Assert.False(city.QualifiesForCelebration(rules));
	}

	[Fact]
	public void SmallCitiesDoNotCelebrate() {
		City city = MakeCity(new Government(), CityResident.Mood.Happy, CityResident.Mood.Happy);
		Assert.False(city.QualifiesForCelebration(rules));
	}

	[Fact]
	public void MoreHappyThanContentCitizensAreNeeded() {
		City city = MakeCity(new Government(), CityResident.Mood.Happy, CityResident.Mood.Content, CityResident.Mood.Content);
		Assert.False(city.QualifiesForCelebration(rules));
	}

	// Celebrating cities waste shields at the lower celebration rate (see
	// City.CalculateCorruption and RulesReviewFixTest).
	[Fact]
	public void CelebratingCitiesWasteFewerShields() {
		City city = MakeCity(new Government(), CityResident.Mood.Happy);
		city.residents[0].tileWorked.overlayTerrainType.baseShieldProduction = 9;
		city.corruption = 0.5f;
		city.celebrationWaste = 0.25f;

		CorruptableValue normal = city.CurrentProductionYield();
		city.celebrating = true;
		CorruptableValue celebrating = city.CurrentProductionYield();

		Assert.True(celebrating.corrupt < normal.corrupt);
		Assert.Equal(normal.useful + normal.corrupt, celebrating.useful + celebrating.corrupt);
	}
}

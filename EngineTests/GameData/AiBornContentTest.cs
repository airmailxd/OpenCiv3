using System.Linq;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Civ3's AI has as many citizens born content as on Regent whatever the
// difficulty level, while the human has the level's number (see
// GameData.AiRulesDifficulty).
public class AiBornContentTest {
	private readonly C7GameData.GameData gameData;
	private readonly CitizenType laborer = new() { IsDefaultCitizen = true };

	public AiBornContentTest() {
		Difficulty regent = new() { Name = "Regent", AiCostFactor = 10, NumberOfCitizensBornContent = 2 };
		Difficulty emperor = new() { Name = "Emperor", AiCostFactor = 8, NumberOfCitizensBornContent = 1 };
		gameData = new C7GameData.GameData() {
			difficulties = [regent, emperor],
			gameDifficulty = emperor,
		};
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);
	}

	private int ContentIn(bool human, int n) {
		Player player = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-" + n), government = new Government(), isHuman = human };
		gameData.players.Add(player);
		City city = new(new Tile(ID.None("tile" + n)), player, "City" + n, ID.None("city" + n));
		player.cities.Add(city);
		for (int i = 0; i < 6; ++i) {
			city.residents.Add(new CityResident() { citizenType = laborer, tileWorked = Tile.NONE });
		}
		city.RecalculateCitizenMoods(gameData);
		return city.residents.Count(r => r.mood == CityResident.Mood.Content);
	}

	[Fact]
	public void TheAiHasRegentsContentCitizens() {
		Assert.Equal(1, ContentIn(human: true, 1));
		Assert.Equal(2, ContentIn(human: false, 2));
	}

	[Fact]
	public void WithoutARegentLevelTheAiHasTheGamesLevel() {
		gameData.difficulties.RemoveAt(0);
		Assert.Equal(1, ContentIn(human: false, 3));
	}
}

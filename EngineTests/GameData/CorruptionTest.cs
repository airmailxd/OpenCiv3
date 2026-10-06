using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class CorruptionTest {
	private static City MakeCity(int rank) {
		Player player = new() { civilization = new Civilization(), government = new Government() };
		return new City(Tile.NONE, player, "Nippur", ID.None("city")) { rankIndex = rank };
	}

	[Fact]
	public void RankCorruptionGrowsWithRank() {
		Assert.True(MakeCity(rank: 13).CalculateRankCorruption(23, 20, 0) > MakeCity(rank: 5).CalculateRankCorruption(23, 20, 0));
	}

	// A courthouse raises the optimal city number by a quarter of the map's
	// (here 20, so to 28), not by a quarter of the adjusted number. Across
	// the cities with courthouses in the Civ3 saves this matches Civ3's
	// corruption far more often.
	[Fact]
	public void CourthousesRaiseTheOptimalCityNumberByAQuarterOfTheMaps() {
		float rankCorruption = MakeCity(rank: 13).CalculateRankCorruption(23, mapOptimalCityNumber: 20, numAntiCorruptionBuildings: 1);
		Assert.Equal(13 / (2 * 28f), rankCorruption, 4);
	}
}

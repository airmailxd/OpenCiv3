using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class BankruptcyTest {
	private readonly C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty() };
	private readonly Player player;
	private readonly City city;

	public BankruptcyTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);
		player = new() {
			civilization = new Civilization(),
			government = new Government(),
			rules = new Rules(),
			taxRate = 0,
			scienceRate = 10,
			luxuryRate = 0,
		};
		city = new City(Tile.NONE, player, "Broke", ID.None("city"));
		player.cities.Add(city);
		gameData.players.Add(player);
	}

	private CityBuilding AddBuilding(string name, int maintenance, SaveBuilding.Flag? flag = null) {
		SaveBuilding sb = new() { name = name, maintenanceCost = maintenance };
		if (flag is SaveBuilding.Flag f) {
			sb.flags.Add(f);
		}
		city.AddBuilding(new Building(sb, gameData));
		return city.constructed_buildings[^1];
	}

	[Fact]
	public void TheTreasuryPaysForADeficit() {
		AddBuilding("Temple", 2);
		player.gold = 10;

		player.DoPerTurnFinanceUpdates(gameData);

		Assert.Equal(8, player.gold);
		Assert.Single(city.constructed_buildings);
	}

	[Fact]
	public void GoingBrokeLosesTheMostExpensiveImprovementAndLeavesTheSliders() {
		CityBuilding temple = AddBuilding("Temple", 1);
		CityBuilding bank = AddBuilding("Bank", 2);
		player.gold = 1;

		player.DoPerTurnFinanceUpdates(gameData);

		Assert.DoesNotContain(bank, city.constructed_buildings);
		Assert.Contains(temple, city.constructed_buildings);
		Assert.Equal(0, player.gold);
		Assert.Equal(10, player.scienceRate);
		Assert.Equal(0, player.taxRate);
	}

	[Fact]
	public void ThePalaceIsNeverLost() {
		AddBuilding("Palace", 5, SaveBuilding.Flag.IsCenterOfEmpire);

		player.DoPerTurnFinanceUpdates(gameData);

		Assert.Single(city.constructed_buildings);
		Assert.Equal(0, player.gold);
	}
}

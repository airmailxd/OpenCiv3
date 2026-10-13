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
			// Civ3's own upkeep, without the house rule that spares AI players it.
			rules = new Rules { AiBuildingMaintenancePercent = 100 },
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

	// Anarchy: "No maintenance costs or unit upkeep needed"
	// (https://civfanatics.com/civ3/civilopedia/governments/), so a broke
	// civ in anarchy keeps its buildings.
	[Fact]
	public void AnarchyChargesNoMaintenance() {
		player.government = new Government { transitionType = true, requiresMaintenance = false };
		CityBuilding temple = AddBuilding("Temple", 1);
		CityBuilding bank = AddBuilding("Bank", 2);
		player.gold = 0;

		for (int turn = 0; turn < 3; ++turn) {
			player.DoPerTurnFinanceUpdates(gameData);
		}

		Assert.Equal(0, city.MaintenanceCosts());
		Assert.Contains(temple, city.constructed_buildings);
		Assert.Contains(bank, city.constructed_buildings);
		Assert.Equal(0, player.gold);
	}

	// Saves from before the flag was imported: the transition government is
	// the one without maintenance.
	[Fact]
	public void OldSavesTreatTheTransitionGovernmentAsMaintenanceFree() {
		Assert.False(new Government { transitionType = true }.RequiresMaintenance());
		Assert.True(new Government().RequiresMaintenance());
	}
}

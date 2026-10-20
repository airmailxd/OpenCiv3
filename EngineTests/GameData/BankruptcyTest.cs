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

	private MapUnit AddUnit(bool requiresSupport = true, Civilization nationality = null) {
		MapUnit unit = new(ID.FromString($"unit-{player.units.Count + 1}")) {
			owner = player,
			unitType = new UnitPrototype { requiresSupport = requiresSupport },
			location = Tile.NONE,
			hitPointsRemaining = 1,
			nationality = nationality,
		};
		player.units.Add(unit);
		return unit;
	}

	// Returns the given numbers from Next(int) in order, then 0.
	private class ScriptedRandom(params int[] values) : System.Random {
		private readonly System.Collections.Generic.Queue<int> queue = new(values);
		public override int Next(int maxValue) => queue.Count > 0 ? queue.Dequeue() : 0;
	}

	// Units of another nationality and types without "Requires Support" are
	// free, and a player with no cities pays no upkeep (C3X's re-creation of
	// the stock count, patch_Leader_count_maintenance_free_units, and
	// "Players with no cities don't pay unit maintenance, per the original
	// game rules").
	[Fact]
	public void ForeignUnitsAndTypesWithoutSupportAreFreeAndNoCitiesPaysNothing() {
		player.government = new Government { unitCost = 1 };
		AddUnit();
		AddUnit(nationality: new Civilization("Rome"));
		AddUnit(requiresSupport: false);
		Assert.Equal(1, player.TotalUnitsAllowedUnitsAndSupportCostRaw().Item3);

		player.cities.Clear();
		Assert.Equal(0, player.TotalUnitsAllowedUnitsAndSupportCostRaw().Item3);
	}

	// The stock game pays building and unit upkeep in a random order, so
	// either a unit or an improvement may be what's given up ("the
	// randomization of whether improv or unit maintenance gets paid first",
	// C3X).
	[Theory]
	[InlineData(0, true)]
	[InlineData(1, false)]
	public void GoingBrokeGivesUpAUnitOrAnImprovementAtRandom(int roll, bool unitGoes) {
		player.government = new Government { unitCost = 1 };
		CityBuilding bank = AddBuilding("Bank", 2);
		MapUnit unit = AddUnit();
		// A free settler, so the civ isn't destroyed when the unit goes (its
		// city has no citizens here).
		AddUnit(nationality: new Civilization("Rome")).unitType.actions.Add(UnitAction.BuildCity);
		// Upkeep of 3 against a treasury of 2: losing either is enough.
		player.gold = 2;
		System.Random original = C7GameData.GameData.rng;
		try {
			C7GameData.GameData.rng = new ScriptedRandom(roll);
			player.DoPerTurnFinanceUpdates(gameData);
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.Equal(!unitGoes, player.units.Contains(unit));
		Assert.Equal(unitGoes, city.constructed_buildings.Contains(bank));
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

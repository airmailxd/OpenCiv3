using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class UnitUpgradeTest {
	private readonly C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty(), rules = new Rules() };
	private readonly Player player;
	private readonly City city;
	private readonly UnitPrototype spearman;
	private readonly UnitPrototype pikeman;

	public UnitUpgradeTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);

		Civilization civ = new() { name = "Testers" };
		player = new() { isHuman = true, civilization = civ, government = new Government(), gold = 100 };
		gameData.players.Add(player);

		spearman = new UnitPrototype() { name = "Spearman", shieldCost = 20 };
		pikeman = new UnitPrototype() { name = "Pikeman", shieldCost = 30 };
		foreach (UnitPrototype p in new[] { spearman, pikeman }) {
			p.categories.Add("Land");
			p.actions.Add(UnitAction.Upgrade);
			p.producibleBy.Add(civ);
			gameData.unitPrototypes.Add(p);
		}
		spearman.upgradesTo.Add(pikeman);

		Tile tile = new(ID.None("tile"));
		city = new City(tile, player, "Testville", ID.None("city"));
		tile.cityAtTile = city;
		player.cities.Add(city);
	}

	private MapUnit MakeSpearman() {
		MapUnit unit = spearman.GetInstance(ID.None("unit"), spearman, player, location: city.location);
		unit.experienceLevel = new ExperienceLevel("veteran", "Veteran", 4, 0, 0);
		unit.hitPointsRemaining = 4;
		return unit;
	}

	private void AddBarracks() {
		SaveBuilding barracks = new() { name = "Barracks" };
		barracks.flags.Add(SaveBuilding.Flag.VeteranGroundUnits);
		city.AddBuilding(new Building(barracks, new C7GameData.GameData()));
	}

	[Fact]
	public void UpgradingNeedsBarracks() {
		Assert.Null(MakeSpearman().GetAvailableUpgrade());
	}

	[Fact]
	public void UpgradeCostsGoldPerShieldOfDifference() {
		AddBarracks();
		MapUnit unit = MakeSpearman();

		Assert.Equal(pikeman, unit.GetAvailableUpgrade());
		Assert.Equal(30, unit.UpgradeCost(pikeman));

		Assert.True(unit.Upgrade());
		Assert.Equal(pikeman, unit.unitType);
		Assert.Equal("Veteran", unit.experienceLevel.displayName);
		Assert.Equal(70, player.gold);
	}

	[Fact]
	public void CannotUpgradeWithoutEnoughGold() {
		AddBarracks();
		player.gold = 10;
		MapUnit unit = MakeSpearman();

		Assert.False(unit.Upgrade());
		Assert.Equal(spearman, unit.unitType);
	}

	// Conquests charges 3 gold a shield of difference ("the costs have gone
	// from 2 * shield_difference to 3 * shield_difference",
	// https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/),
	// which is what its BIQ's RULE says and so what the bundled rules have.
	[Fact]
	public void ConquestsChargesThreeGoldAShield() {
		Assert.Equal(3, new Rules().UpgradeCostPerShield);
		using System.Text.Json.JsonDocument ruleset = EngineTests.Utils.JsonUtils.LoadBaseRuleset();
		Assert.Equal(3, ruleset.RootElement.GetProperty("rules").GetProperty("upgradeCostPerShield").GetInt32());
	}

	// "A city with an [Airport] can be used to upgrade air units"
	// (Conquests Civilopedia, BLDG_Airport); barracks don't do.
	[Fact]
	public void AircraftUpgradeAtAnAirport() {
		UnitPrototype biplane = new() { name = "Biplane", shieldCost = 40 };
		UnitPrototype fighter = new() { name = "Fighter", shieldCost = 50 };
		foreach (UnitPrototype p in new[] { biplane, fighter }) {
			p.categories.Add("Air");
			p.actions.Add(UnitAction.Upgrade);
			p.producibleBy.Add(player.civilization);
			gameData.unitPrototypes.Add(p);
		}
		biplane.upgradesTo.Add(fighter);
		MapUnit plane = biplane.GetInstance(ID.None("plane"), biplane, player, location: city.location);

		AddBarracks();
		Assert.Null(plane.GetAvailableUpgrade());

		SaveBuilding airport = new() { name = "Airport" };
		airport.flags.Add(SaveBuilding.Flag.VeteranAirUnits);
		city.AddBuilding(new Building(airport, new C7GameData.GameData()));
		Assert.Equal(fighter, plane.GetAvailableUpgrade());
		Assert.Equal(30, plane.UpgradeCost(fighter));
	}
}

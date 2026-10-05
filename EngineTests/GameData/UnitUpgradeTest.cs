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
}

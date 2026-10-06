using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// Leonardo's Workshop's cheaper upgrades, and the free units of wonders like
// the Statue of Zeus and Knights Templar.
public class WonderUnitsTest {
	private readonly C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty(), rules = new Rules() };
	private readonly Player player;
	private readonly City city;
	private readonly UnitPrototype spearman;
	private readonly UnitPrototype pikeman;
	private readonly UnitPrototype crusader;

	public WonderUnitsTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);
		gameData.defaultExperienceLevel = new ExperienceLevel("regular", "Regular", 3, 0, 0);
		gameData.defaultExperienceLevelKey = "regular";

		Civilization civ = new() { name = "Testers" };
		player = new() { isHuman = false, civilization = civ, government = new Government(), gold = 0 };
		gameData.players.Add(player);

		spearman = new UnitPrototype() { name = "Spearman", shieldCost = 20 };
		pikeman = new UnitPrototype() { name = "Pikeman", shieldCost = 30 };
		crusader = new UnitPrototype() { name = "Crusader", shieldCost = 40 };
		foreach (UnitPrototype p in new[] { spearman, pikeman, crusader }) {
			p.categories.Add("Land");
			p.actions.Add(UnitAction.Upgrade);
			p.producibleBy.Add(civ);
			gameData.unitPrototypes.Add(p);
		}
		spearman.upgradesTo.Add(pikeman);

		Tile tile = new(ID.None("tile"));
		city = new City(tile, player, "Testville", ID.None("city")) { capital = true };
		tile.cityAtTile = city;
		player.cities.Add(city);
	}

	private MapUnit AddSpearman(Tile location) {
		MapUnit unit = spearman.GetInstance(ID.None("unit"), spearman, player, location: location);
		unit.experienceLevel = new ExperienceLevel("veteran", "Veteran", 4, 0, 0);
		unit.hitPointsRemaining = 4;
		player.units.Add(unit);
		return unit;
	}

	private CityBuilding AddWonder(SaveBuilding sb) {
		sb.greatWonderProperties = new SaveBuilding.GreatWonderProperties();
		city.AddBuilding(new Building(sb, new C7GameData.GameData()));
		return city.constructed_buildings[^1];
	}

	private void AddLeonardo() {
		SaveBuilding leo = new() { name = "Leonardo's Workshop" };
		leo.flags.Add(SaveBuilding.Flag.CheaperUpgrades);
		AddWonder(leo);
	}

	private CityBuilding AddTemplars() {
		return AddWonder(new SaveBuilding() { name = "Knights Templar", unitProduced = "Crusader", unitFrequency = 5 });
	}

	private void AddBarracks() {
		SaveBuilding barracks = new() { name = "Barracks" };
		barracks.flags.Add(SaveBuilding.Flag.VeteranGroundUnits);
		city.AddBuilding(new Building(barracks, new C7GameData.GameData()));
	}

	[Fact]
	public void LeonardoHalvesUpgradeCost() {
		// A blank difficulty would zero an AI's shield costs.
		player.isHuman = true;
		AddBarracks();
		MapUnit unit = AddSpearman(city.location);
		// 10 shields of difference at 3 gold a shield.
		Assert.Equal(30, unit.UpgradeCost(pikeman));

		AddLeonardo();
		Assert.Equal(15, unit.UpgradeCost(pikeman));

		player.gold = 15;
		Assert.True(unit.Upgrade());
		Assert.Equal(pikeman, unit.unitType);
		Assert.Equal(0, player.gold);
	}

	[Fact]
	public void LeonardoDoesNotUpgradeUnitsByItself() {
		AddLeonardo();
		player.gold = 1000;
		MapUnit unit = AddSpearman(city.location);

		WonderUnits.DoPerTurnUpdates(player, gameData);
		Assert.Equal(spearman, unit.unitType);
		Assert.Equal(1000, player.gold);
	}

	[Fact]
	public void ObsoleteLeonardoGivesNoDiscount() {
		player.isHuman = true;
		SaveBuilding leo = new() { name = "Leonardo's Workshop" };
		leo.flags.Add(SaveBuilding.Flag.CheaperUpgrades);
		CityBuilding cb = AddWonder(leo);
		Tech automobile = new() { id = ID.FromString("tech-60") };
		cb.building.renderedObsoleteBy = automobile;
		player.knownTechs.Add(automobile.id);
		player.OnBuildingsChanged();

		MapUnit unit = AddSpearman(city.location);
		Assert.Equal(30, unit.UpgradeCost(pikeman));
	}

	[Fact]
	public void WonderProducesAUnitEveryFiveTurns() {
		AddTemplars();

		for (int turn = 1; turn <= 4; turn++) {
			WonderUnits.ProduceFreeUnits(player, gameData);
			Assert.Empty(player.units);
		}

		WonderUnits.ProduceFreeUnits(player, gameData);
		MapUnit unit = Assert.Single(player.units);
		Assert.Equal(crusader, unit.unitType);
		Assert.Equal(city.location, unit.location);

		for (int turn = 1; turn <= 5; turn++) {
			WonderUnits.ProduceFreeUnits(player, gameData);
		}
		Assert.Equal(2, player.units.Count);
	}

	[Fact]
	public void ObsoleteWonderProducesNoUnits() {
		CityBuilding cb = AddTemplars();
		Tech navigation = new() { id = ID.FromString("tech-45") };
		cb.building.renderedObsoleteBy = navigation;
		player.knownTechs.Add(navigation.id);

		for (int turn = 1; turn <= 10; turn++) {
			WonderUnits.ProduceFreeUnits(player, gameData);
		}
		Assert.Empty(player.units);
	}

	[Fact]
	public void FreeUnitCountdownIsSaved() {
		CityBuilding cb = AddTemplars();
		WonderUnits.ProduceFreeUnits(player, gameData);
		WonderUnits.ProduceFreeUnits(player, gameData);
		Assert.Equal(2, cb.turnsTowardFreeUnit);

		SaveCityBuilding saved = new(cb);
		CityBuilding loaded = saved.ToCityBuilding([cb.building], [player]);
		Assert.Equal(2, loaded.turnsTowardFreeUnit);
	}
}

using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class HealingTest {
	private readonly Player us = new() { civilization = new Civilization(), government = new Government() };
	// Barbarians are always at war with everyone.
	private readonly Player them = new() { civilization = new Civilization() { isBarbarian = true }, government = new Government() };

	public HealingTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData() {
			healRateInFriendlyField = 1,
			healRateInNeutralField = 1,
			healRateInHostileField = 0,
			healRateInCity = 2,
		});
	}

	private MapUnit MakeLandUnit() {
		MapUnit unit = new(ID.None("unit")) {
			owner = us,
			unitType = new UnitPrototype(),
			experienceLevel = new ExperienceLevel("regular", "Regular", 4, 0, 0),
		};
		unit.unitType.categories.Add("Land");
		return unit;
	}

	private static Tile TileOwnedBy(Player player) {
		Tile tile = new(ID.None("tile"));
		if (player != null) {
			tile.owningCity = new City(Tile.NONE, player, "Owner", ID.None("city"));
		}
		return tile;
	}

	[Fact]
	public void UnitsDoNotHealInEnemyTerritory() {
		Assert.Equal(0, MakeLandUnit().HealRateAt(TileOwnedBy(them)));
	}

	[Fact]
	public void UnitsHealInOwnAndNeutralTerritory() {
		Assert.Equal(1, MakeLandUnit().HealRateAt(TileOwnedBy(us)));
		Assert.Equal(1, MakeLandUnit().HealRateAt(TileOwnedBy(null)));
	}

	[Fact]
	public void BarracksHealLandUnitsCompletely() {
		Tile tile = new(ID.None("tile"));
		City city = new(tile, us, "Sparta", ID.None("city"));
		tile.cityAtTile = city;
		us.cities.Add(city);

		MapUnit unit = MakeLandUnit();
		Assert.Equal(2, unit.HealRateAt(tile));

		SaveBuilding barracks = new() { name = "Barracks" };
		barracks.flags.Add(SaveBuilding.Flag.VeteranGroundUnits);
		city.AddBuilding(new Building(barracks, new C7GameData.GameData()));
		Assert.Equal(unit.maxHitPoints, unit.HealRateAt(tile));
	}

	private (Tile, City) MakeCity() {
		Tile tile = new(ID.None("tile"));
		City city = new(tile, us, "Sparta", ID.None("city"));
		tile.cityAtTile = city;
		us.cities.Add(city);
		return (tile, city);
	}

	private MapUnit WoundedUnitAt(Tile tile) {
		MapUnit unit = MakeLandUnit();
		unit.unitType.movement = 1;
		unit.location = tile;
		tile.unitsOnTile.Add(unit);
		unit.hitPointsRemaining = 1;
		unit.movementPoints.reset(unit.MaxMovementPoints());
		return unit;
	}

	[Fact]
	public void FortifiedUnitsInACityHealTwoAtTheStartOfTheTurn() {
		(Tile tile, _) = MakeCity();
		MapUnit unit = WoundedUnitAt(tile);
		unit.Fortify();

		unit.OnBeginTurn();
		Assert.Equal(3, unit.hitPointsRemaining);
		unit.OnBeginTurn();
		Assert.Equal(unit.maxHitPoints, unit.hitPointsRemaining);
	}

	[Fact]
	public void UnitsThatMovedDoNotHeal() {
		(Tile tile, _) = MakeCity();
		MapUnit unit = WoundedUnitAt(tile);
		unit.movementPoints.onUnitMove(1);

		unit.OnBeginTurn();
		Assert.Equal(1, unit.hitPointsRemaining);
	}

	[Fact]
	public void FortifiedUnitsInABarracksCityHealFullyAtTheStartOfTheTurn() {
		(Tile tile, City city) = MakeCity();
		SaveBuilding barracks = new() { name = "Barracks" };
		barracks.flags.Add(SaveBuilding.Flag.VeteranGroundUnits);
		city.AddBuilding(new Building(barracks, new C7GameData.GameData()));
		MapUnit unit = WoundedUnitAt(tile);
		unit.Fortify();

		unit.OnBeginTurn();
		Assert.Equal(unit.maxHitPoints, unit.hitPointsRemaining);
	}
}

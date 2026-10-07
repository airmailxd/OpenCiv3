using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class PillageTest {
	private readonly TerrainImprovement road;
	private readonly TerrainImprovement railroad;
	private readonly TerrainImprovement mine = new("mine", TerrainImprovement.Layer.ResourceDevelopment);
	private readonly Tile tile = new(ID.None("tile"));

	public PillageTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);
		railroad = new("railroad", TerrainImprovement.Layer.Roads, movementCost: 0, upgradesFrom: road);
		tile.overlays.Add(road);
		tile.overlays.Add(mine);
	}

	private MapUnit MakePillager(int movement = 1, bool army = false) {
		Civilization civ = new() { name = "Vandals" };
		MapUnit unit = new(ID.None("unit")) {
			owner = new Player() { civilization = civ },
			nationality = civ,
			unitType = new UnitPrototype() { name = "Warrior", attack = 1, movement = movement, isArmy = army },
			location = tile,
		};
		unit.unitType.actions.Add(UnitAction.Pillage);
		unit.movementPoints.reset(movement);
		return unit;
	}

	[Fact]
	public void PillagingRemovesEveryImprovementAtOnce() {
		MapUnit unit = MakePillager();

		Assert.True(unit.Pillage());
		Assert.False(tile.overlays.HasImprovement(mine));
		Assert.False(tile.overlays.HasImprovement(road));
		Assert.False(unit.movementPoints.canMove);
	}

	[Fact]
	public void PillagingRemovesARailroadOnItsOwnFirst() {
		tile.overlays.Add(railroad);
		MapUnit unit = MakePillager(movement: 2, army: true);

		Assert.True(unit.Pillage());
		Assert.False(tile.overlays.HasImprovement(railroad));
		Assert.True(tile.overlays.HasImprovement(road));
		Assert.True(tile.overlays.HasImprovement(mine));

		Assert.True(unit.Pillage());
		Assert.False(tile.overlays.HasImprovement(road));
		Assert.False(tile.overlays.HasImprovement(mine));
	}

	[Fact]
	public void PillagingCostsOneMovementPoint() {
		MapUnit unit = MakePillager(movement: 3);

		Assert.True(unit.Pillage());
		Assert.Equal(2, unit.movementPoints.remaining);
	}

	[Fact]
	public void OnlyArmiesPillageMoreThanOncePerTurn() {
		tile.overlays.Add(railroad);
		MapUnit unit = MakePillager(movement: 2);

		Assert.True(unit.Pillage());
		Assert.False(unit.CanPillage());
		Assert.True(tile.overlays.HasImprovement(road));
	}

	[Fact]
	public void UnitsWithoutThePillageActionCannotPillage() {
		MapUnit unit = MakePillager();
		unit.unitType.actions.Remove(UnitAction.Pillage);

		Assert.False(unit.Pillage());
		Assert.True(tile.overlays.HasImprovement(mine));
	}
}

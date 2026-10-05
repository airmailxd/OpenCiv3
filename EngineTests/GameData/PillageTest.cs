using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class PillageTest {
	private readonly TerrainImprovement road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);
	private readonly TerrainImprovement mine = new("mine", TerrainImprovement.Layer.ResourceDevelopment);
	private readonly Tile tile = new(ID.None("tile"));

	public PillageTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		tile.overlays.Add(road);
		tile.overlays.Add(mine);
	}

	private MapUnit MakePillager() {
		Civilization civ = new() { name = "Vandals" };
		MapUnit unit = new(ID.None("unit")) {
			owner = new Player() { civilization = civ },
			nationality = civ,
			unitType = new UnitPrototype() { name = "Warrior", attack = 1, movement = 1 },
			location = tile,
		};
		unit.unitType.actions.Add(UnitAction.Pillage);
		unit.movementPoints.reset(1);
		return unit;
	}

	[Fact]
	public void PillagingRemovesTheMineBeforeTheRoad() {
		MapUnit unit = MakePillager();

		Assert.True(unit.Pillage());
		Assert.False(tile.overlays.HasImprovement(mine));
		Assert.True(tile.overlays.HasImprovement(road));
		Assert.False(unit.movementPoints.canMove);
	}

	[Fact]
	public void UnitsWithoutThePillageActionCannotPillage() {
		MapUnit unit = MakePillager();
		unit.unitType.actions.Remove(UnitAction.Pillage);

		Assert.False(unit.Pillage());
		Assert.True(tile.overlays.HasImprovement(mine));
	}
}

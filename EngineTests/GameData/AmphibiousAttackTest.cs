using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class AmphibiousAttackTest : MapBase {
	private readonly Tile sea;
	private readonly Tile shore;
	private readonly Player us = new() { civilization = new Civilization(), government = new Government() };

	public AmphibiousAttackTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		gameMap.numTilesWide = 100;
		gameMap.numTilesTall = 100;

		InitilizeStartTile(MakeCoastTile(), new TileLocation(50, 50));
		sea = startTile;
		shore = AddNeighborsAndUpdateMap(sea, MakePlainsTile(), TileDirection.EAST);

		// Barbarians are at war with everyone.
		Player barbarians = new() { civilization = new Civilization() { isBarbarian = true } };
		MapUnit defender = new(ID.None("defender")) { owner = barbarians, unitType = new UnitPrototype() { defense = 1 } };
		defender.unitType.categories.Add("Land");
		defender.location = shore;
		shore.unitsOnTile.Add(defender);
	}

	private MapUnit MakeAttackerAtSea(bool amphibious) {
		MapUnit unit = new(ID.None("attacker")) {
			owner = us,
			unitType = new UnitPrototype() { attack = 2, isAmphibious = amphibious },
		};
		unit.unitType.categories.Add("Land");
		unit.location = sea;
		return unit;
	}

	[Fact]
	public void OrdinaryUnitsCannotAttackFromShips() {
		Assert.False(MakeAttackerAtSea(amphibious: false).CanEnter(shore));
	}

	[Fact]
	public void AmphibiousUnitsCanAttackFromShips() {
		Assert.True(MakeAttackerAtSea(amphibious: true).CanEnter(shore, out MapUnit.Intent intent));
		Assert.Equal(MapUnit.Intent.Fight, intent);
	}
}

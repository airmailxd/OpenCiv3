using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class ZoneOfControlTest : MapBase {
	private readonly Tile from;
	private readonly Tile to;
	private readonly Tile watchTower;
	private readonly Player us = new() { civilization = new Civilization() };
	// Barbarians are at war with everyone.
	private readonly Player them = new() { civilization = new Civilization() { isBarbarian = true } };

	public ZoneOfControlTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		InitilizeStartTile(MakePlainsTile(), new TileLocation(50, 50));
		from = startTile;
		to = AddNeighborsAndUpdateMap(from, MakePlainsTile(), TileDirection.EAST);
		// A tile north-east of `from` and north-west of `to`.
		watchTower = AddNeighborsAndUpdateMap(from, MakePlainsTile(), TileDirection.NORTHEAST);
		AddNeighborsAndUpdateMap(to, watchTower, TileDirection.NORTHWEST);
	}

	private MapUnit MakeUnit(Player owner, Tile location, bool zoc) {
		MapUnit unit = new(ID.None("unit")) {
			owner = owner,
			unitType = new UnitPrototype() { attack = 3, defense = 1, hasZoneOfControl = zoc },
			location = location,
		};
		unit.unitType.categories.Add("Land");
		location.unitsOnTile.Add(unit);
		return unit;
	}

	[Fact]
	public void EnemyZoneOfControlUnitNextToBothTilesAttacks() {
		MapUnit cavalry = MakeUnit(them, watchTower, zoc: true);
		MapUnit mover = MakeUnit(us, from, zoc: false);

		Assert.Equal([cavalry], mover.FindZoneOfControlAttackers(from, to));
	}

	[Fact]
	public void UnitsWithoutZoneOfControlDoNotAttack() {
		MakeUnit(them, watchTower, zoc: false);
		MapUnit mover = MakeUnit(us, from, zoc: false);

		Assert.Empty(mover.FindZoneOfControlAttackers(from, to));
	}

	[Fact]
	public void FriendlyUnitsDoNotAttack() {
		MakeUnit(us, watchTower, zoc: true);
		MapUnit mover = MakeUnit(us, from, zoc: false);

		Assert.Empty(mover.FindZoneOfControlAttackers(from, to));
	}
}

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

	// "Armies now have the zone of control and blitz ability" (Civ3
	// readme, 1.17f). An army has no attack of its own; its members fight.
	[Fact]
	public void ArmiesWithMembersExertZoneOfControl() {
		MapUnit army = new(ID.None("army")) {
			owner = them,
			unitType = new UnitPrototype() { attack = 0, defense = 0, capacity = 3, isArmy = true, hasZoneOfControl = true },
			location = watchTower,
		};
		army.unitType.categories.Add("Land");
		watchTower.unitsOnTile.Add(army);
		MapUnit mover = MakeUnit(us, from, zoc: false);

		// An empty army can't fight, so it can't take the shot either.
		Assert.Empty(mover.FindZoneOfControlAttackers(from, to));

		MapUnit member = MakeUnit(them, watchTower, zoc: false);
		member.hitPointsRemaining = 3;
		member.loadedOnUnitId = army.id;

		Assert.Equal([army], mover.FindZoneOfControlAttackers(from, to));
	}

	// Units in a fortress "have a zone of control and fire at passing enemy
	// units" (Conquests Civilopedia, GCON_Fortresses), and a barricade is a
	// fortress too (GCON_Barricades).
	[Theory]
	[InlineData(Tile.TileOverlays.FORTRESS)]
	[InlineData(Tile.TileOverlays.BARRICADE)]
	public void UnitsInAFortressExertZoneOfControl(string holding) {
		MapUnit guard = MakeUnit(them, watchTower, zoc: false);
		MapUnit mover = MakeUnit(us, from, zoc: false);
		Assert.Empty(mover.FindZoneOfControlAttackers(from, to));

		watchTower.overlays.Add(new TerrainImprovement(holding, TerrainImprovement.Layer.Holdings));

		Assert.Equal([guard], mover.FindZoneOfControlAttackers(from, to));
	}
	// "All units in a settlement (size 1 to 6) with a city wall automatically
	// have a control zone." (Ozymandias, quoting civ-wiki.de,
	// https://forums.civfanatics.com/threads/zones-of-control.682034/);
	// "Notably this effect is lost when the town becomes a city(size 7+)."
	// (justanick, https://forums.civfanatics.com/threads/coastal-forts-and-bombardment.690690/, #5).
	[Fact]
	public void UnitsInAWalledTownExertZoneOfControlUntilItIsACity() {
		C7GameData.GameData gameData = C7Engine.EngineStorage.gameData;
		gameData.rules = new Rules() { MaximumLevel1CitySize = 6 };
		City town = new(watchTower, them, "Walled Town", ID.None("city"));
		watchTower.cityAtTile = town;
		SaveBuilding walls = new() { name = "Walls" };
		walls.flags.Add(SaveBuilding.Flag.ProvidesWalls);
		walls.flags.Add(SaveBuilding.Flag.CanOnlyBeBuiltInTowns);
		for (int i = 0; i < 6; i++) {
			town.residents.Add(new CityResident() { city = town });
		}
		MapUnit guard = MakeUnit(them, watchTower, zoc: false);
		MapUnit mover = MakeUnit(us, from, zoc: false);
		Assert.Empty(mover.FindZoneOfControlAttackers(from, to));

		town.AddBuilding(new Building(walls, gameData));
		Assert.Equal([guard], mover.FindZoneOfControlAttackers(from, to));

		town.residents.Add(new CityResident() { city = town });
		Assert.Empty(mover.FindZoneOfControlAttackers(from, to));
	}
}

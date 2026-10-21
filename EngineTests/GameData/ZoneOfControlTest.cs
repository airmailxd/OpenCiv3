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

	// In the base game a land unit inside a transport still exerts its zone
	// of control, and air units have none: the C3X mod offers
	// "not-from-inside: Land units may not exert zone of control while
	// inside a transport" and "aerial: Air units can exert zone of control"
	// as changes
	// (https://github.com/maxpetul/C3X/blob/master/default.c3x_config.ini).
	[Fact]
	public void ALandUnitAboardATransportExertsZoneOfControl() {
		MapUnit transport = new(ID.None("transport")) {
			owner = them,
			unitType = new UnitPrototype() { attack = 0, defense = 1, capacity = 2 },
			location = watchTower,
		};
		transport.unitType.categories.Add("Sea");
		watchTower.unitsOnTile.Add(transport);
		MapUnit cavalry = MakeUnit(them, watchTower, zoc: true);
		cavalry.loadedOnUnitId = transport.id;
		MapUnit mover = MakeUnit(us, from, zoc: false);

		Assert.Equal([cavalry], mover.FindZoneOfControlAttackers(from, to));
	}

	// "The game will always select the strongest available unit to try to
	// do damage." (Flintlock, https://forums.civfanatics.com/threads/zones-of-control.682034/, #3).
	[Fact]
	public void OnlyTheStrongestUnitOnATileFires() {
		MakeUnit(them, watchTower, zoc: true);
		MapUnit strong = MakeUnit(them, watchTower, zoc: true);
		strong.unitType.attack = 6;
		MapUnit mover = MakeUnit(us, from, zoc: false);

		Assert.Equal([strong], mover.FindZoneOfControlAttackers(from, to));
	}

	// "The formula for the chance to do damage by ZoC is: 1 - (D*16 / (D*16
	// + A))", with "strength 160 against defence 8" giving 55.6% and
	// strength 8 giving 5.9% (Flintlock, C3X thread post-16537815).
	[Fact]
	public void TheHitChanceIsFlintlocksFormula() {
		Assert.Equal(0.556, MapUnit.ZoneOfControlHitChance(160, 8), 3);
		Assert.Equal(0.059, MapUnit.ZoneOfControlHitChance(8, 8), 3);
		Assert.Equal(0, MapUnit.ZoneOfControlHitChance(0, 8));

		// The higher of attack and bombard.
		MapUnit guard = MakeUnit(them, watchTower, zoc: true);
		guard.unitType.bombard = 8;
		Assert.Equal(8, guard.ZoneOfControlStrength());
	}

	[Fact]
	public void AirUnitsHaveNoZoneOfControl() {
		MapUnit fighter = MakeUnit(them, watchTower, zoc: true);
		fighter.unitType.categories.Clear();
		fighter.unitType.categories.Add("Air");
		MapUnit plane = MakeUnit(us, from, zoc: false);
		plane.unitType.categories.Clear();
		plane.unitType.categories.Add("Air");

		Assert.Empty(plane.FindZoneOfControlAttackers(from, to));
	}
}

using C7Engine;
using C7GameData;
using C7GameData.AIData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.UnitAI;

// A settler takes a spare soldier close by as its escort, and with none
// sets off alone rather than waiting; and soldiers are kept near home for
// escorts rather than all exploring (owner's decision, 2026-10-12).
public sealed class SettlerEscortTest : MapBase {
	private readonly Player player;
	private readonly Tile home;
	private readonly Tile near;
	private readonly Tile far;

	public SettlerEscortTest() {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1));
		InitilizeStartTile(MakePlainsTile(), new TileLocation(50, 50));
		home = startTile;
		near = AddNeighborsAndUpdateMap(home, MakePlainsTile(), TileDirection.EAST);
		far = MakePlainsTile();
		far.XCoordinate = 70;
		far.YCoordinate = 50;
		far.map = gameMap;

		player = MakeTestPlayer(new() { home, near, far });
		player.isHuman = false;
		City city = new(home, player, "Home", ID.None(""));
		home.cityAtTile = city;
		player.cities.Add(city);
	}

	private MapUnit Place(MapUnit unit, Tile tile) {
		unit.owner = player;
		unit.nationality = player.civilization;
		unit.location = tile;
		unit.hitPointsRemaining = 1;
		tile.unitsOnTile.Add(unit);
		player.units.Add(unit);
		return unit;
	}

	private MapUnit Soldier(Tile tile) {
		MapUnit unit = MakeLandUnit();
		unit.unitType.name = "Warrior";
		unit.unitType.attack = 1;
		unit.unitType.defense = 1;
		return Place(unit, tile);
	}

	private (MapUnit, SettlerAI) Settler(Tile tile) {
		MapUnit unit = MakeLandUnit();
		unit.unitType.name = "Settler";
		unit.unitType.actions.Add(UnitAction.BuildCity);
		Place(unit, tile);
		SettlerAI ai = new(new SettlerAIData { goal = SettlerAIData.SettlerGoal.BUILD_CITY, destination = far });
		unit.currentAI = ai;
		return (unit, ai);
	}

	[Fact]
	public void ItTakesASpareSoldierNearby() {
		Soldier(home);
		MapUnit spare = Soldier(near);
		(MapUnit settler, SettlerAI ai) = Settler(home);
		Assert.True(ai.TryToTakeEscort(settler, player));
		Assert.Equal(spare, ai.data.escort);
		Assert.True(spare.currentAI is EscortAI escort && escort.data.unitToEscort == settler);
	}

	[Fact]
	public void ItDoesntTakeACitysOnlyDefender() {
		Soldier(home);
		(MapUnit settler, SettlerAI ai) = Settler(home);
		Assert.False(ai.TryToTakeEscort(settler, player));
		Assert.Null(ai.data.escort);
	}

	[Fact]
	public void ItDoesntTakeASoldierFarAway() {
		Soldier(home);
		Soldier(far);
		(MapUnit settler, SettlerAI ai) = Settler(home);
		Assert.False(ai.TryToTakeEscort(settler, player));
	}

	[Fact]
	public void SoldiersAreKeptHomeForEscorts() {
		// The capital's guard, and a settler being built: one soldier is
		// kept for it, and one more while expanding early.
		Soldier(home);
		player.cities[0].itemBeingProduced = new UnitPrototype { name = "Settler", actions = { UnitAction.BuildCity } };
		MapUnit second = Soldier(home);
		Assert.True(PlayerAI.KeepForEscorts(second, player));
		Soldier(home);
		MapUnit fourth = Soldier(home);
		Assert.False(PlayerAI.KeepForEscorts(fourth, player));
	}
}

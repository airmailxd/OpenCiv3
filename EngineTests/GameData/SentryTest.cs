using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class SentryTest : MapBase {
	private readonly Tile here;
	private readonly Tile nextDoor;
	private readonly Player us = new() { civilization = new Civilization() { name = "Us" } };

	public SentryTest() {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData());
		InitilizeStartTile(MakePlainsTile(), new TileLocation(50, 50));
		here = startTile;
		nextDoor = AddNeighborsAndUpdateMap(here, MakePlainsTile(), TileDirection.EAST);
		AddNeighborsAndUpdateMap(nextDoor, here, TileDirection.WEST);
	}

	private static MapUnit Place(Player owner, Tile tile) {
		MapUnit unit = new(ID.None("unit")) {
			owner = owner,
			nationality = owner.civilization,
			unitType = new UnitPrototype() { name = "Warrior", movement = 1 },
			location = tile,
		};
		unit.unitType.actions.Add(UnitAction.Sentry);
		tile.unitsOnTile.Add(unit);
		return unit;
	}

	[Fact]
	public void SentriedUnitsAreBusy() {
		MapUnit unit = Place(us, here);
		unit.Sentry(enemyOnly: false);
		Assert.True(unit.IsBusy());

		unit.Wake();
		Assert.False(unit.IsBusy());
	}

	[Fact]
	public void SentryWakesWhenAForeignUnitIsNextToIt() {
		MapUnit unit = Place(us, here);
		unit.Sentry(enemyOnly: false);
		Place(new Player() { civilization = new Civilization() { name = "Them" } }, nextDoor);

		unit.OnBeginTurn(skipTurn: true);
		Assert.False(unit.isSentried);
	}

	[Fact]
	public void SentryKeepsSleepingNextToOwnUnits() {
		MapUnit unit = Place(us, here);
		unit.Sentry(enemyOnly: false);
		Place(us, nextDoor);

		unit.OnBeginTurn(skipTurn: true);
		Assert.True(unit.isSentried);
	}

	[Fact]
	public void EnemyOnlySentryIgnoresCivsAtPeace() {
		Player neighbor = new() { civilization = new Civilization() { name = "Friends" } };
		C7GameData.GameData gameData = new();
		gameData.players.Add(us);
		gameData.players.Add(neighbor);
		us.id = ID.FromString("player-1");
		neighbor.id = ID.FromString("player-2");
		us.playerRelationships[neighbor.id] = new PlayerRelationship();
		neighbor.playerRelationships[us.id] = new PlayerRelationship();
		PlayerRelationship.RegisterMultiTurnDeal(us, neighbor, MultiTurnDeal.DEFAULT_PEACE);

		MapUnit unit = Place(us, here);
		unit.Sentry(enemyOnly: true);
		Place(neighbor, nextDoor);

		unit.OnBeginTurn(skipTurn: true);
		Assert.True(unit.isSentried);
	}
}

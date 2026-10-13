using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// "[Helicopters] are essentially air transports that can transport units to
// any square within their operational range, regardless of enemy units.
// Helicopters may carry foot soldiers only" (Conquests Civilopedia,
// PRTO_Helicopter). They drop their passengers and stay at their base (see
// MapUnit_Air.cs).
public class HelicopterTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public HelicopterTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		us = civs[0];
		them = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Prototype(string name) => gameData.unitPrototypes.Single(p => p.name == name);

	private MapUnit Spawn(Player owner, string prototype, Tile tile) => gameData.SpawnUnit(owner, Prototype(prototype), tile);

	private City home;
	private MapUnit helicopter;

	private void SetUp() {
		home = CityInteractions.BuildCity(us.units.First(u => u.unitType.isSettler).location, us, us.GetNextCityName());
		helicopter = Spawn(us, "Helicopter", home.location);
	}

	private Tile DropZone(int distance) {
		return gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable()
			&& home.location.DistanceTo(t) == distance && t.unitsOnTile.Count == 0);
	}

	private MapUnit Board(string prototype) {
		MapUnit unit = Spawn(us, prototype, home.location);
		unit.LoadOntoTransportHere();
		return unit;
	}

	[Fact]
	public void HelicoptersCarryFootSoldiersOnly() {
		SetUp();
		MapUnit infantry = Board("Infantry");
		MapUnit tank = Board("Tank");

		Assert.True(infantry.IsLoadedIn(helicopter));
		Assert.False(tank.IsLoaded());
	}

	[Fact]
	public void AHelicopterDropsItsPassengersWithinItsRangeAndStaysHome() {
		SetUp();
		MapUnit first = Board("Infantry");
		MapUnit second = Board("Marine");
		Assert.True(second.IsLoadedIn(helicopter));
		Tile zone = DropZone(helicopter.OperationalRange());

		Assert.Contains(UnitAction.Airdrop, helicopter.GetAvailableActions());
		Assert.True(helicopter.CanAirdropTo(zone));
		Assert.False(helicopter.CanAirdropTo(DropZone(helicopter.OperationalRange() + 1)));

		helicopter.Airdrop(zone);

		foreach (MapUnit passenger in new[] { first, second }) {
			Assert.Same(zone, passenger.location);
			Assert.False(passenger.IsLoaded());
			Assert.False(passenger.movementPoints.canMove);
		}
		Assert.Same(home.location, helicopter.location);
		Assert.False(helicopter.movementPoints.canMove);
		Assert.Equal(0, helicopter.PassengerCount());
	}

	[Fact]
	public void AnEmptyHelicopterHasNothingToDrop() {
		SetUp();
		Assert.False(helicopter.CanAirdropFrom());
		Assert.DoesNotContain(UnitAction.Airdrop, helicopter.GetAvailableActions());
	}

	[Fact]
	public void AHelicopterDoesntDropOntoForeignUnitsOrCities() {
		SetUp();
		Board("Infantry");
		Tile occupied = DropZone(3);
		Spawn(them, "Warrior", occupied);
		Assert.False(helicopter.CanAirdropTo(occupied));

		Tile sea = gameData.map.tiles.FirstOrDefault(t => t.IsWater() && home.location.DistanceTo(t) <= 3);
		if (sea != null) {
			Assert.False(helicopter.CanAirdropTo(sea));
		}
	}

	[Fact]
	public void AHelicopterOutsideABaseCantDrop() {
		SetUp();
		// As a scenario may leave one in the field.
		MapUnit stranded = Spawn(us, "Helicopter", DropZone(2));
		MapUnit infantry = Spawn(us, "Infantry", stranded.location);
		infantry.LoadOntoTransportHere();
		Assert.True(infantry.IsLoadedIn(stranded));
		Assert.False(stranded.IsAtAirBase());
		Assert.False(stranded.CanAirdropFrom());
	}
}

using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Airlifts (see MapUnit_Airlift.cs): "A city with an [Airport] can be used
// to ... airlift land units" (Conquests Civilopedia, BLDG_Airport), one unit
// a turn from each airport, arriving with no movement left (CKS,
// https://forums.civfanatics.com/threads/air-transport.662163/).
public class AirliftTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public AirliftTest(SaveGameFixture fixture) {
		this.fixture = fixture;
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

	private Building Airport => gameData.Buildings.Single(b => b.name == "Airport");

	private City FoundCity(Player player, Tile tile = null, bool airport = true) {
		tile ??= player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		if (airport) {
			city.AddBuilding(Airport);
		}
		return city;
	}

	private Tile EmptyLandAt(Tile from, int distance) {
		return gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && !t.hasBarbarianCamp && from.DistanceTo(t) == distance
			&& t.unitsOnTile.Count == 0 && !t.IsImpassable() && t.neighbors.Values.All(n => !n.HasCity()));
	}

	[Fact]
	public void TheBiqSaysWhichUnitsCanBeAirlifted() {
		Assert.Contains(UnitAction.Airlift, Prototype("Infantry").actions);
		Assert.Contains(UnitAction.Airlift, Prototype("Tank").actions);
		Assert.Contains(UnitAction.Airlift, Prototype("Worker").actions);
		Assert.DoesNotContain(UnitAction.Airlift, Prototype("Settler").actions);
		Assert.DoesNotContain(UnitAction.Airlift, Prototype("Artillery").actions);
		Assert.DoesNotContain(UnitAction.Airlift, Prototype("Fighter").actions);
	}

	[Fact]
	public void ALandUnitFliesFromOneAirportToAnother() {
		City home = FoundCity(us);
		City far = FoundCity(us, EmptyLandAt(home.location, 12));
		City noAirport = FoundCity(us, EmptyLandAt(home.location, 6), airport: false);
		MapUnit infantry = Spawn(us, "Infantry", home.location);

		Assert.Contains(UnitAction.Airlift, infantry.GetAvailableActions());
		Assert.True(infantry.CanAirliftTo(far.location));
		Assert.False(infantry.CanAirliftTo(noAirport.location));
		Assert.False(infantry.CanAirliftTo(EmptyLandAt(home.location, 3)));

		infantry.Airlift(far.location);

		Assert.Same(far.location, infantry.location);
		// "If you send by air, the units will arrive with no movement."
		Assert.False(infantry.movementPoints.canMove);
	}

	[Fact]
	public void AUnitOutsideAnAirportCantBeAirlifted() {
		City home = FoundCity(us, airport: false);
		City far = FoundCity(us, EmptyLandAt(home.location, 8));
		MapUnit infantry = Spawn(us, "Infantry", home.location);

		Assert.False(infantry.CanAirliftFrom());
		Assert.DoesNotContain(UnitAction.Airlift, infantry.GetAvailableActions());
		Assert.False(infantry.CanAirliftTo(far.location));
	}

	[Fact]
	public void EachAirportSendsOneUnitATurn() {
		City home = FoundCity(us);
		City other = FoundCity(us, EmptyLandAt(home.location, 8));
		City third = FoundCity(us, EmptyLandAt(home.location, 16));
		MapUnit first = Spawn(us, "Infantry", home.location);
		MapUnit second = Spawn(us, "Infantry", home.location);

		first.Airlift(other.location);
		Assert.Same(other.location, first.location);

		// "The limit is one unit per turn per sending airfield or airport."
		Assert.False(second.CanAirliftFrom());
		Assert.False(second.CanAirliftTo(third.location));

		// "You can land as many units as you like each turn."
		MapUnit fromThird = Spawn(us, "Infantry", third.location);
		Assert.True(fromThird.CanAirliftTo(other.location));
		fromThird.Airlift(other.location);
		Assert.Same(other.location, fromThird.location);

		// Next turn the airport sends again.
		gameData.turn++;
		Assert.True(second.CanAirliftTo(third.location));
	}

	[Fact]
	public void ForeignAirportsAndArmiesAreLeftOut() {
		City home = FoundCity(us);
		City theirs = FoundCity(them, EmptyLandAt(home.location, 8));
		MapUnit infantry = Spawn(us, "Infantry", home.location);
		Assert.False(infantry.CanAirliftTo(theirs.location));

		// "You can't airlift armies" (CKS).
		UnitPrototype armyProto = gameData.unitPrototypes.First(p => p.isArmy);
		armyProto.actions.Add(UnitAction.Airlift);
		MapUnit army = gameData.SpawnUnit(us, armyProto, home.location);
		Assert.False(army.CanAirliftFrom());
	}

	[Fact]
	public void TheAirportsUsedThisTurnKeepThroughASave() {
		City home = FoundCity(us);
		City other = FoundCity(us, EmptyLandAt(home.location, 8));
		Spawn(us, "Infantry", home.location).Airlift(other.location);
		Assert.True(us.HasAirliftedFromThisTurn(home.location));

		C7GameData.GameData reloaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON()).ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(reloaded);
		Player loaded = reloaded.players.Single(p => p.id == us.id);
		Assert.True(loaded.HasAirliftedFromThisTurn(reloaded.map.tileAt(home.location.XCoordinate, home.location.YCoordinate)));
		Assert.False(loaded.HasAirliftedFromThisTurn(reloaded.map.tileAt(other.location.XCoordinate, other.location.YCoordinate)));
	}

	[Fact]
	public void TheAIAirliftsADefenderToAThreatenedCity() {
		us.EnsureRelationshipExists(them);
		us.DeclareWarOn(them, gameData.turn);
		City safe = FoundCity(us);
		City threatened = FoundCity(us, EmptyLandAt(safe.location, 12));
		for (int i = 0; i < 3; i++) {
			Spawn(us, "Infantry", safe.location);
		}
		MapUnit lone = Spawn(us, "Infantry", threatened.location);
		Tile near = threatened.location.neighbors.Values.First(t => t.IsLand() && !t.IsImpassable() && t.unitsOnTile.Count == 0);
		Spawn(them, "Tank", near);
		Spawn(them, "Tank", near);

		AirliftAI.PlayTurn(us);

		Assert.Equal(2, threatened.location.unitsOnTile.Count(u => u.owner == us));
		Assert.Equal(2, safe.location.unitsOnTile.Count(u => u.owner == us && u.unitType.name == "Infantry"));
		Assert.Contains(lone, threatened.location.unitsOnTile);
	}

	[Fact]
	public void AirliftHasACommandAndAButton() {
		Assert.Equal(UnitAction.Airlift, C7Action.ToUnitAction(C7Action.UnitAirlift));
		Assert.Equal("Airlift", C7Action.ToTooltip(C7Action.UnitAirlift));
		Assert.True(C7Action.IsTargetedAirMission(UnitAction.Airlift));
	}
}

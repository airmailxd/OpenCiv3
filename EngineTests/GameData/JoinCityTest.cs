using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Join City (see MapUnit_JoinCity.cs): a unit with the ability adds its
// population cost to the city it stands in and disappears (Civ3 FUN_005c2320).
// It may join a city that can grow and isn't starving (FUN_005d0670), and a
// join that would take the city past its size cap fills it to the cap.
public class JoinCityTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly City city;

	public JoinCityTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		us = gameData.players.First(p => p.isHuman && p.units.Any(u => u.unitType.isSettler));
		them = gameData.players.First(p => p != us && !p.isBarbarians);
		city = CityInteractions.BuildCity(DryLand(), us, us.GetNextCityName());
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Prototype(string name) => gameData.unitPrototypes.Single(p => p.name == name);

	// Land with no fresh water by it, far from every unit and city, so a
	// city there needs an aqueduct to grow past the first size cap, with
	// enough food around it to feed a city at the cap.
	private Tile DryLand() {
		return gameData.map.tiles.First(t => t.IsLand() && !t.IsImpassable() && t.overlayTerrainType.allowCities
			&& !t.BordersRiver() && t.neighbors.Values.All(n => n.IsLand() || !n.isFreshWater)
			&& t.GetTilesWithinRankDistance(2).Count(n => n.IsLand() && n.overlayTerrainType.baseFoodProduction >= 2) >= 12
			&& t.OwningPlayer() == null && !t.hasBarbarianCamp && !t.hasGoodyHut
			&& t.GetTilesWithinRankDistance(4).All(n => n.unitsOnTile.Count == 0 && !n.HasCity()));
	}

	// Grows the city to the given size, its new citizens working tiles.
	private void GrowTo(int size) {
		CitizenType defaultCitizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
		while (city.residents.Count < size) {
			CityResident resident = new() { citizenType = defaultCitizen, nationality = us.civilization, city = city };
			city.AddCitizen(resident);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, resident);
		}
	}

	[Fact]
	public void SettlersAndWorkersHaveTheJoinCityAbility() {
		// The BIQ's PRTO "Join City" worker action, imported from conquests.biq.
		Assert.Contains(UnitAction.JoinCity, Prototype("Settler").actions);
		Assert.Contains(UnitAction.JoinCity, Prototype("Worker").actions);
		Assert.DoesNotContain(UnitAction.JoinCity, Prototype("Warrior").actions);
	}

	[Fact]
	public void AWorkerJoinsACityForOneCitizen() {
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), city.location);
		int size = city.residents.Count;

		Assert.Contains(UnitAction.JoinCity, worker.GetAvailableActions());
		Assert.True(worker.JoinCity());

		Assert.Equal(size + 1, city.residents.Count);
		Assert.DoesNotContain(worker, us.units);
		Assert.DoesNotContain(worker, city.location.unitsOnTile);
	}

	[Fact]
	public void ASettlerJoinsACityForTwoCitizens() {
		MapUnit settler = gameData.SpawnUnit(us, Prototype("Settler"), city.location);
		int size = city.residents.Count;

		Assert.True(settler.JoinCity());

		Assert.Equal(size + 2, city.residents.Count);
		Assert.DoesNotContain(settler, us.units);
	}

	[Fact]
	public void AUnitWithoutTheAbilityCantJoin() {
		MapUnit warrior = gameData.SpawnUnit(us, Prototype("Warrior"), city.location);
		int size = city.residents.Count;

		Assert.DoesNotContain(UnitAction.JoinCity, warrior.GetAvailableActions());
		Assert.False(warrior.JoinCity());
		Assert.Equal(size, city.residents.Count);
		Assert.Contains(warrior, us.units);
	}

	[Fact]
	public void AUnitOutsideACityCantJoin() {
		Tile outside = city.location.neighbors.Values.First(t => t.IsLand() && !t.IsImpassable());
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), outside);

		Assert.False(worker.CanJoinCity());
	}

	[Fact]
	public void AUnitWithNoMovesLeftCantJoin() {
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), city.location);
		worker.movementPoints.onConsumeAll();

		Assert.False(worker.CanJoinCity());
	}

	[Fact]
	public void AUnitCantJoinAnotherCivsCity() {
		MapUnit worker = gameData.SpawnUnit(them, Prototype("Worker"), city.location);

		Assert.False(worker.CanJoinCity());
	}

	[Fact]
	public void AUnitCantJoinACityAtItsSizeCap() {
		GrowTo(gameData.rules.MaximumLevel1CitySize);
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), city.location);

		Assert.False(worker.CanJoinCity());
	}

	[Fact]
	public void AnAqueductLetsAUnitJoinPastTheFirstCap() {
		GrowTo(gameData.rules.MaximumLevel1CitySize);
		city.AddBuilding(gameData.Buildings.First(b => b.allowsCitySize2));
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), city.location);
		Assert.True(city.FoodGrowthPerTurn() >= 0, "the city starves at the cap");

		Assert.True(worker.JoinCity());
		Assert.Equal(gameData.rules.MaximumLevel1CitySize + 1, city.residents.Count);
	}

	[Fact]
	public void ASettlerJoiningOneBelowTheCapFillsTheCityToIt() {
		int cap = gameData.rules.MaximumLevel1CitySize;
		GrowTo(cap - 1);
		Assert.True(city.FoodGrowthPerTurn() >= 0, "the city starves below the cap");
		MapUnit settler = gameData.SpawnUnit(us, Prototype("Settler"), city.location);

		Assert.True(settler.JoinCity());

		// Civ3 adds each citizen only while the city can grow, so the second
		// is lost, and the settler is used up all the same.
		Assert.Equal(cap, city.residents.Count);
		Assert.DoesNotContain(settler, us.units);
	}

	[Fact]
	public void AUnitCantJoinAStarvingCity() {
		CitizenType defaultCitizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
		// Citizens who work no tile eat without adding food.
		while (city.FoodGrowthPerTurn() >= 0) {
			city.AddCitizen(new CityResident { citizenType = defaultCitizen, nationality = us.civilization, city = city });
		}
		Assert.True(city.residents.Count < gameData.rules.MaximumLevel1CitySize, "the city starves only at the size cap");
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), city.location);

		Assert.False(worker.CanJoinCity());
	}

	[Fact]
	public void ACapturedUnitsCitizensKeepItsNationality() {
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), city.location);
		worker.nationality = them.civilization;

		Assert.True(worker.JoinCity());

		// "if ordered to join a city, become foreign nationals"
		// (Civilopedia, GCON_Nationality).
		Assert.Equal(them.civilization, city.residents.Last().nationality);
	}

	[Fact]
	public void TheJoinCityOrderJoinsTheCity() {
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), city.location);
		int size = city.residents.Count;

		Assert.Equal(UnitAction.JoinCity, C7Action.ToUnitAction(C7Action.UnitJoinCity));
		Assert.Equal("Join City", C7Action.ToTooltip(C7Action.UnitJoinCity));
		ID previous = EngineStorage.activePlayerID;
		EngineStorage.activePlayerID = us.id;
		try {
			Assert.True(new MsgUnitCommand(worker.id, MsgUnitCommand.Command.JoinCity) { playerID = us.id }.process());
		} finally {
			EngineStorage.activePlayerID = previous;
		}

		Assert.Equal(size + 1, city.residents.Count);
	}
}

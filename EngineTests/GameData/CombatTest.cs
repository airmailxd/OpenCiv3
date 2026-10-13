using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class CombatTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public CombatTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	// Finds two neighboring empty land tiles, outside anyone's borders.
	private (Tile from, TileDirection dir, Tile to) FindAdjacentLand() {
		foreach (Tile from in gameData.map.tiles.Where(t => IsEmptyLand(t) && t.OwningPlayer() == null)) {
			foreach ((TileDirection dir, Tile to) in from.neighbors) {
				if (to != Tile.NONE && IsEmptyLand(to) && to.OwningPlayer() == null) {
					return (from, dir, to);
				}
			}
		}
		throw new System.Exception("No adjacent land tiles found");
	}

	// Finds three empty land tiles in a line, outside anyone's borders.
	private (Tile from, TileDirection dir, Tile to, Tile behind) FindLandInALine() {
		foreach (Tile from in gameData.map.tiles.Where(t => IsEmptyLand(t) && t.OwningPlayer() == null)) {
			foreach ((TileDirection dir, Tile to) in from.neighbors) {
				if (to == Tile.NONE || !IsEmptyLand(to) || to.OwningPlayer() != null) {
					continue;
				}
				if (to.neighbors.TryGetValue(dir, out Tile behind) && behind != Tile.NONE
					&& IsEmptyLand(behind) && behind.OwningPlayer() == null) {
					return (from, dir, to, behind);
				}
			}
		}
		throw new System.Exception("No line of land tiles found");
	}

	// Makes every random roll come out as 0, so attackers win every round
	// and every retreat roll succeeds.
	private class ZeroRandom : System.Random {
		protected override double Sample() => 0.0;
	}

	private async Task WithZeroRandom(System.Func<Task> action) {
		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ZeroRandom();
		try {
			await action();
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	private void GoToWar() {
		us.DeclareWarOn(them, gameData.turn);
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		gameData.SpawnUnit(owner, Prototype(prototype), tile);
		return tile.unitsOnTile.Last();
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp;
	}

	// A unit with a move left can board a transport on a neighbouring tile:
	// it steps onto the transport's tile, which boards it.
	[Fact]
	public void AUnitBoardsATransportOnANeighbouringTile() {
		(Tile land, Tile sea) = FindCoast();
		MapUnit galley = Spawn(us, "Galley", sea);
		MapUnit warrior = Spawn(us, "Warrior", land);
		EngineStorage.pendingMessages.Clear();
		EngineStorage.activePlayerID = us.id;
		try {
			new MsgLoadToTransport(warrior.id, galley.id) { playerID = us.id }.send();
			while (EngineStorage.HasPendingMessagesToEngine()) {
				EngineStorage.ProcessNextMessageToEngine();
			}
		} finally {
			EngineStorage.activePlayerID = null;
		}

		Assert.Equal(sea, warrior.location);
		Assert.Equal(galley.id, warrior.loadedOnUnitId);
	}

	// A unit with no moves left can't.
	[Fact]
	public void AUnitWithNoMovesLeftCantBoardANeighbouringTransport() {
		(Tile land, Tile sea) = FindCoast();
		MapUnit galley = Spawn(us, "Galley", sea);
		MapUnit warrior = Spawn(us, "Warrior", land);
		warrior.movementPoints.onUnitMove(warrior.movementPoints.remaining);
		EngineStorage.pendingMessages.Clear();
		EngineStorage.activePlayerID = us.id;
		try {
			new MsgLoadToTransport(warrior.id, galley.id) { playerID = us.id }.send();
			while (EngineStorage.HasPendingMessagesToEngine()) {
				EngineStorage.ProcessNextMessageToEngine();
			}
		} finally {
			EngineStorage.activePlayerID = null;
		}

		Assert.Equal(land, warrior.location);
		Assert.Null(warrior.loadedOnUnitId);
	}

	// An empty land tile outside anyone's borders, next to empty coastal sea.
	private (Tile land, Tile sea) FindCoast() {
		foreach (Tile land in gameData.map.tiles.Where(t => IsEmptyLand(t) && t.OwningPlayer() == null)) {
			foreach (Tile sea in land.neighbors.Values) {
				if (sea != Tile.NONE && sea.IsWater() && !sea.isFreshWater && sea.unitsOnTile.Count == 0 && sea.OwningPlayer() == null) {
					return (land, sea);
				}
			}
		}
		throw new System.Exception("No coast found");
	}

	[Fact]
	public void CargoSinksWithItsTransport() {
		Tile sea = gameData.map.tiles.First(t => t.IsWater() && !t.isFreshWater && t.unitsOnTile.Count == 0);
		MapUnit galley = Spawn(us, "Galley", sea);
		MapUnit warrior = Spawn(us, "Warrior", sea);
		warrior.loadedOnUnitId = galley.id;

		gameData.RemoveUnit(galley);

		Assert.Empty(sea.unitsOnTile);
		Assert.DoesNotContain(warrior, gameData.mapUnits);
		Assert.DoesNotContain(warrior, us.units);
	}

	[Fact]
	public async Task WorkersAreCapturedNotFought() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);
		MapUnit worker = Spawn(them, "Worker", to);
		ExperienceLevel startingLevel = warrior.experienceLevel;
		int startingHitPoints = warrior.hitPointsRemaining;

		Assert.True(await warrior.Move(dir));

		Assert.Equal(to, warrior.location);
		Assert.Equal(us, worker.owner);
		Assert.Contains(worker, us.units);
		Assert.DoesNotContain(worker, them.units);
		Assert.True(worker.IsCaptive());
		Assert.True(worker.movementPoints.canMove);
		Assert.Equal(startingLevel, warrior.experienceLevel);
		Assert.Equal(startingHitPoints, warrior.hitPointsRemaining);
	}

	[Fact]
	public async Task BeatingTheLastDefenderCapturesTheWorkersBehindIt() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit attacker = Spawn(us, "Warrior", from);
		MapUnit defender = Spawn(them, "Warrior", to);
		MapUnit worker = Spawn(them, "Worker", to);

		await WithZeroRandom(async () => Assert.True(await attacker.Move(dir)));

		Assert.DoesNotContain(defender, gameData.mapUnits);
		Assert.Equal(to, attacker.location);
		Assert.Equal(us, worker.owner);
	}

	[Fact]
	public async Task WorkersCanBeCapturedAfterAttacking() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);
		MapUnit worker = Spawn(them, "Worker", to);
		warrior.hasAttackedThisTurn = true;

		Assert.True(await warrior.Move(dir));

		Assert.Equal(to, warrior.location);
		Assert.Equal(us, worker.owner);
	}

	// Capturing the last settler of a civ with no cities destroys the civ,
	// which removes its other units, among them a worker on the same tile:
	// that one mustn't then be captured too, which would leave the captor a
	// unit on no tile.
	[Fact]
	public async Task NoWorkerIsCapturedAfterItsCivIsDestroyed() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);
		Spawn(them, "Settler", to);
		Spawn(them, "Worker", to);
		foreach (MapUnit u in them.units.Where(u => u.location != to).ToList()) {
			gameData.RemoveUnit(u);
		}
		them.cities.Clear();

		Assert.True(await warrior.Move(dir));

		Assert.True(them.defeated);
		Assert.All(us.units, u => Assert.Contains(u, u.location.unitsOnTile));
	}

	[Fact]
	public async Task CapturedSettlerBecomesTwoSlaveWorkers() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);
		MapUnit settler = Spawn(them, "Settler", to);

		Assert.True(await warrior.Move(dir));

		Assert.DoesNotContain(settler, gameData.mapUnits);
		Assert.DoesNotContain(settler, them.units);
		List<MapUnit> slaves = to.unitsOnTile.Where(u => u.unitType.isWorker).ToList();
		Assert.Equal(2, slaves.Count);
		Assert.All(slaves, s => {
			Assert.Equal(us, s.owner);
			Assert.Contains(s, us.units);
			Assert.True(s.IsCaptive());
			Assert.True(s.movementPoints.canMove);
			Assert.Equal(them.civilization.name, s.nationality.name);
		});
	}

	// Civ3 captures an undefended zero-defence unit if the captor could
	// build it, and destroys it otherwise.
	// https://forums.civfanatics.com/threads/capturing-units-with-zero-defense.631539/
	[Fact]
	public async Task ACatapultIsCapturedByACivThatCanBuildIt() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);
		MapUnit catapult = Spawn(them, "Catapult", to);
		us.knownTechs.Add(catapult.unitType.requiredTech.id);

		Assert.True(await warrior.Move(dir));

		Assert.Equal(to, warrior.location);
		Assert.Equal(us, catapult.owner);
		Assert.Contains(catapult, us.units);
		Assert.DoesNotContain(catapult, them.units);
		Assert.Contains(catapult, to.unitsOnTile);
	}

	[Fact]
	public async Task ACatapultIsDestroyedByACivThatCantBuildIt() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);
		MapUnit catapult = Spawn(them, "Catapult", to);
		us.knownTechs.Remove(catapult.unitType.requiredTech.id);

		Assert.True(await warrior.Move(dir));

		Assert.Equal(to, warrior.location);
		Assert.DoesNotContain(catapult, gameData.mapUnits);
		Assert.DoesNotContain(catapult, us.units);
		Assert.DoesNotContain(catapult, to.unitsOnTile);
	}

	// City Walls give "+50% to defensive bonus against land attacks", a
	// Coastal Fortress "+50% against naval attacks"
	// (https://civfanatics.com/civ3/civilopedia/improvements/).
	[Fact]
	public void WallsDefendAgainstLandAttacksAndCoastalFortressesAgainstShips() {
		(Tile land, Tile sea) = FindCoast();
		City city = CityInteractions.BuildCity(land, them, them.GetNextCityName());
		Building walls = gameData.Buildings.Single(b => b.name == "Walls");
		Building fortress = gameData.Buildings.Single(b => b.name == "Coastal Fortress");
		city.AddBuilding(walls);
		city.AddBuilding(fortress);
		MapUnit galley = Spawn(us, "Galley", sea);
		(Tile from, _, _) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);

		List<string> versusShip = city.GetDefenseBonuses(galley).Select(b => b.description).ToList();
		List<string> versusLand = city.GetDefenseBonuses(warrior).Select(b => b.description).ToList();

		Assert.Contains("Coastal Fortress", versusShip);
		Assert.DoesNotContain("Walls", versusShip);
		Assert.Contains("Walls", versusLand);
		Assert.DoesNotContain("Coastal Fortress", versusLand);
		Assert.Equal(0.5, city.GetDefenseBonuses(galley).Single(b => b.description == "Coastal Fortress").amount);
	}

	// Units like the Javelin Thrower have "a 33% chance" to make a unit
	// (a Worker for it) of a foe they beat attacking
	// (https://forums.civfanatics.com/threads/anyone-know-exactly-how-enslavement-works.107253/).
	[Fact]
	public async Task AJavelinThrowerCanEnslaveTheUnitItBeats() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit javelin = Spawn(us, "Javelin Thrower", from);
		MapUnit warrior = Spawn(them, "Warrior", to);
		warrior.hitPointsRemaining = 1;

		// Every roll is 0, under the 1 in 3 chance.
		await WithZeroRandom(async () => Assert.True(await javelin.Move(dir)));

		Assert.DoesNotContain(warrior, gameData.mapUnits);
		MapUnit slave = Assert.Single(us.units, u => u.unitType.name == "Worker" && u.IsCaptive());
		Assert.Equal(from, slave.location);
		Assert.True(slave.IsCaptive());
		Assert.Same(them.civilization, slave.nationality);
	}

	private class FixedRandom(double value) : System.Random {
		protected override double Sample() => value;
	}

	[Fact]
	public void EnslavementFailsTwoTimesInThree() {
		(Tile from, _, Tile to) = FindAdjacentLand();
		MapUnit javelin = Spawn(us, "Javelin Thrower", from);
		MapUnit warrior = Spawn(them, "Warrior", to);
		System.Random original = C7GameData.GameData.rng;
		try {
			C7GameData.GameData.rng = new FixedRandom(0.34);
			Assert.Null(javelin.RollToEnslave(warrior));
			C7GameData.GameData.rng = new FixedRandom(0.33);
			Assert.NotNull(javelin.RollToEnslave(warrior));
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	// "Barbarian units are not able to enslave other units"
	// (https://forums.civfanatics.com/threads/allowing-barbarians-to-enslave-units.677365/).
	[Fact]
	public void BarbariansDontEnslave() {
		Player barbarians = gameData.players.Single(p => p.isBarbarians);
		(Tile from, _, Tile to) = FindAdjacentLand();
		MapUnit javelin = Spawn(barbarians, "Javelin Thrower", from);
		MapUnit warrior = Spawn(them, "Warrior", to);
		System.Random original = C7GameData.GameData.rng;
		try {
			C7GameData.GameData.rng = new FixedRandom(0);
			Assert.Null(javelin.RollToEnslave(warrior));
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	[Fact]
	public async Task WinningAttackerPaysOnlyForTheMove() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		// A tank against a badly wounded warrior, so the attack can't fail.
		MapUnit tank = Spawn(us, "Tank", from);
		MapUnit warrior = Spawn(them, "Warrior", to);
		warrior.hitPointsRemaining = 1;
		float moveCost = TilePath.GetMovementCost(us, from, dir, to);
		float startingMovement = tank.movementPoints.remaining;

		Assert.True(await tank.Move(dir));

		Assert.Equal(to, tank.location);
		Assert.DoesNotContain(warrior, gameData.mapUnits);
		Assert.Equal(startingMovement - moveCost, tank.movementPoints.remaining, 3);
	}

	[Fact]
	public async Task DefenderRetreatsEvenWithoutMovementPoints() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to, Tile behind) = FindLandInALine();
		MapUnit warrior = Spawn(us, "Warrior", from);
		// Horsemen are fast enough to retreat from a warrior.
		MapUnit horseman = Spawn(them, "Horseman", to);
		horseman.movementPoints.onConsumeAll();

		await WithZeroRandom(async () => Assert.True(await warrior.Move(dir)));

		Assert.Equal(behind, horseman.location);
		Assert.Contains(horseman, behind.unitsOnTile);
		Assert.Contains(horseman, gameData.mapUnits);
		Assert.Equal(1, horseman.hitPointsRemaining);
	}

	[Fact]
	public async Task DefenderDoesNotRetreatIntoAnotherBattle() {
		GoToWar();
		(Tile from, TileDirection dir, Tile to, Tile behind) = FindLandInALine();
		MapUnit warrior = Spawn(us, "Warrior", from);
		MapUnit horseman = Spawn(them, "Horseman", to);
		// One of our units holds the tile the horseman would retreat to.
		MapUnit blocker = Spawn(us, "Warrior", behind);
		int blockerHitPoints = blocker.hitPointsRemaining;

		await WithZeroRandom(async () => Assert.True(await warrior.Move(dir)));

		Assert.DoesNotContain(horseman, gameData.mapUnits);
		Assert.Equal(behind, blocker.location);
		Assert.Equal(blockerHitPoints, blocker.hitPointsRemaining);
		Assert.Contains(blocker, gameData.mapUnits);
	}
}

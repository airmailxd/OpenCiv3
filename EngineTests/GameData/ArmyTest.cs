using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class ArmyTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public ArmyTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	// Returns the given rolls in order, then `rest` forever. A roll below the
	// attacker's odds is a round won by the attacker.
	private class ScriptedRandom(double[] rolls, double rest) : System.Random {
		private readonly Queue<double> queue = new(rolls);
		protected override double Sample() => queue.Count > 0 ? queue.Dequeue() : rest;
		public override double NextDouble() => Sample();
	}

	private async Task WithRandom(System.Random random, System.Func<Task> action) {
		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = random;
		try {
			await action();
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private Building BuildingNamed(string name) {
		return gameData.Buildings.Single(b => b.name == name);
	}

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		return gameData.SpawnUnit(owner, Prototype(prototype), tile);
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
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

	private Tile FindEmptyLand() {
		return gameData.map.tiles.First(t => IsEmptyLand(t) && t.OwningPlayer() == null);
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	private MapUnit SpawnArmy(Player owner, Tile tile, params string[] members) {
		MapUnit army = Spawn(owner, "Army", tile);
		foreach (string member in members) {
			MapUnit unit = Spawn(owner, member, tile);
			unit.LoadOntoTransportHere();
			Assert.True(unit.IsLoadedIn(army));
		}
		return army;
	}

	private void AssertNoOrphanedCargo(C7GameData.GameData data) {
		HashSet<ID> ids = data.mapUnits.Select(u => u.id).ToHashSet();
		Assert.All(data.mapUnits.Where(u => u.loadedOnUnitId != null), u => Assert.Contains(u.loadedOnUnitId, ids));
	}

	// Runs the action with a changed rule, restoring it afterwards. The rules
	// object is shared with the fixture, so it mustn't stay changed.
	private void WithArmyUnloading(System.Action action) {
		bool original = gameData.rules.AllowUnloadFromArmy;
		gameData.rules.AllowUnloadFromArmy = true;
		try {
			action();
		} finally {
			gameData.rules.AllowUnloadFromArmy = original;
		}
	}

	// ---- The army as a container ----

	[Fact]
	public void ArmyPrototypeComesFromTheRuleset() {
		UnitPrototype army = Prototype("Army");
		Assert.True(army.isArmy);
		Assert.Equal(3, army.capacity);
		Assert.Equal(400, army.shieldCost);
		Assert.True(army.hasRadar);
		Assert.True(army.unproducible);
		Assert.DoesNotContain(UnitAction.Unload, army.actions);

		Assert.False(gameData.rules.AllowUnloadFromArmy);
	}

	[Theory]
	[InlineData("Warrior", true)]
	[InlineData("Horseman", true)]
	[InlineData("Tank", true)]
	[InlineData("Settler", false)]
	[InlineData("Worker", false)]
	[InlineData("Catapult", false)]
	[InlineData("Trebuchet", false)]
	[InlineData("Artillery", false)]
	[InlineData("Leader", false)]
	[InlineData("Army", false)]
	[InlineData("Galley", false)]
	public void OnlyLandCombatUnitsJoinArmies(string prototype, bool canJoin) {
		Assert.Equal(canJoin, Prototype(prototype).CanJoinArmy());

		Tile tile = FindEmptyLand();
		MapUnit army = Spawn(us, "Army", tile);
		MapUnit unit = Spawn(us, prototype, tile);
		Assert.Equal(canJoin, unit.GetAvailableActions().Contains(UnitAction.Load));

		unit.LoadOntoTransportHere();
		Assert.Equal(canJoin, unit.IsLoadedIn(army));
		Assert.Equal(canJoin ? 1 : 0, army.Passengers().Count);
	}

	[Fact]
	public void ArmyHoldsThreeUnits() {
		Tile tile = FindEmptyLand();
		MapUnit army = Spawn(us, "Army", tile);
		List<MapUnit> warriors = Enumerable.Range(0, 4).Select(_ => Spawn(us, "Warrior", tile)).ToList();
		foreach (MapUnit w in warriors) {
			w.LoadOntoTransportHere();
		}

		Assert.Equal(3, army.Capacity());
		Assert.Equal(3, army.Passengers().Count);
		Assert.False(warriors[3].IsLoaded());
		Assert.DoesNotContain(UnitAction.Load, warriors[3].GetAvailableActions());
	}

	[Fact]
	public async Task WalkingOntoAnArmyDoesNotLoadTheUnit() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		Spawn(us, "Army", to);
		MapUnit warrior = Spawn(us, "Warrior", from);

		Assert.True(await warrior.Move(dir));

		Assert.Equal(to, warrior.location);
		Assert.False(warrior.IsLoaded());
	}

	[Fact]
	public async Task UnitsStillBoardShipsByMovingOntoThem() {
		foreach (Tile land in gameData.map.tiles.Where(t => IsEmptyLand(t) && t.OwningPlayer() == null)) {
			foreach ((TileDirection dir, Tile sea) in land.neighbors) {
				if (sea == Tile.NONE || !sea.IsWater() || sea.unitsOnTile.Count > 0) {
					continue;
				}
				MapUnit galley = Spawn(us, "Galley", sea);
				MapUnit warrior = Spawn(us, "Warrior", land);

				Assert.True(await warrior.Move(dir));

				Assert.Equal(sea, warrior.location);
				Assert.True(warrior.IsLoadedIn(galley));
				return;
			}
		}
		Assert.Fail("No coast found");
	}

	[Fact]
	public async Task ArmiesCannotBeUnloadedByDefault() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit army = SpawnArmy(us, from, "Warrior", "Warrior");
		MapUnit member = army.Passengers().First();

		Assert.False(army.CanTransport());
		Assert.True(army.CanCarryUnits());
		Assert.DoesNotContain(UnitAction.Unload, army.GetAvailableActions());
		Assert.Empty(member.GetAvailableActions());

		// A member can't walk out of the army, and an unload order is refused.
		Assert.False(await member.Move(dir));
		Assert.Equal(from, member.location);
		new MsgUnloadTransport(army.id).process();
		Assert.All(army.Passengers(), m => Assert.True(m.IsLoadedIn(army)));
		Assert.Equal(2, army.Passengers().Count);
	}

	[Fact]
	public void TheGameOptionAllowsUnloadingArmies() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Warrior", "Warrior");

		WithArmyUnloading(() => {
			Assert.True(army.CanTransport());
			Assert.Contains(UnitAction.Unload, army.GetAvailableActions());

			new MsgUnloadTransport(army.id).process();

			Assert.Empty(army.Passengers());
			Assert.All(tile.unitsOnTile, u => Assert.False(u.IsLoaded()));
		});
	}

	[Fact]
	public async Task MovingAnArmyMovesItsMembers() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit army = SpawnArmy(us, from, "Warrior", "Spearman");
		List<MapUnit> members = army.Passengers();

		Assert.True(await army.Move(dir));

		Assert.Equal(to, army.location);
		Assert.Empty(from.unitsOnTile);
		Assert.All(members, m => {
			Assert.Equal(to, m.location);
			Assert.Contains(m, to.unitsOnTile);
			Assert.True(m.IsLoadedIn(army));
		});
	}

	[Fact]
	public void ArmyMovesAtThePaceOfItsSlowestMember() {
		Tile tile = FindEmptyLand();
		MapUnit army = Spawn(us, "Army", tile);

		// An empty army has only its own movement, not that of a full one.
		Assert.Equal(Prototype("Army").movement, army.MaxMovementPoints());

		Spawn(us, "Horseman", tile).LoadOntoTransportHere();
		Spawn(us, "Horseman", tile).LoadOntoTransportHere();
		Assert.Equal(2, army.MaxMovementPoints());
		army.OnBeginTurn();
		Assert.Equal(2, army.movementPoints.remaining);

		Spawn(us, "Warrior", tile).LoadOntoTransportHere();
		Assert.Equal(1, army.MaxMovementPoints());
		army.OnBeginTurn();
		Assert.Equal(1, army.movementPoints.remaining);
	}

	[Fact]
	public void ArmyHitPointsAreItsMembersCombined() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Warrior", "Warrior");
		List<MapUnit> members = army.Passengers();
		members[0].hitPointsRemaining = 1;

		Assert.Equal(members.Sum(m => m.maxHitPoints), army.CompositeMaxHitPoints());
		Assert.Equal(1 + members[1].hitPointsRemaining, army.CompositeHitPoints());
	}

	[Fact]
	public void CargoDoesNotAddToTheArmysDefense() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Spearman", "Spearman", "Spearman");
		MapUnit lone = Spawn(us, "Spearman", tile);
		MapUnit attacker = Spawn(them, "Warrior", tile.neighbors.Values.First(n => n != Tile.NONE));

		// The members were fortified when they boarded, but that's their own
		// state; the army itself isn't fortified, so it gets no bonus.
		Assert.All(army.Passengers(), m => Assert.True(m.isFortified));
		Assert.False(army.isFortified);

		// The army defends with one spearman's strength, not all three.
		Assert.Equal(lone.StrengthVersus(attacker, CombatRole.Defense, null), army.StrengthVersus(attacker, CombatRole.Defense, null));
		Assert.Equal(Prototype("Spearman").defense, army.CombatBaseStrength(CombatRole.Defense));
		Assert.Equal(0, army.unitType.defense);

		// Members don't defend the tile by themselves; the army does.
		Assert.Equal(army, tile.FindTopDefender(attacker, tile.unitsOnTile.Where(u => u != lone).ToList()));
	}

	[Fact]
	public async Task EmptyArmyCannotFightAndIsDestroyedWhenAttacked() {
		us.DeclareWarOn(them, gameData.turn);
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit army = Spawn(us, "Army", from);
		MapUnit warrior = Spawn(them, "Warrior", to);

		Assert.False(army.IsCombatUnit());
		Assert.False(army.CanAttack());

		// The empty army doesn't attack...
		Assert.False(await army.Move(dir));
		Assert.Equal(from, army.location);
		Assert.Equal(warrior.maxHitPoints, warrior.hitPointsRemaining);

		// ...and has nothing to defend itself with.
		Assert.True(await warrior.Move(dir.Reversed()));
		Assert.DoesNotContain(army, gameData.mapUnits);
		Assert.Equal(from, warrior.location);
		Assert.Equal(warrior.maxHitPoints, warrior.hitPointsRemaining);
	}

	[Fact]
	public void KillingAnArmyKillsItsMembers() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Warrior", "Spearman");
		List<MapUnit> members = army.Passengers();

		gameData.RemoveUnit(army);

		Assert.Empty(tile.unitsOnTile);
		Assert.All(members, m => {
			Assert.DoesNotContain(m, gameData.mapUnits);
			Assert.DoesNotContain(m, us.units);
		});
		AssertNoOrphanedCargo(gameData);
	}

	[Fact]
	public async Task DisbandingAnArmyReleasesItsMembers() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Warrior", "Spearman");
		List<MapUnit> members = army.Passengers();

		await army.Disband();

		Assert.DoesNotContain(army, gameData.mapUnits);
		Assert.All(members, m => {
			Assert.Contains(m, gameData.mapUnits);
			Assert.False(m.IsLoaded());
		});
	}

	[Fact]
	public void NoOrphanedCargoAfterAnArmyDiesAcrossSaveAndLoad() {
		Tile tile = FindEmptyLand();
		MapUnit dead = SpawnArmy(us, tile, "Warrior", "Warrior");
		MapUnit alive = SpawnArmy(us, tile.neighbors.Values.First(n => n != Tile.NONE && IsEmptyLand(n)), "Spearman");
		gameData.RemoveUnit(dead);

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(loaded);

		AssertNoOrphanedCargo(loaded);
		MapUnit loadedArmy = loaded.GetUnit(alive.id);
		Assert.Single(loadedArmy.Passengers());
		Assert.Equal("Spearman", loadedArmy.Passengers()[0].unitType.name);
	}

	[Fact]
	public void LoadingASaveFreesUnitsLoadedOnMissingUnits() {
		Tile tile = FindEmptyLand();
		MapUnit warrior = Spawn(us, "Warrior", tile);
		SaveGame save = SaveGame.FromGameData(gameData);
		save.Units.Single(u => u.id == warrior.id).loadedOnUnitId = ID.FromString("unit-that-died-999999");

		C7GameData.GameData loaded = save.ToGameData(fixture.behaviors);

		Assert.Null(loaded.GetUnit(warrior.id).loadedOnUnitId);
		AssertNoOrphanedCargo(loaded);
	}

	[Fact]
	public void ArmySeesTwoTilesAway() {
		Tile tile = FindEmptyLand();
		Spawn(us, "Army", tile);

		List<Tile> visible = us.tileKnowledge.GetTilesVisibleToUnit(tile);

		Assert.All(tile.GetTilesWithinTileSquare(2).Where(Tile.IsTileValid), t => Assert.Contains(t, visible));
	}

	// ---- Combat ----

	[Fact]
	public void ArmyFightsWithOneMemberAtATime() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Warrior", "Swordsman", "Musketman");
		List<MapUnit> m = army.Passengers();
		MapUnit warrior = m[0], swordsman = m[1], musketman = m[2];

		// The strongest healthy member fights: the swordsman attacks, the
		// musketman defends.
		Assert.Equal(swordsman, army.Combatant(CombatRole.Attack));
		Assert.Equal(musketman, army.Combatant(CombatRole.Defense));

		// Once it's down to its last hit point the next one steps in.
		swordsman.hitPointsRemaining = 1;
		Assert.Equal(musketman, army.Combatant(CombatRole.Attack));

		// When everyone is down to one, the strongest fights on.
		warrior.hitPointsRemaining = 1;
		musketman.hitPointsRemaining = 1;
		Assert.Equal(swordsman, army.Combatant(CombatRole.Attack));

		// Between equals, a member that's already been hurt keeps fighting
		// before a fresh one is brought in.
		MapUnit pair = SpawnArmy(us, tile.neighbors.Values.First(n => n != Tile.NONE && IsEmptyLand(n)), "Warrior", "Warrior");
		MapUnit hurt = pair.Passengers()[1];
		hurt.hitPointsRemaining -= 1;
		Assert.Equal(hurt, pair.Combatant(CombatRole.Attack));
		Assert.Equal(hurt, pair.Combatant(CombatRole.Defense));
	}

	[Fact]
	public async Task ArmyCommitsOnlyTheMembersItNeeds() {
		us.DeclareWarOn(them, gameData.turn);
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit army = SpawnArmy(us, from, "Warrior", "Warrior", "Warrior");
		List<MapUnit> members = army.Passengers();
		MapUnit defender = Spawn(them, "Warrior", to);
		defender.hitPointsRemaining = 2;
		int fullHitPoints = members[0].maxHitPoints;

		// Lose the first round, then win the rest (the last roll fails the
		// promotion, so hit points don't change).
		var random = new ScriptedRandom([0.99, 0.0, 0.0, 0.0], 0.99);
		await WithRandom(random, async () => Assert.True(await army.Move(dir)));

		Assert.DoesNotContain(defender, gameData.mapUnits);
		Assert.Equal(to, army.location);
		Assert.Equal(fullHitPoints - 1, members[0].hitPointsRemaining);
		Assert.Equal(fullHitPoints, members[1].hitPointsRemaining);
		Assert.Equal(fullHitPoints, members[2].hitPointsRemaining);
		Assert.All(members, m => Assert.Equal(to, m.location));
	}

	[Fact]
	public async Task ArmyRotatesMembersByHitPointsAndDiesWithTheLast() {
		us.DeclareWarOn(them, gameData.turn);
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit army = SpawnArmy(us, from, "Warrior", "Warrior");
		List<MapUnit> members = army.Passengers();
		int fullHitPoints = members[0].maxHitPoints;
		MapUnit defender = Spawn(them, "Spearman", to);

		// Lose fullHitPoints - 1 rounds: only the first member is worn down.
		int roundsToWearDownOne = fullHitPoints - 1;
		var random = new ScriptedRandom(Enumerable.Repeat(0.99, roundsToWearDownOne).Concat([0.0, 0.99]).ToArray(), 0.0);
		// Then win against a defender with one hit point left.
		defender.hitPointsRemaining = 1;
		await WithRandom(random, async () => Assert.True(await army.Move(dir)));
		Assert.Equal(1, members[0].hitPointsRemaining);
		Assert.Equal(fullHitPoints, members[1].hitPointsRemaining);

		// Now lose every round of a new fight: the second member is worn down
		// to one, then both die, and the army with them.
		army.OnBeginTurn();
		MapUnit nextDefender = Spawn(them, "Spearman", from);
		await WithRandom(new ScriptedRandom([], 0.99), async () => Assert.False(await army.Move(dir.Reversed())));

		Assert.DoesNotContain(army, gameData.mapUnits);
		Assert.All(members, m => Assert.DoesNotContain(m, gameData.mapUnits));
		Assert.Equal(nextDefender.maxHitPoints, nextDefender.hitPointsRemaining);
		Assert.Empty(to.unitsOnTile);
		AssertNoOrphanedCargo(gameData);
	}

}

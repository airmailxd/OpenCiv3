using System.Collections.Generic;
using System.IO;
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

	// An army with the members, anywhere. Units only join an army in a
	// city (see UnitsJoinAnArmyOnlyInACity), so outside one they're put in
	// directly, as if the army had been filled in a city and moved here.
	private MapUnit SpawnArmy(Player owner, Tile tile, params string[] members) {
		MapUnit army = Spawn(owner, "Army", tile);
		foreach (string member in members) {
			MapUnit unit = Spawn(owner, member, tile);
			army.Board(unit);
			unit.isFortified = true;
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

		Assert.Equal(4, gameData.rules.CitiesNeededToSupportAnArmy);
		Assert.Equal("Army", gameData.rules.BuildArmyUnit);
		Assert.False(gameData.rules.AllowUnloadFromArmy);
	}

	[SkippableFact]
	public void ArmyDataIsImportedFromCiv3() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		// A Conquests scenario with armies and a Military Academy of its own.
		string scenarios = Path.Join(QueryCiv3.Civ3Location.GetCiv3Path(), "Conquests", "Conquests");
		string scenario = Path.Join(scenarios, "3 Fall of Rome.biq");
		SaveGame game = ImportCiv3.ImportBiq(scenario, PathUtils.defaultBicPath, modPath => {
			if (!System.OperatingSystem.IsWindows()) {
				modPath = modPath.Replace("\\conquests\\", "/Conquests/");
			}
			return Path.GetFullPath(Path.Combine(scenarios, modPath, "Text", "PediaIcons.txt"));
		});
		QueryCiv3.BiqData biq = QueryCiv3.BiqData.LoadFile(scenario);

		foreach (QueryCiv3.Biq.PRTO prto in biq.Prto.Where(p => p.Army)) {
			SaveUnitPrototype army = game.UnitPrototypes.First(p => p.name == prto.Name);
			Assert.Contains(SaveUnitPrototype.Flag.Army, army.flags);
			Assert.True(army.unproducible);
		}
		Assert.Contains(game.UnitPrototypes, p => p.flags.Contains(SaveUnitPrototype.Flag.Army));

		QueryCiv3.Biq.BLDG rawAcademy = biq.Bldg.Single(b => b.Name == "Military Academy");
		SaveBuilding academy = game.Buildings.Single(b => b.name == "Military Academy");
		Assert.Equal(rawAcademy.AllowsBuildArmy, academy.flags.Contains(SaveBuilding.Flag.AllowsBuildArmy));
		Assert.Equal(rawAcademy.RequiresVictoriousArmy, academy.flags.Contains(SaveBuilding.Flag.RequiresVictoriousArmy));
		Assert.Equal(rawAcademy.NumberOfArmiesRequired, academy.numberOfArmiesRequired);
		Assert.True(rawAcademy.AllowsBuildArmy);

		QueryCiv3.Biq.RULE rule = biq.Rule[0];
		Assert.Equal(rule.CitiesNeededToSupportAnArmy, game.Rules.CitiesNeededToSupportAnArmy);
		Assert.Equal(biq.Prto[rule.BuildArmyUnit].Name, game.Rules.BuildArmyUnit);
		Assert.False(game.Rules.AllowUnloadFromArmy);
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

		Tile tile = BuildCity(us).location;
		MapUnit army = Spawn(us, "Army", tile);
		MapUnit unit = Spawn(us, prototype, tile);
		Assert.Equal(canJoin, unit.GetAvailableActions().Contains(UnitAction.Load));

		unit.LoadOntoTransportHere();
		Assert.Equal(canJoin, unit.IsLoadedIn(army));
		Assert.Equal(canJoin ? 1 : 0, army.Passengers().Count);
	}

	[Fact]
	public void ArmyHoldsThreeUnitsAndFourWithThePentagon() {
		City city = BuildCity(us);
		Tile tile = city.location;
		MapUnit army = Spawn(us, "Army", tile);
		List<MapUnit> warriors = Enumerable.Range(0, 4).Select(_ => Spawn(us, "Warrior", tile)).ToList();
		foreach (MapUnit w in warriors) {
			w.LoadOntoTransportHere();
		}

		Assert.Equal(3, army.Capacity());
		Assert.Equal(3, army.Passengers().Count);
		Assert.False(warriors[3].IsLoaded());
		Assert.DoesNotContain(UnitAction.Load, warriors[3].GetAvailableActions());

		city.AddBuilding(BuildingNamed("The Pentagon"));

		Assert.Equal(4, army.Capacity());
		warriors[3].LoadOntoTransportHere();
		Assert.True(warriors[3].IsLoadedIn(army));
		Assert.Equal(4, army.Passengers().Count);
	}

	// "Once that army leaves the city of it's construction, you cannot add
	// or remove units from the army" (Civ3 readme). Not outside a city, nor
	// in another civ's one (see also the tests of the army's origin city
	// below).
	[Fact]
	public void UnitsJoinAnArmyOnlyInACity() {
		Tile field = FindEmptyLand();
		MapUnit army = Spawn(us, "Army", field);
		MapUnit outside = Spawn(us, "Warrior", field);
		Assert.DoesNotContain(UnitAction.Load, outside.GetAvailableActions());
		outside.LoadOntoTransportHere();
		Assert.False(outside.IsLoaded());

		Tile home = BuildCity(us).location;
		MapUnit cityArmy = Spawn(us, "Army", home);
		MapUnit inside = Spawn(us, "Warrior", home);
		Assert.Contains(UnitAction.Load, inside.GetAvailableActions());
		inside.LoadOntoTransportHere();
		Assert.True(inside.IsLoadedIn(cityArmy));

		// Not in another civ's city either.
		Tile theirs = BuildCity(them).location;
		MapUnit foreignArmy = Spawn(us, "Army", theirs);
		MapUnit visitor = Spawn(us, "Warrior", theirs);
		visitor.LoadOntoTransportHere();
		Assert.False(visitor.IsLoaded());
		Assert.Empty(army.Passengers());
		Assert.Empty(foreignArmy.Passengers());
	}

	// ---- The army's origin city (see MapUnit_ArmyOrigin.cs) ----

	// The tile next to the city that an army can step onto.
	private (TileDirection dir, Tile to) EmptyNeighbor(Tile city) {
		foreach ((TileDirection dir, Tile to) in city.neighbors) {
			if (to != Tile.NONE && IsEmptyLand(to)) {
				return (dir, to);
			}
		}
		throw new System.Exception("No empty land next to the city");
	}

	[Fact]
	public void UnitsJoinAnArmyInTheCityThatMadeIt() {
		City home = BuildCity(us);
		MapUnit army = Spawn(us, "Army", home.location);
		Assert.Equal(home.id, army.armyOriginCityId);
		Assert.False(army.hasLeftArmyOrigin);

		MapUnit warrior = Spawn(us, "Warrior", home.location);
		Assert.Contains(UnitAction.Load, warrior.GetAvailableActions());
		warrior.LoadOntoTransportHere();
		Assert.True(warrior.IsLoadedIn(army));
	}

	[Fact]
	public void ALeadersArmyRemembersTheCityItWasFormedIn() {
		City home = BuildCity(us);
		for (int i = 0; i < 3; ++i) {
			BuildCity(us);
		}
		MapUnit leader = Spawn(us, "Leader", home.location);
		Assert.True(leader.CanFormArmy());
		MapUnit army = leader.FormArmy();
		Assert.Equal(home.id, army.armyOriginCityId);
	}

	[Fact]
	public void UnitsDoNotJoinAnArmyInAnotherOfTheOwnersCities() {
		City home = BuildCity(us);
		City other = BuildCity(us);
		MapUnit army = Spawn(us, "Army", home.location);
		Assert.Equal(home.id, army.armyOriginCityId);

		// The army is put in the other city as if it had gone there.
		home.location.unitsOnTile.Remove(army);
		other.location.unitsOnTile.Add(army);
		army.location = other.location;
		Assert.True(army.hasLeftArmyOrigin);

		MapUnit warrior = Spawn(us, "Warrior", other.location);
		Assert.False(army.CanTakeMembersHere());
		Assert.DoesNotContain(UnitAction.Load, warrior.GetAvailableActions());
		warrior.LoadOntoTransportHere();
		Assert.False(warrior.IsLoaded());
		Assert.Empty(army.Passengers());
	}

	[Fact]
	public async Task UnitsDoNotJoinAnArmyThatLeftItsCityAndCameBack() {
		City home = BuildCity(us);
		MapUnit army = Spawn(us, "Army", home.location);
		MapUnit first = Spawn(us, "Warrior", home.location);
		first.LoadOntoTransportHere();
		Assert.True(first.IsLoadedIn(army));

		(TileDirection dir, Tile outside) = EmptyNeighbor(home.location);
		army.movementPoints.reset(army.MaxMovementPoints());
		Assert.True(await army.Move(dir));
		Assert.Equal(outside, army.location);
		Assert.True(army.hasLeftArmyOrigin);

		army.movementPoints.reset(army.MaxMovementPoints());
		TileDirection back = outside.neighbors.First(n => n.Value == home.location).Key;
		Assert.True(await army.Move(back));
		Assert.Equal(home.location, army.location);

		MapUnit second = Spawn(us, "Warrior", home.location);
		Assert.False(army.CanTakeMembersHere());
		second.LoadOntoTransportHere();
		Assert.False(second.IsLoaded());
		Assert.Single(army.Passengers());
	}

	[Fact]
	public void TheArmysOriginCitySurvivesSaveAndLoad() {
		City home = BuildCity(us);
		MapUnit stays = Spawn(us, "Army", home.location);
		MapUnit left = Spawn(us, "Army", home.location);
		left.hasLeftArmyOrigin = true;

		SaveGame save = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		C7GameData.GameData loaded = save.ToGameData(fixture.behaviors);

		MapUnit loadedStays = loaded.mapUnits.Single(u => u.id == stays.id);
		MapUnit loadedLeft = loaded.mapUnits.Single(u => u.id == left.id);
		Assert.Equal(home.id, loadedStays.armyOriginCityId);
		Assert.False(loadedStays.hasLeftArmyOrigin);
		Assert.True(loadedStays.CanTakeMembersHere());
		Assert.Equal(home.id, loadedLeft.armyOriginCityId);
		Assert.True(loadedLeft.hasLeftArmyOrigin);
		Assert.False(loadedLeft.CanTakeMembersHere());
	}

	// Saves made before armies remembered their origin city keep the old
	// rule: any of the owner's cities.
	[Fact]
	public void AnArmyFromAnOlderSaveJoinsInAnyOwnCity() {
		City home = BuildCity(us);
		City other = BuildCity(us);
		MapUnit army = Spawn(us, "Army", home.location);
		SaveGame save = SaveGame.FromGameData(gameData);
		SaveUnit saved = save.Units.Single(u => u.id == army.id);
		saved.armyOriginCityId = null;
		saved.hasLeftArmyOrigin = false;
		saved.currentLocation = new TileLocation(other.location);
		C7GameData.GameData loaded = save.ToGameData(fixture.behaviors);

		MapUnit loadedArmy = loaded.mapUnits.Single(u => u.id == army.id);
		Assert.Null(loadedArmy.armyOriginCityId);
		Assert.True(loadedArmy.CanTakeMembersHere());
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
		Unload(army);
		Assert.All(army.Passengers(), m => Assert.True(m.IsLoadedIn(army)));
		Assert.Equal(2, army.Passengers().Count);
	}

	// Sends an unload order as the army's owner, on their turn.
	private static void Unload(MapUnit army) {
		ID previous = EngineStorage.activePlayerID;
		EngineStorage.activePlayerID = army.owner.id;
		try {
			new MsgUnloadTransport(army.id) { playerID = army.owner.id }.process();
		} finally {
			EngineStorage.activePlayerID = previous;
		}
	}

	[Fact]
	public void TheGameOptionAllowsUnloadingArmies() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Warrior", "Warrior");

		WithArmyUnloading(() => {
			Assert.True(army.CanTransport());
			Assert.Contains(UnitAction.Unload, army.GetAvailableActions());

			Unload(army);

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
		Tile tile = BuildCity(us).location;
		MapUnit army = Spawn(us, "Army", tile);

		// An empty army has only its own movement, not that of a full one.
		Assert.Equal(Prototype("Army").movement, army.MaxMovementPoints());

		Spawn(us, "Horseman", tile).LoadOntoTransportHere();
		Spawn(us, "Horseman", tile).LoadOntoTransportHere();
		Assert.Equal(3, army.MaxMovementPoints());
		army.OnBeginTurn();
		Assert.Equal(3, army.movementPoints.remaining);

		Spawn(us, "Warrior", tile).LoadOntoTransportHere();
		Assert.Equal(2, army.MaxMovementPoints());
		army.OnBeginTurn();
		Assert.Equal(2, army.movementPoints.remaining);
	}

	[Fact]
	public void ANewArmyMovesAsFarAsItsMembersThatTurn() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Cavalry", "Cavalry", "Cavalry");

		Assert.Equal(4, army.MaxMovementPoints());
		Assert.Equal(4, army.movementPoints.remaining);
	}

	[Fact]
	public void AnArmyCantOutrunAMemberThatHasAlreadyMoved() {
		Tile tile = BuildCity(us).location;
		MapUnit army = SpawnArmy(us, tile, "Cavalry");
		MapUnit tired = Spawn(us, "Cavalry", tile);
		tired.movementPoints.onUnitMove(2);
		tired.LoadOntoTransportHere();

		Assert.Equal(1, army.movementPoints.remaining);
	}

	[Fact]
	public void AnArmyKeepsWhatItSpentWhenAMemberJoins() {
		Tile tile = BuildCity(us).location;
		MapUnit army = SpawnArmy(us, tile, "Cavalry");
		army.movementPoints.onUnitMove(1);
		Spawn(us, "Cavalry", tile).LoadOntoTransportHere();

		Assert.Equal(3, army.movementPoints.remaining);
	}

	// In Conquests armies "can pillage without expending a movement point"
	// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/),
	// and more than once a turn.
	[Fact]
	public void ArmiesPillageWithoutSpendingMovementPoints() {
		Tile tile = FindEmptyLand();
		TerrainImprovement railroad = gameData.terrainImprovements.First(i => i.layer == TerrainImprovement.Layer.Roads && i.upgradesFrom != null);
		tile.overlays.Add(railroad.upgradesFrom);
		tile.overlays.Add(railroad);
		MapUnit army = SpawnArmy(us, tile, "Cavalry", "Cavalry", "Cavalry");
		army.OnBeginTurn();
		Assert.Equal(4, army.movementPoints.remaining);

		Assert.True(army.Pillage());
		Assert.Equal(4, army.movementPoints.remaining);
		Assert.True(tile.overlays.HasImprovement(railroad.upgradesFrom));

		Assert.True(army.Pillage());
		Assert.Equal(4, army.movementPoints.remaining);
		Assert.False(tile.overlays.HasBeenImproved());

		// A unit on its own still pays a move.
		tile.overlays.Add(railroad.upgradesFrom);
		MapUnit lone = Spawn(us, "Cavalry", tile);
		lone.OnBeginTurn();
		Assert.True(lone.Pillage());
		Assert.Equal(lone.MaxMovementPoints() - 1, lone.movementPoints.remaining);
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

		// The army defends with one spearman's strength, not all three,
		// plus its share of theirs (see ArmyGetsAShareOfItsMembersStrength).
		int defense = Prototype("Spearman").defense;
		int share = 3 * defense / 6;
		Assert.Equal(defense + share, army.CombatBaseStrength(CombatRole.Defense));
		Assert.Equal(lone.StrengthVersus(attacker, CombatRole.Defense, null) * (defense + share) / defense,
			army.StrengthVersus(attacker, CombatRole.Defense, null), 6);
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

	// ---- Production and the support rule ----

	[Fact]
	public void ArmiesAreOnlyBuiltInACityWithTheMilitaryAcademy() {
		City city = BuildCity(us);
		UnitPrototype army = Prototype("Army");
		Assert.DoesNotContain(army, city.ListProductionOptions(gameData));

		city.AddBuilding(BuildingNamed("Military Academy"));

		Assert.Contains(army, city.ListProductionOptions(gameData));
		Assert.Equal(400, army.ShieldCost(us.civilization.traits, 1.0f));

		// Only the city with the academy can build armies.
		City other = BuildCity(us);
		Assert.DoesNotContain(army, other.ListProductionOptions(gameData));
	}

	[Fact]
	public void MilitaryAcademyNeedsAVictoriousArmy() {
		Building academy = BuildingNamed("Military Academy");
		Assert.True(academy.allowsBuildArmy);
		Assert.True(academy.requiresVictoriousArmy);
		City city = BuildCity(us);
		us.knownTechs.Add(academy.requiredTech.id);

		Assert.DoesNotContain(academy, city.ListProductionOptions(gameData));

		us.hasVictoriousArmy = true;
		Assert.Contains(academy, city.ListProductionOptions(gameData));

		// It's a small wonder: one per civ.
		city.AddBuilding(academy);
		Assert.DoesNotContain(academy, BuildCity(us).ListProductionOptions(gameData));
	}

	[Fact]
	public void PentagonNeedsThreeArmies() {
		Building pentagon = BuildingNamed("The Pentagon");
		Assert.True(pentagon.allowsLargerArmies);
		Assert.Equal(3, pentagon.numberOfArmiesRequired);
		City city = BuildCity(us);
		if (pentagon.requiredTech != null)
			us.knownTechs.Add(pentagon.requiredTech.id);

		Spawn(us, "Army", city.location);
		Spawn(us, "Army", city.location);
		Assert.DoesNotContain(pentagon, city.ListProductionOptions(gameData));

		Spawn(us, "Army", city.location);
		Assert.Contains(pentagon, city.ListProductionOptions(gameData));
	}

	[Fact]
	public void UnsupportedArmyKeepsItsShieldsUntilThereAreEnoughCities() {
		City city = BuildCity(us);
		city.AddBuilding(BuildingNamed("Military Academy"));
		UnitPrototype army = Prototype("Army");
		city.SetItemBeingProduced(army);
		int cost = us.ShieldCost(army);
		city.SetStoredShields(cost);

		// One city can't support an army.
		Assert.False(us.CanSupportAnotherArmy());
		Assert.Null(city.ComputeTurnProduction());
		Assert.Equal(cost, city.shieldsStored);
		Assert.Equal(0, us.ArmyCount());

		// Four cities can support one.
		while (us.RemainingCities() < 4) {
			BuildCity(us);
		}
		Assert.True(us.CanSupportAnotherArmy());
		Assert.Equal(army, city.ComputeTurnProduction());
		Assert.Equal(0, city.shieldsStored);

		// But not a second one.
		Spawn(us, "Army", city.location);
		Assert.False(us.CanSupportAnotherArmy());
	}

	[Fact]
	public void PlayerIsWarnedAboutAnUnsupportedArmy() {
		Player human = gameData.players.First(p => p.isHuman);
		City city = BuildCity(human);
		city.AddBuilding(BuildingNamed("Military Academy"));
		UnitPrototype army = Prototype("Army");
		EngineStorage.messagesToUI.Clear();

		// Choosing to build it warns, but the city builds it anyway.
		city.ChooseProduction(army);
		Assert.Equal(army, city.itemBeingProduced);
		Assert.Single(EngineStorage.messagesToUI.OfType<MsgShowMilitaryAdvisorPopup>());
		EngineStorage.messagesToUI.Clear();

		// Reaching full shields warns again, without finishing it.
		int cost = human.ShieldCost(army);
		city.SetStoredShields(cost - 1);
		Assert.True(city.CurrentProductionYield().useful >= 1);
		Assert.Null(city.ComputeTurnProduction());
		Assert.Equal(cost, city.shieldsStored);
		Assert.Single(EngineStorage.messagesToUI.OfType<MsgShowMilitaryAdvisorPopup>());
		EngineStorage.messagesToUI.Clear();
	}

	[Fact]
	public void VictoriousArmySurvivesSaveAndLoad() {
		us.hasVictoriousArmy = true;

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);

		Assert.True(loaded.GetPlayer(us.id).hasVictoriousArmy);
		Assert.False(loaded.GetPlayer(them.id).hasVictoriousArmy);
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

		// Lose the first round, then win the rest (the next roll fails the
		// promotion, so hit points don't change). Slow units make no retreat
		// roll.
		var random = new ScriptedRandom([0.99, 0.0, 0.0], 0.99);
		await WithRandom(random, async () => Assert.True(await army.Move(dir)));

		Assert.DoesNotContain(defender, gameData.mapUnits);
		Assert.Equal(to, army.location);
		Assert.Equal(fullHitPoints - 1, members[0].hitPointsRemaining);
		Assert.Equal(fullHitPoints, members[1].hitPointsRemaining);
		Assert.Equal(fullHitPoints, members[2].hitPointsRemaining);
		Assert.All(members, m => Assert.Equal(to, m.location));
		Assert.True(us.hasVictoriousArmy);
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
		Assert.False(them.hasVictoriousArmy);
	}

	[Fact]
	public async Task DefendingArmyWinsAsAVictoriousArmy() {
		us.DeclareWarOn(them, gameData.turn);
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit attacker = Spawn(us, "Warrior", from);
		MapUnit army = SpawnArmy(them, to, "Spearman");

		await WithRandom(new ScriptedRandom([], 0.99), async () => Assert.False(await attacker.Move(dir)));

		Assert.DoesNotContain(attacker, gameData.mapUnits);
		Assert.Contains(army, gameData.mapUnits);
		Assert.True(them.hasVictoriousArmy);
		Assert.False(us.hasVictoriousArmy);
	}

	// In Conquests the member fighting for an army adds the sum of the
	// members' attack (or defense) divided by 6, rounded down, or by 4 with
	// the Military Academy
	// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/).
	[Fact]
	public void ArmyGetsAShareOfItsMembersStrength() {
		Tile tile = FindEmptyLand();
		MapUnit army = SpawnArmy(us, tile, "Swordsman", "Swordsman", "Swordsman");
		int attack = Prototype("Swordsman").attack, defense = Prototype("Swordsman").defense;
		Assert.Equal(3, attack);
		Assert.Equal(2, defense);

		// 9 / 6 = 1 and 6 / 6 = 1.
		Assert.Equal(attack + 1, army.CombatBaseStrength(CombatRole.Attack));
		Assert.Equal(defense + 1, army.CombatBaseStrength(CombatRole.Defense));
		// Bombarding isn't fighting.
		Assert.Equal(0, army.ArmyStrengthBonus(CombatRole.Bombard));

		// 9 / 4 = 2 and 6 / 4 = 1.
		BuildCity(us).AddBuilding(BuildingNamed("Military Academy"));
		Assert.Equal(attack + 2, army.CombatBaseStrength(CombatRole.Attack));
		Assert.Equal(defense + 1, army.CombatBaseStrength(CombatRole.Defense));

		// A lone unit gets nothing of the sort.
		MapUnit lone = Spawn(us, "Swordsman", tile);
		Assert.Equal(attack, lone.CombatBaseStrength(CombatRole.Attack));
	}

	// "Armies now have blitz"
	// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/):
	// the Conquests BIQ gives the Army unit it.
	[Fact]
	public void ArmiesHaveBlitz() {
		MapUnit army = SpawnArmy(us, FindEmptyLand(), "Warrior");
		Assert.True(army.unitType.hasBlitz);
		army.hasAttackedThisTurn = true;
		Assert.True(army.CanAttackAgainThisTurn());
	}

	// "Armies also heal in enemy territory (one hp/unit pre-Battlefield
	// Medicine and two hp/unit after)"
	// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/).
	[Fact]
	public void ArmyMembersHealInEnemyTerritory() {
		City theirs = BuildCity(them);
		us.DeclareWarOn(them, gameData.turn);
		Tile enemyLand = theirs.location.neighbors.Values.First(t => IsEmptyLand(t) && t.OwningPlayer() == them);
		MapUnit army = SpawnArmy(us, enemyLand, "Warrior");
		MapUnit member = army.Passengers()[0];
		MapUnit lone = Spawn(us, "Warrior", enemyLand);

		Assert.Equal(0, lone.HealRateAt(enemyLand));
		Assert.Equal(1, member.HealRateAt(enemyLand));

		BuildCity(us).AddBuilding(gameData.Buildings.First(b => b.allowsEnemyTerritoryHealing));
		Assert.Equal(2, member.HealRateAt(enemyLand));
	}

	// ---- Retreat ----

	// "The only unit abilities that an army can inherit is mobility, and
	// only when all units in that army have that ability" (Civ3 readme):
	// the army's extra movement point doesn't make it fast.
	[Fact]
	public void AnArmyIsFastOnlyIfAllItsMembersAre() {
		(Tile from, _, Tile to) = FindAdjacentLand();
		MapUnit spearman = Spawn(them, "Spearman", to);

		MapUnit slow = SpawnArmy(us, from, "Warrior", "Warrior");
		Assert.Equal(2, slow.MaxMovementPoints());
		Assert.Equal(0, slow.RetreatChance(spearman, true));
		// Nor does it make a slow army too fast to retreat from.
		MapUnit horseman = Spawn(them, "Horseman", to);
		Assert.True(horseman.RetreatChance(slow, false) > 0);
		gameData.RemoveUnit(slow);

		MapUnit mixed = SpawnArmy(us, from, "Horseman", "Warrior");
		Assert.Equal(0, mixed.RetreatChance(spearman, true));
		gameData.RemoveUnit(mixed);

		MapUnit fast = SpawnArmy(us, from, "Horseman", "Horseman");
		Assert.True(fast.RetreatChance(spearman, true) > 0);
	}

	// A fast attacker that loses rounds, its hit points and the rolls for
	// the fight, starting fresh with its moves.
	private (MapUnit horseman, MapUnit spearman) RetreatFight() {
		us.DeclareWarOn(them, gameData.turn);
		(Tile from, _, Tile to) = FindAdjacentLand();
		MapUnit horseman = Spawn(us, "Horseman", from);
		horseman.movementPoints.reset(horseman.MaxMovementPoints());
		MapUnit spearman = Spawn(them, "Spearman", to);
		Assert.Equal(3, horseman.hitPointsRemaining);
		Assert.True(horseman.RetreatChance(spearman, true) > 0);
		return (horseman, spearman);
	}

	// "Retreat possible if attacking unit drops from 2 HP to 1 HP" (Offa,
	// https://forums.civfanatics.com/threads/retreating-units.104978/).
	[Fact]
	public async Task AnAttackerRetreatsAsItDropsToItsLastHitPoint() {
		(MapUnit horseman, MapUnit spearman) = RetreatFight();
		CombatResult result = CombatResult.Impossible;
		// Two lost rounds, then a successful retreat roll.
		await WithRandom(new ScriptedRandom([0.99, 0.99, 0.0], 0.99), async () => result = await horseman.Fight(spearman));

		Assert.Equal(CombatResult.AttackerRetreated, result);
		Assert.Equal(1, horseman.hitPointsRemaining);
	}

	// An attacker that starts with one hit point has no 2 to 1 drop, so no
	// roll.
	[Fact]
	public async Task AnAttackerOnItsLastHitPointDoesNotRetreat() {
		(MapUnit horseman, MapUnit spearman) = RetreatFight();
		horseman.hitPointsRemaining = 1;
		CombatResult result = CombatResult.Impossible;
		await WithRandom(new ScriptedRandom([0.99], 0.0), async () => result = await horseman.Fight(spearman));

		Assert.Equal(CombatResult.AttackerKilled, result);
	}

	// "Zero chance if defender has 1 HP" (Offa, same thread).
	[Fact]
	public async Task NoRetreatFromAnOpponentOnItsLastHitPoint() {
		(MapUnit horseman, MapUnit spearman) = RetreatFight();
		spearman.hitPointsRemaining = 1;
		CombatResult result = CombatResult.Impossible;
		// The third roll would have been the retreat; it wins a round instead.
		await WithRandom(new ScriptedRandom([0.99, 0.99, 0.0], 0.99), async () => result = await horseman.Fight(spearman));

		Assert.Equal(CombatResult.DefenderKilled, result);
	}

	// The roll applies only if the attacker "would have at least 1/3
	// movement point left after the attack" (boogaboo,
	// https://forums.civfanatics.com/threads/how-do-retreats-work.100878).
	[Fact]
	public async Task AnAttackerWithoutMovesToSpareDoesNotRetreat() {
		(MapUnit horseman, MapUnit spearman) = RetreatFight();
		horseman.movementPoints.onUnitMove(1);
		CombatResult result = CombatResult.Impossible;
		await WithRandom(new ScriptedRandom([0.99, 0.99, 0.0], 0.99), async () => result = await horseman.Fight(spearman));

		Assert.Equal(CombatResult.AttackerKilled, result);
	}
}

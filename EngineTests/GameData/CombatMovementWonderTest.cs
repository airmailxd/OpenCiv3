using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The Great Wall, the Great Lighthouse, Magellan's Voyage, the Heroic Epic and
// the military great leaders it makes more likely.
public class CombatMovementWonderTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly Player barbarians;

	public CombatMovementWonderTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		barbarians = gameData.players.First(p => p.isBarbarians);

		// Seafaring gives ships a move too; the tests of the wonders' moves
		// leave it out.
		us.civilization.traits.Remove(Civilization.Trait.Seafaring);
		them.civilization.traits.Remove(Civilization.Trait.Seafaring);
	}

	// Makes every random roll come out as 0, so attackers win every round
	// and every chance (like a leader appearing) comes true.
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

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	// Gives the player the building in a city of its own.
	private void Give(Player owner, Building building) {
		City city = owner.cities.FirstOrDefault() ?? BuildCity(owner);
		city.AddBuilding(building);
	}

	// A building with just the given flag.
	private Building CustomWonder(SaveBuilding.Flag flag) {
		SaveBuilding sb = new() { name = $"Custom {flag}", greatWonderProperties = new SaveBuilding.GreatWonderProperties() };
		sb.flags.Add(flag);
		return new Building(sb, gameData);
	}

	private void MakeObsolete(Building wonder, Player owner) {
		Tech tech = gameData.techs.First(t => !owner.knownTechs.Contains(t.id));
		wonder.renderedObsoleteBy = tech;
		owner.knownTechs.Add(tech.id);
	}

	private Tile FindWater(System.Func<Tile, bool> predicate) {
		return gameData.map.tiles.First(t => t.IsWater() && !t.isFreshWater && t.unitsOnTile.Count == 0 && !t.HasCity() && predicate(t));
	}

	private Tile FindSea() => FindWater(t => t.IsSea());
	private Tile FindOcean() => FindWater(t => t.baseTerrainType.IsOcean);
	private Tile FindCoast() => FindWater(t => t.IsCoast());

	private void MakeElite(MapUnit unit) {
		unit.experienceLevel = gameData.experienceLevels.Last();
		unit.experienceLevelKey = unit.experienceLevel.key;
		unit.hitPointsRemaining = unit.maxHitPoints;
	}

	[Fact]
	public void RulesetHasTheWonderAndUnitFlags() {
		Assert.True(BuildingNamed("The Great Wall").doubleCombatVsBarbarians);
		Assert.True(BuildingNamed("The Great Lighthouse").safeSeaTravel);
		Assert.True(BuildingNamed("The Great Lighthouse").increasedShipMovement);
		Assert.True(BuildingNamed("Magellan's Voyage").increasedShipMovement);
		Assert.True(BuildingNamed("Heroic Epic").increasesLeaderChance);

		Assert.True(Prototype("Galley").sinksInSea);
		Assert.True(Prototype("Galley").sinksInOcean);
		Assert.False(Prototype("Caravel").sinksInSea);
		Assert.True(Prototype("Caravel").sinksInOcean);
		Assert.False(Prototype("Galleon").sinksInOcean);
		Assert.True(Prototype("Leader").isLeader);
		Assert.Equal("Leader", gameData.rules.BattleCreatedUnit);
	}

	// ---- The Great Wall ----

	[Fact]
	public void GreatWallDoublesStrengthAgainstBarbarians() {
		(Tile from, _, Tile to) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);
		MapUnit barbarian = Spawn(barbarians, "Warrior", to);
		MapUnit enemy = Spawn(them, "Warrior", to);

		double attackBefore = warrior.StrengthVersus(barbarian, CombatRole.Attack, null);
		double defenseBefore = warrior.StrengthVersus(barbarian, CombatRole.Defense, null);
		double defenseMultiplierBefore = StrengthBonus.ListToMultiplier(warrior.ListStrengthBonusesVersus(barbarian, CombatRole.Defense, null));

		Give(us, BuildingNamed("The Great Wall"));

		Assert.Equal(2 * attackBefore, warrior.StrengthVersus(barbarian, CombatRole.Attack, null), 6);
		// The +100% adds to the defender's other bonuses.
		Assert.Equal(defenseBefore * (defenseMultiplierBefore + 1) / defenseMultiplierBefore,
			warrior.StrengthVersus(barbarian, CombatRole.Defense, null), 6);
		// Only against barbarians, and only for the wonder's owner.
		Assert.Equal(attackBefore, warrior.StrengthVersus(enemy, CombatRole.Attack, null), 6);
		Assert.DoesNotContain(barbarian.ListStrengthBonusesVersus(warrior, CombatRole.Defense, null), b => b.amount == 1.0);
		Assert.DoesNotContain(warrior.ListStrengthBonusesVersus(barbarian, CombatRole.Bombard, null), b => b.amount == 1.0);
	}

	[Fact]
	public void ObsoleteGreatWallGivesNoBonus() {
		(Tile from, _, Tile to) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", from);
		MapUnit barbarian = Spawn(barbarians, "Warrior", to);
		double attackBefore = warrior.StrengthVersus(barbarian, CombatRole.Attack, null);

		Building wall = BuildingNamed("The Great Wall");
		Give(us, wall);
		MakeObsolete(wall, us);

		Assert.Equal(attackBefore, warrior.StrengthVersus(barbarian, CombatRole.Attack, null), 6);
	}

	// ---- Ship movement ----

	[Fact]
	public void LighthouseAndMagellansEachGiveShipsAMove() {
		Tile coast = FindCoast();
		MapUnit galley = Spawn(us, "Galley", coast);
		(Tile land, _, _) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", land);
		int baseMovement = Prototype("Galley").movement;
		Assert.Equal(baseMovement, galley.MaxMovementPoints());

		Building lighthouse = BuildingNamed("The Great Lighthouse");
		Give(us, lighthouse);
		Assert.Equal(baseMovement + 1, galley.MaxMovementPoints());

		Give(us, BuildingNamed("Magellan's Voyage"));
		Assert.Equal(baseMovement + 2, galley.MaxMovementPoints());

		MakeObsolete(lighthouse, us);
		Assert.Equal(baseMovement + 1, galley.MaxMovementPoints());

		// Land units and other civs' ships don't benefit.
		Assert.Equal(Prototype("Warrior").movement, warrior.MaxMovementPoints());
		MapUnit theirGalley = Spawn(them, "Galley", FindCoast());
		Assert.Equal(baseMovement, theirGalley.MaxMovementPoints());
	}

	[Fact]
	public void ShipMovementBonusAppliesAtTurnStartAndToNewShips() {
		Give(us, BuildingNamed("Magellan's Voyage"));
		int expected = Prototype("Galley").movement + 1;

		MapUnit galley = Spawn(us, "Galley", FindCoast());
		Assert.Equal(expected, galley.movementPoints.remaining);

		galley.movementPoints.onConsumeAll();
		galley.OnBeginTurn();
		Assert.Equal(expected, galley.movementPoints.remaining);
		// The unit's description shows its full movement.
		Assert.EndsWith($"{expected})", galley.Describe());
	}

	[Fact]
	public void PlusTwoShipMovementGivesTwoMoves() {
		Give(us, CustomWonder(SaveBuilding.Flag.PlusTwoShipMovement));
		MapUnit galley = Spawn(us, "Galley", FindCoast());
		Assert.Equal(Prototype("Galley").movement + 2, galley.MaxMovementPoints());
	}

	// ---- Coastal ships ----

	// Learns the tech for a ship, and what it needs.
	private void Learn(Player player, string prototype) {
		Tech tech = Prototype(prototype).requiredTech;
		foreach (Tech prereq in tech.Prerequisites) {
			player.knownTechs.Add(prereq.id);
		}
		player.knownTechs.Add(tech.id);
	}

	// The fixture's civs are still in the Ancient era, without Astronomy.
	[Fact]
	public void SeaAndOceanAreUnsafeWithoutNavalResearch() {
		Assert.DoesNotContain(Prototype("Caravel").requiredTech.id, us.knownTechs);
		MapUnit galley = Spawn(us, "Galley", FindCoast());

		Assert.False(galley.IsUnsafeWater(FindCoast()));
		Assert.True(galley.IsUnsafeWater(FindSea()));
		Assert.True(galley.IsUnsafeWater(FindOcean()));
	}

	[Fact]
	public void GreatLighthouseMakesSeaButNotOceanSafe() {
		Give(us, BuildingNamed("The Great Lighthouse"));
		MapUnit galley = Spawn(us, "Galley", FindCoast());

		Assert.False(galley.IsUnsafeWater(FindSea()));
		Assert.True(galley.IsUnsafeWater(FindOcean()));
	}

	// Safety comes from the owner's research, not the ship: once a civ can
	// build Caravels the Sea is safe for its Galleys too, and once it can
	// build Galleons so is the Ocean.
	[Fact]
	public void NavalResearchMakesTheWatersSafeForEveryShip() {
		Assert.Contains(us.civilization, Prototype("Caravel").producibleBy);
		MapUnit galley = Spawn(us, "Galley", FindCoast());
		MapUnit theirGalley = Spawn(them, "Galley", FindCoast());

		Learn(us, "Caravel");
		Assert.False(galley.IsUnsafeWater(FindSea()));
		Assert.True(galley.IsUnsafeWater(FindOcean()));

		Learn(us, "Galleon");
		Assert.False(galley.IsUnsafeWater(FindSea()));
		Assert.False(galley.IsUnsafeWater(FindOcean()));

		// Only for the civ that learned it.
		Assert.True(theirGalley.IsUnsafeWater(FindOcean()));
	}

	// Ships may sail into water their civ can't yet sail safely, though
	// paths keep them out of it.
	// Another civ's unique ship doesn't count: the Carrack sails the Ocean,
	// but only for the civ that can build it.
	[Fact]
	public void OnlyTheCivsOwnShipsMakeTheWatersSafe() {
		UnitPrototype carrack = Prototype("Carrack");
		Assert.DoesNotContain(us.civilization, carrack.producibleBy);
		us.knownTechs.Add(carrack.requiredTech.id);
		MapUnit galley = Spawn(us, "Galley", FindCoast());
		Assert.True(galley.IsUnsafeWater(FindOcean()));

		// Were it theirs, it would. (What's safe is worked out again when a
		// tech is learned, so learn one that no ship needs.)
		carrack.producibleBy.Add(us.civilization);
		Tech other = gameData.techs.First(t => !us.knownTechs.Contains(t.id)
			&& !gameData.unitPrototypes.Any(p => p.IsSeaUnit() && p.requiredTech == t));
		us.knownTechs.Add(other.id);
		Assert.False(galley.IsUnsafeWater(FindOcean()));
	}

	[Fact]
	public void GalleyMayMoveFromCoastOntoSeaButPathsAvoidIt() {
		Tile coast = FindWater(t => t.IsCoast() && t.neighbors.Values.Any(n => n != Tile.NONE && n.IsSea() && n.unitsOnTile.Count == 0));
		(TileDirection dir, Tile sea) = coast.neighbors.First(kv => kv.Value != Tile.NONE && kv.Value.IsSea() && kv.Value.unitsOnTile.Count == 0);
		MapUnit galley = Spawn(us, "Galley", coast);

		Assert.True(galley.CanEnter(sea));
		Assert.True(galley.PathAvoids(sea, null));

		Give(us, BuildingNamed("The Great Lighthouse"));
		Assert.False(galley.PathAvoids(sea, null));
	}

	[Fact]
	public void ShipsInUnsafeWaterSinkWithTheirCargo() {
		MapUnit galley = Spawn(us, "Galley", FindOcean());
		MapUnit safeGalley = Spawn(us, "Galley", FindCoast());
		MapUnit theirGalley = Spawn(them, "Galley", FindOcean());
		Learn(them, "Galleon");

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ZeroRandom();
		try {
			Assert.Equal([galley], MapUnit.SinkShipsInUnsafeWater(gameData, us));
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.DoesNotContain(galley, us.units);
		Assert.Contains(safeGalley, us.units);
		Assert.Empty(MapUnit.SinkShipsInUnsafeWater(gameData, them));
		Assert.Contains(theirGalley, them.units);
	}

	// A roll at or above the chance spares the ship.
	private class HighRandom : System.Random {
		protected override double Sample() => 0.99;
	}

	[Fact]
	public void ShipsInUnsafeWaterMaySurvive() {
		MapUnit galley = Spawn(us, "Galley", FindOcean());

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new HighRandom();
		try {
			Assert.Empty(MapUnit.SinkShipsInUnsafeWater(gameData, us));
		} finally {
			C7GameData.GameData.rng = original;
		}
		Assert.Contains(galley, us.units);
	}

	// ---- Seafaring ----

	[Fact]
	public void SeafaringCivsShipsGetAMove() {
		MapUnit galley = Spawn(us, "Galley", FindCoast());
		(Tile land, _, _) = FindAdjacentLand();
		MapUnit warrior = Spawn(us, "Warrior", land);
		MapUnit theirGalley = Spawn(them, "Galley", FindCoast());

		us.civilization.traits.Add(Civilization.Trait.Seafaring);

		Assert.Equal(Prototype("Galley").movement + 1, galley.MaxMovementPoints());
		// It adds to the wonders' moves.
		Give(us, BuildingNamed("Magellan's Voyage"));
		Assert.Equal(Prototype("Galley").movement + 2, galley.MaxMovementPoints());
		// Land units and other civs' ships don't benefit.
		Assert.Equal(Prototype("Warrior").movement, warrior.MaxMovementPoints());
		Assert.Equal(Prototype("Galley").movement, theirGalley.MaxMovementPoints());
	}

	// ---- Great leaders ----

	[Fact]
	public void HeroicEpicIncreasesLeaderChance() {
		Assert.Equal(1.0 / 16.0, MapUnit.LeaderChanceFor(us));
		Give(us, BuildingNamed("Heroic Epic"));
		Assert.Equal(1.0 / 12.0, MapUnit.LeaderChanceFor(us));
	}

	[Fact]
	public async Task EliteVictoryCanProduceALeader() {
		us.DeclareWarOn(them, gameData.turn);
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit tank = Spawn(us, "Tank", from);
		MakeElite(tank);
		MapUnit warrior = Spawn(them, "Warrior", to);
		warrior.hitPointsRemaining = 1;

		await WithZeroRandom(async () => Assert.True(await tank.Move(dir)));

		Assert.DoesNotContain(warrior, gameData.mapUnits);
		MapUnit leader = us.units.Single(u => u.IsLeader());
		Assert.Equal(from, leader.location);
		Assert.True(tank.hasProducedLeader);
	}

	[Fact]
	public async Task NonEliteVictoryProducesNoLeader() {
		us.DeclareWarOn(them, gameData.turn);
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand();
		MapUnit tank = Spawn(us, "Tank", from);
		MapUnit warrior = Spawn(them, "Warrior", to);
		warrior.hitPointsRemaining = 1;

		await WithZeroRandom(async () => Assert.True(await tank.Move(dir)));

		Assert.DoesNotContain(us.units, u => u.IsLeader());
	}

	[Fact]
	public void LeaderRules() {
		(Tile from, _, Tile to) = FindAdjacentLand();
		MapUnit tank = Spawn(us, "Tank", from);
		MakeElite(tank);
		MapUnit enemy = Spawn(them, "Warrior", to);
		MapUnit barbarian = Spawn(barbarians, "Warrior", to);

		Assert.True(tank.CouldProduceLeaderBeating(enemy, wasElite: true));
		// Not from units promoted to elite by the fight, nor from beating
		// barbarians.
		Assert.False(tank.CouldProduceLeaderBeating(enemy, wasElite: false));
		Assert.False(tank.CouldProduceLeaderBeating(barbarian, wasElite: true));

		// One leader at a time, and one per unit.
		MapUnit leader = tank.ProduceLeader();
		Assert.NotNull(leader);
		Assert.False(tank.CouldProduceLeaderBeating(enemy, wasElite: true));
		MapUnit otherTank = Spawn(us, "Tank", from);
		MakeElite(otherTank);
		Assert.False(otherTank.CouldProduceLeaderBeating(enemy, wasElite: true));
		gameData.RemoveUnit(leader);
		Assert.True(otherTank.CouldProduceLeaderBeating(enemy, wasElite: true));
		Assert.False(tank.CouldProduceLeaderBeating(enemy, wasElite: true));

		Assert.True(new SaveUnit(tank).hasProducedLeader);
	}

	[Fact]
	public void LeaderFormsAnArmyInACity() {
		City city = BuildCity(us);
		MapUnit leader = Spawn(us, "Leader", city.location);
		MapUnit outside = Spawn(us, "Leader", FindAdjacentLand().from);

		Assert.False(outside.CanFormArmy());
		Assert.True(leader.CanFormArmy());
		Assert.Contains(UnitAction.BuildArmy, leader.GetAvailableActions());

		MapUnit army = leader.FormArmy();

		Assert.NotNull(army);
		Assert.True(army.IsArmy());
		Assert.Equal(city.location, army.location);
		Assert.DoesNotContain(leader, gameData.mapUnits);
		Assert.DoesNotContain(leader, us.units);
	}

	[Fact]
	public void LeaderHurriesImprovementsButNotGreatWonders() {
		City city = BuildCity(us);
		MapUnit leader = Spawn(us, "Leader", city.location);

		city.SetItemBeingProduced(BuildingNamed("The Great Wall"));
		Assert.False(leader.CanHurryProduction());
		Assert.Contains("great wonders", leader.HurryProductionBlocker());

		city.SetItemBeingProduced(Prototype("Warrior"));
		Assert.False(leader.CanHurryProduction());
		Assert.Contains("units", leader.HurryProductionBlocker());
		// The button still shows, so the player can learn why it won't work.
		Assert.Contains(UnitAction.HurryBuilding, leader.GetAvailableActions());

		Building temple = BuildingNamed("Temple");
		city.SetItemBeingProduced(temple);
		Assert.True(leader.CanHurryProduction());
		Assert.Null(leader.HurryProductionBlocker());

		Assert.True(leader.HurryProductionAsLeader());

		Assert.Equal(us.ShieldCost(temple), city.shieldsStored);
		Assert.DoesNotContain(leader, gameData.mapUnits);
	}

	[Theory]
	[InlineData("Heroic Epic")]
	[InlineData("Forbidden Palace")]
	public void LeaderHurriesSmallWonders(string name) {
		City city = BuildCity(us);
		MapUnit leader = Spawn(us, "Leader", city.location);
		Building wonder = BuildingNamed(name);
		Assert.False(wonder.IsGreatWonder());

		city.SetItemBeingProduced(wonder);
		Assert.True(leader.CanHurryProduction());
		Assert.True(leader.HurryProductionAsLeader());

		Assert.Equal(us.ShieldCost(wonder), city.shieldsStored);
		Assert.DoesNotContain(leader, gameData.mapUnits);
	}

	[Fact]
	public void ProductionCantChangeAfterALeaderHurries() {
		City city = BuildCity(us);
		MapUnit leader = Spawn(us, "Leader", city.location);
		Building temple = BuildingNamed("Temple");
		city.SetItemBeingProduced(temple);
		Assert.True(leader.HurryProductionAsLeader());

		// The leader's shields can't be moved to a unit or great wonder.
		Assert.False(city.ChangeProduction(Prototype("Warrior")));
		Assert.False(city.ChangeProduction(BuildingNamed("The Great Wall")));
		Assert.Equal(temple, city.itemBeingProduced);
		Assert.Equal(us.ShieldCost(temple), city.shieldsStored);

		// Next turn production can change again.
		city.HandleCityProduction(gameData);
		Assert.False(city.hurriedThisTurn);
		Assert.True(city.ChangeProduction(Prototype("Warrior")));
	}

	[Fact]
	public void ProductionCantChangeAfterRushingWithGold() {
		City city = BuildCity(us);
		us.government.hurryingType = Government.HurryProductionType.PaidLabor;
		us.gold = 100000;
		Building temple = BuildingNamed("Temple");
		city.SetItemBeingProduced(temple);
		city.HurryProduction();
		Assert.Equal(us.ShieldCost(temple), city.shieldsStored);

		Assert.False(city.ChangeProduction(Prototype("Warrior")));
		Assert.Equal(temple, city.itemBeingProduced);
	}

	[Fact]
	public void HurriedProductionStaysLockedThroughASave() {
		City city = BuildCity(us);
		MapUnit leader = Spawn(us, "Leader", city.location);
		city.SetItemBeingProduced(BuildingNamed("Temple"));
		Assert.True(leader.HurryProductionAsLeader());

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);

		Assert.True(loaded.cities.Single(c => c.id == city.id).hurriedThisTurn);
	}

	[Fact]
	public void AiLeaderAtWarHurriesInsteadOfFormingAnArmy() {
		City city = BuildCity(us);
		them.DeclareWarOn(us, gameData.turn);
		MapUnit leader = Spawn(us, "Leader", city.location);
		city.SetItemBeingProduced(Prototype("Warrior"));

		Assert.True(PlayerAI.UseLeaderInCity(leader, us));

		// The city switched to an improvement, which the leader finished.
		Building building = Assert.IsType<Building>(city.itemBeingProduced);
		Assert.False(building.IsGreatWonder());
		Assert.Equal(us.ShieldCost(building), city.shieldsStored);
		Assert.DoesNotContain(leader, us.units);
		Assert.DoesNotContain(us.units, u => u.IsArmy());
	}

	[Fact]
	public void AiLeaderWaitsInACityHurriedThisTurn() {
		City city = BuildCity(us);
		MapUnit leader = Spawn(us, "Leader", city.location);
		city.SetItemBeingProduced(Prototype("Warrior"));
		city.FillProductionBox();

		Assert.False(PlayerAI.UseLeaderInCity(leader, us));
		Assert.Contains(leader, us.units);
		Assert.IsType<UnitPrototype>(city.itemBeingProduced);
	}

	[Fact]
	public void AiLeaderOutsideACityHeadsForTheNearestCity() {
		City city = BuildCity(us);
		Tile outside = city.location.neighbors.Values.First(t => t != Tile.NONE && IsEmptyLand(t));
		MapUnit leader = Spawn(us, "Leader", outside);

		C7GameData.UnitAI ai = PlayerAI.GetAIForUnit(leader, us);

		C7Engine.AI.UnitAI.DefenderAI defender = Assert.IsType<C7Engine.AI.UnitAI.DefenderAI>(ai);
		Assert.Equal(city.location, defender.data.destination);
	}
}

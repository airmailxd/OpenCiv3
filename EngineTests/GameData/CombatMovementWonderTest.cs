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

	[Fact]
	public void GalleyStaysOffSeaAndOceanWithoutTheLighthouse() {
		MapUnit galley = Spawn(us, "Galley", FindCoast());
		Tile sea = FindSea();
		Tile ocean = FindOcean();

		Assert.True(galley.CanEnterWaterTerrain(FindCoast()));
		Assert.False(galley.CanEnterWaterTerrain(sea));
		Assert.False(galley.CanEnterWaterTerrain(ocean));
	}

	[Fact]
	public void GreatLighthouseLetsGalleysEnterSeaButNotOcean() {
		Give(us, BuildingNamed("The Great Lighthouse"));
		MapUnit galley = Spawn(us, "Galley", FindCoast());

		Assert.True(galley.CanEnterWaterTerrain(FindSea()));
		Assert.False(galley.CanEnterWaterTerrain(FindOcean()));
	}

	[Fact]
	public void CaravelEntersSeaButNotOcean() {
		MapUnit caravel = Spawn(us, "Caravel", FindCoast());
		Assert.True(caravel.CanEnterWaterTerrain(FindSea()));
		Assert.False(caravel.CanEnterWaterTerrain(FindOcean()));

		MapUnit galleon = Spawn(us, "Galleon", FindCoast());
		Assert.True(galleon.CanEnterWaterTerrain(FindOcean()));
	}

	[Fact]
	public void GalleyCannotMoveFromCoastOntoSea() {
		Tile coast = FindWater(t => t.IsCoast() && t.neighbors.Values.Any(n => n != Tile.NONE && n.IsSea() && n.unitsOnTile.Count == 0));
		(TileDirection dir, Tile sea) = coast.neighbors.First(kv => kv.Value != Tile.NONE && kv.Value.IsSea() && kv.Value.unitsOnTile.Count == 0);
		MapUnit galley = Spawn(us, "Galley", coast);

		Assert.False(galley.CanEnter(sea));

		Give(us, BuildingNamed("The Great Lighthouse"));
		Assert.True(galley.CanEnter(sea));
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
}

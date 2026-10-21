using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.UnitAI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The AI's use of leaders and armies (see ArmyAI), and its hurt units
// healing (see RestAI).
public class ArmyAITest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public ArmyAITest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		return gameData.SpawnUnit(owner, Prototype(prototype), tile);
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	[Fact]
	public void LeaderAtWarFormsAnArmyFromTheAttackersAtHand() {
		City city = BuildCity(us);
		// Enough cities to support the army (see MapUnit.CanFormArmy).
		while (!us.CanSupportAnotherArmy()) {
			BuildCity(us);
		}
		them.DeclareWarOn(us, gameData.turn);
		MapUnit spearman = Spawn(us, "Spearman", city.location);
		MapUnit sword1 = Spawn(us, "Swordsman", city.location);
		MapUnit sword2 = Spawn(us, "Swordsman", city.location);
		MapUnit leader = Spawn(us, "Leader", city.location);
		city.SetItemBeingProduced(Prototype("Warrior"));

		Assert.True(PlayerAI.UseLeaderInCity(leader, us));

		MapUnit army = Assert.Single(us.units, u => u.IsArmy());
		Assert.DoesNotContain(leader, us.units);
		Assert.True(sword1.IsLoadedIn(army));
		Assert.True(sword2.IsLoadedIn(army));
		// The defender stays to guard the city.
		Assert.False(spearman.IsLoaded());
		Assert.IsType<ArmyAI>(army.currentAI);
	}

	[Fact]
	public void LeaderWithoutAttackersNearbyHurriesInstead() {
		City city = BuildCity(us);
		them.DeclareWarOn(us, gameData.turn);
		Spawn(us, "Spearman", city.location);
		MapUnit leader = Spawn(us, "Leader", city.location);
		city.SetItemBeingProduced(Prototype("Warrior"));

		Assert.True(PlayerAI.UseLeaderInCity(leader, us));

		Assert.DoesNotContain(us.units, u => u.IsArmy());
		Assert.IsType<Building>(city.itemBeingProduced);
	}

	[Fact]
	public void TheLastDefenderDoesNotJoinTheArmy() {
		City city = BuildCity(us);
		MapUnit army = Spawn(us, "Army", city.location);
		MapUnit sword = Spawn(us, "Swordsman", city.location);

		ArmyAI.Recruit(army, us);

		Assert.False(sword.IsLoaded());
	}

	[Fact]
	public void AHurtArmyRestsInACity() {
		City city = BuildCity(us);
		them.DeclareWarOn(us, gameData.turn);
		Spawn(us, "Spearman", city.location);
		MapUnit army = Spawn(us, "Army", city.location);
		foreach (string name in new[] { "Swordsman", "Swordsman", "Swordsman" }) {
			MapUnit member = Spawn(us, name, city.location);
			member.LoadOntoTransportHere();
			member.hitPointsRemaining = 1;
		}
		army.movementPoints.reset(army.MaxMovementPoints());

		C7GameData.UnitAI ai = PlayerAI.GetAIForUnit(army, us);
		Assert.IsType<ArmyAI>(ai);
		((ArmyAI)ai).PlayTurnImpl(us, army);

		Assert.Equal(city.location, army.location);
		Assert.False(army.movementPoints.canMove);
		Assert.True(army.heldWithoutMoving);
	}

	private void MakeElite(MapUnit unit) {
		unit.experienceLevel = gameData.experienceLevels.Last();
		unit.experienceLevelKey = unit.experienceLevel.key;
		unit.hitPointsRemaining = unit.maxHitPoints;
	}

	[Fact]
	public void AHurtEliteAttackerGoesToHeal() {
		City city = BuildCity(us);
		Spawn(us, "Spearman", city.location);
		MapUnit sword = Spawn(us, "Swordsman", city.location);
		MakeElite(sword);
		sword.hitPointsRemaining -= 2;

		C7GameData.UnitAI ai = PlayerAI.GetAIForUnit(sword, us);

		RestAI rest = Assert.IsType<RestAI>(ai);
		rest.PlayTurnImpl(us, sword);
		Assert.True(sword.heldWithoutMoving);
	}

	[Fact]
	public void ARegularAttackerDownAHitPointRests() {
		City city = BuildCity(us);
		MapUnit regular = Spawn(us, "Swordsman", city.location);
		regular.hitPointsRemaining -= 1;

		Assert.True(RestAI.ShouldRest(regular, us));
	}

	[Fact]
	public void AnEliteDownAHitPointOrAHealthyAttackerDoesNotRest() {
		City city = BuildCity(us);
		MapUnit healthy = Spawn(us, "Swordsman", city.location);
		MapUnit elite = Spawn(us, "Swordsman", city.location);
		MakeElite(elite);
		elite.hitPointsRemaining -= 1;

		Assert.False(RestAI.ShouldRest(healthy, us));
		Assert.False(RestAI.ShouldRest(elite, us));
	}

	[Fact]
	public void AHumansHurtUnitIsLeftAlone() {
		us.isHuman = true;
		City city = BuildCity(us);
		MapUnit sword = Spawn(us, "Swordsman", city.location);
		sword.hitPointsRemaining = 1;

		Assert.False(RestAI.ShouldRest(sword, us));
	}

	[Fact]
	public void LeaderHurriesAnImprovementTheCityWants() {
		AiExperiments.Enable(us, "leader-hurry-best");
		City city = BuildCity(us);
		Spawn(us, "Spearman", city.location);
		MapUnit leader = Spawn(us, "Leader", city.location);
		city.SetItemBeingProduced(Prototype("Warrior"));

		Assert.True(PlayerAI.UseLeaderInCity(leader, us));

		Building building = Assert.IsType<Building>(city.itemBeingProduced);
		Assert.False(building.IsGreatWonder());
		Assert.Equal(us.ShieldCost(building), city.shieldsStored);
	}

	// On trial ("leader-choice"): at war, a leader with good attackers at
	// hand forms an army.
	[Fact]
	public void ChoosingLeaderFormsAnArmyAtWarWithGoodAttackers() {
		AiExperiments.Enable(us, PlayerAI.LeaderChoice);
		City city = BuildCity(us);
		while (!us.CanSupportAnotherArmy()) {
			BuildCity(us);
		}
		them.DeclareWarOn(us, gameData.turn);
		Spawn(us, "Spearman", city.location);
		Spawn(us, "Swordsman", city.location);
		Spawn(us, "Swordsman", city.location);
		MapUnit leader = Spawn(us, "Leader", city.location);

		Assert.True(PlayerAI.UseLeaderInCity(leader, us));
		Assert.Single(us.units, u => u.IsArmy());
	}

	// On trial: at peace with no war planned, the leader rushes a building
	// rather than form an army, though attackers are at hand.
	[Fact]
	public void ChoosingLeaderAtPeaceRushesABuildingInstead() {
		AiExperiments.Enable(us, PlayerAI.LeaderChoice);
		City city = BuildCity(us);
		while (!us.CanSupportAnotherArmy()) {
			BuildCity(us);
		}
		Spawn(us, "Spearman", city.location);
		for (int i = 0; i < 3; i++) {
			Spawn(us, "Swordsman", city.location);
		}
		MapUnit leader = Spawn(us, "Leader", city.location);
		Building dearest = city.ListProductionOptions(gameData).OfType<Building>()
			.Where(b => !b.IsGreatWonder()).OrderByDescending(b => us.ShieldCost(b)).First();
		city.SetItemBeingProduced(dearest);

		Assert.Equal((city, dearest), PlayerAI.BestBuildForMilitaryLeader(us, leader));
		Assert.True(PlayerAI.UseLeaderInCity(leader, us));
		Assert.Equal(us.ShieldCost(dearest), city.shieldsStored);
		Assert.DoesNotContain(us.units, u => u.IsArmy());
	}

	// On trial: a build that's nearly done is never chosen; the city will
	// finish it soon anyway.
	[Fact]
	public void ChoosingLeaderNeverRushesABuildThatIsNearlyDone() {
		AiExperiments.Enable(us, PlayerAI.LeaderChoice);
		City city = BuildCity(us);
		Building current = city.ListProductionOptions(gameData).OfType<Building>()
			.Where(b => !b.IsGreatWonder()).OrderByDescending(b => us.ShieldCost(b)).First();
		city.SetItemBeingProduced(current);
		city.SetStoredShields(us.ShieldCost(current) - 1);

		(City target, Building item) = PlayerAI.BestBuildForMilitaryLeader(us);
		Assert.False(target == city && item == current);
		// With every build nearly done, nothing qualifies.
		foreach (City c in us.cities) {
			c.SetStoredShields(10_000);
		}
		Assert.Null(PlayerAI.BestBuildForMilitaryLeader(us).Item1);
	}
}

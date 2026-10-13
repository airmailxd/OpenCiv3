using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Aircraft fly missions from their bases (see MapUnit_Air.cs), as the
// Conquests Civilopedia's GCON_Air_Missions describes.
public class AirUnitTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public AirUnitTest(SaveGameFixture fixture) {
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

	// Rolls `next` for every Next, and `sample` for every NextDouble.
	private class FixedRandom(int next, double sample) : System.Random {
		protected override double Sample() => sample;
		public override int Next(int maxValue) => System.Math.Min(next, maxValue - 1);
		public override int Next(int minValue, int maxValue) => System.Math.Clamp(next, minValue, maxValue - 1);
	}

	private static T WithRandom<T>(int next, double sample, System.Func<T> action) {
		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new FixedRandom(next, sample);
		try {
			return action();
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	private UnitPrototype Prototype(string name) => gameData.unitPrototypes.Single(p => p.name == name);

	private MapUnit Spawn(Player owner, string prototype, Tile tile) => gameData.SpawnUnit(owner, Prototype(prototype), tile);

	private City FoundCity(Player player, Tile tile = null) {
		tile ??= player.units.First(u => u.unitType.isSettler).location;
		return CityInteractions.BuildCity(tile, player, player.GetNextCityName());
	}

	private Tile EmptyLandAt(Tile from, int distance) {
		return gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && !t.hasBarbarianCamp && from.DistanceTo(t) == distance
			&& t.unitsOnTile.Count == 0 && !t.IsImpassable());
	}

	private void GoToWar() {
		us.EnsureRelationshipExists(them);
		us.DeclareWarOn(them, gameData.turn);
	}

	[Fact]
	public void AircraftDontMoveLikeOtherUnits() {
		City city = FoundCity(us);
		MapUnit fighter = Spawn(us, "Fighter", city.location);
		Tile next = city.location.neighbors.Values.First(t => t.IsLand());

		Assert.False(fighter.CanEnter(next));
	}

	[Fact]
	public void AircraftRebaseToOwnCitiesWithinSixTimesTheirRange() {
		City home = FoundCity(us);
		MapUnit fighter = Spawn(us, "Fighter", home.location);
		int range = fighter.RebaseRange();
		Assert.Equal(6 * Prototype("Fighter").operationalRange, range);

		City near = FoundCity(us, EmptyLandAt(home.location, 5));
		Tile farTile = gameData.map.tiles.FirstOrDefault(t => t.IsLand() && !t.HasCity() && home.location.DistanceTo(t) > range
			&& t.unitsOnTile.Count == 0);
		Tile plain = EmptyLandAt(home.location, 3);

		Assert.True(fighter.CanRebaseTo(near.location));
		Assert.False(fighter.CanRebaseTo(plain));
		if (farTile != null) {
			City far = FoundCity(us, farTile);
			Assert.False(fighter.CanRebaseTo(far.location));
		}

		fighter.Rebase(near.location);

		Assert.Same(near.location, fighter.location);
		Assert.Contains(fighter, near.location.unitsOnTile);
		Assert.DoesNotContain(fighter, home.location.unitsOnTile);
		Assert.False(fighter.movementPoints.canMove);
	}

	[Fact]
	public void AircraftRebaseOntoCarriersWithRoomAndFlyFromThem() {
		City home = FoundCity(us);
		Tile sea = gameData.map.tiles.First(t => t.IsWater() && home.location.DistanceTo(t) is >= 2 and <= 6);
		MapUnit carrier = Spawn(us, "Carrier", sea);
		MapUnit fighter = Spawn(us, "Fighter", home.location);

		Assert.True(fighter.CanRebaseTo(sea));
		fighter.Rebase(sea);

		Assert.Same(sea, fighter.location);
		Assert.True(fighter.IsLoadedIn(carrier));
		fighter.OnBeginTurn();
		Assert.True(fighter.IsAtAirBase());
		Assert.True(fighter.CanFlyMission(UnitAction.Recon));

		// A full carrier takes no more.
		for (int i = carrier.FreeCapacity(); i > 0; i--) {
			Spawn(us, "Fighter", sea).loadedOnUnitId = carrier.id;
		}
		MapUnit another = Spawn(us, "Fighter", home.location);
		Assert.False(another.CanRebaseTo(sea));
	}

	[Fact]
	public void AircraftAwayFromABaseFlyNoMissions() {
		City home = FoundCity(us);
		MapUnit bomber = Spawn(us, "Bomber", EmptyLandAt(home.location, 2));
		Tile target = EmptyLandAt(home.location, 4);
		Spawn(them, "Warrior", target);
		GoToWar();

		Assert.False(bomber.IsAtAirBase());
		Assert.False(bomber.CanAirBombTile(target));
	}

	[Fact]
	public void BombersBombWithinTheirOperationalRange() {
		City home = FoundCity(us);
		MapUnit bomber = Spawn(us, "Bomber", home.location);
		int range = bomber.OperationalRange();
		Tile inRange = EmptyLandAt(home.location, range);
		Tile outOfRange = gameData.map.tiles.FirstOrDefault(t => t.IsLand() && !t.HasCity() && home.location.DistanceTo(t) == range + 1);
		MapUnit target = Spawn(them, "Warrior", inRange);
		if (outOfRange != null) {
			Spawn(them, "Warrior", outOfRange);
			Assert.False(bomber.CanAirBombTile(outOfRange));
		}
		GoToWar();

		Assert.True(bomber.CanAirBombTile(inRange));
		// Nobody intercepts, and every shot hits: the Bomber's bombardment
		// is lethal on land.
		bool reached = WithRandom(0, 0.0, () => bomber.AirBomb(inRange).Result);

		Assert.True(reached);
		Assert.True(target.hitPointsRemaining <= 0 || !inRange.unitsOnTile.Contains(target));
		Assert.False(bomber.movementPoints.canMove);
		Assert.False(bomber.CanAirBombTile(inRange));
	}

	[Fact]
	public void FightersOnAirSuperiorityInterceptBombersOncePerTurn() {
		GoToWar();
		City ours = FoundCity(us);
		City theirs = FoundCity(them, EmptyLandAt(ours.location, 4));
		MapUnit fighter = Spawn(them, "Fighter", theirs.location);
		fighter.FlyAirSuperiority();
		Assert.True(fighter.isOnAirSuperiority);

		Tile target = EmptyLandAt(theirs.location, 1);
		Assert.True(ours.location.DistanceTo(target) <= Prototype("Bomber").operationalRange);
		MapUnit defender = Spawn(them, "Warrior", target);
		MapUnit bomber = Spawn(us, "Bomber", ours.location);

		// The fighter tries (0 < 50) and, firing 0 against its chance,
		// shoots the bomber down. The run never reaches the target.
		bool reached = WithRandom(0, 0.0, () => bomber.AirBomb(target).Result);

		Assert.False(reached);
		Assert.DoesNotContain(bomber, ours.location.unitsOnTile);
		Assert.Equal(defender.maxHitPoints, defender.hitPointsRemaining);
		Assert.True(fighter.hasInterceptedThisTurn);

		// Its interception for the turn is spent.
		MapUnit second = Spawn(us, "Bomber", ours.location);
		Assert.True(WithRandom(0, 0.0, () => second.AirBomb(target).Result));

		// The stance ends when its owner's turn begins.
		fighter.OnBeginTurn();
		Assert.False(fighter.isOnAirSuperiority);
		Assert.False(fighter.hasInterceptedThisTurn);
	}

	[Fact]
	public void AnInterceptedBomberThatSurvivesStillFailsItsRun() {
		GoToWar();
		City ours = FoundCity(us);
		City theirs = FoundCity(them, EmptyLandAt(ours.location, 4));
		MapUnit fighter = Spawn(them, "Fighter", theirs.location);
		fighter.FlyAirSuperiority();
		Tile target = EmptyLandAt(theirs.location, 1);
		MapUnit defender = Spawn(them, "Warrior", target);
		MapUnit bomber = Spawn(us, "Bomber", ours.location);
		Assert.True(bomber.CanAirBombTile(target));

		bool reached = WithRandom(0, 0.999, () => bomber.AirBomb(target).Result);

		Assert.False(reached);
		Assert.Contains(bomber, ours.location.unitsOnTile);
		Assert.False(bomber.movementPoints.canMove);
		Assert.Equal(defender.maxHitPoints, defender.hitPointsRemaining);
	}

	[Fact]
	public void StealthAircraftAreInterceptedOnlyWithTheStealthChance() {
		GoToWar();
		City ours = FoundCity(us);
		City theirs = FoundCity(them, EmptyLandAt(ours.location, 3));
		MapUnit fighter = Spawn(them, "Fighter", theirs.location);
		fighter.FlyAirSuperiority();
		Tile target = EmptyLandAt(theirs.location, 1);

		MapUnit stealth = Spawn(us, "Stealth Bomber", ours.location);
		MapUnit plain = Spawn(us, "Bomber", ours.location);

		// A roll of 10: under the 50% for air missions, not the 5% for
		// stealth ones.
		Assert.False(WithRandom(10, 0.999, () => stealth.TryToIntercept(target, out _)));
		Assert.True(WithRandom(10, 0.999, () => plain.TryToIntercept(target, out _)));
	}

	[Fact]
	public void ReconRevealsTheTargetAndItsNeighbours() {
		City home = FoundCity(us);
		MapUnit fighter = Spawn(us, "Fighter", home.location);
		Tile target = EmptyLandAt(home.location, fighter.OperationalRange());
		foreach (Tile t in target.neighbors.Values.Append(target)) {
			us.tileKnowledge.knownTiles.Remove(t);
		}

		Assert.True(fighter.CanReconTile(target));
		fighter.Recon(target);

		Assert.True(us.tileKnowledge.isTileKnown(target));
		Assert.True(us.tileKnowledge.isActiveTile(target));
		Assert.All(target.neighbors.Values.Where(Tile.IsTileValid), t => Assert.True(us.tileKnowledge.isTileKnown(t)));
		Assert.False(fighter.movementPoints.canMove);
	}

	[Fact]
	public void PrecisionStrikesNeedSmartWeaponsAndHitOnlyBuildings() {
		City ours = FoundCity(us);
		City theirs = FoundCity(them, EmptyLandAt(ours.location, 4));
		while (theirs.residents.Count < 4) {
			theirs.AddCitizen(new CityResident() {
				city = theirs,
				citizenType = theirs.residents[0].citizenType,
				tileWorked = theirs.residents[0].tileWorked,
			});
		}
		theirs.AddBuilding(gameData.Buildings.Single(b => b.name == "Granary"));
		MapUnit stealth = Spawn(us, "Stealth Fighter", ours.location);
		GoToWar();

		Assert.False(stealth.CanPrecisionBombTile(theirs.location));
		Tech smartWeapons = gameData.techs.First(t => t.HasFlag(SaveTech.Flag.EnablesPrecisionBombing));
		us.knownTechs.Add(smartWeapons.id);
		Assert.True(stealth.CanPrecisionBombTile(theirs.location));

		bool reached = WithRandom(99, 0.0, () => stealth.PrecisionBomb(theirs.location).Result);

		Assert.True(reached);
		Assert.Equal(4, theirs.residents.Count);
		Assert.DoesNotContain(theirs.constructed_buildings, cb => cb.building.name == "Granary");
	}

	[Fact]
	public void ParatroopersDropFromCitiesWithAnAirport() {
		City home = FoundCity(us);
		MapUnit para = Spawn(us, "Paratrooper", home.location);
		Tile target = EmptyLandAt(home.location, para.OperationalRange());

		Assert.False(para.CanAirdropTo(target));
		home.AddBuilding(gameData.Buildings.Single(b => b.name == "Airport"));
		Assert.True(para.CanAirdropTo(target));
		Spawn(them, "Warrior", target);
		Assert.False(para.CanAirdropTo(target));
		Tile other = EmptyLandAt(home.location, 2);

		para.Airdrop(other);

		Assert.Same(other, para.location);
		Assert.False(para.movementPoints.canMove);
	}

	[Fact]
	public void AirSuperiorityIsSaved() {
		City home = FoundCity(us);
		MapUnit fighter = Spawn(us, "Fighter", home.location);
		fighter.FlyAirSuperiority();
		fighter.hasInterceptedThisTurn = true;

		SaveUnit saved = new(fighter);

		Assert.True(saved.isOnAirSuperiority);
		Assert.True(saved.hasInterceptedThisTurn);
	}
}

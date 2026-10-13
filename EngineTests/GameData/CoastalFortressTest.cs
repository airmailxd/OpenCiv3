using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// "The fortress automatically bombards passing enemy ships" (Conquests
// Civilopedia, BLDG_Coastal_Fortress): a ship moving from one tile next to
// the city to another takes a shot, like a zone of control attack (see
// MapUnit_CoastalFortress.cs).
public class CoastalFortressTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public CoastalFortressTest(SaveGameFixture fixture) {
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

	private class FixedRandom(double sample) : System.Random {
		protected override double Sample() => sample;
	}

	private static async Task<T> WithRandomAsync<T>(double sample, System.Func<Task<T>> action) {
		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new FixedRandom(sample);
		try {
			return await action();
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	private UnitPrototype Prototype(string name) => gameData.unitPrototypes.Single(p => p.name == name);

	private Building Fortress => gameData.Buildings.Single(b => b.name == "Coastal Fortress");

	// A city of theirs with a coastal fortress, an empty water tile next to
	// it with our ship on it, and the water tiles next to the ship: one
	// also next to the city, one not.
	private (City city, MapUnit ship, Tile passing, Tile away) Setup() {
		foreach (Tile site in gameData.map.tiles) {
			if (!site.IsLand() || !site.IsAllowCities() || site.HasCity() || site.unitsOnTile.Count > 0
				|| site.neighbors.Values.Any(n => n.HasCity()) || site.GetTilesWithinRankDistance(3).Any(n => n.HasCity())) {
				continue;
			}
			foreach (Tile start in site.neighbors.Values.Where(t => t.IsWater() && t.unitsOnTile.Count == 0)) {
				Tile passing = start.neighbors.Values.FirstOrDefault(t => t.IsWater() && t.unitsOnTile.Count == 0 && site.neighbors.ContainsValue(t));
				Tile away = start.neighbors.Values.FirstOrDefault(t => t.IsWater() && t.unitsOnTile.Count == 0
					&& t != site && !site.neighbors.ContainsValue(t)
					&& !site.neighbors.Values.Any(n => n.HasCity()));
				if (passing == null || away == null) {
					continue;
				}
				City city = CityInteractions.BuildCity(site, them, them.GetNextCityName());
				city.AddBuilding(Fortress);
				us.EnsureRelationshipExists(them);
				us.DeclareWarOn(them, gameData.turn);
				MapUnit ship = gameData.SpawnUnit(us, Prototype("Destroyer"), start);
				return (city, ship, passing, away);
			}
		}
		throw new System.Exception("No coastal city site");
	}

	[Fact]
	public void TheCoastalFortressHasItsNavalPower() {
		Assert.Equal(8, Fortress.navalPower);
	}

	[Fact]
	public async Task AShipSailingPastTheCityIsHit() {
		(City city, MapUnit ship, Tile passing, _) = Setup();
		Assert.Single(ship.FindCoastalFortressesAlong(ship.location, passing));
		int hp = ship.hitPointsRemaining;

		await WithRandomAsync(0.0, () => ship.Move(ship.location.DirectionTo(passing)));

		Assert.Same(passing, ship.location);
		Assert.Equal(hp - 1, ship.hitPointsRemaining);
	}

	[Fact]
	public async Task TheShotCanMiss() {
		(_, MapUnit ship, Tile passing, _) = Setup();
		int hp = ship.hitPointsRemaining;

		await WithRandomAsync(0.999, () => ship.Move(ship.location.DirectionTo(passing)));

		Assert.Equal(hp, ship.hitPointsRemaining);
	}

	[Fact]
	public async Task OnlyMovesBetweenTilesNextToTheCityAreShotAt() {
		(_, MapUnit ship, _, Tile away) = Setup();
		Assert.Empty(ship.FindCoastalFortressesAlong(ship.location, away));
		int hp = ship.hitPointsRemaining;

		await WithRandomAsync(0.0, () => ship.Move(ship.location.DirectionTo(away)));

		Assert.Equal(hp, ship.hitPointsRemaining);
	}

	[Fact]
	public async Task TheFortressNeverSinksAShip() {
		(_, MapUnit ship, Tile passing, _) = Setup();
		ship.hitPointsRemaining = 1;

		Assert.False(await WithRandomAsync(0.0, () => ship.TakeCoastalFortressShot(ship.FindCoastalFortressesAlong(ship.location, passing).Single())));
		Assert.Equal(1, ship.hitPointsRemaining);
	}

	[Fact]
	public void LandUnitsArentShotAt() {
		(City city, _, Tile passing, _) = Setup();
		MapUnit warrior = gameData.SpawnUnit(us, Prototype("Warrior"), city.location.neighbors.Values.First(t => t.IsLand() && !t.HasCity()));
		Assert.Empty(warrior.FindCoastalFortressesAlong(warrior.location, passing));
	}
}

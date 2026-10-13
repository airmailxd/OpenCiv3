using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Anti-aircraft units (Flak, Mobile SAM, and ships like the AEGIS Cruiser)
// shoot at aircraft bombing their tile, with a tenth of the BIQ's air
// defense against the plane's defence, at most four of them (alexman,
// https://forums.civfanatics.com/threads/aerial-combat-mechanics.90783/).
public class AntiAircraftUnitTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public AntiAircraftUnitTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		us = civs[0];
		them = civs[1];
		us.EnsureRelationshipExists(them);
		us.DeclareWarOn(them, gameData.turn);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private class FixedRandom(int next, double sample) : System.Random {
		protected override double Sample() => sample;
		public override int Next(int maxValue) => System.Math.Min(next, maxValue - 1);
		public override int Next(int minValue, int maxValue) => System.Math.Clamp(next, minValue, maxValue - 1);
	}

	private static async Task<T> WithRandomAsync<T>(int next, double sample, System.Func<Task<T>> action) {
		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new FixedRandom(next, sample);
		try {
			return await action();
		} finally {
			C7GameData.GameData.rng = original;
		}
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

	private (MapUnit bomber, Tile target) BomberAndTarget() {
		City home = CityInteractions.BuildCity(us.units.First(u => u.unitType.isSettler).location, us, us.GetNextCityName());
		MapUnit bomber = Spawn(us, "Bomber", home.location);
		Tile target = gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable()
			&& home.location.DistanceTo(t) == 3 && t.unitsOnTile.Count == 0);
		return (bomber, target);
	}

	[Fact]
	public void TheBiqGivesAntiAircraftUnitsTheirAirDefense() {
		Assert.Equal(2, Prototype("Flak").airDefense);
		Assert.Equal(4, Prototype("Mobile SAM").airDefense);
		Assert.Equal(3, Prototype("AEGIS Cruiser").airDefense);
		Assert.Equal(0, Prototype("Infantry").airDefense);
	}

	[Fact]
	public async Task FlakShootsDownABomberOverItsTile() {
		(MapUnit bomber, Tile target) = BomberAndTarget();
		Spawn(them, "Infantry", target);
		Spawn(them, "Flak", target);
		Assert.True(bomber.CanAirBombTile(target));

		// The flak's real strength is 0.2 against the bomber's defence D;
		// under 0.2 / (0.2 + D) it hits.
		double odds = 0.2 / (0.2 + Prototype("Bomber").defense);
		bool reached = await WithRandomAsync(0, odds * 0.9, () => bomber.AirBomb(target));
		Assert.False(reached);
		Assert.DoesNotContain(bomber, us.units);
	}

	[Fact]
	public void APlaneTheFlakMissesGoesOnToBomb() {
		(MapUnit bomber, Tile target) = BomberAndTarget();
		Spawn(them, "Infantry", target);
		Spawn(them, "Flak", target);
		double odds = 0.2 / (0.2 + Prototype("Bomber").defense);

		Assert.False(WithRandom(0, odds * 1.1, () => bomber.TryToIntercept(target, out _)));
		Assert.Contains(bomber, us.units);
	}

	[Fact]
	public void OnlyTheFourStrongestOnTheTargetTileShoot() {
		(MapUnit bomber, Tile target) = BomberAndTarget();
		for (int i = 0; i < 3; i++) {
			Spawn(them, "Flak", target);
		}
		for (int i = 0; i < 2; i++) {
			Spawn(them, "Mobile SAM", target);
		}
		// Not one next door, nor one of ours, nor one of a civ at peace.
		Tile next = target.neighbors.Values.First(t => t.IsLand() && !t.HasCity());
		Spawn(them, "Mobile SAM", next);
		Spawn(us, "Mobile SAM", target);

		var shooters = bomber.AntiAircraftUnits(target);
		Assert.Equal(4, shooters.Count);
		Assert.Equal(new[] { 4, 4, 2, 2 }, shooters.Select(u => u.unitType.airDefense));
		Assert.All(shooters, u => Assert.Same(them, u.owner));
	}

	[Fact]
	public void StealthDoesntHelpAgainstFlak() {
		(MapUnit bomber, Tile target) = BomberAndTarget();
		MapUnit stealth = Spawn(us, "Stealth Bomber", bomber.location);
		Spawn(them, "Mobile SAM", target);
		double odds = 0.4 / (0.4 + Prototype("Stealth Bomber").defense);

		// A roll of 99 would fail any interception chance; the SAM still
		// shoots.
		Assert.True(WithRandom(99, odds * 0.9, () => stealth.TryToIntercept(target, out bool down) && down));
	}
}

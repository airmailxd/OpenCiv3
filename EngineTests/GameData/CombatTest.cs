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
		Assert.Equal(startingLevel, warrior.experienceLevel);
		Assert.Equal(startingHitPoints, warrior.hitPointsRemaining);
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
}

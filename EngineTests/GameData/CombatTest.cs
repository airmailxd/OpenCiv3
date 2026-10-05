using System.Linq;
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

		Player[] civs = gameData.players.Where(p => !p.isBarbarians).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
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
}

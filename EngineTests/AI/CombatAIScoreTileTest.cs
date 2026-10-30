using System;
using System.Linq;
using C7Engine;
using C7Engine.AI.UnitAI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// A tile can hold enemy units yet have no top defender (FindTopDefender then
// gives MapUnit.NONE, which is not null and has no unitType). Scoring such a
// tile used to crash the AI turn with a NullReferenceException.
public sealed class CombatAIScoreTileTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player ai;
	private readonly Player enemy;

	public CombatAIScoreTileTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		ai = civs[0];
		enemy = civs[1];
		ai.EnsureRelationshipExists(enemy);
		ai.DeclareWarOn(enemy, gameData.turn);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	[Fact]
	public void TileWithEnemyUnitsButNoDefenderDoesNotCrash() {
		Tile site = gameData.map.tiles.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& !t.hasBarbarianCamp && !t.IsImpassable() && t.OwningPlayer() == null
			&& t.neighbors.Values.Any(n => n != Tile.NONE && n.IsLand() && n.unitsOnTile.Count == 0));
		MapUnit target = gameData.SpawnUnit(enemy, gameData.unitPrototypes.Single(p => p.name == "Fighter"), site);
		Tile next = site.neighbors.Values.First(n => n != Tile.NONE && n.IsLand() && n.unitsOnTile.Count == 0);
		MapUnit attacker = gameData.SpawnUnit(ai, gameData.unitPrototypes.Single(p => p.name == "Swordsman"), next);
		Assert.Same(MapUnit.NONE, site.FindTopDefender(attacker));

		CombatAI.ScoreTile(site, attacker, ai, new System.Collections.Generic.HashSet<Player> { enemy });
	}
}

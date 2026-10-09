using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The AI fog of war testing option: off, the AI sees everything; on, it
// plans only with what it knows.
public sealed class AIFogOfWarTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player ai;
	private readonly Player enemy;

	public AIFogOfWarTest(SaveGameFixture fixture) {
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
		EngineStorage.aiFogOfWar = false;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsEmptyLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.OwningPlayer() == null;
	}

	// A stretch of empty land, with an enemy warrior three tiles from one
	// of ours. Our civ knows all the tiles around, but only sees those
	// next to its warrior.
	private (MapUnit ours, MapUnit theirs) Standoff() {
		Tile start = gameData.map.tiles.First(t => IsEmptyLand(t)
			&& t.baseTerrainType.Key != "hills" && t.baseTerrainType.Key != "mountains"
			&& t.GetTilesWithinTileSquare(3).All(n => n != Tile.NONE && IsEmptyLand(n)));
		Tile target = start.GetTilesWithinTileSquare(3).First(t => t.DistanceTo(start) == 3);
		foreach (Tile t in start.GetTilesWithinTileSquare(3)) {
			ai.tileKnowledge.knownTiles.Add(t);
		}
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");
		MapUnit ours = gameData.SpawnUnit(ai, warrior, start);
		MapUnit theirs = gameData.SpawnUnit(enemy, warrior, target);
		return (ours, theirs);
	}

	private Tile CombatDestination(MapUnit unit) {
		C7GameData.AIData.CombatAIData data = C7Engine.AI.UnitAI.CombatAI.MakeAiData(unit, ai);
		return data?.destination;
	}

	[Fact]
	public void WithoutTheFogTheAIAttacksUnitsItCantSee() {
		(MapUnit ours, MapUnit theirs) = Standoff();
		ai.tileKnowledge.RecomputeActiveTiles();
		Assert.False(ai.tileKnowledge.isActiveTile(theirs.location));
		Assert.Equal(theirs.location, CombatDestination(ours));
	}

	[Fact]
	public void WithTheFogTheAIOnlyAttacksUnitsItSees() {
		(MapUnit ours, MapUnit theirs) = Standoff();
		EngineStorage.aiFogOfWar = true;
		Assert.Null(CombatDestination(ours));

		// Once it can see the enemy, it goes for it. Its new unit stands on a
		// tile it has explored, next to the enemy (a unit on an unexplored
		// tile sees nothing).
		Tile next = theirs.location.neighbors.Values.First(t => t != Tile.NONE && IsEmptyLand(t)
			&& ai.tileKnowledge.isTileKnown(t));
		gameData.SpawnUnit(ai, gameData.unitPrototypes.First(p => p.name == "Warrior"), next);
		Assert.Equal(theirs.location, CombatDestination(ours));
	}

	[Fact]
	public void WithTheFogUnexploredTilesAreWorthNothingAndUnknownUnitsArentCounted() {
		(MapUnit ours, MapUnit theirs) = Standoff();
		Tile unexplored = gameData.map.tiles.First(t => IsEmptyLand(t) && !ai.tileKnowledge.isTileKnown(t));

		Assert.True(AIFogOfWar.KnowsTile(ai, unexplored));
		Assert.True(AIFogOfWar.SeesUnitsOn(ai, theirs.location));
		Assert.Equal(1, NuclearAI.TargetValue(ai, theirs.location));

		EngineStorage.aiFogOfWar = true;
		AIFogOfWar.Refresh(ai);
		Assert.False(AIFogOfWar.KnowsTile(ai, unexplored));
		Assert.False(AIFogOfWar.SeesUnitsOn(ai, theirs.location));
		Assert.Equal(0, NuclearAI.TargetValue(ai, theirs.location));
		// Humans are never planned for, so it doesn't apply to them.
		Player human = gameData.players.First(p => p.isHuman);
		Assert.True(AIFogOfWar.KnowsTile(human, unexplored));
	}

	[Fact]
	public void WithTheFogTheAIPathsThroughUnexploredTilesLikeAHuman() {
		Assert.False(ai.PathsByExploredMap);
		EngineStorage.aiFogOfWar = true;
		Assert.True(ai.PathsByExploredMap);
		Assert.False(gameData.players.First(p => p.isBarbarians).PathsByExploredMap);
	}
}

using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.UnitAI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// An AI attacker goes for a city (or a stack of defenders) only when our
// attackers near it can, together, expect to beat every defender, or when
// it's very likely to win its own fight (see CombatAI).
public sealed class AttackForceTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player ai;
	private readonly Player enemy;

	public AttackForceTest(SaveGameFixture fixture) {
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

	private static bool IsFlatEmptyLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.OwningPlayer() == null
			&& (t.overlayTerrainType.Key == "grassland" || t.overlayTerrainType.Key == "plains");
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	// An enemy city on open flat land held by two spearmen, with one of our
	// swordsmen next to it, and the tiles around known to us.
	private (Tile city, MapUnit sword) EnemyCityAndAnAttacker() {
		Tile site = gameData.map.tiles.First(t => IsFlatEmptyLand(t) && t.IsAllowCities()
			&& t.GetTilesWithinTileSquare(2).All(n => n != Tile.NONE && n.IsLand() && !n.HasCity() && n.unitsOnTile.Count == 0
				&& n.OwningPlayer() == null && !n.IsImpassable()));
		CityInteractions.BuildCity(site, enemy, "Target");
		for (int i = 0; i < 2; ++i) {
			gameData.SpawnUnit(enemy, Prototype("Spearman"), site).Fortify();
		}
		foreach (Tile t in site.GetTilesWithinTileSquare(3)) {
			ai.tileKnowledge.knownTiles.Add(t);
		}
		Tile next = site.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && t.unitsOnTile.Count == 0);
		return (site, gameData.SpawnUnit(ai, Prototype("Swordsman"), next));
	}

	[Fact]
	public void ALoneAttackerLeavesACityItCantTakeAlone() {
		(Tile city, MapUnit sword) = EnemyCityAndAnAttacker();

		// It could win its own fight, but not take the city.
		MapUnit defender = city.FindTopDefender(sword);
		double odds = CombatAI.EstimateChanceToWin(sword, defender);
		Assert.InRange(odds, 0.3, 0.7);

		Assert.NotEqual(city, CombatAI.MakeAiData(sword, ai)?.destination);
	}

	[Fact]
	public void AttackersTogetherGoForTheCity() {
		(Tile city, MapUnit sword) = EnemyCityAndAnAttacker();
		for (int i = 0; i < 4; ++i) {
			gameData.SpawnUnit(ai, Prototype("Swordsman"), sword.location);
		}

		Assert.Equal(city, CombatAI.MakeAiData(sword, ai)?.destination);
	}

	// The odds of an attack count a river crossing: a defender across a
	// river is harder to beat than one on the same side.
	[Fact]
	public void TheOddsCountARiverCrossing() {
		(Tile defended, Tile across, Tile dry) = gameData.map.tiles
			.Where(t => IsFlatEmptyLand(t))
			.Select(t => (t,
				t.neighbors.Values.FirstOrDefault(n => IsFlatEmptyLand(n) && CombatAI.CrossesRiver(n, t)),
				t.neighbors.Values.FirstOrDefault(n => IsFlatEmptyLand(n) && !CombatAI.CrossesRiver(n, t))))
			.First(x => x.Item2 != null && x.Item3 != null);
		MapUnit defender = gameData.SpawnUnit(enemy, Prototype("Spearman"), defended);
		MapUnit fromAcross = gameData.SpawnUnit(ai, Prototype("Swordsman"), across);
		MapUnit fromDry = gameData.SpawnUnit(ai, Prototype("Swordsman"), dry);
		Assert.True(CombatAI.EstimateChanceToWin(fromAcross, defender) < CombatAI.EstimateChanceToWin(fromDry, defender));
	}

	// An attacker goes after a barbarian it can beat nearby.
	[Fact]
	public void AnAttackerHuntsANearbyBarbarian() {
		Player barbarians = gameData.players.Single(p => p.isBarbarians);
		MapUnit hunter = gameData.map.tiles.Where(IsFlatEmptyLand).Select(t => gameData.SpawnUnit(ai, Prototype("Swordsman"), t)).First();
		Tile prey = hunter.location.GetTilesWithinRankDistance(3).First(t => IsFlatEmptyLand(t) && t.DistanceTo(hunter.location) == 3);
		gameData.SpawnUnit(barbarians, Prototype("Warrior"), prey);
		ai.tileKnowledge.AddTilesToKnown(prey);
		C7GameData.UnitAI plan = PlayerAI.GetAIForUnit(hunter, ai);
		CombatAI combat = Assert.IsType<CombatAI>(plan);
		Assert.Equal(prey, combat.data.destination);
	}
}

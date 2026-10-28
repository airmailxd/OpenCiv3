using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI.UnitAI;
using C7GameData;
using C7GameData.AIData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// Rules from the review of the unit AI (see CombatAI, WorkerAI, DefenderAI
// and UnitAiExtension).
public sealed class AIRulesTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player ai;
	private readonly Player enemy;
	private readonly Tile home;

	public AIRulesTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		ai = civs[0];
		enemy = civs[1];
		ai.EnsureRelationshipExists(enemy);
		home = gameData.map.tiles.First(t => t.IsAllowCities() && HasRoom(t));
		foreach (Tile t in home.GetTilesWithinTileSquare(7)) {
			ai.tileKnowledge.knownTiles.Add(t);
		}
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	// Room for the tests: open land about home and along the row east of
	// it, where they put the other civ's units and cities. (Civ3's maps
	// have little land as open as a whole 13 by 13 square about a tile.)
	private static bool HasRoom(Tile home) {
		Tile t = home;
		for (int i = 0; i <= 6; ++i, t = t.neighbors[TileDirection.EAST]) {
			if (!t.GetTilesWithinTileSquare(i == 0 ? 3 : 2).All(IsOpenLand)) {
				return false;
			}
		}
		return IsOpenLand(home.neighbors[TileDirection.WEST]);
	}

	private static bool IsOpenLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.OwningPlayer() == null && !t.HasRoad();
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private static Tile Step(Tile from, TileDirection direction, int steps = 1) {
		Tile t = from;
		for (int i = 0; i < steps; ++i) {
			t = t.neighbors[direction];
		}
		return t;
	}

	[Fact]
	public void AWorkerJoinsAnUnconnectedCityToTheCapitalByRoad() {
		City capital = CityInteractions.BuildCity(home, ai, "Capital");
		capital.capital = true;
		Tile other = Step(home, TileDirection.EAST, 4);
		CityInteractions.BuildCity(other, ai, "Other");
		gameData.InvalidateCachedTradeNetwork();
		// A quarter of the workers road: with four, one.
		for (int i = 0; i < 3; ++i) {
			MapUnit fellow = gameData.SpawnUnit(ai, Prototype("Worker"), home);
			fellow.currentAI = new WorkerAI(WorkerAI.MakeAiData(fellow, ai));
		}
		MapUnit worker = gameData.SpawnUnit(ai, Prototype("Worker"), home);

		WorkerAIData plan = WorkerAI.MakeAiData(worker, ai);

		Assert.NotNull(plan);
		Assert.True(plan.workerMove.ProvidesRoad());
		// On the way between the two cities.
		Assert.True(plan.destination.DistanceTo(home) < 4 && plan.destination.DistanceTo(other) < 4);
	}

	[Fact]
	public async Task ADefenderGivesUpGoingToACityThatWasLost() {
		City city = CityInteractions.BuildCity(Step(home, TileDirection.EAST, 3), ai, "Frontier");
		MapUnit spear = gameData.SpawnUnit(ai, Prototype("Spearman"), home);
		DefenderAIData data = DefenderAI.MakeAiDataForDefendAtRiskCity(spear, ai, int.MaxValue);
		Assert.Equal(city.location, data.destination);
		spear.currentAI = new DefenderAI(data);

		city.owner = enemy;
		ai.cities.Remove(city);
		enemy.cities.Add(city);

		Assert.Equal(C7GameData.UnitAI.Result.Error, await spear.currentAI.PlayTurn(ai, spear));
		Assert.Equal(home, spear.location);
	}
}

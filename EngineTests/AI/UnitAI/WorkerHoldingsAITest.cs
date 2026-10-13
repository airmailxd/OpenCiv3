using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI.UnitAI;
using C7GameData;
using C7GameData.AIData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.UnitAI;

// AI workers founding colonies and building outposts (see WorkerHoldingsAI).
public class WorkerHoldingsAITest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly TerrainImprovement road;

	public WorkerHoldingsAITest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler)).ToArray();
		us = civs[0];
		them = civs[1];
		road = gameData.terrainImprovements.Single(ti => ti.key == Tile.TileOverlays.ROAD);
		// The tests count the workers they give us.
		foreach (MapUnit u in us.units.Where(u => u.unitType.isWorker).ToList()) {
			u.RemoveFromPlay();
		}
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Worker => gameData.unitPrototypes.Single(p => p.name == "Worker");

	// A city of ours, a line of three land tiles leading away from it, and a
	// luxury we know of on the last, outside the city's borders.
	private (City city, List<Tile> line, Resource luxury) CityWithLuxuryThreeAway() {
		Resource luxury = gameData.Resources.First(r => r.Category == ResourceCategory.LUXURY);
		if (luxury.Prerequisite != null) {
			us.knownTechs.Add(luxury.Prerequisite);
		}
		foreach (Tile site in gameData.map.tiles) {
			if (!site.IsLand() || !site.IsAllowCities() || site.OwningPlayer() != null
				|| site.GetTilesWithinRankDistance(6).Any(n => n.HasCity() || n.OwningPlayer() != null || n.unitsOnTile.Count > 0)) {
				continue;
			}
			List<Tile> line = new();
			Tile t = site;
			for (int i = 0; i < 3; ++i) {
				t = t.neighbors[TileDirection.NORTHEAST];
				if (t == Tile.NONE || !t.IsLand() || t.IsImpassable()) {
					break;
				}
				line.Add(t);
			}
			if (line.Count < 3 || line[2].hasGoodyHut || line[2].hasBarbarianCamp) {
				continue;
			}
			City city = CityInteractions.BuildCity(site, us, "Silkton");
			if (line[2].OwningPlayer() != null) {
				continue;
			}
			foreach (Tile known in site.GetTilesWithinRankDistance(8)) {
				us.tileKnowledge.knownTiles.Add(known);
				// Only our luxury is worth a colony.
				known.Resource = Resource.NONE;
			}
			line[2].Resource = luxury;
			line[2].ResourceKey = luxury.Key;
			gameData.InvalidateCachedTradeNetwork();
			return (city, line, luxury);
		}
		throw new InvalidOperationException("no site for a city with a luxury three tiles away");
	}

	[Fact]
	public void AWorkerIsSentToFoundAColonyOnAMissingLuxury() {
		(City city, List<Tile> line, Resource _) = CityWithLuxuryThreeAway();
		List<MapUnit> workers = new();
		for (int i = 0; i < WorkerHoldingsAI.ColonyMinWorkers; ++i) {
			workers.Add(gameData.SpawnUnit(us, Worker, city.location));
		}
		WorkerAIData plan = WorkerAI.MakeAiData(workers[0], us);
		Assert.True(plan.buildColony);
		Assert.Equal(line[2], plan.destination);

		// Only one worker goes at a time.
		workers[0].currentAI = new WorkerAI(plan);
		Assert.False(WorkerAI.MakeAiData(workers[1], us)?.buildColony ?? false);
	}

	[Fact]
	public void NoColonyWithoutWorkersToSpare() {
		(City city, List<Tile> _, Resource _) = CityWithLuxuryThreeAway();
		for (int i = 0; i < WorkerHoldingsAI.ColonyMinWorkers - 1; ++i) {
			gameData.SpawnUnit(us, Worker, city.location);
		}
		MapUnit worker = us.units.First(u => u.unitType.isWorker && u.location == city.location);
		Assert.False(WorkerAI.MakeAiData(worker, us)?.buildColony ?? false);
	}

	[Fact]
	public async System.Threading.Tasks.Task TheWorkerRoadsAndFoundsTheColony() {
		(City city, List<Tile> line, Resource luxury) = CityWithLuxuryThreeAway();
		line[0].overlays.Add(road);
		line[1].overlays.Add(road);
		for (int i = 0; i < WorkerHoldingsAI.ColonyMinWorkers - 1; ++i) {
			gameData.SpawnUnit(us, Worker, city.location);
		}
		MapUnit worker = gameData.SpawnUnit(us, Worker, line[2]);
		WorkerAIData plan = WorkerAI.MakeAiData(worker, us);
		Assert.True(plan.buildColony);
		WorkerAI ai = new(plan);
		worker.currentAI = ai;

		// The first turn roads the tile, and a later one founds the colony.
		for (int turn = 0; turn < 10 && us.units.Contains(worker); ++turn) {
			await ((C7GameData.UnitAI)ai).PlayTurn(us, worker);
			if (us.units.Contains(worker)) {
				worker.PerformEndOfTurnAction();
				worker.movementPoints.reset(worker.MaxMovementPoints());
			}
		}
		Assert.DoesNotContain(worker, us.units);
		Assert.True(line[2].HasRoad());
		Assert.Same(us, line[2].ColonyOwner());
		Assert.True(gameData.GetTradeNetwork().GetResourcesAvailableToCity(us, city).ContainsKey(luxury));
	}

	[Fact]
	public void SpareWorkersBuildAnOutpostOnAHillFacingARival() {
		Terraform outpost = gameData.Terraforms.Single(t => t.Name == "Outpost");
		us.knownTechs.Add(outpost.RequiredTech);
		TerrainType hills = gameData.terrainTypes.First(t => t.Key == "hills");

		// Our city and a rival's, near enough that their borders meet.
		(City city, List<Tile> line, Resource _) = CityWithLuxuryThreeAway();
		// No colony to found instead.
		line[2].Resource = Resource.NONE;
		gameData.InvalidateCachedTradeNetwork();
		Tile rivalSite = city.location.GetTilesWithinRankDistance(5)
			.First(t => t.RankDistanceTo(city.location) == 5 && t.IsLand() && t.IsAllowCities() && !t.HasCity());
		CityInteractions.BuildCity(rivalSite, them, "Rivalton");
		Tile hill = city.location.GetTilesWithinRankDistance(3).FirstOrDefault(t => t.OwningPlayer() == us && !t.HasCity() && t.IsLand()
			&& !t.IsImpassable() && t.GetTilesWithinRankDistance(WorkerHoldingsAI.OutpostRivalDistance).Any(n => n.OwningPlayer() == them));
		Assert.NotNull(hill);
		hill.baseTerrainType = hills;
		hill.overlayTerrainType = hills;

		List<MapUnit> workers = new();
		for (int i = 0; i < WorkerHoldingsAI.OutpostMinWorkers - 1; ++i) {
			workers.Add(gameData.SpawnUnit(us, Worker, city.location));
		}
		// With too few workers, there's no outpost.
		Assert.NotEqual(outpost, WorkerAI.MakeAiData(workers[0], us)?.workerMove);

		workers.Add(gameData.SpawnUnit(us, Worker, city.location));
		WorkerAIData plan = WorkerAI.MakeAiData(workers[0], us);
		Assert.Equal(outpost, plan.workerMove);
		// Our hill, or another like it.
		Tile site = plan.destination;
		Assert.True(site.overlayTerrainType.height >= 2);
		Assert.Same(us, site.OwningPlayer());
		Assert.Contains(site.GetTilesWithinRankDistance(WorkerHoldingsAI.OutpostRivalDistance), n => n.OwningPlayer() == them);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.AIData;
using EngineTests.GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// Kings & Nomads: the AI's workers road their cities to the city-states,
// and go out of our land only with an escort (see CityStateRoadAI). None
// of it touches an ordinary game.
public sealed class CityStateRoadAITest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public CityStateRoadAITest(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private sealed class World {
		public C7GameData.GameData gameData;
		public Player civ;
		public City cityState;
		public City city;
		public MapUnit worker;
		public MapUnit garrison;
	}

	private static UnitPrototype Prototype(C7GameData.GameData gameData, string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private static TerrainImprovement Road(C7GameData.GameData gameData) {
		return gameData.terrainImprovements.Single(ti => ti.key == Tile.TileOverlays.ROAD);
	}

	private static bool IsOpenLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && !t.HasCity() && !t.IsImpassable() && !t.hasBarbarianCamp && !t.HasRoad();
	}

	// A city of a civ's four tiles from a city-state on open land, held by
	// a Spearman, with a Worker in it. The civ's other units are far away,
	// and the land around has no resources, so that the road is the
	// worker's first job.
	private static World NewWorld() {
		for (int seed = 1; seed < 8; ++seed) {
			(_, C7GameData.GameData gameData) = KingsAndNomadsScenarioTest.NewGame(seed);
			EngineStorage.InitializeGameDataForTests(gameData);
			EngineStorage.animationsEnabled = false;
			EngineStorage.aiFogOfWar = false;
			foreach (Player p in gameData.players.Where(p => !p.isBarbarians)) {
				p.isHuman = false;
			}
			Player independents = gameData.players.Single(p => p.isBarbarians);
			foreach (City cityState in independents.cities) {
				Tile cs = cityState.location;
				Player civ = gameData.players.Where(p => !p.isBarbarians)
					.FirstOrDefault(p => p.units.All(u => u.location.DistanceTo(cs) > 8));
				if (civ == null || cs.GetTilesWithinRankDistance(6).Any(t => t != Tile.NONE && t != cs && (t.HasCity() || t.unitsOnTile.Count > 0))) {
					continue;
				}
				Tile site = cs.GetTilesWithinRankDistance(4).FirstOrDefault(t => t.DistanceTo(cs) == 4 && IsOpenLand(t) && t.IsAllowCities()
					&& t.OwningPlayer() == null && t.continent == cs.continent
					&& t.GetTilesWithinRankDistance(4).All(n => n == Tile.NONE || n.HasCity() || n.IsWater() || IsOpenLand(n)));
				if (site == null) {
					continue;
				}
				World w = new() { gameData = gameData, civ = civ, cityState = cityState };
				w.city = CityInteractions.BuildCity(site, civ, "Trader");
				gameData.UpdateTileOwners();
				foreach (Tile t in cs.GetTilesWithinRankDistance(8)) {
					if (t != Tile.NONE) {
						civ.tileKnowledge.knownTiles.Add(t);
						t.Resource = Resource.NONE;
					}
				}
				gameData.InvalidateCachedTradeNetwork();
				w.garrison = gameData.SpawnUnit(civ, Prototype(gameData, "Spearman"), site);
				w.worker = gameData.SpawnUnit(civ, Prototype(gameData, "Worker"), site);
				return w;
			}
		}
		throw new InvalidOperationException("No city-state with open land around it");
	}

	private static void BuildRoad(World w, Tile t) {
		t.overlays.Add(Road(w.gameData));
		w.gameData.InvalidateCachedTradeNetwork();
	}

	[Fact]
	public void OnlyTheScenarioRoadsToCityStates() {
		World w = NewWorld();
		Assert.True(CityStateRoadAI.IsOn(w.civ));

		C7GameData.GameData ordinary = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(ordinary);
		Player ai = ordinary.players.First(p => !p.isBarbarians && !p.isHuman);
		Assert.False(CityStateRoadAI.IsOn(ai));
		MapUnit worker = ai.units.FirstOrDefault(u => u.unitType.isWorker) ?? ai.units.First(u => u.IsLandUnit());
		Assert.Empty(CityStateRoadAI.ComputeRoadTiles(ai, worker));
	}

	// The road-less tiles listed are a road that joins the city-state to
	// our city: once built, the two trade.
	[Fact]
	public void TheRoadTilesJoinTheCityStateToOurCity() {
		World w = NewWorld();
		List<Tile> tiles = CityStateRoadAI.ComputeRoadTiles(w.civ, w.worker);
		Assert.NotEmpty(tiles);
		Assert.True(tiles.Count <= CityStateRoadAI.MaxRoadTiles);
		Assert.All(tiles, t => Assert.False(t.HasRoad() || t.HasCity()));
		// From our city outwards.
		Assert.True(tiles.First().DistanceTo(w.city.location) <= tiles.Last().DistanceTo(w.city.location));
		Assert.Empty(w.gameData.GetTradeNetwork().ConnectedCityStates(w.civ));

		foreach (Tile t in tiles) {
			BuildRoad(w, t);
		}
		Assert.Contains(w.cityState, w.gameData.GetTradeNetwork().ConnectedCityStates(w.civ));
		// Then there's nothing left to build.
		Assert.Empty(CityStateRoadAI.ComputeRoadTiles(w.civ, w.worker));
	}

	// With a spare soldier in the city, a worker whose road tile is outside
	// our land takes it as an escort, and the city keeps its garrison.
	[Fact]
	public void AWorkerTakesAnEscortOutOfOurLand() {
		World w = NewWorld();
		foreach (Tile t in CityStateRoadAI.ComputeRoadTiles(w.civ, w.worker).Where(t => !CityStateRoadAI.IsOutside(w.civ, t))) {
			BuildRoad(w, t);
		}
		MapUnit spare = w.gameData.SpawnUnit(w.civ, Prototype(w.gameData, "Spearman"), w.city.location);

		WorkerAIData plan = WorkerAI.MakeAiData(w.worker, w.civ);
		Assert.NotNull(plan);
		Assert.True(plan.cityStateRoad);
		Assert.True(CityStateRoadAI.IsOutside(w.civ, plan.destination));
		Assert.True(plan.needsEscort);
		Assert.Contains(plan.escort, new[] { spare, w.garrison });
		EscortAI escortAi = Assert.IsType<EscortAI>(plan.escort.currentAI);
		Assert.Same(w.worker, escortAi.data.unitToEscort);
		MapUnit stays = plan.escort == spare ? w.garrison : spare;
		Assert.IsNotType<EscortAI>(stays.currentAI);
	}

	// With no soldier to spare, the worker doesn't leave our land.
	[Fact]
	public void WithoutAnEscortAWorkerStaysInOurLand() {
		World w = NewWorld();
		foreach (Tile t in CityStateRoadAI.ComputeRoadTiles(w.civ, w.worker).Where(t => !CityStateRoadAI.IsOutside(w.civ, t))) {
			BuildRoad(w, t);
		}

		WorkerAIData plan = WorkerAI.MakeAiData(w.worker, w.civ);
		Assert.True(plan == null || !CityStateRoadAI.LeavesOurLand(w.civ, plan));
		Assert.False(plan?.needsEscort ?? false);
		Assert.IsNotType<EscortAI>(w.garrison.currentAI);
	}

	// The escort has the worker move first and goes with it; the worker
	// doesn't step out of our land while its escort is elsewhere.
	[Fact]
	public async Task TheEscortAndTheWorkerGoTogether() {
		World w = NewWorld();
		foreach (Tile t in CityStateRoadAI.ComputeRoadTiles(w.civ, w.worker).Where(t => !CityStateRoadAI.IsOutside(w.civ, t))) {
			BuildRoad(w, t);
		}
		MapUnit spare = w.gameData.SpawnUnit(w.civ, Prototype(w.gameData, "Spearman"), w.city.location);
		WorkerAIData plan = WorkerAI.MakeAiData(w.worker, w.civ);
		Assert.True(plan.needsEscort);
		w.worker.currentAI = new WorkerAI(plan);
		MapUnit escort = plan.escort;

		// The worker goes on alone within our land, but waits there for an
		// escort not with it.
		Tile away = w.city.location.neighbors.Values.First(t => t != Tile.NONE && IsOpenLand(t) && !CityStateRoadAI.IsOutside(w.civ, t)
			&& !plan.pathToDestination.path.Contains(t));
		escort.location.unitsOnTile.Remove(escort);
		away.unitsOnTile.Add(escort);
		escort.location = away;
		await w.worker.currentAI.PlayTurn(w.civ, w.worker);
		Assert.False(CityStateRoadAI.IsOutside(w.civ, w.worker.location));
		Assert.False(w.worker.movementPoints.canMove);

		// Next turn the escort joins it, and from then on they go together.
		for (int turn = 0; turn < 6 && w.worker.location != plan.destination; ++turn) {
			w.worker.movementPoints.reset(w.worker.MaxMovementPoints());
			escort.movementPoints.reset(escort.MaxMovementPoints());
			++w.gameData.turn;
			await escort.currentAI.PlayTurn(w.civ, escort);
			if (CityStateRoadAI.IsOutside(w.civ, w.worker.location)) {
				Assert.Equal(w.worker.location, escort.location);
			}
		}
		Assert.Equal(plan.destination, w.worker.location);
		Assert.Equal(w.worker.location, escort.location);
	}

	// A city-state's garrison stays in its city: it doesn't attack an
	// escorted worker next to it.
	[Fact]
	public async Task TheGarrisonLeavesAnEscortedWorkerAlone() {
		World w = NewWorld();
		Tile next = w.cityState.location.neighbors.Values.First(t => t != Tile.NONE && IsOpenLand(t));
		MapUnit spearman = w.gameData.SpawnUnit(w.civ, Prototype(w.gameData, "Spearman"), next);
		MapUnit worker = w.gameData.SpawnUnit(w.civ, Prototype(w.gameData, "Worker"), next);
		Player independents = w.gameData.players.Single(p => p.isBarbarians);
		int garrison = w.cityState.location.unitsOnTile.Count(u => u.owner == independents);

		await BarbarianAI.PlayTurn(independents, w.gameData);
		Assert.Same(w.civ, worker.owner);
		Assert.Contains(spearman, w.civ.units);
		Assert.Equal(garrison, w.cityState.location.unitsOnTile.Count(u => u.owner == independents));
	}
}

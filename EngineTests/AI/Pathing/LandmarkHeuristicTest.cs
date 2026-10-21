using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.AI.Pathing;

// The landmark heuristic must only change which of several equally cheap
// paths a search returns: on a generated map covered in roads and
// railroads, every search must find a path exactly when the plain search
// does, and one that costs the same.
public sealed class LandmarkHeuristicTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly ITestOutputHelper output;

	public LandmarkHeuristicTest(SaveGameFixture fixture, ITestOutputHelper output) {
		this.output = output;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public void Dispose() {
		AStarAlgorithm.UseLandmarks = true;
	}

	// The cost of the path as A* measures it.
	private static double PathCost(Tile start, TilePath path, MapUnit unit) {
		double cost = 0;
		Tile from = start;
		foreach (Tile to in path.path) {
			cost += UnitWalker.EdgeCost(unit.owner, from, from.DirectionTo(to), to, unit.MaxMovementPoints());
			from = to;
		}
		return cost;
	}

	private TerrainImprovement Improvement(string key) {
		return gameData.Terraforms.Select(t => t.Improvement).First(i => i != null && i.layer == TerrainImprovement.Layer.Roads && i.key == key);
	}

	private void Compare(List<MapUnit> units, List<Tile> land, Random rng, int pairs, ref int compared, ref int differentPaths) {
		AStarAlgorithm algorithm = null;
		for (int i = 0; i < pairs; ++i) {
			MapUnit unit = units[rng.Next(units.Count)];
			Tile start = land[rng.Next(land.Count)];
			// Mostly nearby goals, as the AI's are, and some far away.
			Tile goal = rng.Next(4) == 0
				? land[rng.Next(land.Count)]
				: land.Where(t => t.DistanceTo(start) <= 12).OrderBy(_ => rng.Next()).FirstOrDefault() ?? start;
			algorithm = new AStarAlgorithm(unit);

			AStarAlgorithm.UseLandmarks = false;
			TilePath plain = algorithm.PathFrom(start, goal, unit);
			AStarAlgorithm.UseLandmarks = true;
			TilePath withLandmarks = algorithm.PathFrom(start, goal, unit);

			Assert.Equal(plain.PathLength() > 0, withLandmarks.PathLength() > 0);
			if (plain.PathLength() > 0) {
				double a = PathCost(start, plain, unit);
				double b = PathCost(start, withLandmarks, unit);
				// Step costs are floats (a road step is 1/3), so two routes of
				// equal cost can sum a few billionths apart.
				Assert.True(Math.Abs(a - b) <= 1e-6, $"{unit.unitType.name} from {start} to {goal}: {a} without landmarks, {b} with");
				if (!plain.path.SequenceEqual(withLandmarks.path)) {
					++differentPaths;
				}
				++compared;
			}
		}
	}

	[Fact]
	public void FindsPathsAsCheapAsThePlainSearch() {
		Random rng = new(7);
		TerrainImprovement road = Improvement(Tile.TileOverlays.ROAD);
		TerrainImprovement railroad = Improvement(Tile.TileOverlays.RAILROAD);
		List<Tile> land = gameData.map.tiles.Where(t => t.IsLand() && !t.HasCity()).ToList();
		foreach (Tile t in land) {
			double r = rng.NextDouble();
			if (r < 0.25) {
				t.overlays.Add(railroad);
			} else if (r < 0.6) {
				t.overlays.Add(road);
			}
		}

		Player ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
		List<MapUnit> units = new();
		foreach (string name in new[] { "Warrior", "Horseman", "Knight", "Cavalry", "Worker" }) {
			UnitPrototype proto = gameData.unitPrototypes.FirstOrDefault(p => p.name == name);
			if (proto != null) {
				units.Add(gameData.SpawnUnit(ai, proto, land[rng.Next(land.Count)]));
			}
		}
		Assert.Contains(units, u => u.MaxMovementPoints() > 1);

		int compared = 0;
		int differentPaths = 0;
		Compare(units, land, rng, 400, ref compared, ref differentPaths);

		// Change the roads after the landmark distances were worked out:
		// new railroads make steps cheaper, removed roads dearer.
		for (int round = 0; round < 4; ++round) {
			foreach (Tile t in land.OrderBy(_ => rng.Next()).Take(land.Count / 10)) {
				TerrainImprovement current = t.overlays.ImprovementAtLayer(TerrainImprovement.Layer.Roads);
				if (current != null && rng.Next(3) == 0) {
					t.overlays.Remove(current);
				} else {
					t.overlays.Add(rng.Next(2) == 0 ? railroad : road);
				}
			}
			Compare(units, land, rng, 200, ref compared, ref differentPaths);
		}

		output.WriteLine($"{compared} paths compared, {differentPaths} took a different (equally cheap) way");
		Assert.True(compared > 500);
	}
}

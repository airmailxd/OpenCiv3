using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.Pathing;

// Checks the optimized A* against a straightforward reference Dijkstra on
// generated maps: both must agree on reachability and on path cost.
public sealed class PerfPathingTests : MapBase {
	private static readonly TerrainType plains = new() { Key = "plains", movementCost = 1 };
	private static readonly TerrainType hills = new() { Key = "hills", movementCost = 2 };
	private static readonly TerrainType mountains = new() { Key = "mountains", movementCost = 3 };
	private static readonly TerrainType blocked = new() { Key = "desert", movementCost = 1, impassable = true };
	private static readonly TerrainType coast = new() { Key = "coast", movementCost = 1 };

	private static readonly TerrainImprovement testRoad = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);
	private static readonly TerrainImprovement testRailroad = new("railroad", TerrainImprovement.Layer.Roads, movementCost: 0);

	private GameMap MakeMap(int seed, int wide, int tall, double waterChance, double roadChance, double railChance, double riverChance) {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(seed));
		Random rng = new(seed);
		GameMap map = new() { numTilesWide = wide, numTilesTall = tall, tiles = new List<Tile>() };
		for (int i = 0; i < wide * tall / 2; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			double r = rng.NextDouble();
			TerrainType t = r < waterChance ? coast
				: r < waterChance + 0.05 ? blocked
				: r < waterChance + 0.25 ? hills
				: r < waterChance + 0.35 ? mountains
				: plains;
			Tile tile = new(ID.None("tile")) {
				XCoordinate = x,
				YCoordinate = y,
				baseTerrainType = t.Key == "coast" ? coast : plains,
				overlayTerrainType = t,
			};
			map.tiles.Add(tile);
		}
		map.computeNeighbors();
		map.recomputeContinents();

		foreach (Tile tile in map.tiles) {
			if (tile.IsWater()) {
				continue;
			}
			double r = rng.NextDouble();
			if (r < railChance) {
				tile.overlays.Add(testRailroad);
			} else if (r < railChance + roadChance) {
				tile.overlays.Add(testRoad);
			}
			tile.riverNorth = rng.NextDouble() < riverChance;
			tile.riverEast = rng.NextDouble() < riverChance;
			tile.riverSoutheast = rng.NextDouble() < riverChance;
			tile.riverWest = rng.NextDouble() < riverChance;
		}
		return map;
	}

	private static MapUnit MakeUnit(bool land, int movement, Tile location, bool isHuman) {
		MapUnit unit = land ? MakeLandUnit(movement) : MakeWaterUnit(movement);
		unit.owner.isHuman = isHuman;
		unit.location = location;
		return unit;
	}

	// Plain Dijkstra over UnitWalker's edges with the same passability rules
	// as the pathfinder. Returns the cost to the destination, or null.
	private static double? ReferenceCost(Tile start, Tile destination, MapUnit unit) {
		UnitWalker walker = new(unit);
		bool landStrip = unit.IsWaterUnit() && (unit.owner.HasExploredTile(destination) || !unit.owner.isHuman);
		Dictionary<Tile, double> best = new() { [start] = 0 };
		HashSet<Tile> done = new();
		PriorityQueue<Tile, double> open = new();
		open.Enqueue(start, 0);
		while (open.TryDequeue(out Tile current, out double c)) {
			if (!done.Add(current)) {
				continue;
			}
			if (current == destination) {
				return c;
			}
			foreach (Edge<Tile> e in walker.getEdges(current)) {
				Tile n = e.current;
				if (n == Tile.NONE || done.Contains(n)) {
					continue;
				}
				if (landStrip && GameMap.IsLandStrip(current, n)) {
					continue;
				}
				bool passable = n == destination ? unit.CanEnterForcefully(n) : unit.CanEnterPeacefully(n);
				if (!passable) {
					continue;
				}
				double nc = c + e.distanceToCurrent;
				if (!best.TryGetValue(n, out double old) || nc < old) {
					best[n] = nc;
					open.Enqueue(n, nc);
				}
			}
		}
		return null;
	}

	// Sums the edge costs along a path, checking that each step is legal.
	private static double PathCost(Tile start, TilePath path, MapUnit unit) {
		UnitWalker walker = new(unit);
		double total = 0;
		Tile current = start;
		foreach (Tile next in path.path) {
			Edge<Tile> edge = walker.getEdges(current).FirstOrDefault(e => e.current == next);
			Assert.NotNull(edge);
			total += edge.distanceToCurrent;
			current = next;
		}
		Assert.Equal(path.destination, current);
		return total;
	}

	private void CompareOnMap(GameMap map, bool land, int movement, bool isHuman, int seed, int pairs) {
		Random rng = new(seed);
		List<Tile> candidates = map.tiles.Where(t => land ? t.IsLand() : t.IsWater()).ToList();
		int reachable = 0;
		for (int i = 0; i < pairs; ++i) {
			Tile start = candidates[rng.Next(candidates.Count)];
			Tile destination = map.tiles[rng.Next(map.tiles.Count)];
			MapUnit unit = MakeUnit(land, movement, start, isHuman);

			double? expected = ReferenceCost(start, destination, unit);
			TilePath path = PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(start, destination, unit);

			if (expected == null) {
				Assert.Empty(path.path);
				continue;
			}
			if (start == destination) {
				Assert.Empty(path.path);
				continue;
			}
			++reachable;
			Assert.NotEmpty(path.path);
			Assert.Equal(expected.Value, PathCost(start, path, unit), 5);
		}
		Assert.True(reachable > 0);
	}

	[Theory]
	[InlineData(1, 1, false)]
	[InlineData(2, 2, false)]
	[InlineData(3, 3, false)]
	[InlineData(4, 1, true)]
	[InlineData(5, 3, true)]
	public void LandPathsMatchReferenceWithRoads(int seed, int movement, bool isHuman) {
		GameMap map = MakeMap(seed, 40, 40, waterChance: 0.15, roadChance: 0.3, railChance: 0, riverChance: 0.1);
		CompareOnMap(map, land: true, movement, isHuman, seed, pairs: 60);
	}

	[Theory]
	[InlineData(11, 1)]
	[InlineData(12, 3)]
	public void LandPathsMatchReferenceWithRailroads(int seed, int movement) {
		GameMap map = MakeMap(seed, 40, 40, waterChance: 0.15, roadChance: 0.3, railChance: 0.2, riverChance: 0.1);
		CompareOnMap(map, land: true, movement, isHuman: false, seed, pairs: 60);
	}

	[Theory]
	[InlineData(21, 1, false)]
	[InlineData(22, 3, false)]
	[InlineData(23, 5, false)]
	[InlineData(24, 3, true)]
	public void WaterPathsMatchReference(int seed, int movement, bool isHuman) {
		GameMap map = MakeMap(seed, 40, 40, waterChance: 0.55, roadChance: 0.2, railChance: 0, riverChance: 0);
		CompareOnMap(map, land: false, movement, isHuman, seed, pairs: 60);
	}

	[Fact]
	public void WaterUnitsSailThroughOwnCitiesBetweenWaterBodies() {
		GameMap map = MakeMap(31, 40, 40, waterChance: 0.5, roadChance: 0, railChance: 0, riverChance: 0);
		Random rng = new(31);
		MapUnit probe = MakeUnit(false, 3, map.tiles.First(t => t.IsWater()), false);

		// Found cities for the unit's owner on land tiles bordering water.
		List<Tile> coastalLand = map.tiles.Where(t => t.IsLand() && t.neighbors.Values.Any(n => n != Tile.NONE && n.IsWater())).ToList();
		for (int i = 0; i < 15; ++i) {
			Tile t = coastalLand[rng.Next(coastalLand.Count)];
			if (t.cityAtTile != null || t.neighbors.Values.Any(n => n.cityAtTile != null)) {
				continue;
			}
			City city = new(t, probe.owner, "City " + i, ID.None("city"));
			t.cityAtTile = city;
			probe.owner.cities.Add(city);
		}

		List<Tile> water = map.tiles.Where(t => t.IsWater()).ToList();
		List<Tile> targets = water.Concat(probe.owner.cities.Select(c => c.location)).ToList();
		int reachable = 0;
		for (int i = 0; i < 80; ++i) {
			Tile start = water[rng.Next(water.Count)];
			Tile destination = targets[rng.Next(targets.Count)];
			probe.location = start;

			double? expected = ReferenceCost(start, destination, probe);
			TilePath path = PathingAlgorithmChooser.GetAlgorithm(probe).PathFrom(start, destination, probe);
			if (expected == null || start == destination) {
				Assert.Empty(path.path);
				continue;
			}
			++reachable;
			Assert.Equal(expected.Value, PathCost(start, path, probe), 5);
		}
		Assert.True(reachable > 0);
	}

	[Theory]
	[InlineData(41, true)]
	[InlineData(42, false)]
	public void FindFirstReachableMatchesSequentialSearch(int seed, bool land) {
		GameMap map = MakeMap(seed, 40, 40, waterChance: land ? 0.3 : 0.55, roadChance: 0.2, railChance: 0, riverChance: 0.1);
		Random rng = new(seed);
		List<Tile> starts = map.tiles.Where(t => land ? t.IsLand() : t.IsWater()).ToList();

		for (int i = 0; i < 25; ++i) {
			Tile start = starts[rng.Next(starts.Count)];
			MapUnit unit = MakeUnit(land, 2, start, false);
			List<Tile> candidates = Enumerable.Range(0, 12).Select(_ => map.tiles[rng.Next(map.tiles.Count)]).ToList();

			int expectedIndex = -1;
			double expectedCost = 0;
			for (int j = 0; j < candidates.Count; ++j) {
				double? c = ReferenceCost(start, candidates[j], unit);
				if (c != null) {
					expectedIndex = j;
					expectedCost = c.Value;
					break;
				}
			}

			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
			int index = algorithm.FindFirstReachable(start, candidates, unit, out TilePath path);
			Assert.Equal(expectedIndex, index);
			if (index >= 0) {
				Assert.Equal(candidates[index], path.destination);
				Assert.Equal(expectedCost, PathCost(start, path, unit), 5);
			}
		}
	}

	[Fact]
	public void BoundedSearchGivesUpBeyondMaxCost() {
		GameMap map = MakeMap(51, 40, 40, waterChance: 0, roadChance: 0, railChance: 0, riverChance: 0);
		Tile start = map.tileAt(20, 20);
		Tile destination = map.tileAt(30, 20);
		MapUnit unit = MakeUnit(true, 1, start, false);
		AStarAlgorithm algorithm = (AStarAlgorithm)PathingAlgorithmChooser.GetAlgorithm(unit);

		double? expected = ReferenceCost(start, destination, unit);
		Assert.NotNull(expected);
		Assert.NotEmpty(algorithm.PathFrom(start, destination, unit, expected.Value).path);
		Assert.Empty(algorithm.PathFrom(start, destination, unit, expected.Value - 0.5).path);
	}

	[Fact]
	public void HeuristicFloorAccountsForRailroads() {
		GameMap map = MakeMap(61, 20, 20, waterChance: 0, roadChance: 0.5, railChance: 0, riverChance: 0);
		Tile start = map.tiles.First(t => t.IsLand());
		MapUnit unit = MakeUnit(true, 1, start, false);
		Assert.Equal(1.0f / 3, MovementCostFloor.MinimumTileCost(unit, start), 5);

		// Pre-existing railroads on a (new) map drop the floor to zero.
		GameMap railMap = MakeMap(62, 20, 20, waterChance: 0, roadChance: 0, railChance: 0.5, riverChance: 0);
		Tile railStart = railMap.tiles.First(t => t.IsLand());
		Assert.Equal(0f, MovementCostFloor.MinimumTileCost(MakeUnit(true, 1, railStart, false), railStart));
	}
}

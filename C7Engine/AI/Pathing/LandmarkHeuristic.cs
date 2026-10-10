using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7GameData;

namespace C7Engine.Pathing {
	// A lower bound on the cost of a path for an AI land unit, much closer to
	// the real cost than DistanceTo times the cheapest step once there are
	// railroads (whose steps cost nothing, which makes that bound 0).
	//
	// This is the "ALT" heuristic (A*, landmarks and the triangle
	// inequality). For a few landmark tiles on each landmass we know the
	// cost d(L, t) of the cheapest path from the landmark to every tile t
	// of the landmass, in a graph whose steps never cost more than the real
	// ones. Then for any tiles n and g, d(n, g) >= |d(L, g) - d(L, n)|.
	//
	// The graph: the land tiles, with a step between every two neighbors
	// (all the steps a land unit's search can take) costing
	//   min(1, cheapest terrain movement cost, max(the two tiles' road
	//       layer costs) if both have one).
	// A step costs a unit with m >= 1 movement points at least that over
	// m: TilePath.GetMovementCost is either the terrain cost of the tile
	// entered or (both having road-layer costs) the larger of those, and
	// UnitWalker.EdgeCost caps its fraction of a turn at 1. Units' own
	// rules (borders, rivers, who's in the way) can only make steps dearer or
	// impossible, so one set of distances serves every AI land unit.
	//
	// The step costs only depend on the tiles' road-layer costs (a city
	// without a road counts as 0), which change when roads and cities are
	// built or removed. TileOverlays reports those changes. A step getting
	// cheaper can make the distances too large to be a lower bound, so they
	// are lowered (only as far as needed) before the next search. A step
	// getting dearer leaves them a valid, if weaker, bound, so nothing is
	// done then.
	//
	// The distances satisfy d(L, b) <= d(L, a) + cost(a, b) for every step,
	// so the bound is consistent, as the search needs. Steps are symmetric,
	// so one table per landmark gives both directions.
	internal static class LandmarkHeuristic {
		private const int LandmarksPerLandmass = 4;
		// Searches on smaller landmasses are quick anyway.
		private const int MinLandmassSize = 32;

		private sealed class Landmass {
			// The map index of each of the landmass's tiles.
			public int[] tiles;
			// For each tile, the local index of its neighbors (-1 for none).
			public int[] neighbors;
			// distances[landmark][local index]
			public double[][] distances;
		}

		private sealed class MapTables {
			public int tileCount;
			// For each map tile, its landmass (-1 if none) and its index in it.
			public int[] landmassOf;
			public int[] localIndex;
			public readonly List<Landmass> landmasses = new();
			// Below 1 if a terrain can be crossed for less (never in Civ3).
			public float terrainWeight;
			// Map indices of tiles whose road-layer cost may have changed.
			public readonly List<int> changed = new();
			public bool[] isChanged;
			// Set if the map couldn't be laid out (e.g. a hand-built test map
			// whose tiles aren't all in its list).
			public bool unusable;
		}

		private static readonly ConditionalWeakTable<GameMap, MapTables> tables = new();

		// What a search needs to compute the bound towards its goal.
		internal sealed class Goal {
			public int[] landmassOf;
			public int[] localIndex;
			public int landmass;
			public double[][] distances;
			public double[] goalDistances;
			public double scale;

			// The bound for the tile with this map index, or 0 if the tile isn't
			// on the goal's landmass (a land unit can't get from there to the
			// goal over land).
			public double Estimate(int mapIndex) {
				if ((uint)mapIndex >= (uint)landmassOf.Length || landmassOf[mapIndex] != landmass) {
					return 0;
				}
				int local = localIndex[mapIndex];
				double best = 0;
				for (int l = 0; l < distances.Length; ++l) {
					double difference = Math.Abs(goalDistances[l] - distances[l][local]);
					if (difference > best) {
						best = difference;
					}
				}
				return best * scale;
			}
		}

		// Records that the tile's road-layer cost may have changed.
		public static void OnOverlaysChanged(Tile tile) {
			GameMap map = tile?.map;
			if (map == null || !tables.TryGetValue(map, out MapTables t)) {
				return;
			}
			lock (t) {
				int index = IndexOf(map, tile);
				if (index >= 0 && index < t.isChanged.Length && !t.isChanged[index]) {
					t.isChanged[index] = true;
					t.changed.Add(index);
				}
			}
		}

		// The bound for searches by `unit` towards `goal`, or null if the unit
		// or the goal isn't one this can help with.
		public static Goal ForSearch(MapUnit unit, Tile goal, float movementPoints) {
			Player owner = unit.owner;
			if (owner == null || owner.isHuman || owner.PathsByExploredMap || !unit.IsLandUnit() || movementPoints < 1) {
				return null;
			}
			GameMap map = goal?.map;
			if (map == null || map.tiles == null || map.tiles.Count == 0) {
				return null;
			}
			int goalIndex = IndexOf(map, goal);
			if (goalIndex < 0) {
				return null;
			}

			MapTables t;
			lock (tables) {
				if (!tables.TryGetValue(map, out t) || t.tileCount != map.tiles.Count) {
					t = Build(map);
					tables.AddOrUpdate(map, t);
				}
			}
			lock (t) {
				if (t.unusable) {
					return null;
				}
				ApplyChanges(map, t);
				int landmass = t.landmassOf[goalIndex];
				if (landmass < 0) {
					return null;
				}
				Landmass lm = t.landmasses[landmass];
				int local = t.localIndex[goalIndex];
				double[] goalDistances = new double[lm.distances.Length];
				for (int l = 0; l < goalDistances.Length; ++l) {
					goalDistances[l] = lm.distances[l][local];
				}
				return new Goal {
					landmassOf = t.landmassOf,
					localIndex = t.localIndex,
					landmass = landmass,
					distances = lm.distances,
					goalDistances = goalDistances,
					// Divided by the movement points (see above), and scaled
					// down a hair so that rounding can't make it overestimate.
					scale = (1 - 1e-9) / movementPoints,
				};
			}
		}

		private static int IndexOf(GameMap map, Tile tile) {
			int index = map.tileCoordsToIndex(tile.XCoordinate, tile.YCoordinate);
			if ((uint)index < (uint)map.tiles.Count && ReferenceEquals(map.tiles[index], tile)) {
				return index;
			}
			return -1;
		}

		private static float StepCost(Tile a, Tile b, float terrainWeight) {
			float cost = terrainWeight;
			float aCost = a.overlays.MovementCost();
			float bCost = b.overlays.MovementCost();
			if (aCost != -1 && bCost != -1) {
				cost = Math.Min(cost, Math.Max(aCost, bCost));
			}
			return Math.Max(0, cost);
		}

		private static MapTables Build(GameMap map) {
			int n = map.tiles.Count;
			MapTables t = new() {
				tileCount = n,
				landmassOf = new int[n],
				localIndex = new int[n],
				isChanged = new bool[n],
			};
			Array.Fill(t.landmassOf, -1);
			Array.Fill(t.localIndex, -1);

			float terrainWeight = 1;
			foreach (Tile tile in map.tiles) {
				terrainWeight = Math.Min(terrainWeight, tile.MovementCost());
			}
			if (EngineStorage.gameData != null) {
				foreach (TerrainType tt in EngineStorage.gameData.terrainTypes) {
					terrainWeight = Math.Min(terrainWeight, tt.movementCost);
				}
			}
			t.terrainWeight = Math.Max(0, terrainWeight);

			// Group the land tiles by which can be reached from which over
			// land, as a land unit's search moves.
			int[] component = new int[n];
			Array.Fill(component, -1);
			List<List<int>> components = new();
			Queue<int> queue = new();
			for (int i = 0; i < n; ++i) {
				Tile start = map.tiles[i];
				if (component[i] >= 0 || start == null || start == Tile.NONE || !start.IsLand()) {
					continue;
				}
				List<int> members = new();
				component[i] = components.Count;
				queue.Enqueue(i);
				while (queue.Count > 0) {
					int x = queue.Dequeue();
					members.Add(x);
					foreach (Tile neighbor in map.tiles[x].neighbors.Values) {
						if (neighbor == null || neighbor == Tile.NONE || !neighbor.IsLand()) {
							continue;
						}
						int y = IndexOf(map, neighbor);
						if (y < 0) {
							t.unusable = true;
							return t;
						}
						if (component[y] < 0) {
							component[y] = components.Count;
							queue.Enqueue(y);
						}
					}
				}
				components.Add(members);
			}

			foreach (List<int> members in components) {
				if (members.Count < MinLandmassSize) {
					continue;
				}
				// Keep the map order, so that the landmarks chosen don't depend
				// on the order of the flood fill.
				members.Sort();
				int id = t.landmasses.Count;
				Landmass lm = new() { tiles = members.ToArray(), neighbors = new int[members.Count * 8] };
				for (int i = 0; i < lm.tiles.Length; ++i) {
					t.landmassOf[lm.tiles[i]] = id;
					t.localIndex[lm.tiles[i]] = i;
				}
				for (int i = 0; i < lm.tiles.Length; ++i) {
					int k = 0;
					foreach (Tile neighbor in map.tiles[lm.tiles[i]].neighbors.Values) {
						if (k == 8) {
							break;
						}
						if (neighbor == null || neighbor == Tile.NONE || !neighbor.IsLand()) {
							continue;
						}
						lm.neighbors[i * 8 + k++] = t.localIndex[IndexOf(map, neighbor)];
					}
					for (; k < 8; ++k) {
						lm.neighbors[i * 8 + k] = -1;
					}
				}
				ChooseLandmarks(map, lm, t.terrainWeight);
				t.landmasses.Add(lm);
			}
			return t;
		}

		// Picks each landmark as the tile farthest from those picked so far
		// (the first one farthest from the landmass's first tile), which
		// spreads them around the edges, where they bound the most paths well.
		private static void ChooseLandmarks(GameMap map, Landmass lm, float terrainWeight) {
			int count = lm.tiles.Length;
			double[] nearest = Dijkstra(map, lm, 0, terrainWeight);
			List<double[]> distances = new();
			for (int l = 0; l < LandmarksPerLandmass; ++l) {
				int farthest = 0;
				for (int i = 1; i < count; ++i) {
					if (nearest[i] > nearest[farthest]) {
						farthest = i;
					}
				}
				if (l > 0 && nearest[farthest] <= 0) {
					break;
				}
				double[] d = Dijkstra(map, lm, farthest, terrainWeight);
				distances.Add(d);
				if (l == 0) {
					nearest = (double[])d.Clone();
				} else {
					for (int i = 0; i < count; ++i) {
						nearest[i] = Math.Min(nearest[i], d[i]);
					}
				}
			}
			lm.distances = distances.ToArray();
		}

		private static double[] Dijkstra(GameMap map, Landmass lm, int source, float terrainWeight) {
			double[] d = new double[lm.tiles.Length];
			Array.Fill(d, double.PositiveInfinity);
			d[source] = 0;
			PriorityQueue<int, double> open = new();
			open.Enqueue(source, 0);
			Propagate(map, lm, d, open, terrainWeight);
			return d;
		}

		// Lowers distances until every step satisfies
		// d[b] <= d[a] + cost(a, b), starting from the queued tiles.
		private static void Propagate(GameMap map, Landmass lm, double[] d, PriorityQueue<int, double> open, float terrainWeight) {
			while (open.TryDequeue(out int x, out double queued)) {
				if (queued > d[x]) {
					continue;
				}
				Tile xTile = map.tiles[lm.tiles[x]];
				for (int k = 0; k < 8; ++k) {
					int y = lm.neighbors[x * 8 + k];
					if (y < 0) {
						continue;
					}
					double candidate = d[x] + StepCost(xTile, map.tiles[lm.tiles[y]], terrainWeight);
					if (candidate < d[y]) {
						d[y] = candidate;
						open.Enqueue(y, candidate);
					}
				}
			}
		}

		// Lowers the distances where steps next to changed tiles got cheaper.
		private static void ApplyChanges(GameMap map, MapTables t) {
			if (t.changed.Count == 0) {
				return;
			}
			PriorityQueue<int, double> open = new();
			foreach (int index in t.changed) {
				t.isChanged[index] = false;
				int landmass = t.landmassOf[index];
				if (landmass < 0) {
					continue;
				}
				Landmass lm = t.landmasses[landmass];
				int x = t.localIndex[index];
				Tile xTile = map.tiles[index];
				foreach (double[] d in lm.distances) {
					for (int k = 0; k < 8; ++k) {
						int y = lm.neighbors[x * 8 + k];
						if (y < 0) {
							continue;
						}
						float cost = StepCost(xTile, map.tiles[lm.tiles[y]], t.terrainWeight);
						if (d[y] + cost < d[x]) {
							d[x] = d[y] + cost;
							open.Enqueue(x, d[x]);
						}
						if (d[x] + cost < d[y]) {
							d[y] = d[x] + cost;
							open.Enqueue(y, d[y]);
						}
					}
					Propagate(map, lm, d, open, t.terrainWeight);
				}
			}
			t.changed.Clear();
		}
	}
}

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7GameData;
using static C7GameData.TerrainImprovement;

namespace C7Engine.Pathing {
	// Computes a lower bound on the cost of a single pathing step, which is
	// what makes the A* heuristic admissible: a path of DistanceTo(a, b) steps
	// can never cost less than DistanceTo(a, b) * (cheapest possible step).
	//
	// The bound must hold for every edge UnitWalker can produce, i.e. for every
	// value TilePath.GetMovementCost can return:
	//   - 1 for tiles a human hasn't explored, and for water -> city moves
	//   - the terrain movement cost of the destination tile
	//   - max(from, to) of the road-layer overlay costs, when both tiles have
	//     one (roads, railroads, or 0 for a city tile without a road).
	//
	// Overlay costs change during the game, so the overlay part of the bound
	// is the minimum of
	//   - the cheapest overlay edge on the map when it was first scanned
	//     (covers scenario maps with pre-built railroads), and
	//   - the cost of every road-layer terraform some player can build (a new
	//     road or railroad can only appear once someone has its tech).
	// Adjacent cities can't be founded, so a 0-cost edge between two road-less
	// cities can only come from the initial map, which the scan covers.
	internal static class MovementCostFloor {
		private sealed class MapScan {
			public int tileCount;
			public float minTerrainCost = float.MaxValue;
			// Cheapest max(from, to) overlay cost over all adjacent tile pairs.
			public float minOverlayEdgeCost = float.MaxValue;
			// Same, restricted to pairs of adjacent cities (the only overlay
			// edges a water unit can take).
			public float minCityEdgeCost = float.MaxValue;
		}

		private static readonly ConditionalWeakTable<GameMap, MapScan> scans = new();

		// Returns the minimum cost, in movement points, of a single step taken
		// by `unit`, or 0 if no useful bound can be computed.
		public static float MinimumTileCost(MapUnit unit, Tile start) {
			GameMap map = start?.map;
			if (map == null || map.tiles == null || map.tiles.Count == 0) {
				return 0;
			}

			// Maps that don't contain the start tile (as built by some unit
			// tests) can't be scanned reliably, so give up on the heuristic.
			int index = map.tileCoordsToIndex(start.XCoordinate, start.YCoordinate);
			if ((uint)index >= (uint)map.tiles.Count || !ReferenceEquals(map.tiles[index], start)) {
				return 0;
			}

			MapScan scan;
			lock (scans) {
				if (!scans.TryGetValue(map, out scan) || scan.tileCount != map.tiles.Count) {
					scan = Scan(map);
					scans.AddOrUpdate(map, scan);
				}
			}

			GameData gameData = EngineStorage.gameData;

			float terrainFloor = scan.minTerrainCost;
			if (gameData != null) {
				foreach (TerrainType tt in gameData.terrainTypes) {
					if (tt.movementCost < terrainFloor) {
						terrainFloor = tt.movementCost;
					}
				}
			}

			// Unexplored tiles (for humans) and water -> city moves cost 1.
			float floor = Math.Min(1f, terrainFloor);

			if (unit.IsWaterUnit() && !unit.IsLandUnit()) {
				floor = Math.Min(floor, scan.minCityEdgeCost);
			} else {
				floor = Math.Min(floor, scan.minOverlayEdgeCost);
				if (gameData != null) {
					foreach (Terraform tf in gameData.Terraforms) {
						TerrainImprovement imp = tf.Improvement;
						if (imp == null || imp.layer != Layer.Roads || imp.movementCost < 0 || imp.movementCost >= floor) {
							continue;
						}
						if (AnyPlayerHasTech(gameData.players, tf)) {
							floor = imp.movementCost;
						}
					}
				}
			}

			return Math.Max(0f, floor);
		}

		private static bool AnyPlayerHasTech(List<Player> players, Terraform tf) {
			if (tf.RequiredTech == null) {
				return true;
			}
			foreach (Player p in players) {
				if (p.HasTech(tf.RequiredTech)) {
					return true;
				}
			}
			return false;
		}

		private static MapScan Scan(GameMap map) {
			MapScan scan = new() { tileCount = map.tiles.Count };
			foreach (Tile t in map.tiles) {
				if (t == null || t == Tile.NONE) {
					continue;
				}
				float terrain = t.MovementCost();
				if (terrain < scan.minTerrainCost) {
					scan.minTerrainCost = terrain;
				}

				float cost = t.overlays.MovementCost();
				if (cost == -1) {
					continue;
				}
				bool isCity = t.HasCity();
				foreach (Tile n in t.neighbors.Values) {
					if (n == null || n == Tile.NONE) {
						continue;
					}
					float nCost = n.overlays.MovementCost();
					if (nCost == -1) {
						continue;
					}
					float edge = Math.Max(cost, nCost);
					if (edge < scan.minOverlayEdgeCost) {
						scan.minOverlayEdgeCost = edge;
					}
					if (isCity && n.HasCity() && edge < scan.minCityEdgeCost) {
						scan.minCityEdgeCost = edge;
					}
				}
			}
			return scan;
		}

		// Converts a per-tile cost floor into a per-edge floor, matching the
		// way UnitWalker turns movement costs into fractions of a turn.
		public static float EdgeFloor(float tileCostFloor, float unitMovementPoints) {
			return tileCostFloor >= unitMovementPoints ? 1f : tileCostFloor / unitMovementPoints;
		}
	}
}

using System.Collections.Generic;
using System;
using C7GameData;

namespace C7Engine.Pathing {
	public class TileAndCost : IComparable<TileAndCost> {
		public Tile tile;
		public double cost = 0;

		public int CompareTo(TileAndCost other) {
			return cost.CompareTo(other.cost);
		}
	}

	// An implementation fo the A* path searching algorithm.
	//
	// See https://en.wikipedia.org/wiki/A*_search_algorithm and
	// https://github.com/yairm210/Unciv/blob/master/core/src/com/unciv/logic/map/AStar.kt
	// for some useful details.
	//
	// Per-tile search data is kept in pooled arrays (see PathSearchContext)
	// rather than dictionaries, stale heap entries are skipped instead of
	// being re-expanded, and the (expensive) passability check runs at most
	// once per tile per search, and only for edges that would improve the
	// best known cost of the tile.
	public class AStarAlgorithm : PathingAlgorithm {
		private readonly EdgeWalker<Tile> edgeWalker;

		// An estimate of the cost between two tiles, usually between a tile in
		// the search and the destination tile. This allows targeting the search
		// by doing things like focusing the search in the cardinal direction of
		// the destination instead of expanding outwards in all directions.
		private Func<Tile, Tile, double> costHeuristic;

		// A delegate that determines if a tile is passable during pathfinding.
		// Receives the tile being evaluated and the final destination tile.
		private Func<Tile, Tile, bool> isPassable;

		// Set when this algorithm was created for a specific unit (see
		// PathingAlgorithmChooser). In that case we walk the tile neighbors
		// directly, use an admissible and consistent heuristic computed per
		// search, and check passability with the unit's CanEnter* methods.
		private readonly MapUnit unit;

		public AStarAlgorithm(EdgeWalker<Tile> edgeWalker, Func<Tile, Tile, double> costHeuristic, Func<Tile, Tile, bool> isPassable) {
			this.edgeWalker = edgeWalker;
			this.costHeuristic = costHeuristic;
			this.isPassable = isPassable;
		}

		// Creates an A* search specialized for moving the given unit.
		public AStarAlgorithm(MapUnit unit) {
			this.unit = unit;
			this.edgeWalker = new UnitWalker(unit);
		}

		public override TilePath PathFrom(Tile start, Tile destination, MapUnit unit) {
			return PathFrom(start, destination, unit, double.PositiveInfinity);
		}

		// Like PathFrom, but gives up (returning an empty path) if the
		// destination costs more than `maxCost` turns to reach.
		public TilePath PathFrom(Tile start, Tile destination, MapUnit unit, double maxCost) {
			// Exit early if the AI can't possibly get there, such as when
			// starting and ending on different continents. Don't waste time
			// checking every tile on the continent just to discover the path is
			// impossible.
			if (destination == null || destination == Tile.NONE || PathingShortcuts.IsTriviallyUnreachable(start, destination, unit)) {
				return TilePath.EmptyPath(destination);
			}

			PathSearchContext ctx = PathSearchContext.Rent(start.map);
			try {
				int found = Search(ctx, start, destination, unit, maxCost);
				return found >= 0 ? ctx.MakePath(found, destination) : TilePath.EmptyPath(destination);
			} finally {
				PathSearchContext.Return(ctx);
			}
		}

		public override int FindFirstReachable(Tile start, IReadOnlyList<Tile> candidates, MapUnit unit, out TilePath path) {
			// For human water units, whether land strips block movement depends
			// on whether the destination was explored, so a single search can't
			// answer for every candidate.
			if (this.unit == null || (unit.IsWaterUnit() && unit.owner.isHuman)) {
				return base.FindFirstReachable(start, candidates, unit, out path);
			}

			path = null;
			int first = -1;
			for (int i = 0; i < candidates.Count; ++i) {
				Tile c = candidates[i];
				if (c == null || c == Tile.NONE) {
					continue;
				}
				if (c == start) {
					path = TilePath.EmptyPath(start);
					return i;
				}
				if (!PathingShortcuts.IsTriviallyUnreachable(start, c, unit)) {
					first = i;
					break;
				}
			}
			if (first < 0) {
				return -1;
			}

			// Usually the best candidate is reachable, and a targeted A* search
			// finds it quickly.
			int next = -1;
			PathSearchContext ctx = PathSearchContext.Rent(start.map);
			try {
				int found = Search(ctx, start, candidates[first], unit, double.PositiveInfinity);
				if (found >= 0) {
					path = ctx.MakePath(found, candidates[first]);
					return first;
				}

				// Otherwise the failed search has expanded every tile reachable
				// from the start, so we can tell which candidates are reachable
				// without searching for each of them.
				for (int i = first + 1; i < candidates.Count; ++i) {
					Tile c = candidates[i];
					if (c == null || c == Tile.NONE) {
						continue;
					}
					if (c == start) {
						path = TilePath.EmptyPath(start);
						return i;
					}
					if (IsReachableFromExpanded(ctx, start, c, unit)) {
						next = i;
						break;
					}
				}
			} finally {
				PathSearchContext.Return(ctx);
			}

			if (next < 0) {
				return -1;
			}
			path = PathFrom(start, candidates[next], unit);
			return next;
		}

		// After an exhaustive search, returns true if a path to `c` exists:
		// either the search expanded it, or it can be entered from a tile the
		// search expanded. Intermediate tiles are always checked for peaceful
		// entry, and only the destination for forceful entry, so the tiles
		// expanded by a failed search are exactly those reachable with
		// peaceful moves (plus the start).
		private bool IsReachableFromExpanded(PathSearchContext ctx, Tile start, Tile c, MapUnit pathUnit) {
			if (PathingShortcuts.IsTriviallyUnreachable(start, c, pathUnit)) {
				return false;
			}

			int id = ctx.PeekId(c);
			if (id >= 0 && (ctx.flags[id] & PathSearchContext.CLOSED) != 0) {
				return true;
			}

			Player owner = unit.owner;
			if (!CanWalkOnto(c, owner, owner.isHuman, unit.IsLandUnit(), unit.IsWaterUnit())) {
				return false;
			}

			bool applyLandStrip = pathUnit.IsWaterUnit() && (pathUnit.owner.HasExploredTile(c) || !pathUnit.owner.isHuman);
			bool adjacentToExpanded = false;
			foreach (Tile n in c.neighbors.Values) {
				if (n == Tile.NONE) {
					continue;
				}
				int nid = ctx.PeekId(n);
				if (nid < 0 || (ctx.flags[nid] & PathSearchContext.CLOSED) == 0) {
					continue;
				}
				if (applyLandStrip && GameMap.IsLandStrip(n, c)) {
					continue;
				}
				adjacentToExpanded = true;
				break;
			}

			return adjacentToExpanded && unit.CanEnterForcefully(c);
		}

		// The terrain part of UnitWalker's edge filter.
		private static bool CanWalkOnto(Tile neighbor, Player owner, bool isHuman, bool isLandUnit, bool isWaterUnit) {
			if (isHuman && !owner.HasExploredTile(neighbor)) {
				return true;
			}
			if (isLandUnit) {
				return neighbor.IsLand();
			}
			if (isWaterUnit) {
				return neighbor.IsWater() || (neighbor.cityAtTile != null && neighbor.cityAtTile.owner == owner);
			}
			return false;
		}

		// Runs the search, returning the node id of the destination, or -1 if
		// it can't be reached. The context is left as the search left it.
		private int Search(PathSearchContext ctx, Tile start, Tile destination, MapUnit pathUnit, double maxCost) {
			// TODO: the possibility here is to create a building/improvement/tech effect like "canal"
			// (much like what the Panama canal is irl) that allows that kind of movement
			bool applyLandStrip = pathUnit.IsWaterUnit()
				&& (pathUnit.owner.HasExploredTile(destination) || !pathUnit.owner.isHuman);

			bool specialized = unit != null;
			Player owner = specialized ? unit.owner : null;
			bool isHuman = specialized && owner.isHuman;
			bool isLandUnit = specialized && unit.IsLandUnit();
			bool isWaterUnit = specialized && unit.IsWaterUnit();
			float movementPoints = specialized ? unit.MaxMovementPoints() : 0;

			// The heuristic is DistanceTo times the cheapest possible step,
			// which never overestimates (see MovementCostFloor), scaled down a
			// hair so that float rounding can't make it inadmissible.
			double heuristicScale = 0;
			if (specialized) {
				float tileFloor = MovementCostFloor.MinimumTileCost(unit, start);
				heuristicScale = MovementCostFloor.EdgeFloor(tileFloor, movementPoints) * (1 - 1e-6);
			}

			// Note: ctx's arrays may be reallocated by ctx.Id, so they are
			// always accessed through ctx.
			int startId = ctx.Id(start);
			ctx.cost[startId] = 0;
			ctx.open.Enqueue(new PathSearchContext.Entry(startId, 0), 0);

			while (ctx.open.TryDequeue(out PathSearchContext.Entry entry, out _)) {
				int current = entry.node;

				// Skip stale entries for tiles we've since found a cheaper way to.
				if (entry.cost > ctx.cost[current]) {
					continue;
				}

				// The specialized heuristic is consistent, so the first time a
				// tile is expanded we already have its cheapest path.
				if (specialized) {
					if ((ctx.flags[current] & PathSearchContext.CLOSED) != 0) {
						continue;
					}
					ctx.flags[current] |= PathSearchContext.CLOSED;
				}

				Tile currentTile = ctx.tiles[current];

				// If this is the destination, we're done.
				if (currentTile == destination) {
					return current;
				}

				double currentCost = ctx.cost[current];

				if (specialized) {
					foreach (KeyValuePair<TileDirection, Tile> pair in currentTile.neighbors) {
						Tile neighbor = pair.Value;
						if (neighbor == Tile.NONE || !CanWalkOnto(neighbor, owner, isHuman, isLandUnit, isWaterUnit)) {
							continue;
						}
						if (applyLandStrip && GameMap.IsLandStrip(currentTile, neighbor)) {
							continue;
						}

						int id = ctx.Id(neighbor);
						if ((ctx.flags[id] & PathSearchContext.CLOSED) != 0) {
							continue;
						}

						float edgeCost = UnitWalker.EdgeCost(owner, currentTile, pair.Key, neighbor, movementPoints);
						Relax(ctx, current, id, neighbor, destination, currentCost + edgeCost, maxCost, heuristicScale);
					}
				} else {
					foreach (Edge<Tile> neighborEdge in edgeWalker.getEdges(currentTile)) {
						Tile neighbor = neighborEdge.current;
						if (applyLandStrip && GameMap.IsLandStrip(currentTile, neighbor)) {
							continue;
						}
						int id = ctx.Id(neighbor);
						Relax(ctx, current, id, neighbor, destination, currentCost + neighborEdge.distanceToCurrent, maxCost, heuristicScale);
					}
				}
			}

			return -1;
		}

		private void Relax(PathSearchContext ctx, int from, int id, Tile neighbor, Tile destination, double newCost, double maxCost, double heuristicScale) {
			// Only consider edges that improve on what we already know, and
			// only then pay for the passability check.
			if (newCost >= ctx.cost[id] || newCost > maxCost) {
				return;
			}
			if (!IsPassable(ctx, id, neighbor, destination)) {
				return;
			}

			ctx.cost[id] = newCost;
			ctx.parent[id] = from;
			double estimate = unit != null
				? newCost + Heuristic(neighbor, destination, heuristicScale)
				: newCost + costHeuristic(neighbor, destination);
			ctx.open.Enqueue(new PathSearchContext.Entry(id, newCost), estimate);
		}

		// Memoized per tile for the duration of the search.
		private bool IsPassable(PathSearchContext ctx, int id, Tile neighbor, Tile destination) {
			byte f = ctx.flags[id];
			if ((f & PathSearchContext.PASSABILITY_KNOWN) != 0) {
				return (f & PathSearchContext.PASSABLE) != 0;
			}

			bool passable;
			if (unit != null) {
				passable = neighbor == destination ? unit.CanEnterForcefully(neighbor) : unit.CanEnterPeacefully(neighbor);
			} else {
				passable = isPassable(neighbor, destination);
			}

			ctx.flags[id] = (byte)(f | PathSearchContext.PASSABILITY_KNOWN | (passable ? PathSearchContext.PASSABLE : 0));
			return passable;
		}

		private static double Heuristic(Tile from, Tile to, double scale) {
			return scale == 0 ? 0 : from.DistanceTo(to) * scale;
		}
	}
}

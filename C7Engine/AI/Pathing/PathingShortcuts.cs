using System.Collections.Generic;
using C7GameData;

namespace C7Engine.Pathing {
	// Cheap checks that prove a destination can't be reached, so that the
	// pathfinder doesn't have to flood a whole landmass or ocean to find out.
	//
	// These only apply to AI players: for humans, unexplored tiles are
	// assumed passable by any unit, so connectivity can't be decided from the
	// map alone.
	internal static class PathingShortcuts {
		// Returns true if `unit` can't possibly get from `start` to
		// `destination` with the moves UnitWalker allows.
		public static bool IsTriviallyUnreachable(Tile start, Tile destination, MapUnit unit) {
			WaterBodies waterBodies = null;
			return IsTriviallyUnreachable(start, destination, unit, ref waterBodies);
		}

		// Same as above. `waterBodies` caches what a water unit can reach from
		// `start`: pass the same variable (initially null) for every
		// destination checked from the same start, so it's only computed once.
		public static bool IsTriviallyUnreachable(Tile start, Tile destination, MapUnit unit, ref WaterBodies waterBodies) {
			if (unit.owner.PathsByExploredMap || start == destination) {
				return false;
			}

			if (unit.IsLandUnit()) {
				// AI land units only ever step onto land tiles.
				if (!destination.IsLand()) {
					return true;
				}
				// Different landmasses aren't connected over land.
				return start.IsLand() && start.continent != destination.continent;
			}

			if (unit.IsWaterUnit()) {
				if (waterBodies == null || !waterBodies.IsFor(start, unit.owner)) {
					waterBodies = new WaterBodies(start, unit.owner);
				}
				return waterBodies.CannotReach(destination);
			}

			return false;
		}

		// Tracks which bodies of water (Tile.continent ids of water tiles) a
		// water unit can reach from its start tile. Water bodies are only
		// connected through cities of the unit's owner, which ships can sail
		// through.
		public sealed class WaterBodies {
			private readonly Tile start;
			private readonly Player owner;
			private readonly HashSet<int> reachable = new();
			private readonly bool unknown;

			public WaterBodies(Tile start, Player owner) {
				this.start = start;
				this.owner = owner;

				if (start.IsWater()) {
					reachable.Add(start.continent);
				} else {
					foreach (Tile n in start.neighbors.Values) {
						if (n == Tile.NONE) {
							continue;
						}
						if (n.IsWater()) {
							reachable.Add(n.continent);
						} else if (IsOwnCity(n)) {
							// Cities can't normally be adjacent; don't try to
							// reason about this case.
							unknown = true;
						}
					}
				}

				// Expand through our own cities until nothing changes.
				bool changed = true;
				while (changed && !unknown) {
					changed = false;
					foreach (City c in owner.cities) {
						Tile loc = c.location;
						if (loc == null || loc == Tile.NONE || !AdjacentToReachableWater(loc)) {
							continue;
						}
						foreach (Tile n in loc.neighbors.Values) {
							if (n != Tile.NONE && n.IsWater() && reachable.Add(n.continent)) {
								changed = true;
							}
						}
					}
				}
			}

			public bool IsFor(Tile start, Player owner) {
				return this.start == start && this.owner == owner;
			}

			private bool IsOwnCity(Tile t) {
				return t.cityAtTile != null && t.cityAtTile.owner == owner;
			}

			private bool AdjacentToReachableWater(Tile t) {
				foreach (Tile n in t.neighbors.Values) {
					if (n != Tile.NONE && n.IsWater() && reachable.Contains(n.continent)) {
						return true;
					}
				}
				return false;
			}

			public bool CannotReach(Tile destination) {
				if (unknown) {
					return false;
				}
				if (destination.IsWater()) {
					return !reachable.Contains(destination.continent);
				}
				// Water units can only enter land tiles with one of our cities.
				if (!IsOwnCity(destination)) {
					return true;
				}
				return !AdjacentToReachableWater(destination);
			}
		}
	}
}

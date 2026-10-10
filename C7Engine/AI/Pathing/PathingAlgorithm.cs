using System.Collections.Generic;
using C7GameData;

namespace C7Engine.Pathing {
	public abstract class PathingAlgorithm {
		public abstract TilePath PathFrom(Tile start, Tile destination, MapUnit unit);

		// Given candidate destinations in order of preference, returns the
		// index of the first one `unit` can reach from `start` (or -1 if none
		// can be reached), along with the path to it.
		//
		// This is equivalent to calling PathFrom for each candidate in turn
		// until one succeeds, but implementations may answer it with far less
		// searching.
		public virtual int FindFirstReachable(Tile start, IReadOnlyList<Tile> candidates, MapUnit unit, out TilePath path) {
			for (int i = 0; i < candidates.Count; ++i) {
				Tile candidate = candidates[i];
				if (candidate == null || candidate == Tile.NONE) {
					continue;
				}
				TilePath p = PathFrom(start, candidate, unit);
				if (candidate == start || (p != null && p.PathLength() > 0)) {
					path = p;
					return i;
				}
			}
			path = null;
			return -1;
		}

		// Like FindFirstReachable, but remembers in `reachable` what it
		// learned about which tiles the unit can reach from `start`, and uses
		// what an earlier call with the same `reachable` learned. The answer
		// is the same as without it, so it may only be shared between calls
		// for the same unit and start while nothing on the map changes.
		public virtual int FindFirstReachable(Tile start, IReadOnlyList<Tile> candidates, MapUnit unit, ReachableTiles reachable, out TilePath path) {
			return FindFirstReachable(start, candidates, unit, out path);
		}

		// Like FindFirstReachable, for callers that don't need the path.
		public virtual int FindFirstReachableIndex(Tile start, IReadOnlyList<Tile> candidates, MapUnit unit) {
			return FindFirstReachable(start, candidates, unit, out _);
		}
	}

	// The tiles a unit can reach from where it stands, kept from a search
	// that had to look at all of them (see FindFirstReachable), so that
	// later questions about other tiles needn't search them all again.
	public sealed class ReachableTiles : System.IDisposable {
		internal Tile start;
		internal MapUnit unit;
		// The failed search, which expanded every tile the unit can reach.
		internal PathSearchContext search;

		internal bool Knows(Tile from, MapUnit forUnit) {
			return search != null && start == from && unit == forUnit;
		}

		public void Dispose() {
			if (search != null) {
				PathSearchContext.Return(search);
				search = null;
			}
		}
	}

}

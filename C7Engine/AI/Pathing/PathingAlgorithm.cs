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
	}

}

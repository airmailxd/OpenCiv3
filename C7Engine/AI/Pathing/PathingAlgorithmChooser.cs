using C7GameData;

namespace C7Engine.Pathing {
	/**
	 * Returns a pathing algorithm to use.
	 */
	public class PathingAlgorithmChooser {
		// The returned A* search uses DistanceTo times the cheapest possible
		// step cost for the unit as its heuristic (see MovementCostFloor). That
		// never overestimates, even with zero-cost railroads around (in which
		// case it degrades to Dijkstra), so the paths found are always optimal.
		//
		// Passability: every tile on the way must be enterable peacefully. The
		// destination must be enterable with CanEnter (e.g. to attack it), or
		// for human-owned units forcefully, since only humans can declare war
		// by moving (they're asked to confirm).
		public static PathingAlgorithm GetAlgorithm(MapUnit unit) {
			return new AStarAlgorithm(unit);
		}
	}
}

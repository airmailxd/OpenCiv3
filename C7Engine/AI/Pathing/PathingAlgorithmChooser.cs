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
		// Passability: the destination only needs to be enterable forcefully
		// (e.g. to attack it), every tile on the way must be enterable
		// peacefully.
		public static PathingAlgorithm GetAlgorithm(MapUnit unit) {
			return new AStarAlgorithm(unit);
		}
	}
}

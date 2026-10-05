using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using C7Engine.AI.UnitAI;
using C7GameData;
using C7GameData.AIData;
using C7Engine.Pathing;
using Serilog;

namespace C7Engine {
	public class ExplorerAI : C7GameData.UnitAI {
		private static ILogger log = Log.ForContext<ExplorerAI>();
		public ExplorerAIData data;

		public ExplorerAI(ExplorerAIData d) {
			data = d;
			if (d?.explorer != null) {
				Register(d.explorer.owner, this);
			}
		}

		// Every ExplorerAI created for a player's units, so that we can find
		// the active exploration targets without scanning all units.
		private static readonly UnitAiRegistry<ExplorerAI> registry = new(ai => ai.data?.explorer);

		private static void Register(Player player, ExplorerAI ai) {
			registry.Register(player, ai);
		}

		public static ExplorerAIData? MaybeMakeAiData(MapUnit unit, Player player) {
			ForgetAbandonedExplorationTargets(player, unit);

			HashSet<Tile> borderTiles = player.tileKnowledge.borderTiles;

			IEnumerable<Tile> candidates = borderTiles.Where(x => (x.IsLand() && unit.IsLandUnit()) || (!x.IsLand() && !unit.IsLandUnit()));
			ExplorerAIData? result = PickBestTileToExplore(unit, CalculateExplorationScores(player, unit, candidates));
			if (result != null) {
				result.explorer = unit;
			}

			if (result == null) {
				if (!unit.IsLandUnit()) {
					player.tileKnowledge.fullyExploredOceans = true;
				}

				return result;
			}

			log.Information($"Set AI for unit {unit.id} at {unit.location} to explore with destination of " + result.destination);
			player.tileKnowledge.aiExplorationTargets.Add(result.destination);
			return result;
		}

		UnitAI.MoveResult UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			if (data == null) {
				return UnitAI.Result.Error;
			}

			if (player.tileKnowledge.isTileKnown(data.destination)) {
				player.tileKnowledge.aiExplorationTargets.Remove(data.destination);
				return UnitAI.Result.Done;
			}

			// If we're at the destination we're done.
			if (unit.location == data.destination) {
				player.tileKnowledge.aiExplorationTargets.Remove(data.destination);
				return UnitAI.Result.Done;
			}

			return this.TryToMoveAlongPath(unit, ref data.pathToDestination);
		}

		public string SummarizePlan() {
			return "ExplorerAI: " + data.ToString();
		}

		public void UpdateOnDeath() { }

		// Targets are only removed when an explorer reaches or sees them, so
		// explorers that die or get another job leave theirs behind. Keep only
		// the targets other explorers are still heading to, so the set (which
		// is scanned for every candidate tile) doesn't grow without limit.
		//
		// The explorers are found through the registry of ExplorerAIs rather
		// than by scanning every unit of the player.
		private static void ForgetAbandonedExplorationTargets(Player player, MapUnit unit) {
			HashSet<Tile> activeTargets = new();
			registry.ForEachActive(player, ai => {
				if (ai.data.explorer != unit && ai.data.destination != null) {
					activeTargets.Add(ai.data.destination);
				}
			});
			player.tileKnowledge.aiExplorationTargets.RemoveWhere(t => !activeTargets.Contains(t));
		}

		private static int DistanceToNearestCity(Player player, Tile t) {
			int result = int.MaxValue;
			foreach (City c in player.cities) {
				int distance = t.DistanceTo(c.location);
				if (distance < result) {
					result = distance;
				}
			}
			return result;
		}

		private static int DistanceToNearestExplorationTarget(Player player, Tile t) {
			int result = int.MaxValue;
			foreach (Tile target in player.tileKnowledge.aiExplorationTargets) {
				int distance = t.DistanceTo(target);
				if (distance < result) {
					result = distance;
				}
			}
			return result;
		}

		private static Dictionary<Tile, float> CalculateExplorationScores(Player player, MapUnit unit, IEnumerable<Tile> possibleNewLocations) {
			Dictionary<Tile, float> explorationScores = new Dictionary<Tile, float>();
			foreach (Tile t in possibleNewLocations) {
				int numUnknownNeighbors = numUnknownNeighboringTiles(player, t);

				// Don't waste time on uninteresting tiles.
				if (numUnknownNeighbors == 0) {
					continue;
				}

				// Don't waste time on tiles already being explored.
				if (player.tileKnowledge.aiExplorationTargets.Contains(t)) {
					continue;
				}

				// Give a large score to tiles that we don't know much about.
				int score = numUnknownNeighbors;
				score *= numUnknownNeighbors;

				// But penalize tiles that are far away from our cities, to
				// encourate semi-local exploration. Without this we won't know
				// city sites.
				if (player.cities.Count > 0) {
					score -= DistanceToNearestCity(player, t);
				}

				// Similarly with tiles that are far away from us. We use
				// distanceTo as a quick heuristic to avoid expensive
				// pathfinding.
				score -= unit.location.DistanceTo(t);

				// Finally, reward tiles that are far away from tiles already
				// being explored by other explorers to encourage exploring in
				// different directions.
				if (player.tileKnowledge.aiExplorationTargets.Count > 0) {
					score += DistanceToNearestExplorationTarget(player, t);
				}

				explorationScores[t] = score;
			}

			return explorationScores;
		}

		private static ExplorerAIData? PickBestTileToExplore(MapUnit unit, Dictionary<Tile, float> explorationScores) {
			if (explorationScores.Count == 0) {
				return null;
			}

			// Pick the best tile we can actually reach. This is a stable sort,
			// so ties keep the order in which the candidates were scored.
			List<Tile> orderedTiles = explorationScores.OrderByDescending(t => t.Value).Select(t => t.Key).ToList();
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
			int index = algorithm.FindFirstReachable(unit.location, orderedTiles, unit, out TilePath path);
			if (index < 0) {
				return null;
			}

			ExplorerAIData result = new();
			result.destination = orderedTiles[index];
			result.pathToDestination = path;
			return result;
		}

		private static int numUnknownNeighboringTiles(Player player, Tile t) {
			//Do not try to explore a tile with a city.  If we own it, we know all tiles.
			//If someone else does, that would be war, which is not a scout's job.
			if (t.cityAtTile != null) {
				return 0;
			}
			//Calculate whether it, and its neighbors are in known tiles.
			int discoverableTiles = 0;
			if (!player.tileKnowledge.isTileKnown(t)) {
				discoverableTiles++;
			}
			foreach (Tile n in t.neighbors.Values) {
				if (!player.tileKnowledge.isTileKnown(n)) {
					discoverableTiles++;
				}
			}
			return discoverableTiles;
		}
	}
}

using C7Engine.Pathing;
using C7GameData;
using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData.AIData;
using C7Engine.AI;
using C7Engine.AI.UnitAI;
using Serilog;

namespace C7Engine {
	public class WorkerAI : UnitAI {
		private static ILogger log = Log.ForContext<WorkerAI>();
		private WorkerAIData data;

		public WorkerAI(WorkerAIData d) {
			data = d;
			if (d?.worker != null) {
				registry.Register(d.worker.owner, this);
			}
		}

		// Every WorkerAI created for a player's units, so that we can find the
		// tiles other workers are heading to without scanning all units.
		private static readonly UnitAiRegistry<WorkerAI> registry = new(ai => ai.data?.worker);

		// While an AI player's units act, what its workers plan around barely
		// changes, so it is computed once and shared between them: the tiles
		// worth improving, and the best terraform for each tile and kind of
		// worker. The terraforms are recomputed whenever a job completes, as
		// that can change what other tiles allow (e.g. irrigation chains or
		// newly connected resources). Outside of this, e.g. for a human's
		// automated workers, everything is computed fresh.
		private sealed class PlanningCache {
			private readonly Player player;

			private int cityCount = -1;
			private int knownTechCount = -1;
			private Government government;
			private List<Tile> highPriorityTiles;
			private List<Tile> lowPriorityTiles;

			private readonly Dictionary<(Tile, UnitPrototype), Terraform> bestTerraforms = new();

			public PlanningCache(Player player) {
				this.player = player;
			}

			// Throws away everything if the player gained or lost a city, a
			// tech, or changed government since it was computed.
			private void Validate() {
				if (cityCount != player.cities.Count || knownTechCount != player.knownTechs.Count || government != player.government) {
					cityCount = player.cities.Count;
					knownTechCount = player.knownTechs.Count;
					government = player.government;
					highPriorityTiles = null;
					lowPriorityTiles = null;
					bestTerraforms.Clear();
				}
			}

			public (List<Tile> high, List<Tile> low) PriorityTiles() {
				Validate();
				if (highPriorityTiles == null) {
					(highPriorityTiles, lowPriorityTiles) = ComputePriorityTiles(player);
				}
				return (highPriorityTiles, lowPriorityTiles);
			}

			public Terraform? BestTerraform(Tile t, MapUnit unit) {
				Validate();
				if (!bestTerraforms.TryGetValue((t, unit.unitType), out Terraform? result)) {
					result = ComputeTileImprovement(t, unit);
					bestTerraforms[(t, unit.unitType)] = result;
				}
				return result;
			}

			public void OnJobCompleted() {
				bestTerraforms.Clear();
			}
		}

		private static readonly Dictionary<Player, PlanningCache> planningCaches = new();

		// Shares planning work between the player's workers until the returned
		// object is disposed. Only use this while nothing but the player's own
		// units can change the map.
		internal static IDisposable BeginPlanning(Player player) {
			if (planningCaches.ContainsKey(player)) {
				return new PlanningScope(null);
			}
			planningCaches[player] = new PlanningCache(player);
			return new PlanningScope(player);
		}

		private sealed class PlanningScope(Player player) : IDisposable {
			public void Dispose() {
				if (player != null) {
					planningCaches.Remove(player);
				}
			}
		}

		public static WorkerAIData MakeAiData(MapUnit unit, Player player) {
			List<Tile> highPriorityTiles;
			List<Tile> lowPriorityTiles;
			if (planningCaches.TryGetValue(player, out PlanningCache cache)) {
				(highPriorityTiles, lowPriorityTiles) = cache.PriorityTiles();
			} else {
				(highPriorityTiles, lowPriorityTiles) = ComputePriorityTiles(player);
			}

			// Don't send several workers to the same tile: skip tiles another
			// of our workers is already heading to or working on.
			HashSet<Tile> reservedTiles = new();
			registry.ForEachActive(player, otherWorker => {
				if (otherWorker.data.worker != unit && otherWorker.data.destination != null) {
					reservedTiles.Add(otherWorker.data.destination);
				}
			});

			WorkerAIData? result = GetPlanToImproveNearestUnimproved(unit, player, highPriorityTiles, reservedTiles);
			if (result != null) {
				return result;
			}

			// If all our high priority tiles are improved, improve the lower
			// priority tiles.
			return GetPlanToImproveNearestUnimproved(unit, player, lowPriorityTiles, reservedTiles);
		}

		// The tiles our workers might improve. First, the tiles our civ works
		// and the resources we know about in our borders, then the rest of the
		// tiles in our borders. Each tile is only listed once, the first time
		// it comes up.
		private static (List<Tile> high, List<Tile> low) ComputePriorityTiles(Player player) {
			// First, check if there are any tiles our civ works that we can
			// improve.
			HashSet<Tile> seen = new();
			List<Tile> highPriorityTiles = new();
			foreach (City city in player.cities) {
				foreach (CityResident cr in city.residents) {
					if (cr.tileWorked != Tile.NONE && seen.Add(cr.tileWorked)) {
						highPriorityTiles.Add(cr.tileWorked);
					}
				}
			}

			// Owned tiles that aren't worked are lower priority, but if they
			// have a resource they're a higher priority.
			List<Tile> lowPriorityTiles = new();
			HashSet<Tile> seenLowPriority = new();
			foreach (City city in player.cities) {
				foreach (Tile t in city.GetTilesWithinBorders()) {
					if ((t.Resource.Category == ResourceCategory.LUXURY || t.Resource.Category == ResourceCategory.STRATEGIC)
						&& player.KnowsAboutResource(t.Resource)) {
						if (seen.Add(t)) {
							highPriorityTiles.Add(t);
						}
					} else if (seenLowPriority.Add(t)) {
						lowPriorityTiles.Add(t);
					}
				}
			}

			// A tile that is also high priority was already considered (and
			// rejected) by the time we look at the low priority tiles.
			lowPriorityTiles.RemoveAll(seen.Contains);

			return (highPriorityTiles, lowPriorityTiles);
		}

		public string SummarizePlan() {
			return "WorkerAI: " + data.ToString();
		}

		public void UpdateOnDeath() { }

		UnitAI.MoveResult UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			if (data == null) {
				return UnitAI.Result.Error;
			}

			// If we're at the destination, do our planned move.
			if (unit.location == data.destination) {
				return PerformWorkerMove(unit, data.workerMove);
			}

			// Otherwise we're moving towards our actual goal. To avoid wasting
			// worker moves, see if there's anything we can do on this tile
			// before moving to the next.
			//
			// This also functions as a way to build a road network, since we
			// will eventually road between all worked tiles of all cities.
			Terraform? improvement = GetTileImprovement(unit.location, unit);
			if (improvement != null) {
				return PerformWorkerMove(unit, improvement);
			}

			return this.TryToMoveAlongPath(unit, ref data.pathToDestination);
		}

		private static Terraform? GetTileImprovement(Tile t, MapUnit unit) {
			if (planningCaches.TryGetValue(unit.owner, out PlanningCache cache)) {
				return cache.BestTerraform(t, unit);
			}
			return ComputeTileImprovement(t, unit);
		}

		private static Terraform? ComputeTileImprovement(Tile t, MapUnit unit) {
			if (!t.IsLand()) {
				return null;
			}

			Player player = unit.owner;
			List<Terraform> terraforms = EngineStorage.gameData.Terraforms;

			// Don't waste worker moves on unowned tiles. We do allow roading
			// unowned tiles though.
			if (t.owningCity == null || t.owningCity.owner != player) {
				foreach (Terraform terraform in terraforms) {
					if (terraform.ProvidesRoad() && unit.CanPerformTerraformAction(terraform, t)) {
						return terraform;
					}
				}
				return null;
			}

			// The highest scoring terraform, preferring quicker ones and then
			// ones listed first.
			Terraform? best = null;
			int bestScore = 0;
			foreach (Terraform terraform in terraforms) {
				if (!unit.CanPerformTerraformAction(terraform, t)) {
					continue;
				}
				int aiScore = terraform.CalculateAIScore(player, t);
				if (aiScore <= 0) {
					continue;
				}
				if (best == null || aiScore > bestScore || (aiScore == bestScore && terraform.TurnsToComplete < best.TurnsToComplete)) {
					best = terraform;
					bestScore = aiScore;
				}
			}

			return best;
		}

		private UnitAI.Result PerformWorkerMove(MapUnit unit, Terraform workerMove) {
			if (unit.CanPerformTerraformAction(workerMove)) {
				unit.PerformTerraformAction(workerMove);

				// A finished job clears the worker's job, and may change which
				// terraforms other tiles allow.
				if (unit.WorkerJob == null && planningCaches.TryGetValue(unit.owner, out PlanningCache cache)) {
					cache.OnJobCompleted();
				}
				return UnitAI.Result.InProgress;
			}

			return UnitAI.Result.Done;
		}

		// Given a list of tile candidates, return a plan to improve the nearest
		// unimproved tile that no other worker has claimed. Among equally near
		// tiles, the one with the best yield wins, and then the one listed
		// first.
		private static WorkerAIData? GetPlanToImproveNearestUnimproved(MapUnit unit, Player player, List<Tile> tiles, HashSet<Tile> reservedTiles) {
			// Sort the tiles by distance, keeping the list order for ties.
			int[] distances = new int[tiles.Count];
			int[] order = new int[tiles.Count];
			for (int i = 0; i < tiles.Count; ++i) {
				distances[i] = tiles[i].DistanceTo(unit.location);
				order[i] = i;
			}
			Array.Sort(order, (a, b) => {
				int byDistance = distances[a].CompareTo(distances[b]);
				return byDistance != 0 ? byDistance : a.CompareTo(b);
			});

			// Go through the tiles one distance at a time, skipping tiles the
			// worker can't get to (e.g. a coastal city's tiles across the
			// water), or it would never get anything done.
			List<(Tile tile, Terraform improvement)> options = new();
			List<double> yields = new();
			List<Tile> ordered = new();
			PathingAlgorithm algorithm = null;
			for (int start = 0; start < order.Length;) {
				int end = start;
				while (end < order.Length && distances[order[end]] == distances[order[start]]) {
					++end;
				}

				options.Clear();
				for (int i = start; i < end; ++i) {
					Tile t = tiles[order[i]];
					if (reservedTiles.Contains(t)) {
						continue;
					}
					Terraform? improvement = GetTileImprovement(t, unit);
					if (improvement != null) {
						options.Add((t, improvement));
					}
				}

				if (options.Count > 0) {
					// Prefer the best yield, and then the tile listed first. Only
					// compute yields when there's a choice to make.
					if (options.Count > 1) {
						yields.Clear();
						foreach ((Tile tile, Terraform _) in options) {
							yields.Add(CityTileAssignmentAI.CalculateTileYieldScore(tile, 2, player));
						}
						int[] byYield = new int[options.Count];
						for (int i = 0; i < byYield.Length; ++i) {
							byYield[i] = i;
						}
						Array.Sort(byYield, (a, b) => {
							int byScore = yields[b].CompareTo(yields[a]);
							return byScore != 0 ? byScore : a.CompareTo(b);
						});
						List<(Tile, Terraform)> sorted = new(options.Count);
						foreach (int i in byYield) {
							sorted.Add(options[i]);
						}
						options.Clear();
						options.AddRange(sorted);
					}

					ordered.Clear();
					foreach ((Tile tile, Terraform _) in options) {
						ordered.Add(tile);
					}
					algorithm ??= PathingAlgorithmChooser.GetAlgorithm(unit);
					int index = algorithm.FindFirstReachable(unit.location, ordered, unit, out TilePath path);
					if (index >= 0) {
						return MakePlan(unit, options[index].tile, options[index].improvement, path);
					}
				}

				start = end;
			}
			return null;
		}

		private static WorkerAIData MakePlan(MapUnit unit, Tile destination, Terraform improvement, TilePath path) {
			WorkerAIData result = new () {
				workerMove = improvement,
				destination = destination,
				pathToDestination = path,
				worker = unit,
			};
			log.Information($"Set AI for unit at {unit.location} to {improvement} with destination of " + result.destination);
			return result;
		}
	}
}

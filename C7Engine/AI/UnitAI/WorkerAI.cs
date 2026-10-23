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
			private List<Tile> connectionRoads;
			private HashSet<Tile> connectionRoadSet;

			public PlanningCache(Player player) {
				this.player = player;
			}

			// The road-less tiles on the roads that would join our cities
			// and resources to the capital (see ComputeConnectionRoads).
			// Roads built during the turn don't change it, which at worst
			// sends a second worker to a tile a road was just finished on.
			public (List<Tile> tiles, HashSet<Tile> set) ConnectionRoads(MapUnit unit) {
				Validate();
				if (connectionRoads == null) {
					connectionRoads = ComputeConnectionRoads(player, unit);
					connectionRoadSet = new HashSet<Tile>(connectionRoads);
				}
				return (connectionRoads, connectionRoadSet);
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
					connectionRoads = null;
					connectionRoadSet = null;
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
			bool colonyTrip = false;
			bool outpostTrip = false;
			registry.ForEachActive(player, otherWorker => {
				if (otherWorker.data.worker != unit && otherWorker.data.destination != null) {
					reservedTiles.Add(otherWorker.data.destination);
					colonyTrip |= otherWorker.data.buildColony;
					outpostTrip |= otherWorker.data.workerMove?.Improvement?.key == Tile.TileOverlays.OUTPOST;
				}
			});

			// Tiles where enemies could catch the worker are skipped, as are
			// those in the land of civs we're at war with (see AIThreat).
			AIFogOfWar.Refresh(player);
			AIThreat threat = AIThreat.Assess(player);
			if (threat.AnyEnemies || PlayerRelationship.IsInAnyWar(player, EngineStorage.gameData.players)) {
				foreach (Tile t in highPriorityTiles) {
					if (threat.IsUnsafeForCivilian(t)) {
						reservedTiles.Add(t);
					}
				}
				foreach (Tile t in lowPriorityTiles) {
					if (threat.IsUnsafeForCivilian(t)) {
						reservedTiles.Add(t);
					}
				}
			}

			// Under Rising Tide, nor tiles the sea will take before the
			// work pays off (see TideAI).
			if (TideAI.IsOn()) {
				foreach (Tile t in highPriorityTiles.Concat(lowPriorityTiles)) {
					if (TideAI.DrownsWithin(t, TideAI.MinImprovedTileTurns)) {
						reservedTiles.Add(t);
					}
				}
			}

			// Looking for a tile the worker can reach may take a search over
			// every tile it can reach, which is kept for the next question
			// rather than done again for each distance and list.
			using ReachableTiles reachable = new();

			// Now and then a worker founds a colony or builds an outpost
			// (see WorkerHoldingsAI).
			PathingAlgorithm algorithm = null;
			List<Tile> colonySites = WorkerHoldingsAI.ColonySites(unit, player, colonyTrip);
			colonySites.RemoveAll(threat.IsUnsafeForCivilian);
			colonySites.RemoveAll(t => TideAI.DrownsWithin(t, TideAI.MinImprovedTileTurns));
			if (colonySites.Count > 0) {
				algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
				int index = algorithm.FindFirstReachable(unit.location, colonySites, unit, reachable, out TilePath path);
				if (index >= 0) {
					WorkerAIData colony = MakePlan(unit, colonySites[index], null, path);
					colony.buildColony = true;
					return colony;
				}
			}
			List<Tile> outpostSites = WorkerHoldingsAI.OutpostSites(unit, player, outpostTrip);
			outpostSites.RemoveAll(threat.IsUnsafeForCivilian);
			outpostSites.RemoveAll(t => TideAI.DrownsWithin(t, TideAI.MinImprovedTileTurns));
			if (outpostSites.Count > 0) {
				algorithm ??= PathingAlgorithmChooser.GetAlgorithm(unit);
				int index = algorithm.FindFirstReachable(unit.location, outpostSites, unit, reachable, out TilePath path);
				if (index >= 0) {
					return MakePlan(unit, outpostSites[index], WorkerHoldingsAI.OutpostTerraform(unit), path);
				}
			}

			// Joining our cities and resources to the capital by road comes
			// before improving tiles, for a quarter of our workers (so once
			// we have four), and only on our own land. (In AI trials a third of
			// the workers, at least one, also roading unclaimed land, cost
			// civs two cities on Pangaeas: early worker turns are worth more
			// on tiles. This narrower rule did no harm.)
			(List<Tile> connectionTiles, HashSet<Tile> connectionSet) = cache != null ? cache.ConnectionRoads(unit)
				: (ComputeConnectionRoads(player, unit), null);
			if (connectionTiles.Count > 0) {
				connectionSet ??= new HashSet<Tile>(connectionTiles);
				int roadWorkers = 0;
				int workers = 0;
				registry.ForEachActive(player, other => {
					if (other.data.worker == unit) {
						return;
					}
					++workers;
					if (other.data.destination != null && connectionSet.Contains(other.data.destination)
							&& other.data.workerMove != null && other.data.workerMove.ProvidesRoad()) {
						++roadWorkers;
					}
				});
				int share = (workers + 1) / 4;
				if (roadWorkers < share) {
					List<Tile> open = connectionTiles.Where(t => !reservedTiles.Contains(t) && RoadTerraform(unit, t) != null
							&& t.OwningPlayer() == player)
						.OrderBy(t => t.DistanceTo(unit.location)).ToList();
					if (open.Count > 0) {
						algorithm ??= PathingAlgorithmChooser.GetAlgorithm(unit);
						int index = algorithm.FindFirstReachable(unit.location, open, unit, reachable, out TilePath path);
						if (index >= 0) {
							log.Debug("{Unit} builds a road at {Tile} to join the empire up", unit, open[index]);
							return MakePlan(unit, open[index], RoadTerraform(unit, open[index]), path);
						}
					}
				}
			}

			WorkerAIData? result = GetPlanToImproveNearestUnimproved(unit, player, highPriorityTiles, reservedTiles, reachable);
			if (result != null) {
				return result;
			}

			// If all our high priority tiles are improved, improve the lower
			// priority tiles.
			return GetPlanToImproveNearestUnimproved(unit, player, lowPriorityTiles, reservedTiles, reachable);
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
			// A worker enemies could catch stops work and makes for one of
			// our cities; one whose job is where enemies could catch it
			// finds another (see AIThreat).
			AIThreat threat = AIThreat.Assess(player);
			if (threat.IsUnsafeForCivilian(unit.location) && MakeForCity(player, unit, "runs from the enemy to") is UnitAI.MoveResult flee) {
				return flee;
			}
			if (data == null) {
				return Idle(player, unit);
			}
			if (unit.location != data.destination && threat.IsUnsafeForCivilian(data.destination)) {
				log.Debug("{Unit} gives up its job at {Destination}, where enemies could catch it", unit, data.destination);
				return UnitAI.Result.Error;
			}

			// If we're at the destination, do our planned move.
			if (unit.location == data.destination) {
				if (data.buildColony) {
					return FoundColony(unit);
				}
				return PerformWorkerMove(unit, data.workerMove);
			}

			// On the way to a colony, the worker only builds roads, which
			// link the colony to our cities.
			if (data.buildColony) {
				Terraform? road = unit.location.HasRoad() ? null : RoadTerraform(unit, unit.location);
				if (road != null) {
					return PerformWorkerMove(unit, road);
				}
				return this.TryToMoveAlongPath(unit, ref data.pathToDestination);
			}

			// Otherwise we're moving towards our actual goal. To avoid wasting
			// worker moves, see if there's anything we can do on this tile
			// before moving to the next.
			//
			// This also functions as a way to build a road network, since we
			// will eventually road between all worked tiles of all cities.
			// (Stopping only for roads on the connecting roads and for tiles
			// cities work did no better in AI trials.)
			Terraform? improvement = GetTileImprovement(unit.location, unit);
			if (improvement != null) {
				return PerformWorkerMove(unit, improvement);
			}

			return this.TryToMoveAlongPath(unit, ref data.pathToDestination);
		}

		// HEURISTIC: what a step onto another civ's land adds to the cost of
		// a connecting road, so that it keeps to our land (and unclaimed
		// land) where it can, as a road through a neighbour can be cut.
		private const float ForeignRoadStepCost = 2;

		// The road-less tiles on the cheapest roads that would join each of
		// our cities, and each known luxury and strategic resource in our
		// borders, to the capital's network: from the city to the capital,
		// and from the resource to the nearest of our cities that is joined
		// to it. Cities on other landmasses are left to harbors. Existing
		// roads cost little to walk, so the paths make use of them. (An AI
		// heuristic: Civ3 sources only say that cities and resources must
		// be joined by road for trade, see TradeNetwork.)
		private static List<Tile> ComputeConnectionRoads(Player player, MapUnit unit) {
			List<Tile> result = new();
			GameData gameData = EngineStorage.gameData;
			City capital = player.cities.Find(c => c.IsCapital()) ?? player.cities.FirstOrDefault();
			if (gameData == null || capital == null || !unit.IsLandUnit()) {
				return result;
			}
			TradeNetwork network = gameData.GetTradeNetwork();
			HashSet<Tile> seen = new();
			AStarAlgorithm algorithm = new(unit) {
				StepPenalty = t => t.OwningPlayer() is Player owner && owner != player ? ForeignRoadStepCost : 0,
			};
			void AddRoadless(TilePath path) {
				if (path == null) {
					return;
				}
				foreach (Tile t in path.path) {
					if (t.IsLand() && !t.HasRoad() && !t.HasCity() && (t.OwningPlayer() == null || t.OwningPlayer() == player)
							&& !AIThreat.IsHostileTerritory(player, t) && seen.Add(t)) {
						result.Add(t);
					}
				}
			}

			int continent = capital.location.continent;
			List<Tile> joined = player.cities.Where(c => c.location.continent == continent && network.ConnectedToCapital(player, c))
				.Select(c => c.location).ToList();
			foreach (City c in player.cities) {
				if (c == capital || c.location.continent != continent || network.ConnectedToCapital(player, c)) {
					continue;
				}
				AddRoadless(algorithm.PathFrom(c.location, capital.location, unit));
			}
			foreach (City c in player.cities) {
				if (c.location.continent != continent) {
					continue;
				}
				foreach (Tile t in c.GetTilesWithinBorders()) {
					if (t.owningCity != c || t.Resource == null || !player.KnowsAboutResource(t.Resource)
							|| (t.Resource.Category != ResourceCategory.LUXURY && t.Resource.Category != ResourceCategory.STRATEGIC)
							|| network.JoinedToCapital(player, t)) {
						continue;
					}
					Tile nearest = joined.OrderBy(j => j.DistanceTo(t)).FirstOrDefault();
					if (nearest != null) {
						AddRoadless(algorithm.PathFrom(t, nearest, unit));
					}
				}
			}
			return result;
		}

		// The turn a worker with nothing to do started waiting, or -1.
		private int idleSinceTurn = -1;

		// A worker with nothing to do goes to the nearest of our cities and
		// waits out the turn there, out of harm's way; next turn it looks
		// for work again (its plan fails, so it gets a new one), once a
		// turn rather than every time it's asked.
		private UnitAI.MoveResult Idle(Player player, MapUnit unit) {
			int turn = EngineStorage.gameData?.turn ?? 0;
			if (idleSinceTurn >= 0 && idleSinceTurn != turn) {
				return UnitAI.Result.Error;
			}
			idleSinceTurn = turn;
			if (unit.location.cityAtTile?.owner != player && MakeForCity(player, unit, "has nothing to do, so goes to") is UnitAI.MoveResult go) {
				return go;
			}
			unit.movementPoints.onConsumeAll();
			return UnitAI.Result.InProgress;
		}

		// The path to the city the worker is going to, while it is.
		private TilePath refugePath;

		// Moves the worker towards the nearest of our cities it can reach.
		// Returns null if there is none.
		private UnitAI.MoveResult? MakeForCity(Player player, MapUnit unit, string why) {
			if (refugePath == null || refugePath.destination?.cityAtTile?.owner != player) {
				List<Tile> cities = player.cities.Select(c => c.location).OrderBy(t => t.DistanceTo(unit.location)).ToList();
				if (cities.Count == 0
						|| PathingAlgorithmChooser.GetAlgorithm(unit).FindFirstReachable(unit.location, cities, unit, out TilePath path) < 0) {
					refugePath = null;
					return null;
				}
				refugePath = path;
				log.Information("{Unit} {Why} {City}", unit, why, path.destination.cityAtTile);
			}
			UnitAI.MoveResult result = this.TryToMoveAlongPath(unit, ref refugePath);
			if (result.Result == UnitAI.Result.Error) {
				refugePath = null;
				return null;
			}
			return result;
		}

		private static Terraform? GetTileImprovement(Tile t, MapUnit unit) {
			if (planningCaches.TryGetValue(unit.owner, out PlanningCache cache)) {
				return cache.BestTerraform(t, unit);
			}
			return ComputeTileImprovement(t, unit);
		}

		// The terraform that builds a road on the tile, if the unit can.
		private static Terraform? RoadTerraform(MapUnit unit, Tile t) {
			foreach (Terraform terraform in EngineStorage.gameData.Terraforms) {
				if (terraform.ProvidesRoad() && unit.CanPerformTerraformAction(terraform, t)) {
					return terraform;
				}
			}
			return null;
		}

		// At the colony's site, the worker roads the tile, so the trade
		// network reaches the resource, and then founds the colony, which
		// uses it up.
		private static UnitAI.Result FoundColony(MapUnit unit) {
			if (!unit.location.CanTakeColony(unit.owner)) {
				return UnitAI.Result.Done;
			}
			if (!unit.location.HasRoad() && RoadTerraform(unit, unit.location) is Terraform road) {
				unit.PerformTerraformAction(road);
				return UnitAI.Result.InProgress;
			}
			if (!unit.CanBuildColony()) {
				return UnitAI.Result.InProgress;
			}
			unit.BuildColony();
			return UnitAI.Result.Done;
		}

		// Colonies and outposts (whose terraform scores 0, see
		// terraforms.ai_score.default) are planned in WorkerHoldingsAI, not
		// here.
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

				// Building over an improvement (e.g. irrigating a mined tile)
				// throws the old one away, so only what it adds on top of the
				// old one counts. Without this, whichever of a mine or
				// irrigation the tile has can't be built again, so the other
				// always looked like a pure gain and workers swapped the two
				// back and forth forever.
				TerrainImprovement replaced = t.overlays.GetReplacementTarget(terraform);
				if (replaced != null) {
					aiScore -= KeptImprovementScore(player, t, replaced) + ReplacementMargin;
				}
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

		// How much better than the improvement already on a tile a replacement
		// must score before a worker tears the old one down. Small differences
		// aren't worth the worker turns, and the margin keeps the choice from
		// wavering between two improvements that score about the same.
		private const int ReplacementMargin = 1;

		// The AI score of the improvement already on the tile, as if it were
		// being built now, or 0 if no terraform builds it.
		private static int KeptImprovementScore(Player player, Tile t, TerrainImprovement existing) {
			foreach (Terraform terraform in EngineStorage.gameData.Terraforms) {
				if (terraform.Improvement == existing) {
					return Math.Max(0, terraform.CalculateAIScore(player, t));
				}
			}
			return 0;
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
		private static WorkerAIData? GetPlanToImproveNearestUnimproved(MapUnit unit, Player player, List<Tile> tiles, HashSet<Tile> reservedTiles,
				ReachableTiles reachable) {
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
					int index = algorithm.FindFirstReachable(unit.location, ordered, unit, reachable, out TilePath path);
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
			log.Debug("Set AI for unit at {Location} to {Improvement} with destination of {Destination}", unit.location, improvement, result.destination);
			return result;
		}
	}
}

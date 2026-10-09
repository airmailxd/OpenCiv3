using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.AIData;
using C7Engine.AI;
using C7Engine.AI.StrategicAI;
using C7Engine.AI.UnitAI;
using Serilog;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static C7GameData.PlayerRelationship;

namespace C7Engine {
	public class PlayerAI {
		private static ILogger log = Log.ForContext<PlayerAI>();

		public static readonly int MAX_LAND_EXPLORERS = 10;
		public static readonly int MAX_WATER_EXPLORERS = 4;

		public static async Task PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.isBarbarians || !player.isIncludedInGame) {
				return;
			}
			List<Tech> techs = gameData.techs;

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			log.Information("-> Begin " + player.civilization.cityNames[0] + " turn");

			// TODO: Before we call this method to automatically end obsolete deals, we could make this more versatile.
			// For example unless we have a good reason, as a human, receiving luxuries, gpt,
			// or having an active RoP, doesn't hurt us.
			PlayerRelationship.CheckForObsoleteDeals(player, EngineStorage.gameData.players, EngineStorage.gameData.turn);

			// Tell anyone wandering around our territory to leave. Any war that
			// starts gets planned for in the priorities and unit moves below.
			await TerritoryDemands.MakeDemands(player, gameData);

			MaybeDoPriorityReevaluation(player);
			MaybePickTechToResearch(player, techs);

			// Roughly every 4 turns, see if there are trades to be made.
			if (GameData.rng.Next(100) < 25) {
				await AttemptTrading(player);
			}

			EspionageAI.PlayTurn(player, gameData);

			UpgradeUnits(player);
			await DoUnitActions(player);

			// Before ending the turn, adjust our sliders. We do this after unit
			// moves so that any worker moves that finished during the unit moves
			// will be usable (like if a luxury got hooked up).
			AdjustSliders(player);

			log.Information("-> End " + player.civilization.cityNames[0] + $" turn {stopwatch.ElapsedMilliseconds} milliseconds");
		}

		// Upgrade units sitting in cities that can upgrade them, keeping a
		// reserve of gold for emergencies.
		private static void UpgradeUnits(Player player) {
			const int GOLD_RESERVE = 100;
			Dictionary<City, CityUpgradeInfo> upgradeInfo = new();
			foreach (MapUnit unit in player.units.ToList()) {
				UnitPrototype upgrade = GetAvailableUpgrade(unit, upgradeInfo);
				if (upgrade != null && player.gold - unit.UpgradeCost(upgrade) >= GOLD_RESERVE) {
					// We just worked out the upgrade, so don't have Upgrade()
					// look it up (and the city's resources) again.
					unit.UpgradeTo(upgrade);
				}
			}
		}

		// What a city offers for upgrading units, gathered once per city.
		private sealed class CityUpgradeInfo {
			public bool upgradesLandUnits;
			public bool upgradesSeaUnits;
			public HashSet<Resource> resources;
		}

		// Same result as MapUnit.GetAvailableUpgrade, but shares each city's
		// buildings and resources between all the units in it instead of
		// recomputing them per unit.
		private static UnitPrototype GetAvailableUpgrade(MapUnit unit, Dictionary<City, CityUpgradeInfo> upgradeInfo) {
			if (!unit.unitType.actions.Contains(UnitAction.Upgrade)) {
				return null;
			}

			City city = unit.location?.cityAtTile;
			if (!City.IsValidCity(city) || city.owner != unit.owner) {
				return null;
			}

			if (!upgradeInfo.TryGetValue(city, out CityUpgradeInfo info)) {
				info = new CityUpgradeInfo();
				foreach (CityBuilding cb in city.GetBuildings()) {
					info.upgradesLandUnits |= cb.building.providesVeteranGroundUnits;
					info.upgradesSeaUnits |= cb.building.providesVeteranSeaUnits;
				}
				upgradeInfo[city] = info;
			}

			bool hasUpgradeBuilding = (unit.IsLandUnit() && info.upgradesLandUnits)
				|| (unit.IsWaterUnit() && info.upgradesSeaUnits);
			if (!hasUpgradeBuilding) {
				return null;
			}

			info.resources ??= city.GetAvailableResources(EngineStorage.gameData).Keys.ToHashSet();
			return unit.unitType.GetProducibleUpgrade(city, info.resources);
		}

		private static void MaybeDoPriorityReevaluation(Player player) {
			if (player.turnsUntilPriorityReevaluation == 0) {
				log.Information("Re-evaluating strategic priorities for " + player);
				List<StrategicPriority> priorities = StrategicPriorityArbitrator.Arbitrate(player);
				player.strategicPriorityData.Clear();
				foreach (StrategicPriority priority in priorities) {
					player.strategicPriorityData.Add(priority);
					priority.OnChosen(player);
				}
				player.turnsUntilPriorityReevaluation = 15 + GameData.rng.Next(10);

				string summary = string.Join(',', player.strategicPriorityData);
				log.Information($"Player {player.civilization.name} now has priorities of: {summary}. {player.turnsUntilPriorityReevaluation} turns until next re-evaluation");

				// Wake up any fortified units so our new strategy (if we have one)
				// takes effect.
				foreach (MapUnit u in player.units) {
					u.Wake();
				}
			} else {
				player.turnsUntilPriorityReevaluation--;
			}
		}

		public static void MaybePickTechToResearch(Player player, List<Tech> techs) {
			while (player.currentlyResearchedTech == null || player.knownTechs.Contains(player.currentlyResearchedTech)) {
				Tech toResearch = player.GetAvailableTechsToResearch(techs).FirstOrDefault();
				if (toResearch == null) {
					log.Information($"Player {player.civilization.name} has no techs available to research.");
					player.SetCurrentlyResearchedTech(null);
					break;
				}

				// Drop any queued techs we already know, so we never get stuck
				// re-selecting a known tech.
				while (player.ResearchQueue.Count > 0 && player.knownTechs.Contains(player.ResearchQueue.Peek().id)) {
					player.ResearchQueue.Dequeue();
				}
				if (player.ResearchQueue.Count <= 0) {
					player.AddTechItemToResearchQueue(toResearch);
				}
				player.SetCurrentlyResearchedTech(player.ResearchQueue.Peek().id);
				log.Information($"Player {player.civilization.name} is researching {player.ResearchQueue.Peek().Name}.");
			}
		}

		private static async Task DoUnitActions(Player player) {
			// Reorder the list so that settlers are second to last, giving our escorts a
			// chance to configure themselves without wasting a turn.
			// Finally reorder so that the workers are last, so in case a settler builds a new city,
			// they move to terraform a tile instead of wasting a turn (especially in the very first round in a new game).
			player.units = player.units.OrderBy(x => x.unitType.isSettler).ThenBy(x => x.unitType.isWorker).ToList();

			// Any time we have an unescorted settler, wake up any other units
			// on the same tile to force us to re-evaluate whether they should
			// be an escort. Otherwise, can have perfectly fine escorts sitting
			// there fortified.
			foreach (MapUnit u in player.units) {
				if (u.currentAI is SettlerAI settlerAi && settlerAi.data.escort == null) {
					foreach (MapUnit uu in u.location.unitsOnTile) {
						uu.Wake();
					}
				}
			}

			// Track the units that may be exploring, so counting explorers
			// doesn't need a pass over every unit each time a unit picks a plan.
			HashSet<MapUnit> explorers = new();
			foreach (MapUnit u in player.units) {
				if (u.currentAI is ExplorerAI) {
					explorers.Add(u);
				}
			}
			possibleExplorers[player] = explorers;
			try {
				using (WorkerAI.BeginPlanning(player)) {
					await DoUnitActions(player, explorers);
				}
			} finally {
				possibleExplorers.Remove(player);
			}
		}

		private static async Task DoUnitActions(Player player, HashSet<MapUnit> explorers) {
			// Do things with units. Copy into an array first to avoid collection-was-modified exception
			foreach (MapUnit unit in player.units.ToArray()) {
				// A great leader that has reached one of our cities is used
				// up there. Until then it heads for a city like a defender.
				if (UseLeaderInCity(unit, player)) {
					continue;
				}

				// Nuclear weapons fire at a worthwhile target if there is one,
				// and otherwise wait (they may be loaded, and so fortified,
				// aboard a submarine).
				if (unit.IsNuclearWeapon()) {
					if (unit.hitPointsRemaining > 0) {
						await NuclearAI.TryStrike(player, unit);
					}
					continue;
				}

				// Don't waste time recalculating behaviors for fortified units.
				// This means we'll have to unfortify all our units after
				// interesting events like war declarations, but this seems like
				// a good tradeoff for faster turns.
				if (unit.isFortified) {
					continue;
				}

				// For each unit, if there's already an AI task assigned, it will attempt to complete its goal.
				// It may fail due to conditions having changed since that goal was assigned; in that case it will
				// get a new task to try to complete.
				//
				// Cap our attempts at 2 to avoid getting stuck in bad situations.
				for (int attempt = 0; attempt < 2; ++attempt) {
					if (unit.currentAI == null) {
						unit.currentAI = GetAIForUnit(unit, player);
						if (unit.currentAI is ExplorerAI) {
							explorers.Add(unit);
						}
					}

					// If the unit is still the process of doing its plan, allow
					// it to continue next turn.
					UnitAI.Result result = await unit.currentAI.PlayTurn(player, unit);
					if (result == UnitAI.Result.InProgress) {
						break;
					}

					// A plan that failed, e.g. because its destination got blocked,
					// shouldn't cost the unit the rest of its turn: pick a new one
					// now if it can still move.
					if (result == UnitAI.Result.Error) {
						unit.currentAI = null;
						if (unit.hitPointsRemaining <= 0 || !unit.movementPoints.canMove) {
							break;
						}
						continue;
					}

					if (unit.hitPointsRemaining <= 0 || unit.isFortified) {
						unit.currentAI = null;
						break;
					}

					// Otherwise we need a new plan for next turn. Pick it now
					// to avoid things like new units being preferred for
					// exploration instead of units already far away from home
					// for exploration.
					unit.currentAI = GetAIForUnit(unit, player);
					if (unit.currentAI is ExplorerAI) {
						explorers.Add(unit);
					}
				}

				// Moving already adds what a unit sees to our knowledge and
				// recomputes the active tiles, as do units dying or being
				// captured, so only recompute them when this revealed new tiles.
				int knownTileCount = player.tileKnowledge.knownTiles.Count;
				player.tileKnowledge.AddTilesToKnown(unit.location, recomputeActiveTiles: false);
				if (player.tileKnowledge.knownTiles.Count != knownTileCount) {
					player.tileKnowledge.RecomputeActiveTiles();
				}
			}
		}

		// While a player's units are acting, the units that might have an
		// ExplorerAI: every unit that had one when the units started acting
		// or was given one since. Whether each still does is checked when
		// counting.
		private static readonly Dictionary<Player, HashSet<MapUnit>> possibleExplorers = new();

		// The number of the player's units exploring, among land units or
		// among other units.
		private static int CountExplorers(Player player, bool landUnits) {
			int result = 0;
			if (possibleExplorers.TryGetValue(player, out HashSet<MapUnit> explorers)) {
				foreach (MapUnit u in explorers) {
					// A unit that died or changed hands is no longer one of
					// our units.
					if (u.currentAI is ExplorerAI && u.IsLandUnit() == landUnits
						&& u.owner == player && u.hitPointsRemaining > 0) {
						++result;
					}
				}
				return result;
			}

			foreach (MapUnit u in player.units) {
				if (u.currentAI is ExplorerAI && u.IsLandUnit() == landUnits) {
					++result;
				}
			}
			return result;
		}

		// A leader in one of our cities finishes the city's improvement or
		// small wonder. A city building a unit is switched to its most
		// expensive improvement first. The AI doesn't use armies yet, so
		// leaders never form them.
		//
		// A city whose production is locked in (it was hurried this turn)
		// keeps the leader until next turn. One building a great wonder, or
		// with nothing a leader can hurry, sends it on to another city.
		// Returns true if the leader was used up.
		internal static bool UseLeaderInCity(MapUnit unit, Player player) {
			if (!unit.IsLeader() || !unit.CanOfferHurryProduction()) {
				return false;
			}
			if (unit.CanHurryProduction()) {
				return unit.HurryProductionAsLeader();
			}

			City city = unit.location.cityAtTile;
			if (city.hurriedThisTurn) {
				return false;
			}
			if (city.itemBeingProduced is Building current) {
				// An improvement finishing this turn anyway: wait for the
				// next one.
				if (!current.IsGreatWonder()) {
					return false;
				}
			} else if (BestImprovementForLeader(city) is Building improvement) {
				city.ChangeProduction(improvement);
				if (unit.CanHurryProduction()) {
					return unit.HurryProductionAsLeader();
				}
			}

			// Try another city.
			UnitAI ai = MakeLeaderAI(unit, player);
			if (ai is DefenderAI { data.destination: Tile destination } && destination != unit.location) {
				unit.currentAI = ai;
				unit.Wake();
			}
			return false;
		}

		// The most expensive improvement or small wonder the city can build
		// that a leader could hurry, or null.
		private static Building BestImprovementForLeader(City city) {
			Building best = null;
			foreach (IProducible p in city.ListProductionOptions(EngineStorage.gameData)) {
				if (p is Building b && !b.IsGreatWonder() && city.owner.ShieldCost(b) > city.shieldsStored
						&& (best == null || city.owner.ShieldCost(b) > city.owner.ShieldCost(best))) {
					best = b;
				}
			}
			return best;
		}

		// Whether a leader arriving in the city could be used there: it isn't
		// building a great wonder, and has an improvement to hurry.
		private static bool CityCanUseLeader(City city) {
			if (city.itemBeingProduced is Building b && b.IsGreatWonder()) {
				return false;
			}
			return BestImprovementForLeader(city) != null;
		}

		// Sends a leader straight to the nearest of our cities that can use
		// it, or failing that to the nearest city. A leader can't fight, so it
		// shouldn't explore or escort.
		internal static UnitAI MakeLeaderAI(MapUnit unit, Player player) {
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
			Tile destination = null;
			TilePath path = null;
			foreach (bool needUsable in new[] { true, false }) {
				foreach (City city in player.cities.OrderBy(c => c.location.DistanceTo(unit.location))) {
					if (needUsable && !CityCanUseLeader(city)) {
						continue;
					}
					if (city.location == unit.location) {
						destination = city.location;
						break;
					}
					TilePath p = algorithm.PathFrom(unit.location, city.location, unit);
					if (p != null && p.PathLength() > 0) {
						destination = city.location;
						path = p;
						break;
					}
				}
				if (destination != null) {
					break;
				}
			}

			// With no city to go to, the leader stays where it is.
			DefenderAIData data = new() {
				goal = DefenderAIData.DefenderGoal.DEFEND_CITY,
				destination = destination ?? unit.location,
				defender = unit,
				pathToDestination = path,
			};
			log.Information("Sending leader {Unit} to {Destination}", unit, data.destination);
			return new DefenderAI(data);
		}

		public static UnitAI GetAIForUnit(MapUnit unit, Player player) {
			//figure out an AI behavior
			//TODO: Use strategies, not names
			if (unit.IsLeader()) {
				return MakeLeaderAI(unit, player);
			} else if (unit.unitType.name == "Settler") {
				return new SettlerAI(SettlerAI.MakeAiData(unit, player));
			} else if (unit.unitType.name == "Worker") {
				return new WorkerAI(WorkerAI.MakeAiData(unit, player));
			} else if (unit.location.cityAtTile != null && unit.CanDefendOnLand() && unit.location.unitsOnTile.Count(u => u.CanDefendOnLand() && u != unit) == 0) {
				return new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(unit, player));
			} else if (GetCombatAIIfUnitCanAttackNearbyBarbCamp(unit, player) is UnitAI unitAI && unitAI != null) {
				return unitAI;
			} else if (unit.unitType.name == "Catapult") {
				//For now tell catapults to sit tight.  It's getting really annoying watching them pointlessly bombard barb camps forever
				return new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(unit, player));
			}

			// Special case: we're at war.
			if (IsInAnyWar(player, EngineStorage.gameData.players)) {
				// Priority 1: ensure we don't have any unguarded cities.
				//
				// If this is an offensive unit only go defend if there are
				// fewer than 3 units in a city, otherwise consider offensive
				// action.
				//
				// With minDefenders at int.MaxValue this never returns null
				// (it falls back to the best city), so the priority 3 fallback
				// below is only reached for offensive units, whose search here
				// used a different threshold and found nothing, returning
				// before any pathfinding. DefenderAI keeps the per-city inputs
				// of that search (defenders, units en route, distances) for the
				// unit, so the fallback reuses them and only re-scores.
				int minDefenders = unit.unitType.attack >= unit.unitType.defense ? 3 : int.MaxValue;
				DefenderAIData maybeDefend = DefenderAI.MakeAiDataForDefendAtRiskCity(unit, player, minDefenders);
				if (maybeDefend != null) {
					return new DefenderAI(maybeDefend);
				}

				// Priority 2: go on the offensive.
				CombatAIData maybeCombat = CombatAI.MakeAiData(unit, player);
				if (maybeCombat != null) {
					return new CombatAI(maybeCombat);
				}

				// Priority 3: defend our cities.
				return new DefenderAI(DefenderAI.MakeAiDataForDefendAtRiskCity(unit, player, minDefenders: int.MaxValue));
			}

			// If there's an unescorted settler, escort it.
			EscortAIData? maybeEscortData = EscortAI.MaybeMakeAiData(unit, player);
			if (maybeEscortData != null) {
				return new EscortAI(maybeEscortData);
			}

			// As long as we don't have too many explorers yet of this unit's
			// type (land vs sea), start a new exploring unit.
			int maxExplorers = unit.IsLandUnit() ? MAX_LAND_EXPLORERS : MAX_WATER_EXPLORERS;
			if (CountExplorers(player, unit.IsLandUnit()) < maxExplorers) {
				ExplorerAIData? maybeAiData = ExplorerAI.MaybeMakeAiData(unit, player);
				if (maybeAiData != null) {
					return new ExplorerAI(maybeAiData);
				}
			}

			//Nowhere to explore or too many explorers.  What to do now?
			//Priority 1: Adequate defense of cities.
			//Priority 2: Clearing out barbs
			//Priority 3: Defending chokepoints
			//Priority 4: ???
			//Priority 5: Profit!
			//(Realistically, as we evolve there will be a lot of options, such as defending borders from barbs, preparing attackers on other civs, defending
			//resources.  I expect we'll have some sort of arbiter that decides between competing priorities, with each being given a score as to how important
			//they are, including a weight by how far away the task is.  But this will evolve gradually over a long time)

			//As of today (4/7/2022), let's tackle just one of those - adequate defense of cities.  The AI is really good at losing cities to barbs right now,
			//and that's a problem.
			return new DefenderAI(DefenderAI.MakeAiDataForDefendAtRiskCity(unit, player, minDefenders: int.MaxValue));
		}

		private static UnitAI GetCombatAIIfUnitCanAttackNearbyBarbCamp(MapUnit unit, Player player) {
			if (unit.unitType.attack <= 0) {
				return null;
			}

			Tile closestBarbCamp = FindNearbyBarbCamp(unit, player, out TilePath path);
			if (closestBarbCamp != Tile.NONE) {
				CombatAIData caid = new CombatAIData();
				caid.destination = closestBarbCamp;
				caid.path = path;
				log.Information($"Set unit {unit} to take out barb camp at {closestBarbCamp}");
				return new CombatAI(caid);
			}
			return null;
		}

		private const int MAX_BARB_CAMP_DISTANCE = 3;

		// How far out of its way a unit will go to reach a nearby camp, as a
		// path cost (see AStarAlgorithm.PathFrom).
		private const double MAX_BARB_CAMP_PATH_COST = 2 * MAX_BARB_CAMP_DISTANCE;

		// Returns the closest known barbarian camp the unit can reach, if one
		// is within MAX_BARB_CAMP_DISTANCE tiles, or Tile.NONE. Equally close
		// camps are taken in the order GetTilesWithinTileSquare lists them.
		//
		// Camps the unit can't get to (e.g. across water) don't count, or the
		// unit would be sent towards the same unreachable camp every turn.
		private static Tile FindNearbyBarbCamp(MapUnit unit, Player player, out TilePath path) {
			path = null;
			Tile start = unit.location;

			// The tiles within a DistanceTo of n are exactly the tile square of
			// rank n, so only those need checking.
			List<Tile> camps = null;
			foreach (Tile t in start.GetTilesWithinTileSquare(MAX_BARB_CAMP_DISTANCE)) {
				if (t == Tile.NONE || !t.hasBarbarianCamp || !player.tileKnowledge.isTileKnown(t) || !unit.CanEnter(t)) {
					continue;
				}
				camps ??= new List<Tile>();
				camps.Add(t);
			}
			if (camps == null) {
				return Tile.NONE;
			}

			// Nearest first; OrderBy is stable, so ties keep their order.
			if (camps.Count > 1) {
				camps = camps.OrderBy(t => t.DistanceTo(start)).ToList();
			}

			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
			foreach (Tile camp in camps) {
				TilePath p = algorithm is AStarAlgorithm aStar
					? aStar.PathFrom(start, camp, unit, MAX_BARB_CAMP_PATH_COST)
					: algorithm.PathFrom(start, camp, unit);
				if (camp == start || (p != null && p.PathLength() > 0)) {
					path = p;
					return camp;
				}
			}
			return Tile.NONE;
		}

		// Whether GetAIForUnit would make this unit an explorer, without
		// planning anything or changing any state. The unit doesn't need to be
		// in play, so this can be asked about a unit we're considering
		// building.
		//
		// `hypothetical` says the unit isn't in play (it was made just to ask
		// this), which lets the answer be shared with other such questions
		// this turn (see HasTileToExploreCached).
		internal static bool WouldExplore(MapUnit unit, Player player, bool hypothetical = false) {
			// Mirror the checks GetAIForUnit makes before considering
			// exploration.
			if (unit.IsLeader() || unit.unitType.name == "Settler" || unit.unitType.name == "Worker") {
				return false;
			}
			if (unit.location.cityAtTile != null && unit.CanDefendOnLand() && unit.location.unitsOnTile.Count(u => u.CanDefendOnLand() && u != unit) == 0) {
				return false;
			}
			if (unit.unitType.attack > 0 && FindNearbyBarbCamp(unit, player, out _) != Tile.NONE) {
				return false;
			}
			if (unit.unitType.name == "Catapult") {
				return false;
			}
			if (IsInAnyWar(player, EngineStorage.gameData.players)) {
				return false;
			}
			// EscortAI.MaybeMakeAiData would have this unit escort an
			// unescorted settler.
			if (unit.CanDefendOnLand() && unit.location.unitsOnTile.Any(u => u.currentAI is SettlerAI settlerAi && settlerAi.data.escort == null)) {
				return false;
			}
			int maxExplorers = unit.IsLandUnit() ? MAX_LAND_EXPLORERS : MAX_WATER_EXPLORERS;
			if (CountExplorers(player, unit.IsLandUnit()) >= maxExplorers) {
				return false;
			}
			return hypothetical ? HasTileToExploreCached(unit, player) : HasTileToExplore(unit, player);
		}

		// Whether ExplorerAI.MaybeMakeAiData would find a tile for this unit
		// to explore: it takes the best scored candidate it can reach, so this
		// is whether any candidate it would score is reachable. Which one comes
		// first doesn't matter, so this skips the scoring and asks the pathing
		// algorithm once about all of them, nearest first.
		private static bool HasTileToExplore(MapUnit unit, Player player) {
			return HasTileToExplore(unit, player, ExcludedExplorationTargets(player, unit));
		}

		// Exploration targets of explorers that are gone or have other jobs
		// are forgotten before candidates are scored, so the candidates left
		// out are the targets of the other units that are still exploring.
		private static HashSet<Tile> ExcludedExplorationTargets(Player player, MapUnit unit) {
			TileKnowledge knowledge = player.tileKnowledge;
			if (knowledge.aiExplorationTargets.Count == 0) {
				return null;
			}
			HashSet<Tile> excluded = ExplorerAI.ActiveExplorationTargets(player, unit);
			excluded.IntersectWith(knowledge.aiExplorationTargets);
			return excluded;
		}

		private static bool HasTileToExplore(MapUnit unit, Player player, HashSet<Tile> excludedTargets) {
			TileKnowledge knowledge = player.tileKnowledge;

			bool isLandUnit = unit.IsLandUnit();
			List<Tile> candidates = new();
			foreach (Tile t in knowledge.borderTiles) {
				if (t.IsLand() != isLandUnit) {
					continue;
				}
				if (excludedTargets != null && excludedTargets.Contains(t)) {
					continue;
				}
				if (HasUnknownNeighboringTiles(knowledge, t)) {
					candidates.Add(t);
				}
			}
			if (candidates.Count == 0) {
				return false;
			}

			// Searching towards a near candidate first keeps the common case
			// (something nearby is reachable) quick.
			Tile start = unit.location;
			candidates.Sort((a, b) => start.DistanceTo(a).CompareTo(start.DistanceTo(b)));
			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
			return algorithm.FindFirstReachable(start, candidates, unit, out _) >= 0;
		}

		// Matches ExplorerAI's test for whether a tile is worth exploring (at
		// least one unknown tile among itself and its neighbors, and no
		// city). Border tiles are the unknown tiles next to known ones, so for
		// them this comes down to having no city; the neighbors only need
		// checking if a tile became known without going through TileKnowledge
		// (which tests do).
		private static bool HasUnknownNeighboringTiles(TileKnowledge knowledge, Tile t) {
			if (t.cityAtTile != null) {
				return false;
			}
			if (!knowledge.isTileKnown(t)) {
				return true;
			}
			foreach (Tile n in t.neighbors.Values) {
				if (n != Tile.NONE && !knowledge.isTileKnown(n)) {
					return true;
				}
			}
			return false;
		}

		// HasTileToExplore for units that aren't in play, such as the units a
		// city is considering building. A city asks this for every kind of
		// unit it could build, every time it picks something to build, and the
		// answer only depends on the unit through where it starts and which
		// tiles it can enter, so answers are shared until the turn or what the
		// player knows changes.
		//
		// What's known about the map, the player's cities (which ships can
		// sail through) and the exploration targets are checked for changes.
		// Other units moving during the turn aren't, which can only change
		// the answer if they block the way to everything left to explore.
		private sealed class ExplorationCheckCache {
			public int turn = int.MinValue;
			public int knownTileCount = -1;
			public int borderTileCount = -1;
			public int cityCount = -1;
			public HashSet<Tile> excludedTargets;
			public readonly Dictionary<ExplorationCheckKey, bool> results = new();
		}

		// Everything about a unit that decides which tiles it can enter, for
		// a unit that isn't in play (so isn't loaded, in an army or carrying
		// anything).
		private readonly record struct ExplorationCheckKey(Tile start, bool land, bool water, bool air, bool combat, bool loadable, bool amphibious);

		private static readonly ConditionalWeakTable<Player, ExplorationCheckCache> explorationChecks = new();

		private static bool HasTileToExploreCached(MapUnit unit, Player player) {
			TileKnowledge knowledge = player.tileKnowledge;
			HashSet<Tile> excluded = ExcludedExplorationTargets(player, unit);
			int turn = EngineStorage.gameData?.turn ?? 0;

			ExplorationCheckCache cache = explorationChecks.GetValue(player, _ => new ExplorationCheckCache());
			bool sameExcluded = (cache.excludedTargets == null || cache.excludedTargets.Count == 0)
				? (excluded == null || excluded.Count == 0)
				: excluded != null && cache.excludedTargets.SetEquals(excluded);
			if (cache.turn != turn || cache.knownTileCount != knowledge.knownTiles.Count
				|| cache.borderTileCount != knowledge.borderTiles.Count || cache.cityCount != player.cities.Count || !sameExcluded) {
				cache.results.Clear();
				cache.turn = turn;
				cache.knownTileCount = knowledge.knownTiles.Count;
				cache.borderTileCount = knowledge.borderTiles.Count;
				cache.cityCount = player.cities.Count;
				cache.excludedTargets = excluded;
			}

			ExplorationCheckKey key = new(unit.location, unit.IsLandUnit(), unit.IsWaterUnit(), unit.IsAirUnit(),
				unit.IsCombatUnit(), unit.IsLoadable(), unit.unitType.isAmphibious);
			if (!cache.results.TryGetValue(key, out bool result)) {
				result = HasTileToExplore(unit, player, excluded);
				cache.results[key] = result;
			}
			return result;
		}

		private static async Task AttemptTrading(Player us) {
			GameData gD = EngineStorage.gameData;

			log.Information($"{us} is checking for trading opportunities");
			foreach (Player them in EngineStorage.gameData.players) {
				// We can't trade with players we don't know or players we're at
				// war with.
				if (!us.playerRelationships.ContainsKey(them.id) || AtWar(us, them)) {
					continue;
				}

				// Barbarians can't trade.
				if (them.isBarbarians || us.isBarbarians) {
					continue;
				}

				// Figure out what techs are available for trading.
				List<Tech> techsTheyCanTrade = them.GetTechsTradableTo(us, gD.techs);
				List<Tech> techsWeCanTrade = us.GetTechsTradableTo(them, gD.techs);

				// If we can't trade techs there's no point in continuing - we
				// can't yet trade anything else interesting.
				if (techsWeCanTrade.Count == 0 && techsTheyCanTrade.Count == 0) {
					continue;
				}

				// Tech costs don't change until a deal is made, so look each
				// one up once. Every tech traded here is in one of the two
				// lists, and we need its cost for both players.
				Dictionary<Tech, int> costForUs = new();
				Dictionary<Tech, int> costForThem = new();
				foreach (Tech t in techsTheyCanTrade.Concat(techsWeCanTrade)) {
					costForUs[t] = gD.TechCostFor(t, us);
					costForThem[t] = gD.TechCostFor(t, them);
				}

				techsTheyCanTrade.Sort((a, b) => { return costForUs[b].CompareTo(costForUs[a]); });
				techsWeCanTrade.Sort((a, b) => { return costForThem[b].CompareTo(costForThem[a]); });

				// The value of each offer to each player, kept up to date as
				// techs are added or removed. This matches
				// TradeOffer.GoldEquivalentFor, without the gold.
				int weGiveTechsForThem = 0;
				int weGiveTechsForUs = 0;
				int weWantTechsForThem = 0;
				int weWantTechsForUs = 0;

				TradeOffer weGive = new();
				Func<int> CalculateWeGiveValue = () => {
					int gold = weGive.gold ?? 0;
					return Math.Max(gold + weGiveTechsForThem, gold + weGiveTechsForUs);
				};
				TradeOffer weWant = new();
				Func<int> CalculateWeWantValue = () => {
					int gold = weWant.gold ?? 0;
					return Math.Min(gold + weWantTechsForThem, gold + weWantTechsForUs);
				};

				// Figure out the value of what we have available to trade.
				weGive.gold = us.gold;
				foreach (Tech t in techsWeCanTrade) {
					weGive.techs.Add(t);
					weGiveTechsForThem += costForThem[t];
					weGiveTechsForUs += costForUs[t];
				}
				int ourMaxPossibleOffer = CalculateWeGiveValue();

				// Going from the most to the least valuable valuable techs, see
				// if we can afford them. This greedy algorithm should be good
				// enough - we don't need perfect binpacking.
				foreach (Tech t in techsTheyCanTrade) {
					int cost = costForUs[t];
					if (cost < ourMaxPossibleOffer) {
						weWant.techs.Add(t);
						weWantTechsForThem += costForThem[t];
						weWantTechsForUs += costForUs[t];
						ourMaxPossibleOffer -= cost;
					}
				}

				// Also ask for any gold we can get.
				weWant.gold = Math.Min(ourMaxPossibleOffer, them.gold);

				// At this point we are getting as much as we possibly can get
				// from the opponent. However, we might be overpaying, possibly
				// by a significant amount. Keep removing techs from our offer
				// as long as it doesn't make our offer worse than theirs.
				int theirOfferValue = CalculateWeWantValue();
				for (int i = 0; i < weGive.techs.Count;) {
					Tech t = weGive.techs[i];
					if (CalculateWeGiveValue() - costForThem[t] >= theirOfferValue) {
						weGive.techs.RemoveAt(i);
						weGiveTechsForThem -= costForThem[t];
						weGiveTechsForUs -= costForUs[t];
					} else {
						++i;
					}
				}

				// Now use any gold to even things out, if possible.
				int remainingDelta = Math.Max(0, CalculateWeGiveValue() - theirOfferValue);
				weGive.gold -= Math.Min(remainingDelta, weGive.gold.Value);

				// And ensure we minimize the total gold traded, to keep the
				// logs cleaner.
				int redundantGold = Math.Min(weGive.gold.Value, weWant.gold.Value);
				weGive.gold -= redundantGold;
				weWant.gold -= redundantGold;
				if (weGive.gold == 0) {
					weGive.gold = null;
				}
				if (weWant.gold == 0) {
					weWant.gold = null;
				}

				// Finally if the deal is too mismatched or only contains a swap
				// of gold, abandon it. Otherwise we can execute the deal.

				float tradeFactor = them.isHuman ? 1.0f : gD.gameDifficulty.AIToAITradeRate / 100.0f;
				if (CalculateWeGiveValue() > tradeFactor * CalculateWeWantValue()) {
					continue;
				}
				if (weGive.techs.Count == 0 && weWant.techs.Count == 0) {
					continue;
				}

				if (them.isHuman) {
					// A LAN guest who has left can't answer, so isn't asked.
					if (!EngineStorage.IsPlayerReachable(them.id)) {
						continue;
					}
					// The human receiving the offer takes the UI to respond.
					// In a hotseat game they may not be the player at the screen.
					if (EngineStorage.uiFollowsActivePlayer) {
						EngineStorage.uiControllerID = them.id;
					}
					EngineStorage.diplomacyPlayerID = them.id;
					new MsgShowTradeOffer(us, them, weWant, weGive).send();
					await EngineStorage.WaitForDiplomacyCompleted(them.id);
					EngineStorage.diplomacyPlayerID = null;
				} else if (them.WouldAcceptDealFrom(gD, us, weGive, weWant)) {
					us.ExecuteDeal(gD, them, weWant, weGive);
				}
			}
		}

		private static void AdjustSliders(Player player) {
			const int MAX_SLIDER_VALUE = 10;
			const int MAX_AI_LUXURY_SLIDER = 5;

			// Start by zeroing out the sliders.
			player.luxuryRate = 0;
			player.scienceRate = 0;
			player.taxRate = 0;

			// Increase the luxury slider until only a small handful of cities
			// are unhappy (sometimes making all cities happy with the luxury
			// slider is too expensive and it's easier to just use entertainers
			// there).
			while (MostCitiesUnhappy(player) && player.luxuryRate < Math.Min(MAX_AI_LUXURY_SLIDER, player.maxLuxuryRate)) {
				++player.luxuryRate;
			}

			// Fix up any remaining unhappy cities.
			FixRemainingUnhappyCities(player);

			// Now max out the science slider and then decrease it (increasing
			// the tax rate) until we're not losing money.
			player.scienceRate = Math.Min(player.maxScienceRate, MAX_SLIDER_VALUE - player.luxuryRate);
			player.taxRate = MAX_SLIDER_VALUE - player.luxuryRate - player.scienceRate;
			player.ApplyGovernmentRateCap();
			int goldPerTurn;
			if (player.scienceRate > 0 && player.taxRate < player.maxRate
				&& !IsTolerable(player, goldPerTurn = player.CalculateGoldPerTurn())) {
				// Only the cities' commerce changes as the sliders move; see
				// CityTaxesAndWealth.
				int fixedGoldPerTurn = goldPerTurn - CityTaxesAndWealth(player);
				do {
					player.scienceRate--;
					player.taxRate++;
				} while (player.scienceRate > 0 && player.taxRate < player.maxRate
					&& !IsTolerable(player, fixedGoldPerTurn + CityTaxesAndWealth(player)));
			}

			log.Information($"{player} slider values: Science: {player.scienceRate}, Luxury: {player.luxuryRate}, Tax: {player.taxRate}");
		}

		// Returns true if more than 10% of the player's cities are unhappy with
		// current settings.
		private static bool MostCitiesUnhappy(Player player) {
			int unhappyCities = 0;
			foreach (City city in player.cities) {
				City.Mood cityMood = city.RecalculateCitizenMoods(EngineStorage.gameData);
				if (cityMood == City.Mood.Unhappy) {
					++unhappyCities;
				}
			}
			return unhappyCities / (double)player.cities.Count > .1;
		}

		// In each city, reassign citizens, managing moods, to ensure that we
		// don't have any cities that will riot.
		private static void FixRemainingUnhappyCities(Player player) {
			GameData gameData = EngineStorage.gameData;
			CitizenType defaultCitizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
			foreach (City city in player.cities) {
				// TODO: This throws away existing nationalities, fix that.
				int numResidents = city.residents.Count;
				city.RemoveAllCitizens();

				// Nothing the assignments depend on changes while this city's
				// citizens are reassigned, apart from which tiles are worked,
				// so the tile yields can be shared between them.
				CityTileAssignmentAI.AssignmentContext context = new(gameData, city);
				for (int i = 0; i < numResidents; ++i) {
					CityResident newResident = new() {
						citizenType = defaultCitizenType,
						nationality = city.owner.civilization,
						city = city
					};
					city.AddCitizen(newResident);
					CityTileAssignmentAI.AssignNewCitizenToTile(gameData, newResident, manageMoods: true, context);
				}
			}
		}

		public static bool BudgetIsTolerable(Player player) {
			return IsTolerable(player, player.CalculateGoldPerTurn());
		}

		private static bool IsTolerable(Player player, int gpt) {
			var tolerableDeficit = gpt > player.gold * -0.1;
			return gpt > 0 || tolerableDeficit;
		}

		// The part of the player's gold per turn that the science and tax
		// sliders affect.
		//
		// Gold per turn (Player.AggregateFlows().Netflows()) is the sum of
		// each city's taxes and wealth from City.CurrentCommerceYield(), plus
		// terms the sliders don't affect: gold-per-turn deals, interest
		// (which depends on the treasury), building maintenance and unit
		// support. The corrupted, beaker and happiness amounts cancel out,
		// and the tax collector split only moves taxes between two terms of
		// the same sum. So once the rest is known, only the cities' commerce
		// needs recomputing as the sliders move.
		internal static int CityTaxesAndWealth(Player player) {
			int result = 0;
			foreach (City city in player.cities) {
				CommerceBreakdown commerce = city.CurrentCommerceYield();
				result += commerce.taxes + commerce.wealth;
			}
			return result;
		}
	}
}

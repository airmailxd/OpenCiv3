using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7Engine.AI.UnitAI;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.AIData;
using Serilog;

namespace C7Engine.AI {
	// Kings & Nomads: the AI's workers join its cities to the independent
	// city-states by road, which makes them trade partners (see
	// CityStateTrade), and keep out of harm's way outside our borders
	// (owner's design, 2026-10-23: "teach the AI's workers to set out and
	// build roads connecting their cities to nearby city-states", and "a
	// worker working on, or travelling through, a tile outside its own
	// civ's territory should have a military escort with it").
	//
	// Each city-state not yet joined, near one of our cities, is weighed by
	// what it would bring (its gold a turn and the luxuries and strategic
	// resources in its borders that we lack) against the road-less tiles on
	// the cheapest road to it, which makes use of the roads already there.
	// Workers then road those tiles one at a time, as they do the roads
	// joining our own cities (see WorkerAI.MakeAiData).
	//
	// In this scenario a worker job that takes the worker outside our land
	// (the city-state roads, but also colonies, outposts and paths that cut
	// across unclaimed land) needs an escort: a spare soldier nearby, which
	// stacks with the worker and moves with it (EscortAI, as for settlers).
	// With none to hand the worker stays home, keeping to paths within our
	// borders. Barbarians walking onto a lone worker destroy it, and rivals
	// at war capture it.
	//
	// The city-states' garrisons stay in their cities and never attack (see
	// KingsAndNomadsAI.HoldsCityState); in AI trials none did, so an
	// escorted worker may road the tiles next to a city-state, which it
	// must, as a road only joins a city-state that reaches its city tile.
	// Other enemies within reach still keep it away.
	//
	// In AI trials (AiTrialTest, 20 games of 6 civs and 120 turns on a 70x70
	// map, every civ AI), the worker-turns a civ's workers ended outside its
	// borders with no defender fell from 42 to 9 on Continents and from 49
	// to 12 on Pangaea, with no significant change in cities or score. The
	// trade itself stayed rare, a few gold a civ over the game: few
	// city-states are near a civ's cities while it has a soldier to spare,
	// and the hordes take most of them by halfway through. Nearly all the
	// workers lost (about two a civ) were taken inside our borders by
	// rivals at war.
	//
	// Everything here is gated on the scenario (IsOn), so other games play
	// as before.
	public static class CityStateRoadAI {
		private static readonly ILogger log = Log.ForContext(typeof(CityStateRoadAI));

		public static bool IsOn(Player player) {
			return KingsAndNomadsAI.PlaysFor(player) && CityStateTrade.IsOn(EngineStorage.gameData);
		}

		// HEURISTIC: how far (in tiles) from the nearest of our cities a
		// city-state may be for our workers to road to it, and the most
		// road-less tiles the road may need (owner: "don't over-invest: a
		// few road tiles per connection").
		internal const int MaxDistance = 8;
		internal const int MaxRoadTiles = 6;

		// HEURISTIC: how many city-states our workers road towards at once,
		// so that one road is finished before the next is begun.
		internal const int MaxTargets = 2;

		// HEURISTIC: what a step onto a tile without a road adds to the cost
		// of the road's path (a step on open ground costs one), so that it
		// keeps to roads already built; and a step onto another civ's land,
		// as a road through a neighbour can be cut.
		private const float RoadlessStepCost = 1;
		private const float ForeignStepCost = 4;

		// HEURISTIC: what a city-state is worth, in gold a turn: its trade
		// income, and for each kind of luxury or strategic resource in its
		// borders that we lack, as much again or a little less; all weighed
		// by its citizens, plus its specialists' gold and beakers.
		private const double LuxuryValue = 3;
		private const double StrategicValue = 2;

		// Whether the tile is outside our land: not in our borders, and not
		// one of our cities.
		public static bool IsOutside(Player player, Tile t) {
			return t.OwningPlayer() != player && t.cityAtTile?.owner != player;
		}

		// Whether a city-state's garrison stands on the tile.
		private static bool IsCityStateTile(Tile t) {
			return t.cityAtTile is City c && CityStateTrade.IsCityState(EngineStorage.gameData, c);
		}

		// Whether an escorted worker may go to the tile: it isn't the land of
		// a civ we're at war with, and no enemy we can see other than a
		// city-state's garrison could reach it next turn.
		public static bool SafeWithEscort(AIThreat threat, Player player, Tile t) {
			return !AIThreat.IsHostileTerritory(player, t) && !threat.EnemyCanReach(t, IsCityStateTile);
		}

		// The road-less tiles on the roads that would join the city-states
		// most worth joining to our cities, from our cities outwards.
		internal static List<Tile> ComputeRoadTiles(Player player, MapUnit unit) {
			List<Tile> result = new();
			GameData gameData = EngineStorage.gameData;
			if (!IsOn(player) || gameData == null || !unit.IsLandUnit() || player.cities.Count == 0) {
				return result;
			}
			TradeNetwork network = gameData.GetTradeNetwork();
			HashSet<City> connected = network.ConnectedCityStates(player);
			City home = player.cities.Find(c => c.IsCapital()) ?? player.cities[0];
			Dictionary<Resource, int> have = network.GetResourcesAvailableToCity(player, home);
			int rank = gameData.rules?.MaxRankOfWorkableTiles ?? 2;
			AStarAlgorithm algorithm = new(unit) {
				StepPenalty = t => (t.IsRoaded() || t.HasCity() ? 0 : RoadlessStepCost)
					+ (t.OwningPlayer() is Player owner && owner != player && !owner.isBarbarians ? ForeignStepCost : 0),
			};
			double maxCost = 3 * MaxDistance * (1 + RoadlessStepCost);

			List<(double score, City cityState, City from, List<Tile> tiles)> options = new();
			foreach (City cityState in gameData.cities) {
				if (!CityStateTrade.IsCityState(gameData, cityState) || connected.Contains(cityState)
						|| !player.tileKnowledge.isTileKnown(cityState.location)) {
					continue;
				}
				City from = player.cities.Where(c => c.location.continent == cityState.location.continent)
					.MinBy(c => c.location.DistanceTo(cityState.location));
				if (from == null || from.location.DistanceTo(cityState.location) > MaxDistance) {
					continue;
				}
				// From the city-state, which a worker can't enter, to our city.
				TilePath path = algorithm.PathFrom(cityState.location, from.location, unit, maxCost);
				if (path == null || path.PathLength() <= 0) {
					continue;
				}
				List<Tile> roadless = new();
				bool blocked = false;
				foreach (Tile t in path.path) {
					if (!t.IsLand() || t.HasRoad() || t.HasCity()) {
						continue;
					}
					// A road on another civ's land could be cut, and one in
					// the land of a civ we're at war with can't be built.
					if ((t.OwningPlayer() is Player owner && owner != player && !owner.isBarbarians) || AIThreat.IsHostileTerritory(player, t)) {
						blocked = true;
						break;
					}
					roadless.Add(t);
				}
				if (blocked || roadless.Count == 0 || roadless.Count > MaxRoadTiles) {
					continue;
				}
				HashSet<Resource> lacking = new();
				foreach (Tile t in cityState.location.GetTilesWithinRankDistance(rank)) {
					if (t != Tile.NONE && t.owningCity == cityState && t.Resource != Resource.NONE
							&& (t.Resource.Category == ResourceCategory.LUXURY || t.Resource.Category == ResourceCategory.STRATEGIC)
							&& player.KnowsAboutResource(t.Resource) && !have.ContainsKey(t.Resource)) {
						lacking.Add(t.Resource);
					}
				}
				// The shields come to each of our cities on the road.
				double value = CityStateTrade.IncomePerCityState
					+ CityStateTrade.ShieldsPerCityState * (network.CityGroups(player).Find(g => g.Contains(from))?.Count ?? 1)
					+ lacking.Sum(r => r.Category == ResourceCategory.LUXURY ? LuxuryValue : StrategicValue);
				// A bigger city-state is a better partner: its specialists'
				// gold and beakers, and the more it will have as it grows
				// (owner's ruling, 2026-10-25: weigh it by its citizens).
				value = value * cityState.residents.Count / GameSetup.CityStateSize
					+ CityStates.Specialists(cityState) * (CityStates.SpecialistGold + CityStates.SpecialistBeakers);
				// Our city is the end of the path: the tiles nearest it first.
				roadless.Reverse();
				options.Add((value / roadless.Count, cityState, from, roadless));
			}

			HashSet<Tile> seen = new();
			HashSet<City> origins = new();
			roadOrigins.AddOrUpdate(player, origins);
			roadTiles.AddOrUpdate(player, seen);
			foreach ((double score, City cityState, City from, List<Tile> tiles) in options.OrderByDescending(o => o.score).ThenBy(o => o.cityState.id.ToString())
					.Take(MaxTargets)) {
				log.Debug("{Player} wants a road to {CityState} ({Tiles} tiles, worth {Score:0.0} a tile)", player, cityState, tiles.Count, score);
				origins.Add(from);
				foreach (Tile t in tiles) {
					if (seen.Add(t)) {
						result.Add(t);
					}
				}
			}
			return result;
		}

		// The cities the roads wanted when they were last worked out start
		// from (see ComputeRoadTiles), near which a worker may find an
		// escort (see EscortAvailable).
		private static readonly ConditionalWeakTable<Player, HashSet<City>> roadOrigins = new();

		// The road-less tiles of those roads, likewise.
		private static readonly ConditionalWeakTable<Player, HashSet<Tile>> roadTiles = new();

		// Whether the tile is one of them and still has no road.
		public static bool IsWantedRoad(Player player, Tile t) {
			return !t.HasRoad() && !t.HasCity() && roadTiles.TryGetValue(player, out HashSet<Tile> tiles) && tiles.Contains(t);
		}

		// HEURISTIC: how far, in tiles, a worker looks for a spare soldier to
		// escort it, as a settler does (see SettlerAI.TryToTakeEscort).
		private const int EscortSearchDistance = 3;

		// The unit escorting the worker, if any.
		public static MapUnit EscortOf(MapUnit worker, Player player) {
			foreach (MapUnit u in player.units) {
				if (u.currentAI is EscortAI e && e.data?.unitToEscort == worker) {
					return u;
				}
			}
			return null;
		}

		// The nearest spare soldier on the worker's landmass within
		// EscortSearchDistance tiles of the worker, or of one of the tiles
		// `near` (where the worker will need it), which walks to the worker:
		// one that is exploring, defending or idle, but not one of the
		// King's guard, and not one its city needs to keep (see
		// KingsAndNomadsAI.GarrisonWanted). Hordes' units aren't spare.
		public static MapUnit FindSpareEscort(MapUnit worker, Player player, IReadOnlyList<Tile> near) {
			MapUnit best = null;
			int bestDistance = int.MaxValue;
			AIThreat threat = null;
			foreach (MapUnit u in player.units) {
				if (u == worker || u.hitPointsRemaining <= 0 || u.IsLoaded() || u.IsLeader() || u.IsArmy() || u.unitType.isKing
						|| !u.CanDefendOnLand() || !DevelopmentAI.IsMilitaryLandUnit(u.unitType)
						|| !(u.currentAI == null || u.currentAI is DefenderAI || u.currentAI is ExplorerAI)
						|| u.location.continent != worker.location.continent) {
					continue;
				}
				int distance = u.location.DistanceTo(worker.location);
				foreach (Tile t in near) {
					distance = Math.Min(distance, u.location.DistanceTo(t));
				}
				if (distance > EscortSearchDistance || distance >= bestDistance || KingsAndNomadsAI.GuardsTheKing(u, player)) {
					continue;
				}
				City city = u.location.cityAtTile;
				if (city != null && city.owner == player) {
					threat ??= AIThreat.Assess(player);
					int defenders = city.location.unitsOnTile.Count(o => o.owner == player && o.CanDefendOnLand() && !o.unitType.isKing
						&& !o.unitType.isWorker && !o.unitType.isSettler && o.currentAI is not EscortAI && o.currentAI is not WarCampaign.MemberAI);
					if (defenders <= KingsAndNomadsAI.GarrisonWanted(player, city, threat)) {
						continue;
					}
				}
				best = u;
				bestDistance = distance;
			}
			return best;
		}

		// Whether the worker has an escort, or a spare soldier near it or near
		// a city a road to a city-state starts from could become one.
		public static bool EscortAvailable(MapUnit worker, Player player) {
			List<Tile> near = roadOrigins.TryGetValue(player, out HashSet<City> origins) ? origins.Select(c => c.location).ToList() : new();
			return EscortOf(worker, player) != null || FindSpareEscort(worker, player, near) != null;
		}

		// Gives the plan the worker's escort, or a spare soldier as a new
		// one. Returns false if there is none.
		public static bool TakeEscort(MapUnit worker, Player player, WorkerAIData plan) {
			MapUnit escort = EscortOf(worker, player);
			if (escort == null) {
				// Near the worker, or where it leaves our land.
				List<Tile> near = new();
				if (plan.pathToDestination?.path?.FirstOrDefault(t => IsOutside(player, t)) is Tile exit) {
					near.Add(exit);
				}
				escort = FindSpareEscort(worker, player, near);
				if (escort == null) {
					return false;
				}
				escort.Wake();
				escort.currentAI = new EscortAI(new EscortAIData { unitToEscort = worker });
				log.Information("Worker {Worker} takes {Escort} as its escort", worker.id, escort);
			}
			plan.needsEscort = true;
			plan.escort = escort;
			return true;
		}

		// Lets the worker's escort go, if it has one.
		public static void ReleaseEscort(MapUnit worker, Player player) {
			MapUnit escort = EscortOf(worker, player);
			if (escort?.currentAI is EscortAI e) {
				e.data.unitToEscort = null;
			}
		}

		// HEURISTIC: what a step outside our land adds to the path of a
		// worker with no escort, so that it goes round within our borders
		// where it can.
		private const float OutsideStepCost = 10;

		// The tiles of our land the worker can reach without leaving it (and
		// the worker's own).
		public static HashSet<Tile> OurLandAround(MapUnit worker, Player player) {
			HashSet<Tile> result = new() { worker.location };
			Queue<Tile> queue = new();
			queue.Enqueue(worker.location);
			while (queue.Count > 0) {
				foreach (Tile n in queue.Dequeue().neighbors.Values) {
					if (n != Tile.NONE && n.IsLand() && !n.IsImpassable() && !IsOutside(player, n) && result.Add(n)) {
						queue.Enqueue(n);
					}
				}
			}
			return result;
		}

		// Whether the plan takes the worker outside our land: to its job, or
		// on the way there.
		public static bool LeavesOurLand(Player player, WorkerAIData plan) {
			if (IsOutside(player, plan.destination)) {
				return true;
			}
			return plan.pathToDestination?.path?.Any(t => IsOutside(player, t)) == true;
		}

		// For a job within our land: finds a way there that stays in our
		// borders, and gives it to the plan. Returns false if there is none.
		public static bool KeepWithinOurLand(MapUnit worker, Player player, WorkerAIData plan) {
			if (IsOutside(player, plan.destination)) {
				return false;
			}
			AStarAlgorithm algorithm = new(worker) { OutsideTerritoryCost = OutsideStepCost };
			TilePath path = algorithm.PathFrom(worker.location, plan.destination, worker);
			if (path == null || path.PathLength() <= 0 || path.path.Any(t => IsOutside(player, t))) {
				return false;
			}
			plan.pathToDestination = path;
			return true;
		}
	}
}

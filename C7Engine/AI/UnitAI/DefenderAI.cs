using C7GameData;
using C7GameData.AIData;
using C7Engine.Pathing;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Runtime.CompilerServices;
using Serilog;

namespace C7Engine.AI.UnitAI {
	class DefenderAI : C7GameData.UnitAI {
		private static ILogger log = Log.ForContext<DefenderAI>();
		public DefenderAIData data;

		public static DefenderAIData MakeAiDataForDefendInPlace(MapUnit unit, Player player) {
			DefenderAIData ai = new DefenderAIData();
			ai.goal = DefenderAIData.DefenderGoal.DEFEND_CITY;
			ai.destination = unit.location;
			ai.defender = unit;
			log.Debug("Set defender AI for {Unit} with destination of {Destination}", unit, ai.destination);
			return ai;
		}

		public static DefenderAIData? MakeAiDataForDefendAtRiskCity(MapUnit unit, Player player, int minDefenders) {
			if (!unit.CanDefendOnLand()) {
				// Just fortify in place if we can't defend on land.
				return MakeAiDataForDefendInPlace(unit, player);
			}

			City cityToDefend = FindAtRiskCityToDefend(unit, player, minDefenders);
			if (cityToDefend == null) {
				// With minDefenders at int.MaxValue the caller wants a plan
				// whatever happens; with no city to go to (such as a civ that
				// has lost them all), the unit defends where it is.
				return minDefenders == int.MaxValue ? MakeAiDataForDefendInPlace(unit, player) : null;
			}

			DefenderAIData ai = new DefenderAIData();
			ai.destination = cityToDefend.location;
			ai.goal = DefenderAIData.DefenderGoal.DEFEND_CITY;
			ai.defender = unit;

			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
			ai.pathToDestination = algorithm.PathFrom(unit.location, ai.destination, unit);

			log.Debug("Unit {Unit} tasked with defending {City}", unit, cityToDefend.name);
			return ai;
		}

		public DefenderAI(DefenderAIData d) {
			data = d;
			if (d?.defender != null) {
				Register(d.defender.owner, this);
			}
		}

		// Every DefenderAI created for a player's units, so that we can count
		// the units heading to each city without scanning all units.
		private static readonly UnitAiRegistry<DefenderAI> registry = new(ai => ai.data?.defender);

		private static void Register(Player player, DefenderAI ai) {
			registry.Register(player, ai);
			// Once assigned, the new plan counts as a unit en route to its
			// city, which the snapshot doesn't know about.
			lastSnapshots.Remove(player);
		}

		private static int CurrentTurn() {
			return EngineStorage.gameData?.turn ?? 0;
		}

		// Calls action(destination) for every unit of `player` whose current
		// AI is a DefenderAI.
		private static void ForEachActiveDefenderDestination(Player player, Action<Tile> action) {
			// Leaders heading for a city aren't defending it.
			registry.ForEachActive(player, ai => {
				if (!ai.data.defender.IsLeader()) {
					action(ai.data.destination);
				}
			});
		}

		C7GameData.UnitAI.MoveResult C7GameData.UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			if (data.destination == unit.location) {
				if (!unit.isFortified) {
					unit.Fortify();
					log.Debug("Fortifying {Unit} at {Destination}", unit, data.destination);
				}
				return C7GameData.UnitAI.Result.Done;
			} else {
				log.Debug("Moving defender towards " + data.destination);
				return this.TryToMoveAlongPath(unit, ref data.pathToDestination);
			}
		}

		public string SummarizePlan() {
			return "DefenderAI: " + data.ToString();
		}

		public void UpdateOnDeath() { }

		// The per-city inputs of FindAtRiskCityToDefend, which PlayerAI asks
		// for repeatedly (with different minDefenders) while choosing a plan
		// for a unit. They're reused as long as the unit and its owner's units
		// and cities are where they were.
		internal sealed class CityDefenseSnapshot {
			public MapUnit unit;
			public C7GameData.UnitAI unitAI;
			public Player player;
			public int turn;
			public Tile unitLocation;
			public int unitCount;
			public int cityCount;
			public City[] cities;
			public int[] defenders;
			public int[] enRoute;
			public int[] distance;
		}

		// The last snapshot taken for each player. Weak, so that it doesn't
		// keep a finished game alive.
		private static readonly ConditionalWeakTable<Player, CityDefenseSnapshot> lastSnapshots = new();

		internal static CityDefenseSnapshot GetSnapshot(MapUnit unit, Player player) {
			int turn = CurrentTurn();
			lastSnapshots.TryGetValue(player, out CityDefenseSnapshot snap);
			// The unit's own current plan counts towards the units en route,
			// so a snapshot taken under another plan is stale.
			if (snap != null && snap.unit == unit && snap.unitAI == unit.currentAI && snap.player == player && snap.turn == turn
				&& snap.unitLocation == unit.location && snap.unitCount == player.units.Count
				&& snap.cityCount == player.cities.Count && SameCities(snap.cities, player.cities)) {
				return snap;
			}

			int n = player.cities.Count;
			snap = new CityDefenseSnapshot() {
				unit = unit,
				unitAI = unit.currentAI,
				player = player,
				turn = turn,
				unitLocation = unit.location,
				unitCount = player.units.Count,
				cityCount = n,
				cities = player.cities.ToArray(),
				defenders = new int[n],
				enRoute = new int[n],
				distance = new int[n],
			};

			Dictionary<Tile, int> cityIndex = new(n, ReferenceEqualityComparer.Instance);
			for (int i = 0; i < n; ++i) {
				City c = snap.cities[i];
				int numDefenders = 0;
				foreach (MapUnit u in c.location.unitsOnTile) {
					if (u.CanDefendOnLand()) {
						++numDefenders;
					}
				}
				snap.defenders[i] = numDefenders;
				snap.distance[i] = c.location.DistanceTo(unit.location);
				cityIndex.TryAdd(c.location, i);
			}

			// Count the units already on the way to each city.
			ForEachActiveDefenderDestination(player, destination => {
				// The city may have been captured since the unit set out.
				City city = destination?.cityAtTile;
				if (city != null && cityIndex.TryGetValue(destination, out int i) && snap.cities[i] == city) {
					++snap.enRoute[i];
				}
			});

			lastSnapshots.AddOrUpdate(player, snap);
			return snap;
		}

		private static bool SameCities(City[] cached, List<City> current) {
			for (int i = 0; i < cached.Length; ++i) {
				if (cached[i] != current[i]) {
					return false;
				}
			}
			return true;
		}

		/**
		 * Finds a nearby city that could use extra defenders.
		 *
		 * This is not a brilliant method, with many flaws such as whether the
		 * city needs more defenders, or if the units present are defenders.
		 * Returns null if there is none, as when the player has no cities.
		 */
		private static City FindAtRiskCityToDefend(MapUnit unit, Player player, int minDefenders) {
			if (player.cities.Count == 0) {
				return null;
			}

			CityDefenseSnapshot snap = GetSnapshot(unit, player);

			// Assign a score to each city, where the highest score is the city
			// we want to send our unit to.
			float bestScore = (float)int.MinValue;
			City bestCity = null;
			for (int i = 0; i < snap.cities.Length; ++i) {
				float score = 0;
				if (snap.defenders[i] < minDefenders) {
					// Add to the score if there aren't many defenders, with a
					// larger score for less defended cities.
					score += 10f;
				}

				// Penalize cities for being far away.
				score -= snap.distance[i];

				// Make the city less important if there are already units on
				// the way.
				score -= 3f * snap.enRoute[i];

				if (score > bestScore) {
					bestScore = score;
					bestCity = snap.cities[i];
				}
			}

			// If there are no cities in need of defending at this defense level,
			// don't return a city. This will let us decide to use units for
			// offense instead. We can always call this method with a larger
			// min defenders value if we really want to use defenders.
			if (bestScore <= 0 && minDefenders != int.MaxValue) {
				return null;
			}

			return bestCity;
		}
	}

	// Tracks the unit AIs of a given type created for each player's units, so
	// that "which of my units currently have this kind of plan" can be
	// answered without scanning every unit.
	//
	// An AI counts as active while it is its unit's currentAI. AIs are
	// created right before being assigned, so ones that were never assigned
	// are only dropped once they are from a previous turn.
	internal sealed class UnitAiRegistry<TAi> where TAi : class, C7GameData.UnitAI {
		private sealed class Registration {
			public TAi ai;
			public int createdTurn;
			public bool seenAssigned;
		}

		private sealed class Entries {
			public readonly List<Registration> list = new();
			public int pruneAt = 64;
		}

		private readonly Func<TAi, MapUnit> unitOf;
		private readonly ConditionalWeakTable<Player, Entries> byPlayer = new();

		public UnitAiRegistry(Func<TAi, MapUnit> unitOf) {
			this.unitOf = unitOf;
		}

		private static int CurrentTurn() {
			return EngineStorage.gameData?.turn ?? 0;
		}

		public void Register(Player player, TAi ai) {
			if (player == null) {
				return;
			}
			Entries entries = byPlayer.GetOrCreateValue(player);
			entries.list.Add(new Registration() { ai = ai, createdTurn = CurrentTurn() });

			// Keep the list from growing without bound for players whose
			// registry is rarely scanned.
			if (entries.list.Count >= entries.pruneAt) {
				ForEachActive(player, null);
				entries.pruneAt = Math.Max(64, entries.list.Count * 2);
			}
		}

		// Calls action for every active AI of the player, dropping AIs that
		// are no longer in use.
		public void ForEachActive(Player player, Action<TAi> action) {
			if (!byPlayer.TryGetValue(player, out Entries entries)) {
				return;
			}
			int turn = CurrentTurn();
			entries.list.RemoveAll(r => {
				MapUnit unit = unitOf(r.ai);
				if (unit == null) {
					return true;
				}
				bool assigned = unit.currentAI == r.ai && unit.owner == player;
				if (!assigned) {
					return r.seenAssigned || r.createdTurn != turn;
				}
				r.seenAssigned = true;
				action?.Invoke(r.ai);
				return false;
			});
		}
	}
}

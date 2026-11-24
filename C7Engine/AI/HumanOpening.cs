using System;
using System.Linq;
using C7GameData;

namespace C7Engine.AI {
	// Switch "human-opening": Advanced AI only, on by default with fast-expand
	// (paired trials, Emperor, 16 games, 130 turns, Pangaea and Continents
	// maps: score +73 to +160, cities at turn 100 +1.0 to +1.4). Built
	// from the owner's "Celts Wheat" play log (doc/human-play-own-saves.md
	// style review, 2026-11-09): on the same map and start the owner had 4
	// cities at turn 36 and 22 at turn 96 where Advanced had 3 and 13. He
	//  - chose what a city builds by its shield output, so that a Settler
	//    comes out about when the city has grown to give the citizens, and
	//    a slow city builds a Worker or a Warrior instead of a late Settler;
	//  - kept one cheap Warrior per city early, not Spearmen or Archers;
	//  - sent his first Warriors scouting (180 land tiles known at turn
	//    24, Advanced 58) and so saw 14 city sites to Advanced's 5.
	// While the civ is expanding at peace with no enemy near the city:
	//  - a Settler is judged by the turns its shields need (T_build) against
	//    the turns until the city has the size for it (T_grow): when it
	//    would be done no more than SlackTurns before the city is big enough
	//    it may be started early and gains SettlerBonus; when its shields
	//    would be ready more than SlackTurns early, the city builds a
	//    Worker first (WorkerBonus); when the shields would need more than
	//    SlackTurns past the growth, the Settler loses LateSettlerPenalty
	//    and a Worker gains WorkerBonus;
	//  - while the civ has fewer than Cities cities, a city that has a
	//    defender builds no more soldiers, and the one it builds is the
	//    cheapest (each shield above it loses CostPenalty); the Stock
	//    garrison and the settler-escort rule are off;
	//  - the first Scouts soldiers explore instead of waiting to escort.
	// All numbers are guesses (UNVERIFIED), to be searched in trials.
	public static class HumanOpening {
		public const string Switch = "human-opening";
		public const string SlackKnob = "OpeningSlack";
		public const string SettlerBonusKnob = "OpeningSettlerBonus";
		public const string WorkerBonusKnob = "OpeningWorkerBonus";
		public const string LatePenaltyKnob = "OpeningLateSettlerPenalty";
		public const string CitiesKnob = "OpeningCities";
		public const string ScoutsKnob = "OpeningScouts";
		public const float CostPenalty = 3;
		public const float ExtraSoldierPenalty = 40;

		public static bool On(Player player) => player != null && player.IsAdvancedAi && AiExperiments.Has(player, Switch);

		private static double Knob(Player player, string name) => Advanced.AdvancedTuning.Get(player, name);

		// Expanding at peace, no enemy near.
		internal static bool Active(ChooseProducible.ProducibleStats stats, City city, Player player) {
			return On(player) && !stats.atWar && stats.InExpansionPhase && !LeanGarrison.EnemyNear(player, city);
		}

		// Whether the garrison rules are loosened for the player's size.
		internal static bool Loose(Player player) => On(player) && player.cities.Count < Knob(player, CitiesKnob);

		// The turns the city needs to have the citizens for the settler,
		// and to build it; (int.MaxValue if never).
		private static (int grow, int build) Times(ChooseProducible.ProducibleStats stats, City city, UnitPrototype settler) {
			int growth = stats.FoodGrowthPerTurn;
			int grow = city.residents.Count > settler.populationCost ? 0
				: growth <= 0 ? int.MaxValue
				: FastExpand.TurnsToSize(city, settler.populationCost + 1, growth, stats.TurnsUntilGrowth);
			return (grow, city.TurnsToProduce(settler));
		}

		// Whether a settler too big for the city yet may be started: its
		// shields won't be done more than SlackTurns before the citizens.
		internal static bool MayStartSettler(ChooseProducible.ProducibleStats stats, City city, Player player, UnitPrototype settler) {
			(int grow, int build) = Times(stats, city, settler);
			return build != int.MaxValue && grow != int.MaxValue && build >= grow - (int)Knob(player, SlackKnob);
		}

		// What the switch adds to a unit's score (the caller has checked
		// Active).
		internal static float UnitAdjustment(ChooseProducible.ProducibleStats stats, City city, Player player, UnitPrototype unit) {
			int slack = (int)Knob(player, SlackKnob);
			float adjustment = 0;
			if (unit.actions.Contains(UnitAction.BuildCity)) {
				(int grow, int build) = Times(stats, city, unit);
				if (build == int.MaxValue || grow == int.MaxValue) {
					return 0;
				}
				if (build > grow + slack && city.residents.Count <= unit.populationCost + 1) {
					return -(float)Knob(player, LatePenaltyKnob);
				}
				if (build >= grow - slack) {
					return stats.CityGuarded ? (float)Knob(player, SettlerBonusKnob) : 0;
				}
				return 0;
			}
			bool settlerPending = false;
			if (unit.isWorker) {
				// A worker fills the wait when a settler would be ready too
				// early, or stands in for a settler the shields can't make
				// in time.
				UnitPrototype settler = SettlerPrototype(city, player);
				if (settler != null && stats.CityGuarded) {
					(int grow, int build) = Times(stats, city, settler);
					settlerPending = build != int.MaxValue && grow != int.MaxValue
						&& (build < grow - slack || build > grow + slack);
				}
				return settlerPending ? (float)Knob(player, WorkerBonusKnob) : 0;
			}
			if (!DevelopmentAI.IsMilitaryLandUnit(unit) || !Loose(player)) {
				return adjustment;
			}
			// The cheapest soldier the city can build, and no second one.
			if (stats.CityGuarded) {
				return -ExtraSoldierPenalty;
			}
			return -CostPenalty * Math.Max(0, unit.shieldCost - CheapestSoldierCost(city, player));
		}

		private static UnitPrototype SettlerPrototype(City city, Player player) {
			foreach (UnitPrototype p in city.ListProductionOptions(EngineStorage.gameData).OfType<UnitPrototype>()) {
				if (p.actions.Contains(UnitAction.BuildCity)) {
					return p;
				}
			}
			return null;
		}

		private static int CheapestSoldierCost(City city, Player player) {
			int cheapest = int.MaxValue;
			foreach (UnitPrototype p in city.ListProductionOptions(EngineStorage.gameData).OfType<UnitPrototype>()) {
				if (DevelopmentAI.IsMilitaryLandUnit(p) && p.defense > 0) {
					cheapest = Math.Min(cheapest, p.shieldCost);
				}
			}
			return cheapest == int.MaxValue ? 0 : cheapest;
		}
	}
}

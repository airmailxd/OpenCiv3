using System;
using C7GameData;

namespace C7Engine.AI {
	// Trial switch "fast-expand": Advanced AI only, off by default. Strong
	// humans reach their 10th city at a median turn 83 (Regent 87), the
	// Advanced AI at about turn 95-105 (doc/human-benchmarks.md, and the
	// trial replays below).
	//
	// Replays of trial games (Regent, 100x100, 8 civs; seeds 7100-7103,
	// turns 1-120) with each city's build logged showed:
	// - Food sets the pace, and the cities are near it: a Settler costs 2
	//   citizens and 30 shields (compendium 02, "Settler"), a town's food
	//   box is 20 (compendium 02, section 2), and most city-turns had +2
	//   food under Despotism, so a city can make a settler every ~20
	//   turns; Advanced cities made their first at age 23 (median) and
	//   then one every 18.
	// - The shields went 41% to defenders (58% in turns 1-40), 25% to
	//   settlers, 14% to buildings (Temples, Courthouses, Walls): Civ3's
	//   garrison of two defenders and an attacker per city (see
	//   ChooseProducible.StockGarrisonBonus) is filled for each new city.
	// - A settler took 6.2 turns on average from being built to founding
	//   on land (median 5, a fifth 10 turns or more; the Standard AI 5.5),
	//   and each turn on the way delays the new city and the cities it
	//   will found in turn. 8% of Advanced settlers found no site and
	//   joined a city (Standard 3%), mostly after turn 85.
	//
	// So, while the civ is expanding (an open city site left that no
	// settler is being built for), at peace and with no enemy near the city:
	// - the garrison is one soldier per city plus an escort for each
	//   settler being built and a couple to spare, not Civ3's 2 + 1;
	//   soldiers beyond it lose SoldierPenalty;
	// - settlers gain SettlerBonus, and buildings and wonders lose
	//   BuildingPenalty;
	// - a city may start its settler before it has the citizens, when the
	//   shields will take about as long as the growth (the shields wait in
	//   the box until the city is big enough, see City.ComputeTurnProduction);
	// and always, nearer sites are preferred more strongly
	// (TravelDiscountPerTile).
	// Tried and dropped: working tiles to balance a settler's food and
	// shields (10th city 2 turns later than without it in 8 games).
	// The Standard AI and this switch off are unchanged.
	public static class FastExpand {
		public const string Switch = "fast-expand";

		public static bool On(Player player) => player != null && player.IsAdvancedAi && AiExperiments.Has(player, Switch);

		// UNVERIFIED: all the numbers below are guesses, not Civ3 rules.
		// What a settler gains: about one open site's worth
		// (ChooseProducible.SettlerSiteBonus).
		public const float SettlerBonus = 20;
		// What a soldier past the expansion garrison loses, and a building
		// or wonder: about a settler's site bonus.
		public const float SoldierPenalty = 20;
		public const float BuildingPenalty = 20;
		// Soldiers wanted on top of one per city and one per settler being
		// built: a couple against barbarians.
		public const int SpareSoldiers = 2;
		// A settler may be started when its shields would be done no more
		// than this many turns before the city has the citizens for it.
		public const int PrebuildSlackTurns = 2;
		// The food box below size 7 (compendium 02, section 2: "20 for
		// sizes 1-6 (town)"). The rules may differ; the city's own box is
		// used for the current size.
		public const int TownFoodBox = 20;

		// A settler's site is worth this share less for each tile it must
		// walk beyond FreeTravelTiles (SettlerLocationAI uses 4%). In the
		// replays a settler took 6.2 turns on average from being built to
		// founding its city on land (median 5, a fifth 10 or more), against
		// the Standard AI's 5.5; each turn on the way is a turn of the new
		// city's growth lost, and the turns add up over the generations of
		// cities that found cities.
		public const float TravelDiscountPerTile = .08f;
		public const int FreeTravelTiles = 3;

		public static float TravelFactor(int distance) {
			int beyond = Math.Max(0, distance - FreeTravelTiles);
			return Math.Max(0.05f, 1f - TravelDiscountPerTile * beyond);
		}

		// Whether the switch shapes this city's build now: on, expanding,
		// at peace, no hostile unit near.
		internal static bool Active(ChooseProducible.ProducibleStats stats, City city, Player player) {
			return On(player) && !stats.atWar && stats.InExpansionPhase && !LeanGarrison.EnemyNear(player, city);
		}

		// The soldiers the expansion garrison wants, and those we have (or
		// are building).
		internal static (int have, int want) Soldiers(Player player) {
			int have = 0;
			int settlers = 0;
			foreach (MapUnit u in player.units) {
				if (DevelopmentAI.IsMilitaryLandUnit(u.unitType)) {
					++have;
				}
			}
			foreach (City c in player.cities) {
				if (c.itemBeingProduced is UnitPrototype p) {
					if (DevelopmentAI.IsMilitaryLandUnit(p)) {
						++have;
					} else if (p.actions.Contains(UnitAction.BuildCity)) {
						++settlers;
					}
				}
			}
			return (have, player.cities.Count + settlers + SpareSoldiers);
		}

		// What the switch adds to a unit's score (the caller has checked
		// Active).
		internal static float UnitAdjustment(ChooseProducible.ProducibleStats stats, Player player, UnitPrototype unit) {
			if (unit.actions.Contains(UnitAction.BuildCity)) {
				return stats.CityGuarded ? SettlerBonus : 0;
			}
			// A city's own guard and a settler's escort are always wanted.
			if (!DevelopmentAI.IsMilitaryLandUnit(unit) || !stats.CityGuarded || stats.HasUnescortedSettler) {
				return 0;
			}
			(int have, int want) = stats.FastExpandSoldiers;
			return have >= want ? -SoldierPenalty : 0;
		}

		// What the switch adds to a building's score (the caller has
		// checked Active). A building the city needs to grow (an Aqueduct
		// at its cap) keeps most of its large bonus.
		internal static float BuildingAdjustment(Building building) => -BuildingPenalty;

		// Whether a city too small to finish the unit now may start it:
		// the shields will be done no sooner than PrebuildSlackTurns before
		// the city grows to the size it needs (one more than the unit's
		// population cost).
		internal static bool MayPrebuild(ChooseProducible.ProducibleStats stats, City city, UnitPrototype unit) {
			int growth = stats.FoodGrowthPerTurn;
			if (growth <= 0) {
				return false;
			}
			int turnsToSize = TurnsToSize(city, unit.populationCost + 1, growth, stats.TurnsUntilGrowth);
			int turnsToBuild = city.TurnsToProduce(unit);
			return turnsToBuild != int.MaxValue && turnsToSize != int.MaxValue && turnsToBuild >= turnsToSize - PrebuildSlackTurns;
		}

		// Turns until the city reaches the size at this growth: the rest of
		// its own box, then a town's box for each further citizen.
		internal static int TurnsToSize(City city, int size, int growth, int turnsUntilGrowth) {
			int steps = size - city.residents.Count;
			if (steps <= 0) {
				return 0;
			}
			if (turnsUntilGrowth < 0 || turnsUntilGrowth == int.MaxValue) {
				return int.MaxValue;
			}
			return turnsUntilGrowth + (steps - 1) * (int)Math.Ceiling(TownFoodBox / (double)growth);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine.AI {
	// While a civ is still expanding, one city in nine (at least one), the
	// one with the most surplus food, gets a Granary early and then turns out
	// settlers and workers ahead of other builds (Suede, doc/suede-benchmarks.md
	// idea 5). On trial as "granary-pump".
	//
	// Civ3 rule (doc/civ3-compendium/02-city-economy.md, cities, "Granary"):
	// when the city grows only half the food box is used up, so it keeps half
	// its food and regrows about twice as fast; a settler or worker takes
	// population, so this is the city that can pay for them. The rule only
	// helps once the box is half full, and the pump city has food to spare.
	// UNVERIFIED: the sizes of the two bonuses; guesses, about a defender's
	// worth and a settler site's worth.
	public static class GranaryPump {
		public const string Name = "granary-pump";
		public const int CitiesPerPump = 9;
		public const float GranaryBonus = 60;
		public const float UnitBonus = 40;

		// The player's pump cities: the best surplus-food ones, one per
		// CitiesPerPump cities and at least one. Ties go by city id so the
		// choice is stable.
		public static List<City> Pumps(Player player) {
			int count = Math.Max(1, player.cities.Count / CitiesPerPump);
			return player.cities
				.Where(c => c.FoodGrowthPerTurn() > 0)
				.OrderByDescending(c => c.FoodGrowthPerTurn())
				.ThenBy(c => c.id.ToString())
				.Take(count)
				.ToList();
		}

		internal static bool IsOn(Player player, ChooseProducible.ProducibleStats stats) {
			return AiExperiments.Has(player, Name) && stats.InExpansionPhase;
		}

		// Extra score for the Granary in a pump city that has none.
		internal static float BuildingBonus(ChooseProducible.ProducibleStats stats, City city, Player player, Building building) {
			if (!building.doublesCityGrowthRate || !IsOn(player, stats) || city.HasGranary()) {
				return 0;
			}
			return Pumps(player).Contains(city) ? GranaryBonus : 0;
		}

		// Extra score for a settler or worker from a pump city that has its
		// Granary. A settler is the better of the two, so the workers come
		// when no site is open or the city is too small for a settler: the
		// two alternate through the limits ScoreUnit already sets on each.
		internal static float UnitAdjustment(ChooseProducible.ProducibleStats stats, City city, Player player, UnitPrototype unit) {
			bool settler = unit.actions.Contains(UnitAction.BuildCity);
			if ((!settler && !unit.isWorker) || !IsOn(player, stats) || !city.HasGranary()) {
				return 0;
			}
			return Pumps(player).Contains(city) ? UnitBonus : 0;
		}
	}
}

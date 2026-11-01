using System;
using C7GameData;

namespace C7Engine.AI {
	// Trial switch "grow-to-settle" (ZUR-72, the owner's idea 6 from
	// watching Advanced games, doc/ai-roadmap.md): Advanced AI only. While
	// there is land left to settle, a growing city about to hit a limit,
	// its size cap (no Aqueduct or Hospital yet) or its happiness limit,
	// sheds the population it would grow into as Settlers (2 citizens) and
	// Workers (1 citizen; compendium 02, "Settler" and "Worker") instead of
	// growing into disorder or wasting the food. Specialists only when the
	// luxury slider at its cap can't keep the city in order: that is
	// lux-slider-high (PlayerAI.LuxSliderHigh), on for Advanced, which
	// this is meant to sit on.
	public static class GrowToSettle {
		public const string Switch = "grow-to-settle";

		public static bool On(Player player) => player != null && player.IsAdvancedAi && AiExperiments.Has(player, Switch);

		// UNVERIFIED: guesses, not Civ3 rules. What a settler (or worker)
		// from such a city adds to its score: about one open site's worth
		// (ChooseProducible.SettlerSiteBonus).
		public const float SettlerBonus = 20;
		public const float WorkerBonus = 10;
		// The workers such cities may build past the usual cap
		// (ChooseProducible.EnoughWorkers), per city of ours.
		public const float ExtraWorkersPerCity = .25f;

		// Whether the city should shed population rather than grow: we are
		// on, there is an open city site, it grows, and it is at or one step
		// from its size cap, or at its happiness limit.
		internal static bool Sheds(ChooseProducible.ProducibleStats stats, City city, Player player) {
			if (!On(player) || stats.FoodGrowthPerTurn <= 0 || stats.NumberOfReachableOpenCitySpots <= 0) {
				return false;
			}
			return AtSizeCap(city, player) || AtHappyLimit(city);
		}

		// At the cap, or one step from it. UNVERIFIED shortcut: one step
		// from the Aqueduct (or Hospital) size counts even where the city
		// could grow past it (fresh water, or the building), which is
		// harmless: that city still sheds a step early.
		internal static bool AtSizeCap(City city, Player player) {
			int size = city.residents.Count;
			if (!city.CanGrowPopulationByOne(EngineStorage.gameData)) {
				return true;
			}
			return size == player.rules.GrowthLimitWithoutAqueduct - 1 || size == player.rules.GrowthLimitWithoutHospital - 1;
		}

		// One more unhappy citizen would put the city into disorder (more
		// unhappy than happy, Civ3's rule), or it already keeps
		// entertainers to stay out of it.
		internal static bool AtHappyLimit(City city) {
			int happy = 0, unhappy = 0, entertainers = 0;
			foreach (CityResident cr in city.residents) {
				if (cr.citizenType.IsDefaultCitizen) {
					if (cr.mood == CityResident.Mood.Happy) { ++happy; }
					if (cr.mood == CityResident.Mood.Unhappy) { ++unhappy; }
				} else if (cr.citizenType.Luxuries > 0) {
					++entertainers;
				}
			}
			return entertainers > 0 || (unhappy > 0 && unhappy >= happy);
		}

		// What a settler or worker from the city adds.
		internal static float UnitAdjustment(ChooseProducible.ProducibleStats stats, City city, Player player, UnitPrototype unit) {
			bool settler = unit.actions.Contains(UnitAction.BuildCity);
			if ((!settler && !unit.isWorker) || unit.populationCost <= 0 || !Sheds(stats, city, player)) {
				return 0;
			}
			return settler ? SettlerBonus : WorkerBonus;
		}

		// The workers past the usual cap such a city may build.
		internal static int ExtraWorkers(ChooseProducible.ProducibleStats stats, City city, Player player) {
			if (!Sheds(stats, city, player)) {
				return 0;
			}
			return (int)Math.Ceiling(player.cities.Count * ExtraWorkersPerCity);
		}
	}
}

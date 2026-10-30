using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine.AI {
	// On trial (switch "early-rush", notes #35 of doc/ai-strategy-notes.md):
	// while a civ is still expanding, its neighbours are building settlers,
	// not defenders. So the civ raises barracks and a handful of attackers,
	// sends one stack at a weakly defended enemy city (the capital, or any
	// city of size 3 or more: a smaller one is razed when taken), and goes
	// back to settlers. The march itself is an ordinary WarCampaign; what
	// is new here is when to start one, whom to pick, and how big.
	public static class EarlyRush {
		public const string Switch = "early-rush";

		// HEURISTIC: the attackers a rush gathers.
		public const int Attackers = 5;
		// HEURISTIC: the turns to wait after a rush before the next one.
		public const int CooldownTurns = 40;
		// HEURISTIC: the smallest city worth taking: smaller ones are
		// destroyed or reduced to nothing by the capture.
		public const int MinTargetSize = 3;
		// HEURISTIC: the most defenders we can see in a city we rush.
		public const int MaxVisibleDefenders = 3;
		// HEURISTIC: how many times a fortified defender's strength (a
		// fortified unit in a city gets +50%) our best attack must reach.
		private const double FortifiedDefence = 1.5;
		// HEURISTIC: the capital is worth more than another city of the same
		// size and defence.
		public const float CapitalFactor = 1.5f;
		// HEURISTIC: what a barracks in the city the rush gathers in adds to
		// its score.
		public const float BarracksBonus = 30;

		public static bool On(Player player) {
			return AiExperiments.Has(player, Switch);
		}

		// Whether the civ is in its expansion phase and so should rush.
		internal static bool Expanding(Player player) {
			if (player.cities.Count == 0) {
				return false;
			}
			return new ChooseProducible.ProducibleStats(player.cities[0], player).InExpansionPhase;
		}

		private static IEnumerable<UnitPrototype> LandUnitsBuildable(Player player) {
			City city = player.cities.OrderByDescending(c => c.residents.Count).FirstOrDefault();
			if (city == null) {
				return Enumerable.Empty<UnitPrototype>();
			}
			return city.ListProductionOptions(EngineStorage.gameData).OfType<UnitPrototype>()
				.Where(u => u.IsLandUnit() && !u.isArmy && !u.isSettler && !u.isWorker);
		}

		// The best attack of the land units the civ can build.
		public static int BestAttack(Player player) {
			return LandUnitsBuildable(player).Select(u => u.attack).DefaultIfEmpty(0).Max();
		}

		// The best defence of the land units the civ can build: what its
		// cities will be defended with, whatever we can't see.
		public static int BestDefense(Player player) {
			return LandUnitsBuildable(player).Select(u => u.defense).DefaultIfEmpty(0).Max();
		}

		// Whether their defenders, by their tech, are weaker than our
		// attackers by ours.
		public static bool Outteched(Player us, Player them) {
			int attack = BestAttack(us);
			int defense = BestDefense(them);
			return attack > 0 && attack >= FortifiedDefence * defense;
		}
	}
}

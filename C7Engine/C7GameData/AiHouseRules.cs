using C7Engine.AI;

namespace C7GameData {
	// The house rules that favor AI players (Rules.AiBuildingMaintenancePercent
	// and the rest). These are not Civ3: they make the AI a stronger opponent
	// beyond its difficulty level bonuses. They never apply to humans.
	//
	// While a bonus is on trial (see AiTrialTest), an AiExperiments switch
	// turns it on for some AI players only, overriding the rule's value.
	public static class AiHouseRules {
		private static bool IsAi(Player player) {
			return player != null && !player.isHuman && !player.isBarbarians;
		}

		// The percentage of their normal maintenance this player's buildings
		// cost.
		public static int BuildingMaintenancePercent(Player player) {
			if (!IsAi(player)) {
				return 100;
			}
			if (AiExperiments.Has(player, "ai-maintenance-0")) {
				return 0;
			}
			if (AiExperiments.Has(player, "ai-maintenance-25")) {
				return 25;
			}
			if (AiExperiments.Has(player, "ai-maintenance-50")) {
				return 50;
			}
			return player.rules?.AiBuildingMaintenancePercent ?? 100;
		}

		// Units each of this player's cities supports for free beyond its
		// government and the difficulty level.
		public static int ExtraFreeUnitsPerCity(Player player) {
			if (!IsAi(player)) {
				return 0;
			}
			if (AiExperiments.Has(player, "ai-unit-support-2")) {
				return 2;
			}
			return player.rules?.AiExtraFreeUnitsPerCity ?? 0;
		}

		// The factor on this player's cities' corruption and waste.
		public static float CorruptionFactor(Player player) {
			if (!IsAi(player)) {
				return 1;
			}
			if (AiExperiments.Has(player, "ai-corruption-50")) {
				return .5f;
			}
			return (player.rules?.AiCorruptionPercent ?? 100) / 100f;
		}

		// Citizens born content in each of this player's cities beyond the
		// difficulty level's number.
		public static int ExtraContentCitizens(Player player) {
			if (!IsAi(player)) {
				return 0;
			}
			if (AiExperiments.Has(player, "ai-content-2")) {
				return 2;
			}
			if (AiExperiments.Has(player, "ai-content-1")) {
				return 1;
			}
			return player.rules?.AiExtraContentCitizens ?? 0;
		}

		// The factor on what this player pays in shields, food to grow and
		// research, beyond the difficulty level's cost factor.
		public static float CostFactor(Player player) {
			if (!IsAi(player)) {
				return 1;
			}
			if (AiExperiments.Has(player, "ai-cost-80")) {
				return .8f;
			}
			if (AiExperiments.Has(player, "ai-cost-90")) {
				return .9f;
			}
			return (player.rules?.AiCostPercent ?? 100) / 100f;
		}

		// Whether this player's new fighting units start veteran.
		public static bool UnitsStartVeteran(Player player) {
			if (!IsAi(player)) {
				return false;
			}
			return AiExperiments.Has(player, "ai-veteran") || (player.rules?.AiUnitsStartVeteran ?? false);
		}
	}
}

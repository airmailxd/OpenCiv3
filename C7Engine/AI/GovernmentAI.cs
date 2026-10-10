using System;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// Changes the AI's government. Before this the AI never left Despotism,
	// whose tile penalty and corruption starve its economy for the whole game.
	//
	// The AI revolts when a government it can switch to is clearly better for
	// its economy than its own, and picks the best one when the anarchy ends.
	// The scores are AI preferences, not rules: the revolution itself goes
	// through the same rules as a human's (Player.GetTurnsOfAnarchyForTransition,
	// with the difficulty's cap on the AI's anarchy).
	public static class GovernmentAI {
		private static readonly ILogger log = Log.ForContext(typeof(GovernmentAI));

		// How much better a government has to score before the AI goes through
		// anarchy for it, so small swings (a few units more or less to
		// support) don't make it change back and forth.
		internal const float RevolutionMargin = 10;

		public static void PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.isBarbarians || player.cities.Count == 0) {
				return;
			}

			if (player.government.transitionType) {
				// Leave anarchy as soon as the rules allow, as a human picking a
				// government would (see SelectGovernmentMsg).
				if (gameData.turn >= player.inAnarchyUntilTurn) {
					Government best = BestGovernment(player, gameData);
					if (best != null) {
						log.Information("{Player} leaves anarchy for {Government}", player, best.name);
						player.government = best;
						player.ApplyGovernmentRateCap();
					}
				}
				return;
			}

			// Anarchy in the middle of a war is costly, so the AI waits for
			// peace, except to get out of Despotism, which always pays.
			if (!player.government.hasTilePenalty && PlayerRelationship.IsInAnyWar(player, gameData.players)) {
				return;
			}

			Government candidate = BestGovernment(player, gameData);
			if (candidate == null || candidate == player.government
					|| Score(player, candidate) < Score(player, player.government) + RevolutionMargin) {
				return;
			}

			// The same as StartGovernmentTransitionMsg.
			Government anarchy = gameData.governments.Find(g => g.transitionType);
			if (anarchy == null) {
				return;
			}
			log.Information("{Player} starts a revolution to get {Government}", player, candidate.name);
			player.government = anarchy;
			player.ApplyGovernmentRateCap();
			player.inAnarchyUntilTurn = gameData.turn + player.GetTurnsOfAnarchyForTransition(gameData);
		}

		internal static Government BestGovernment(Player player, GameData gameData) {
			return player.GetAvailableGovernments(gameData).OrderByDescending(g => Score(player, g)).FirstOrDefault();
		}

		// How good the government would be for the player's economy, roughly
		// in gold per turn equivalents.
		internal static float Score(Player player, Government government) {
			if (government.transitionType) {
				return float.MinValue;
			}

			float score = 0;

			// Despotism's -1 on any tile making more than 2 of something.
			if (!government.hasTilePenalty) {
				score += 20;
			}
			if (government.hasTradeBonus) {
				score += 10;
			}

			// Corruption and waste. Communal corruption doesn't grow with
			// distance, so it gets better the bigger the empire.
			score -= government.corruptionType switch {
				Government.CorruptionType.Minimal => 0,
				Government.CorruptionType.Nuisance => 4,
				Government.CorruptionType.Problematic => 8,
				Government.CorruptionType.Rampant => 12,
				Government.CorruptionType.Communal => Math.Max(2, 10 - player.cities.Count / 3),
				Government.CorruptionType.Catastrophic => 40,
				_ => 0,
			};

			// War weariness only matters to an AI that fights, and its units
			// cost what they cost below; keep it small so it can't flip the
			// choice every time a war starts or ends.
			score -= 2 * government.warWeariness;

			// What the current army would cost to support: a Republic's
			// upkeep for an army raised under Despotism can be ruinous.
			score -= UnitSupportCost(player, government) / 2f;

			return score;
		}

		// The gold per turn the player's units would cost under the
		// government, as Player.TotalUnitsAllowedUnitsAndSupportCostRaw works
		// it out for the current one.
		internal static int UnitSupportCost(Player player, Government government) {
			if (government.allUnitsFree) {
				return 0;
			}
			Difficulty difficulty = EngineStorage.gameData.gameDifficulty;
			int free = difficulty.AdditionalFreeUnitSupport;
			foreach (City city in player.cities) {
				free += difficulty.UnitSupportBonusForEachSettlement;
				if (city.residents.Count <= player.rules.MaximumLevel1CitySize) {
					free += government.freeUnitsPerTown;
				} else if (city.residents.Count <= player.rules.MaximumLevel2CitySize) {
					free += government.freeUnitsPerCity;
				} else {
					free += government.freeUnitsPerMetropolis;
				}
			}
			int paid = player.units.Count(u => !u.IsCaptive());
			return Math.Max(0, paid - free) * government.unitCost;
		}
	}
}

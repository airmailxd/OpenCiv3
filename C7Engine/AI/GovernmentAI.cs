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
					Government best = BestGovernment(player, gameData, WarPressure(player, gameData));
					if (best != null) {
						log.Information("{Player} leaves anarchy for {Government}", player, best.name);
						player.government = best;
						player.ApplyGovernmentRateCap();
					}
				}
				return;
			}

			// Anarchy in the middle of a war is costly, so the AI waits for
			// peace, except to get out of Despotism, which always pays, or
			// when the war has worn its people down (see WarPressure).
			int pressure = WarPressure(player, gameData);
			if (!player.government.hasTilePenalty && pressure == 0 && PlayerRelationship.IsInAnyWar(player, gameData.players)) {
				return;
			}

			Government candidate = BestGovernment(player, gameData, pressure);
			if (candidate == null || candidate == player.government
					|| Score(player, candidate, pressure) < Score(player, player.government, pressure) + RevolutionMargin) {
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

		internal static Government BestGovernment(Player player, GameData gameData, int warPressure = 0) {
			return player.GetAvailableGovernments(gameData).OrderByDescending(g => Score(player, g, warPressure)).FirstOrDefault();
		}

		// After a long war the Civ3 AI "switches to Comm or Monarchy", which
		// a player put down to war and war weariness: "I think it is entirely
		// based on War & war weariness." (chiefpaco,
		// https://forums.civfanatics.com/threads/about-governments.21298/,
		// compendium 08 section 3.6). So an AI at war may change government
		// once the war has worn its people down, and weighs war weariness by
		// how worn they are. The project owner asked for this. In paired
		// trials (AiTrialTest, 8 games of 200 turns, as the switch
		// "war-government") it scored -29 +- 25 on Continents and +6 +- 47
		// on Pangaea, with about a turn more anarchy per civ: not clearly
		// weaker, so it is how every AI plays.
		//
		// HEURISTIC: a war this many turns old counts as a long one.
		internal const int LongWarTurns = 30;

		// HEURISTIC: the war weariness level (Player.WarWearinessLevel, 0 to
		// 4) against an enemy at which a government that suffers war
		// weariness is worth leaving mid-war. At level 2 half the citizens
		// of a government with low war weariness are unhappy, and all of
		// one with high.
		internal const int SignificantWarWearinessLevel = 2;

		// How hard a war presses the AI to change government, from 0 (not
		// at war, or not pressed) to 4: the highest war weariness level
		// against an enemy it is at war with, if that is significant and its
		// government suffers from war weariness, and at least 1 once a war
		// has lasted LongWarTurns.
		internal static int WarPressure(Player player, GameData gameData) {
			int level = 0;
			bool longWar = false;
			foreach (Player other in gameData.players) {
				if (other == player || other.isBarbarians || other.id == null
						|| !player.playerRelationships.TryGetValue(other.id, out PlayerRelationship pr) || !pr.AtWar()) {
					continue;
				}
				level = Math.Max(level, Player.WarWearinessLevel(pr.warWearinessPoints));
				if (pr.warStartTurn >= 0 && gameData.turn - pr.warStartTurn >= LongWarTurns) {
					longWar = true;
				}
			}
			// In anarchy, the government it left is unknown, so the level
			// alone decides.
			bool suffers = player.government != null && (player.government.warWeariness > 0 || player.government.transitionType);
			int pressure = suffers && level >= SignificantWarWearinessLevel ? level : 0;
			return longWar ? Math.Max(1, pressure) : pressure;
		}

		// How good the government would be for the player's economy, roughly
		// in gold per turn equivalents. Under war pressure (see WarPressure)
		// war weariness counts for more.
		internal static float Score(Player player, Government government, int warPressure = 0) {
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
			// HEURISTIC: under war pressure, 8 more for each level of
			// pressure and degree of war weariness, so that at level 2 a
			// Republic (low war weariness in the standard rules) loses 16
			// and a Democracy (high) 32, which outweighs their trade bonus
			// against Monarchy, Communism or Fascism (none).
			score -= 8 * warPressure * government.warWeariness;

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
			int extraPerCity = AiHouseRules.ExtraFreeUnitsPerCity(player);
			foreach (City city in player.cities) {
				free += difficulty.UnitSupportBonusForEachSettlement + extraPerCity;
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

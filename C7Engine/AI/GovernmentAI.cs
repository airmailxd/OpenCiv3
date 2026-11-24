using System;
using System.Collections.Generic;
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
		// The favourite government's tilt: a quarter of the score, and at
		// least FavoriteGovernmentMinimum. Civ3's eighth (see Score) was too
		// little to tell apart governments that score alike, so under
		// Modern's rules every AI chose Freedom.
		internal const float FavoriteGovernmentWeight = .25f;
		internal const float FavoriteGovernmentMinimum = 6;

		// Suede (notes #23, #73) leaves Despotism as early as he can: the
		// research goes to the best government within reach, and the AI
		// revolts the moment it knows it, with no margin to clear. The
		// bonus is added to the value of each tech on the way there.
		// UNVERIFIED: the size; a guess, well above any unit's worth.
		public const string EarlyGovernment = "early-government";
		public const double EarlyGovernmentBonus = 400;

		// HEURISTIC: a government more techs away than this isn't "early".
		internal const int EarlyGovernmentMaxTechs = 4;

		public static void PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.IsIndependent || player.cities.Count == 0) {
				return;
			}

			if (player.government.transitionType) {
				// Leave anarchy as soon as the rules allow, as a human picking a
				// government would (see SelectGovernmentMsg).
				if (gameData.turn >= player.inAnarchyUntilTurn) {
					Government best = BestGovernment(player, gameData, WarPressure(player, gameData));
					if (best != null) {
						log.Information("{Player} leaves anarchy for {Government}", player, best.name);
						player.AdoptGovernment(best);
					}
				}
				return;
			}

			Government candidate = RevolutionTarget(player, gameData);
			if (candidate == null) {
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

		// The government the AI would start a revolution for now, or null to
		// stay as it is.
		internal static Government RevolutionTarget(Player player, GameData gameData) {
			// Anarchy in the middle of a war is costly, so the AI waits for
			// peace, except to get out of Despotism, which always pays, or
			// when the war has worn its people down (see WarPressure).
			int pressure = WarPressure(player, gameData);
			if (!player.government.hasTilePenalty && pressure == 0 && PlayerRelationship.IsInAnyWar(player, gameData.players)) {
				return null;
			}

			Government candidate = BestGovernment(player, gameData, pressure);
			bool early = AiExperiments.Has(player, EarlyGovernment) && player.government.hasTilePenalty;
			if (early && IsLosingAWar(player)) {
				return null;
			}
			if (candidate == null || candidate == player.government
					|| Score(player, candidate, pressure) < Score(player, player.government, pressure) + (early ? 0 : RevolutionMargin)) {
				return null;
			}
			return candidate;
		}

		private static bool IsLosingAWar(Player player) {
			foreach (Player other in EngineStorage.gameData.players) {
				if (other != player && !other.IsIndependent && !other.defeated && other.id != null && player.playerRelationships.TryGetValue(other.id, out PlayerRelationship pr) && pr.AtWar()
						&& PeaceAI.Assess(player, other) <= PeaceAI.WarOutlook.Losing) {
					return true;
				}
			}
			return false;
		}

		// The techs still to learn on the way to the best government (by
		// Score) the player doesn't have yet but could soon, while in a
		// government with the tile penalty and with the switch on; empty
		// otherwise.
		internal static HashSet<Tech> EarlyGovernmentPath(Player player, GameData gameData) {
			return AiExperiments.Has(player, EarlyGovernment) ? RoutePath(player, gameData) : new HashSet<Tech>();
		}

		// The same, whether or not early-government is on.
		internal static HashSet<Tech> RoutePath(Player player, GameData gameData) {
			HashSet<Tech> path = new();
			if (player.government == null || !player.government.hasTilePenalty) {
				return path;
			}
			Government best = null;
			float bestScore = float.MinValue;
			foreach (Government g in gameData.governments) {
				if (g.transitionType || g.hasTilePenalty || !gameData.rules.AllowsGovernment(g)) {
					continue;
				}
				Tech tech = gameData.techs.Find(t => t.id == g.prerequisiteTech);
				if (tech == null || player.HasTech(g.prerequisiteTech)) {
					return new HashSet<Tech>();
				}
				HashSet<Tech> missing = new();
				AddMissing(player, tech, missing);
				float score = Score(player, g);
				if (missing.Count <= EarlyGovernmentMaxTechs && score > bestScore) {
					best = g;
					bestScore = score;
					path = missing;
				}
			}
			return best == null ? new HashSet<Tech>() : path;
		}

		// Suede's route (ZUR-130): Code of Law and Philosophy first, then
		// Philosophy's free tech on Republic (the first civ to learn
		// Philosophy picks any researchable tech free, so Republic must be
		// researchable then). With the switch on, while in Despotism and
		// before anyone has Philosophy, the techs still to learn for
		// Philosophy and for Republic's prerequisites count for more; when
		// a free tech is due, Republic's does. Once Philosophy is known to
		// anyone, the route is Republic and what it needs. The revolt itself stays with
		// early-government.
		public const string RepublicRoute = "republic-route";
		public const double RepublicRouteBonus = 400;

		internal static HashSet<Tech> RepublicRoutePath(Player player, GameData gameData) {
			HashSet<Tech> path = new();
			if (!AiExperiments.Has(player, RepublicRoute) || player.government == null || !player.government.hasTilePenalty) {
				return path;
			}
			Government republic = gameData.governments.Find(g => g.name == "Republic");
			Tech republicTech = republic == null || !gameData.rules.AllowsGovernment(republic) ? null : gameData.techs.Find(t => t.id == republic.prerequisiteTech);
			Tech philosophy = gameData.techs.Find(t => t.Name == "Philosophy");
			if (republicTech == null || philosophy == null || player.HasTech(republicTech.id)) {
				return path;
			}
			if (player.freeTechsRemaining > 0) {
				path.Add(republicTech);
				return path;
			}
			if (player.IsFirstToLearn(gameData, philosophy) && !player.HasTech(philosophy.id)) {
				AddMissing(player, philosophy, path);
				foreach (Tech prereq in republicTech.Prerequisites) {
					AddMissing(player, prereq, path);
				}
			} else {
				// Philosophy is gone to someone else (or this civ has it):
				// go straight for Republic.
				AddMissing(player, republicTech, path);
			}
			return path;
		}

		// The techs worth asking another civ for first: Republic's route
		// with republic-route on, early-government's route otherwise.
		internal static HashSet<Tech> AskFirstPath(Player player, GameData gameData) {
			return AiExperiments.Has(player, RepublicRoute) ? RepublicRoutePath(player, gameData) : RoutePath(player, gameData);
		}

		private static void AddMissing(Player player, Tech tech, HashSet<Tech> missing) {
			if (tech == null || player.HasTech(tech.id) || !missing.Add(tech)) {
				return;
			}
			foreach (Tech prereq in tech.Prerequisites) {
				AddMissing(player, prereq, missing);
			}
		}

		// HEURISTIC: the civ's favourite government is the one it picks
		// whenever it scores within this much of the best (as well as the
		// tilt in Score). Civ3's tilt alone (see Score) never outweighed
		// Freedom's trade bonus and minimal corruption under Modern's rules:
		// in 12 headless games every AI chose Freedom (owner's request,
		// 2026-11-22: "respect the civ's favourite government more").
		internal const float FavoriteGovernmentBand = 20;

		internal static Government BestGovernment(Player player, GameData gameData, int warPressure = 0) {
			List<Government> available = player.GetAvailableGovernments(gameData);
			Government best = available.OrderByDescending(g => Score(player, g, warPressure)).FirstOrDefault();
			string favorite = player.civilization?.favoriteGovernment;
			Government liked = favorite == null ? null : available.Find(g => g.name == favorite && !g.transitionType);
			if (best != null && liked != null && liked != best && !liked.hasTilePenalty
					&& Score(player, liked, warPressure) >= Score(player, best, warPressure) - FavoriteGovernmentBand) {
				return liked;
			}
			return best;
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
				if (other == player || other.IsIndependent || other.id == null
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

			// The buildings and wonders only this government may build
			// (Modern's T, F, O and E buildings).
			score += OwnBuildingsWorth(player, government);

			// Workers that work faster (Modern's Order works at 6, Freedom at
			// 4; Civ3's governments all at 1).
			score += Math.Min(5, Math.Max(0, government.workerRate - 1));

			// A civ going to war (or in one) values free units and the
			// draft more.
			if (OpportunityWar.Victim(player, EngineStorage.gameData) != null || PlayerRelationship.IsInAnyWar(player, EngineStorage.gameData.players)) {
				score += Math.Min(10, (government.freeUnitsPerCity + government.freeUnitsPerTown) / 2f + government.draftLimit);
			}

			// "Shunned/favorite government is worth 12.5%" of the score
			// (Flintlock reading the stock code, C3X thread post-16217016;
			// doc/civ3-compendium/05 section 5.8). Whether it is added or
			// multiplied is not stated (UNVERIFIED); a tilt by an eighth of
			// the score's size keeps a negative score from getting better by
			// being shunned.
			string name = government.name;
			if (name != null && name == player.civilization?.favoriteGovernment) {
				score += Math.Max(FavoriteGovernmentMinimum, Math.Abs(score) * FavoriteGovernmentWeight);
			} else if (name != null && name == player.civilization?.shunnedGovernment) {
				score -= Math.Max(FavoriteGovernmentMinimum, Math.Abs(score) * FavoriteGovernmentWeight);
			}

			return score;
		}

		// HEURISTIC: what the buildings and wonders only this government may
		// build are worth to the player, roughly in gold a turn: an
		// improvement for what it adds to each city (content faces, gold,
		// production, culture, corruption, research, taxes), counted for up
		// to OwnBuildingCities cities; a wonder for what it adds once or to
		// every city. One whose advance is far off counts for less; one
		// needing an advance of no era the player lacks (another civ's part
		// of a wonder) not at all. A government whose buildings stand in for
		// another's (all four of Modern's have a granary and a harbour)
		// gains about what it loses, so what tells them apart is what only
		// one has: Tradition's temples, Freedom's banks, Order's CCTV,
		// Equality's airports. The League's notes on Modern's buildings
		// (CivPlayersCiv3League.github.io/modern/buildings.psv) give the
		// same effects.
		internal const int OwnBuildingCities = 8;
		internal const float OwnBuildingWeight = .1f;
		internal const float OwnBuildingsCap = 15;

		internal static float OwnBuildingsWorth(Player player, Government government) {
			GameData gameData = EngineStorage.gameData;
			if (gameData == null || government?.id == null) {
				return 0;
			}
			int cities = Math.Clamp(player.cities.Count, 1, OwnBuildingCities);
			float worth = 0;
			foreach (Building b in gameData.Buildings) {
				if (b.requiredGovernment?.id != government.id) {
					continue;
				}
				if (b.renderedObsoleteBy != null && player.HasTech(b.renderedObsoleteBy.id)) {
					continue;
				}
				float reach = 1;
				if (b.requiredTech != null && !player.HasTech(b.requiredTech.id)) {
					if (b.requiredTech.EraCivilopediaName == EraUtils.HIDDEN_CVLPD) {
						continue;
					}
					reach = b.requiredTech.Prerequisites.All(t => player.HasTech(t.id)) ? .5f : .25f;
				}
				float perCity = b.contentFacesInCity + b.goldPerTurn - b.maintenanceCost + b.productionBonusPercent / 12.5f
					+ b.culturePerTurn / 4f + (b.reducesCorruption ? 1.5f : 0) + (b.increasesResearch || b.doublesResearch ? 1.5f : 0)
					+ (b.increasesTax ? 1.5f : 0) + (b.increasesLuxury ? 1 : 0);
				float value;
				if (b.IsGreatWonder() || b.isSmallWonder) {
					value = 3 + b.contentFacesAllCities * cities / 2f + perCity + (b.unitProducedName != null ? 3 : 0)
						+ (b.doublesCityGrowthEverywhere ? 3 : 0) + (b.twoFreeAdvances ? 4 : 0);
				} else {
					value = Math.Max(0, perCity) * cities;
				}
				worth += Math.Max(0, value) * reach;
			}
			return Math.Min(OwnBuildingsCap, worth * OwnBuildingWeight);
		}

		// The gold per turn the player's units would cost under the
		// government, as Player.TotalUnitsAllowedUnitsAndSupportCostRaw works
		// it out for the current one.
		internal static int UnitSupportCost(Player player, Government government) {
			if (government.allUnitsFree) {
				return 0;
			}
			Difficulty difficulty = EngineStorage.gameData.AiBonusDifficulty(player);
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
			int paid = player.units.Count(u => !u.IsCaptive() && u.unitType.requiresSupport);
			return Math.Max(0, paid - free) * government.unitCost;
		}
	}
}

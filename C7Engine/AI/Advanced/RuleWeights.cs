using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.Advanced {
	// The seven strategy categories of doc/suede-rules.json, each with a
	// weight from 0 to 100 (the Weight* knobs of AdvancedTuning). A weight
	// of 50 is today's behaviour exactly; above it a category's existing
	// levers lean harder, below it softer (see Lean). Weights only rank
	// what is already legal and affordable: they scale priorities and
	// targets, they never switch a rule on and never lift a feasibility
	// check (CityIsTooSmall, defence minimums, growth checks). A rule the
	// registry marks `disabled` has no switch here to turn on.
	//
	// With the weights-by-state switch on, the effective weight is
	// base x era x state x opportunity, worked out when the civ re-plans;
	// off, it is the base weight.
	public enum RuleCategory {
		Expansion, Workers, Research, Military, Diplomacy, Infrastructure, Wonders,
	}

	public static class RuleWeights {
		public const string ByState = "weights-by-state";
		public const double Neutral = 50;

		public static string KnobName(RuleCategory c) => "Weight" + c;

		// The facts the multipliers read, as the engine knows them.
		public sealed record Situation(
			int era = 0,
			int openSites = 0,
			bool expanding = false,
			bool scarceLand = false,
			bool contestedLand = false,
			bool invasionThreat = false,
			bool atWar = false,
			bool losing = false,
			bool behindInTech = false,
			bool missingResources = false,
			bool strongestRivalCanFight = false,
			bool corruptCity = false);

		// Era: expansion fades as the land fills (HEURISTIC: Ancient ~100,
		// then 80, 50, 30 per cent of it); research rises with the eras.
		private static readonly double[] expansionByEra = [1, .8, .5, .3];
		private static readonly double[] researchByEra = [1, 1.1, 1.2, 1.2];
		private static readonly double[] militaryByEra = [1, 1, 1.1, 1.1];
		private static readonly double[] workersByEra = [1, 1, 1.1, 1.1];

		// Registry multipliers (doc/suede-rules.json `modifiers`: the mean
		// factor of the category's rules that are not `disabled`; a test
		// checks these against the file).
		public const double ScarceLand = .68, ContestedLand = 1.17, InvasionThreat = .4, NoSites = .25;
		public const double ResearchExpansionPhase = 1.2, WondersExpansionPhase = 1.2, CorruptCity = 1.2;
		public const double StrongestRival = .92;
		// Opportunity (HEURISTIC, not from the registry).
		public const double BehindInTech = 1.2, MissingResources = 1.2;

		private static int EraSlot(int era) => Math.Clamp(era, 0, 3);

		// base x era x state x opportunity, clamped to 0..100.
		public static double Effective(RuleCategory c, double baseWeight, Situation s) {
			double w = baseWeight * Era(c, s) * State(c, s) * Opportunity(c, s);
			return Math.Clamp(w, 0, 100);
		}

		public static double Era(RuleCategory c, Situation s) => c switch {
			RuleCategory.Expansion => expansionByEra[EraSlot(s.era)],
			RuleCategory.Research => researchByEra[EraSlot(s.era)],
			RuleCategory.Military => militaryByEra[EraSlot(s.era)],
			RuleCategory.Workers => workersByEra[EraSlot(s.era)],
			_ => 1,
		};

		public static double State(RuleCategory c, Situation s) {
			double m = 1;
			switch (c) {
				case RuleCategory.Expansion:
					if (s.openSites == 0) {
						m *= NoSites;
					} else if (s.scarceLand) {
						m *= ScarceLand;
					}
					if (s.contestedLand && s.openSites > 0) {
						m *= ContestedLand;
					}
					if (s.invasionThreat) {
						m *= InvasionThreat;
					}
					break;
				case RuleCategory.Military:
					if (s.invasionThreat) {
						m *= InvasionThreat;
					}
					break;
				case RuleCategory.Research:
					if (s.expanding) {
						m *= ResearchExpansionPhase;
					}
					break;
				case RuleCategory.Wonders:
					if (s.expanding) {
						m *= WondersExpansionPhase;
					}
					break;
				case RuleCategory.Infrastructure:
					if (s.corruptCity) {
						m *= CorruptCity;
					}
					break;
				case RuleCategory.Diplomacy:
					if (s.strongestRivalCanFight) {
						m *= StrongestRival;
					}
					break;
			}
			return m;
		}

		public static double Opportunity(RuleCategory c, Situation s) {
			double m = 1;
			if (c == RuleCategory.Research && s.behindInTech) {
				m *= BehindInTech;
			}
			if (c == RuleCategory.Diplomacy && s.missingResources) {
				m *= MissingResources;
			}
			return m;
		}

		// A lever's value leaned by the effective weight: unchanged at 50,
		// pushed away from its neutral point above it, pulled toward it
		// below. Returned as given when the weight is 50.
		public static double Lean(double weight, double lever, double neutral = Neutral) =>
			weight == Neutral ? lever : neutral + (lever - neutral) * weight / Neutral;

		// ---- per player ----

		private sealed class Cached {
			public double[] weights;
		}
		private static readonly ConditionalWeakTable<Player, Cached> cache = new();

		public static double BaseWeight(Player player, RuleCategory c) => AdvancedTuning.Get(player, KnobName(c));

		// The weight now: worked out at the last re-plan with the
		// weights-by-state switch on, the base weight otherwise. 50 for a
		// civ that isn't an Advanced AI.
		public static double Effective(Player player, RuleCategory c) {
			if (player == null || !player.IsAdvancedAi) {
				return Neutral;
			}
			if (AdvancedTuning.Get(player, ByState) >= .5 && cache.TryGetValue(player, out Cached k) && k.weights != null) {
				return k.weights[(int)c];
			}
			return BaseWeight(player, c);
		}

		// Convenience for a lever: the value leaned by the player's weight.
		public static double Lean(Player player, RuleCategory c, double lever, double neutral = Neutral) =>
			Lean(Effective(player, c), lever, neutral);

		public static int LeanInt(Player player, RuleCategory c, int lever, int neutral = (int)Neutral) {
			double w = Effective(player, c);
			return w == Neutral ? lever : (int)Math.Round(Lean(w, lever, neutral));
		}

		// Works out the weights from the situation; called when the civ re-plans.
		public static void Update(Player player, GameData gameData) {
			if (player == null || !player.IsAdvancedAi || AdvancedTuning.Get(player, ByState) < .5) {
				return;
			}
			Situation s = Observe(player, gameData);
			double[] w = new double[Enum.GetValues<RuleCategory>().Length];
			foreach (RuleCategory c in Enum.GetValues<RuleCategory>()) {
				w[(int)c] = Effective(c, BaseWeight(player, c), s);
			}
			cache.GetOrCreateValue(player).weights = w;
		}

		// The facts, from what the civ knows.
		public static Situation Observe(Player player, GameData gameData) {
			List<Player> met = gameData.players.Where(p => p != player && !p.IsIndependent && !p.defeated
				&& player.playerRelationships.ContainsKey(p.id)).ToList();
			List<Player> enemies = met.Where(p => AtWar(player, p)).ToList();
			int open = player.cities.Count == 0 ? 0 : player.cities.Max(c => ChooseProducible.NumberOfReachableOpenCitySpots(c));
			bool expanding = open > 0 && player.cities.Count < 2 * Math.Max(1, player.GetAdjustedOptimalCityNumber(gameData));
			float ours = player.CalculateMilitaryStrength();
			Player strongest = met.Where(p => PeaceAI.CanFightEachOther(player, p))
				.OrderByDescending(p => p.CalculateMilitaryStrength()).FirstOrDefault();
			bool threat = strongest != null && strongest.CalculateMilitaryStrength() > RulePlanner.ThreatStrengthRatio(player) * Math.Max(1f, ours);
			bool losing = enemies.Count > 0 && PeaceAI.Assess(player, enemies.OrderBy(e => PeaceAI.Assess(player, e)).First()) <= PeaceAI.WarOutlook.Losing;
			bool behind = false;
			if (met.Count > 0) {
				behind = player.knownTechs.Count < RulePlanner.BehindInTechShare(player) * met.Average(p => p.knownTechs.Count);
			}
			bool missing = met.Any(p => !AtWar(player, p) && TradeOffer.TradableResources(gameData, p, player)
				.Any(r => TradeOffer.ResourceWorth(gameData, player, r, given: false) > 0));
			return new Situation(
				era: player.EraIndex(),
				openSites: open,
				expanding: expanding,
				scarceLand: open < Math.Max(1, player.cities.Count) / 2,
				contestedLand: met.Any(p => p.cities.Count > 0 && player.cities.Any(c => p.cities.Any(o => o.location.continent == c.location.continent
					&& o.location.DistanceTo(c.location) <= 8))),
				invasionThreat: threat,
				atWar: enemies.Count > 0,
				losing: losing,
				behindInTech: behind,
				missingResources: missing,
				strongestRivalCanFight: strongest != null,
				corruptCity: player.cities.Any(c => c.corruption >= .5f));
		}

		// ---- rule-use log ----

		// Per civ: which categories moved which decisions ("production",
		// "research", "war", "trade"), counted when a lever came out
		// different from the one a weight of 50 gives. The trial writes
		// the counts to its JSON as ruleUse.
		private static readonly Dictionary<Player, SortedDictionary<string, int>> use = new();

		public static void Used(Player player, RuleCategory c, string decision) {
			if (player == null || !player.IsAdvancedAi || Effective(player, c) == Neutral) {
				return;
			}
			if (!use.TryGetValue(player, out SortedDictionary<string, int> counts)) {
				use[player] = counts = new();
			}
			string key = $"{decision}:{c}";
			counts[key] = counts.GetValueOrDefault(key) + 1;
		}

		public static IReadOnlyDictionary<string, int> UseOf(Player player) =>
			use.TryGetValue(player, out SortedDictionary<string, int> counts) ? counts : new SortedDictionary<string, int>();

		public static void ResetUse() => use.Clear();
	}
}

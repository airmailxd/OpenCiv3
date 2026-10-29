using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.Advanced {
	// The Advanced AI's planner when no AI model is set up: rules that read
	// the civ's situation and set its plan (see StrategicPlan). Everything
	// it reads is what the civ knows: its own state, and rivals it has met.
	//
	// Its judgement of war is deliberately sober: Vox Deorum's LLM
	// strategists lost games to "wishful thinking" (picking an offensive
	// strategy while losing), so a war is only sought with a clear edge in
	// strength, and a losing war turns the plan to defence at once.
	public static class RulePlanner {
		// HEURISTIC: how much stronger than a rival (with its pact partners)
		// the civ must be to make it the plan's target, the same edge
		// PeaceAI looks for before threatening a war (WinningStrengthRatio
		// is 1.5) and a bit more, since the plan starts the war itself.
		public static float TargetStrengthRatio(Player p) => (float)AdvancedTuning.Get(p, "TargetStrengthRatio");
		// HEURISTIC: a rival this much stronger is a threat to guard against.
		public static float ThreatStrengthRatio(Player p) => (float)AdvancedTuning.Get(p, "ThreatStrengthRatio");
		// HEURISTIC: behind in techs when below this share of the average
		// of the civs it has met.
		public static float BehindInTechShare(Player p) => (float)AdvancedTuning.Get(p, "BehindInTechShare");

		public static StrategicPlan Plan(Player player, GameData gameData) {
			StrategicPlan plan = new() { source = "rules" };
			List<string> why = [];
			float ours = player.CalculateMilitaryStrength();
			List<Player> met = gameData.players.Where(p => p != player && !p.IsIndependent && !p.defeated
				&& player.playerRelationships.ContainsKey(p.id)).ToList();
			List<Player> enemies = met.Where(p => AtWar(player, p)).ToList();

			if (enemies.Count > 0) {
				PlanWar(player, plan, enemies, met, why);
			} else {
				PlanPeace(player, gameData, plan, met, ours, why);
			}

			// Science: catch up with the civs it knows.
			if (met.Count > 0) {
				double average = met.Average(p => p.knownTechs.Count);
				if (player.knownTechs.Count < BehindInTechShare(player) * average) {
					Raise(plan, PlanFocus.Science, 20);
					Raise(plan, PlanFocus.Trade, 10);
					why.Add($"behind in techs ({player.knownTechs.Count} vs {average:0.#})");
				}
			}

			// Order: cities in disorder make nothing.
			int disorder = player.cities.Count(c => c.isInCivilDisorder);
			if (disorder > 0) {
				Raise(plan, PlanFocus.Happiness, 25);
				why.Add($"{disorder} cities in disorder");
			}

			plan.rationale = string.Join("; ", why);
			plan.Clamp();
			return plan;
		}

		private static void PlanWar(Player player, StrategicPlan plan, List<Player> enemies, List<Player> met, List<string> why) {
			// The war that goes worst decides.
			Player worst = enemies.OrderBy(e => PeaceAI.Assess(player, e)).First();
			PeaceAI.WarOutlook outlook = PeaceAI.Assess(player, worst);
			if (outlook <= PeaceAI.WarOutlook.Losing) {
				Set(plan, PlanFocus.Defense, 90);
				Set(plan, PlanFocus.Offense, 45);
				Set(plan, PlanFocus.Settlers, 25);
				Set(plan, PlanFocus.Culture, 35);
				Set(plan, PlanFocus.Science, 40);
				plan.aggressionShift = -2;
				// No new fronts.
				foreach (Player p in met.Where(p => !enemies.Contains(p))) {
					plan.stances[p.id.ToString()] = RivalStance.Friend;
				}
				plan.victory = null;
				why.Add($"losing the war with {worst}: defend, no new wars");
				return;
			}
			if (outlook == PeaceAI.WarOutlook.Even) {
				Set(plan, PlanFocus.Defense, 65);
				Set(plan, PlanFocus.Offense, 65);
				Set(plan, PlanFocus.Artillery, 60);
				plan.aggressionShift = -1;
				foreach (Player p in met.Where(p => !enemies.Contains(p))) {
					plan.stances[p.id.ToString()] = RivalStance.Friend;
				}
				why.Add($"even war with {worst}: build up both ways");
				return;
			}
			// Winning every war: press the weakest enemy that has cities.
			Player prey = enemies.Where(e => e.cities.Count > 0).OrderBy(e => e.CalculateMilitaryStrength()).FirstOrDefault();
			Set(plan, PlanFocus.Offense, AdvancedTuning.GetInt(player, "WinningOffense"));
			Set(plan, PlanFocus.Artillery, 70);
			Set(plan, PlanFocus.Defense, 50);
			Set(plan, PlanFocus.Settlers, 40);
			if (prey != null) {
				plan.stances[prey.id.ToString()] = RivalStance.Target;
			}
			foreach (Player p in met.Where(p => !enemies.Contains(p))) {
				plan.stances[p.id.ToString()] = RivalStance.Friend;
			}
			plan.victory = "Conquest";
			why.Add($"winning the war{(prey != null ? $": press {prey}" : "")}");
		}

		private static void PlanPeace(Player player, GameData gameData, StrategicPlan plan, List<Player> met, float ours, List<string> why) {
			// Expand while there is room: land taken early pays all game.
			int open = player.cities.Count == 0 ? 0 : player.cities.Max(c => ChooseProducible.NumberOfReachableOpenCitySpots(c));
			bool expanding = open > 0 && player.cities.Count < 2 * Math.Max(1, player.GetAdjustedOptimalCityNumber(gameData));
			if (expanding) {
				Set(plan, PlanFocus.Settlers, AdvancedTuning.GetInt(player, "ExpandSettlers"));
				Set(plan, PlanFocus.Workers, 60);
				Set(plan, PlanFocus.Growth, 60);
				Set(plan, PlanFocus.Offense, 35);
				plan.aggressionShift = AdvancedTuning.GetInt(player, "ExpandAggression");
				plan.victory = "Space";
				why.Add($"{open} open city sites: expand");
			}

			// A threat on its land: guard against it.
			Player threat = met.Where(p => PeaceAI.CanFightEachOther(player, p))
				.OrderByDescending(p => p.CalculateMilitaryStrength()).FirstOrDefault();
			if (threat != null && threat.CalculateMilitaryStrength() > ThreatStrengthRatio(player) * Math.Max(1f, ours)) {
				Raise(plan, PlanFocus.Defense, 20);
				plan.stances[threat.id.ToString()] = RivalStance.Friend;
				why.Add($"{threat} is much stronger: guard, keep the peace with them");
			}

			// Room gone: a much weaker neighbor it can march on is the
			// cheapest land left.
			if (!expanding) {
				Player prey = met.Where(p => p.cities.Count > 0 && PeaceAI.CanFightEachOther(player, p)
						&& plan.StanceToward(p.id) != RivalStance.Friend
						&& ours >= TargetStrengthRatio(player) * Math.Max(1f, PeaceAI.StrengthWithPartners(gameData, p))
						&& p.cities.Any(c => player.cities.Any(o => o.location.continent == c.location.continent)))
					.OrderBy(p => p.CalculateMilitaryStrength()).FirstOrDefault();
				if (prey != null) {
					plan.stances[prey.id.ToString()] = RivalStance.Target;
					plan.aggressionShift = AdvancedTuning.GetInt(player, "PreyAggression");
					Set(plan, PlanFocus.Offense, 75);
					Set(plan, PlanFocus.Artillery, 65);
					plan.victory = "Conquest";
					why.Add($"no room left; {prey} is much weaker: prepare to take their land");
				} else {
					Set(plan, PlanFocus.Science, AdvancedTuning.GetInt(player, "BuildScience"));
					Set(plan, PlanFocus.Production, AdvancedTuning.GetInt(player, "BuildProduction"));
					Set(plan, PlanFocus.Trade, 60);
					Set(plan, PlanFocus.Growth, 60);
					Set(plan, PlanFocus.Culture, 55);
					plan.victory = "Space";
					why.Add("no room and no easy prey: build up");
				}
			}
		}

		private static void Set(StrategicPlan plan, PlanFocus f, int value) => plan.focus[f] = value;

		private static void Raise(StrategicPlan plan, PlanFocus f, int by) => plan.focus[f] = plan.Focus(f) + by;
	}
}

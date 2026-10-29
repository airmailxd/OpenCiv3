using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.Advanced {
	// The Advanced AI: the Standard AI steered by a plan (see StrategicPlan,
	// PlanEffects), and playing with no advantage over a human: no
	// difficulty bonuses (GameData.AiBonusDifficulty), no house rules
	// (AiHouseRules) and the fog of war (AIFogOfWar).
	//
	// The plan is set every PlanEveryTurns turns, and at once when the
	// situation turns (a war begins or ends, a city is won or lost), the
	// cadence Vox Deorum found best for its LLM strategists (every 5 turns
	// beat every turn) plus its interrupts.
	public static class AdvancedAI {
		private static readonly ILogger log = Log.ForContext(typeof(AdvancedAI));

		public const int PlanEveryTurns = 5;
		public const int PlanLastsTurns = 10;

		// Early in the AI's turn, before it plots wars and picks production.
		public static void PlayTurn(Player player, GameData gameData) {
			if (!player.IsAdvancedAi || !NeedsNewPlan(player, gameData)) {
				return;
			}
			StrategicPlan plan = RulePlanner.Plan(player, gameData);
			Stamp(plan, player, gameData);
			player.strategicPlan = plan;
			log.Information("{Player} plans ({Source}): {Plan}", player, plan.source, Describe(plan));
		}

		// Marks a plan as set now, with what was seen when it was set.
		public static void Stamp(StrategicPlan plan, Player player, GameData gameData) {
			plan.setTurn = gameData.turn;
			plan.expiresTurn = gameData.turn + PlanLastsTurns;
			plan.citiesWhenSet = player.cities.Count;
			plan.enemiesWhenSet = Enemies(player, gameData);
		}

		public static bool NeedsNewPlan(Player player, GameData gameData) {
			StrategicPlan plan = player.strategicPlan;
			if (plan == null || !plan.ActiveOn(gameData.turn) || gameData.turn - plan.setTurn >= PlanEveryTurns) {
				return true;
			}
			// The situation turned.
			return player.cities.Count != plan.citiesWhenSet
				|| !Enemies(player, gameData).SequenceEqual(plan.enemiesWhenSet ?? []);
		}

		private static List<string> Enemies(Player player, GameData gameData) {
			return gameData.players.Where(p => p != player && !p.IsIndependent && !p.defeated && AtWar(player, p))
				.Select(p => p.id.ToString()).OrderBy(id => id).ToList();
		}

		public static string Describe(StrategicPlan plan) {
			string focus = string.Join(" ", plan.focus.Where(f => f.Value != StrategicPlan.NeutralFocus)
				.Select(f => $"{f.Key}={f.Value}"));
			string stances = string.Join(" ", plan.stances.Where(s => s.Value != RivalStance.Neutral)
				.Select(s => $"{s.Key}:{s.Value}"));
			return $"[{focus}] aggression{plan.aggressionShift:+0;-0;0} {stances} victory={plan.victory} — {plan.rationale}";
		}
	}
}

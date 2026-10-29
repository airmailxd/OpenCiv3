using System;
using System.Collections.Generic;

namespace C7GameData {
	// What a plan can ask the AI to build more or less of. These are Civ3's
	// own build-often/never categories (BIQ RACE flags; see
	// doc/civ3-compendium/08, section 3.2), less Explore, which the AI never
	// builds (the owner's choice).
	public enum PlanFocus {
		Offense, Defense, Artillery, Settlers, Workers, Naval, Air,
		Growth, Production, Happiness, Science, Wealth, Trade, Culture,
	}

	// How the plan treats a rival: as the one to make war on, as one to
	// leave in peace, or as war plotting would anyway.
	public enum RivalStance { Neutral, Target, Friend }

	// An Advanced AI player's strategy: the dials the classical AI reads
	// (see C7Engine.AI.Advanced.PlanEffects). A planner sets it every few
	// turns (see C7Engine.AI.Advanced.AdvancedAI). It never moves a unit
	// itself: like Vox Deorum's LLM strategist for Civ5, it steers the AI
	// that does.
	//
	// The neutral plan (every focus 50, no aggression shift, no stances)
	// leaves the AI playing exactly as the Standard AI.
	public class StrategicPlan {
		public const int NeutralFocus = 50;
		public const int MaxAggressionShift = 2;

		// 0 (avoid) to 100 (all out); 50, or missing, is neutral.
		public Dictionary<PlanFocus, int> focus = new();
		// Added to the civ's aggression level for war plotting (Civ3's
		// levels run -2 to 2).
		public int aggressionShift;
		// By rival player id.
		public Dictionary<string, RivalStance> stances = new();
		// The victory the plan works toward, for the planner's own memory;
		// one of Victories, or null.
		public string victory;

		// Who made the plan ("rules" or "model"), why, and when.
		public string source;
		public string rationale;
		// The model planner's notes to itself, carried to its next briefing
		// (RuneBench's agents kept a notes file the same way).
		public string notes;
		public int setTurn = -1;
		// The plan stops steering on this turn if no planner renews it, so
		// a planner that fails (no answer from the model, say) hands the AI
		// back to its own judgement. Vox Deorum's flavors expire after 10
		// turns for the same reason.
		public int expiresTurn = -1;

		// What the planner saw when it set the plan, to tell when something
		// has changed enough to plan again (see AdvancedAI.NeedsNewPlan).
		public int citiesWhenSet;
		public List<string> enemiesWhenSet = new();

		public static readonly string[] Victories = ["Conquest", "Domination", "Space", "Culture", "Diplomatic"];

		public int Focus(PlanFocus f) => focus.TryGetValue(f, out int v) ? v : NeutralFocus;

		public RivalStance StanceToward(ID rival) =>
			rival != null && stances.TryGetValue(rival.ToString(), out RivalStance s) ? s : RivalStance.Neutral;

		public bool ActiveOn(int turn) => setTurn >= 0 && (expiresTurn < 0 || turn < expiresTurn);

		// Keeps every value in range, so whatever a planner says can't push
		// the AI past what it was built for.
		public void Clamp() {
			foreach (PlanFocus f in new List<PlanFocus>(focus.Keys)) {
				focus[f] = Math.Clamp(focus[f], 0, 100);
			}
			aggressionShift = Math.Clamp(aggressionShift, -MaxAggressionShift, MaxAggressionShift);
			if (victory != null && Array.IndexOf(Victories, victory) < 0) {
				victory = null;
			}
		}
	}
}

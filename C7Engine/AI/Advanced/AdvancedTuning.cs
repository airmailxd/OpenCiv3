using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using C7GameData;

namespace C7Engine.AI.Advanced {
	// The Advanced AI's tunable numbers, and the on-trial AI behaviours
	// (AiExperiments switches) it plays with.
	//
	// Three layers, the later winning:
	// - each knob's default, the values training settled on (see
	//   tools/advanced-ai-training and doc/ai-roadmap.md);
	// - AI trials' overrides, C7_ADVANCED_TUNING="name=value,...", which is
	//   how training tries changes;
	// - the player's own policy (see AdvancedPolicy), which the model
	//   reviewer adjusts as the game goes (see PolicyReview).
	public static class AdvancedTuning {
		public sealed record Knob(string name, double defaultValue, double step, double min, double max, string meaning) {
			public bool IsBehaviour => step == 1 && min == 0 && max == 1;
		}

		// The on-trial behaviours an Advanced AI may take up; 1 turns one on.
		public static readonly string[] Behaviours = [
			WonderPlanner.Timing, WonderPlanner.Value, "war-production", "war-no-peace",
			"leader-hurry-best", DevelopmentAI.LeanMilitary, PlayerAI.NoAiLuxury,
		];

		public static readonly IReadOnlyList<Knob> Knobs = [
			new("FocusPoints", 15, 5, 0, 60,
				"How hard the plan's build priorities tilt city production (0 = not at all)."),
			new("TargetStrengthRatio", 1.75, .25, 1, 4,
				"How many times stronger than a neighbour (with its allies) you must be to make it a war target once land runs out."),
			new("ThreatStrengthRatio", 1.5, .25, 1, 4,
				"How many times stronger than you a neighbour must be before you raise defence and keep the peace with it."),
			new("BehindInTechShare", .9, .05, .5, 1,
				"Below this share of the average tech count of the civs you know, science and trade are raised."),
			new("ExpandSettlers", 85, 10, 50, 100,
				"Settler priority (50 neutral, 100 most) while open city sites remain."),
			new("ExpandAggression", -1, 1, -2, 2,
				"War-readiness shift while expanding (-2 most peaceful, 2 most warlike)."),
			new("BuildScience", 65, 10, 0, 100,
				"Science priority at peace with no room left and no easy war target."),
			new("BuildProduction", 65, 10, 0, 100,
				"Production priority at peace with no room left and no easy war target."),
			new("WinningOffense", 80, 10, 50, 100,
				"Offensive unit priority while winning a war."),
			new("PreyAggression", 1, 1, -2, 2,
				"War-readiness shift once a much weaker neighbour is the target."),
			new("PlanEveryTurns", 5, 2, 1, 15,
				"How often, in turns, the rules re-plan (they also re-plan at once on a war, peace or a city won or lost)."),
			new(WonderPlanner.Timing, 0, 1, 0, 1,
				"1: start great wonders only when the city can finish them in good time."),
			new(WonderPlanner.Value, 0, 1, 0, 1,
				"1: value great wonders by what they do for this civ, not culture and happiness alone."),
			new("war-production", 0, 1, 0, 1,
				"1: productive cities build the units a war campaign is short of."),
			new("war-no-peace", 0, 1, 0, 1,
				"1: while your army marches and you are not losing, refuse peace unless offered a city."),
			new("leader-hurry-best", 0, 1, 0, 1,
				"1: great leaders hurry the most valuable building in reach."),
			new(DevelopmentAI.LeanMilitary, 0, 1, 0, 1,
				"1: stop adding soldiers that cost upkeep once cities are guarded."),
			new(PlayerAI.NoAiLuxury, 0, 1, 0, 1,
				"1: don't raise the luxury rate for unhappy cities."),
		];

		private static readonly Dictionary<string, Knob> byName = Knobs.ToDictionary(k => k.name);

		public static bool IsKnob(string name) => name != null && byName.ContainsKey(name);

		public static Knob KnobNamed(string name) => byName[name];

		private static Dictionary<string, double> overrides = new();

		// The game-wide value (default or trial override).
		public static double Get(string name) =>
			overrides.TryGetValue(name, out double v) ? v : byName.TryGetValue(name, out Knob k) ? k.defaultValue : throw new ArgumentException(name);

		// The value this player plays by: its own policy's, or the game-wide.
		public static double Get(Player player, string name) =>
			player?.advancedPolicy != null && player.advancedPolicy.knobs.TryGetValue(name, out double v) ? v : Get(name);

		public static int GetInt(Player player, string name) => (int)Math.Round(Get(player, name));

		public static IEnumerable<string> BehavioursOn(Player player) => Behaviours.Where(b => Get(player, b) >= .5);

		// A value brought into the knob's range, and onto its steps for a
		// behaviour.
		public static double Clamp(string name, double value) {
			Knob k = byName[name];
			value = Math.Clamp(value, k.min, k.max);
			return k.IsBehaviour ? Math.Round(value) : value;
		}

		// "name=value,name=value"; unknown names are an error, so a typo
		// can't silently train nothing.
		public static void Override(string spec) {
			Dictionary<string, double> parsed = new();
			foreach (string part in (spec ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
				string[] kv = part.Split('=', 2);
				if (kv.Length != 2 || !byName.ContainsKey(kv[0].Trim())) {
					throw new ArgumentException($"Unknown Advanced AI tuning: {part}");
				}
				parsed[kv[0].Trim()] = double.Parse(kv[1], CultureInfo.InvariantCulture);
			}
			overrides = parsed;
		}
	}
}

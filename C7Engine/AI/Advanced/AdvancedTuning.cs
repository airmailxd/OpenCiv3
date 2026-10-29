using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace C7Engine.AI.Advanced {
	// The Advanced AI's tunable numbers, and the on-trial AI behaviours
	// (AiExperiments switches) it plays with. Their defaults are the values
	// training settled on (see tools/advanced-ai-training and
	// doc/ai-roadmap.md); AI trials can override them with
	// C7_ADVANCED_TUNING="name=value,...", which is how training tries
	// changes.
	//
	// They apply to every Advanced AI in the game alike.
	public static class AdvancedTuning {
		// The on-trial behaviours an Advanced AI may take up; a value of 1
		// turns one on.
		public static readonly string[] Behaviours = [
			WonderPlanner.Timing, WonderPlanner.Value, "war-production", "war-no-peace",
			"leader-hurry-best", DevelopmentAI.LeanMilitary, PlayerAI.NoAiLuxury,
		];

		public static readonly IReadOnlyDictionary<string, double> Defaults = new Dictionary<string, double> {
			// See PlanEffects.FocusPoints.
			["FocusPoints"] = 15,
			// See RulePlanner.
			["TargetStrengthRatio"] = 1.75,
			["ThreatStrengthRatio"] = 1.5,
			["BehindInTechShare"] = .9,
			["ExpandSettlers"] = 85,
			["ExpandAggression"] = -1,
			["BuildScience"] = 65,
			["BuildProduction"] = 65,
			["WinningOffense"] = 80,
			["PreyAggression"] = 1,
			// See AdvancedAI.
			["PlanEveryTurns"] = 5,
			// The behaviours, all off until training shows they help.
			[WonderPlanner.Timing] = 0,
			[WonderPlanner.Value] = 0,
			["war-production"] = 0,
			["war-no-peace"] = 0,
			["leader-hurry-best"] = 0,
			[DevelopmentAI.LeanMilitary] = 0,
			[PlayerAI.NoAiLuxury] = 0,
		};

		private static Dictionary<string, double> overrides = new();

		public static double Get(string name) =>
			overrides.TryGetValue(name, out double v) ? v : Defaults.TryGetValue(name, out double d) ? d : throw new ArgumentException(name);

		public static int GetInt(string name) => (int)Math.Round(Get(name));

		public static IEnumerable<string> BehavioursOn() => Behaviours.Where(b => Get(b) >= .5);

		// "name=value,name=value"; unknown names are an error, so a typo
		// can't silently train nothing.
		public static void Override(string spec) {
			Dictionary<string, double> parsed = new();
			foreach (string part in (spec ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
				string[] kv = part.Split('=', 2);
				if (kv.Length != 2 || !Defaults.ContainsKey(kv[0].Trim())) {
					throw new ArgumentException($"Unknown Advanced AI tuning: {part}");
				}
				parsed[kv[0].Trim()] = double.Parse(kv[1], CultureInfo.InvariantCulture);
			}
			overrides = parsed;
		}
	}
}

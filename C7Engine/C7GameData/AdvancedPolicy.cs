using System.Collections.Generic;

namespace C7GameData {
	// An Advanced AI's policy: the knob values and behaviours its rule
	// planner plays by (see C7Engine.AI.Advanced.AdvancedTuning), where they
	// differ from the game-wide ones. The model reviews it every
	// PolicyReview.ReviewEveryTurns turns, changing a few knobs a step or
	// two at a time and keeping what worked, rather than playing the game
	// itself.
	public class AdvancedPolicy {
		public Dictionary<string, double> knobs = new();
		public List<PolicyReviewRecord> reviews = new();
		// The model's notes to itself, carried to its next review.
		public string notes;
	}

	// One review: how the civ stood, what changed and why. The next review
	// sees how things went since.
	public class PolicyReviewRecord {
		public int turn;
		public PolicySnapshot standing;
		public Dictionary<string, double> changes = new();
		public string rationale;
		// A lesson for later games (see PolicyPlaybook).
		public string lesson;
	}

	// How the civ stood on a turn, measured against the civs it knows.
	public class PolicySnapshot {
		public int score;
		// 1 is first among the civs it has met (and itself).
		public int scoreRank;
		public int civsKnown;
		public int cities;
		public double averageRivalCities;
		public int citizens;
		public int techs;
		public double averageRivalTechs;
		// Its military strength over the strongest rival it has met.
		public double strengthVsStrongest;
		public int atWarWith;
		public string era;
	}
}

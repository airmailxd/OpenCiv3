namespace C7GameData {
	using System;
	using QueryCiv3.Biq;

	public class ExperienceLevel {
		public string key;
		public string displayName;
		public int baseHitPoints;
		public double retreatChance;
		public double promotionChance;

		public ExperienceLevel(string key, string displayName, int baseHitPoints, double retreatChance, double promotionChance) {
			this.key = key;
			this.displayName = displayName;
			this.baseHitPoints = baseHitPoints;
			this.retreatChance = retreatChance;
			this.promotionChance = promotionChance;
		}

		// The BIQ's retreat bonus for the level (50 for a regular), which the
		// game's retreat roll uses as a whole number (see
		// MapUnit.RetreatChance). Kept as a share in retreatChance, as saves
		// store it.
		public int RetreatBonus() {
			return (int)Math.Round(retreatChance * 100);
		}

		public static ExperienceLevel ImportFromCiv3(string key, EXPR expr, int levelIndex) {
			var promotionChances = new double[] { 0.5, 0.25, 0.125, 0.0 };
			return new ExperienceLevel(key, expr.Name, expr.BaseHitPoints, expr.RetreatBonus / 100.0, promotionChances[levelIndex]);
		}
	}
}

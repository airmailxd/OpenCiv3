namespace C7GameData {
	// A city's governor, which Civ3 shows as "City Governor" when the player
	// picks "Contact Governor" (labels.txt, #GOVERNOR_WIN). Its General
	// options are "Manage citizen moods" and "Emphasize Food", "Emphasize
	// Production" and "Emphasize Commerce"; the Production options
	// ("Manage Production" and what to build) aren't here yet.
	//
	// Only human players' cities use it: computer players manage their
	// cities themselves (see PlayerAI).
	public class CityGovernor {
		// Before the turn's disorder check, and whenever the city grows,
		// citizens are made entertainers so the city doesn't riot, and
		// entertainers no longer needed go back to work. See
		// CityTileAssignmentAI.GovernMoods.
		public bool manageMoods;

		// Citizens placed on tiles favour tiles with more of these. See
		// CityTileAssignmentAI.AssignNewCitizenToTile.
		public bool emphasizeFood;
		public bool emphasizeProduction;
		public bool emphasizeCommerce;

		public bool EmphasizesAnything => emphasizeFood || emphasizeProduction || emphasizeCommerce;

		public CityGovernor Copy() {
			return (CityGovernor)MemberwiseClone();
		}

		public bool SameAs(CityGovernor other) {
			return other != null && manageMoods == other.manageMoods && emphasizeFood == other.emphasizeFood
				&& emphasizeProduction == other.emphasizeProduction && emphasizeCommerce == other.emphasizeCommerce;
		}
	}

	// Which of the player's cities a governor setting is for, as Civ3 offers
	// them: "This City", "All Cities" or "Continental Cities" (the cities on
	// the same continent).
	public enum GovernorScope {
		ThisCity,
		AllCities,
		ContinentalCities,
	}
}

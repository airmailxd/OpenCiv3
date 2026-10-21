namespace C7GameData {
	// A city's governor, which Civ3 shows as "City Governor" when the player
	// picks "Contact Governor" (labels.txt, #GOVERNOR_WIN). Its General
	// options are "Manage citizen moods", "Emphasize Food", "Emphasize
	// Production", "Emphasize Commerce", "Manage Production", "Never build
	// Wonders" and "Never build Small Wonders"; its Production options say how
	// often to produce each kind of thing ("Never", "Sometimes", "Often").
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

		// The governor chooses what the city builds once it finishes
		// something, rather than the city building the same unit again (Civ3
		// FUN_00438ab0 at 0x438c49: a human's city repeats its unit unless
		// its governor flag 0x10 is set). See City.ChooseNextProduction.
		public bool manageProduction;

		// What the city builds next is never a great wonder, or a small
		// wonder (governor flags 0x20 and 0x40, Civ3 FUN_0042e430 through
		// FUN_00439a50). See GovernorProduction.
		public bool neverBuildWonders;
		public bool neverBuildSmallWonders;

		// The Production options: the kinds the city never produces, and
		// those it produces often. A kind in neither is produced
		// "Sometimes", the default. Civ3 keeps the same two bit masks per
		// city (FUN_004c9310 and FUN_004c9330, read by the governor window,
		// FUN_00546520). See GovernorProduction.
		public ProductionKind produceNever;
		public ProductionKind produceOften;

		public bool EmphasizesAnything => emphasizeFood || emphasizeProduction || emphasizeCommerce;

		public enum Frequency {
			Never,
			Sometimes,
			Often,
		}

		public Frequency HowOften(ProductionKind kind) {
			if ((produceNever & kind) != 0) {
				return Frequency.Never;
			}
			return (produceOften & kind) != 0 ? Frequency.Often : Frequency.Sometimes;
		}

		public void SetHowOften(ProductionKind kind, Frequency frequency) {
			produceNever &= ~kind;
			produceOften &= ~kind;
			if (frequency == Frequency.Never) {
				produceNever |= kind;
			} else if (frequency == Frequency.Often) {
				produceOften |= kind;
			}
		}

		public CityGovernor Copy() {
			return (CityGovernor)MemberwiseClone();
		}

		public bool SameAs(CityGovernor other) {
			return other != null && manageMoods == other.manageMoods && emphasizeFood == other.emphasizeFood
				&& emphasizeProduction == other.emphasizeProduction && emphasizeCommerce == other.emphasizeCommerce
				&& manageProduction == other.manageProduction && neverBuildWonders == other.neverBuildWonders
				&& neverBuildSmallWonders == other.neverBuildSmallWonders
				&& produceNever == other.produceNever && produceOften == other.produceOften;
		}
	}

	// The kinds of thing the governor's Production options are about, in the
	// order and with the bits Civ3 gives them (labels.txt #GOVERNOR_WIN,
	// "Produce Offensive Ground Units" to "Produce Culture"; the bits are
	// those FUN_00546520 reads into the window's rows). They are also the
	// RACE "build often" and "build never" categories.
	[System.Flags]
	public enum ProductionKind {
		None = 0,
		OffensiveGroundUnits = 0x1,
		DefensiveGroundUnits = 0x2,
		Artillery = 0x4,
		Settlers = 0x8,
		Workers = 0x10,
		NavalUnits = 0x20,
		AirUnits = 0x40,
		Growth = 0x80,
		Production = 0x100,
		Happiness = 0x200,
		Science = 0x400,
		Wealth = 0x800,
		Trade = 0x1000,
		Exploration = 0x2000,
		Culture = 0x4000,
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

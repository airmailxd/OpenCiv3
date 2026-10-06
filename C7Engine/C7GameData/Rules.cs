using System.Collections.Generic;

namespace C7GameData {
	public class Rules {
		public int MaximumResearchTime;
		public int MinimumResearchTime;
		public int ShieldValueInGold;
		public int ForestValueInShields;
		public int CitizenValueInShields;
		public int TurnPenaltyForEachHurrySacrifice;
		public int MaximumLevel1CitySize;
		public int MaximumLevel2CitySize;
		public int FoodNeededToGrowForLevel1Cities = 20;
		public int FoodNeededToGrowForLevel2Cities = 40;
		public int FoodNeededToGrowForLevel3Cities = 60;
		public float BuildingDiscountForCivTraits = .5f;
		public string StartUnitType1;
		public string StartUnitType2;
		public string ScoutUnitType;
		public int MaxRankOfWorkableTiles;
		public int MaxRankOfBarbarianCampTiles;
		public int DefaultDealDuration;
		public float TreasuryInterestRate = .05f;
		public int MaxInterest = 50;
		public int ShieldCostPerGold;
		public float ShieldRateForDisbanding; // per cent
		public bool AllowLesserUnitProduction; // for example, allow building a Spearman/Pikeman when we can build a Musketman (simultaneously)
		public int RadarTileVisibility; // how many tiles, a unit with the Radar ability, can see ahead

		// Game option: whether units can be unloaded from an army once they've
		// been loaded into it. Armies are rigid by default.
		public bool AllowUnloadFromArmy = false;

		// Game option: whether games with more than one human (hotseat or
		// LAN) show the scoreboard of players' scores and the turn clock.
		public bool ShowScoreboard = true;

		// Each army needs this many cities to support it. A civ can't build an
		// army unless (armies + 1) * CitiesNeededToSupportAnArmy <= cities.
		public int CitiesNeededToSupportAnArmy = 4;

		// The unit a city builds through a building that allows building
		// armies (the Military Academy).
		public string BuildArmyUnit;

		// The unit (a military great leader) an elite unit's victory can
		// create. Null means victories create no leaders.
		public string BattleCreatedUnit;

		// How many of each spaceship part (by Building.spaceshipPart index)
		// the spaceship needs, from the BIQ's RULE section. Conquests needs
		// one of each of its ten parts. A part missing from the list needs
		// one.
		public List<int> SpaceshipPartsRequired = new();

		public int SpaceshipPartsNeeded(int partIndex) {
			if (partIndex < 0) {
				return 0;
			}
			return partIndex < SpaceshipPartsRequired.Count ? SpaceshipPartsRequired[partIndex] : 1;
		}

		public int MinimumPopulationForWeLoveTheKing = 3;
		public int GoldenAgeDuration = 20;
		public int UpgradeCostPerShield = 3; // gold per shield of difference between a unit and its upgrade

		// The names of a civ's culture levels, lowest first, as in "Our people
		// have developed a Solid culture." These are the Civ3 defaults; games
		// imported from Civ3 take them from the BIQ.
		public List<string> CultureLevelNames = ["Fledgling", "Weak", "Fragile", "Solid", "Strong", "Glorious"];

		// How the people of a civ regard another civ's culture, from the
		// highest ratio of the other's culture to theirs to the lowest, as in
		// "The Romans are in awe of our culture."
		public List<CultureOpinion> CultureOpinions = [
			new() { Name = "in awe of", Numerator = 3, Denominator = 1 },
			new() { Name = "admirers of", Numerator = 2, Denominator = 1 },
			new() { Name = "impressed with", Numerator = 1, Denominator = 1 },
			new() { Name = "unimpressed by", Numerator = 3, Denominator = 4 },
			new() { Name = "dismissive of", Numerator = 1, Denominator = 2 },
			new() { Name = "disdainful of", Numerator = 1, Denominator = 3 },
		];
	}

	// An opinion one civ's people hold of another civ's culture when the other
	// civ has at least Numerator/Denominator times their culture.
	public class CultureOpinion {
		public string Name;
		public int Numerator;
		public int Denominator;
	}
}

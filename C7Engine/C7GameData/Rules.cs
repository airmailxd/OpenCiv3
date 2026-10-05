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

		public int MinimumPopulationForWeLoveTheKing = 3;
		public int GoldenAgeDuration = 20;
		public int UpgradeCostPerShield = 3; // gold per shield of difference between a unit and its upgrade
	}
}

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
		// The share of a unit's shield cost a city gets when the unit is
		// disbanded in it (rounded down, and nothing towards a wonder). Civ3
		// gives a quarter; it isn't in the BIQ. See
		// https://civfanatics.com/civ3/faq/ ("The number of shields added is
		// 1/4 the shield cost of the unit, rounded down") and
		// https://forums.civfanatics.com/threads/disbanding-units-for-shields.211405/
		public float ShieldRateForDisbanding = .25f;
		public bool AllowLesserUnitProduction; // for example, allow building a Spearman/Pikeman when we can build a Musketman (simultaneously)
		public int RadarTileVisibility; // how many tiles, a unit with the Radar ability, can see ahead

		// Game option: whether units can be unloaded from an army once they've
		// been loaded into it. Armies are rigid by default.
		public bool AllowUnloadFromArmy = false;

		// Game option: whether games with more than one human (hotseat or
		// LAN) show the scoreboard of players' scores and the turn clock.
		public bool ShowScoreboard = true;

		// Game option, not in Civ3: the capital and the cities nearest it
		// (the first CitiesFreeOfCorruption by rank) have no corruption or
		// waste. Off by default, so saves and Civ3 games play as before.
		public bool CoreCitiesFreeOfCorruption = false;
		public int CitiesFreeOfCorruption = 5;

		// Game option from Civ3 (Play the World): the food, shields and
		// commerce cities generate each turn are doubled, speeding up growth,
		// research and production. Off by default.
		public bool AcceleratedProduction = false;

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

		// The gold each civ starts a new game with.
		public int StartingTreasury = 10;

		// The civilopedia name of the first era, which civs start a new game
		// in.
		public string FirstEraCivilopediaName = "ERAS_Ancient_Times";

		// The food each citizen eats a turn.
		public int FoodConsumptionPerCitizen = 2;

		// How many road moves a unit can make for one movement point. Games
		// imported from Civ3 build the road's movement cost from it (see
		// SaveTerrainImprovement.Civ3Improvements).
		public int MovementAlongRoads = 3;

		// The defense bonus of a fortress, in per cent. Games imported from
		// Civ3 build the fortress's defense bonus from it.
		public int FortressDefensiveBonus = 50;

		// The following come from the BIQ's RULE section, but nothing uses
		// them yet. The defaults are only for saves made before they were
		// imported.

		// How fast borders grow with culture, and the base for the culture
		// thresholds at which they do.
		public int BorderExpansionMultiplier = 3;
		public int BorderFactor = 2;
		// The cost of each future tech.
		public int FutureTechCost;
		// The chance, in per cent, that a city in disorder riots.
		public int ChanceOfRioting = 20;
		// How many unhappy citizens each happy face from luxuries or
		// buildings makes content.
		public int CitizensAffectedByEachHappyFace = 2;
		// City defense bonuses, in per cent, from buildings and from
		// citizens.
		public int BuildingDefensiveBonus = 100;
		public int CitizenDefensiveBonus = 25;
		// The turns of unhappiness each drafted citizen causes.
		public int TurnPenaltyForEachDraftedCitizen = 20;
		// The unit a unit that enslaves its defeated foe makes (Civ3's
		// Slave), and the unit of capture-the-flag games.
		public string SlaveUnitType;
		public string FlagUnitType;

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

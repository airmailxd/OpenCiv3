using System.Collections.Generic;
using System.Linq;

namespace C7GameData.Save {
	public class SaveBuilding {
		public enum Flag {
			IsCenterOfEmpire,
			VeteranGroundUnits,
			VeteranSeaUnits,
			MustBeCoastal,
			MustBeNearRiver,
			IncreasesLuxuryTrade,
			ReducesCorruption,
			ForbiddenPalace,
			IncreasesShieldsInWater,
			IncreasesFoodInWater,
			IncreasesTradeInWater,
			// +1 commerce on every tile in the city, land or water, that
			// already produces commerce (the Colossus).
			IncreasedTrade,
			AllowsCitySize2,
			AllowsCitySize3,
			DoublesCityGrowthRate,
			ProvidesWalls,
			CanOnlyBeBuiltInTowns,
			// A city keeps only one building with this flag: building one
			// replaces any other (the power plants).
			ReplacesOtherBuildings,
			TreasuryEarnsInterest,
			AllowsBuildArmy,
			AllowsLargerArmies,
			RequiresVictoriousArmy,
			Plus50PercentResearch,
			// +100% research in the city (like Copernicus' Observatory).
			DoublesResearchOutput,
			Plus50PercentLuxury,
			// Increases both tax and luxury output.
			Plus50PercentCommerce,
			AllowsEnemyTerritoryHealing,
			// Halves war weariness in the city.
			ReducesWarWeariness,
			// Halves war weariness in every city of the owner.
			ReducesWarWearinessEverywhere,
			// Pays the upkeep of the owner's commercial buildings (like Smith's
			// Trading Company).
			PaysTradeMaintenance,
			// The building's content faces in all cities only reach cities on
			// the same continent (like JS Bach's Cathedral).
			ContinentalMoodEffects,
			// Great wonder effects.
			SafeSeaTravel,
			GainAnyTechKnownByTwoCivs,
			DoubleCombatVsBarbarians,
			IncreasedShipMovement,
			PlusTwoShipMovement,
			CheaperUpgrades,
			TwoFreeAdvances,
			AllowDiplomaticVictory,
			AllowsNuclearWeapons,
			// Civ3's great wonder "double city growth" flag (Longevity), as
			// opposed to the granary-style DoublesCityGrowthRate.
			DoublesCityGrowthEverywhere,
			// Conquests: old wonders earn gold from tourists.
			TouristAttraction,
			// Small wonder effects.
			IncreasesLeaderChance,
			IncreasedArmyValue,
			DecreasesMissileSuccess,
			AllowsSpyMissions,
			BuildSpaceshipParts,
			// The required resources must be on tiles in the city's radius,
			// not merely connected to it by the trade network.
			GoodsMustBeInCityRadius,
		}

		public class GreatWonderProperties {
			// The name of the building this building gives to every city in the
			// empire on on the continent (like the pyramids or the internet).
			public string buildingGainedInEveryCity;
			public string buildingGainedInEveryCityOnContinent;
		}

		public string name;
		public int shieldCost;
		public int populationCost;
		public ID requiredTech;
		public string requiredBuilding;
		public GreatWonderProperties? greatWonderProperties;
		public bool isSmallWonder;
		public int culturePerTurn;
		public int contentFacesInCity;
		public double combatDefenseBonus;
		public int maintenanceCost;
		// The percentage by which the building increases the city's useful
		// shield production.
		public int productionBonusPercent;
		public int iconRowIndex;
		public ID? renderedObsoleteBy;

		// How many armies a civ needs in the field before it can build this
		// (like the Pentagon). Zero if there's no such requirement.
		public int numberOfArmiesRequired;

		// How many of requiredBuilding the civ needs across its cities before
		// it can build this (like Wall Street's five stock exchanges). Zero or
		// one means only the building city needs it.
		public int requiredBuildingCount;

		// The government the civ must have to build this (like the Secret
		// Police HQ under Communism), or null if any government will do.
		public ID? requiredGovernment;

		// Content faces (negative: unhappy faces) this building adds in every
		// city of its owner, or every city on its continent with
		// ContinentalMoodEffects (like the Hanging Gardens).
		public int contentFacesAllCities;

		// The building whose content faces this one doubles in every city
		// (like the Oracle doubling temples).
		public string doublesHappinessOf;

		// Pollution the building adds to its city.
		public int pollution;

		// For a spaceship part, its index in Rules.SpaceshipPartsRequired
		// (Civ3's BLDG "spaceship part" field). Null for ordinary buildings.
		public int? spaceshipPart;

		// A unit this building gives its owner every unitFrequency turns (like
		// the Statue of Zeus).
		public string unitProduced;
		public int unitFrequency;

		// Assorted boolean flags for the building. They're stored in this set
		// rather than as booleans to avoid bloating the json file.
		public HashSet<Flag> flags = new();

		// The set of traits this building has. Civilizations with a matching
		// trait get discounted production costs.
		public HashSet<Civilization.Trait> traits = new();

		public HashSet<string> requiredResources = [];

		// Paths to Lua functions 
		public SortedSet<string> onFinishedUnitProduction = [];
		public SortedSet<string> productionPrerequisites = [];
		public SortedSet<string> tileModifiers = [];

		public SaveBuilding() { }
	}
}

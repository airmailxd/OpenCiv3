using System;
using C7GameData;
using C7GameData.Save;

namespace C7Engine.AI {
	// How a city governor's Production options steer what the city builds.
	//
	// Civ3 chooses a city's production with one function for computer and
	// human cities alike (FUN_0042e430). It scores every option, and then
	// applies the city's two masks of production kinds (the governor's
	// "Never" and "Often", FUN_004c9310 and FUN_004c9330):
	// - an option whose kinds are all set to "Never" can't be chosen; Civ3
	//   gives it a score of INT_MIN. This only applies when some kind is set
	//   to "Never", and an option of no kind at all is then ruled out too, as
	//   its (empty) set of kinds is all "Never";
	// - an option of any kind set to "Often" scores twice as much, or 5/4 as
	//   much while its owner has fewer than 3 cities (the count is the
	//   owner's +0x194, the number the game raises when a city is won);
	// - "Never build Wonders" and "Never build Small Wonders" skip the great
	//   and small wonders (FUN_00439a50 with flags 0x20 and 0x40).
	// Each unit and improvement is put into kinds by its rules: units by
	// their PRTO AI strategies (FUN_004453e0), improvements by their effects
	// (FUN_004450d0). For "Often" some get more kinds than for "Never" (the
	// second argument of those functions): a Settler counts as Growth for
	// "Often", but "Never" Growth doesn't stop it.
	//
	// Here the options are scored by ChooseProducible, the AI's own scoring,
	// not Civ3's. A computer player's cities have no governor options, so
	// their choices don't change.
	public static class GovernorProduction {
		// Whether the governor never lets the city build this.
		public static bool Excludes(CityGovernor governor, IProducible option) {
			if (governor == null) {
				return false;
			}
			if (option is Building building) {
				if (governor.neverBuildWonders && building.IsGreatWonder()) {
					return true;
				}
				if (governor.neverBuildSmallWonders && building.isSmallWonder) {
					return true;
				}
			}
			if (governor.produceNever == ProductionKind.None) {
				return false;
			}
			ProductionKind kinds = KindsOf(option, often: false);
			return (governor.produceNever & kinds) == kinds;
		}

		// Whether the governor wants this built often.
		public static bool Favours(CityGovernor governor, IProducible option) {
			return governor != null && governor.produceOften != ProductionKind.None
				&& (governor.produceOften & KindsOf(option, often: true)) != 0;
		}

		// The option's score with the governor's "Often" taken into account.
		// Civ3's scores are positive; ours can be negative, so a favoured
		// option with a negative score has it divided instead, which raises
		// it just as multiplying raises a positive one.
		public static float AdjustScore(CityGovernor governor, Player player, IProducible option, float score) {
			if (!Favours(governor, option) || float.IsInfinity(score) || score <= int.MinValue) {
				return score;
			}
			float factor = player.cities.Count < 3 ? 1.25f : 2f;
			return score >= 0 ? score * factor : score / factor;
		}

		// The kinds of production this is, as Civ3 sorts them. With often,
		// those for the governor's "Often"; otherwise those for its "Never".
		public static ProductionKind KindsOf(IProducible option, bool often) {
			return option switch {
				UnitPrototype unit => KindsOf(unit, often),
				Building building => KindsOf(building, often, 0),
				// Civ3's Wealth is an improvement with the "Capitalization"
				// flag, which FUN_004450d0 counts as Wealth.
				Inflow => ProductionKind.Wealth,
				_ => ProductionKind.None,
			};
		}

		// Civ3 FUN_004453e0, from the unit's PRTO AI strategies.
		public static ProductionKind KindsOf(UnitPrototype unit, bool often) {
			ProductionKind kinds = ProductionKind.None;
			void Add(SaveUnitPrototype.Flag strategy, ProductionKind kind, ProductionKind alsoOften = ProductionKind.None) {
				if (unit.flags.Contains(strategy)) {
					kinds |= kind;
					if (often) {
						kinds |= alsoOften;
					}
				}
			}
			Add(SaveUnitPrototype.Flag.AIOffense, ProductionKind.OffensiveGroundUnits, ProductionKind.Exploration);
			Add(SaveUnitPrototype.Flag.AIDefense, ProductionKind.DefensiveGroundUnits, ProductionKind.Exploration);
			Add(SaveUnitPrototype.Flag.AIArtillery, ProductionKind.Artillery);
			Add(SaveUnitPrototype.Flag.AIExplore, ProductionKind.Exploration);
			Add(SaveUnitPrototype.Flag.AIArmy, ProductionKind.OffensiveGroundUnits);
			Add(SaveUnitPrototype.Flag.AICruiseMissile, ProductionKind.Artillery);
			Add(SaveUnitPrototype.Flag.AIAirBombard, ProductionKind.AirUnits);
			Add(SaveUnitPrototype.Flag.AIAirDefense, ProductionKind.AirUnits);
			Add(SaveUnitPrototype.Flag.AINavalPower, ProductionKind.NavalUnits, ProductionKind.Exploration);
			Add(SaveUnitPrototype.Flag.AIAirTransport, ProductionKind.AirUnits);
			Add(SaveUnitPrototype.Flag.AINavalTransport, ProductionKind.NavalUnits, ProductionKind.Exploration);
			Add(SaveUnitPrototype.Flag.AINavalCarrier, ProductionKind.NavalUnits, ProductionKind.AirUnits);
			Add(SaveUnitPrototype.Flag.AITerraform, ProductionKind.Workers,
				ProductionKind.Growth | ProductionKind.Production | ProductionKind.Science | ProductionKind.Wealth | ProductionKind.Trade);
			Add(SaveUnitPrototype.Flag.AISettle, ProductionKind.Settlers, ProductionKind.Growth);
			Add(SaveUnitPrototype.Flag.AITacticalNuke, ProductionKind.Artillery);
			Add(SaveUnitPrototype.Flag.AIICBM, ProductionKind.Artillery);
			Add(SaveUnitPrototype.Flag.AINavalMissileTransport, ProductionKind.NavalUnits, ProductionKind.Artillery);
			return kinds;
		}

		// Civ3 FUN_004450d0, from the improvement's effects. A wonder that
		// gives another improvement in every city is also of that
		// improvement's kinds.
		public static ProductionKind KindsOf(Building building, bool often) {
			return KindsOf(building, often, 0);
		}

		private static ProductionKind KindsOf(Building building, bool often, int depth) {
			const ProductionKind Military = ProductionKind.OffensiveGroundUnits | ProductionKind.DefensiveGroundUnits;
			ProductionKind kinds = ProductionKind.None;
			void Add(bool has, ProductionKind kind, ProductionKind alsoOften = ProductionKind.None) {
				if (has) {
					kinds |= kind;
					if (often) {
						kinds |= alsoOften;
					}
				}
			}

			if (building.greatWonderProperties != null && depth < 4) {
				if (building.greatWonderProperties.buildingGainedInEveryCity is Building everyCity) {
					kinds |= KindsOf(everyCity, often, depth + 1);
				}
				if (building.greatWonderProperties.buildingGainedInEveryCityOnContinent is Building continent) {
					kinds |= KindsOf(continent, often, depth + 1);
				}
			}

			// The BIQ's improvement flags.
			Add(building.providesVeteranGroundUnits, Military);
			Add(building.increasesResearch, ProductionKind.Science);
			Add(building.increasesLuxury, ProductionKind.Happiness);
			Add(building.increasesTax, ProductionKind.Wealth);
			Add(building.removesPopulationPollution, ProductionKind.Growth);
			Add(building.reducesBuildingPollution,
				ProductionKind.Growth | ProductionKind.Production | ProductionKind.Science | ProductionKind.Wealth);
			Add(building.HasFlag(SaveBuilding.Flag.ResistantToBribery), ProductionKind.DefensiveGroundUnits);
			Add(building.reducesCorruption, ProductionKind.Production | ProductionKind.Science | ProductionKind.Wealth);
			Add(building.doublesCityGrowthRate, ProductionKind.Growth, ProductionKind.Settlers | ProductionKind.Workers);
			Add(building.increasesLuxuryTrade, ProductionKind.Trade | ProductionKind.Happiness);
			Add(building.allowsCitySize2, ProductionKind.Growth);
			Add(building.allowsCitySize3, ProductionKind.Growth);
			Add(building.providesVeteranSeaUnits, ProductionKind.NavalUnits);
			Add(building.providesVeteranAirUnits, ProductionKind.AirUnits);
			Add(building.allowsWaterTrade, ProductionKind.Trade);
			Add(building.allowsAirTrade, ProductionKind.Trade);
			Add(building.reducesWarWeariness, ProductionKind.Happiness);
			Add(building.HasFlag(SaveBuilding.Flag.IncreasesShieldsInWater), ProductionKind.Production);
			Add(building.HasFlag(SaveBuilding.Flag.IncreasesFoodInWater), ProductionKind.Growth);
			Add(building.HasFlag(SaveBuilding.Flag.IncreasesTradeInWater),
				ProductionKind.Happiness | ProductionKind.Science | ProductionKind.Wealth);

			// The BIQ's great wonder flags.
			Add(building.safeSeaTravel, ProductionKind.NavalUnits | ProductionKind.Trade | ProductionKind.Exploration);
			Add(building.gainAnyTechKnownByTwoCivs, ProductionKind.Science);
			Add(building.doubleCombatVsBarbarians, Military);
			Add(building.increasedShipMovement || building.plusTwoShipMovement, ProductionKind.NavalUnits | ProductionKind.Exploration);
			Add(building.doublesResearch, ProductionKind.Science);
			Add(building.HasFlag(SaveBuilding.Flag.IncreasedTrade), ProductionKind.Science | ProductionKind.Wealth);
			Add(building.cheaperUpgrades, Military | ProductionKind.Artillery | ProductionKind.NavalUnits | ProductionKind.AirUnits);
			Add(building.paysTradeMaintenance, ProductionKind.Trade | ProductionKind.Wealth);
			Add(building.allowsNuclearWeapons, ProductionKind.Artillery);
			Add(building.doublesCityGrowthEverywhere, ProductionKind.Growth, ProductionKind.Settlers | ProductionKind.Workers);
			Add(building.twoFreeAdvances, ProductionKind.Science);
			Add(building.reducesWarWearinessEverywhere, ProductionKind.Happiness);
			Add(building.HasFlag(SaveBuilding.Flag.DoubleCityDefenses), ProductionKind.DefensiveGroundUnits);
			Add(building.allowDiplomaticVictory && often, ProductionKind.Trade);

			// The BIQ's small wonder flags.
			Add(building.increasesLeaderChance, ProductionKind.OffensiveGroundUnits);
			Add(building.allowsBuildArmy, ProductionKind.OffensiveGroundUnits);
			Add(building.allowsLargerArmies, ProductionKind.OffensiveGroundUnits);
			Add(building.treasuryEarnsInterest, ProductionKind.Wealth);
			Add(building.buildSpaceshipParts, ProductionKind.Exploration);
			Add(building.isForbiddenPalace, ProductionKind.Production | ProductionKind.Science | ProductionKind.Wealth);
			Add(building.decreasesMissileSuccess, ProductionKind.DefensiveGroundUnits);
			Add(building.allowsSpyMissions && often, Military);
			Add(building.allowsEnemyTerritoryHealing, Military | ProductionKind.NavalUnits);

			// Its other values.
			Add(building.IsSpaceshipPart && often, ProductionKind.Growth | ProductionKind.Science | ProductionKind.Exploration);
			Add(building.bombardDefense > 0 || building.combatDefenseBonus != null, ProductionKind.DefensiveGroundUnits);
			Add(building.navalPower > 0, ProductionKind.DefensiveGroundUnits | ProductionKind.NavalUnits);
			Add(building.airPower > 0, ProductionKind.DefensiveGroundUnits | ProductionKind.AirUnits);
			Add(building.culturePerTurn > 0, ProductionKind.Culture);
			Add(building.productionBonusPercent > 0, ProductionKind.Production);
			Add(GivesContentFaces(building), ProductionKind.Happiness);
			Add(building.doublesHappinessOf is Building doubled && GivesContentFaces(doubled), ProductionKind.Happiness);
			return kinds;
		}

		private static bool GivesContentFaces(Building building) {
			return building.contentFacesInCity > 0 || building.contentFacesAllCities > 0;
		}
	}
}

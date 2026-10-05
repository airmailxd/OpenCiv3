using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData.Save;
using C7Engine;
using C7Engine.Lua;

namespace C7GameData {
	public class Building : IProducible {
		public class GreatWonderProperties {
			// The building this building gives to every city in the empire on
			// on the continent (like the pyramids or the internet), if any.
			public Building buildingGainedInEveryCity;
			public Building buildingGainedInEveryCityOnContinent;
		}

		public string name { get; set; }
		public int shieldCost { get; set; }
		public int populationCost { get; set; } // Will always be equal to 0 in the Civ3 rule set

		// Filled in in SaveGame::ConvertBuildings
		public Tech requiredTech { get; set; }
		public Tech? renderedObsoleteBy;

		public Building requiredBuilding;

		List<Func<City, bool>> productionPrerequisites = [];
		public Action<MapUnit> onFinishedUnitProduction;
		public Action<Tile.Yield> tileModifier;

		// Filled in in SaveGame::ConvertBuildings. Non-null for great wonders.
		public GreatWonderProperties? greatWonderProperties;

		public bool isSmallWonder;
		public bool isCenterOfEmpire;
		public bool increasesLuxuryTrade;
		public bool reducesCorruption;
		public bool isForbiddenPalace;
		public bool allowsCitySize2;
		public bool allowsCitySize3;
		public bool doublesCityGrowthRate;
		public bool providesWalls;
		public bool onlyUsefulInTowns;
		public StrengthBonus? combatDefenseBonus;
		public bool providesVeteranGroundUnits;
		public bool providesVeteranSeaUnits;
		public bool allowsEnemyTerritoryHealing;
		public bool reducesWarWeariness;
		public bool reducesWarWearinessEverywhere;
		public bool treasuryEarnsInterest;
		public bool increasesResearch;
		public bool increasesLuxury;
		public bool increasesTax;
		public int productionBonusPercent = 0;

		// Army buildings: the Military Academy lets its city build armies, and
		// the Pentagon lets the owner's armies carry one more unit.
		public bool allowsBuildArmy;
		public bool allowsLargerArmies;
		public bool requiresVictoriousArmy;
		public int numberOfArmiesRequired;

		public int culturePerTurn = 0;
		public int maintenanceCost = 0;

		// The number of unhappy faces that become content in the city with this
		// building.
		public int contentFacesInCity = 0;

		// The number of happy faces that become content in the city with this
		// building. Note that this is less powerful than other sources of
		// unhappiness, like drafting or poprushing, which converts happy faces
		// to sad faces.
		public int unhappyFacesInCity = 0;

		public HashSet<Resource> requiredResources { get; set; } = [];

		public int iconRowIndex = 0;

		SaveBuilding dataSource;

		public Building(SaveBuilding building, GameData gameData) {
			dataSource = building;

			name = building.name;
			shieldCost = building.shieldCost;
			populationCost = building.populationCost;
			isSmallWonder = building.isSmallWonder;
			culturePerTurn = building.culturePerTurn;
			maintenanceCost = building.maintenanceCost;
			iconRowIndex = building.iconRowIndex;

			if (building.contentFacesInCity < 0) {
				unhappyFacesInCity = -building.contentFacesInCity;
			} else {
				contentFacesInCity = building.contentFacesInCity;
			}

			if (building.combatDefenseBonus > 0) {
				combatDefenseBonus = new(name, building.combatDefenseBonus);
			}

			isCenterOfEmpire = building.flags.Contains(SaveBuilding.Flag.IsCenterOfEmpire);
			increasesLuxuryTrade = building.flags.Contains(SaveBuilding.Flag.IncreasesLuxuryTrade);
			reducesCorruption = building.flags.Contains(SaveBuilding.Flag.ReducesCorruption);
			isForbiddenPalace = building.flags.Contains(SaveBuilding.Flag.ForbiddenPalace);
			allowsCitySize2 = building.flags.Contains(SaveBuilding.Flag.AllowsCitySize2);
			allowsCitySize3 = building.flags.Contains(SaveBuilding.Flag.AllowsCitySize3);
			doublesCityGrowthRate = building.flags.Contains(SaveBuilding.Flag.DoublesCityGrowthRate);
			providesWalls = building.flags.Contains(SaveBuilding.Flag.ProvidesWalls);
			onlyUsefulInTowns = building.flags.Contains(SaveBuilding.Flag.CanOnlyBeBuiltInTowns);
			providesVeteranGroundUnits = building.flags.Contains(SaveBuilding.Flag.VeteranGroundUnits);
			providesVeteranSeaUnits = building.flags.Contains(SaveBuilding.Flag.VeteranSeaUnits);
			allowsEnemyTerritoryHealing = building.flags.Contains(SaveBuilding.Flag.AllowsEnemyTerritoryHealing);
			reducesWarWeariness = building.flags.Contains(SaveBuilding.Flag.ReducesWarWeariness);
			reducesWarWearinessEverywhere = building.flags.Contains(SaveBuilding.Flag.ReducesWarWearinessEverywhere);
			treasuryEarnsInterest = building.flags.Contains(SaveBuilding.Flag.TreasuryEarnsInterest);
			allowsBuildArmy = building.flags.Contains(SaveBuilding.Flag.AllowsBuildArmy);
			allowsLargerArmies = building.flags.Contains(SaveBuilding.Flag.AllowsLargerArmies);
			requiresVictoriousArmy = building.flags.Contains(SaveBuilding.Flag.RequiresVictoriousArmy);
			numberOfArmiesRequired = building.numberOfArmiesRequired;
			increasesResearch = building.flags.Contains(SaveBuilding.Flag.Plus50PercentResearch);
			increasesLuxury = building.flags.Contains(SaveBuilding.Flag.Plus50PercentLuxury)
				|| building.flags.Contains(SaveBuilding.Flag.Plus50PercentCommerce);
			increasesTax = building.flags.Contains(SaveBuilding.Flag.Plus50PercentCommerce);
			productionBonusPercent = building.productionBonusPercent;

			if (building.greatWonderProperties != null) {
				greatWonderProperties = new();
			}

			LoadLuaFunctions(gameData);
		}

		[LuaMethod]
		public bool IsGreatWonder() {
			return this.greatWonderProperties != null;
		}

		// Facts about a city and its owner that the CanProduce check of every
		// building needs. Listing a city's production options gathers them
		// once instead of rescanning the empire for each building. Only valid
		// while nothing changes, i.e. for one pass over the buildings.
		internal sealed class ProductionContext {
			private readonly City city;

			// The buildings the city has, including ones granted by wonders.
			internal readonly HashSet<Building> cityBuildings = new();

			// The rest are filled in when first needed.
			private HashSet<string> namesBeingProducedByEmpire;
			private HashSet<Building> builtByEmpire;
			private HashSet<IProducible> producedByOtherCities;
			private int armyCount = -1;

			internal ProductionContext(City city) {
				this.city = city;
				foreach (CityBuilding cb in city.EffectiveBuildings()) {
					cityBuildings.Add(cb.building);
				}
			}

			// Whether any of the owner's cities, including this one, is
			// producing something with the given name.
			internal bool EmpireIsProducing(string name) {
				if (namesBeingProducedByEmpire == null) {
					namesBeingProducedByEmpire = new();
					foreach (City c in city.owner.cities) {
						if (c.itemBeingProduced != null) {
							namesBeingProducedByEmpire.Add(c.itemBeingProduced.name);
						}
					}
				}
				return namesBeingProducedByEmpire.Contains(name);
			}

			// Whether any of the owner's cities has built the building.
			internal bool EmpireHasBuilt(Building building) {
				if (builtByEmpire == null) {
					builtByEmpire = new(ReferenceEqualityComparer.Instance);
					foreach (City c in city.owner.cities) {
						foreach (CityBuilding cb in c.constructed_buildings) {
							builtByEmpire.Add(cb.building);
						}
					}
				}
				return builtByEmpire.Contains(building);
			}

			// Whether another of the owner's cities is producing this exact
			// item.
			internal bool OtherCityIsProducing(IProducible producible) {
				if (producedByOtherCities == null) {
					producedByOtherCities = new(ReferenceEqualityComparer.Instance);
					foreach (City c in city.owner.cities) {
						if (c != city && c.itemBeingProduced != null) {
							producedByOtherCities.Add(c.itemBeingProduced);
						}
					}
				}
				return producedByOtherCities.Contains(producible);
			}

			internal int ArmyCount() {
				if (armyCount < 0) {
					armyCount = city.owner.ArmyCount();
				}
				return armyCount;
			}
		}

		public bool CanProduce(City city, HashSet<Resource> accessibleResources) {
			return CanProduce(city, accessibleResources, null);
		}

		// The context, when given, must have been made for this city since
		// the last change to the game.
		internal bool CanProduce(City city, HashSet<Resource> accessibleResources, ProductionContext context) {
			if (!city.owner.HasRequiredTechnology(this)) {
				return false;
			}

			if (renderedObsoleteBy != null && city.owner.knownTechs.Contains(renderedObsoleteBy.id)) {
				return false;
			}

			if (context != null ? context.cityBuildings.Contains(this) : city.HasEffectiveBuilding(this)) {
				return false;
			}

			if (greatWonderProperties != null) {
				// We can't build a great wonder if it was already built.
				if (EngineStorage.gameData.GreatWondersBuilt.Contains(name)) {
					return false;
				}

				// We can't build a great wonder if another one of our cities is
				// building it.
				if (context != null) {
					if (context.EmpireIsProducing(name)) {
						return false;
					}
				} else {
					foreach (City c in city.owner.cities) {
						if (c.itemBeingProduced != null && c.itemBeingProduced.name == name) {
							return false;
						}
					}
				}
			}

			// TODO: Add logic for wonders and the palace. Small wonders are
			// only buildable once their effects are implemented, which so far
			// is just the army ones.
			if (isCenterOfEmpire || (isSmallWonder && !IsSupportedSmallWonder())) {
				return false;
			}

			if (isSmallWonder) {
				// A civ builds each small wonder once, in one city at a time.
				if (context != null) {
					if (context.EmpireHasBuilt(this) || context.OtherCityIsProducing(this)) {
						return false;
					}
				} else {
					foreach (City c in city.owner.cities) {
						if (c.constructed_buildings.Exists(cb => cb.building == this)) {
							return false;
						}
						if (c != city && c.itemBeingProduced == this) {
							return false;
						}
					}
				}
			}

			if (requiresVictoriousArmy && !city.owner.hasVictoriousArmy) {
				return false;
			}

			if (numberOfArmiesRequired > 0
				&& (context != null ? context.ArmyCount() : city.owner.ArmyCount()) < numberOfArmiesRequired) {
				return false;
			}

			if (requiredBuilding != null &&
				!(context != null ? context.cityBuildings.Contains(requiredBuilding) : city.HasEffectiveBuilding(requiredBuilding))) {
				return false;
			}

			foreach (Resource resource in requiredResources) {
				if (!accessibleResources.Contains(resource)) {
					return false;
				}
			}

			foreach (Func<City, bool> prerequisite in productionPrerequisites) {
				if (!prerequisite(city)) {
					return false;
				}
			}

			return true;
		}

		private bool IsSupportedSmallWonder() {
			return allowsBuildArmy || allowsLargerArmies;
		}

		// The civilization strengths this building is associated with.
		public IReadOnlySet<Civilization.Trait> traits => dataSource.traits;

		public int ShieldCost(HashSet<Civilization.Trait> civTraits, float costFactor) {
			foreach (Civilization.Trait trait in dataSource.traits) {
				if (civTraits.Contains(trait)) {
					return (int)(shieldCost * EngineStorage.gameData.rules.BuildingDiscountForCivTraits * costFactor);
				}
			}
			return (int)(shieldCost * costFactor);
		}

		public bool isGreatWonderObsolete(Player owner) {
			if (greatWonderProperties == null) {
				return false;
			}
			if (renderedObsoleteBy == null) {
				return false;
			}
			return owner.knownTechs.Contains(renderedObsoleteBy.id);
		}

		public SaveBuilding ToSaveBuilding() {
			return dataSource;
		}

		public override string ToString() {
			return name;
		}

		private void LoadLuaFunctions(GameData gameData) {
			BehaviorEngine luaEngine = gameData.luaBehaviorEngine;

			foreach (var path in dataSource.productionPrerequisites) {
				var rule = luaEngine.ImportFunc<Func<City, bool>>(path);
				productionPrerequisites.Add(rule);
			}

			foreach (var path in dataSource.onFinishedUnitProduction) {
				var handler = luaEngine.ImportFunc<Action<MapUnit>>(path);
				onFinishedUnitProduction += handler;
			}

			foreach (var path in dataSource.tileModifiers) {
				var modifier = luaEngine.ImportFunc<Action<Tile.Yield>>(path);
				tileModifier += modifier;
			}
		}
	}
}

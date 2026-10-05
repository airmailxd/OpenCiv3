using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serilog;
using C7Engine;
using MoonSharp.Interpreter;

namespace C7GameData {
	public class CityBuilding {
		public Building building;
		public Player builtByPlayer;
		public int year;
		public int totalCulture; // This represents the total culture produced by the building.
								 // In Civ3, this value is displayed in the cultural advisor tab
	}

	public struct CommerceBreakdown {
		public int corrupted;
		public int taxes;
		public int beakers;
		public int happiness;
		public int wealth;
	}

	public struct CorruptableValue {
		public CorruptableValue(int value, float corruption) {
			// Apply the corruption amount, ensuring that there is always at
			// least one shield/commerce if we started with at least one.
			useful = (int)Math.Round(value * (1 - corruption));
			if (value > 0) {
				useful = Math.Max(1, useful);
			}

			// Whatever is left over is corrupt.
			corrupt = value - useful;
		}

		public int useful;
		public int corrupt;
	}

	public class City {
		private static ILogger log = Log.ForContext<City>();

		public ID id { get; set; }
		public Tile location { get; internal set; }
		public string name;
		public Dictionary<Player, int> perPlayerCulture = new();

		//Temporary production code because production is fun.
		public IProducible itemBeingProduced;
		public int shieldsStored { get; private set; } = 0;

		public int foodStored = 0;

		public bool capital = false;
		public Player owner { get; set; }
		public List<CityResident> residents = new List<CityResident>();

		// The list of buildings built in this city. You probably want to use
		// GetBuildings, which can also include buildings granted by wonders.
		public List<CityBuilding> constructed_buildings;

		// The order of this city within all the cities of a player for the
		// purposes of rank corruption calculations.
		//
		// This is updated each turn to avoid each city needing to do an O(n)
		// scan of the list, which would cause an O(n^2) overall calcuation.
		public int rankIndex = -1;

		// The amount of corruption, between 0 and 1.
		public float corruption = 0;

		// The number of turns of unhappiness this city will experience due to
		// pop rushing. Larger values result in larger numbers of citizens being
		// unhappy as well, in addition to the time penalty.
		public int turnsOfUnhappinessDueToPopRushing = 0;

		public bool isInCivilDisorder = false;

		// Whether the city is celebrating "We Love the King Day".
		public bool celebrating = false;

		public static City NONE = new City(Tile.NONE, null, "Dummy City", ID.None("city"));

		public static bool IsValidCity(City city) {
			return city != null && city != City.NONE;
		}

		public City(Tile location, Player owner, string name, ID id) {
			this.id = id;
			this.location = location;
			this.owner = owner;
			this.name = name;
			if (owner != null) {
				this.perPlayerCulture.Add(owner, 0);
			}
			constructed_buildings = new();
		}

		internal City() {
			constructed_buildings = new();
		}

		public void SetItemBeingProduced(IProducible producible) {
			this.itemBeingProduced = producible;
		}

		// Sets what the city builds, on the player's order. Starting an army
		// the empire can't support yet is allowed, but the player is warned
		// that it won't be finished until there are enough cities.
		public void ChooseProduction(IProducible producible) {
			SetItemBeingProduced(producible);
			if (IsUnsupportedArmy(producible)) {
				WarnAboutUnsupportedArmy($"{name} has started on an army, but our empire is too small to support another one. "
					+ $"It won't be finished until we have {owner.rules.CitiesNeededToSupportAnArmy} cities for each army.");
			}
		}

		// Whether the item is an army that the owner doesn't have enough
		// cities to support.
		private bool IsUnsupportedArmy(IProducible producible) {
			return producible is UnitPrototype { isArmy: true } && !owner.CanSupportAnotherArmy();
		}

		private void WarnAboutUnsupportedArmy(string message) {
			if (owner.isHuman)
				new MsgShowMilitaryAdvisorPopup(owner, message, happy: false).send();
		}

		// Changes production at the player's request. Stored shields carry over
		// to the new item, as in Civ 3, but any beyond its cost are lost.
		public void ChangeProduction(IProducible producible) {
			ChooseProduction(producible);
			shieldsStored = Math.Min(shieldsStored, owner.ShieldCost(producible));
		}

		public bool IsCapital() {
			return capital;
		}

		/// <summary>
		/// Sets the current shield amount in the production box. If the add parameter is true, the shields get appended.<br/>
		/// </summary>
		/// <param name="shields">The number of shields to be added</param>
		/// <param name="add">If true, the shields get appended, otherwise they overwrite the current value</param>
		[LuaMethod]
		public void SetStoredShields(int shields, bool add = false) {
			// TODO: account for overflow if needed
			if (add)
				this.shieldsStored += shields;
			else
				this.shieldsStored = shields;
		}

		// Bumped by every AddBuilding and RemoveBuilding, in any city. Caches
		// derived from city buildings compare against it, so they are
		// invalidated even when a building is swapped for another without
		// the number of buildings changing.
		private static long buildingsVersion = 0;
		internal static long BuildingsVersion => Interlocked.Read(ref buildingsVersion);

		private static void OnBuildingsChanged() {
			Interlocked.Increment(ref buildingsVersion);
		}

		// The cached result of EffectiveBuildings: the buildings built in this
		// city followed by those granted by the owner's active great wonders.
		// It is immutable and replaced as a whole, and it is checked against
		// everything it was computed from on each use: the owner, the location
		// (for continent-wide wonders), the constructed_buildings list and its
		// count, City.BuildingsVersion and the owner's wonder snapshot (which
		// validates itself, see Player.GetBuildingSnapshot).
		private sealed class EffectiveBuildingsCache {
			internal Player owner;
			internal Tile location;
			internal List<CityBuilding> source;
			internal int sourceCount;
			internal long buildingsVersion;
			internal Player.BuildingSnapshot wonders;

			// The buildings, with the constructed ones first. Entries from
			// grantedStart on are granted by wonders. Never modified.
			internal List<CityBuilding> buildings;
			internal int grantedStart;
		}

		private EffectiveBuildingsCache effectiveBuildingsCache;

		private EffectiveBuildingsCache GetEffectiveBuildingsCache() {
			Player.BuildingSnapshot wonders = owner.GetBuildingSnapshot();
			EffectiveBuildingsCache cache = effectiveBuildingsCache;
			if (cache != null
				&& ReferenceEquals(cache.wonders, wonders)
				&& ReferenceEquals(cache.owner, owner)
				&& ReferenceEquals(cache.location, location)
				&& ReferenceEquals(cache.source, constructed_buildings)
				&& cache.sourceCount == constructed_buildings.Count
				&& cache.buildingsVersion == BuildingsVersion) {
				return cache;
			}

			cache = new EffectiveBuildingsCache {
				owner = owner,
				location = location,
				source = constructed_buildings,
				sourceCount = constructed_buildings.Count,
				buildingsVersion = BuildingsVersion,
				wonders = wonders,
			};

			HashSet<Building> buildingsSeen = new();
			List<CityBuilding> result = new();
			foreach (CityBuilding cb in constructed_buildings) {
				result.Add(cb);
				buildingsSeen.Add(cb.building);
			}
			cache.grantedStart = result.Count;

			// Loop through all the wonders we control that aren't obsolete.
			foreach ((City c, CityBuilding cb) in wonders.activeWonders) {
				Building b = cb.building;

				if (b.greatWonderProperties.buildingGainedInEveryCity != null
					&& buildingsSeen.Add(b.greatWonderProperties.buildingGainedInEveryCity)) {
					// Adding to buildingsSeen as we go keeps two wonders that
					// grant the same building from granting it twice.
					result.Add(new CityBuilding() {
						building = b.greatWonderProperties.buildingGainedInEveryCity,
						builtByPlayer = cb.builtByPlayer,
						year = cb.year,
						totalCulture = 0, // TODO: calculate this
					});
				}
				if (b.greatWonderProperties.buildingGainedInEveryCityOnContinent != null
					&& c.location.continent == location.continent
					&& buildingsSeen.Add(b.greatWonderProperties.buildingGainedInEveryCityOnContinent)) {
					result.Add(new CityBuilding() {
						building = b.greatWonderProperties.buildingGainedInEveryCityOnContinent,
						builtByPlayer = cb.builtByPlayer,
						year = cb.year,
						totalCulture = 0, // TODO: calculate this
					});
				}
			}

			cache.buildings = result;
			effectiveBuildingsCache = cache;
			return cache;
		}

		// The same buildings as GetBuildings, without allocating. The list is
		// shared and must not be modified, nor its granted entries (which
		// GetBuildings hands out fresh copies of).
		internal List<CityBuilding> EffectiveBuildings() {
			return GetEffectiveBuildingsCache().buildings;
		}

		public List<CityBuilding> GetBuildings() {
			EffectiveBuildingsCache cache = GetEffectiveBuildingsCache();
			List<CityBuilding> result = new(cache.buildings.Count);
			for (int i = 0; i < cache.buildings.Count; ++i) {
				CityBuilding cb = cache.buildings[i];
				if (i < cache.grantedStart) {
					result.Add(cb);
				} else {
					// Buildings granted by wonders aren't stored anywhere, so
					// callers have always received new objects for them.
					result.Add(new CityBuilding() {
						building = cb.building,
						builtByPlayer = cb.builtByPlayer,
						year = cb.year,
						totalCulture = 0, // TODO: calculate this
					});
				}
			}
			return result;
		}

		// Whether this city has the building, including one granted by a
		// wonder.
		internal bool HasEffectiveBuilding(Building building) {
			foreach (CityBuilding cb in EffectiveBuildings()) {
				if (cb.building == building) {
					return true;
				}
			}
			return false;
		}

		public IEnumerable<IProducible> ListProductionOptions(GameData gameData) {
			HashSet<Resource> accessibleResources = GetAccessibleResources(gameData);
			return ListProductionOptions(gameData, accessibleResources);
		}

		// Like the original Where over units, buildings and inflows, this is
		// evaluated lazily each time it is enumerated. The facts every building
		// check needs (this city's buildings, what the empire is building) are
		// gathered once per enumeration instead of once or twice per building.
		private IEnumerable<IProducible> ListProductionOptions(GameData gameData, HashSet<Resource> accessibleResources) {
			foreach (UnitPrototype unitPrototype in gameData.unitPrototypes) {
				if (unitPrototype.CanProduce(this, accessibleResources)) {
					yield return unitPrototype;
				}
			}

			Building.ProductionContext context = null;
			foreach (Building building in gameData.Buildings) {
				context ??= new Building.ProductionContext(this);
				if (building.CanProduce(this, accessibleResources, context)) {
					yield return building;
				}
			}

			foreach (Inflow inflow in gameData.Inflows) {
				if (inflow.CanProduce(this, accessibleResources)) {
					yield return inflow;
				}
			}
		}

		private HashSet<Resource> GetAccessibleResources(GameData gameData) {
			return gameData.GetTradeNetwork().GetResourcesAvailableToCity(owner, this).Keys.ToHashSet();
		}

		public int FoodNeededToGrow() {
			Rules rules = owner.rules;
			if (residents.Count <= rules.MaximumLevel1CitySize) {
				return rules.FoodNeededToGrowForLevel1Cities;
			} else if (residents.Count <= rules.MaximumLevel2CitySize) {
				return rules.FoodNeededToGrowForLevel2Cities;
			} else {
				return rules.FoodNeededToGrowForLevel3Cities;
			}
		}

		public int TurnsUntilGrowth() {
			int foodGrowthPerTurn = FoodGrowthPerTurn();
			int foodNeededToGrow = FoodNeededToGrow();
			if (foodGrowthPerTurn == 0 || foodStored > foodNeededToGrow) {
				return int.MaxValue;
			} else if (foodGrowthPerTurn < 0) {
				return int.MinValue;
			}

			int additionalFoodNeeded = foodNeededToGrow - foodStored;
			int turnsRoundedDown = additionalFoodNeeded / foodGrowthPerTurn;
			if (additionalFoodNeeded % foodGrowthPerTurn != 0) {
				return turnsRoundedDown + 1;
			}
			return turnsRoundedDown;
		}

		public int TurnsToProduce(IProducible item) {
			int additionalProductionNeeded = owner.ShieldCost(item) - shieldsStored;
			int usefulShields = CurrentProductionYield().useful;
			if (usefulShields == 0) {
				return int.MaxValue;
			}

			int turnsRoundedDown = additionalProductionNeeded / usefulShields;
			if (additionalProductionNeeded % usefulShields != 0) {
				return Math.Max(turnsRoundedDown + 1, 1);
			}
			return Math.Max(turnsRoundedDown, 1);
		}

		public int TurnsUntilProductionFinished() {
			return TurnsToProduce(itemBeingProduced);
		}

		private int ShieldCostForHurrying() {
			// If there are no shields in the box, hurrying costs double.
			if (shieldsStored == 0) {
				return owner.ShieldCost(itemBeingProduced) * 2;
			}
			return owner.ShieldCost(itemBeingProduced) - shieldsStored;
		}

		// Returns the feasibility of hurrying production
		public class HurryProductionDetails {
			public string? errorMessage;
			public string? costMessage;

			public int popCost = -1;
			public int goldCost = -1;
		}
		public HurryProductionDetails GetHurryProductionDetails() {
			Rules rules = EngineStorage.gameData.rules;

			if (isInCivilDisorder) {
				return new HurryProductionDetails() { errorMessage = "The city is in disorder and cannot hurry production." };
			}

			// Nothing to hurry: no production, a full box, or something like
			// Wealth that costs no shields.
			if (itemBeingProduced == null) {
				return new HurryProductionDetails() { errorMessage = "The city is not producing anything to hurry." };
			}
			int shieldCost = ShieldCostForHurrying();
			if (shieldCost <= 0) {
				return new HurryProductionDetails() { errorMessage = "There is nothing to hurry in this city." };
			}

			switch (owner.government.hurryingType) {
				case Government.HurryProductionType.CannotHurry:
					return new HurryProductionDetails() { errorMessage = "We cannot hurry production with this government." };

				case Government.HurryProductionType.ForcedLabor:
					int popCost = (int)Math.Ceiling((float)shieldCost / rules.CitizenValueInShields);
					if (popCost > residents.Count / 2f) {
						return new HurryProductionDetails() { errorMessage = $"Hurrying production would take the lives of too many citizens ({popCost})." };
					}
					return new HurryProductionDetails() {
						costMessage = $"Hurrying production will take the lives of {popCost} citizen(s), are you sure?",
						popCost = popCost,
					};

				case Government.HurryProductionType.PaidLabor:
					int goldCost = shieldCost * rules.ShieldValueInGold;
					if (goldCost > owner.gold) {
						return new HurryProductionDetails() { errorMessage = $"Hurrying production would cost too much gold! ({goldCost})." };
					}
					return new HurryProductionDetails() {
						costMessage = $"Hurrying production will cost {goldCost} gold, are you sure?",
						goldCost = goldCost,
					};
			}
			throw new Exception($"Unknown hurrying type: {owner.government.hurryingType}");
		}

		public void HurryProduction() {
			Rules rules = EngineStorage.gameData.rules;
			HurryProductionDetails details = GetHurryProductionDetails();

			// The UI only offers hurrying when it's possible, but the city may
			// have changed since, so quietly ignore a request that no longer is.
			if (details.errorMessage != null) {
				log.Warning("Not hurrying production in {City}: {Error}", this, details.errorMessage);
				return;
			}

			switch (owner.government.hurryingType) {
				case Government.HurryProductionType.ForcedLabor:
					RemoveCitizens(details.popCost);
					turnsOfUnhappinessDueToPopRushing += rules.TurnPenaltyForEachHurrySacrifice * details.popCost;
					shieldsStored = owner.ShieldCost(itemBeingProduced);
					break;

				case Government.HurryProductionType.PaidLabor:
					owner.gold -= details.goldCost;
					shieldsStored = owner.ShieldCost(itemBeingProduced);
					break;
			}
		}

		public void HandleCityProduction(GameData gameData) {
			IProducible producedItem = ComputeTurnProduction();
			if (producedItem == null) {
				return;
			}

			log.Debug("Produced {ProducedItem} in {City}", producedItem, this);
			if (producedItem is UnitPrototype prototype) {
				AddUnit(prototype, gameData);
			} else if (producedItem is Building building) {
				AddBuilding(building);

				// If we completed a great wonder, mark it as completed so no
				// other civ can build it. If any other cities are building the
				// wonder then change production to the most expensive option,
				// to avoid wasting the shields.
				//
				// TODO: This should interrupt the player's turn to let them
				// make the choice. And does the game allow wonder shuffling in
				// this situation?
				if (building.greatWonderProperties != null) {
					gameData.GreatWondersBuilt.Add(building.name);
					owner.MaybeStartGoldenAgeFromWonders(gameData);

					foreach (Player p in gameData.players) {
						if (p == this.owner) {
							continue;
						}

						foreach (City c in p.cities) {
							if (c.itemBeingProduced.name == building.name) {
								c.SetItemBeingProduced(c.GetMostExpensiveItemToProduce());
							}
						}
					}
				}
			} else if (producedItem is Inflow inflow) {
				// we don't want to "complete" the inflow production
				// unless the player has a reason to change it, this should be ongoing
				return;
			}

			SetItemBeingProduced(ChooseProducible.Choose(this, owner));
		}

		private IProducible GetMostExpensiveItemToProduce() {
			IProducible best = null;
			int bestCost = 0;
			foreach (IProducible ip in ListProductionOptions(EngineStorage.gameData)) {
				int cost = owner.ShieldCost(ip);
				if (best == null || cost > bestCost) {
					best = ip;
					bestCost = cost;
				}
			}
			return best;
		}

		public void HandleCityGrowth(GameData gameData) {
			int foodNeededToGrow = FoodNeededToGrow();
			int foodGrowth = FoodGrowthPerTurn();
			bool hasGranary = HasGranary();

			foodStored += foodGrowth;
			foodStored = Math.Min(foodStored, foodNeededToGrow);

			// Handle the city starving. A size 1 city can't shrink any further.
			if (foodStored < 0) {
				if (residents.Count > 1) {
					RemoveLastCitizen();
				}
				foodStored = 0;
				return;
			}

			// No growth necessary.
			if (foodStored < foodNeededToGrow) {
				return;
			}

			if (CanGrowPopulationByOne(gameData)) {
				CityResident newResident = new CityResident();
				newResident.nationality = owner.civilization;
				newResident.city = this;
				newResident.citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
				AddCitizen(newResident);
				C7Engine.AI.CityTileAssignmentAI.AssignNewCitizenToTile(gameData, newResident);

				if (hasGranary) {
					foodStored /= 2;
				} else {
					foodStored = 0;
				}
			}
		}

		private bool CanGrowPopulationByOne(GameData gD) {
			// We can always grow up to size 6.
			if (residents.Count + 1 <= gD.rules.MaximumLevel1CitySize) {
				return true;
			}

			// If the city doesn't have fresh water and doesn't have an aqueduct
			// then it can't grow into a city.
			//
			// TODO: lakes are bodies of water under 20 tiles (https://civilization.fandom.com/wiki/Fresh_Water_Lake_(Civ3))
			bool hasFreshwaterAccess = location.BordersRiver();
			foreach (Tile t in location.neighbors.Values) {
				if (!t.IsLand() && t.isFreshWater) {
					hasFreshwaterAccess = true;
					break;
				}
			}

			List<CityBuilding> buildings = EffectiveBuildings();
			bool canGrowIntoCity = hasFreshwaterAccess;
			if (!hasFreshwaterAccess) {
				foreach (CityBuilding cb in buildings) {
					if (cb.building.allowsCitySize2 || cb.building.allowsCitySize3) {
						canGrowIntoCity = true;
						break;
					}
				}
			}

			if (!canGrowIntoCity) {
				return false;
			}

			// With fresh water or an aqueduct we can grow to size 2.
			if (residents.Count + 1 <= gD.rules.MaximumLevel2CitySize) {
				return true;
			}

			// If we're a city trying to grow into a metropolis, we need a
			// hospital.
			foreach (CityBuilding cb in buildings) {
				if (cb.building.allowsCitySize3) {
					return true;
				}
			}
			return false;
		}

		public bool HasGranary() {
			foreach (CityBuilding cb in EffectiveBuildings()) {
				if (cb.building.doublesCityGrowthRate) {
					return true;
				}
			}
			return false;
		}

		public bool HasWalls() {
			foreach (CityBuilding cb in EffectiveBuildings()) {
				if (cb.building.providesWalls) {
					return true;
				}
			}
			return false;
		}

		public IEnumerable<StrengthBonus> GetDefenseBonuses() {
			GameData gD = EngineStorage.gameData;

			// Cities give defense bonuses based on their size.
			if (residents.Count > gD.rules.MaximumLevel2CitySize) {
				yield return gD.cityLevel3DefenseBonus;
			} else if (residents.Count > gD.rules.MaximumLevel1CitySize) {
				yield return gD.cityLevel2DefenseBonus;
			} else {
				yield return gD.cityLevel1DefenseBonus;
			}

			bool isTown = residents.Count <= gD.rules.MaximumLevel1CitySize;

			// Buildings, such as walls, can also give bonuses.
			foreach (CityBuilding cb in EffectiveBuildings()) {
				if (cb.building.combatDefenseBonus is not StrengthBonus defenseBonus) {
					continue;
				}

				// If this building doesn't have the "only useful in towns" flag
				// we can always provide the bonus regardless of the city size.
				//
				// But if the building is only useful in towns we need to check
				// the city size before providing the bonus.
				if (!cb.building.onlyUsefulInTowns || isTown) {
					yield return defenseBonus;
				}
			}
		}

		/**
		 * Computes turn production.  If the production queue finishes,
		 * returns the item that is built.  Otherwise, returns null.
		 */
		public IProducible ComputeTurnProduction() {
			int shieldsBefore = shieldsStored;
			int cost = owner.ShieldCost(itemBeingProduced);
			shieldsStored += CurrentProductionYield().useful;

			// Like a settler waiting for the city to grow, an army the empire
			// can't support waits with its shields kept in the box.
			bool unsupportedArmy = IsUnsupportedArmy(itemBeingProduced);
			if (shieldsStored >= cost && residents.Count > itemBeingProduced.populationCost && !unsupportedArmy) {
				shieldsStored = 0;
				RemoveCitizens(itemBeingProduced.populationCost);
				return itemBeingProduced;
			}

			if (unsupportedArmy && shieldsStored >= cost && shieldsBefore < cost) {
				WarnAboutUnsupportedArmy($"The army in {name} is ready, but our empire is too small to support another one. "
					+ $"It will be finished once we have {owner.rules.CitiesNeededToSupportAnArmy} cities for each army.");
			}

			shieldsStored = Math.Min(shieldsStored, owner.ShieldCost(itemBeingProduced));
			return null;
		}

		public int CurrentFoodYield() {
			List<CityBuilding> buildings = EffectiveBuildings();
			int yield = location.FoodYield(this, buildings).yield;
			foreach (CityResident r in residents) {
				yield += r.tileWorked.FoodYield(this, buildings).yield;
			}
			return yield;
		}

		public CorruptableValue CurrentProductionYield() {
			List<CityBuilding> buildings = EffectiveBuildings();
			int yield = location.ProductionYield(this, buildings).yield;
			foreach (CityResident r in residents) {
				yield += r.tileWorked.ProductionYield(this, buildings).yield;
			}
			// A celebrating city wastes half as many shields.
			CorruptableValue result = new(yield, celebrating ? corruption / 2 : corruption);

			// Using our value of corruption, figure out how much useful
			// production we have to work with. Special case anarchy, where no
			// useful production is available. We do this here rather than
			// setting corruption to 100% because CorruptableValue would give us
			// one useful commerce in that situation.
			//
			// The same is true for civil disorder.
			if (owner.government.transitionType || isInCivilDisorder) {
				result.useful = 0;
				result.corrupt = yield;
			}

			// Factories and power plants boost the shields left after waste.
			int productionBonusPercent = 0;
			foreach (CityBuilding cb in buildings) {
				productionBonusPercent += cb.building.productionBonusPercent;
			}
			result.useful += result.useful * productionBonusPercent / 100;

			// TODO: add specialist shields here. Do specialists still work in
			// civil disorder?

			return result;
		}

		public CommerceBreakdown CurrentCommerceYieldRaw(bool respectCivilDisorder = true) {
			List<CityBuilding> buildings = EffectiveBuildings();
			int uncorruptedCommerce = location.CommerceYield(this, buildings).yield;
			foreach (CityResident r in residents) {
				uncorruptedCommerce += r.tileWorked.CommerceYield(this, buildings).yield;
			}

			// Using our value of corruption, figure out how much useful
			// commerce we have to work with. Special case anarchy, where no
			// useful commerce is available. We do this here rather than setting
			// corruption to 100% because CorruptableValue would give us one
			// useful commerce in that situation.
			//
			// The same is true in civil disorder, but we allow ignoring civil
			// disorder for the purpose of calculating whether a city would be
			// happy at a certain luxury slider value even while the city is in
			// civil disorder.
			CorruptableValue commerce = new CorruptableValue(uncorruptedCommerce, corruption);
			if (owner.government.transitionType || (isInCivilDisorder && respectCivilDisorder)) {
				commerce.useful = 0;
				commerce.corrupt = uncorruptedCommerce;
			}

			// TODO: Science/Luxury commerce doesn't seem to be tabulating correctly, can be negative in some cases with specialists, might be ImportCiv3 issue?
			CommerceBreakdown result = new();
			result.corrupted = commerce.corrupt;
			result.beakers = (int)Math.Floor(commerce.useful * owner.scienceRate / 10.0);
			result.happiness = (int)Math.Floor(commerce.useful * owner.luxuryRate / 10.0);
			result.taxes = commerce.useful - result.beakers - result.happiness;

			// Each library, marketplace and similar building adds 50% to the
			// share of commerce it affects.
			int researchBuildings = 0, luxuryBuildings = 0, taxBuildings = 0;
			foreach (CityBuilding cb in buildings) {
				researchBuildings += cb.building.increasesResearch ? 1 : 0;
				luxuryBuildings += cb.building.increasesLuxury ? 1 : 0;
				taxBuildings += cb.building.increasesTax ? 1 : 0;
			}
			result.beakers += result.beakers * researchBuildings / 2;
			result.happiness += result.happiness * luxuryBuildings / 2;
			result.taxes += result.taxes * taxBuildings / 2;

			foreach (CityResident cr in residents) {
				result.beakers += cr.citizenType.Research;
				result.happiness += cr.citizenType.Luxuries;
				result.taxes += cr.citizenType.Taxes;
			}

			return result;
		}

		[MoonSharpHidden]
		public CommerceBreakdown CurrentCommerceYield(bool respectCivilDisorder = true) {
			CommerceBreakdown result = CurrentCommerceYieldRaw(respectCivilDisorder);
			if (this.itemBeingProduced is not Inflow) {
				return result;
			}

			// commerce lua infow
			if (this.itemBeingProduced is Inflow inflowCommerce && inflowCommerce.TryGetInflowYieldFunc(InflowYield.commerce, out var commerceYieldFunc)) {
				int extraCommerce = commerceYieldFunc.Invoke(new ScriptContext(this.owner, this));
				result.wealth += extraCommerce;
			}

			// science lua infow
			if (this.itemBeingProduced is Inflow inflowScience && inflowScience.TryGetInflowYieldFunc(InflowYield.science, out var scienceYieldFunc)) {
				int extraBeakers = scienceYieldFunc.Invoke(new ScriptContext(this.owner, this));
				result.beakers += extraBeakers;
			}

			// happiness lua infow
			if (this.itemBeingProduced is Inflow inflowHappiness && inflowHappiness.TryGetInflowYieldFunc(InflowYield.happiness, out var happinessYieldFunc)) {
				int extraHappiness = happinessYieldFunc.Invoke(new ScriptContext(this.owner, this));
				result.happiness += extraHappiness;
			}

			// corruption lua infow
			if (this.itemBeingProduced is Inflow inflowCorruption && inflowCorruption.TryGetInflowYieldFunc(InflowYield.corruption, out var corruptionYieldFunc)) {
				int lessCorruption = corruptionYieldFunc.Invoke(new ScriptContext(this.owner, this));
				result.corrupted -= lessCorruption;
			}

			return result;
		}

		public int MaintenanceCostsRaw() {
			int result = 0;
			foreach (CityBuilding cb in constructed_buildings) {
				result += cb.building.maintenanceCost;
			}
			return result;
		}

		[MoonSharpHidden]
		public int MaintenanceCosts() {
			int result = 0;
			result += MaintenanceCostsRaw();
			// maintenance lua infow
			if (this.itemBeingProduced is Inflow inflowMaintenance && inflowMaintenance.TryGetInflowYieldFunc(InflowYield.maintenance, out var maintenanceYieldFunc)) {
				int lessMaintenance = maintenanceYieldFunc.Invoke(new ScriptContext(this.owner, this));
				result -= lessMaintenance;
			}
			return result;
		}

		public int FoodGrowthPerTurn() {
			return CurrentFoodYield() - FoodConsumedPerTurn();
		}

		public int FoodConsumedPerTurn() {
			// TODO: exclude resisters in the future.
			return residents.Count * 2;
		}


		private void RemoveLastCitizen() {
			RemoveCitizenAt(residents.Count - 1);
		}

		public void RemoveRandomCitizen() {
			if (residents.Count <= 1)
				return; // TODO: Handle extreme case

			var idx = GameData.rng.Next(residents.Count);
			RemoveCitizenAt(idx);
		}

		private void RemoveCitizenAt(int index) {
			residents[index].tileWorked.personWorkingTile = null;
			residents.RemoveAt(index);
		}

		public void AddCitizen(CityResident cr) {
			residents.Add(cr);
		}

		public void RemoveCitizens(int number) {
			for (int i = 0; i < number; i++) {
				if (residents.Count > 0) {
					RemoveLastCitizen();
				} else {
					Log.Warning("Trying to remove last citizen from {City}", name);
					break;
				}
			}
		}

		public void RemoveAllCitizens() {
			while (residents.Count > 0) {
				RemoveLastCitizen();
			}
		}

		public override string ToString() {
			return $"{name} ({residents.Count})";
		}

		public int GetCulture() {
			return perPlayerCulture[owner];
		}

		public int GetCultureFor(Player player) {
			return perPlayerCulture.GetValueOrDefault(player, 0);
		}

		public int GetCulturePerTurnRaw() {
			// The year can't change during the loop, so look it up once.
			int currentGameYear = CurrentGameYear();
			int result = 0;
			foreach (CityBuilding cb in EffectiveBuildings()) {
				var multiplier = AgeMultiplier(cb, currentGameYear);
				result += cb.building.culturePerTurn * multiplier;
			}
			return result;
		}

		private static int CurrentGameYear() {
			GameData gameData = EngineStorage.gameData;
			return gameData?.timeOptions?.GetRawNumber(gameData.turn) ?? 0;
		}

		private static int AgeMultiplier(CityBuilding cb, int currentGameYear) {
			int ageInMillennia = (int) Math.Floor((currentGameYear - cb.year) / 1000f);

			// Buildings from older saves may have a build year in the future.
			if (ageInMillennia < 0)
				ageInMillennia = 0;

			return 1 << ageInMillennia;
		}

		[MoonSharpHidden]
		public int GetCulturePerTurn() {
			int result = GetCulturePerTurnRaw();
			// culture lua infow
			if (this.itemBeingProduced is Inflow inflow && inflow.TryGetInflowYieldFunc(InflowYield.culture, out var cultureYieldFunc)) {
				int extraCulture = cultureYieldFunc.Invoke(new ScriptContext(this.owner, this));
				result += extraCulture;
			}

			return result;
		}

		public int GetBorderExpansionLevel() {
			// Give ourselves a minimum of 1 culture to avoid taking the log of 0
			int culture = Math.Max(1, GetCulture());

			// Take the log10 of culture, rounding down (so a culture of 123
			// would be 2, a culture of 5 would be 0, etc) and then add one to
			// get the expansion level. With 0-9 culture our culture goal is 10^1
			// and we have one tile of borders, with 10-99 our culture goal is
			// 10^2 and we have two tiles of borders.
			return (int)Math.Floor(Math.Log10(culture)) + 1;
		}

		public void AddBuilding(Building building) {
			constructed_buildings.Add(new CityBuilding {
				building = building,
				builtByPlayer = owner,
				year = CurrentGameYear(),
				totalCulture = 0
			});
			OnBuildingsChanged();
		}
		public void RemoveBuilding(CityBuilding building) {
			constructed_buildings.Remove(building);
			OnBuildingsChanged();
		}

		public void AddUnit(UnitPrototype proto, GameData gameData) {
			MapUnit newUnit = proto.GetInstance(gameData.GenerateID(proto.name), proto, owner, location: location);
			newUnit.experienceLevelKey = gameData.defaultExperienceLevelKey;
			newUnit.experienceLevel = gameData.defaultExperienceLevel;
			newUnit.hitPointsRemaining = newUnit.maxHitPoints;

			location.unitsOnTile.Add(newUnit);
			gameData.mapUnits.Add(newUnit);
			owner.AddUnit(newUnit);

			foreach (CityBuilding b in EffectiveBuildings()) {
				b.building.onFinishedUnitProduction?.Invoke(newUnit);
			}
		}

		// The list of tiles that could be worked by this city.
		// This isn't necessarily a subset of our borders, because we're allowed
		// to work tiles owned by our civ in our big fat cross, even if our
		// borders haven't expanded yet.
		public List<Tile> GetWorkableTiles() {
			List<Tile> result = new();
			foreach (Tile t in GetTilesOfRank(owner.rules.MaxRankOfWorkableTiles)) {
				// Skip tiles not owned by this player.
				if (t.owningCity == null || t.owningCity.owner != this.owner) {
					continue;
				}

				// Skip tiles with cities on them.
				if (t.HasCity()) {
					continue;
				}

				result.Add(t);
			}
			return result;
		}

		// The list of tiles that are within the borders of this city, without
		// taking into account border collisions with other cities.
		public List<Tile> GetTilesWithinBorders() {
			return GetTilesOfRank(GetBorderExpansionLevel());
		}

		// Like GetTilesWithinRankDistance, but with the filtering of ocean
		// tiles for city border calculations.
		private List<Tile> GetTilesOfRank(int rank) {
			List<Tile> result = new();
			foreach (Tile t in location.GetTilesWithinRankDistance(rank)) {
				// Borders of a city near the map's edge run off it: skip the
				// off-map placeholder, which has no terrain or map.
				if (t == Tile.NONE) {
					continue;
				}

				// Law II
				// Ocean tiles may only hold claims of rank 2.
				if (t.baseTerrainType.IsOcean && t.RankDistanceTo(location) > 2) {
					continue;
				}
				result.Add(t);
			}
			return result;
		}

		// See https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/
		private float CalculateDistanceCorruption(GameData gameData, int numAntiCorruptionBuildings) {
			float maxD = (location.map.numTilesWide + location.map.numTilesTall) / 4;

			float distanceToPalace = owner.citiesWithCorruptionWonders.Min(x => location.RankDistanceTo(x.location));
			if (owner.government.corruptionType == Government.CorruptionType.Communal) {
				distanceToPalace = maxD / 4;
			}

			// Cities cut off from the capital's trade network suffer more.
			bool connectedToCapital = gameData.GetTradeNetwork().ConnectedToCapital(owner, this);
			float tradeFactor = connectedToCapital ? 1.0f : 5.0f/4.0f;

			float govtFactor = owner.government.corruptionType switch {
				Government.CorruptionType.Minimal => 3.0f/4.0f,
				Government.CorruptionType.Nuisance => 1f,
				Government.CorruptionType.Problematic => 1f,
				Government.CorruptionType.Rampant => 3.0f/2.0f,
				Government.CorruptionType.Catastrophic => 1f, // anarchy, special cased
				Government.CorruptionType.Communal => 1f,
				Government.CorruptionType.Off => 0f
			};


			float adjustedDistance =
					(float)Math.Pow(0.5f, numAntiCorruptionBuildings)
					* Math.Min(govtFactor * tradeFactor * distanceToPalace, maxD);
			return adjustedDistance / maxD;
		}

		// See https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/
		private float CalculateRankCorruption(int adjustedOptimalCityNumber, int numAntiCorruptionBuildings) {
			int rank = rankIndex;
			if (owner.government.corruptionType == Government.CorruptionType.Communal) {
				rank = owner.cities.Count / 2;
			}

			float nOpt = Math.Max(
				1,
				adjustedOptimalCityNumber + .25f * numAntiCorruptionBuildings);

			if (rank < nOpt) {
				return rank / (2 * nOpt);
			} else {
				return (2 * rank - nOpt) / (2 * nOpt);
			}
		}

		public void CalculateCorruption(GameData gameData) {
			CalculateCorruption(gameData, owner.GetAdjustedOptimalCityNumber(gameData));
		}

		// The adjusted optimal city number is empire-wide, so when updating
		// every city Player.DoCorruptionCalculations works it out once and
		// passes it in rather than rescanning the empire for each city.
		internal void CalculateCorruption(GameData gameData, int adjustedOptimalCityNumber) {
			int numAntiCorruptionBuildings = 0;

			// TODO: Handle the SPHQ.
			int numCorruptionReducingSmallWondersInCity = 0;
			foreach (CityBuilding cb in EffectiveBuildings()) {
				if (cb.building.reducesCorruption) {
					++numAntiCorruptionBuildings;
				}
				if (cb.building.isForbiddenPalace) {
					++numCorruptionReducingSmallWondersInCity;
				}
			}

			corruption = CalculateDistanceCorruption(gameData, numAntiCorruptionBuildings)
					+ CalculateRankCorruption(adjustedOptimalCityNumber, numAntiCorruptionBuildings);
			// TODO: apply policeman modifiers, before applying the max

			// Corruption maxes out at 90%, and this max can be reduced further
			// via courthouses/police stations, and the forbidden palace/SPHQ.
			float maxCorruption = Math.Max(
				0,
				.9f - (.1f * numAntiCorruptionBuildings + .7f * numCorruptionReducingSmallWondersInCity));
			corruption = Math.Max(corruption, 0);
			corruption = Math.Min(corruption, maxCorruption);
		}

		// Does the per turn culture updating for the city and returns whether
		// the borders need to be updated.
		public bool UpdateCultureAndCheckForExpansion() {
			int start = GetBorderExpansionLevel();
			// Only built buildings keep a culture total. GetBuildings also
			// lists the buildings granted by wonders, but as new objects each
			// time, so adding to their totals never had any effect.
			foreach (CityBuilding cb in constructed_buildings) {
				cb.totalCulture += cb.building.culturePerTurn;
			}

			perPlayerCulture[owner] += GetCulturePerTurn();

			return start != GetBorderExpansionLevel();
		}

		// Initializes the citizen moods, before positive and negative
		// influcences are added. A fixed number of citizens are born content,
		// based on the difficulty level, and after that all citizens are born
		// unhappy. Specialists and resisters are excluded from this.
		private void InitializeMoodsForDifficulty(Difficulty gameDifficulty) {
			int numLaborers = residents.Count(x => x.citizenType.IsDefaultCitizen);
			int content = Math.Min(gameDifficulty.NumberOfCitizensBornContent, numLaborers);

			foreach (CityResident r in residents) {
				if (!r.citizenType.IsDefaultCitizen) {
					continue;
				}

				if (content > 0) {
					--content;
					r.mood = CityResident.Mood.Content;
				} else {
					r.mood = CityResident.Mood.Unhappy;
				}
			}
		}

		// Attemps to move the specified number of citizens that have mood `from`
		// to mood `to`, returning the actual number changed.
		private int ApplyMoodChange(int count, CityResident.Mood from, CityResident.Mood to) {
			int result = 0;

			foreach (CityResident r in residents) {
				if (!r.citizenType.IsDefaultCitizen) {
					continue;
				}

				if (count <= 0) {
					break;
				}

				if (r.mood == from) {
					--count;
					++result;
					r.mood = to;
				}
			}

			return result;
		}

		// Handles the process of consuming content to happy moves, first making
		// content faces happy, then unhappy to happy at 1/2 the rate, and then
		// applying the remainder of unhappy->content, if any.
		private void ConsumeContentToHappyMoves(int contentToHappyMoves) {
			CityResident.Mood happy = CityResident.Mood.Happy;
			CityResident.Mood content = CityResident.Mood.Content;
			CityResident.Mood unhappy = CityResident.Mood.Unhappy;

			// Now move content faces to happy faces.
			contentToHappyMoves -= ApplyMoodChange(contentToHappyMoves, content, happy);

			// If there are moves left over we can try moving unhappy
			// faces to happy, but it takes two "points".
			contentToHappyMoves -= 2 * ApplyMoodChange(contentToHappyMoves / 2, unhappy, happy);

			// Account for the remainder of the integer division
			// (ApplyMoodChange ignores a negative input).
			ApplyMoodChange(contentToHappyMoves, unhappy, content);
		}

		public enum Mood {
			Unhappy,
			Happy
		};

		// A city that is big enough, isn't starving, has no unhappy citizens
		// and more happy than content ones celebrates "We Love the King Day".
		// Specialists don't count.
		public bool QualifiesForCelebration(Rules rules) {
			if (residents.Count < rules.MinimumPopulationForWeLoveTheKing || FoodGrowthPerTurn() < 0) {
				return false;
			}
			List<CityResident> laborers = residents.Where(r => r.citizenType.IsDefaultCitizen).ToList();
			int happy = laborers.Count(r => r.mood == CityResident.Mood.Happy);
			int content = laborers.Count(r => r.mood == CityResident.Mood.Content);
			bool anyUnhappy = laborers.Any(r => r.mood == CityResident.Mood.Unhappy);
			return !anyUnhappy && happy > content;
		}

		// This function does the heavy lifting of happiness calculations,
		// combining the various bonuses and penalties that affect citizen moods.
		//
		// Some references:
		//  - https://forums.civfanatics.com/threads/how-is-happiness-calculated.74966/
		//  - https://codehappy.net/apolyton/threads/83368-1.htm
		//
		public Mood RecalculateCitizenMoods(GameData gameData) {
			CityResident.Mood happy = CityResident.Mood.Happy;
			CityResident.Mood content = CityResident.Mood.Content;
			CityResident.Mood unhappy = CityResident.Mood.Unhappy;
			InitializeMoodsForDifficulty(gameData.gameDifficulty);

			// We want to track the move deltas from content to happy and unhappy
			// to content. We can also move from unhappy straight to content,
			// but it costs 2 "points" from the contentToHappy counter, and is
			// only applied if there are no content faces.
			int contentToHappyMoves = 0;
			int unhappyToContentMoves = 0;

			// Each citizen lost to pop rushing has a 20 turn penalty, so
			// multiple citizens lost causes multiple unhappy faces.
			if (turnsOfUnhappinessDueToPopRushing > 0) {
				contentToHappyMoves -= (turnsOfUnhappinessDueToPopRushing - 1) / gameData.rules.TurnPenaltyForEachHurrySacrifice + 1;
			}

			// TODO: add penalty for drafting

			// War weariness makes citizens unhappy, like pop rushing.
			contentToHappyMoves -= owner.WarWearinessUnhappiness(this);

			List<CityBuilding> buildings = EffectiveBuildings();
			// TODO: add penalty for aggression against home country

			// Building happiness/unhappiness, which only affects the unhappy to
			// content transition, nothing with happy faces.
			//
			// TODO: account for wonders and buildings with global/continental effects.
			foreach (CityBuilding cb in buildings) {
				unhappyToContentMoves -= cb.building.unhappyFacesInCity;
				unhappyToContentMoves += cb.building.contentFacesInCity;
			}

			// Depending on the government type, land defensive units can serve
			// as military police.
			int landDefenders = 0;
			foreach (MapUnit unit in location.unitsOnTile) {
				if (unit.CanDefendOnLand()) {
					++landDefenders;
				}
			}
			unhappyToContentMoves += Math.Min(owner.government.militaryPoliceLimit, landDefenders);

			// Luxury spending moves content faces to happy faces, one face for
			// every two luxuries.
			//
			// Don't respect civil disorder during this calculation, because if
			// we are currently in civil disorder our commerce is all corrupt,
			// but we still need to be able to calculate whether a certain
			// luxury slider value would get us out of civil disorder.
			contentToHappyMoves += CurrentCommerceYield(respectCivilDisorder: false).happiness / 2;

			// As do luxury resources, which can be boosted by marketplaces.
			int effectiveLux = GetLuxuries(gameData).Keys.Count;
			bool increasesLuxuryTrade = false;
			foreach (CityBuilding cb in buildings) {
				if (cb.building.increasesLuxuryTrade) {
					increasesLuxuryTrade = true;
					break;
				}
			}
			if (increasesLuxuryTrade) {
				effectiveLux = (int)(Math.Floor(effectiveLux / 2f) * Math.Ceiling(effectiveLux / 2f) + Math.Ceiling(effectiveLux / 2f));
			}
			contentToHappyMoves += effectiveLux;

			if (contentToHappyMoves >= 0) {
				if (unhappyToContentMoves >= 0) {
					// First apply all the unhappy->content moves. If there are
					// left over content faces that's ok, they can't be used to
					// get a citizen to happy.
					ApplyMoodChange(unhappyToContentMoves, unhappy, content);

					// Then do the same for content->happy moves, which can also
					// make unhappy->happy moves.
					ConsumeContentToHappyMoves(contentToHappyMoves);
				} else {
					int happyPoints = contentToHappyMoves;
					int sadPoints = -unhappyToContentMoves; // Deal with positive numbers

					// Each "sadness point" wipes away a content->happy move,
					// so netralize things out.
					contentToHappyMoves = Math.Max(0, happyPoints - sadPoints);
					sadPoints = Math.Max(0, sadPoints - happyPoints);

					// Use up all our content to happy moves.
					ConsumeContentToHappyMoves(contentToHappyMoves);

					// Now apply any remaining "sadness points", moving happy
					// faces back to content.
					ApplyMoodChange(sadPoints, happy, content);
				}
			} else {
				if (unhappyToContentMoves >= 0) {
					int sadPoints = -contentToHappyMoves; // Deal with positive numbers
					int contentPoints = unhappyToContentMoves;

					// Each H->C move point wipes away a content move, so
					// netralize things out.
					unhappyToContentMoves = Math.Max(0, contentPoints - sadPoints);
					sadPoints = Math.Max(0, sadPoints - contentPoints);

					// Content faces get moved to unhappy by the penalty. We
					// start with some content based on the difficulty level so
					// this is meaningful. We don't need to worry about H->U
					// moves because there is no way to have happy faces on this
					// code path.
					ApplyMoodChange(sadPoints, content, unhappy);

					// Then our positive U->C moves get applied.
					ApplyMoodChange(unhappyToContentMoves, unhappy, content);
				} else {
					int sadPoints = -contentToHappyMoves; // Deal with positive numbers

					// We have buildings that make happy faces go to content
					// faces, but there is no way to have happy faces on this
					// code path, so we just need to move content faces to
					// unhappy faces with the main penalty.
					ApplyMoodChange(sadPoints, content, unhappy);
				}
			}

			int happyCount = 0;
			int unhappyCount = 0;
			foreach (CityResident cr in residents) {
				if (cr.mood == CityResident.Mood.Happy) { ++happyCount; }
				if (cr.mood == CityResident.Mood.Unhappy) { ++unhappyCount; }
			}
			if (unhappyCount > 0 && unhappyCount > happyCount) {
				return Mood.Unhappy;
			} else {
				return Mood.Happy;
			}
		}

		private Dictionary<Resource, int> ListResourceAccess(GameData gameData, ResourceCategory category) {
			Dictionary<Resource, int> availableResources = gameData.GetTradeNetwork().GetResourcesAvailableToCity(owner, this);
			Dictionary<Resource, int> result = new();
			foreach ((Resource r, int count) in availableResources) {
				if (r.Category == category) {
					result[r] = count;
				}
			}
			return result;
		}

		public Dictionary<Resource, int> GetStrategicResources(GameData gameData) {
			return ListResourceAccess(gameData, ResourceCategory.STRATEGIC);
		}

		public Dictionary<Resource, int> GetLuxuries(GameData gameData) {
			return ListResourceAccess(gameData, ResourceCategory.LUXURY);
		}
	}
}

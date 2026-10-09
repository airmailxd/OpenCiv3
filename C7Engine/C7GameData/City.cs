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
								 // For a building that grants a unit every few turns (the Statue of
								 // Zeus, Knights Templar), the turns since it last did.
		public int turnsTowardFreeUnit;
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

		// What the player has queued up to build after itemBeingProduced, in
		// order.
		public List<IProducible> productionQueue = new();

		public int foodStored = 0;

		public bool capital = false;

		// Changing hands changes the buildings of both players, which tells
		// the caches derived from them (see Player.GetBuildingSnapshot).
		public Player owner {
			get => _owner;
			set {
				Player previous = _owner;
				_owner = value;
				if (!ReferenceEquals(previous, value)) {
					previous?.OnBuildingsChanged();
					value?.OnBuildingsChanged();
				}
			}
		}
		private Player _owner;

		public List<CityResident> residents = new List<CityResident>();

		// The list of buildings built in this city. You probably want to use
		// GetBuildings, which can also include buildings granted by wonders.
		//
		// Change it through AddBuilding and RemoveBuilding, or by assigning a
		// new list, so the owner's caches hear of it. (This city's own cache
		// also notices the list being changed directly, but the owner's
		// empire-wide one doesn't.)
		public List<CityBuilding> constructed_buildings {
			get => _constructedBuildings;
			set {
				_constructedBuildings = value;
				_owner?.OnBuildingsChanged();
			}
		}
		private List<CityBuilding> _constructedBuildings;

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

		// Resistance in a conquered city: the number of citizens still loyal
		// to resistanceFrom, the civ it was taken from. While any resist the
		// city produces no shields or commerce, as in Civ3. See
		// StartResistance and UpdateResistance.
		public int resisters = 0;
		public Player resistanceFrom;

		public bool IsInResistance => resisters > 0;

		// Whether production was hurried this turn, with gold, citizens or a
		// great leader. As in Civ3, the city then can't change what it's
		// building until the turn ends, so the hurried shields can't be
		// moved to something else.
		public bool hurriedThisTurn = false;

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

		// Whether the player may switch production to the item. Production
		// hurried this turn is locked in.
		public bool CanChangeProduction(IProducible producible) {
			return !hurriedThisTurn || producible == itemBeingProduced;
		}

		// Changes production at the player's request. Stored shields carry over
		// to the new item, as in Civ 3, but any beyond its cost are lost.
		// Returns false if production was hurried this turn and can't change.
		public bool ChangeProduction(IProducible producible) {
			if (!CanChangeProduction(producible)) {
				log.Information("Not changing production in {City}: it was hurried this turn", this);
				return false;
			}
			ChooseProduction(producible);
			shieldsStored = Math.Min(shieldsStored, owner.ShieldCost(producible));
			return true;
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

		// The cached result of EffectiveBuildings: the buildings built in this
		// city followed by those granted by the owner's active great wonders.
		// It is immutable and replaced as a whole, and it is checked against
		// everything it was computed from on each use, in O(1): the owner, the
		// location (for continent-wide wonders), the constructed_buildings
		// list, its count and whether it has been modified since (which
		// catches a building being swapped for another without the count
		// changing), and the owner's wonder snapshot (which validates itself,
		// see Player.GetBuildingSnapshot).
		private sealed class EffectiveBuildingsCache {
			internal Player owner;
			internal Tile location;
			internal List<CityBuilding> source;
			internal int sourceCount;
			internal List<CityBuilding>.Enumerator sourceVersion;
			internal Player.BuildingSnapshot wonders;

			// The buildings, with the constructed ones first. Entries from
			// grantedStart on are granted by wonders. Never modified.
			internal List<CityBuilding> buildings;
			internal int grantedStart;

			// Every building the owner's wonders provide to this city, whether
			// or not the city has also built it.
			internal HashSet<Building> providedByWonders;
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
				&& CollectionVersion.Unchanged(cache.sourceVersion)) {
				return cache;
			}

			cache = new EffectiveBuildingsCache {
				owner = owner,
				location = location,
				source = constructed_buildings,
				sourceCount = constructed_buildings.Count,
				sourceVersion = constructed_buildings.GetEnumerator(),
				wonders = wonders,
			};

			HashSet<Building> buildingsSeen = new();
			HashSet<Building> providedByWonders = new();
			List<CityBuilding> result = new();
			foreach (CityBuilding cb in constructed_buildings) {
				result.Add(cb);
				buildingsSeen.Add(cb.building);
			}
			cache.grantedStart = result.Count;

			// Granted buildings belong to the city's owner, not to whoever
			// built the wonder, so a captured wonder's buildings make culture
			// for its captor.
			//
			// Loop through all the wonders we control that aren't obsolete.
			foreach ((City c, CityBuilding cb) in wonders.activeWonders) {
				Building b = cb.building;

				if (b.greatWonderProperties.buildingGainedInEveryCity != null) {
					providedByWonders.Add(b.greatWonderProperties.buildingGainedInEveryCity);
				}
				if (b.greatWonderProperties.buildingGainedInEveryCityOnContinent != null
					&& c.location.continent == location.continent) {
					providedByWonders.Add(b.greatWonderProperties.buildingGainedInEveryCityOnContinent);
				}

				if (b.greatWonderProperties.buildingGainedInEveryCity != null
					&& buildingsSeen.Add(b.greatWonderProperties.buildingGainedInEveryCity)) {
					// Adding to buildingsSeen as we go keeps two wonders that
					// grant the same building from granting it twice.
					result.Add(new CityBuilding() {
						building = b.greatWonderProperties.buildingGainedInEveryCity,
						builtByPlayer = owner,
						year = cb.year,
						totalCulture = 0, // TODO: calculate this
					});
				}
				if (b.greatWonderProperties.buildingGainedInEveryCityOnContinent != null
					&& c.location.continent == location.continent
					&& buildingsSeen.Add(b.greatWonderProperties.buildingGainedInEveryCityOnContinent)) {
					result.Add(new CityBuilding() {
						building = b.greatWonderProperties.buildingGainedInEveryCityOnContinent,
						builtByPlayer = owner,
						year = cb.year,
						totalCulture = 0, // TODO: calculate this
					});
				}
			}

			cache.buildings = result;
			cache.providedByWonders = providedByWonders;
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
			return GetAvailableResources(gameData).Keys.ToHashSet();
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

		// Whether the production box is empty. Hurrying then costs double, as
		// in Civ3.
		private bool HurryingFromAnEmptyBox() => shieldsStored == 0;

		private int ShieldCostForHurrying() {
			// If there are no shields in the box, hurrying costs double.
			if (HurryingFromAnEmptyBox()) {
				return owner.ShieldCost(itemBeingProduced) * 2;
			}
			return owner.ShieldCost(itemBeingProduced) - shieldsStored;
		}

		// The gold it costs to buy the rest of the current item: the BIQ's
		// "shield value in gold" (4 in the standard rules) for each shield
		// still needed, the same for units and improvements, doubled from an
		// empty box. "The cost is four gold per shield purchased (or eight if
		// no shields have yet been expended on building it--this represents
		// the extra cost of starting and finishing the project on the same
		// turn)."
		// https://apolyton.net/forum/civilization-series/civilization-iii/132835-food-shield-gold-explanation-help-please
		internal int HurryGoldCost() {
			return ShieldCostForHurrying() * owner.rules.ShieldValueInGold;
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
			if (IsInResistance) {
				return new HurryProductionDetails() { errorMessage = "The city is resisting our rule and cannot hurry production." };
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

			// Civ3 never lets wonders, great or small, be bought or rushed
			// with citizens: "you cannot rush wonders, even the small ones"
			// (https://apolyton.net/forum/miscellaneous/archives/civ3-strategy-archive/85927-when-and-why-to-hurry-the-production);
			// "a Leader is the only way to speed up Wonder production"
			// (https://apolyton.net/forum/civilization-series/civilization-iii/44853-can-you-hurry-up-wonders).
			if (itemBeingProduced is Building { isSmallWonder: true } || itemBeingProduced is Building b && b.IsGreatWonder()) {
				return new HurryProductionDetails() { errorMessage = "Wonders cannot be hurried." };
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
					int goldCost = HurryGoldCost();
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
			hurriedThisTurn = true;
		}

		// Fills the production box, so that the current item is finished at
		// the end of the turn. A great leader hurries production this way.
		internal void FillProductionBox() {
			if (itemBeingProduced != null) {
				shieldsStored = owner.ShieldCost(itemBeingProduced);
				hurriedThisTurn = true;
			}
		}

		public void HandleCityProduction(GameData gameData) {
			// The turn is over, so production can be changed again.
			hurriedThisTurn = false;
			IProducible producedItem = ComputeTurnProduction();
			if (producedItem == null) {
				return;
			}

			log.Debug("Produced {ProducedItem} in {City}", producedItem, this);
			if (producedItem is UnitPrototype prototype) {
				AddUnit(prototype, gameData);
			} else if (producedItem is Building building) {
				if (building.isCenterOfEmpire) {
					owner.MovePalaceTo(this, building);
				} else if (building.IsSpaceshipPart) {
					// Spaceship parts go to the owner's ship, not the city.
					SpaceRace.OnPartCompleted(gameData, this, building);
				} else {
					AddBuilding(building);
				}

				if (building.buildSpaceshipParts) {
					SpaceRace.OnApolloCompleted(gameData, this, building);
				}

				// Theory of Evolution: two free advances, once, on completion.
				if (building.twoFreeAdvances) {
					owner.GrantFreeTechs(gameData, 2, building.name, this);
				}

				// If we completed a great wonder, mark it as completed so no
				// other civ can build it. If any other cities are building the
				// wonder then change production to the next item in their
				// queue, or else the most expensive option, to avoid wasting
				// the shields.
				//
				// TODO: This should interrupt the player's turn to let them
				// make the choice. And does the game allow wonder shuffling in
				// this situation?
				if (building.greatWonderProperties != null) {
					gameData.GreatWondersBuilt.Add(building.name);
					owner.MaybeStartGoldenAgeFromWonders(gameData);
					AnnounceWonder(gameData, building);

					foreach (Player p in gameData.players) {
						if (p == this.owner) {
							continue;
						}

						foreach (City c in p.cities) {
							if (c.itemBeingProduced?.name == building.name) {
								c.SetItemBeingProduced(c.TakeNextQueuedProduction(gameData) ?? c.GetMostExpensiveItemToProduce());
							}
						}
					}
				}
			} else if (producedItem is Inflow inflow) {
				// we don't want to "complete" the inflow production
				// unless the player has a reason to change it, this should be ongoing
				return;
			}

			IProducible next = ChooseNextProduction(gameData, producedItem);
			SetItemBeingProduced(next);
			if (owner.isHuman) {
				new MsgCityProductionCompleted(owner, this, producedItem.name, next?.name).send();
			}
		}

		// Tells every other human player which civ has completed a great
		// wonder. The builder hears of it from the production popup.
		private void AnnounceWonder(GameData gameData, Building wonder) {
			foreach (Player p in gameData.players) {
				if (!p.isHuman || p.defeated || p == owner) {
					continue;
				}
				new MsgWonderCompleted(p, owner, wonder.name, name).send();
			}
		}

		// What the city builds once it has finished an item: the next item in
		// its production queue that can still be built, or, for a human player
		// with nothing queued, the same unit again. Otherwise the AI picks.
		private IProducible ChooseNextProduction(GameData gameData, IProducible completed) {
			IProducible queued = TakeNextQueuedProduction(gameData);
			if (queued != null) {
				return queued;
			}
			if (owner.isHuman && completed is UnitPrototype && ListProductionOptions(gameData).Contains(completed)) {
				return completed;
			}
			return ChooseProducible.Choose(this, owner);
		}

		// Takes the next item off the production queue that can still be
		// built, dropping any before it that can't. Null if there's none.
		private IProducible TakeNextQueuedProduction(GameData gameData) {
			if (productionQueue.Count == 0) {
				return null;
			}
			HashSet<IProducible> options = ListProductionOptions(gameData).ToHashSet();
			while (productionQueue.Count > 0) {
				IProducible queued = productionQueue[0];
				productionQueue.RemoveAt(0);
				if (options.Contains(queued)) {
					return queued;
				}
			}
			return null;
		}

		// Adds an item to the end of the production queue, to be built after
		// the current item and those already queued.
		public void EnqueueProduction(IProducible producible) {
			productionQueue.Add(producible);
		}

		public void ClearProductionQueue() {
			productionQueue.Clear();
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

			// With Longevity a full food box adds two citizens instead of one.
			// The second still needs room to grow (an aqueduct or hospital
			// where the city reaches the next size level).
			int newCitizens = owner.GetBuildingSnapshot().doublesCityGrowthEverywhere ? 2 : 1;
			bool grew = false;
			for (int i = 0; i < newCitizens && CanGrowPopulationByOne(gameData); ++i) {
				CityResident newResident = new CityResident();
				newResident.nationality = owner.civilization;
				newResident.city = this;
				newResident.citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
				AddCitizen(newResident);
				C7Engine.AI.CityTileAssignmentAI.AssignNewCitizenToTile(gameData, newResident);
				grew = true;
			}

			if (grew) {
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

		// The city's barracks, harbor and airport, built or granted by a wonder:
		// the buildings that make veteran land, sea and air units. Null if it
		// has none.
		public Building Barracks() => EffectiveBuildingWhere(b => b.providesVeteranGroundUnits);
		public Building Harbor() => EffectiveBuildingWhere(b => b.providesVeteranSeaUnits);
		public Building Airport() => EffectiveBuildingWhere(b => b.providesVeteranAirUnits);

		private Building EffectiveBuildingWhere(Func<Building, bool> predicate) {
			foreach (CityBuilding cb in EffectiveBuildings()) {
				if (predicate(cb.building)) {
					return cb.building;
				}
			}
			return null;
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
			return yield * YieldMultiplier();
		}

		// Under Civ3's Accelerated Production rule a city generates twice the
		// food, shields and commerce its tiles produce. The doubling comes
		// before waste and corruption, and citizens still eat as much as ever.
		internal int YieldMultiplier() {
			return owner?.rules?.AcceleratedProduction == true ? 2 : 1;
		}

		// The food, shields and commerce the government's tile penalty (e.g.
		// despotism's) takes from the city's tiles this turn.
		public (int food, int shields, int commerce) TileYieldPenalties() {
			List<CityBuilding> buildings = EffectiveBuildings();
			int food = location.FoodYield(this, buildings).penalty;
			int shields = location.ProductionYield(this, buildings).penalty;
			int commerce = location.CommerceYield(this, buildings).penalty;
			foreach (CityResident r in residents) {
				food += r.tileWorked.FoodYield(this, buildings).penalty;
				shields += r.tileWorked.ProductionYield(this, buildings).penalty;
				commerce += r.tileWorked.CommerceYield(this, buildings).penalty;
			}
			return (food, shields, commerce);
		}

		public CorruptableValue CurrentProductionYield() {
			List<CityBuilding> buildings = EffectiveBuildings();
			int yield = location.ProductionYield(this, buildings).yield;
			foreach (CityResident r in residents) {
				yield += r.tileWorked.ProductionYield(this, buildings).yield;
			}
			yield *= YieldMultiplier();
			// A celebrating city wastes half as many shields.
			CorruptableValue result = new(yield, celebrating ? corruption / 2 : corruption);

			// Using our value of corruption, figure out how much useful
			// production we have to work with. Special case anarchy, where no
			// useful production is available. We do this here rather than
			// setting corruption to 100% because CorruptableValue would give us
			// one useful commerce in that situation.
			//
			// The same is true for civil disorder and resistance.
			if (owner.government.transitionType || isInCivilDisorder || IsInResistance) {
				result.useful = 0;
				result.corrupt = yield;
			}

			// Specialists work only when the city does: policemen cut waste,
			// and civil engineers add their own shields. Assumption: the
			// engineers' shields count before factories and power plants,
			// like those of the city's tiles.
			if (!owner.government.transitionType && !isInCivilDisorder && !IsInResistance) {
				RecoverWithPolicemen(ref result);
				foreach (CityResident cr in residents) {
					result.useful += cr.citizenType?.Construction ?? 0;
				}
			}

			// Factories and power plants boost the shields left after waste.
			result.useful += result.useful * ProductionBonusPercent(buildings) / 100;

			return result;
		}

		// Policemen (specialists with a corruption value) win back lost
		// commerce or shields. Civ3 doesn't document the amount; this takes
		// the BIQ's value as an absolute amount, like the other specialists'
		// taxes, science and shields: the standard policeman's 1 recovers one
		// corrupt commerce and one wasted shield. Never more than was lost.
		private void RecoverWithPolicemen(ref CorruptableValue value) {
			int recovered = 0;
			foreach (CityResident cr in residents) {
				recovered += cr.citizenType?.Corruption ?? 0;
			}
			recovered = Math.Min(recovered, value.corrupt);
			if (recovered > 0) {
				value.corrupt -= recovered;
				value.useful += recovered;
			}
		}

		// The percentage the given buildings add to the city's useful shields.
		// As in Civ3, a production building only works alongside the building
		// it requires: a power plant ("increases factory output") does nothing
		// without a Factory, even one granted by the Hoover Dam. And a city
		// only runs one power plant (they replace each other), so of several
		// plants, e.g. a built Coal Plant and the Hoover Dam's Hydro Plant,
		// only the best counts.
		internal static int ProductionBonusPercent(List<CityBuilding> buildings) {
			int total = 0;
			int bestReplaceable = 0;
			foreach (CityBuilding cb in buildings) {
				Building b = cb.building;
				if (b.productionBonusPercent == 0) {
					continue;
				}
				if (b.requiredBuilding != null && !buildings.Exists(other => other.building == b.requiredBuilding)) {
					continue;
				}
				if (b.replacesOtherBuildings) {
					bestReplaceable = Math.Max(bestReplaceable, b.productionBonusPercent);
				} else {
					total += b.productionBonusPercent;
				}
			}
			return total + bestReplaceable;
		}

		// The pollution the city's buildings add to it, from the BIQ's
		// per-building pollution value (e.g. the Iron Works' 4).
		//
		// TODO: nothing consumes this yet. Civ3's city pollution (from
		// population and production, reduced by the Mass Transit System and
		// Recycling Center) and the chance of polluting tiles it drives aren't
		// implemented.
		public int BuildingPollution() {
			int result = 0;
			foreach (CityBuilding cb in EffectiveBuildings()) {
				result += cb.building.pollution;
			}
			return result;
		}

		public CommerceBreakdown CurrentCommerceYieldRaw(bool respectCivilDisorder = true) {
			List<CityBuilding> buildings = EffectiveBuildings();
			int uncorruptedCommerce = location.CommerceYield(this, buildings).yield;
			foreach (CityResident r in residents) {
				uncorruptedCommerce += r.tileWorked.CommerceYield(this, buildings).yield;
			}
			uncorruptedCommerce *= YieldMultiplier();

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
			RecoverWithPolicemen(ref commerce);
			// A resisting city produces nothing, like one in disorder.
			bool inDisorder = (isInCivilDisorder || IsInResistance) && respectCivilDisorder;
			bool inAnarchy = owner.government.transitionType;
			if (inAnarchy || inDisorder) {
				commerce.useful = 0;
				commerce.corrupt = uncorruptedCommerce;
			}

			// TODO: Science/Luxury commerce doesn't seem to be tabulating correctly, can be negative in some cases with specialists, might be ImportCiv3 issue?
			CommerceBreakdown result = new();
			result.corrupted = commerce.corrupt;
			// Civ3 rounds each share to the nearest whole value rather than
			// down (8 commerce at 70% science is 6 beakers, not 5). Luxuries
			// are rounded first, then science, from what's left, and taxes get
			// the remainder.
			result.happiness = RoundedShare(commerce.useful, owner.luxuryRate);
			result.beakers = Math.Min(RoundedShare(commerce.useful, owner.scienceRate), commerce.useful - result.happiness);
			result.taxes = commerce.useful - result.beakers - result.happiness;

			// Each library, marketplace and similar building adds 50% to the
			// share of commerce it affects, and each research-doubling wonder
			// (Copernicus, Newton's) adds another 100%. The bonuses stack
			// additively on the base share.
			int researchPercent = 0, luxuryBuildings = 0, taxBuildings = 0;
			foreach (CityBuilding cb in buildings) {
				researchPercent += cb.building.increasesResearch ? 50 : 0;
				researchPercent += cb.building.doublesResearch ? 100 : 0;
				luxuryBuildings += cb.building.increasesLuxury ? 1 : 0;
				taxBuildings += cb.building.increasesTax ? 1 : 0;
			}
			result.beakers += result.beakers * researchPercent / 100;
			result.happiness += result.happiness * luxuryBuildings / 2;
			result.taxes += result.taxes * taxBuildings / 2;

			// Specialists add their own taxes, beakers and luxuries. A city in
			// disorder produces nothing, specialists included. Under anarchy
			// no taxes or science are collected, but entertainers still
			// entertain (they're how a city keeps order then).
			if (!inDisorder) {
				foreach (CityResident cr in residents) {
					result.happiness += cr.citizenType.Luxuries;
					if (!inAnarchy) {
						result.beakers += cr.citizenType.Research;
						result.taxes += cr.citizenType.Taxes;
					}
				}
			}

			return result;
		}

		// Shares out commerce by the owner's sliders, as CurrentCommerceYieldRaw
		// does (without the buildings' bonuses).
		private void AddUsefulCommerce(ref CommerceBreakdown result, int commerce) {
			if (commerce <= 0) {
				return;
			}
			int happiness = RoundedShare(commerce, owner.luxuryRate);
			int beakers = Math.Min(RoundedShare(commerce, owner.scienceRate), commerce - happiness);
			result.happiness += happiness;
			result.beakers += beakers;
			result.taxes += commerce - happiness - beakers;
		}

		// The share of `commerce` a slider at `rate` (in tenths) gets, rounding
		// halves up.
		private static int RoundedShare(int commerce, int rate) {
			return (commerce * rate + 5) / 10;
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
				// The commerce saved from corruption is useful again, so it is
				// shared out by the sliders like the rest.
				int lessCorruption = Math.Min(corruptionYieldFunc.Invoke(new ScriptContext(this.owner, this)), result.corrupted);
				result.corrupted -= lessCorruption;
				AddUsefulCommerce(ref result, lessCorruption);
			}

			return result;
		}

		public int MaintenanceCostsRaw() {
			EffectiveBuildingsCache cache = GetEffectiveBuildingsCache();
			bool commercialUpkeepPaid = cache.wonders.paysTradeMaintenance;
			int result = 0;
			foreach (CityBuilding cb in constructed_buildings) {
				Building b = cb.building;
				// Civ3 charges nothing for a building one of the owner's
				// wonders also provides (a granary alongside the Pyramids),
				// and Smith's Trading Company pays for commercial buildings.
				if (cache.providedByWonders.Contains(b)
					|| (commercialUpkeepPaid && b.traits.Contains(Civilization.Trait.Commercial))) {
					continue;
				}
				result += b.maintenanceCost;
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

		// The city's share of its owner's treasury, by population: what a
		// conqueror, or barbarians sacking it, carry off.
		public int PlunderableGold() {
			int totalPopulation = owner.cities.Sum(c => c.residents.Count);
			return totalPopulation > 0 ? (int)((long)owner.gold * residents.Count / totalPopulation) : 0;
		}

		// Resisters still eat: resistance stops the city's shields and
		// commerce, not its farming (see IsInResistance).
		public int FoodConsumedPerTurn() {
			return residents.Count * 2;
		}

		// Civ3's resistance in a city taken by force. Some of the citizens of
		// the civ it was taken from resist: assumption, half of them, rounded
		// up (Civ3 also weighs the two civs' culture, which we don't). No one
		// resists for the barbarians.
		public void StartResistance(Player formerOwner) {
			if (formerOwner == null || formerOwner.isBarbarians || formerOwner == owner) {
				return;
			}
			int nationals = residents.Count(r => r.nationality == formerOwner.civilization);
			resisters = (nationals + 1) / 2;
			resistanceFrom = resisters > 0 ? formerOwner : null;
		}

		// Each turn every resister may give up, more likely the bigger the
		// garrison: each land defender in the city quells as many resisters
		// as the difficulty level's military law (usually 1), after the
		// Civ3 rule documented at
		// https://www.civfanatics.com/civ3/strategy/game-mechanics/the-inner-workings-of-resistance-revealed/.
		// As a simple stand-in for its odds, a resister gives up with chance
		// (1 + quelling) / (2 + resisters), at most 90%, so an ungarrisoned
		// city calms down over a few turns and a strong garrison ends it
		// quickly. Resistance also ends if the old owner is gone or has the
		// city back.
		public void UpdateResistance(GameData gameData) {
			if (resisters <= 0) {
				return;
			}
			if (resistanceFrom == null || resistanceFrom.defeated || resistanceFrom == owner) {
				EndResistance();
				return;
			}
			resisters = Math.Min(resisters, residents.Count);

			int garrison = location.unitsOnTile.Count(u => u.owner == owner && u.CanDefendOnLand());
			int quelling = garrison * Math.Max(1, gameData.gameDifficulty?.MilitaryLaw ?? 1);
			double chance = Math.Min(0.9, (1.0 + quelling) / (2.0 + resisters));
			int remaining = 0;
			for (int i = 0; i < resisters; ++i) {
				if (GameData.rng.NextDouble() >= chance) {
					++remaining;
				}
			}
			resisters = remaining;
			if (resisters == 0) {
				EndResistance();
				log.Information("Resistance in {City} has ended", this);
				if (owner.isHuman) {
					new MsgShowTemporaryPopup($"The resistance in {name} has been quelled.", location, owner).send();
				}
			}
		}

		private void EndResistance() {
			resisters = 0;
			resistanceFrom = null;
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
			(int improvements, int wonders) = GetCulturePerTurnBySource();
			return improvements + wonders;
		}

		// GetCulturePerTurnRaw split into what the city's ordinary buildings
		// and its wonders (great and small) make.
		public (int improvements, int wonders) GetCulturePerTurnBySource() {
			// The year can't change during the loop, so look it up once.
			int currentGameYear = CurrentGameYear();
			EffectiveBuildingsCache cache = GetEffectiveBuildingsCache();
			int improvements = 0;
			int wonders = 0;
			for (int i = 0; i < cache.buildings.Count; ++i) {
				CityBuilding cb = cache.buildings[i];
				// Only the owner's own buildings make culture: a captured
				// city's wonders make none for its captor.
				if (cb.builtByPlayer != null && cb.builtByPlayer != owner) {
					continue;
				}
				// Buildings granted by wonders never get old.
				bool granted = i >= cache.grantedStart;
				int culture = cb.building.culturePerTurn * (granted ? 1 : AgeMultiplier(cb, currentGameYear));
				if (cb.building.IsGreatWonder() || cb.building.isSmallWonder) {
					wonders += culture;
				} else {
					improvements += culture;
				}
			}
			return (improvements, wonders);
		}

		private static int CurrentGameYear() {
			GameData gameData = EngineStorage.gameData;
			return gameData?.timeOptions?.GetRawNumber(gameData.turn) ?? 0;
		}

		// A building's culture doubles once it is more than a thousand years
		// old, and only once: Civ3 saves show a temple from 1830 BC making 4
		// culture a turn in 230 AD, not 8, and a library from 620 AD still
		// making 3 in 1620 AD.
		private static int AgeMultiplier(CityBuilding cb, int currentGameYear) {
			return currentGameYear - cb.year > 1000 ? 2 : 1;
		}

		// Conquests tourism: the gold a turn this city's great wonders with
		// the tourist attraction flag earn its owner once they're old enough.
		// The table is the Civilopedia's (GCON_Tourist_Attraction).
		//
		// Assumptions: the gold goes straight to the treasury, untouched by
		// corruption or the tax rate; a wonder still attracts tourists once
		// obsolete and after its city changes hands; and the bands start at
		// 1000 years inclusive.
		public int TourismGold() {
			int currentGameYear = CurrentGameYear();
			int result = 0;
			foreach (CityBuilding cb in constructed_buildings) {
				if (cb.building.touristAttraction && cb.building.IsGreatWonder()) {
					result += TourismGoldForAge(currentGameYear - cb.year);
				}
			}
			return result;
		}

		internal static int TourismGoldForAge(int years) {
			if (years < 1000) return 0;
			if (years <= 1500) return 2;
			if (years <= 1750) return 4;
			if (years <= 1875) return 6;
			if (years <= 2000) return 8;
			if (years <= 2250) return 10;
			if (years <= 2500) return 12;
			return 14;
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
			// A new power plant replaces any other one in the city.
			if (building.replacesOtherBuildings) {
				constructed_buildings.RemoveAll(cb => cb.building.replacesOtherBuildings);
			}
			constructed_buildings.Add(new CityBuilding {
				building = building,
				builtByPlayer = owner,
				year = CurrentGameYear(),
				totalCulture = 0
			});
			// The Apollo Program is remembered for the world, so that
			// SpaceRace.ApolloProgramBuilt needn't search the cities for it.
			if (building.buildSpaceshipParts) {
				EngineStorage.gameData?.GreatWondersBuilt.Add(building.name);
			}
			owner?.OnBuildingsChanged();
		}
		public void RemoveBuilding(CityBuilding building) {
			constructed_buildings.Remove(building);
			owner?.OnBuildingsChanged();
		}

		public void AddUnit(UnitPrototype proto, GameData gameData) {
			MapUnit newUnit = proto.GetInstance(gameData.GenerateID(proto.name), proto, owner, location: location);
			newUnit.experienceLevelKey = gameData.defaultExperienceLevelKey;
			newUnit.experienceLevel = gameData.defaultExperienceLevel;
			newUnit.hitPointsRemaining = newUnit.maxHitPoints;

			location.unitsOnTile.Add(newUnit);
			TileChangeJournal.Record(location);
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
		internal float CalculateRankCorruption(int adjustedOptimalCityNumber, int mapOptimalCityNumber, int numAntiCorruptionBuildings) {
			int rank = rankIndex;
			if (owner.government.corruptionType == Government.CorruptionType.Communal) {
				rank = owner.cities.Count / 2;
			}

			// Each courthouse or police station raises the city's optimal
			// city number by a quarter of the map's, unaffected by difficulty
			// level, government or wonders. This fits the Civ3 saves better
			// than raising the adjusted number by 25%.
			float nOpt = Math.Max(
				1,
				adjustedOptimalCityNumber + .25f * mapOptimalCityNumber * numAntiCorruptionBuildings);

			if (rank < nOpt) {
				return rank / (2 * nOpt);
			} else {
				return (2 * rank - nOpt) / (2 * nOpt);
			}
		}

		public void CalculateCorruption(GameData gameData) {
			CalculateCorruption(gameData, owner.GetAdjustedOptimalCityNumber(gameData));
		}

		// Scales every city's corruption and waste before the cap. Civ3's
		// formula is 1; we run slightly below it to go easier on players.
		internal const float CorruptionScale = 0.9f;

		// The adjusted optimal city number is empire-wide, so when updating
		// every city Player.DoCorruptionCalculations works it out once and
		// passes it in rather than rescanning the empire for each city.
		internal void CalculateCorruption(GameData gameData, int adjustedOptimalCityNumber) {
			int numAntiCorruptionBuildings = 0;

			// Civ3's Secret Police HQ is a second Forbidden Palace: the BIQ
			// gives it the same "Forbidden Palace" flag, so it counts here and
			// as a center of the empire for distance corruption.
			int numCorruptionReducingSmallWondersInCity = 0;
			foreach (CityBuilding cb in EffectiveBuildings()) {
				if (cb.building.reducesCorruption) {
					++numAntiCorruptionBuildings;
				}
				if (cb.building.isForbiddenPalace) {
					++numCorruptionReducingSmallWondersInCity;
				}
			}

			corruption = (CalculateDistanceCorruption(gameData, numAntiCorruptionBuildings)
					+ CalculateRankCorruption(adjustedOptimalCityNumber, gameData.map.optimalNumberOfCities, numAntiCorruptionBuildings))
					* CorruptionScale;
			// Policemen are applied to the corrupt amounts themselves, see
			// RecoverWithPolicemen.

			// Corruption maxes out at 90%, and this max can be reduced further
			// via courthouses/police stations, and the forbidden palace/SPHQ.
			float maxCorruption = Math.Max(
				0,
				.9f - (.1f * numAntiCorruptionBuildings + .7f * numCorruptionReducingSmallWondersInCity));
			// The difficulty level scales humans' corruption (100% in the
			// standard rules); like its optimal city percentage, the AI is
			// unaffected. 0 is taken as unset, as older games saved it so.
			if (owner.isHuman && gameData.gameDifficulty != null && gameData.gameDifficulty.CorruptionPercentage > 0) {
				corruption *= gameData.gameDifficulty.CorruptionPercentage / 100f;
			}
			corruption = Math.Max(corruption, 0);
			corruption = Math.Min(corruption, maxCorruption);

			// The capital is rank 0, so the next CitiesFreeOfCorruption
			// cities nearest it are ranks 1 to CitiesFreeOfCorruption.
			Rules rules = gameData.rules;
			if (rules != null && rules.CoreCitiesFreeOfCorruption && rankIndex <= rules.CitiesFreeOfCorruption) {
				corruption = 0;
			}
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

		// The net number of unhappy citizens made content (negative: content
		// citizens made unhappy) by the buildings in this city and by the
		// owner's great wonders:
		//  - each building's own faces in its city (the Temple's one, the
		//    Hanging Gardens' three, Shakespeare's Theater's eight, ...);
		//  - a wonder that doubles a building (the Oracle for Temples, the
		//    Sistine Chapel for Cathedrals) adds that building's faces again
		//    in every city of the owner that has it, wonder-granted ones
		//    (Temple of Artemis) included;
		//  - a wonder's faces in all cities (the Hanging Gardens' one, Cure
		//    for Cancer's one, JS Bach's two), limited to the wonder's
		//    continent when it has continental mood effects (JS Bach).
		// An obsolete great wonder does none of this, not even in its own city.
		//
		// Assumption: the all-cities faces don't add to the wonder's own city,
		// which only gets its in-city faces. That matches the Civilopedia: the
		// Hanging Gardens (3 in city, 1 in all) make "three unhappy citizens
		// content in its city and one ... in all other friendly cities", and
		// JS Bach (2 and 2) "decreases unhappy citizens by two per city".
		// Assumption: the all-cities faces only reach the owner's cities; Cure
		// for Cancer has no flag for other civs and the Conquests Civilopedia
		// just says "in every city".
		internal int BuildingContentFaces(List<CityBuilding> buildings) {
			int result = 0;
			foreach (CityBuilding cb in buildings) {
				if (cb.building.isGreatWonderObsolete(owner)) {
					continue;
				}
				result -= cb.building.unhappyFacesInCity;
				result += cb.building.contentFacesInCity;
			}

			foreach ((City c, CityBuilding cb) in owner.GetBuildingSnapshot().activeWonders) {
				Building wonder = cb.building;
				if (wonder.doublesHappinessOf != null) {
					foreach (CityBuilding doubled in buildings) {
						if (doubled.building == wonder.doublesHappinessOf) {
							result += doubled.building.contentFacesInCity;
							break;
						}
					}
				}
				if (wonder.contentFacesAllCities != 0 && c != this
					&& (!wonder.continentalMoodEffects || c.location.continent == location.continent)) {
					result += wonder.contentFacesAllCities;
				}
			}
			return result;
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

			// TODO: add penalty for drafting, once drafting is implemented
			// (Government.draftLimit is imported but nothing drafts yet).

			// War weariness makes citizens unhappy, like pop rushing.
			contentToHappyMoves -= owner.WarWearinessUnhappiness(this);

			List<CityBuilding> buildings = EffectiveBuildings();
			// TODO: add penalty for aggression against home country

			// Building and wonder happiness/unhappiness, which only affects the
			// unhappy to content transition, nothing with happy faces.
			unhappyToContentMoves += BuildingContentFaces(buildings);

			// Depending on the government type, land defensive units can serve
			// as military police.
			int landDefenders = 0;
			foreach (MapUnit unit in location.unitsOnTile) {
				if (unit.CanDefendOnLand()) {
					++landDefenders;
				}
			}
			// Each makes as many citizens content as the difficulty level's
			// military law (1 in the standard rules).
			// 0 is taken as unset, as older games saved it so.
			int militaryLaw = Math.Max(1, gameData.gameDifficulty?.MilitaryLaw ?? 1);
			unhappyToContentMoves += Math.Min(owner.government.militaryPoliceLimit, landDefenders) * militaryLaw;

			// Luxury spending moves content faces to happy faces, one face for
			// every luxury (see the civfanatics thread above: "one luxury
			// happiness point affects one citizen").
			//
			// Don't respect civil disorder during this calculation, because if
			// we are currently in civil disorder our commerce is all corrupt,
			// but we still need to be able to calculate whether a certain
			// luxury slider value would get us out of civil disorder.
			contentToHappyMoves += CurrentCommerceYield(respectCivilDisorder: false).happiness;

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

			// A city riots when happy - unhappy < 0; content citizens count as
			// 0. Specialists take no part: only laborers' moods are reset
			// above, so a specialist's mood is stale and must be ignored.
			int happyCount = 0;
			int unhappyCount = 0;
			foreach (CityResident cr in residents) {
				if (!cr.citizenType.IsDefaultCitizen) {
					continue;
				}
				if (cr.mood == CityResident.Mood.Happy) { ++happyCount; }
				if (cr.mood == CityResident.Mood.Unhappy) { ++unhappyCount; }
			}
			if (happyCount - unhappyCount < 0) {
				return Mood.Unhappy;
			} else {
				return Mood.Happy;
			}
		}

		// The resources this city can use: those on its own trade network
		// segment, plus, for cities connected to the capital, those imported
		// from other civs through deals, less those exported to them.
		//
		// The returned dictionary may be shared with other cities, so it must
		// not be modified.
		public Dictionary<Resource, int> GetAvailableResources(GameData gameData) {
			C7Engine.Pathing.TradeNetwork network = gameData.GetTradeNetwork();
			Dictionary<Resource, int> local = network.GetResourcesAvailableToCity(owner, this);
			if (!network.ConnectedToCapital(owner, this)) {
				return local;
			}
			List<ResourceDeal> inbound = TradeReport.ResourceDeals(gameData, owner, DealDetails.Inbound);
			List<ResourceDeal> outbound = TradeReport.ResourceDeals(gameData, owner, DealDetails.Outbound);
			if (inbound.Count == 0 && outbound.Count == 0) {
				return local;
			}

			Dictionary<Resource, int> result = new(local);
			foreach (ResourceDeal deal in inbound) {
				if (owner.KnowsAboutResource(deal.resource)) {
					result[deal.resource] = result.GetValueOrDefault(deal.resource) + 1;
				}
			}
			foreach (ResourceDeal deal in outbound) {
				if (result.TryGetValue(deal.resource, out int count)) {
					if (count <= 1) {
						result.Remove(deal.resource);
					} else {
						result[deal.resource] = count - 1;
					}
				}
			}
			return result;
		}

		private Dictionary<Resource, int> ListResourceAccess(GameData gameData, ResourceCategory category) {
			Dictionary<Resource, int> availableResources = GetAvailableResources(gameData);
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

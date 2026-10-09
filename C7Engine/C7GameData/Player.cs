using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using C7Engine.AI.StrategicAI;
using C7Engine;
using MoonSharp.Interpreter;
using Serilog;
using static C7GameData.EraUtils;
using static C7GameData.MultiTurnDeal;
using static C7GameData.PlayerRelationship;
using static C7GameData.Tile;

namespace C7GameData {

	public struct PlayerCommerceBreakdown {
		public int corrupted;           // Amount of commerce lost directly to corruption
		public int taxes;               // Amount of treasury income from REGULAR citizens working tiles
		public int taxmenTaxes;         // Amount of treasury income from tax collector specialists
		public int beakers;             // Amount of commerce going to science
		public int happiness;           // Amount of commerce going to entertainment
		public int fromOtherCivs;       // Income from other Civ GPT deals
		public int toOtherCivs;         // Expenses paid to other Civ GPT deals
		public int interest;            // Interest income from Wall Street-flag small wonder
		public int maintenance;         // Expenses due to aggregate building maintenance
		public int unitSupport;         // Expenses due to unit support costs
		public int wealthProduction;    // Amount of extra commerce from "building" an Inflow that produces commerce
		public int tourism;             // Gold from old great wonders that are tourist attractions (Conquests)

		public int Inflows() {
			return corrupted + taxes + taxmenTaxes + beakers + happiness + fromOtherCivs + interest + wealthProduction + tourism;
		}

		public int Outflows() {
			return corrupted + beakers + happiness + toOtherCivs + maintenance + unitSupport;
		}

		public int Netflows() {
			return Inflows() - Outflows();
		}

		public int CityInflows() {
			return corrupted + taxes + beakers + happiness + wealthProduction;
		}
	}

	// A set of techs that also has an order, which enumerating it follows
	// (plain HashSets only happen to enumerate in insertion order). It is a
	// HashSet so callers can still test membership quickly; don't add to or
	// remove from it.
	public sealed class OrderedTechSet : HashSet<Tech>, IEnumerable<Tech> {
		private readonly List<Tech> ordered;

		// The techs must not contain duplicates.
		public OrderedTechSet(List<Tech> ordered) : base(ordered) {
			this.ordered = ordered;
		}

		public new List<Tech>.Enumerator GetEnumerator() {
			return ordered.GetEnumerator();
		}

		IEnumerator<Tech> IEnumerable<Tech>.GetEnumerator() {
			return ordered.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return ordered.GetEnumerator();
		}
	}

	// O(1) checks that a List or HashSet hasn't been modified since an
	// enumerator was taken from it. Their enumerators are documented to throw
	// from MoveNext once the collection has been modified (and List counts
	// replacing an entry as a modification), so trying to move a copy of the
	// stored enumerator tells us, without walking the collection. (The
	// enumerator is passed by value, so the stored one never moves.) The
	// exception is only thrown, and caught, once per change.
	internal static class CollectionVersion {
		internal static bool Unchanged<T>(List<T>.Enumerator enumerator) {
			try {
				enumerator.MoveNext();
				return true;
			} catch (InvalidOperationException) {
				return false;
			}
		}

		internal static bool Unchanged<T>(HashSet<T>.Enumerator enumerator) {
			try {
				enumerator.MoveNext();
				return true;
			} catch (InvalidOperationException) {
				return false;
			}
		}
	}

	public class Alliance {
		public int index;
		public string name;

		public Alliance(int index, string name) {
			this.index = index;
			this.name = name;
		}
	}

	public class Player {
		private static ILogger log = Log.ForContext<Player>();

		public ID id { get; internal set; }
		public int primaryColorIndex;
		public int secondaryColorIndex;
		public bool isBarbarians { get => civilization.isBarbarian; }
		//TODO: Refactor front-end so it sends player GUID with requests.
		//We should allow multiple humans, this is a temporary measure.
		public bool isHuman = false;
		public bool hasPlayedThisTurn = false;
		public bool skipFirstTurn = false;

		// The name of the person playing, for human players in a hotseat game.
		// Null when no name was given.
		public string name;

		// Has this player been defeated?
		public bool defeated = false;

		public Civilization civilization;

		// Answers if this player-civ is simply included in the game.
		// Some .biq scenarios contain players/civs in their data
		// that are not a part of the gameplay.
		// ex. Mongols in `4 Middle Ages.biq` scenario.
		public bool isIncludedInGame = true;

		// Answers if the human player can pick and play as this player-civ
		// or is it only an AI player
		public bool canBePicked = true;

		public List<MapUnit> units = new List<MapUnit>();
		public List<City> cities = new List<City>();
		public TileKnowledge tileKnowledge { get; private set; }

		// On a LAN guest's machine that isn't sent all of the game, this
		// player's totals as the host worked them out (see HostFacts); null
		// otherwise.
		public Save.PlayerFacts hostFacts;

		//Ordered list of priority data.  First is most important.
		public List<StrategicPriority> strategicPriorityData = new List<StrategicPriority>();

		// A map from player id to the relationship this player has with the other player.
		public Dictionary<ID, PlayerRelationship> playerRelationships = new();

		// The list of techs known by this player.
		public HashSet<ID> knownTechs = new();

		// A mapping of resource types to their corresponding tiles within player territory.
		// Provides lookup of controlled resources without scanning the entire map.
		public Dictionary<Resource, List<Tile>> resourcesInBorders;

		// The tech the player is currently researching.
		public ID currentlyResearchedTech { get; private set; }

		// A queue of technologies the player has specified they want to research
		public Queue<Tech> ResearchQueue { get; private set; } = new();

		// The civilopedia name of the era this player is in.
		//
		// The civilopedia name is what is used for art lookups, not the actual
		// name.
		public string eraCivilopediaName;

		public int turnsUntilPriorityReevaluation = 0;

		// The values of the science/happiness/tax sliders (tax is implicit)
		// A value of 1 => 10%, a value of 10 => 100%.
		//
		// INVARIANT: LuxuryRate + ScienceRate + TaxRate = 10
		public int luxuryRate = 0;
		public int scienceRate = 5;
		public int taxRate = 5;

		// The government caps how high any one slider can go.
		public int maxRate => Math.Clamp(government?.rateCap ?? 10, 0, 10);
		public int maxScienceRate => maxRate;
		public int minScienceRate { get; private set; } = 0;
		public int maxLuxuryRate => maxRate;
		public int minLuxuryRate { get; private set; } = 0;

		// Moves slider points above the government's rate cap to sliders that
		// still have room, preferring tax, then science, then luxury. If the
		// cap is too low for the three sliders to add up to 10, the leftover
		// stays in tax.
		public void ApplyGovernmentRateCap() {
			int excess = 0;
			int Trim(int rate) {
				if (rate <= maxRate) {
					return rate;
				}
				excess += rate - maxRate;
				return maxRate;
			}
			int Fill(int rate) {
				int moved = Math.Min(excess, Math.Max(0, maxRate - rate));
				excess -= moved;
				return rate + moved;
			}

			taxRate = Trim(taxRate);
			scienceRate = Trim(scienceRate);
			luxuryRate = Trim(luxuryRate);

			taxRate = Fill(taxRate);
			scienceRate = Fill(scienceRate);
			luxuryRate = Fill(luxuryRate);
			taxRate += excess;
		}

		// The amount of gold this player has. It never goes below zero: the
		// callers that spend gold (hurrying, espionage, upgrades, deals) check
		// the treasury first, and the turn's deficit is handled by selling
		// buildings and disbanding units. Should anything still overspend,
		// the treasury is emptied and a warning logged rather than the turn
		// crashing.
		private int _gold = 0;
		public int gold {
			get => _gold;
			set {
				if (value < 0) {
					log.Warning("Tried to set {Player}'s gold to {Gold}; emptying the treasury instead", this, value);
					value = 0;
				}
				_gold = value;
			}
		}

		private int lastGoldPerTurn = 0;

		/// <summary>
		/// Sets the current gold amount of the player. If the add parameter is true, the gold gets appended.<br/>
		/// </summary>
		/// <param name="amount">The number of gold to be added</param>
		/// <param name="add">If true, the gold gets appended, otherwise it overwrites the current value</param>
		/// Scripts may take more gold than there is; the treasury is then emptied.
		[LuaMethod]
		public void SetGold(int amount, bool add = false) {
			int newGold = add ? gold + amount : amount;
			this.gold = Math.Max(0, newGold);
		}

		// The number of "beakers" (gold) spent on the currently researched
		// tech.
		public int beakers = 0;

		// The number of turns the player has been researching the current tech.
		// Only turns that produced beakers count.
		public int turnsResearched = 0;

		// Whether this turn's finances put any beakers into research. Set by
		// DoPerTurnFinanceUpdates for DoPerTurnScienceUpdates, which runs
		// right after it, so it needn't be saved.
		private bool researchFundedThisTurn;

		// If the government is anarchy (or a govt with the transition bool set
		// to true), the turn number at which switching governments is allowed.
		public int inAnarchyUntilTurn = 0;

		// The current government of the player.
		public Government government;

		// The rules of this game.
		public Rules rules;

		public List<City> citiesWithCorruptionWonders = new();

		// The number of free techs this player has remaining. Free techs can
		// be awarded after researching a tech (like Philosophy) or after
		// completing a wonder (like Theory of Evolution).
		public int freeTechsRemaining = 0;

		// The tech a human player last discovered, while they haven't yet
		// chosen what to research next, for the science advisor to announce.
		// It isn't saved.
		public Tech lastDiscoveredTech;

		public Alliance alliance;

		// Whether one of this player's armies has won a battle. The Military
		// Academy can't be built until one has.
		public bool hasVictoriousArmy = false;

		// How many of each spaceship part (by Building.spaceshipPart index)
		// this civ has built. Losing the capital destroys them. See SpaceRace.
		public List<int> spaceshipParts = new();

		// War weariness points from saves made before they were kept per
		// enemy (see PlayerRelationship.warWearinessPoints), which can't be
		// put down to any one civ. They count like one more enemy's and fade
		// like any war's after peace. New points never go here.
		public int warWeariness = 0;

		// Each civ gets one golden age per game.
		public bool hadGoldenAge = false;
		public int goldenAgeTurnsRemaining = 0;

		// How many cities this player has founded, used to pick the next
		// name from the civ's city name list.
		public int citiesFounded = 0;

		// Whether this player has never held a city or a settler, like a
		// scenario civ that starts with only units. Such a civ isn't destroyed
		// for having no cities or settlers while it still has units. It's
		// worked out when a game is loaded (see SaveGame.ConvertCities) and
		// cleared once the player gains a city. A civ that loses its starting
		// settlers before founding a city is still destroyed.
		public bool neverHadCityOrSettler = false;

		public bool InGoldenAge => goldenAgeTurnsRemaining > 0;

		public int EraIndex() {
			return GetEraIndex(eraCivilopediaName);
		}

		public static int EraIndex(string era) {
			if (era == "ERAS_Ancient_Times") {
				return 0;
			} else if (era == "ERAS_Middle_Ages") {
				return 1;
			} else if (era == "ERAS_Industrial_Age") {
				return 2;
			} else if (era == "ERAS_Modern_Era") {
				return 3;
			}
			return -1;
		}

		public static string EraIndexToEra(int index) {
			if (index <= 0) {
				return "ERAS_Ancient_Times";
			} else if (index == 1) {
				return "ERAS_Middle_Ages";
			} else if (index == 2) {
				return "ERAS_Industrial_Age";
			} else {
				return "ERAS_Modern_Era";
			}
		}

		public void AddUnit(MapUnit unit) {
			this.units.Add(unit);
		}

		public void SetCurrentlyResearchedTech(ID id) {
			// Award a free tech if the player has one.
			if (id != null && freeTechsRemaining > 0) {
				--freeTechsRemaining;
				beakers = 0;
				turnsResearched = 0;

				Tech tech = Tech.FindById(EngineStorage.gameData.techs, id);
				log.Information("Awarding {Tech} to player {Player}", tech.Name, this);
				CompleteResearchAndBeginNew(EngineStorage.gameData, tech);
				return;
			}

			currentlyResearchedTech = id;
			if (id != null) {
				lastDiscoveredTech = null;
			}

			// Clear out previous progress.
			beakers = 0;
			turnsResearched = 0;
		}

		public void AddTechItemToResearchQueue(Tech tech) {
			ResearchQueue.Enqueue(tech);
		}

		private IEnumerable<string> CityNameGenerator() {
			List<string> cityNames = civilization?.cityNames;

			// A civ with no city names (as some scenarios define) gets
			// numbered names, so this never loops forever finding nothing.
			if (cityNames == null || cityNames.Count == 0) {
				string baseName = civilization?.adjective;
				if (string.IsNullOrEmpty(baseName)) {
					baseName = civilization?.name;
				}
				if (string.IsNullOrEmpty(baseName)) {
					baseName = "City";
				}
				for (int n = 1; ; ++n) {
					yield return $"{baseName} {n}";
				}
			}

			int loopCounter = 0;

			// Perpetual generator expression to yield all city names lazily
			while (true) {
				foreach (string city in cityNames) {
					if (loopCounter == 0) yield return city;
					else if (loopCounter == 1) yield return $"New {city}";
					else {
						if (loopCounter % 2 == 0) yield return $"{city} {(loopCounter / 2) + 1}";
						else yield return $"New {city} {(loopCounter / 2) + 1}";
					}
				}
				loopCounter++;
			}
		}

		public string GetNextCityName() {
			// Names held by any city in the game, so a civ that lost a city
			// doesn't reuse its name while the conqueror still holds it.
			HashSet<string> cityNameHashSet = (EngineStorage.gameData?.cities ?? cities)
				.Select(city => city.name).ToHashSet();

			// Continue down the list from the last city founded rather than
			// restarting at the top, so lost cities' names aren't reused.
			return CityNameGenerator().Skip(citiesFounded).First(x => !cityNameHashSet.Contains(x));
		}

		public Player() {
			tileKnowledge = new TileKnowledge(this);
		}

		public bool HasExploredTile(Tile tile) {
			return this.tileKnowledge.knownTiles.Contains(tile);
		}

		// Whether paths for our units go only by what we've explored, taking
		// tiles we haven't to be passable: a human's always do, and an AI's
		// do with the AI fog of war (see C7Engine.AI.AIFogOfWar).
		public bool PathsByExploredMap => isHuman || (EngineStorage.aiFogOfWar && !isBarbarians);

		// Whether to tell this player news meant just for them, such as a
		// golden age beginning: a human, and while spectators may watch as
		// it (see EngineStorage.newsForComputerPlayers), a computer player.
		public bool IsToldNews => isHuman || (EngineStorage.newsForComputerPlayers && !isBarbarians);

		public bool IsAtPeaceWith(Player other) {
			return AtPeace(this, other);
		}

		public void EnsureRelationshipExists(Player other) {
			if (this.isBarbarians || other.isBarbarians || this.id == other.id || this.defeated || other.defeated)
				return;

			// If the mutual relationship is not established it means that the 2 civs
			// were not aware of each other (aka they just met), and therefore cannot be at war.
			// Initialize the relationship and establish peace automatically between them.
			if (!this.playerRelationships.ContainsKey(other.id) || !other.playerRelationships.ContainsKey(this.id)) {
				this.playerRelationships.TryAdd(other.id, new PlayerRelationship());
				other.playerRelationships.TryAdd(this.id, new PlayerRelationship());
				RegisterMultiTurnDeal(this, other, DEFAULT_PEACE);

				log.Information("Established first contact and relationship between players {Player} and {Other}", this, other);
			}
		}

		// afterWarning is true when the other civ told us to leave its
		// territory or face war, and we chose war: that is no sneak attack,
		// even though our units are inside its borders.
		// afterWarning: the other civ was warned, so this isn't a sneak attack.
		// provoked: the other civ brought the war on itself, so its people
		// don't rally against us.
		public void DeclareWarOn(Player other, int currentTurn, bool afterWarning = false, bool provoked = false) {
			EnsureRelationshipExists(other);

			// Check to see if there was a sneak attack - we consider a sneak
			// attack any attack where the player's units were inside the
			// borders of the civ they're declaring war on.
			bool isSneakAttack = !afterWarning && IsASneakAttackOn(other);

			// TODO: take into account broken right of passage, or other deals, etc?
			// Perhaps we need a dedicated method to calculate this.
			//
			// Refuse contact from the aggressor civ until enough turns have
			// elapsed. The exact civ3 mechanism here is unknown, so we just
			// pick some reasonable random number. To penalize sneak attacks we
			// use a higher upper bound.
			int refuseContactUntilTurn = currentTurn + GameData.rng.Next(5, isSneakAttack ? 16 : 12);

			DeclareWar(this, other, isSneakAttack, refuseContactUntilTurn, currentTurn);

			// The other civ's people rally against whoever declares war on
			// them, AI or human: war happiness, unless they provoked it, by
			// their nuclear weapons, their caught spies, or (per the project
			// owner) ignoring our demand to take their units out of our
			// territory (see WarWearinessWhenWarIsDeclaredOnUs).
			if (!isBarbarians && !provoked && playerRelationships.TryGetValue(other.id, out PlayerRelationship ourView)
				&& ourView.nuclearAtrocityCount == 0 && ourView.espionageIncidents == 0) {
				other.AddWarWeariness(this, WarWearinessWhenWarIsDeclaredOnUs);
			}

			// Whenever war is declared, re-evaluate priorities.
			turnsUntilPriorityReevaluation = 0;
			other.turnsUntilPriorityReevaluation = 0;
		}

		// Whether, on a tile known to the other player and inside their
		// borders, the top unit is ours.
		//
		// Rather than every tile the other player knows, this looks at the
		// tiles our units are on: a tile whose top unit is ours is one of
		// them, as each unit on a tile has that tile as its location and is
		// in its owner's list of units.
		private bool IsASneakAttackOn(Player other) {
			foreach (MapUnit unit in units) {
				Tile location = unit.location;
				if (location == null || location.owningCity == null || location.owningCity.owner != other) {
					continue;
				}
				if (location.unitsOnTile.Count == 0 || location.unitsOnTile[0].owner != this) {
					continue;
				}
				if (other.tileKnowledge.knownTiles.Contains(location)) {
					return true;
				}
			}

			return false;
		}

		public bool WillAcceptCommunicationFrom(Player other, int currentTurn) {
			EnsureRelationshipExists(other);

			if (EngineStorage.gameData.AreInLockedWar(this, other)) return false;

			PlayerRelationship pr = playerRelationships[other.id];
			if (AtPeace(this, other)) {
				return true;
			}
			return currentTurn >= pr.refuseContactUntilTurn;
		}

		public bool SitsOutFirstTurn() {
			return this.isBarbarians || this.skipFirstTurn;
		}

		public static bool CanMoveFreely(Player player, Tile sourceTile, Tile targetTile) {
			if (!player.HasExploredTile(targetTile))
				return true;

			Player targetTileOwner = targetTile.OwningPlayer();
			Player sourceTileOwner = sourceTile.OwningPlayer();

			// We are free to move if:
			// - the tile we are on is unowned, or we own the tile
			// - and the tile we are moving to is unowned, or we own the tile
			if ((sourceTileOwner == null || sourceTileOwner == player)
				&& (targetTileOwner == player || targetTileOwner == null))
				return true;

			// All the other cases are either from or to "enemy" tiles
			// and without a RoP agreement the cost is never reduced.
			// check other && RoP
			if (sourceTileOwner != null && sourceTileOwner != player
				&& HaveActiveRightOfPassage(player, sourceTileOwner)) {
				return true;
			}
			if (targetTileOwner != null && targetTileOwner != player
				&& HaveActiveRightOfPassage(player, targetTileOwner)) {
				return true;
			}

			return false;
		}

		public bool KnowsAboutResource(Resource resource) {
			if (resource.Prerequisite == null) {
				return true;
			}
			return knownTechs.Contains(resource.Prerequisite);
		}

		public int ArmyCount() {
			int result = 0;
			foreach (MapUnit u in units) {
				if (u.IsArmy()) {
					++result;
				}
			}
			return result;
		}

		// Whether the player has enough cities to support one more army: each
		// army needs CitiesNeededToSupportAnArmy cities.
		public bool CanSupportAnotherArmy() {
			int citiesPerArmy = rules?.CitiesNeededToSupportAnArmy ?? 0;
			return (ArmyCount() + 1) * citiesPerArmy <= RemainingCities();
		}

		// Whether this player's armies can carry an extra unit, thanks to a
		// building like the Pentagon.
		public bool HasLargerArmies() {
			return GetBuildingSnapshot().hasLargerArmies;
		}

		public int RemainingCities() {
			int result = 0;
			foreach (City city in cities) {
				// Destroyed cities have a size of zero.
				if (city.residents.Count > 0) {
					++result;
				}
			}
			return result;
		}

		public override string ToString() {
			if (civilization != null)
				return $"{civilization.name} [{this.id}]";
			return "";
		}

		[LuaMethod]
		public List<Tech> GetKnownTechs() {
			List<Tech> result = new();
			foreach (Tech tech in EngineStorage.gameData.techs) {
				if (knownTechs.Contains(tech.id)) {
					result.Add(tech);
				}
			}
			return result;
		}

		public PlayerCommerceBreakdown AggregateFlows() {
			var result = new PlayerCommerceBreakdown
			{
				corrupted = 0,
				taxes = 0,
				taxmenTaxes = 0,
				beakers = 0,
				happiness = 0,
				fromOtherCivs = 0,
				toOtherCivs = 0,
				interest = 0,
				maintenance = 0,
				unitSupport = 0,
				wealthProduction = 0
			};

			// If player has no cities, apply no expenses or income.
			// This is how this behaves in regular Civ 3 as well (if you're not defeated, e.g. you still have a King unit or settler)
			if (cities.Count == 0) return result;

			// Assume player has no buildings that generate interest income until we check
			int interestBuildings = 0;

			foreach (City city in cities) {
				AddCityFlows(ref result, ref interestBuildings, ComputeCityFlows(city));
			}

			return AddEmpireFlows(result, interestBuildings, TotalUnitsAllowedUnitsAndSupportCost().Item3);
		}

		// One city's contribution to AggregateFlows.
		private struct CityFlows {
			public CommerceBreakdown commerce;
			public int maintenance;
			public int interestBuildings;
			public int taxmenTaxes;
			public int tourism;
		}

		private static CityFlows ComputeCityFlows(City city) {
			CityFlows result = new();
			result.commerce = city.CurrentCommerceYield();
			result.maintenance = city.MaintenanceCosts();
			foreach (CityBuilding cb in city.constructed_buildings) {
				if (cb.building.treasuryEarnsInterest) {
					++result.interestBuildings;
				}
			}
			foreach (CityResident cr in city.residents) {
				result.taxmenTaxes += cr.citizenType.Taxes;
			}
			result.tourism = city.TourismGold();
			return result;
		}

		private static void AddCityFlows(ref PlayerCommerceBreakdown result, ref int interestBuildings, in CityFlows city) {
			result.corrupted += city.commerce.corrupted;
			result.taxes += city.commerce.taxes;
			result.beakers += city.commerce.beakers;
			result.happiness += city.commerce.happiness;
			result.maintenance += city.maintenance;
			result.wealthProduction += city.commerce.wealth;
			result.tourism += city.tourism;

			interestBuildings += city.interestBuildings;

			// Split city income into "regular citizen" and "tax collector" buckets
			result.taxes -= city.taxmenTaxes;
			result.taxmenTaxes += city.taxmenTaxes;
		}

		// Adds the parts of AggregateFlows that don't come from the cities.
		private PlayerCommerceBreakdown AddEmpireFlows(PlayerCommerceBreakdown result, int interestBuildings, int unitSupport) {
			foreach (var pr in playerRelationships.Values) {
				foreach (var mtd in pr.multiTurnDeals) {
					if (mtd.dealSubType == DealSubType.GoldPerTurn) {
						if (mtd.dealDetails == DealDetails.Inbound) result.fromOtherCivs += mtd.goldPerTurn;
						else if (mtd.dealDetails == DealDetails.Outbound) result.toOtherCivs += mtd.goldPerTurn;
					}
				}
			}

			result.unitSupport = unitSupport;

			if (interestBuildings > 0) result.interest = interestBuildings * Math.Min((int)(gold * rules.TreasuryInterestRate), rules.MaxInterest);

			return result;
		}

		// The same as CalculateGoldPerTurn, from per-city flows computed
		// earlier and the current unit support cost.
		private int GoldPerTurnFrom(CityFlows[] cityFlows, int unitSupport) {
			if (cityFlows.Length == 0) {
				return 0;
			}
			PlayerCommerceBreakdown result = new();
			int interestBuildings = 0;
			foreach (CityFlows flows in cityFlows) {
				AddCityFlows(ref result, ref interestBuildings, flows);
			}
			return AddEmpireFlows(result, interestBuildings, unitSupport).Netflows();
		}

		public int MaintenanceCosts() {
			int result = 0;
			foreach (City c in cities) {
				result += c.MaintenanceCosts();
			}
			return result;
		}

		// TODO: Add interest and GPT deals
		public int CalculateGoldPerTurn() {
			return AggregateFlows().Netflows();
		}

		// Whether we, an AI, would accept the deal, in which the other player
		// gives theirOffer and we give ourOffer. Cities count for nothing in
		// the valuation (see TradeOffer.GoldEquivalentFor), and per the
		// project owner we never give a city away except in a peace treaty.
		// Peace is weighed up by PeaceAI.
		public bool WouldAcceptDealFrom(GameData gameData, Player other, TradeOffer theirOffer, TradeOffer ourOffer) {
			// TODO: consider any factors like trade reputations here and culture groups
			if (TradeOffer.ProblemWithDeal(gameData, other, this, theirOffer, ourOffer) != null) {
				return false;
			}
			// We don't deal with a civ we refuse to talk to, as for a while
			// after a war starts.
			if (!WillAcceptCommunicationFrom(other, gameData.turn)) {
				return false;
			}
			int theirGoldValue = theirOffer.GoldEquivalentFor(gameData, this);
			int ourGoldValue = ourOffer.GoldEquivalentFor(gameData, this);

			if (theirOffer.partOfPeaceTreaty || ourOffer.partOfPeaceTreaty) {
				return C7Engine.AI.PeaceAI.WouldAcceptPeace(this, other, theirOffer, ourOffer, theirGoldValue, ourGoldValue);
			}
			if (ourOffer.cities.Count > 0) {
				return false;
			}
			return theirGoldValue >= ourGoldValue;
		}

		// Carries out a deal in which we give ourOffer and the other player
		// gives theirOffer. Returns false, changing nothing, if the deal can no
		// longer be made, such as when one side spent the gold it offered
		// while the other thought it over.
		public bool ExecuteDeal(GameData gameData, Player other, TradeOffer theirOffer, TradeOffer ourOffer) {
			string problem = TradeOffer.ProblemWithDeal(gameData, other, this, theirOffer, ourOffer);
			if (problem != null) {
				log.Warning("Not executing a trade between {Player} and {Other}: {Problem}", this, other, problem);
				return false;
			}
			log.Information("Executing trade between {Player} and {Other}", this, other);
			log.Information("  {Player} gives {Offer}, worth {Gold} gold", this, ourOffer.ToString(), ourOffer.GoldEquivalentFor(gameData, other));
			log.Information("  {Other} gives {Offer}, worth {Gold} gold)", other, theirOffer.ToString(), theirOffer.GoldEquivalentFor(gameData, this));
			if (theirOffer.partOfPeaceTreaty || ourOffer.partOfPeaceTreaty) {
				SignPeaceAfterWar(this, other, gameData);
			}

			if (ourOffer.gold.HasValue) {
				other.gold += ourOffer.gold.Value;
				this.gold -= ourOffer.gold.Value;
			}
			if (theirOffer.gold.HasValue) {
				this.gold += theirOffer.gold.Value;
				other.gold -= theirOffer.gold.Value;
			}

			other.CompleteResearchAndBeginNew(gameData, ourOffer.techs);
			this.CompleteResearchAndBeginNew(gameData, theirOffer.techs);

			// Cities change hands last, once the gold and techs have.
			foreach (City city in ourOffer.cities.ToList()) {
				CityInteractions.TransferCity(city, other, viaDeal: true);
			}
			foreach (City city in theirOffer.cities.ToList()) {
				CityInteractions.TransferCity(city, this, viaDeal: true);
			}
			return true;
		}

		// The beakers all our cities produce each turn.
		public int BeakersPerTurn() {
			int beakersPerTurn = 0;
			foreach (City city in cities) {
				beakersPerTurn += city.CurrentCommerceYield().beakers;
			}
			return beakersPerTurn;
		}

		public int EstimateTurnsToResearch(GameData gameData, Tech tech) {
			return EstimateTurnsToResearch(gameData, tech, BeakersPerTurn());
		}

		// Like EstimateTurnsToResearch, with the beakers per turn (from
		// BeakersPerTurn) computed once by a caller estimating many techs.
		public int EstimateTurnsToResearch(GameData gameData, Tech tech, int beakersPerTurn) {
			int remainingCost = gameData.TechCostFor(tech, this);
			if (remainingCost > 0 && beakersPerTurn == 0) {
				// No research is happening.
				return int.MaxValue;
			}

			// A tech that's already paid for is done, even with no science.
			int turnsRemaining = remainingCost <= 0 ? 0 : (int)Math.Ceiling((double)remainingCost / beakersPerTurn);

			int maxTurnsRemaining = rules.MaximumResearchTime - turnsResearched;
			int minTurnsRemaining = rules.MinimumResearchTime - turnsResearched;

			int result = Math.Min(turnsRemaining, maxTurnsRemaining);
			result = Math.Max(result, minTurnsRemaining);

			return result;
		}

		public string SummarizeScience(GameData gD) {
			Tech tech = Tech.FindById(gD.techs, currentlyResearchedTech);
			if (tech == null) {
				return "Not selected (-- turns)";
			}
			return SummarizeScience(tech, EstimateTurnsToResearch(gD, tech));
		}

		// Like SummarizeScience, with the beakers per turn (from
		// BeakersPerTurn) computed once by the caller.
		public string SummarizeScience(GameData gD, int beakersPerTurn) {
			Tech tech = Tech.FindById(gD.techs, currentlyResearchedTech);
			if (tech == null) {
				return "Not selected (-- turns)";
			}
			return SummarizeScience(tech, EstimateTurnsToResearch(gD, tech, beakersPerTurn));
		}

		private static string SummarizeScience(Tech tech, int turns) {
			if (turns == int.MaxValue) {
				return $"{tech.Name} (-- turns)";
			}

			return $"{tech.Name} ({turns} turns)";
		}

		// Get a fresh research queue that overrides any current or previous queues.
		// This queue is being reversed in the end, to provide the actual queue
		// that the player would go through
		public void CalculateFreshTechQueueAndAssignNewCurrent(Tech tech) {
			ResearchQueue.Clear();
			IEnumerable<Tech> techQueue = GetResearchQueueFor(tech, ResearchQueue, new HashSet<Tech>()).Reverse();
			ResearchQueue = new Queue<Tech>(techQueue);

			if (ResearchQueue.Count > 0)
				SetCurrentlyResearchedTech(ResearchQueue.Peek().id);
		}

		// Append a new queue at the tail end of the current queue
		public void CalculateTechQueueAndAppendToCurrentQueue(Tech tech) {
			IEnumerable<Tech> techQueue = GetResearchQueueFor(tech, new Queue<Tech>(), new HashSet<Tech>()).Reverse();
			HashSet<Tech> alreadyQueued = new(ResearchQueue);
			foreach (Tech t in techQueue) {
				if (alreadyQueued.Add(t)) {
					AddTechItemToResearchQueue(t);
				}
			}
		}

		/// <summary>
		/// This produces a queue, but in reverse order, going from the last tech to the first.
		/// We can't reverse when returning from this method, because of its recursive nature,
		/// so this must be done from the caller method.
		/// </summary>
		/// <param name="tech"></param>
		/// <param name="tempQueue">The queue being built.</param>
		/// <param name="inQueue">The techs in tempQueue, for fast lookups. Must start out matching tempQueue.</param>
		/// <returns></returns>
		private Queue<Tech> GetResearchQueueFor(Tech tech, Queue<Tech> tempQueue, HashSet<Tech> inQueue) {
			return GetResearchQueueFor(tech, tempQueue, inQueue, new HashSet<Tech>());
		}

		// expanded holds the techs this has already been called for. Once a
		// call for a tech returns, every unknown tech it leads back to is in
		// the queue, so calling it again would add nothing; skipping it keeps
		// a tech tree with many paths to the same tech (which the full tree
		// has) from being walked once per path, without changing the result.
		private Queue<Tech> GetResearchQueueFor(Tech tech, Queue<Tech> tempQueue, HashSet<Tech> inQueue, HashSet<Tech> expanded) {

			if (tech == null) {
				return new Queue<Tech>();
			}

			if (!expanded.Add(tech)) {
				return tempQueue;
			}

			List<Tech> requiredTechs = OrderTechs(tech.Prerequisites);

			// first, add the tech the user clicked
			if (inQueue.Add(tech)) {
				tempQueue.Enqueue(tech);
			}

			// second, get the direct required techs
			foreach (Tech t in requiredTechs) {
				if (!knownTechs.Contains(t.id)) {
					if (inQueue.Add(t)) {
						tempQueue.Enqueue(t);
					}
				}
			}

			// last, recursively get the required techs of these required techs
			foreach (Tech t in requiredTechs) {
				if (!knownTechs.Contains(t.id)) {
					if (t.Prerequisites.Count > 0) {
						GetResearchQueueFor(t, tempQueue, inQueue, expanded);
					}
				}
			}
			return tempQueue;
		}

		/// <summary>
		/// Takes all the techs in the game and keeps only what could be researched next at a particular point in the game.
		/// </summary>
		/// <param name="allTechs"></param>
		/// <returns>The techs, which enumerate in research priority order.</returns>
		public OrderedTechSet GetAvailableTechsToResearch(List<Tech> allTechs) {
			HashSet<Tech> result = new();
			foreach (Tech tech in allTechs) {
				if (knownTechs.Contains(tech.id)) {
					continue;
				}
				if (GetEraIndex(tech.EraCivilopediaName) > EraIndex()) {
					continue;
				}

				bool prereqsKnown = true;
				foreach (Tech prereq in tech.Prerequisites) {
					if (!knownTechs.Contains(prereq.id)) {
						prereqsKnown = false;
						break;
					}
				}
				if (prereqsKnown) {
					result.Add(tech);
				}
			}
			return new OrderedTechSet(OrderTechs(result.ToList()));
		}

		/// <summary>
		/// The techs this player knows that it could trade to the recipient: only
		/// techs the recipient could research right now.
		/// </summary>
		public List<Tech> GetTechsTradableTo(Player recipient, List<Tech> allTechs) {
			OrderedTechSet researchable = recipient.GetAvailableTechsToResearch(allTechs);
			return allTechs.FindAll(t => knownTechs.Contains(t.id) && researchable.Contains(t));
		}

		// Placeholder ordering of techs. Duplicates are dropped, keeping the
		// first.
		private static List<Tech> OrderTechs(List<Tech> techs) {
			if (techs == null || techs.Count == 0) {
				return new List<Tech>();
			}
			// TODO: We would want to eventually order them based on how the AI would do it
			// Details on how Civ3 does it: https://forums.civfanatics.com/threads/what-will-the-ai-research-next.45559/
			HashSet<Tech> seen = new();
			List<Tech> result = new();
			foreach (Tech t in techs.OrderBy(t => t.Cost)) {
				if (seen.Add(t)) {
					result.Add(t);
				}
			}
			return result;
		}

		public List<Government> GetAvailableGovernments(GameData gameData) {
			List<Government> result = new();
			foreach (Government g in gameData.governments) {
				// You can't deliberately switch into the transition type, you
				// have to switch from one non-transitional type to another.
				if (g.transitionType) {
					continue;
				}

				if (this.HasTech(g.prerequisiteTech)) {

					result.Add(g);
				}
			}
			return result;
		}

		// Whether the player knows any tech matching the predicate, e.g. one
		// with a rule flag like Tech.EnablesIrrigationEverywhere.
		[MoonSharpHidden]
		public bool KnowsTechWhere(Func<Tech, bool> predicate) {
			List<Tech> techs = EngineStorage.gameData?.techs;
			if (techs == null) {
				return false;
			}
			foreach (Tech t in techs) {
				if (predicate(t) && knownTechs.Contains(t.id)) {
					return true;
				}
			}
			return false;
		}

		public bool HasTech(ID techId) {
			bool hasTech = techId == null || this.knownTechs.Contains(techId);
			return hasTech;
		}

		// See https://forums.civfanatics.com/threads/everything-about-corruption-c3c-edition.76619/
		//
		// This is the empire-wide information factored out of the rank corruption
		// calculations so it can be reused for anarchy calculations.
		public int GetAdjustedOptimalCityNumber(GameData gameData) {
			int mapOptimalCityNumber = gameData.map.optimalNumberOfCities;
			// The difficulty level's percentage only applies to humans; the AI
			// always gets the full optimal city number. Fitted against 24k
			// cities from Civ3 saves: using the difficulty level for the AI
			// too made its corruption far too high on the hard levels. This
			// is deliberate, and the project owner confirmed it stays
			// human-only.
			int percentOptimalCities = isHuman ? gameData.gameDifficulty.PercentageOfOptimalCities : 100;

			float commercialCivFactor = civilization.traits.Contains(Civilization.Trait.Commercial) ? .25f : 0;

			// The Secret Police HQ carries the BIQ's Forbidden Palace flag, so
			// it counts here like a Forbidden Palace, under Communism (see
			// Building.WorksAsForbiddenPalaceFor).
			int numCorruptionReducingSmallWondersInEmpire = 0;
			foreach (City c in cities) {
				// We use constructed_buildings here because great wonders can't
				// supply small wonders like forbidden palaces, so we can avoid
				// doing extra work for each city.
				foreach (CityBuilding cb in c.constructed_buildings) {
					if (cb.building.WorksAsForbiddenPalaceFor(this)) {
						++numCorruptionReducingSmallWondersInEmpire;
					}
				}
			}

			float communalCorruptionFactor =
				government.corruptionType == Government.CorruptionType.Communal ? 3.0f : 3.0f/8.0f;

			float result = mapOptimalCityNumber * percentOptimalCities / 100.0f
				  * (1 + commercialCivFactor + OptimalCityGovernmentFactor() + communalCorruptionFactor * numCorruptionReducingSmallWondersInEmpire);
			return (int)result;
		}

		// How much the government adds to the optimal city number.
		private float OptimalCityGovernmentFactor() {
			return government.corruptionType switch {
				Government.CorruptionType.Minimal => .1f,
				Government.CorruptionType.Nuisance => .1f,
				Government.CorruptionType.Problematic => 0,
				Government.CorruptionType.Rampant => 0,
				Government.CorruptionType.Catastrophic => 0, // anarchy, special cased
				Government.CorruptionType.Communal => 2,
				Government.CorruptionType.Off => 0
			};
		}

		// Notes:
		//  - see https://www.civforum.de/showthread.php?3153-Anarchie-Wieviel-Runden-welche-Strategie&p=67682&viewfull=1#post67682 (in German)
		//    This claims Soren Johnson said the time is
		//    1-5 years, random + 0-3 years, depending on the number of cities.
		//    I think the 1-5 is actually 2 to 6, since the min is 2.
		//
		//  - https://forums.civfanatics.com/threads/frequently-asked-questions-civ3-play-the-world-conquests.170282/
		//    For Religious civilizations, anarchy only lasts 1 turn in Vanilla
		//    and Play the World, and 2 turns in Conquests. For non-Religious
		//    civilizations, the formula is: 1 (2 for Conquests) + random number
		//    between 1-4 + number between 0-3 depending on size of your empire.
		//
		//  - https://civfanatics.com/civ3/faq/ confirms religious civs get 2
		//    turns in Conquests, and
		//    https://forums.civfanatics.com/threads/duration-of-anarchy.683384/
		//    measured Conquests, finding the FAQ slightly off: "Very small
		//    empires incur 2 to 6 turns of anarchy", "Small empires incur 3 to
		//    7 turns of anarchy, mid sized empires 4 to 8 turns and larger
		//    empires 5 to 9 turns", "Religious tribes always incur 2 turns of
		//    anarchy". That is 2 + a random 0 to 4 + 0 to 3 for the empire's
		//    size, which we follow.
		//
		// How the empire size maps to 0-3 isn't documented: UNVERIFIED (no
		// Civ3 source found), we scale it by the optimal city number.
		// The Conquests base length of anarchy, which is all a religious civ
		// suffers.
		private const int AnarchyBaseTurns = 2;

		public int GetTurnsOfAnarchyForTransition(GameData gameData) {
			if (civilization.traits.Contains(Civilization.Trait.Religious)) {
				return AnarchyBaseTurns;
			}

			// Conquests, as measured above: a base of 2 plus a random 0 to 4,
			// so 2 to 6 turns before the empire size is counted.
			int randomPortion = AnarchyBaseTurns + GameData.rng.Next(5);

			// Now we use the OCN to determine the city factor, which is between
			// 0 and 3. This means that sprawling empires will have longer
			// anarchy, but this will scale with map size.
			float numCities = cities.Count;
			int cityPortion = (int)Math.Min(3f, 3f * numCities / GetAdjustedOptimalCityNumber(gameData));

			// The result is the sume of the random portion and the city portion,
			// with the AI cap applied if relevant for this difficulty.
			//
			// Note that this AI transition time is applied after the early
			// return for religious civs. There is a known strategy on higher
			// difficulty levels of using religious civs to help cut down on the
			// AI advantage due to this fact.
			int result = randomPortion + cityPortion;
			if (!isHuman && gameData.gameDifficulty.MaxAiGovernmentTransitionTime > 0) {
				result = Math.Min(result, gameData.gameDifficulty.MaxAiGovernmentTransitionTime);
			}
			return result;
		}

		public void DoPerTurnFinanceUpdates(GameData gameData) {
			if (isBarbarians) {
				return;
			}

			// Process per-city contributions. Each city's flows are computed
			// once: disbanding a unit only changes the unit support cost, and
			// losing an improvement only changes the flows of its own city
			// (wonders, small wonders and the palace are never lost here).
			CityFlows[] cityFlows = new CityFlows[cities.Count];
			int beakersThisTurn = 0;
			for (int i = 0; i < cities.Count; ++i) {
				cityFlows[i] = ComputeCityFlows(cities[i]);
				beakersThisTurn += cityFlows[i].commerce.beakers;
			}
			beakers += beakersThisTurn;
			researchFundedThisTurn = beakersThisTurn > 0;
			int unitSupportCost = TotalUnitsAllowedUnitsAndSupportCost().Item3;

			// As in Civ 3, a deficit is fine while the treasury can pay for it.
			// When it can't, units over the support limit are disbanded and
			// then city improvements are lost, but the sliders are left alone.
			List<string> disbandedUnits = new();
			List<string> lostImprovements = new();
			while (gold + GoldPerTurnFrom(cityFlows, unitSupportCost) < 0) {
				// Disband one unit at a time and check the budget again, so we
				// never disband more than needed. Captives are free, so
				// disbanding one wouldn't lower the support cost.
				int disbandableCount = 0;
				foreach (MapUnit u in units) {
					if (!u.IsCaptive()) {
						++disbandableCount;
					}
				}
				if (unitSupportCost > 0 && disbandableCount > 0) {
					MapUnit unitToRemove = NonCaptiveUnitAt(GameData.rng.Next(disbandableCount));
					log.Information("{Player} is out of gold, disbanding {Unit} at {Location}", this, unitToRemove, unitToRemove.location);
					disbandedUnits.Add(unitToRemove.name);
					gameData.RemoveUnit(unitToRemove);
					unitSupportCost = TotalUnitsAllowedUnitsAndSupportCost().Item3;
					continue;
				}

				// Then give up the improvement that costs the most to maintain.
				(City city, CityBuilding building) = MostExpensiveImprovementToMaintain();
				if (city != null) {
					log.Information("{Player} is out of gold, losing the {Building} in {City}", this, building.building.name, city);
					lostImprovements.Add($"{building.building.name} in {city.name}");
					city.RemoveBuilding(building);
					new MsgCityChanged(city).send();
					cityFlows[cities.IndexOf(city)] = ComputeCityFlows(city);
					unitSupportCost = TotalUnitsAllowedUnitsAndSupportCost().Item3;
					continue;
				}

				// Nothing left to give up, for example when the deficit comes
				// from gold-per-turn deals. The treasury bottoms out at zero.
				log.Warning("{Player} was unable to get the budget under control despite disbanding units and losing improvements (gold={Gold}, gpt={GoldPerTurn})",
					this, gold, GoldPerTurnFrom(cityFlows, unitSupportCost));
				break;
			}

			if (IsToldNews && lostImprovements.Count > 0) {
				new MsgShowDomesticAdvisorPopup(this,
					$"We can no longer support our {string.Join(", ", lostImprovements)}.\nWe must think more about our treasury!").send();
			}
			if (IsToldNews && disbandedUnits.Count > 0) {
				new MsgShowMilitaryAdvisorPopup(this,
					$"We have insufficient gold to continue supporting all our units.\nWe had to disband: {string.Join(", ", disbandedUnits)}.", happy: false).send();
			}

			lastGoldPerTurn = GoldPerTurnFrom(cityFlows, unitSupportCost);
			gold = Math.Max(0, gold + lastGoldPerTurn);
		}

		// The index'th unit that isn't a captive, in the order of the units
		// list.
		private MapUnit NonCaptiveUnitAt(int index) {
			foreach (MapUnit u in units) {
				if (u.IsCaptive()) {
					continue;
				}
				if (index == 0) {
					return u;
				}
				--index;
			}
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		// The improvement (not the palace or a wonder) with the highest
		// maintenance cost, or (null, null) if nothing costs anything.
		private (City, CityBuilding) MostExpensiveImprovementToMaintain() {
			City bestCity = null;
			CityBuilding best = null;
			foreach (City c in cities) {
				foreach (CityBuilding cb in c.constructed_buildings) {
					Building b = cb.building;
					if (b.maintenanceCost <= 0 || b.isCenterOfEmpire || b.isSmallWonder || b.IsGreatWonder()) {
						continue;
					}
					if (best == null || b.maintenanceCost > best.building.maintenanceCost) {
						bestCity = c;
						best = cb;
					}
				}
			}
			return (bestCity, best);
		}

		public void HandleCityUpdates(GameData gameData) {
			foreach (City c in cities) {
				// Ensure borders expand before we assign the new citizen, so that
				// the new citizen can go on one of our new tiles.
				if (c.UpdateCultureAndCheckForExpansion()) {
					gameData.UpdateTileOwners();
					BarbarianInteractions.DisperseCampsWithinBorders(gameData);

					// Update the trade network if borders expanded, as a new
					// resource may be part of the network.
					EngineStorage.gameData.InvalidateCachedTradeNetwork();
				}

				c.UpdateResistance(gameData);
				c.HandleCityGrowth(gameData);
				c.HandleCityProduction(gameData);
			}
		}

		public void DoPerTurnScienceUpdates(GameData gameData) {
			if (currentlyResearchedTech == null) {
				return;
			}

			// Only turns that put beakers into the tech count towards the
			// maximum research time: "You cannot use up more than 50 turns of
			// actual research, no matter how little you spend, as long as you
			// spend more than zero"
			// (https://forums.civfanatics.com/threads/the-research-slider.662300/).
			// Otherwise a civ that stopped funding research would, once it
			// started again, finish the tech at once because the clamp had
			// run out.
			if (researchFundedThisTurn) {
				turnsResearched++;
			}
			researchFundedThisTurn = false;

			// Check to see if the player has finished researching their
			// tech, and if they have, add it to the list of known techs
			Tech tech = Tech.FindById(gameData.techs, currentlyResearchedTech);
			if (EstimateTurnsToResearch(gameData, tech) > 0) {
				return;
			}

			CompleteResearchAndBeginNew(gameData, tech);
		}

		// The Great Library: each turn, while it is active, its owner learns
		// every tech known by at least two other civs it has contact with.
		//
		// Assumptions, as the exact Civ3 rule isn't documented in detail:
		//  - "contact" means we have a relationship with the civ (we have met
		//    them), regardless of whether we are at war with them;
		//  - barbarians and defeated civs don't count;
		//  - only techs we could research ourselves right now (prerequisites
		//    known, not beyond our era) are given, as with tech trading. Each
		//    tech learned can open up the next, so we repeat until nothing new
		//    qualifies.
		// Returns the techs learned.
		public List<Tech> DoGreatLibraryUpdates(GameData gameData) {
			List<Tech> learned = new();
			if (isBarbarians || defeated) {
				return learned;
			}

			City libraryCity = null;
			Building library = null;
			foreach ((City c, CityBuilding cb) in GetBuildingSnapshot().activeWonders) {
				if (cb.building.gainAnyTechKnownByTwoCivs) {
					libraryCity = c;
					library = cb.building;
					break;
				}
			}
			if (libraryCity == null) {
				return learned;
			}

			List<Player> contacts = gameData.players.Where(p => p != this
				&& !p.isBarbarians
				&& !p.defeated
				&& playerRelationships.ContainsKey(p.id)).ToList();
			if (contacts.Count < 2) {
				return learned;
			}

			while (true) {
				Tech gained = GetAvailableTechsToResearch(gameData.techs)
					.FirstOrDefault(t => contacts.Count(p => p.knownTechs.Contains(t.id)) >= 2);
				if (gained == null) {
					break;
				}
				log.Information("{Player} learns {Tech} from the Great Library", this, gained.Name);
				CompleteResearchingTech(gameData, gained);
				learned.Add(gained);
			}

			if (learned.Count > 0) {
				// As with research, a human with nothing queued chooses what
				// to research next rather than having it picked for them.
				if (isHuman && currentlyResearchedTech == null && ResearchQueue.Count == 0) {
					lastDiscoveredTech = learned[^1];
				} else {
					PlayerAI.MaybePickTechToResearch(this, gameData.techs);
				}
				if (IsToldNews) {
					new MsgShowTemporaryPopup($"The {library.name} in {libraryCity.name} has given us {string.Join(", ", learned.Select(t => t.Name))}.",
						libraryCity.location, this).send();
				}
			}
			return learned;
		}

		// Grants techs for a wonder like Theory of Evolution, which gives its
		// builder free advances on completion. The free techs go through the
		// same path as Philosophy's bonus tech: the tech being researched is
		// completed at once, then the next one picked (from the research
		// queue, or automatically), and so on until the free techs are used.
		// If nothing is being researched, the free techs are kept until a
		// tech is chosen.
		//
		// Assumption: Civ3 completes the current research and then the next
		// one, and research progress isn't carried over.
		// Returns the techs learned right away.
		public List<Tech> GrantFreeTechs(GameData gameData, int count, string source, City city) {
			HashSet<ID> before = new(knownTechs);
			freeTechsRemaining += count;
			if (currentlyResearchedTech != null && !knownTechs.Contains(currentlyResearchedTech)) {
				SetCurrentlyResearchedTech(currentlyResearchedTech);
			}

			List<Tech> learned = gameData.techs.Where(t => knownTechs.Contains(t.id) && !before.Contains(t.id)).ToList();
			if (IsToldNews && learned.Count > 0 && city != null) {
				new MsgShowTemporaryPopup($"The {source} in {city.name} has given us {string.Join(", ", learned.Select(t => t.Name))}.",
					city.location, this).send();
			}
			return learned;
		}

		// Learns a tech acquired outside research, such as one stolen by
		// espionage, then carries on researching.
		public void AcquireTech(GameData gameData, Tech tech) {
			CompleteResearchAndBeginNew(gameData, tech);
		}

		private void CompleteResearchAndBeginNew(GameData gameData, IEnumerable<Tech> techs) {
			foreach (Tech tech in techs) {
				CompleteResearchingTech(gameData, tech);
			}
			PlayerAI.MaybePickTechToResearch(this, gameData.techs);
		}
		// Asks a human with nothing to research what to research next, once
		// they have a city. The message names the tech just discovered, which
		// a LAN client doesn't otherwise know.
		public void AskWhatToResearch(GameData gameData) {
			if (isHuman && cities.Count > 0 && currentlyResearchedTech == null
					&& GetAvailableTechsToResearch(gameData.techs).Count > 0) {
				new MsgShowScienceSelection(this, lastDiscoveredTech).send();
			}
		}

		private void CompleteResearchAndBeginNew(GameData gameData, Tech tech) {
			CompleteResearchingTech(gameData, tech);

			// A human whose research queue has run out is asked what to
			// research next (see AskWhatToResearch) rather than having it
			// picked for them.
			if (isHuman && currentlyResearchedTech == null && ResearchQueue.Count == 0) {
				lastDiscoveredTech = tech;
				return;
			}
			PlayerAI.MaybePickTechToResearch(this, gameData.techs);
		}

		private void CompleteResearchingTech(GameData gameData, Tech tech) {
			// If this tech awards the first civ to research it a free tech and
			// no other civs know about the tech, this player gets the bonus.
			if (tech.BonusTechToFirstCivThatResearches) {
				bool awardBonus = true;
				foreach (Player p in gameData.players) {
					if (p.knownTechs.Contains(tech.id)) {
						awardBonus = false;
					}
				}
				if (awardBonus) {
					++freeTechsRemaining;
				}
			}

			knownTechs.Add(tech.id);
			// trigger callback for techs that enable improvements to redraw map
			TechImprovementCallback(this, tech);

			// Only reset research if this was the tech being researched. A tech
			// gained another way (e.g. a trade) shouldn't wipe current progress.
			if (currentlyResearchedTech == tech.id) {
				SetCurrentlyResearchedTech(null);
			}

			// Remove the gained tech from the research queue, wherever it is.
			// It isn't necessarily at the front if it came from a trade.
			ResearchQueue = new Queue<Tech>(ResearchQueue.Where(t => t.id != tech.id));

			if (CanAdvanceToNextEra(gameData)) {
				eraCivilopediaName = GetNextEraNameByIndex(EraIndex());
			}
		}

		private static void TechImprovementCallback(Player player, Tech tech) {
			var terraforms = EngineStorage.gameData.Terraforms;
			if (terraforms.Any(t => t.Improvement is { layer: TerrainImprovement.Layer.Roads } && t.RequiredTech == tech.id)) {
				foreach (var city in player.cities) {
					var cityLoc = city.location;
					TryAddRoad(cityLoc, cityLoc.HasRoad(), cityLoc.HasRailroad());
					TryAddRailroad(cityLoc, cityLoc.HasRailroad());
				}
			}
		}

		private bool CanAdvanceToNextEra(GameData gameData) {
			foreach (Tech t in gameData.techs) {
				if (t.EraCivilopediaName != eraCivilopediaName) {
					continue;
				}

				if (knownTechs.Contains(t.id)) {
					continue;
				}

				// This is a tech in our era that we don't know. If it is
				// required for era advancement we can't go to the next era yet.
				if (t.RequiredForEraAdvancement) {
					return false;
				}
			}

			return true;
		}

		public (int, int, int) TotalUnitsAllowedUnitsAndSupportCostRaw() {
			int freeUnits = 0;

			Difficulty difficulty = EngineStorage.gameData.gameDifficulty;
			if (!isHuman) {
				freeUnits += difficulty.AdditionalFreeUnitSupport;
			}

			foreach (City city in cities) {
				if (!isHuman) {
					freeUnits += difficulty.UnitSupportBonusForEachSettlement;
				}

				if (city.residents.Count <= rules.MaximumLevel1CitySize) {
					freeUnits += government.freeUnitsPerTown;
				} else if (city.residents.Count <= rules.MaximumLevel2CitySize) {
					freeUnits += government.freeUnitsPerCity;
				} else {
					freeUnits += government.freeUnitsPerMetropolis;
				}
			}

			if (government.allUnitsFree) {
				freeUnits = units.Count;
			} else {
				// Captives are always free.
				foreach (MapUnit u in units) {
					if (u.IsCaptive()) {
						++freeUnits;
					}
				}
			}

			int totalUnits = units.Count;
			int allowedUnits = freeUnits;
			int unitSupportCost = Math.Max(0, (totalUnits - allowedUnits) * government.unitCost);
			return (totalUnits, allowedUnits, unitSupportCost);
		}

		[MoonSharpHidden]
		public (int, int, int) TotalUnitsAllowedUnitsAndSupportCost() {
			(int totalUnits, int allowedUnits, int unitSupportCost) result = TotalUnitsAllowedUnitsAndSupportCostRaw();

			foreach (City city in cities) {
				// unitSupport lua infow
				if (city.itemBeingProduced is Inflow inflowUnitSupport && inflowUnitSupport.TryGetInflowYieldFunc(InflowYield.unitsupport, out var unitSupportYieldFunc)) {
					int unitSupportLess = unitSupportYieldFunc.Invoke(new ScriptContext(this, city));
					result.unitSupportCost -= unitSupportLess;
				}
			}

			result.unitSupportCost = Math.Max(0, result.unitSupportCost);

			return (result.totalUnits, result.allowedUnits, result.unitSupportCost);
		}

		// See https://forums.civfanatics.com/threads/military-advisor-relative-strength-assessment-definition.62980/post-1211499 and
		// https://forums.civfanatics.com/threads/study-of-inner-workings-of-military-advisor.83599/
		public float CalculateMilitaryStrength() {
			float result = 4; // The +4 base is hypothesized to avoid div by 0 bugs.
			foreach (MapUnit unit in units) {
				UnitPrototype up = unit.unitType;
				result += unit.maxHitPoints * (up.attack * 3 + up.defense * 2) + up.bombard;
			}
			return result;
		}

		public enum MilitaryStrength {
			WeakTo,
			EquivalentTo,
			StrongTo,
		};

		// See https://forums.civfanatics.com/threads/study-of-inner-workings-of-military-advisor.83599/
		public MilitaryStrength CompareMilitaryStrengthTo(Player other) {
			float us = CalculateMilitaryStrength();
			float them = other.CalculateMilitaryStrength();

			if (us < 0.8 * them) {
				return MilitaryStrength.WeakTo;
			} else if (us > 1.2 * them) {
				return MilitaryStrength.StrongTo;
			} else {
				return MilitaryStrength.EquivalentTo;
			}
		}

		// After losing the capital, rebuilds the palace for free in the city
		// closest to where the old capital was, preferring larger cities.
		// Returns the new capital, or null if there was nothing to do.
		public City RelocatePalace(GameData gameData, Tile oldCapitalLocation) {
			if (cities.Count == 0 || cities.Any(c => c.IsCapital())) {
				return null;
			}

			City newCapital = cities
				.OrderBy(c => c.location.DistanceTo(oldCapitalLocation))
				.ThenByDescending(c => c.residents.Count)
				.First();
			newCapital.capital = true;

			Building palace = gameData.Buildings.Find(b => b.isCenterOfEmpire);
			if (palace != null && !newCapital.constructed_buildings.Any(cb => cb.building == palace)) {
				newCapital.AddBuilding(palace);
			}

			log.Information("{Player} moved its palace to {City}", this, newCapital);
			return newCapital;
		}

		// Puts the palace in the city, which becomes the capital, removing it
		// from the old capital (as building a new palace does in Civ3).
		public void MovePalaceTo(City newCapital, Building palace) {
			foreach (City c in cities) {
				if (c == newCapital) {
					continue;
				}
				c.capital = false;
				CityBuilding old = c.constructed_buildings.Find(cb => cb.building.isCenterOfEmpire);
				if (old != null) {
					c.RemoveBuilding(old);
				}
			}
			newCapital.capital = true;
			if (!newCapital.constructed_buildings.Any(cb => cb.building == palace)) {
				newCapital.AddBuilding(palace);
			}
			// The trade network re-checks which city is the capital itself.
			if (EngineStorage.gameData != null) {
				DoCorruptionCalculations(EngineStorage.gameData);
			}
			log.Information("{Player} built a new palace in {City}", this, newCapital);
		}

		// War weariness, as players measured it in Civ3
		// (https://www.civfanatics.com/civ3/strategy/game-mechanics/how-does-war-weariness-work/,
		// "the article" below). "Each civ have one wwp number against each of
		// the other civs", kept in our relationship with them
		// (PlayerRelationship.warWearinessPoints), starting at 0:
		// - "Subtract 30 wwp if the AI attacks you, except when AI is
		//   provoked by: use of nuclear weapons, failed spy mission". We
		//   count war being declared on us, and per the project owner by
		//   any civ, AI or human: the victim of a declaration starts with
		//   war happiness against the aggressor, which the war's weariness
		//   then wears away and eventually turns into war weariness. Also per
		//   the project owner, a civ that ignored a demand to take its units
		//   out of the aggressor's territory provoked the war, and gets none
		//   (TerritoryDemands).
		// - "Add 1 wwp if you have units in enemys territory when in war.
		//   (In beginning of the turn)"
		// - "Add 1 wwp for each lost unit without defence value,
		//   improvement pillage/bombed, unit that are bombard down to 1 hp"
		// - "Add 2 wwp when a human attacker is defeated"
		// - "Add 2 wwp when a unit with defence value is attacked. (Even if
		//   you win)"
		// - "Add 16 wwp when a size 1 city is captured 17 wwp for bigger
		//   cities."
		// - "Subtract 1 wwp if level >= 1, no enemy inside your territory
		//   and no units in enemys territory."
		// - "Subtract 1/20 of current wwp each turn in peace (round up)"
		// The article lists the points "for a human", and says Civ3 has a bug
		// that gives an AI the human's points in a human-AI battle (and both
		// AIs the first's in an AI-AI war). We give every civ its own points
		// as the rules above say, without the bug.
		public const int WarWearinessWhenWarIsDeclaredOnUs = -30;
		public const int WarWearinessPerTurnInEnemyTerritory = 1;
		public const int WarWearinessForLostUnitWithoutDefence = 1;
		public const int WarWearinessForPillagedOrBombedImprovement = 1;
		public const int WarWearinessForUnitBombardedToOneHitPoint = 1;
		public const int WarWearinessForDefeatedAttacker = 2;
		public const int WarWearinessForUnitAttacked = 2;
		public const int WarWearinessForLostSizeOneCity = 16;
		public const int WarWearinessForLostCity = 17;
		public const int WarWearinessRecoveryPerQuietTurn = 1;
		private const int WarWearinessDecayDivisorAtPeace = 20;

		// The article's war weariness levels: below 0 points is level -1 (war
		// happiness), up to 30 level 0 (no effect), then a level for every 30
		// more points, up to level 4 from 121.
		private const int WarWearinessPointsPerLevel = 30;
		private const int MaxWarWearinessLevel = 4;

		public static int WarWearinessLevel(int points) {
			if (points < 0) {
				return -1;
			}
			if (points == 0) {
				return 0;
			}
			return Math.Min(MaxWarWearinessLevel, (points - 1) / WarWearinessPointsPerLevel);
		}

		// The share of a city's citizens (in percent) made unhappy at each
		// level (0 to 4), by government war weariness: low as in the
		// article's Republic, high as in its Democracy. A Democracy at level
		// 3 or more revolts in Civ3; we don't model that, so it stays at
		// 100% unhappy.
		private static readonly int[] LowWarWearinessUnhappyPercent = { 0, 25, 50, 50, 100 };
		private static readonly int[] HighWarWearinessUnhappyPercent = { 0, 50, 100, 100, 100 };

		// "All government: Level -1: 25% happy people"
		private const int WarHappinessPercent = 25;

		// "subtract 25% for police station and 1 for US (Universal
		// Sufferage)": police stations take a quarter off the unhappy
		// citizens, and Universal Suffrage (the BIQ's "reduces war weariness
		// in all cities") one more.
		private const int PoliceStationReductionPercent = 25;
		private const int EverywhereReduction = 1;

		// Our war weariness points against the other civ.
		public int WarWearinessPointsAgainst(Player other) {
			return other != null && other.id != null && playerRelationships.TryGetValue(other.id, out PlayerRelationship pr)
				? pr.warWearinessPoints : 0;
		}

		// Adds war weariness points (or takes them off, if negative) against
		// the other civ. Barbarians, and civs we have no relationship with,
		// don't count.
		public void AddWarWeariness(Player other, int points) {
			if (other == null || other == this || other.isBarbarians || isBarbarians || other.id == null) {
				return;
			}
			if (playerRelationships.TryGetValue(other.id, out PlayerRelationship pr)) {
				pr.warWearinessPoints += points;
			}
		}

		// A unit of the defender's was attacked by one of the attacker's.
		public static void AddWarWearinessForAttack(MapUnit attacker, MapUnit defender, bool attackerDefeated) {
			if (attacker?.owner == null || defender?.owner == null) {
				return;
			}
			if (defender.unitType.defense > 0) {
				defender.owner.AddWarWeariness(attacker.owner, WarWearinessForUnitAttacked);
			}
			if (attackerDefeated) {
				attacker.owner.AddWarWeariness(defender.owner, WarWearinessForDefeatedAttacker);
			}
		}

		// The city's owner lost it to the captor. The size is from before
		// the capture.
		public static void AddWarWearinessForLostCity(Player owner, Player captor, int size) {
			owner?.AddWarWeariness(captor, size <= 1 ? WarWearinessForLostSizeOneCity : WarWearinessForLostCity);
		}

		// Called once per turn.
		public void UpdateWarWeariness(GameData gameData) {
			if (warWeariness > 0) {
				warWeariness -= DecayAtPeace(warWeariness);
			}
			if (isBarbarians) {
				return;
			}
			foreach (Player other in gameData.players) {
				if (other == this || other.isBarbarians || other.id == null
					|| !playerRelationships.TryGetValue(other.id, out PlayerRelationship pr)) {
					continue;
				}
				if (other.defeated || !pr.AtWar()) {
					pr.warWearinessPoints -= DecayAtPeace(pr.warWearinessPoints);
					continue;
				}
				bool weAreInTheirTerritory = HasUnitsInTerritoryOf(other);
				bool theyAreInOurTerritory = other.HasUnitsInTerritoryOf(this);
				if (weAreInTheirTerritory) {
					pr.warWearinessPoints += WarWearinessPerTurnInEnemyTerritory;
				} else if (!theyAreInOurTerritory && WarWearinessLevel(pr.warWearinessPoints) >= 1) {
					pr.warWearinessPoints -= WarWearinessRecoveryPerQuietTurn;
				}
			}
		}

		// A twentieth of the points, rounded up, toward 0. The article only
		// gives this for positive points; we fade war happiness (negative
		// points) the same way.
		private static int DecayAtPeace(int points) {
			int decay = (Math.Abs(points) + WarWearinessDecayDivisorAtPeace - 1) / WarWearinessDecayDivisorAtPeace;
			return Math.Sign(points) * decay;
		}

		private bool HasUnitsInTerritoryOf(Player other) {
			foreach (MapUnit unit in units) {
				Tile location = unit.location;
				if (location != null && location != Tile.NONE && location.OwningPlayer() == other) {
					return true;
				}
			}
			return false;
		}

		private int UnhappyPercent(int level) {
			if (level <= 0) {
				return 0;
			}
			return government.warWeariness switch {
				1 => LowWarWearinessUnhappyPercent[level],
				>= 2 => HighWarWearinessUnhappyPercent[level],
				_ => 0,
			};
		}

		// Every source of war weariness points: those against each civ, and
		// those from older saves.
		private IEnumerable<int> WarWearinessPointSources() {
			yield return warWeariness;
			foreach (PlayerRelationship pr in playerRelationships.Values) {
				yield return pr.warWearinessPoints;
			}
		}

		// The number of citizens in the city made unhappy by war weariness:
		// for each enemy, the share its level and our government give,
		// rounded down, added together; then police stations and Universal
		// Suffrage reduce it, and it can't be more than the city's citizens.
		public int WarWearinessUnhappiness(City city) {
			int citizens = city.residents.Count;
			int unhappy = 0;
			foreach (int points in WarWearinessPointSources()) {
				unhappy += citizens * UnhappyPercent(WarWearinessLevel(points)) / 100;
			}
			if (unhappy == 0) {
				return 0;
			}

			foreach (CityBuilding cb in city.EffectiveBuildings()) {
				if (cb.building.reducesWarWeariness) {
					unhappy -= unhappy * PoliceStationReductionPercent / 100;
					break;
				}
			}
			if (GetBuildingSnapshot().reducesWarWearinessEverywhere) {
				unhappy -= EverywhereReduction;
			}
			return Math.Clamp(unhappy, 0, citizens);
		}

		// The number of content citizens in the city made happy by war
		// happiness: a quarter of them for each civ we're at level -1
		// against, whatever our government. "War happiness is calculated
		// independent in the same way. (No effect of improvments)"
		public int WarHappiness(City city) {
			int citizens = city.residents.Count;
			int happy = 0;
			foreach (int points in WarWearinessPointSources()) {
				if (WarWearinessLevel(points) < 0) {
					happy += citizens * WarHappinessPercent / 100;
				}
			}
			return Math.Min(happy, citizens);
		}

		public void StartGoldenAge(GameData gameData, string reason) {
			if (hadGoldenAge || isBarbarians) {
				return;
			}
			hadGoldenAge = true;
			goldenAgeTurnsRemaining = gameData.rules.GoldenAgeDuration;
			log.Information("{Player} starts a golden age: {Reason}", this, reason);
			if (IsToldNews) {
				new MsgShowMilitaryAdvisorPopup(this, $"{reason}\nOur civilization enters a Golden Age!", happy: true).send();
			}
		}

		// Called at the end of each turn.
		public void AdvanceGoldenAge() {
			if (goldenAgeTurnsRemaining > 0) {
				--goldenAgeTurnsRemaining;
			}
		}

		// A golden age starts once the great wonders this civ has built cover
		// all of its strengths.
		public void MaybeStartGoldenAgeFromWonders(GameData gameData) {
			if (hadGoldenAge || civilization.traits.Count == 0) {
				return;
			}
			HashSet<Civilization.Trait> wonderTraits = new();
			foreach (City c in cities) {
				foreach (CityBuilding cb in c.constructed_buildings) {
					if (cb.building.IsGreatWonder() && cb.builtByPlayer == this) {
						wonderTraits.UnionWith(cb.building.traits);
					}
				}
			}
			if (civilization.traits.IsSubsetOf(wonderTraits)) {
				StartGoldenAge(gameData, "Our wonders have inspired the people.");
			}
		}

		public void DoCorruptionCalculations(GameData gameData) {
			if (cities.Count == 0) {
				return;
			}

			// Precalculate the cities of interest for distance corruption.
			citiesWithCorruptionWonders.Clear();
			bool foundCapital = false;
			foreach (City c in cities) {
				if (c.IsCapital()) {
					citiesWithCorruptionWonders.Add(c);
					foundCapital = true;
				}
				// We use constructed_buildings here because great wonders can't
				// supply small wonders like forbidden palaces, so we can avoid
				// doing extra work for each city.
				if (c.constructed_buildings.Any(x => x.building.WorksAsForbiddenPalaceFor(this))) {
					citiesWithCorruptionWonders.Add(c);
				}
			}
			if (!foundCapital) {
				// TODO: Ensure we always have a capital, even in scenarios
				// without palaces added.
				citiesWithCorruptionWonders.Add(cities[0]);
			}

			// Order the cities by distance to the capital, using OrderBy to get
			// a stable sort (https://stackoverflow.com/a/148123). We want a
			// stable sort, because if two cities are the same distance from the
			// capital, the tiebreaker is city age (which we don't track yet) and
			// then order in the database, which a stable sort gives us.
			//
			// TODO: track city age.
			City capital = cities.Find(x => x.IsCapital());
			if (capital == null) {
				capital = cities[0];
			}
			List<City> citiesInRankOrdering = cities.OrderBy(x => x.location.RankDistanceTo(capital.location)).ToList();

			// This is the same for every city (it depends on the whole empire,
			// not on anything the loop below changes), so work it out once.
			int adjustedOptimalCityNumber = GetAdjustedOptimalCityNumber(gameData);
			for (int i = 0; i < citiesInRankOrdering.Count; ++i) {
				citiesInRankOrdering[i].rankIndex = i;

				// For each city, calculate its corruption level so this
				// calculation doesn't have to be done on the fly.
				citiesInRankOrdering[i].CalculateCorruption(gameData, adjustedOptimalCityNumber);
			}
		}

		// Call once at turn advance, decrement penalty turns of unhappiness for drafting / whipping
		public void DecrementCityUnhappinessPenalties(GameData gameData) {
			foreach (City c in cities) {
				// TODO: Add drafting unhappiness decrement when implemented
				c.turnsOfUnhappinessDueToPopRushing -= (c.turnsOfUnhappinessDueToPopRushing > 0) ? 1 : 0;
			}
		}

		public void RecalculateCitizenMoods(GameData gameData, bool goIntoDisorderIfUnhappy = false) {
			foreach (City c in cities) {
				City.Mood cityMood = c.RecalculateCitizenMoods(gameData);

				// Disorder and celebrations start and end only at the turn's
				// check, so a recalculation elsewhere (e.g. loading a save)
				// leaves them alone.
				if (goIntoDisorderIfUnhappy) {
					c.isInCivilDisorder = cityMood == City.Mood.Unhappy;
					c.celebrating = !c.isInCivilDisorder && c.QualifiesForCelebration(rules);
				}
			}
		}

		// Returns a list of specialists that this player can use.
		public List<CitizenType> GetKnownSpecialists(GameData gameData) {
			return gameData.citizenTypes.FindAll(x => {
				return !x.IsDefaultCitizen && this.HasTech(x.PrerequisiteTech);
			});
		}

		// Returns the list of all wonders owned by this player, excluding those
		// that have become obsolete.
		public List<Tuple<City, CityBuilding>> GetActiveWonders() {
			return new List<Tuple<City, CityBuilding>>(GetBuildingSnapshot().activeWonders);
		}

		// Bumped whenever the buildings of this player's cities change: by
		// City.AddBuilding and City.RemoveBuilding, when a city's building list
		// is replaced, and when a city changes hands (for both players).
		private long buildingsVersion = 0;
		internal long BuildingsVersion => Interlocked.Read(ref buildingsVersion);

		internal void OnBuildingsChanged() {
			Interlocked.Increment(ref buildingsVersion);
		}

		// Empire-wide facts derived from the buildings in this player's cities
		// (the active great wonders, and whether a building like the Pentagon
		// makes armies larger). Every city yield needs the wonders, so they are
		// cached here instead of rescanning every building of every city.
		//
		// The snapshot is immutable and is replaced, never modified, so readers
		// on other threads always see a consistent one. It is checked against
		// everything it was computed from each time it is used, in O(1):
		//  - this player's BuildingsVersion, which catches buildings being
		//    added, removed or swapped in any of our cities, and cities
		//    changing hands;
		//  - the cities list (same list object, same count, and not modified
		//    since), which catches cities being founded, captured, lost,
		//    destroyed or reordered, and a new list on load, even where the
		//    code doing it doesn't tell us;
		//  - knownTechs (same set, same count, and not modified since), which
		//    catches wonders going obsolete.
		internal sealed class BuildingSnapshot {
			internal long buildingsVersion;
			internal List<City> citiesList;
			internal int citiesCount;
			internal List<City>.Enumerator citiesVersion;
			internal HashSet<ID> knownTechs;
			internal int knownTechsCount;
			internal HashSet<ID>.Enumerator knownTechsVersion;

			// Do not modify; GetActiveWonders hands out copies.
			internal List<Tuple<City, CityBuilding>> activeWonders;
			internal bool hasLargerArmies;
			internal bool reducesWarWearinessEverywhere;
			internal bool paysTradeMaintenance;
			internal int shipMovementBonus;
			internal bool safeSeaTravel;
			internal bool doubleCombatVsBarbarians;
			internal bool increasesLeaderChance;
			// Longevity: cities grow by two citizens at a time.
			internal bool doublesCityGrowthEverywhere;

			internal bool IsValidFor(Player player) {
				if (buildingsVersion != player.BuildingsVersion
					|| !ReferenceEquals(citiesList, player.cities)
					|| citiesCount != player.cities.Count
					|| !ReferenceEquals(knownTechs, player.knownTechs)
					|| (knownTechs != null && knownTechsCount != knownTechs.Count)) {
					return false;
				}
				return CollectionVersion.Unchanged(citiesVersion)
					&& (knownTechs == null || CollectionVersion.Unchanged(knownTechsVersion));
			}
		}

		private BuildingSnapshot buildingSnapshot;

		internal BuildingSnapshot GetBuildingSnapshot() {
			BuildingSnapshot snapshot = buildingSnapshot;
			if (snapshot != null && snapshot.IsValidFor(this)) {
				return snapshot;
			}

			// Take the versions before scanning, so a change made during the
			// scan leaves the snapshot stale rather than wrongly valid.
			snapshot = new BuildingSnapshot {
				buildingsVersion = BuildingsVersion,
				citiesList = cities,
				citiesCount = cities.Count,
				citiesVersion = cities.GetEnumerator(),
				knownTechs = knownTechs,
				knownTechsCount = knownTechs?.Count ?? 0,
				activeWonders = new(),
			};
			if (knownTechs != null) {
				snapshot.knownTechsVersion = knownTechs.GetEnumerator();
			}
			foreach (City c in cities.ToArray()) {
				foreach (CityBuilding cb in c.constructed_buildings) {
					if (cb.building.allowsLargerArmies) {
						snapshot.hasLargerArmies = true;
					}
					bool isGreatWonder = cb.building.greatWonderProperties != null;
					if (isGreatWonder && cb.building.isGreatWonderObsolete(this)) {
						continue;
					}
					// Combat and movement effects (the Great Wall, the Great
					// Lighthouse, Magellan's Voyage, the Heroic Epic) come from
					// any of our buildings but an obsolete great wonder.
					AddCombatAndMovementEffects(snapshot, cb.building);
					if (!isGreatWonder) {
						continue;
					}
					snapshot.activeWonders.Add(new Tuple<City, CityBuilding>(c, cb));
					if (cb.building.reducesWarWearinessEverywhere) {
						snapshot.reducesWarWearinessEverywhere = true;
					}
					if (cb.building.paysTradeMaintenance) {
						snapshot.paysTradeMaintenance = true;
					}
					if (cb.building.doublesCityGrowthEverywhere) {
						snapshot.doublesCityGrowthEverywhere = true;
					}
				}
			}
			buildingSnapshot = snapshot;
			return snapshot;
		}

		private static void AddCombatAndMovementEffects(BuildingSnapshot snapshot, Building building) {
			// Each wonder that increases ship movement adds its own +1, so
			// the Great Lighthouse and Magellan's Voyage stack (an assumption:
			// in Conquests the Great Lighthouse is usually obsolete by the
			// time Magellan's is built). The PlusTwoShipMovement flag, which
			// no Conquests building uses, adds +2.
			if (building.increasedShipMovement) {
				snapshot.shipMovementBonus += 1;
			}
			if (building.plusTwoShipMovement) {
				snapshot.shipMovementBonus += 2;
			}
			if (building.safeSeaTravel) {
				snapshot.safeSeaTravel = true;
			}
			if (building.doubleCombatVsBarbarians) {
				snapshot.doubleCombatVsBarbarians = true;
			}
			if (building.increasesLeaderChance) {
				snapshot.increasesLeaderChance = true;
			}
		}

		// The extra movement points this player's ships get from wonders
		// like the Great Lighthouse and Magellan's Voyage, and one more if
		// the civ is seafaring.
		public int ShipMovementBonus() {
			int bonus = GetBuildingSnapshot().shipMovementBonus;
			if (civilization?.traits.Contains(Civilization.Trait.Seafaring) == true) {
				bonus += 1;
			}
			return bonus;
		}

		// Whether this player's coastal ships (like the Galley) may enter
		// Sea tiles, thanks to the Great Lighthouse.
		public bool HasSafeSeaTravel() {
			return GetBuildingSnapshot().safeSeaTravel;
		}

		// Whether this player's ships are safe on water of the given terrain.
		// Coast is always safe. Sea is safe once the player can build a ship
		// that doesn't "sink in sea" (in Conquests, the Caravel, with
		// Astronomy), or has the Great Lighthouse, and Ocean once they can
		// build one that doesn't "sink in ocean" (the Galleon, with
		// Magnetism, or for the Portuguese their Carrack, with Astronomy).
		// Only the civ's own ships count, so others' unique units don't, and
		// resources aren't needed. See MapUnit.IsUnsafeWater.
		public bool CanSailSafelyOn(TerrainType terrain) {
			if (terrain.IsOcean) {
				UpdateSafeWaters();
				return safeOnOcean;
			}
			if (terrain.IsSea) {
				UpdateSafeWaters();
				return safeOnSea || HasSafeSeaTravel();
			}
			return true;
		}

		// What the player's naval research makes safe, worked out again only
		// when they learn a tech, as pathfinding asks for every water tile.
		private int safeWatersTechCount = -1;
		private bool safeOnSea;
		private bool safeOnOcean;

		private void UpdateSafeWaters() {
			if (knownTechs.Count == safeWatersTechCount) {
				return;
			}
			safeWatersTechCount = knownTechs.Count;
			safeOnSea = false;
			safeOnOcean = false;
			foreach (UnitPrototype p in EngineStorage.gameData?.unitPrototypes ?? []) {
				if (!p.IsSeaUnit() || (p.requiredTech != null && !knownTechs.Contains(p.requiredTech.id))) {
					continue;
				}
				if (civilization != null && !p.producibleBy.Contains(civilization)) {
					continue;
				}
				safeOnSea |= !p.sinksInSea;
				safeOnOcean |= !p.sinksInOcean;
			}
		}

		// Whether this player's units fight at double strength against
		// barbarians, thanks to the Great Wall.
		public bool HasDoubleCombatVsBarbarians() {
			return GetBuildingSnapshot().doubleCombatVsBarbarians;
		}

		// Whether this player's elite units are more likely to produce a
		// great leader, thanks to the Heroic Epic.
		public bool HasIncreasedLeaderChance() {
			return GetBuildingSnapshot().increasesLeaderChance;
		}

		public void MaybeSpawnBonusUnits(GameData gD) {
			// Bonus units only spawn on the first turn, if we have a city and
			// are a non-barbarian AI player.
			if (gD.turn != 1 || cities.Count != 1 || isHuman || isBarbarians) {
				return;
			}

			for (int i = 0; i < gD.gameDifficulty.ExtraStartUnit1; ++i) {
				cities[0].AddUnit(gD.unitPrototypes.Find(x => x.name == gD.rules.StartUnitType1), gD);
			}
			for (int i = 0; i < gD.gameDifficulty.ExtraStartUnit2; ++i) {
				cities[0].AddUnit(gD.unitPrototypes.Find(x => x.name == gD.rules.StartUnitType2), gD);
			}
			// The best defender and attacker the city can build, if it can
			// build any units at all.
			List<UnitPrototype> buildableUnits = cities[0].ListProductionOptions(gD).OfType<UnitPrototype>().ToList();
			UnitPrototype bestDefender = buildableUnits.MaxBy(u => u.defense);
			UnitPrototype bestAttacker = buildableUnits.MaxBy(u => u.attack);
			for (int i = 0; i < gD.gameDifficulty.NumberOfAIDefensiveStartingUnits && bestDefender != null; ++i) {
				cities[0].AddUnit(bestDefender, gD);
			}
			for (int i = 0; i < gD.gameDifficulty.NumberOfAIOffensiveStartingUnits && bestAttacker != null; ++i) {
				cities[0].AddUnit(bestAttacker, gD);
			}
		}

		public void UpdateResourcesInBorders(IEnumerable<Tile> ownedTiles) {
			resourcesInBorders = ownedTiles
								.Where(t => t.Resource != Resource.NONE)
								.GroupBy(t => t.Resource)
								.ToDictionary(g => g.Key, g => g.ToList());
		}

		public bool HasRequiredTechnology(IProducible producible) {
			return producible.requiredTech == null ||
				   knownTechs.Contains(producible.requiredTech.id);
		}

		public bool CanBridgeRoads() {
			ID engineeringTechId = Tech.FindBridgeTech(EngineStorage.gameData.techs)?.id;
			// With no such tech, nothing is ever bridged.
			return engineeringTechId is not null && knownTechs != null && knownTechs.Contains(engineeringTechId);
		}

		public int ShieldCost(IProducible producible) {
			if (producible == null) {
				return int.MaxValue;
			}
			// At higher difficulties, AI players get a cost discount.
			Difficulty difficulty = EngineStorage.gameData.gameDifficulty;
			float costFactor = isHuman ? 1.0f : difficulty.AiCostFactor / (float)(difficulty.HumanCostFactor);

			return producible.ShieldCost(civilization.traits, costFactor);
		}

		// The key of this player in GameData.history, which is id.ToString().
		// Cached (with the id it was made from, in case the id is replaced),
		// since building the string allocates.
		private Tuple<ID, string> historyKey;
		public string HistoryKey {
			get {
				ID currentId = id;
				Tuple<ID, string> key = historyKey;
				if (key == null || !ReferenceEquals(key.Item1, currentId)) {
					key = new Tuple<ID, string>(currentId, currentId.ToString());
					historyKey = key;
				}
				return key.Item2;
			}
		}

		public void UpdateHistory(GameData gameData) {
			if (!gameData.history.TryGetValue(HistoryKey, out List<HistTurnRecord> history))
				return;

			if (gameData.gameOver) // Game is already over
				return;

			int n = history.Count;
			HistTurnRecord lastTurn = history.LastOrDefault();

			// TODO: Make formulas moddable

			// "Power is an amalgam of cities, gold, culture, advances, resources, military strength,
			// nuclear weapons, and wonder."
			// Exact formula is unknown, so we repeat the previous value.
			// TODO: Figure out power formula
			int power = (lastTurn?.Power ?? 100);

			// Score is the _average_ of "turn scores". We keep the exact sum
			// of the turn scores and round only the average, as rounding a
			// running average each turn would let errors build up (and a
			// rounded-down average could only rise in large enough steps).
			// Records without a sum come from older saves; the best we can
			// do there is assume every turn scored the recorded average.
			double previousSum = 0;
			if (lastTurn != null) {
				previousSum = lastTurn.TurnScoreSum ?? (double)lastTurn.Score * n;
			}
			float turnScore = ScoreVictory.ComputeTurnScore(this, gameData);
			double turnScoreSum = previousSum + turnScore;
			int score = (int)Math.Round(turnScoreSum / (n + 1), MidpointRounding.AwayFromZero);

			// Culture is "the sum of the cultural value of all your cities"
			int totalCulture = 0;
			foreach (City c in cities) {
				totalCulture += c.GetCulture();
			}

			history.Add(new HistTurnRecord {
				Date = gameData.timeOptions.GetRawNumber(gameData.turn),
				Power = power,
				Score = score,
				TurnScoreSum = turnScoreSum,
				Culture = totalCulture,
				VP = 0 // TODO: victory points
			});
		}
	}

}

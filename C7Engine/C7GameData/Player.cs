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

		// The amount of gold this player has.
		private int _gold = 0;
		public int gold {
			get => _gold;
			set {
				if (value < 0) {
					// TODO: the exception is ok for development, but perhaps a warning log
					// and a Math Max function (0, value) is more appropriate at some point
					throw new Exception($"bad gold value of {value} for {this}");
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
		[LuaMethod]
		public void SetGold(int amount, bool add = false) {
			if (add)
				this.gold += amount;
			else
				this.gold = amount;
		}

		// The number of "beakers" (gold) spent on the currently researched
		// tech.
		public int beakers = 0;

		// The number of turns the player has been researching the current tech.
		public int turnsResearched = 0;

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

		// How tired of war the people are. It builds up while at war and
		// clears once the civ is at peace with everyone.
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
		public void DeclareWarOn(Player other, int currentTurn, bool afterWarning = false) {
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

			DeclareWar(this, other, isSneakAttack, refuseContactUntilTurn);

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

		// How much more than it gives an AI that is winning a war wants for
		// making peace, for each of the other side's cities.
		private const int PeaceTributePerCity = 30;

		// How much stronger than the other side an AI must be to think it is
		// winning the war.
		private const float WinningStrengthRatio = 1.5f;

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

			// Peace is welcome once we are talking again, unless we are
			// clearly winning: then the other side has to pay for it. A
			// placeholder until the AI weighs up wars properly.
			if ((theirOffer.partOfPeaceTreaty || ourOffer.partOfPeaceTreaty)
				&& CalculateMilitaryStrength() > WinningStrengthRatio * other.CalculateMilitaryStrength()) {
				ourGoldValue += PeaceTributePerCity * Math.Max(1, other.cities.Count);
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
			// too made its corruption far too high on the hard levels.
			int percentOptimalCities = isHuman ? gameData.gameDifficulty.PercentageOfOptimalCities : 100;

			float commercialCivFactor = civilization.traits.Contains(Civilization.Trait.Commercial) ? .25f : 0;

			// TODO: Handle the SPHQ.
			int numCorruptionReducingSmallWondersInEmpire = 0;
			foreach (City c in cities) {
				// We use constructed_buildings here because great wonders can't
				// supply small wonders like forbidden palaces, so we can avoid
				// doing extra work for each city.
				foreach (CityBuilding cb in c.constructed_buildings) {
					if (cb.building.isForbiddenPalace) {
						++numCorruptionReducingSmallWondersInEmpire;
					}
				}
			}

			float govtFactor = government.corruptionType switch {
				Government.CorruptionType.Minimal => .1f,
				Government.CorruptionType.Nuisance => .1f,
				Government.CorruptionType.Problematic => 0,
				Government.CorruptionType.Rampant => 0,
				Government.CorruptionType.Catastrophic => 0, // anarchy, special cased
				Government.CorruptionType.Communal => 2,
				Government.CorruptionType.Off => 0
			};

			float communalCorruptionFactor =
				government.corruptionType == Government.CorruptionType.Communal ? 3.0f : 3.0f/8.0f;

			float result = mapOptimalCityNumber * percentOptimalCities / 100.0f
				  * (1 + commercialCivFactor + govtFactor + communalCorruptionFactor * numCorruptionReducingSmallWondersInEmpire);
			return (int)result;
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
		public int GetTurnsOfAnarchyForTransition(GameData gameData) {
			if (civilization.traits.Contains(Civilization.Trait.Religious)) {
				return 2;
			}

			// We add Next(3)+Next(3) to roughly approximate a normal
			// distribution. With the base of 2, this gets us a random value
			// between 2 and 6.
			int randomPortion = 2 + GameData.rng.Next(3) + GameData.rng.Next(3);

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
			for (int i = 0; i < cities.Count; ++i) {
				cityFlows[i] = ComputeCityFlows(cities[i]);
				beakers += cityFlows[i].commerce.beakers;
			}
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

			if (isHuman && lostImprovements.Count > 0) {
				new MsgShowDomesticAdvisorPopup(this,
					$"We can no longer support our {string.Join(", ", lostImprovements)}.\nWe must think more about our treasury!").send();
			}
			if (isHuman && disbandedUnits.Count > 0) {
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

				c.HandleCityGrowth(gameData);
				c.HandleCityProduction(gameData);
			}
		}

		public void DoPerTurnScienceUpdates(GameData gameData) {
			if (currentlyResearchedTech == null) {
				return;
			}

			// TODO: This isn't quite accurate. This should only be
			// incremented if the player is actually spending money on
			// research, or has a science specialist.
			turnsResearched++;

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
				if (isHuman) {
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
			if (isHuman && learned.Count > 0 && city != null) {
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

		// Civ 3 doesn't publish its war weariness formula, so this is an
		// approximation: each turn at war adds a point per enemy, and another
		// if we started that war; losing a unit adds a point, or three if it
		// died attacking.
		private const int WarWearinessPerTurnAtWar = 1;
		private const int WarWearinessForStartingTheWar = 1;
		private const int WarWearinessForUnitLostDefending = 1;
		private const int WarWearinessForUnitLostAttacking = 3;

		// The weariness points that make one unhappy face in every city, by
		// government war weariness level (low, high).
		private const int WarWearinessPerFaceLow = 20;
		private const int WarWearinessPerFaceHigh = 10;

		// Called once per turn.
		public void UpdateWarWeariness(GameData gameData) {
			List<Player> enemies = gameData.players.Where(p =>
				p != this && !p.isBarbarians && !p.defeated && AtWar(this, p)).ToList();
			if (enemies.Count == 0) {
				warWeariness = 0;
				return;
			}
			foreach (Player enemy in enemies) {
				warWeariness += WarWearinessPerTurnAtWar;
				// Whether we declared the current war. For wars from before
				// that was recorded, fall back to whether we ever declared war
				// on them.
				bool weStartedIt = enemy.playerRelationships.TryGetValue(id, out PlayerRelationship pr)
					&& (pr.otherStartedCurrentWar ?? pr.warDeclarationCount > 0);
				if (weStartedIt) {
					warWeariness += WarWearinessForStartingTheWar;
				}
			}
		}

		public void AddWarWearinessForLostUnit(bool diedAttacking) {
			warWeariness += diedAttacking ? WarWearinessForUnitLostAttacking : WarWearinessForUnitLostDefending;
		}

		// The number of citizens in the city made unhappy by war weariness.
		// Police stations, and wonders like Universal Suffrage, halve it.
		public int WarWearinessUnhappiness(City city) {
			int pointsPerFace = government.warWeariness switch {
				1 => WarWearinessPerFaceLow,
				>= 2 => WarWearinessPerFaceHigh,
				_ => 0,
			};
			if (pointsPerFace == 0 || warWeariness == 0) {
				return 0;
			}

			int faces = warWeariness / pointsPerFace;
			bool reduced = GetBuildingSnapshot().reducesWarWearinessEverywhere;
			if (!reduced) {
				foreach (CityBuilding cb in city.EffectiveBuildings()) {
					if (cb.building.reducesWarWeariness) {
						reduced = true;
						break;
					}
				}
			}
			if (reduced) {
				faces /= 2;
			}
			return Math.Min(faces, city.residents.Count);
		}

		public void StartGoldenAge(GameData gameData, string reason) {
			if (hadGoldenAge || isBarbarians) {
				return;
			}
			hadGoldenAge = true;
			goldenAgeTurnsRemaining = gameData.rules.GoldenAgeDuration;
			log.Information("{Player} starts a golden age: {Reason}", this, reason);
			if (isHuman) {
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
				if (c.constructed_buildings.Any(x => x.building.isForbiddenPalace)) {
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
			for (int i = 0; i < gD.gameDifficulty.NumberOfAIDefensiveStartingUnits; ++i) {
				UnitPrototype unit = (UnitPrototype)cities[0].ListProductionOptions(gD).MaxBy(
					x => {
						if (x is UnitPrototype u) {
							return u.defense;
						}
						return -1;
					}
				);
				cities[0].AddUnit(unit, gD);
			}
			for (int i = 0; i < gD.gameDifficulty.NumberOfAIOffensiveStartingUnits; ++i) {
				UnitPrototype unit = (UnitPrototype)cities[0].ListProductionOptions(gD).MaxBy(
					x => {
						if (x is UnitPrototype u) {
							return u.attack;
						}
						return -1;
					}
				);
				cities[0].AddUnit(unit, gD);
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

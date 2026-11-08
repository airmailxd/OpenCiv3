using System.Collections.Generic;
using System.Linq;
using C7Engine.AI;
using C7GameData;
using Serilog;

namespace C7GameData {
	// The saved state of the turn's Staging, the Mercenary Market (see
	// C7Engine.Staging). Players are stored by their ID strings so the
	// state serializes as it is.
	public class StagingState {
		// The turn being staged, or -1 if none has been.
		public int turn = -1;

		// The unit types on offer this turn, by name.
		public List<string> offers = new();

		// Each civ's bids, by player ID: gold for each offer, in order.
		public Dictionary<string, List<int>> bids = new();

		// The IDs of the humans yet to bid. The market closes when it's
		// empty.
		public List<string> bidding = new();

		// The city each human chose for each offer, by player ID: a city ID
		// for each offer, in order, or null for none chosen (their capital).
		public Dictionary<string, List<string>> destinations = new();

		// Whether the market has closed and the units gone to the winners.
		public bool resolved = true;

		// Saves from before the city was chosen with the bid may hold units
		// still waiting for one; they're placed as the game loads.
		public List<StagingWin> toPlace = new();
	}

	// A unit a player won in the Mercenary Market.
	public class StagingWin {
		public string player;
		public string unitType;
	}

	// What became of an offer in the market: who won it (more than one if
	// they tied), and, as told to a player, what they paid for it (0 if
	// they didn't win it). The bids stay secret.
	public class MarketResult {
		public string unitType;
		public List<Player> winners = new();
		public int price;
	}
}

namespace C7Engine {
	// Eternal War's Staging, the Mercenary Market (owner's design,
	// 2026-10-23, replacing the free unit of 2026-10-22). At the start of
	// every turn after the first, before anyone moves, a few units go up for
	// sale: Rules.MarketOffersPerTurn of them, 3 on a standard map, one more
	// for each size above and one fewer for each below. They're chosen at
	// random from the units of the age of the most advanced civ and of the
	// ancient times, and may be ones a civ can't build yet, but never a
	// unique unit; a ship is one time in ShipOneIn. Every civ bids gold on
	// them blind, the humans within BidSeconds, and its bids together may
	// not come to more than its treasury. The highest bid for each unit
	// wins it and pays its bid; civs tied for the highest each get one and
	// each pay. A unit no one bid on goes unsold. No one is told anyone
	// else's bids, only who won. A human chooses, with each bid, the city
	// the unit goes to if they win it, their capital unless they choose
	// another (owner's request, 2026-11-07); the AIs choose once they've
	// won. The market closes when every human is done or out of time. A
	// ship may be sent to any city; one sent inland puts to sea at the coast
	// nearest it.
	public static class Staging {
		private static readonly ILogger log = Log.ForContext(typeof(Staging));

		// How long the humans have to bid, in seconds.
		public const int BidSeconds = 30;

		// Whether the market is open, so no one may move.
		public static bool InProgress(GameData gameData) {
			return gameData.rules?.StagingUnits == true && gameData.staging != null
				&& gameData.staging.bidding.Count > 0;
		}

		// Whether the player is yet to bid.
		public static bool IsBidding(GameData gameData, Player player) {
			return player != null && gameData.staging?.bidding.Contains(player.id.ToString()) == true;
		}

		// The units on offer this turn.
		public static List<UnitPrototype> Offers(GameData gameData) {
			return (gameData.staging?.offers ?? []).Select(n => gameData.unitPrototypes.Find(p => p.name == n)).Where(p => p != null).ToList();
		}

		// A ship is offered one time in this many, when there are ships to
		// offer.
		public const int ShipOneIn = 20;

		// How many units the market offers a turn on a standard map.
		public const int StandardOffers = 3;

		// The units offered a turn on the world size (its index among the
		// sizes, and the standard one's): StandardOffers, one more for each
		// size above standard and one fewer for each below, but at least one.
		public static int OffersForWorldSize(int size, int standard) {
			if (size < 0 || standard < 0) {
				return StandardOffers;
			}
			return System.Math.Max(1, StandardOffers + size - standard);
		}

		// The unit types the market may offer: the fighting land and sea
		// units of the most advanced civ's era and of the ancient times (by
		// the era of the advance each needs), leaving out the unique units.
		// If the leading era has none, the one before.
		public static List<UnitPrototype> Pool(GameData gameData) {
			List<Player> civs = gameData.players.Where(p => !p.IsIndependent && !p.defeated).ToList();
			int civilizations = gameData.civilizations.Count(c => !c.IsIndependent);
			int leading = civs.Count == 0 ? 0 : civs.Max(p => p.EraIndex());
			List<UnitPrototype> mayBeGiven = gameData.unitPrototypes.Where(p => MayBeGiven(p, civilizations)).ToList();
			int Era(UnitPrototype p) => EraUtils.GetEraIndex(p.requiredTech.EraCivilopediaName);
			for (int era = leading; era >= 0; --era) {
				List<UnitPrototype> pool = mayBeGiven.Where(p => Era(p) == era || Era(p) == 0).ToList();
				if (mayBeGiven.Any(p => Era(p) == era)) {
					return pool;
				}
			}
			return [];
		}

		// A unique unit is one only a few civilizations may build. A unit one
		// replaces, like the Musketman (France's Musketeer), is still built
		// by most, so it may be offered, even to a civ that can't build it.
		private static bool MayBeGiven(UnitPrototype unit, int civilizations) {
			return unit.requiredTech != null && !unit.unproducible
				&& (unit.IsLandUnit() || unit.IsSeaUnit()) && !unit.IsAirUnit()
				&& unit.attack + unit.defense > 0
				&& !unit.isSettler && !unit.isWorker && !unit.isKing && !unit.isLeader && !unit.isArmy
				&& !unit.isNuclearWeapon && !unit.isTacticalMissile && !unit.isCruiseMissile
				&& unit.producibleBy.Count(c => !c.IsIndependent) * 2 > civilizations;
		}

		// A unit type from the pool: a ship one time in ShipOneIn, and
		// otherwise a land unit, each kind as likely as any other.
		public static UnitPrototype Pick(List<UnitPrototype> pool) {
			List<UnitPrototype> ships = pool.Where(u => u.IsSeaUnit()).ToList();
			List<UnitPrototype> land = pool.Where(u => !u.IsSeaUnit()).ToList();
			List<UnitPrototype> from = land.Count == 0 || (ships.Count > 0 && GameData.rng.Next(ShipOneIn) == 0) ? ships : land;
			return from[GameData.rng.Next(from.Count)];
		}

		// The civs that take part: those with a city.
		private static IEnumerable<Player> Bidders(GameData gameData) {
			return gameData.players.Where(p => !p.IsIndependent && !p.defeated && p.cities.Count > 0);
		}

		// Opens the turn's market as the turn begins: chooses the offers and
		// has the AIs bid, leaving the humans to. With no humans to wait
		// for, it closes at once.
		public static void BeginTurn(GameData gameData) {
			StagingState state = gameData.staging ??= new();
			state.offers.Clear();
			state.bids.Clear();
			state.bidding.Clear();
			state.destinations.Clear();
			state.toPlace.Clear();
			state.resolved = true;
			if (gameData.rules?.StagingUnits != true) {
				return;
			}
			List<UnitPrototype> pool = Pool(gameData);
			if (pool.Count == 0) {
				log.Warning("No unit types to offer in the market on turn {Turn}", gameData.turn);
				return;
			}
			int count = System.Math.Max(1, gameData.rules.MarketOffersPerTurn);
			for (int i = 0; i < count; i++) {
				state.offers.Add(Pick(pool).name);
			}
			state.turn = gameData.turn;
			state.resolved = false;
			log.Information("Mercenary Market for turn {Turn}: {Offers}", gameData.turn, string.Join(", ", state.offers));

			List<UnitPrototype> offers = Offers(gameData);
			foreach (Player player in Bidders(gameData)) {
				if (player.isHuman) {
					state.bidding.Add(player.id.ToString());
				} else {
					Bid(gameData, player, AiBids(gameData, player, offers));
				}
			}
			if (state.bidding.Count == 0) {
				Resolve(gameData);
			}
		}

		// Records the player's bids, one for each offer, and the city each
		// unit goes to if they win it. Missing or negative bids count as
		// none, and if they come to more than the player's gold, the last are
		// cut until they don't. A city missing or not theirs counts as their
		// capital.
		public static void Bid(GameData gameData, Player player, List<int> amounts, List<City> cities = null) {
			StagingState state = gameData.staging;
			List<int> bids = Enumerable.Range(0, state.offers.Count)
				.Select(i => amounts != null && i < amounts.Count ? System.Math.Max(0, amounts[i]) : 0).ToList();
			int over = bids.Sum() - player.gold;
			for (int i = bids.Count - 1; i >= 0 && over > 0; --i) {
				int cut = System.Math.Min(bids[i], over);
				bids[i] -= cut;
				over -= cut;
			}
			state.bids[player.id.ToString()] = bids;
			state.destinations[player.id.ToString()] = Enumerable.Range(0, state.offers.Count)
				.Select(i => cities != null && i < cities.Count && cities[i]?.owner == player ? cities[i].id.ToString() : null).ToList();
			state.bidding.Remove(player.id.ToString());
		}

		// The city a human's units go to unless they choose another: their
		// capital, or if they have none, their first city.
		public static City DefaultCity(Player player) {
			return player.cities.Find(c => c.IsCapital()) ?? player.cities.FirstOrDefault();
		}

		// The city a unit the player won goes to: the one they chose for the
		// offer if it's still theirs, otherwise their capital.
		private static City Destination(GameData gameData, Player player, int offer, UnitPrototype unit) {
			if (gameData.staging.destinations.TryGetValue(player.id.ToString(), out List<string> chosen)
				&& offer < chosen.Count && chosen[offer] != null) {
				City city = player.cities.Find(c => c.id.ToString() == chosen[offer]);
				if (city != null) {
					return city;
				}
			}
			return DefaultCity(player) ?? AiChoice(gameData, player, unit);
		}

		// The AI's bids, one for each offer (see MarketAI).
		public static List<int> AiBids(GameData gameData, Player player, List<UnitPrototype> offers) {
			return MarketAI.Bids(gameData, player, offers);
		}

		// Lets AiTrialTest count the units each civ buys and the gold it
		// pays. Null outside a trial.
		internal static System.Action<Player, UnitPrototype, int> onBuy;

		// Closes the market: each unit goes to its highest bidder, or to each
		// of those tied for it, who pay their bids. The AIs choose a city for
		// theirs now; the humans' go where they chose with their bids.
		// Returns an entry for each offer naming its winners.
		public static List<MarketResult> Resolve(GameData gameData) {
			StagingState state = gameData.staging;
			List<MarketResult> results = new();
			if (state == null || state.resolved) {
				return results;
			}
			state.resolved = true;
			List<UnitPrototype> offers = Offers(gameData);
			for (int i = 0; i < offers.Count; i++) {
				UnitPrototype unit = offers[i];
				int best = state.bids.Values.Select(b => i < b.Count ? b[i] : 0).DefaultIfEmpty(0).Max();
				MarketResult result = new() { unitType = unit.name, price = best };
				if (best > 0) {
					foreach ((string id, List<int> bids) in state.bids) {
						Player player = gameData.players.Find(p => p.id.ToString() == id);
						if (player == null || i >= bids.Count || bids[i] != best || player.defeated || player.cities.Count == 0) {
							continue;
						}
						player.gold -= best;
						result.winners.Add(player);
						onBuy?.Invoke(player, unit, best);
						log.Information("{Player} buys a {Unit} for {Gold} gold", player, unit.name, best);
						City city = player.isHuman ? Destination(gameData, player, i, unit) : AiChoice(gameData, player, unit);
						Place(gameData, player, unit, city);
					}
				}
				results.Add(result);
			}
			MarketAI.Learn(gameData, state, results);
			return results;
		}

		// Tells the humans who won what. Each is told what they paid, but not
		// what anyone else bid.
		private static void Announce(GameData gameData, List<MarketResult> results) {
			foreach (Player player in gameData.players.Where(p => p.isHuman && !p.defeated && EngineStorage.IsPlayerReachable(p.id))) {
				List<int> own = gameData.staging.bids.TryGetValue(player.id.ToString(), out List<int> b) ? b : [];
				List<MarketResult> told = results.Select((r, i) => new MarketResult {
					unitType = r.unitType,
					winners = r.winners,
					price = r.winners.Contains(player) && i < own.Count ? own[i] : 0,
				}).ToList();
				new MsgShowMarketResults(player, told).send();
			}
		}

		// The cities the player may choose for the unit: any of theirs. A
		// ship sent to an inland city puts to sea nearby (see Place).
		public static List<City> Choices(Player player, UnitPrototype unit) {
			return player.cities.ToList();
		}

		// The player's units in the city that could defend it.
		public static List<MapUnit> Garrison(City city) {
			return city.location.unitsOnTile.Where(u => u.owner == city.owner && u.unitType.defense > 0 && !u.unitType.isWorker).ToList();
		}

		// The city an AI puts a unit it won in, or a human's that is chosen
		// for them (see MarketAI).
		public static City AiChoice(GameData gameData, Player player, UnitPrototype unit) {
			return MarketAI.Choice(gameData, player, unit, Choices(player, unit));
		}

		// Puts the player's unit in the city, or a ship the city can't hold
		// on the nearest water it can sail safely, coast first. Returns the unit, or null
		// if there was nowhere for it.
		public static MapUnit Place(GameData gameData, Player player, UnitPrototype unit, City city) {
			Tile tile = city.location;
			if (unit.IsSeaUnit() && !tile.NeighborsOcean()) {
				tile = NearestSea(gameData, player, unit, city.location);
				if (tile == null) {
					log.Warning("No sea for {Player}'s Staging {Unit} near {City}", player, unit.name, city);
					return null;
				}
			}
			MapUnit placed = gameData.SpawnUnit(player, unit, tile);
			// The player sees around the unit, as if it had sailed there:
			// a ship put to sea may be somewhere they haven't been.
			player.tileKnowledge.AddTilesToKnown(tile);
			log.Information("{Player}'s Staging {Unit} placed at {City}", player, unit.name, city);
			return placed;
		}

		// The salt water tile nearest the given one that the ship can sail
		// safely, with no one else's units on it: the nearest coast, or if
		// there's none, the nearest sea or ocean.
		private static Tile NearestSea(GameData gameData, Player player, UnitPrototype unit, Tile from) {
			bool Safe(Tile t) => !((unit.sinksInOcean && t.baseTerrainType.IsOcean) || (unit.sinksInSea && t.baseTerrainType.IsSea))
				|| player.CanSailSafelyOn(t.baseTerrainType);
			return gameData.map.tiles
				.Where(t => t.baseTerrainType.IsWater && !t.isFreshWater && Safe(t) && t.unitsOnTile.All(u => u.owner == player))
				.OrderBy(t => !t.baseTerrainType.IsCoast)
				.ThenBy(t => from.DistanceTo(t))
				.FirstOrDefault();
		}

		// Asks the humans yet to bid. One who can't answer (a LAN guest who
		// has left) or has no city is answered for: no bids.
		public static void AskHumans(GameData gameData) {
			StagingState state = gameData.staging;
			foreach (string id in state.bidding.ToList()) {
				Player player = gameData.players.Find(p => p.id.ToString() == id);
				if (player == null) {
					state.bidding.Remove(id);
					continue;
				}
				AskHuman(gameData, player);
			}
			Continue(gameData);
		}

		// Once every human has bid, closes the market and announces it.
		private static void Continue(GameData gameData) {
			StagingState state = gameData.staging;
			if (state.bidding.Count > 0 || state.resolved) {
				return;
			}
			Announce(gameData, Resolve(gameData));
		}

		// Asks the human for their bids and the city for each unit, from
		// their cities, their capital first.
		public static void AskHuman(GameData gameData, Player player) {
			if (player == null || !IsBidding(gameData, player)) {
				return;
			}
			if (player.defeated || player.cities.Count == 0 || !EngineStorage.IsPlayerReachable(player.id)) {
				Bid(gameData, player, []);
				return;
			}
			City capital = DefaultCity(player);
			List<City> cities = [capital, .. player.cities.Where(c => c != capital).OrderBy(c => c.name)];
			new MsgShowMarket(player, gameData.staging.offers.ToList(), player.gold, BidSeconds, cities).send();
		}

		// The human's bids, and the city for each unit. Returns false if they
		// weren't to bid.
		public static bool AnswerBids(GameData gameData, Player player, List<int> amounts, List<City> cities = null) {
			if (!IsBidding(gameData, player)) {
				return false;
			}
			Bid(gameData, player, amounts, cities);
			Continue(gameData);
			return true;
		}

		// A game loaded during Staging starts with a human's turn, so the
		// market is closed without the bids still to come. Units an older
		// save holds still waiting for a city go to the owner's capital.
		public static void FinishOnLoad(GameData gameData) {
			StagingState state = gameData.staging;
			if (state == null) {
				return;
			}
			foreach (string id in state.bidding.ToList()) {
				Player player = gameData.players.Find(p => p.id.ToString() == id);
				if (player == null) {
					state.bidding.Remove(id);
				} else {
					Bid(gameData, player, []);
				}
			}
			Resolve(gameData);
			foreach (StagingWin win in state.toPlace) {
				Player player = gameData.players.Find(p => p.id.ToString() == win.player);
				UnitPrototype unit = gameData.unitPrototypes.Find(p => p.name == win.unitType);
				if (player != null && unit != null && !player.defeated && DefaultCity(player) is City city) {
					Place(gameData, player, unit, city);
				}
			}
			state.toPlace.Clear();
		}
	}
}

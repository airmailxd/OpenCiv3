using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using C7Engine.Pathing;
using C7GameData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// How the AI deals in rights of passage (see RightOfPassage for the
	// rules).
	//
	// It seeks one when it is at war, or planning a war, and the way from
	// its cities to the enemy's runs through a third civ's territory: there,
	// without one, its units would be told to leave (see TerritoryDemands)
	// and couldn't use the roads. Civ3's AI does ask for them for this
	// reason: "$PLAYER0, we must pass through $CIVNAME1 to achieve our
	// objectives. Will you sign a right of passage agreement with
	// $CIVNAME4?" and "We need to cross $CIVNAME1. I hope that you will sign
	// a right of passage agreement" (Conquests diplomacy.txt,
	// #AIRIGHTOFPASSAGE). This, and the valuation below, are per the project
	// owner: "AI should seek Rights of passage when at war with a civ and
	// needing to run through 3rd civ's territory to get to the enemy. Value
	// should scale off of each member's empire size" (owner's decision,
	// 2026-10-11).
	//
	// Valuation (HEURISTIC): a right of passage lets each side into the
	// other's land, so the civ with the bigger empire gives more. Its worth
	// to a civ, in gold, is
	//
	//   Need(civ, partner) + GoldPerCity * (Cities(partner) - Cities(civ))
	//
	// where Need is WarNeedGold plus TileNeedGold for each of the partner's
	// tiles on the way to the enemy (up to MaxTilesCounted), or nothing if
	// the way doesn't cross the partner's land. The size part is what one
	// side gains and the other gives, so a smaller civ pays a bigger one
	// for it, and the needs are the deal's surplus. The AI accepts a deal
	// with a right of passage when its worth plus what it is given covers
	// what it gives, and pays a partner what the partner would ask (in gold,
	// or techs when short of gold) as long as that is no more than the
	// right of passage is worth to it.
	//
	// It refuses one, whatever it is offered, to (HEURISTIC, unless cited):
	// - a civ that broke one with its units inside someone's borders:
	//   "other civs won't sign ROP with you anymore"
	//   (https://civfanatics.com/civ3/strategy/game-mechanics/ai-attitude-study/),
	//   and "the last time we had a right of passage treaty with $CIVNAME1,
	//   you sort of attacked us" (Conquests diplomacy.txt,
	//   #AIREJECTPASSAGEVIOLATION);
	// - a neighbour much stronger than it (ThreatStrengthRatio), whose
	//   armies it doesn't want inside its borders on its roads, as Civ3's AI
	//   is known to use them for attacks ("An AI ally can use RoP to ride
	//   your railroads into your core, declare on you, and walk into one of
	//   your undefended cities", per
	//   https://forums.civfanatics.com/threads/right-of-passage-agreements.427477/);
	// - a civ at war with its military ally or mutual protection partner,
	//   so as not to open the way to them.
	//
	// The AI doesn't plan to break a right of passage to attack from inside
	// (Civ3's "RoP rape"), but a war it declares while its units happen to
	// be inside is a sneak attack all the same.
	public static class RightOfPassageAI {
		private static readonly ILogger log = Log.ForContext(typeof(RightOfPassageAI));

		// HEURISTIC: the gold a partner's city is worth in the size part.
		public const int GoldPerCity = 10;
		// HEURISTIC: what getting through to an enemy is worth.
		public const int WarNeedGold = 50;
		public const int TileNeedGold = 10;
		public const int MaxTilesCounted = 20;
		// HEURISTIC: how many of a civ's tiles the way must cross to be
		// worth asking for: a corner can be walked around.
		public const int MinTilesCrossed = 2;
		// HEURISTIC: how far (in tiles) a city of an enemy without a
		// campaign may be from our nearest city to plan a way to it.
		private const int MaxRouteDistance = 20;
		// HEURISTIC: how much stronger a neighbour must be to be refused.
		public const float ThreatStrengthRatio = 2f;
		// HEURISTIC: cities this close make two civs neighbours.
		private const int NeighbourDistance = 8;
		// HEURISTIC: the gold the AI keeps when paying for one.
		private const int GoldReserve = 50;
		// HEURISTIC: how often to ask the same civ again.
		private const int TurnsBetweenRequests = 10;

		// Lets AiTrialTest count the rights of passage AIs sign. Null outside
		// a trial.
		internal static Action<Player, Player> onSigned;

		private sealed class RouteCache {
			public int turn = -1;
			public Dictionary<Player, int> tilesCrossed = new();
		}

		private static readonly ConditionalWeakTable<Player, RouteCache> routes = new();
		private static readonly ConditionalWeakTable<Player, Dictionary<ID, int>> lastRequestTurn = new();

		public static int Cities(Player player) {
			return Math.Max(1, player.cities.Count);
		}

		// The right of passage's worth to us, in gold (see above).
		public static int Worth(GameData gameData, Player us, Player partner) {
			return Need(gameData, us, partner) + GoldPerCity * (Cities(partner) - Cities(us));
		}

		// What getting through the partner's territory to our enemies is
		// worth to us, or 0 if the way doesn't cross it.
		public static int Need(GameData gameData, Player us, Player partner) {
			if (us.isHuman || us.isBarbarians) {
				return 0;
			}
			int tiles = TerritoryOnWarRoutes(gameData, us).GetValueOrDefault(partner);
			if (tiles < MinTilesCrossed) {
				return 0;
			}
			return WarNeedGold + TileNeedGold * Math.Min(tiles, MaxTilesCounted);
		}

		// Whether we'd sign a right of passage with them, given the gold
		// worth of what each side gives besides it.
		public static bool WouldAccept(GameData gameData, Player us, Player them, int theirGoldValue, int ourGoldValue) {
			if (Refuses(gameData, us, them)) {
				return false;
			}
			return Worth(gameData, us, them) + theirGoldValue - ourGoldValue >= 0;
		}

		// Whether we won't sign a right of passage with them at any price
		// (see above).
		public static bool Refuses(GameData gameData, Player us, Player them) {
			if (RightOfPassage.HasBetrayed(them, gameData.players)) {
				return true;
			}
			if (IsThreat(us, them)) {
				return true;
			}
			foreach (Player p in gameData.players) {
				if (p != us && p != them && AtWar(them, p) && TryGetRelationship(us, p, out PlayerRelationship ours)
					&& ours.multiTurnDeals.Any(d => d.dealSubType is DealSubType.MilitaryAlliance or DealSubType.MutualProtectionPact)) {
					return true;
				}
			}
			return false;
		}

		// A neighbour much stronger than us.
		public static bool IsThreat(Player us, Player them) {
			float ours = us.CalculateMilitaryStrength();
			float theirs = them.CalculateMilitaryStrength();
			if (theirs < ThreatStrengthRatio * Math.Max(1f, ours)) {
				return false;
			}
			foreach (City a in us.cities) {
				foreach (City b in them.cities) {
					if (a.location.DistanceTo(b.location) <= NeighbourDistance) {
						return true;
					}
				}
			}
			return false;
		}

		// For each third civ whose land the ways to our enemies' cities run
		// through, how many of its tiles they cross. The ways are those of
		// our campaign (see WarCampaign) and, for each other enemy we are at
		// war with, from our city nearest one of its cities we know of to
		// that city. Worked out once a turn.
		public static Dictionary<Player, int> TerritoryOnWarRoutes(GameData gameData, Player player) {
			RouteCache cache = routes.GetOrCreateValue(player);
			if (cache.turn == gameData.turn) {
				return cache.tilesCrossed;
			}
			cache.turn = gameData.turn;
			cache.tilesCrossed = new();

			List<(Player enemy, Tile from, Tile to, MapUnit unit)> ways = new();
			Player campaignEnemy = null;
			if (WarCampaign.TryGetCampaign(player, out Player enemy, out City target, out City staging, out List<MapUnit> units)) {
				campaignEnemy = enemy;
				MapUnit unit = units.FirstOrDefault(u => u.IsLandUnit() && PlayerAI.IsStillOurs(u, player))
					?? LandUnitOn(player, staging.location.continent);
				if (unit != null) {
					ways.Add((enemy, staging.location, target.location, unit));
				}
			}
			foreach (Player p in gameData.players) {
				if (p == player || p == campaignEnemy || p.isBarbarians || p.defeated || !TryGetRelationship(player, p, out PlayerRelationship r)
					|| !r.AtWar()) {
					continue;
				}
				(City ours, City theirs) = NearestCities(player, p);
				MapUnit unit = ours != null ? LandUnitOn(player, ours.location.continent) : null;
				if (unit != null) {
					ways.Add((p, ours.location, theirs.location, unit));
				}
			}

			foreach ((Player e, Tile from, Tile to, MapUnit unit) in ways) {
				foreach (Tile t in Way(from, to, unit)) {
					Player owner = t.OwningPlayer();
					if (owner == null || owner == player || owner == e || owner.isBarbarians
						|| AtWar(player, owner)) {
						continue;
					}
					cache.tilesCrossed[owner] = cache.tilesCrossed.GetValueOrDefault(owner) + 1;
				}
			}
			return cache.tilesCrossed;
		}

		// The tiles on the way from one tile to next to another, which a
		// land unit could stand on (an enemy city can't be walked into).
		private static IEnumerable<Tile> Way(Tile from, Tile to, MapUnit unit) {
			List<Tile> candidates = to.neighbors.Values.Where(t => t != Tile.NONE && t.IsLand() && t.continent == to.continent)
				.OrderBy(t => t.DistanceTo(from)).ToList();
			if (candidates.Count == 0) {
				return [];
			}
			int index = PathingAlgorithmChooser.GetAlgorithm(unit).FindFirstReachable(from, candidates, unit, out TilePath path);
			if (index < 0 || path == null || path.path == null) {
				return [];
			}
			return path.path.ToList();
		}

		private static MapUnit LandUnitOn(Player player, int continent) {
			MapUnit result = null;
			foreach (MapUnit u in player.units) {
				if (u.IsLandUnit() && !u.IsLoaded() && u.location != null && u.location != Tile.NONE
					&& u.location.continent == continent) {
					if (u.IsCombatUnit()) {
						return u;
					}
					result ??= u;
				}
			}
			return result;
		}

		// Our city and theirs (one we know of) nearest each other on the
		// same continent, within MaxRouteDistance, or nulls.
		private static (City, City) NearestCities(Player player, Player enemy) {
			City bestOurs = null, bestTheirs = null;
			int best = MaxRouteDistance + 1;
			foreach (City theirs in enemy.cities) {
				if (!AIFogOfWar.KnowsTile(player, theirs.location)) {
					continue;
				}
				foreach (City ours in player.cities) {
					if (ours.location.continent != theirs.location.continent) {
						continue;
					}
					int d = ours.location.DistanceTo(theirs.location);
					if (d < best) {
						best = d;
						bestOurs = ours;
						bestTheirs = theirs;
					}
				}
			}
			return (bestOurs, bestTheirs);
		}

		// Asks each civ whose land the ways to our enemies cross for a right
		// of passage, paying what it would ask.
		public static async Task PlayTurn(Player player, GameData gameData) {
			if (player.isHuman || player.isBarbarians || player.cities.Count == 0) {
				return;
			}
			Dictionary<Player, int> crossed = TerritoryOnWarRoutes(gameData, player);
			foreach ((Player them, int tiles) in crossed.ToList()) {
				if (tiles < MinTilesCrossed || !RightOfPassage.CanSign(gameData, player, them)
					|| !them.WillAcceptCommunicationFrom(player, gameData.turn) || RecentlyAsked(gameData, player, them)) {
					continue;
				}
				(TradeOffer weGive, TradeOffer weWant) = MakeOffer(gameData, player, them);
				if (weGive == null) {
					continue;
				}
				lastRequestTurn.GetOrCreateValue(player)[them.id] = gameData.turn;
				log.Information("{Player} asks {Them} for a right of passage, offering {Offer}", player, them, weGive.ToString());

				if (them.isHuman) {
					await OfferToHuman(player, them, weGive, weWant);
				} else if (them.WouldAcceptDealFrom(gameData, player, weGive, weWant)) {
					player.ExecuteDeal(gameData, them, weWant, weGive);
				}
				if (HaveActiveRightOfPassage(player, them)) {
					log.Information("{Player} and {Them} signed a right of passage", player, them);
					onSigned?.Invoke(player, them);
				}
			}
		}

		private static bool RecentlyAsked(GameData gameData, Player player, Player them) {
			return lastRequestTurn.TryGetValue(player, out Dictionary<ID, int> asked) && asked.TryGetValue(them.id, out int turn)
				&& gameData.turn - turn < TurnsBetweenRequests;
		}

		// A right of passage for what they'd ask, or nulls if it is worth
		// less than that to us or we can't pay it. An AI asks what it'd need
		// to accept (minus its worth); with a human, whose needs we don't
		// know, we offer what the size part says they give.
		public static (TradeOffer weGive, TradeOffer weWant) MakeOffer(GameData gameData, Player us, Player them) {
			if (!them.isHuman && Refuses(gameData, them, us)) {
				return (null, null);
			}
			int theirWorth = them.isHuman ? GoldPerCity * (Cities(us) - Cities(them)) : Worth(gameData, them, us);
			int price = Math.Max(0, -theirWorth);
			if (price > Worth(gameData, us, them)) {
				return (null, null);
			}

			TradeOffer weGive = new() { rightOfPassage = true };
			TradeOffer weWant = new() { rightOfPassage = true };
			if (price == 0) {
				return (weGive, weWant);
			}
			int spare = Math.Max(0, us.gold - GoldReserve);
			if (spare >= price) {
				weGive.gold = price;
				return (weGive, weWant);
			}
			// Short of gold: make it up with techs, the cheapest to them
			// first, so as to give away as little as we can.
			int value = spare;
			foreach (Tech t in us.GetTechsTradableTo(them, gameData.techs).OrderBy(t => gameData.TechCostFor(t, them))) {
				if (value >= price) {
					break;
				}
				weGive.techs.Add(t);
				value += gameData.TechCostFor(t, them);
			}
			if (value < price) {
				return (null, null);
			}
			// The techs may cover it on their own, or with some gold.
			int goldNeeded = Math.Max(0, price - (value - spare));
			if (goldNeeded > 0) {
				weGive.gold = goldNeeded;
			}
			// The techs may be worth more to us than to them: we'd turn the
			// deal down ourselves (as when a human agrees to it).
			if (weGive.GoldEquivalentFor(gameData, us) > Worth(gameData, us, them)) {
				return (null, null);
			}
			return (weGive, weWant);
		}

		private static async Task OfferToHuman(Player us, Player them, TradeOffer weGive, TradeOffer weWant) {
			// A LAN guest who has left can't answer, so isn't asked.
			if (!EngineStorage.IsPlayerReachable(them.id)) {
				return;
			}
			// The human receiving the offer takes the UI to respond. In a
			// hotseat game they may not be the player at the screen.
			if (EngineStorage.uiFollowsActivePlayer) {
				EngineStorage.uiControllerID = them.id;
			}
			EngineStorage.diplomacyPlayerID = them.id;
			EngineStorage.diplomacyAIPlayerID = us.id;
			new MsgShowTradeOffer(us, them, weWant, weGive).send();
			await EngineStorage.WaitForDiplomacyCompleted(them.id);
			EngineStorage.diplomacyPlayerID = null;
			EngineStorage.diplomacyAIPlayerID = null;
		}
	}
}

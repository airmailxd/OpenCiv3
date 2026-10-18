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
	// 2026-10-11). Without the embassy it needs as the proposer, it
	// establishes one first if it can spare the gold. Now and then it also
	// offers one to a civ it is at peace with, as Civ3's AI does to be
	// friendly (owner's decision, 2026-10-11), with gold as the sizes say
	// (below); the other civ signs if it is worth it to them. In paired AI
	// trials (AiTrialTest, 16 games
	// each) it did no harm: civs asking signed about one right of passage a
	// game on Pangaea and scored a little higher (+122 +- 115), and were
	// unchanged on Continents (-29 +- 48).
	//
	// Valuation (HEURISTIC): a right of passage lets each side into the
	// other's land, so the civ with the bigger empire gives more. Its worth
	// to a civ, in gold, is
	//
	//   Need(civ, partner) + SizePayment(civ, partner) - SizePayment(partner, civ)
	//
	// where Need is WarNeedGold plus TileNeedGold for each of the partner's
	// tiles on the way to the enemy (up to MaxTilesCounted), or nothing if
	// the way doesn't cross the partner's land, and SizePayment is what
	// the size part says the first pays the second (below). The size part
	// is what one side gains and the other gives, so a smaller civ pays a
	// bigger one for it, and the needs are the deal's surplus. The AI
	// accepts a deal with a right of passage when its worth plus what it is
	// given covers what it gives, and pays a partner what the partner would
	// ask (in gold, or techs when short of gold) as long as that is no more
	// than the right of passage is worth to it.
	//
	// The bigger empire is paid: the AI "should ask for gold if their
	// empire is bigger, or offer gold if smaller" (owner's decision,
	// 2026-10-12). The size part is SizeGold, GoldPerCity for each city
	// the bigger has more, paid by the smaller as far as its treasury goes
	// (SizePayment); equal sizes, no gold. So proposing one (asking for one
	// at war, or in friendship), the AI asks a smaller partner for that
	// gold, and offers a bigger one what it'd ask (an AI's needs lower it;
	// for a human, as much of it as we can spare); and offered one, it
	// wants that gold from a smaller civ and pays a bigger one up to it.
	// What it asks always passes its own valuation, as a human agreeing to
	// an AI's offer proposes it back.
	//
	// It refuses one, whatever it is offered, to (HEURISTIC, unless cited):
	// - a civ that broke one with its units inside someone's borders:
	//   "other civs won't sign ROP with you anymore"
	//   (https://civfanatics.com/civ3/strategy/game-mechanics/ai-attitude-study/),
	//   and "the last time we had a right of passage treaty with $CIVNAME1,
	//   you sort of attacked us" (Conquests diplomacy.txt,
	//   #AIREJECTPASSAGEVIOLATION);
	// - a civ at war with its military ally or mutual protection partner,
	//   so as not to open the way to them.
	//
	// The AI doesn't plan to break a right of passage to attack from inside
	// (Civ3's "RoP rape"), but a war it declares while its units happen to
	// be inside is a sneak attack all the same.
	public static class RightOfPassageAI {
		private static readonly ILogger log = Log.ForContext(typeof(RightOfPassageAI));

		// HEURISTIC: the gold a partner's city is worth in the size part.
		public const int GoldPerCity = 5;
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
		// HEURISTIC: the chance each turn of offering a right of passage in
		// friendship to a civ we're at peace with.
		public const int FriendlyOfferChancePercent = 5;
		// HEURISTIC: the gold the AI keeps when paying for one.
		private const int GoldReserve = 25;
		// HEURISTIC: the gold the AI keeps when establishing an embassy to
		// ask for one.
		private const int EmbassyGoldReserve = 10;
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
			return Need(gameData, us, partner) + SizePayment(us, partner) - SizePayment(partner, us);
		}

		// The size part in gold, whichever side pays it (see above).
		public static int SizeGold(Player a, Player b) {
			return GoldPerCity * Math.Abs(Cities(a) - Cities(b));
		}

		// The gold the payer, if the smaller, pays the bigger payee for the
		// size part, as far as its treasury goes, or 0 if it isn't the
		// smaller (owner's decision, 2026-10-12; see above).
		public static int SizePayment(Player payer, Player payee) {
			if (Cities(payer) >= Cities(payee)) {
				return 0;
			}
			return Math.Min(SizeGold(payer, payee), Math.Max(0, payer.gold));
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
			foreach (Player p in gameData.players) {
				if (p != us && p != them && AtWar(them, p) && TryGetRelationship(us, p, out PlayerRelationship ours)
					&& ours.multiTurnDeals.Any(d => d.dealSubType is DealSubType.MilitaryAlliance or DealSubType.MutualProtectionPact)) {
					return true;
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
				if (tiles >= MinTilesCrossed) {
					EstablishEmbassyIfNeeded(gameData, player, them);
				}
				if (tiles < MinTilesCrossed || !RightOfPassage.CanSign(gameData, player, them)
					|| !them.WillAcceptCommunicationFrom(player, gameData.turn) || RecentlyAsked(gameData, player, them)) {
					continue;
				}
				(TradeOffer weGive, TradeOffer weWant) = MakeOffer(gameData, player, them);
				if (weGive == null) {
					continue;
				}
				lastRequestTurn.GetOrCreateValue(player)[them.id] = gameData.turn;
				log.Information("{Player} asks {Them} for a right of passage, offering {Offer} for {Want}", player, them, weGive.ToString(),
					weWant.ToString());

				if (them.isHuman) {
					await OfferToHuman(player, them, weGive, weWant);
				} else if (them.WouldAcceptDealFrom(gameData, player, weGive, weWant)) {
					// We propose it, so they carry it out (see ExecuteDeal).
					them.ExecuteDeal(gameData, player, weGive, weWant);
				}
				if (HaveActiveRightOfPassage(player, them)) {
					log.Information("{Player} and {Them} signed a right of passage", player, them);
					onSigned?.Invoke(player, them);
				}
			}

			if (GameData.rng.Next(100) < FriendlyOfferChancePercent) {
				await OfferFriendlyPassage(player, gameData);
			}
		}

		// Offers a right of passage to a civ we're at peace with and could
		// sign one with, the nearest first, asking or offering gold as the
		// sizes say (but never techs: it is only friendship): an AI signs it
		// if it is worth it to them, a human is asked.
		internal static async Task OfferFriendlyPassage(Player player, GameData gameData) {
			Player them = null;
			TradeOffer weGive = null, weWant = null;
			foreach (Player p in gameData.players
				.Where(p => p != player && !p.isBarbarians && !p.defeated && RightOfPassage.CanSign(gameData, player, p)
					&& !Refuses(gameData, player, p) && !RecentlyAsked(gameData, player, p) && !WarCampaign.IsPlannedTarget(player, p)
					&& p.WillAcceptCommunicationFrom(player, gameData.turn))
				.OrderBy(p => NearestCityDistance(player, p))) {
				(weGive, weWant) = MakeOffer(gameData, player, p, payWithTechs: false);
				if (weGive != null) {
					them = p;
					break;
				}
			}
			if (them == null) {
				return;
			}
			lastRequestTurn.GetOrCreateValue(player)[them.id] = gameData.turn;
			log.Information("{Player} offers {Them} a right of passage in friendship, giving {Offer} for {Want}", player, them,
				weGive.ToString(), weWant.ToString());
			if (them.isHuman) {
				await OfferToHuman(player, them, weGive, weWant);
			} else if (them.WouldAcceptDealFrom(gameData, player, weGive, weWant)) {
				// We propose it, so they carry it out (see ExecuteDeal).
				them.ExecuteDeal(gameData, player, weGive, weWant);
			}
			if (HaveActiveRightOfPassage(player, them)) {
				onSigned?.Invoke(player, them);
			}
		}

		private static int NearestCityDistance(Player a, Player b) {
			int best = int.MaxValue;
			foreach (City x in a.cities) {
				foreach (City y in b.cities) {
					if (x.location.map != null && x.location.map == y.location.map) {
						best = Math.Min(best, x.location.DistanceTo(y.location));
					}
				}
			}
			return best;
		}

		// Proposing a right of passage needs our embassy with them (see
		// RightOfPassage), so without one we establish one, as the AI does
		// anyway when it can spare the gold (see EspionageAI), but keeping
		// less in reserve, as we need it now.
		private static void EstablishEmbassyIfNeeded(GameData gameData, Player player, Player them) {
			if (!TryGetRelationship(player, them, out PlayerRelationship ours) || ours.hasEmbassy) {
				return;
			}
			if (Espionage.Unavailable(gameData, player, EspionageMission.EstablishEmbassy, them, null) != null
				|| player.gold - Espionage.Cost(gameData, player, EspionageMission.EstablishEmbassy, them, null) < EmbassyGoldReserve) {
				return;
			}
			log.Information("{Player} establishes an embassy with {Them} to ask for a right of passage", player, them);
			Espionage.Perform(gameData, player, EspionageMission.EstablishEmbassy, them, null);
		}

		private static bool RecentlyAsked(GameData gameData, Player player, Player them) {
			return lastRequestTurn.TryGetValue(player, out Dictionary<ID, int> asked) && asked.TryGetValue(them.id, out int turn)
				&& gameData.turn - turn < TurnsBetweenRequests;
		}

		// A right of passage on the terms the sizes say (owner's decision,
		// 2026-10-12; see above), or nulls if it is worth too little to us
		// or we can't pay. Bigger than them, we ask their SizePayment, which
		// our worth always covers. Smaller, we offer what they'd ask: an AI
		// what it'd need to accept (minus its worth); a human, whose needs we
		// don't know, SizeGold, or as much of it as we can spare. Short of
		// gold for an AI, we make it up with techs if payWithTechs.
		public static (TradeOffer weGive, TradeOffer weWant) MakeOffer(GameData gameData, Player us, Player them,
				bool payWithTechs = true) {
			if (!them.isHuman && Refuses(gameData, them, us)) {
				return (null, null);
			}
			TradeOffer weGive = new() { rightOfPassage = true };
			TradeOffer weWant = new() { rightOfPassage = true };
			int spare = Math.Max(0, us.gold - GoldReserve);

			if (Cities(us) > Cities(them)) {
				int ask = SizePayment(them, us);
				if (ask > 0) {
					weWant.gold = ask;
				}
				return (weGive, weWant);
			}

			int price = them.isHuman ? Math.Min(SizeGold(us, them), spare) : Math.Max(0, -Worth(gameData, them, us));
			if (price > Worth(gameData, us, them)) {
				return (null, null);
			}
			if (price == 0) {
				return (weGive, weWant);
			}
			if (spare >= price) {
				weGive.gold = price;
				return (weGive, weWant);
			}
			if (!payWithTechs) {
				return (null, null);
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

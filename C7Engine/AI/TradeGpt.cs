using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7GameData;

namespace C7Engine.AI {
	// Trial switch "trade-gpt" (from SuedeCivIII's tips on tech trading):
	// between AIs, techs are sold for gold per turn rather than the lump
	// sums the old TechDeal pays, a civ tries to trade with every civ it can
	// on first contact and whenever it learns a tech, even for cheap
	// techs, and a buyer is shown the biggest gold surplus it can have, by
	// lowering its science rate while the deal is made (an AI pays gold per
	// turn only up to its surplus, see TradeOffer.AffordsGoldPerTurn). Off,
	// AI tech deals are as they were.
	public static class TradeGpt {
		public const string Switch = "trade-gpt";

		// Most deals a civ makes with one other in a turn.
		private const int MostDealsPerPair = 3;

		public static bool On(Player us, Player them) {
			return AiExperiments.Has(us, Switch) && !us.isHuman && !them.isHuman;
		}

		private class Seen {
			public int techs = -1;
			public HashSet<ID> met = new();
		}
		private static readonly ConditionalWeakTable<Player, Seen> seen = new();

		// Whether the civ has met someone or learned a tech since it last
		// asked, which is when it looks for trades at once.
		public static bool WantsToTradeNow(Player us) {
			if (!AiExperiments.Has(us, Switch)) {
				return false;
			}
			Seen s = seen.GetOrCreateValue(us);
			bool changed = s.techs != us.knownTechs.Count;
			s.techs = us.knownTechs.Count;
			foreach (ID id in us.playerRelationships.Keys) {
				changed |= s.met.Add(id);
			}
			return changed;
		}

		// Tries to trade techs for gold per turn with them, in both
		// directions, returning how many deals were made.
		public static int TradeTechs(GameData gD, Player us, Player them) {
			int made = 0;
			for (int i = 0; i < MostDealsPerPair; ++i) {
				if (!(TryDeal(gD, us, them) || TryDeal(gD, them, us))) {
					break;
				}
				++made;
			}
			return made;
		}

		// Buys one tech from seller, if the buyer can pay for it and both
		// accept.
		private static bool TryDeal(GameData gD, Player buyer, Player seller) {
			List<Tech> techs = seller.GetTechsTradableTo(buyer, gD.techs);
			if (techs.Count == 0) {
				return false;
			}
			int duration = TradeOffer.DealDuration(gD);
			foreach (Tech t in techs.OrderByDescending(t => gD.TechCostFor(t, buyer))) {
				int ask = new TradeOffer { techs = { t } }.GoldEquivalentFor(gD, seller, given: true, counterpart: buyer);
				int gpt = Math.Max(1, (ask + duration - 1) / duration);
				(int sci, int tax) = (buyer.scienceRate, buyer.taxRate);
				try {
					RaiseTaxUntil(buyer, () => TradeOffer.AffordsGoldPerTurn(buyer, gpt));
					if (!TradeOffer.AffordsGoldPerTurn(buyer, gpt)) {
						continue;
					}
					TradeOffer buyerGives = new() { goldPerTurn = gpt };
					TradeOffer sellerGives = new() { techs = { t } };
					// The buyer pays up to a turn's rounding over the price, so
					// only the seller's agreement is asked.
					if (seller.WouldAcceptDealFrom(gD, buyer, buyerGives, sellerGives)
							&& buyer.ExecuteDeal(gD, seller, sellerGives, buyerGives)) {
						return true;
					}
				} finally {
					// The deal is made; the rates go back in the same turn.
					(buyer.scienceRate, buyer.taxRate) = (sci, tax);
				}
			}
			return false;
		}

		// Moves science to tax a point at a time until enough() holds or
		// there is no more to move.
		private static void RaiseTaxUntil(Player p, Func<bool> enough) {
			while (!enough() && p.scienceRate > p.minScienceRate && p.taxRate < p.maxRate) {
				--p.scienceRate;
				++p.taxRate;
			}
		}
	}
}

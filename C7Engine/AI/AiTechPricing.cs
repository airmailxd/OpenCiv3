using System;
using System.Linq;
using C7GameData;

namespace C7Engine.AI {
	// How one AI prices techs it trades with another, as Soren Johnson
	// (Firaxis) described it for 1.17f (Apolyton, "v1.17 AI vs AI tech
	// trading", https://codehappy.net/apolyton/threads/42905-1.htm; compendium
	// 06, section 6.8):
	// - A tech is worth what it is to the buyer. "The whole
	//   techs-are-cheaper-if-known-by-other-civs rule only applies to _known_
	//   civs. So even if every civ but one know Republic, it is still full
	//   value to that last civ if it is still alone" (post #16), and a tech
	//   the buyer is "close to discovering" is "not worth very much via
	//   trade" (post #11). GameData.TechCostFor already counts only the civs
	//   the player knows and takes off its progress, so the seller asks the
	//   buyer's cost rather than its own.
	// - The AIs "always try to meet the _full_ value of the tech", but "When
	//   necessary, a AI might be willing to sell a tech for down to
	//   half-value, but only if the other AI has nothing else to give for
	//   it" (post #2). How far it bends is the difficulty's AI-to-AI trade
	//   rate: "the AI trading rate determines how much the AI will bend if
	//   the other AI does not have enough money" (Soren, Apolyton 48219,
	//   post #4). UNVERIFIED: that the bend is to 100 / rate of the value,
	//   which reaches half at the highest rate, Sid's 200.
	//
	// Paired AI trials (as the switch "ai-tech-buyer-price", 8 games of 200
	// turns, score per civ against the same civs without it): +82 +- 76 on
	// Continents and -76 +- 88 on Pangaea, with 1.8 and 3.3 more techs at
	// the end (both over two standard errors). Not clearly weaker, and it
	// is Civ3's rule, so adopted.
	public static class AiTechPricing {
		// Whether p, an AI, prices the techs it trades with counterpart at
		// the buyer's cost: when counterpart is an AI too.
		public static bool PricesAtBuyerCost(Player p, Player counterpart) {
			return p != null && counterpart != null && !p.isHuman && !counterpart.isHuman;
		}

		// The least share, in percent, of a tech's value an AI takes from an
		// AI that has nothing more to give.
		public static int LeastSharePercent(GameData gameData) {
			int rate = gameData?.gameDifficulty?.AIToAITradeRate ?? 100;
			if (rate <= 100) {
				return 100;
			}
			return Math.Max(50, (int)Math.Ceiling(10000.0 / rate));
		}

		// Whether the giver's offer has all the gold it has and every tech
		// it could give the receiver.
		public static bool GivesAllItCan(GameData gameData, Player giver, Player receiver, TradeOffer offer) {
			return (offer.gold ?? 0) >= giver.gold
				&& giver.GetTechsTradableTo(receiver, gameData.techs).All(t => offer.techs.Contains(t));
		}

		// Whether us, an AI selling techs to them, an AI, takes their offer
		// though it is worth less than ours: it is all they have, and worth
		// at least the least share of ours. Only for plain trades of techs
		// and gold.
		public static bool TakesShortfall(GameData gameData, Player us, Player them, TradeOffer theirOffer, TradeOffer ourOffer,
				int theirGoldValue, int ourGoldValue) {
			if (!PricesAtBuyerCost(us, them) || ourOffer.techs.Count == 0 || theirGoldValue >= ourGoldValue) {
				return false;
			}
			bool plain = ourOffer.cities.Count == 0 && !ourOffer.GivesOverTime() && !ourOffer.worldMap && !ourOffer.territoryMap
				&& (ourOffer.contacts?.Count ?? 0) == 0 && !TradeOffer.IncludesAllianceOrPact(theirOffer, ourOffer)
				&& TradeOffer.EmbargoTarget(theirOffer, ourOffer) == null && !TradeOffer.IncludesRightOfPassage(theirOffer, ourOffer)
				&& !theirOffer.partOfPeaceTreaty && !ourOffer.partOfPeaceTreaty;
			return plain && GivesAllItCan(gameData, them, us, theirOffer)
				&& (long)theirGoldValue * 100 >= (long)ourGoldValue * LeastSharePercent(gameData);
		}
	}
}

using System;
using System.Linq;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// How an AI answers a deal on the table, as Civ3 grades it in five steps:
	// the foreign advisor's "This deal will probably be acceptable.", "We're
	// getting close to a deal here.", "I doubt they will accept this
	// proposal.", "They will probably be insulted by this deal." and "They
	// would never accept such a deal." (Conquests script.txt,
	// #DIPLOADVICETRADE_DEAL_ACCEPT, _WEAKREJECT, _NEUTRALREJECT,
	// _STRONGREJECT, _TOTALREJECT), which the leader's own replies in
	// diplomacy.txt match (#AIWILLACCEPT, #AIWEAKREJECT, #AINEUTRALREJECT,
	// #AISTRONGREJECT, #AITOTALREJECT).
	public enum DealReaction {
		Accept,
		WeakReject,
		NeutralReject,
		StrongReject,
		TotalReject,
	}

	// What an AI says to a deal a human puts on the table, and the
	// counter-offers it makes when asked, as Civ3's deal screen lets a human
	// ask: "What do you want for $GIVE0?" (Conquests diplomacy.txt,
	// #USERASKFORCOUNTER) of something the AI would give, and "Would you care
	// to offer something in exchange for $GIVE0?" (#USERASKFOREXCHANGE) of
	// something the human would give. The AI answers with a deal on the
	// table ("How about this?", #AICOUNTERPEACE) or none ("Regrettably, we
	// don't see how a deal could be worked out...", #AINOCOUNTERPEACE).
	//
	// Sources (CivFanatics; compendium 06, section 6.8):
	// - A counter-offer leaves the AI a margin: when it offers something for
	//   a tech, "Usually they give 10%-15% more of what they offer", and
	//   "The limit is near when you find they are not giving everything they
	//   have" (Qitai, "A small trick to keep AI happy", Civ3, Jun 2003,
	//   https://forums.civfanatics.com/threads/a-small-trick-to-keep-ai-happy.57086/).
	//   So the AI builds its counter-offer as though what the human gives
	//   were worth CounterValuePercent of what it is (100 / 112.5, the middle
	//   of 10-15% more), on top of its usual markup (TradeMarkup).
	//   UNVERIFIED: that "What do you want for" asks the same margin the
	//   other way, about an eighth more than the AI would take.
	// - It offers the cash it has first: asked what it would give for a map,
	//   the AI offered "the 20-40 gold that they have in stock", sometimes
	//   with furs or silk (BiggN, Apolyton, "Won't the AI offer anything?",
	//   https://apolyton.net/forum/miscellaneous/archives/civ3-general-archive/43227-won-t-the-ai-offer-anything).
	//   So it puts in gold first, then, with all its gold in, techs and
	//   resources. When it asks, it asks for gold first, then for techs and
	//   resources, the least valuable to it first, as for peace terms
	//   (PeaceAI.PeaceTermsFor), and no more gold than it then needs.
	// - No source gives the value gaps between the five reactions
	//   (HEURISTIC, see ReactionTiers).
	public static class CounterOfferAI {
		public const int CounterValuePercent = 89;

		// HEURISTIC: how much more the human's side would have to be worth
		// for the AI to take the deal, in per cent, at most, for each
		// reaction short of acceptance: up to a quarter more is close, up to
		// twice is a plain no, up to four times an insult, and beyond that,
		// or where no amount would do, never.
		private static readonly (int percentNeeded, DealReaction reaction)[] ReactionTiers = [
			(125, DealReaction.WeakReject),
			(200, DealReaction.NeutralReject),
			(400, DealReaction.StrongReject),
		];

		public static DealReaction Reaction(GameData gameData, Player ai, Player human, TradeOffer humanGives, TradeOffer humanWants) {
			if (ai.WouldAcceptDealFrom(gameData, human, humanGives, humanWants)) {
				return DealReaction.Accept;
			}
			// Would it take the deal if the human's side were worth that much
			// more?
			foreach ((int percentNeeded, DealReaction reaction) in ReactionTiers) {
				if (ai.WouldAcceptDealFrom(gameData, human, humanGives, humanWants, percentNeeded)) {
					return reaction;
				}
			}
			return DealReaction.TotalReject;
		}

		// Whether the AI will haggle with the human at all. At war the deal
		// screen asks for peace terms instead (PeaceAI.PeaceTermsFor).
		public static bool WillHaggle(GameData gameData, Player ai, Player human) {
			return ai != null && human != null && ai != human && !ai.isHuman && !ai.IsIndependent
				&& !AtWar(ai, human) && ai.WillAcceptCommunicationFrom(human, gameData.turn);
		}

		// "What do you want for $GIVE0?": what the AI asks the human to add
		// to their side for what they want of it. Returns the deal, as what
		// the human gives and what the human gets, or null if the AI sees
		// none.
		public static (TradeOffer humanGives, TradeOffer humanWants)? Counter(GameData gameData, Player ai, Player human,
				TradeOffer humanGives, TradeOffer humanWants) {
			if (!WillHaggle(gameData, ai, human) || humanGives == null || humanWants == null || humanWants.IsEmpty()) {
				return null;
			}
			TradeOffer gives = humanGives.Copy();
			TradeOffer wants = humanWants.Copy();
			bool Accepts() => ai.WouldAcceptDealFrom(gameData, human, gives, wants, CounterValuePercent);
			if (Accepts()) {
				return (gives, wants);
			}

			int? originalGold = gives.gold;
			int baseGold = originalGold ?? 0;
			int spare = Math.Max(0, human.gold - baseGold);
			// Asks for no more gold than it then needs.
			bool LeastGold() {
				gives.gold = baseGold + spare;
				if (!Accepts()) {
					return false;
				}
				int least = LeastThatWorks(spare, g => {
					gives.gold = baseGold + g;
					return Accepts();
				});
				gives.gold = least == 0 ? originalGold : baseGold + least;
				return true;
			}

			if (spare > 0 && LeastGold()) {
				return (gives, wants);
			}
			foreach (Tech t in human.GetTechsTradableTo(ai, gameData.techs).Where(t => !gives.techs.Contains(t))
					.OrderBy(t => gameData.TechCostFor(t, ai))) {
				gives.techs.Add(t);
				if (LeastGold()) {
					return (gives, wants);
				}
			}
			foreach (Resource r in TradeOffer.TradableResources(gameData, human, ai)
					.Where(r => !gives.resources.Contains(r.Key) && TradeOffer.ResourceWorth(gameData, ai, r, given: false, counterpart: human) > 0)
					.OrderBy(r => TradeOffer.ResourceWorth(gameData, ai, r, given: false, counterpart: human))) {
				gives.resources.Add(r.Key);
				if (LeastGold()) {
					return (gives, wants);
				}
			}
			return null;
		}

		// "Would you care to offer something in exchange for $GIVE0?": what
		// the AI adds to its side for what the human gives. Returns the deal,
		// as what the human gives and what the human gets, or null if the AI
		// would give nothing for it.
		public static (TradeOffer humanGives, TradeOffer humanWants)? Exchange(GameData gameData, Player ai, Player human,
				TradeOffer humanGives, TradeOffer humanWants) {
			if (!WillHaggle(gameData, ai, human) || humanGives == null || humanWants == null || humanGives.IsEmpty()) {
				return null;
			}
			TradeOffer gives = humanGives.Copy();
			TradeOffer wants = humanWants.Copy();
			bool Accepts() => ai.WouldAcceptDealFrom(gameData, human, gives, wants, CounterValuePercent);
			if (!Accepts()) {
				return null;
			}

			// Gold first, as much as it would give.
			int? originalGold = wants.gold;
			int baseGold = originalGold ?? 0;
			int spare = Math.Max(0, ai.gold - baseGold);
			int most = MostThatWorks(spare, g => {
				wants.gold = baseGold + g;
				return Accepts();
			});
			wants.gold = most == 0 ? originalGold : baseGold + most;
			bool added = most > 0;

			// With all its gold in, techs and resources, those it gives up
			// least for first.
			if (most == spare) {
				foreach (Tech t in ai.GetTechsTradableTo(human, gameData.techs).Where(t => !wants.techs.Contains(t))
						.OrderBy(t => gameData.TechCostFor(t, human))) {
					wants.techs.Add(t);
					if (Accepts()) {
						added = true;
					} else {
						wants.techs.Remove(t);
					}
				}
				foreach (Resource r in TradeOffer.TradableResources(gameData, ai, human).Where(r => !wants.resources.Contains(r.Key))
						.OrderBy(r => TradeOffer.ResourceWorth(gameData, ai, r, given: true, counterpart: human))) {
					wants.resources.Add(r.Key);
					if (Accepts()) {
						added = true;
					} else {
						wants.resources.Remove(r.Key);
					}
				}
			}
			return added ? (gives, wants) : null;
		}

		// The most, from 0 to max, for which works holds, where it holds for
		// 0 and for everything below any amount it holds for.
		private static int MostThatWorks(int max, Func<int, bool> works) {
			if (max <= 0 || works(max)) {
				return Math.Max(0, max);
			}
			int low = 0, high = max;
			while (high - low > 1) {
				int mid = low + (high - low) / 2;
				if (works(mid)) {
					low = mid;
				} else {
					high = mid;
				}
			}
			return low;
		}

		// The least, from 0 to max, for which works holds, where it holds for
		// max and for everything above any amount it holds for.
		private static int LeastThatWorks(int max, Func<int, bool> works) {
			if (works(0)) {
				return 0;
			}
			int low = 0, high = max;
			while (high - low > 1) {
				int mid = low + (high - low) / 2;
				if (works(mid)) {
					high = mid;
				} else {
					low = mid;
				}
			}
			return high;
		}
	}
}

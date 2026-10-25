using System;
using System.Linq;
using C7GameData;

namespace C7Engine.AI {
	// What an AI won't trade away, or prices higher, in deals offered to it.
	// (AI heuristics: no Civ3 source gives its AI's rules.)
	public static class TradeGuards {
		// HEURISTIC: a tech known to no more than this many other civs is
		// worth half again its cost to give away.
		private const int MonopolyKnownBy = 1;

		// Whether we won't give them what we'd give in this deal:
		// - anything to a civ we're about to attack (see WarCampaign);
		// - techs to a civ we're furious with;
		// - a strategic resource to a civ that could use it against us: one
		//   on our landmass that's stronger than we are.
		public static bool Refuses(GameData gameData, Player us, Player them, TradeOffer ourOffer) {
			bool givesSomething = ourOffer.techs.Count > 0 || (ourOffer.resources?.Count ?? 0) > 0 || ourOffer.goldPerTurn > 0
				|| (ourOffer.workers?.Count ?? 0) > 0;
			if (givesSomething && WarCampaign.IsPlannedTarget(us, them)) {
				return true;
			}
			if (ourOffer.techs.Count > 0 && Attitudes.Level(gameData, us, them) == AttitudeLevel.Furious) {
				return true;
			}
			bool givesStrategic = (ourOffer.resources ?? new()).Any(key => gameData.Resources.Find(r => r.Key == key)?.Category == ResourceCategory.STRATEGIC);
			if (givesStrategic && PeaceAI.CanFightEachOther(us, them) && them.CompareMilitaryStrengthTo(us) == Player.MilitaryStrength.StrongTo) {
				return true;
			}
			return false;
		}

		// What giving away techs few others know costs us beyond their price.
		public static int MonopolyPremium(GameData gameData, Player us, TradeOffer ourOffer) {
			int premium = 0;
			foreach (Tech t in ourOffer.techs) {
				int knownBy = gameData.players.Count(p => p != us && !p.IsIndependent && !p.defeated && p.knownTechs.Contains(t.id));
				if (knownBy <= MonopolyKnownBy) {
					premium += gameData.TechCostFor(t, us) / 2;
				}
			}
			return premium;
		}

		// How much less gold promised over time is worth from a payer that
		// may not keep paying: half, from one at war or that can barely
		// afford it.
		public static int RiskDiscount(GameData gameData, Player payer, TradeOffer theirOffer) {
			if (theirOffer.goldPerTurn <= 0) {
				return 0;
			}
			bool risky = PlayerRelationship.IsInAnyWar(payer, gameData.players) || TradeOffer.MaxGoldPerTurn(payer) < 2 * theirOffer.goldPerTurn;
			return risky ? theirOffer.goldPerTurn * TradeOffer.DealDuration(gameData) / 2 : 0;
		}
	}
}

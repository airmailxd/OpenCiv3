using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// The deals other than tech trades that an AI proposes, now and then, in
	// PlayerAI.AttemptTrading: buying a luxury or strategic resource it lacks
	// from a civ with one to spare, selling its own spares, mutual
	// protection pacts with civs it likes, and military alliances and trade
	// embargoes against its enemies (see AllianceDeal and EmbargoDeal). Civ3's AIs do offer such deals
	// (Conquests' Text/diplomacy.txt has AI lines offering them), but no
	// Civ3 source gives when or at what price, so all of this is HEURISTIC,
	// priced as the AI prices deals offered to it (TradeOffer.ResourceWorth,
	// PactAI):
	// - It buys the resource worth most to it, paying the seller's asking
	//   price per turn (or, if its income can't cover that, the whole
	//   deal's worth in gold at once), as long as that is no more than the
	//   resource is worth to it.
	// - It sells the spare the buyer values most, for gold per turn halfway
	//   between its asking price and the resource's worth to the buyer, and
	//   sells strategic resources only to civs it doesn't dislike.
	// - It offers a mutual protection pact only to a civ it is gracious
	//   toward, while neither is at war with anyone (conservatively
	//   stricter than what it accepts, see PactAI), and only some of the
	//   time (PactProposalPercent), so as not to pester a human.
	// It deals over time only with civs whose trading reputation is intact
	// (TradingReputationBroken), and never proposes what
	// TradeOffer.ProblemWithDeal rules out (such as trade under an embargo).
	public static class DealProposalAI {
		public const int PactProposalPercent = 25;

		public static (TradeOffer weGive, TradeOffer weWant)? ResourceDeal(GameData gD, Player us, Player them) {
			if (TradingReputationBroken(us, them, gD.players)) {
				return null;
			}
			return BuyResource(gD, us, them) ?? SellResource(gD, us, them);
		}

		private static (TradeOffer weGive, TradeOffer weWant)? BuyResource(GameData gD, Player us, Player them) {
			Resource wanted = null;
			int worth = 0;
			foreach (Resource r in TradeOffer.TradableResources(gD, them, us)) {
				int w = TradeOffer.ResourceWorth(gD, us, r, given: false);
				if (w > worth) {
					wanted = r;
					worth = w;
				}
			}
			if (wanted == null) {
				return null;
			}
			int ask = TradeOffer.ResourceWorth(gD, them, wanted, given: true);
			if (ask > worth) {
				return null;
			}
			TradeOffer weGive = new();
			int lumpSum = ask * TradeOffer.DealDuration(gD);
			if (ask <= TradeOffer.MaxGoldPerTurn(us)) {
				weGive.goldPerTurn = ask;
			} else if (lumpSum <= us.gold) {
				weGive.gold = lumpSum;
			} else {
				return null;
			}
			TradeOffer weWant = new() { resources = { wanted.Key } };
			return Checked(gD, us, them, weGive, weWant);
		}

		private static (TradeOffer weGive, TradeOffer weWant)? SellResource(GameData gD, Player us, Player them) {
			bool dislike = Attitudes.Level(gD, us, them) is AttitudeLevel.Annoyed or AttitudeLevel.Furious;
			Resource best = null;
			int bestPrice = 0;
			int maxPerTurn = -1;
			foreach (Resource r in TradeOffer.TradableResources(gD, us, them)) {
				if (dislike && r.Category == ResourceCategory.STRATEGIC) {
					continue;
				}
				int value = TradeOffer.ResourceWorth(gD, them, r, given: false);
				int ask = TradeOffer.ResourceWorth(gD, us, r, given: true);
				if (value < ask) {
					continue;
				}
				if (maxPerTurn < 0) {
					maxPerTurn = TradeOffer.MaxGoldPerTurn(them);
				}
				int price = Math.Min((value + ask + 1) / 2, maxPerTurn);
				if (price >= ask && price > bestPrice) {
					best = r;
					bestPrice = price;
				}
			}
			if (best == null || bestPrice <= 0) {
				return null;
			}
			return Checked(gD, us, them, new TradeOffer { resources = { best.Key } }, new TradeOffer { goldPerTurn = bestPrice });
		}

		public static (TradeOffer weGive, TradeOffer weWant)? PactDeal(GameData gD, Player us, Player them) {
			if (Pacts.ProblemSigningMutualProtectionPact(gD, us, them) != null
				|| TradingReputationBroken(us, them, gD.players)
				|| Attitudes.Level(gD, us, them) != AttitudeLevel.Gracious
				|| IsInAnyWar(us, gD.players) || IsInAnyWar(them, gD.players)) {
				return null;
			}
			if (GameData.rng.Next(100) >= PactProposalPercent) {
				return null;
			}
			return Checked(gD, us, them, new TradeOffer { mutualProtectionPact = true }, new TradeOffer { mutualProtectionPact = true });
		}

		// HEURISTIC: when at war, the AI asks a civ that is annoyed or
		// furious with the enemy, and not yet at war with it, to join the war
		// in a military alliance, paying what it would ask to join
		// (PactAI.JoinWarGold and JoinWarGoldPerCity for each of the enemy's
		// cities), if it can spare that gold. Against the enemy with the most
		// cities, if there's a choice. Only some of the time
		// (PactProposalPercent), and the same civ not again for
		// TurnsBetweenRequests turns. Civ3's AI does ask: "Let us punish them
		// together!" (Conquests diplomacy.txt, #AIMILITARYALLIANCE).
		public static (TradeOffer weGive, TradeOffer weWant)? AllianceDeal(GameData gD, Player us, Player them) {
			if (TradingReputationBroken(us, them, gD.players) || AskedRecently(gD, us, them)) {
				return null;
			}
			Player enemy = null;
			foreach (Player p in gD.players) {
				if (p == us || p == them || p.isBarbarians || p.defeated || !AtWar(us, p) || AtWar(them, p)
					|| Attitudes.Level(gD, them, p) is not (AttitudeLevel.Annoyed or AttitudeLevel.Furious)
					|| Pacts.ProblemSigningMilitaryAlliance(gD, us, them, p) != null) {
					continue;
				}
				if (enemy == null || p.cities.Count > enemy.cities.Count) {
					enemy = p;
				}
			}
			if (enemy == null) {
				return null;
			}
			int price = PactAI.JoinWarGold + PactAI.JoinWarGoldPerCity * enemy.cities.Count;
			if (us.gold - price < AllianceGoldReserve || GameData.rng.Next(100) >= PactProposalPercent) {
				return null;
			}
			(TradeOffer, TradeOffer)? deal = Checked(gD, us, them,
				new TradeOffer { gold = price, militaryAllianceAgainst = enemy.id },
				new TradeOffer { militaryAllianceAgainst = enemy.id });
			if (deal != null) {
				RecordRequest(gD, us, them);
			}
			return deal;
		}

		// HEURISTIC: the AI asks a civ to join an embargo against a civ it is
		// furious with (or at war with) that the other civ doesn't like
		// better than it likes us, so that the other civ would agree (see
		// PactAI). Rate-limited like AllianceDeal. Civ3's AI does ask: "Join
		// us in a trade embargo against pathetic $CIVNAME3" (Conquests
		// diplomacy.txt, #AITRADEEMBARGO).
		public static (TradeOffer weGive, TradeOffer weWant)? EmbargoDeal(GameData gD, Player us, Player them) {
			if (TradingReputationBroken(us, them, gD.players) || AskedRecently(gD, us, them)) {
				return null;
			}
			int? towardUs = Attitudes.Points(gD, them, us);
			Player target = null;
			foreach (Player p in gD.players) {
				if (p == us || p == them || p.isBarbarians || p.defeated
					|| !(AtWar(us, p) || Attitudes.Level(gD, us, p) == AttitudeLevel.Furious)
					|| !(AtWar(them, p) || Attitudes.Level(gD, them, p) is AttitudeLevel.Annoyed or AttitudeLevel.Furious)
					|| Pacts.ProblemSigningTradeEmbargo(gD, us, them, p) != null) {
					continue;
				}
				int? towardTarget = Attitudes.Points(gD, them, p);
				if (!AtWar(them, p) && towardUs != null && towardTarget != null && towardUs >= towardTarget) {
					continue;
				}
				if (target == null || p.cities.Count > target.cities.Count) {
					target = p;
				}
			}
			if (target == null || GameData.rng.Next(100) >= PactProposalPercent) {
				return null;
			}
			(TradeOffer, TradeOffer)? deal = Checked(gD, us, them,
				new TradeOffer { tradeEmbargoAgainst = target.id },
				new TradeOffer { tradeEmbargoAgainst = target.id });
			if (deal != null) {
				RecordRequest(gD, us, them);
			}
			return deal;
		}

		// What the AI says to a human it offers a military alliance or
		// embargo, from Conquests' diplomacy.txt (#AIMILITARYALLIANCE,
		// #AITRADEEMBARGO), or null for other deals.
		public static string Greeting(GameData gD, TradeOffer weGive, TradeOffer weWant) {
			if (TradeOffer.AllianceTarget(weGive, weWant) is ID allianceTarget && gD.GetPlayer(allianceTarget) is Player enemy) {
				return $"I believe that we have a common interest: punishing the warmongering {enemy.civilization.noun}. Join our crusade.";
			}
			if (TradeOffer.EmbargoTarget(weGive, weWant) is ID embargoTarget && gD.GetPlayer(embargoTarget) is Player target) {
				return $"Join us in a trade embargo against pathetic {target.civilization.name}. They are not deserving of your commerce.";
			}
			return null;
		}

		// HEURISTIC: the gold the AI keeps when paying a civ to join a war.
		public const int AllianceGoldReserve = 25;
		// HEURISTIC: how often to ask the same civ for an alliance or embargo.
		public const int TurnsBetweenRequests = 10;

		private static readonly ConditionalWeakTable<Player, Dictionary<ID, int>> lastRequestTurn = new();

		private static bool AskedRecently(GameData gD, Player us, Player them) {
			return lastRequestTurn.TryGetValue(us, out Dictionary<ID, int> turns) && turns.TryGetValue(them.id, out int turn)
				&& gD.turn - turn < TurnsBetweenRequests;
		}

		private static void RecordRequest(GameData gD, Player us, Player them) {
			lastRequestTurn.GetOrCreateValue(us)[them.id] = gD.turn;
		}

		private static (TradeOffer weGive, TradeOffer weWant)? Checked(GameData gD, Player us, Player them, TradeOffer weGive, TradeOffer weWant) {
			if (TradeOffer.ProblemWithDeal(gD, us, them, weGive, weWant) != null) {
				return null;
			}
			return (weGive, weWant);
		}
	}
}

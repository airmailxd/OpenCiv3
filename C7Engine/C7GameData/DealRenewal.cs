using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using C7Engine;

namespace C7GameData {
	// What happens when a deal made for a number of turns runs out (see
	// PlayerRelationship.CheckForObsoleteDeals): the humans in it are told,
	// and on its next turn an AI in it offers to renew it on the same terms
	// if it would still accept them, or tells a human it won't. In Civ3 the
	// AI's leader comes to say a deal "is about to expire", and either that
	// "we do not wish to extend the deal at this time" (Conquests'
	// Text/diplomacy.txt, AIRENEGOTIATE) or offers to renew it
	// (AIRENEGOTIATEPEACE: "Our treaty has nearly expired ... Shall we renew
	// this?"). UNVERIFIED: Civ3 raises it a turn before the deal ends and
	// the deal carries on if renewed; here the deal has already ended, and
	// renewing makes a new one for the full length. The "Always Renegotiate
	// Deals" preference isn't modelled.
	//
	// The deals waiting for their AI's turn aren't saved, so a game saved
	// and loaded in between loses the offer (the deal still ends).
	public static class DealRenewal {
		// The terms of the deals between a and b that ran out on turn.
		public sealed class Expired {
			public Player a, b;
			public int turn;
			public TradeOffer aGives = new(), bGives = new();
		}

		private static readonly ConditionalWeakTable<GameData, List<Expired>> pending = new();

		private static List<Expired> Pending(GameData gameData) => pending.GetValue(gameData, _ => new List<Expired>());

		// Records a deal between player and other (player's copy) that has
		// just run out, and tells the humans in it.
		public static void OnExpired(GameData gameData, Player player, Player other, MultiTurnDeal deal) {
			if (gameData == null || deal == null) {
				return;
			}
			List<Expired> list = Pending(gameData);
			Expired e = list.Find(x => (x.a == player && x.b == other) || (x.a == other && x.b == player));
			if (e == null) {
				e = new Expired { a = player, b = other, turn = gameData.turn };
				list.Add(e);
			}
			e.turn = gameData.turn;
			bool playerIsA = e.a == player;
			TradeOffer playerGives = playerIsA ? e.aGives : e.bGives;
			TradeOffer otherGives = playerIsA ? e.bGives : e.aGives;
			AddTerms(deal, playerGives, otherGives);

			Notify(player, other, Describe(deal, gameData));
			Notify(other, player, Describe(Mirror(deal), gameData));
		}

		// Adds a deal's terms, as one side's copy of it, to what that side
		// and the other would give to make it again.
		internal static void AddTerms(MultiTurnDeal deal, TradeOffer sideGives, TradeOffer otherGives) {
			TradeOffer giver = deal.dealDetails == DealDetails.Inbound ? otherGives : sideGives;
			switch (deal.dealSubType) {
				case DealSubType.GoldPerTurn:
					giver.goldPerTurn += deal.goldPerTurn;
					break;
				case DealSubType.ResourcePerTurn:
				case DealSubType.LuxuryPerTurn:
					if (deal.resourcePerTurn != null && !giver.resources.Contains(deal.resourcePerTurn)) {
						giver.resources.Add(deal.resourcePerTurn);
					}
					break;
				case DealSubType.RightOfPassage:
					sideGives.rightOfPassage = otherGives.rightOfPassage = true;
					break;
				case DealSubType.MutualProtectionPact:
					sideGives.mutualProtectionPact = otherGives.mutualProtectionPact = true;
					break;
				case DealSubType.MilitaryAlliance:
					sideGives.militaryAllianceAgainst = otherGives.militaryAllianceAgainst = deal.againstPlayer;
					break;
				case DealSubType.TradeEmbargo:
					sideGives.tradeEmbargoAgainst = otherGives.tradeEmbargoAgainst = deal.againstPlayer;
					break;
			}
		}

		// The other side's copy of a one-way deal.
		private static MultiTurnDeal Mirror(MultiTurnDeal deal) {
			DealDetails details = deal.dealDetails switch {
				DealDetails.Inbound => DealDetails.Outbound,
				DealDetails.Outbound => DealDetails.Inbound,
				_ => deal.dealDetails,
			};
			return new MultiTurnDeal(deal.dealType, deal.dealSubType, details, deal.goldPerTurn, deal.resourcePerTurn,
				deal.dealDuration, deal.turnStartDeal, deal.againstPlayer);
		}

		private static string Describe(MultiTurnDeal deal, GameData gameData) {
			bool outbound = deal.dealDetails == DealDetails.Outbound;
			string Against() => gameData.GetPlayer(deal.againstPlayer)?.civilization?.noun ?? "others";
			string resource = gameData.Resources.Find(r => r.Key == deal.resourcePerTurn)?.Name ?? deal.resourcePerTurn;
			return deal.dealSubType switch {
				DealSubType.GoldPerTurn => outbound ? $"pay {deal.goldPerTurn} gold per turn" : $"receive {deal.goldPerTurn} gold per turn",
				DealSubType.ResourcePerTurn or DealSubType.LuxuryPerTurn => outbound ? $"send {resource}" : $"receive {resource}",
				DealSubType.RightOfPassage => "a right of passage",
				DealSubType.MutualProtectionPact => "a mutual protection pact",
				DealSubType.MilitaryAlliance => $"a military alliance against the {Against()}",
				DealSubType.TradeEmbargo => $"a trade embargo against the {Against()}",
				_ => "a deal",
			};
		}

		private static void Notify(Player human, Player other, string terms) {
			if (human == null || !human.isHuman) {
				return;
			}
			Tell(human, $"Our deal with the {other?.civilization?.noun} for {terms} has expired.");
		}

		// Shows a human a message over their capital. Hotseat holds it until
		// it is their turn.
		public static void Tell(Player human, string message) {
			City city = human.cities.Find(c => c.IsCapital()) ?? human.cities.FirstOrDefault();
			if (city != null) {
				new MsgShowTemporaryPopup(message, city.location, human).send();
			}
		}

		// Takes the expired deals the AI is in, as (what the AI gave, the
		// other civ, what the other civ gave), for the AI to offer again.
		// Deals between humans, and ones left from an earlier turn whose AI
		// never played (such as a destroyed one), are dropped.
		public static List<(TradeOffer aiGives, Player other, TradeOffer otherGives)> TakeFor(GameData gameData, Player ai) {
			List<(TradeOffer, Player, TradeOffer)> result = new();
			List<Expired> list = Pending(gameData);
			list.RemoveAll(e => e.turn < gameData.turn - 1 || (e.a.isHuman && e.b.isHuman));
			foreach (Expired e in list.Where(e => e.a == ai || e.b == ai).ToList()) {
				list.Remove(e);
				if (e.a == ai) {
					result.Add((e.aGives, e.b, e.bGives));
				} else {
					result.Add((e.bGives, e.a, e.aGives));
				}
			}
			return result;
		}
	}
}

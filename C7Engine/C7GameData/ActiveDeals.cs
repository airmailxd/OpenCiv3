using System.Collections.Generic;

namespace C7GameData;

// The deals in force between a player and another civ, as Civ3's "Active
// Deals" button on the negotiation screen lists them.
public class ActiveDeals {
	// Agreements both sides keep, like peace or a right of passage.
	public List<string> agreements = new();
	// What the other civ gives the player each turn.
	public List<string> theyGive = new();
	// What the player gives the other civ each turn.
	public List<string> weGive = new();

	public bool IsEmpty => agreements.Count == 0 && theyGive.Count == 0 && weGive.Count == 0;

	public static ActiveDeals Between(GameData gameData, Player player, Player other) {
		ActiveDeals result = new();
		if (!PlayerRelationship.TryGetRelationship(player, other, out PlayerRelationship relationship)) {
			return result;
		}

		foreach (MultiTurnDeal deal in relationship.multiTurnDeals) {
			if (deal == null) {
				continue;
			}
			string description = Describe(gameData, player, other, deal);
			if (description == null) {
				continue;
			}
			switch (deal.dealDetails) {
				case DealDetails.Inbound:
					result.theyGive.Add(description);
					break;
				case DealDetails.Outbound:
					result.weGive.Add(description);
					break;
				default:
					result.agreements.Add(description);
					break;
			}
		}
		return result;
	}

	private static string Describe(GameData gameData, Player player, Player other, MultiTurnDeal deal) {
		string name;
		switch (deal.dealSubType) {
			case DealSubType.Peace:
				name = "Peace Treaty";
				break;
			case DealSubType.MutualProtectionPact:
				name = "Mutual Protection Pact";
				break;
			case DealSubType.RightOfPassage:
				name = "Right of Passage";
				break;
			case DealSubType.MilitaryAlliance:
				name = $"Military Alliance against the {CivNoun(gameData, deal.againstPlayer)}";
				break;
			case DealSubType.TradeEmbargo:
				name = $"Trade Embargo against the {CivNoun(gameData, deal.againstPlayer)}";
				break;
			case DealSubType.GoldPerTurn:
				name = $"{deal.goldPerTurn} gold per turn";
				break;
			case DealSubType.ResourcePerTurn:
			case DealSubType.LuxuryPerTurn:
				name = gameData.Resources.Find(r => r.Key == deal.resourcePerTurn)?.Name ?? deal.resourcePerTurn;
				break;
			default:
				return null;
		}

		// Peace made at first contact, and pacts between locked allies, have
		// no end; peace signed to end a war counts down its first turns.
		bool lockedPact = deal.dealSubType == DealSubType.MutualProtectionPact
			&& gameData.AreInLockedPeace(player, other);
		int turnsLeft = deal.TurnsRemaining(gameData.turn);
		if (turnsLeft > 0 && !lockedPact) {
			name += $" ({turnsLeft} {(turnsLeft == 1 ? "turn" : "turns")})";
		}
		return name;
	}

	private static string CivNoun(GameData gameData, ID playerId) {
		return gameData.GetPlayer(playerId)?.civilization?.noun ?? "unknown";
	}
}

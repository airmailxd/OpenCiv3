using System.Collections.Generic;
using System.Linq;

namespace C7GameData {
	// The rules of Civ3's right of passage agreement, which "allow[s] his or
	// her military units to move through your territory and use your
	// transportation network (roads and railroads)" (Conquests Civilopedia,
	// GCON_Agreements). It is mutual: "RoP is a two way street. As you enter
	// their territory freely, they can also enter your territory"
	// (https://forums.civfanatics.com/threads/rite-of-passage.66734/), so it
	// is kept as an Exchange deal (DealSubType.RightOfPassage).
	//
	// What it does, and where:
	// - Units may stay in the other civ's territory without being told to
	//   leave or face war (see TerritoryDemands.MustStayOutOf).
	// - "Rival civilizations cannot benefit from the roads or rails within
	//   your territory without a right of passage agreement" (Conquests
	//   Civilopedia, GCON_Territory); see Player.CanMoveFreely.
	// - It lasts the rules' deal length, 20 turns in Civ3 ("It expires after
	//   20 turns",
	//   https://forums.civfanatics.com/threads/right-of-passage.9133/); see
	//   PlayerRelationship.CheckForObsoleteDeals.
	// - Declaring war ends it, like every agreement. Units inside the
	//   borders stay where they are (the "RoP rape" of
	//   https://forums.civfanatics.com/threads/not-a-bug-unit-teleportation-while-redeclaring-war-after-a-peace-treaty.399474/:
	//   "In Civ3, you could move your units into the enemies land when you
	//   had the rights of passage, and then declare war on it"), but that is
	//   a sneak attack, after which "other civs won't sign ROP with you
	//   anymore" (https://civfanatics.com/civ3/strategy/game-mechanics/ai-attitude-study/);
	//   see PlayerRelationship.rightOfPassageBetrayals.
	public static class RightOfPassage {
		// Why the proposer can't sign a right of passage with the other civ
		// now, or null if it can.
		//
		// - The proposer must know Writing and have an embassy with the
		//   other civ (owner's decision, 2026-10-11: "The deal creator must
		//   have Writing and an embassy. Map making isnt needed"). The BIQ's
		//   flag on Map Making (Tech.EnablesRightOfPassage, "Can now
		//   negotiate right of passage agreements", Conquests Civilopedia,
		//   TECH_Map_Making) is read but not used. Rules with no tech named
		//   Writing need none.
		// - At peace: at war the only deal is peace (TradeOffer.ProblemWithDeal).
		// - Not already in force.
		public static string ProblemSigning(GameData gameData, Player a, Player b) {
			if (a == null || b == null || a == b) {
				return "a civ can't sign a right of passage with itself";
			}
			if (!PlayerRelationship.TryGetRelationship(a, b, out PlayerRelationship ab)
				|| !PlayerRelationship.TryGetRelationship(b, a, out PlayerRelationship ba)) {
				return "the civs haven't met";
			}
			if (ab.AtWar()) {
				return "the civs are at war";
			}
			if (PlayerRelationship.HaveActiveRightOfPassage(a, b)) {
				return "the civs already have a right of passage";
			}
			if (!ab.hasEmbassy) {
				return "a right of passage needs an embassy with the other civ";
			}
			if (!KnowsWriting(gameData, a)) {
				return "a right of passage needs Writing";
			}
			return null;
		}

		public static bool CanSign(GameData gameData, Player a, Player b) {
			return ProblemSigning(gameData, a, b) == null;
		}

		// Whether the player knows Writing (the tech with the BIQ's "enables
		// diplomats" flag), or the rules have no such tech.
		public static bool KnowsWriting(GameData gameData, Player player) {
			return player.KnowsEmbassyTech(gameData?.techs);
		}

		// Signs a right of passage between the two, for the rules' deal
		// length from this turn.
		public static void Sign(GameData gameData, Player a, Player b) {
			PlayerRelationship.RegisterMultiTurnDeal(a, b, new MultiTurnDeal(DealType.DiplomaticAgreement,
				DealSubType.RightOfPassage, DealDetails.Exchange, 0, null, gameData.rules.DefaultDealDuration, gameData.turn));
		}

		// Whether the civ broke a right of passage by declaring war with its
		// units inside the other civ's borders, which every civ remembers:
		// "If you have any units in their territory when you break the ROP
		// treaty, ... other civs won't sign ROP with you anymore"
		// (https://civfanatics.com/civ3/strategy/game-mechanics/ai-attitude-study/).
		// The betrayal is recorded in the victim's view of the betrayer.
		public static bool HasBetrayed(Player betrayer, IEnumerable<Player> players) {
			foreach (Player p in players) {
				if (p != betrayer && p.playerRelationships.TryGetValue(betrayer.id, out PlayerRelationship view)
					&& view.rightOfPassageBetrayals > 0) {
					return true;
				}
			}
			return false;
		}
	}
}

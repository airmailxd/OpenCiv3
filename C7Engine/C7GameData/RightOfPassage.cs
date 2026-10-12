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
		// Why the two civs can't sign a right of passage now, or null if
		// they can.
		//
		// - An embassy, either way round: "When your civilization builds an
		//   embassy in a rival's capital city, or when a rival builds one in
		//   your capital, you can negotiate Diplomatic Agreements with that
		//   civilization" (Conquests Civilopedia, GCON_Agreements), and the
		//   project owner: "An embassy must be present to make a ROP"
		//   (owner's decision, 2026-10-11).
		// - The tech that enables it, Map Making in Civ3 ("Can now
		//   negotiate right of passage agreements", Conquests Civilopedia,
		//   TECH_Map_Making; the BIQ flags it on Map Making alone).
		//   UNVERIFIED (no Civ3 source found): that both civs need it, not
		//   just one. A game whose rules flag no tech (like a save made
		//   before the flag was read) needs none.
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
			if (!ab.hasEmbassy && !ba.hasEmbassy) {
				return "neither civ has an embassy with the other";
			}
			if (!KnowsEnablingTech(gameData, a) || !KnowsEnablingTech(gameData, b)) {
				return "a right of passage needs Map Making";
			}
			return null;
		}

		public static bool CanSign(GameData gameData, Player a, Player b) {
			return ProblemSigning(gameData, a, b) == null;
		}

		// Whether the player knows a tech that lets civs sign rights of
		// passage, or the rules have no such tech.
		public static bool KnowsEnablingTech(GameData gameData, Player player) {
			List<Tech> techs = gameData?.techs;
			if (techs == null || !techs.Any(t => t.EnablesRightOfPassage)) {
				return true;
			}
			return techs.Any(t => t.EnablesRightOfPassage && player.knownTechs.Contains(t.id));
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

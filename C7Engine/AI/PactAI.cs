using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// How the AI weighs mutual protection pacts, military alliances and trade
	// embargoes offered to it (see Pacts for the rules). It is wary of them,
	// as each can drag it into a war or cut its trade.
	//
	// HEURISTIC throughout, except where cited: no Civ3 source gives the AI's
	// prices for these.
	// - A military alliance: Civ3's AI refuses unless the other civ is
	//   already at war with the enemy: "if you haven't declared war yet,
	//   you'll typically get an answer like 'We don't think a deal can be
	//   worked out'", and "You may very well have to pay the other civ
	//   gold/techs/resources to get them to sign on with you"
	//   (https://forums.civfanatics.com/threads/how-to-make-alliances.249407/,
	//   https://forums.civfanatics.com/threads/how-to-form-a-strategic-alliance.154837/).
	//   An AI already at war with the enemy joins for nothing; otherwise it
	//   must be at least annoyed with the enemy and be paid JoinWarGold plus
	//   JoinWarGoldPerCity for each of the enemy's cities.
	// - A mutual protection pact: only with a civ it is polite or gracious
	//   toward, which isn't at war with anyone (so as not to inherit a
	//   war), and that it isn't annoyed or furious at.
	// - A trade embargo: only against a civ it is at war with or annoyed or
	//   furious at, and that it likes less than the civ asking.
	// Beyond that, the rest of the deal must balance (theirValue >= ourValue).
	public static class PactAI {
		public const int JoinWarGold = 50;
		public const int JoinWarGoldPerCity = 10;

		public static bool WouldAccept(GameData gameData, Player us, Player them, TradeOffer theirOffer, TradeOffer ourOffer,
				int theirValue, int ourValue) {
			int price = ourValue;
			if (TradeOffer.IncludesMutualProtectionPact(theirOffer, ourOffer)) {
				AttitudeLevel? level = Attitudes.Level(gameData, us, them);
				if (level is not (AttitudeLevel.Gracious or AttitudeLevel.Polite) || IsInAnyWar(them, gameData.players)) {
					return false;
				}
			}
			ID allianceTarget = TradeOffer.AllianceTarget(theirOffer, ourOffer);
			if (allianceTarget != null) {
				Player enemy = gameData.GetPlayer(allianceTarget);
				if (enemy == null || !AtWar(them, enemy)) {
					return false;
				}
				if (!AtWar(us, enemy)) {
					if (!Dislikes(gameData, us, enemy)) {
						return false;
					}
					// Joining a war is only worth a price if, with the civ
					// asking, we'd win it (and can reach the enemy).
					if (!PeaceAI.WouldWinWarWith(gameData, us, enemy, them.CalculateMilitaryStrength())) {
						return false;
					}
					price += JoinWarGold + JoinWarGoldPerCity * enemy.cities.Count;
				}
			}
			ID embargoTarget = TradeOffer.EmbargoTarget(theirOffer, ourOffer);
			if (embargoTarget != null) {
				Player target = gameData.GetPlayer(embargoTarget);
				if (target == null || !(AtWar(us, target) || Dislikes(gameData, us, target))) {
					return false;
				}
				int? towardTarget = Attitudes.Points(gameData, us, target);
				int? towardThem = Attitudes.Points(gameData, us, them);
				if (!AtWar(us, target) && towardTarget != null && towardThem != null && towardThem >= towardTarget) {
					return false;
				}
			}
			return theirValue >= price;
		}

		private static bool Dislikes(GameData gameData, Player us, Player other) {
			return Attitudes.Level(gameData, us, other) is AttitudeLevel.Annoyed or AttitudeLevel.Furious;
		}
	}
}

using System;
using System.Linq;
using static C7GameData.PlayerRelationship;

namespace C7GameData {
	// How an AI feels about another civ, from best to worst, as Civ3 names it.
	public enum AttitudeLevel {
		Gracious,
		Polite,
		Cautious,
		Annoyed,
		Furious,
	}

	// An AI's attitude toward another civ, as in Civ3. The AI Attitude Study
	// (https://civfanatics.com/civ3/strategy/game-mechanics/ai-attitude-study/)
	// counts it in points where "Good things actually give you a negative
	// number, and bad things give you a positive number": "-11 and lower =
	// Gracious", "-1 through -10 = Polite", "0 = Cautious", "1-10 =
	// Annoyed", "11 through 100 = Furious". The values below are the
	// study's, for the factors the engine tracks. It doesn't track razed
	// cities, privateers or the temporary effects of combat, so they don't
	// count.
	//
	// p1.playerRelationships[p2] records what p2 did to p1, so the factors
	// of the AI's attitude toward another civ are in
	// ai.playerRelationships[other].
	public static class Attitudes {
		public const int GraciousAttitude = -11;
		public const int PoliteAttitude = -1;
		public const int AnnoyedAttitude = 1;
		public const int FuriousAttitude = 11;

		// HEURISTIC: how many turns a trade of techs counts as recent, and
		// how long the AI remembers being paid tribute. The study gives no
		// length for either; 20 turns is the length of a per-turn deal, and
		// about the time before an AI demands tribute again
		// (https://forums.civfanatics.com/threads/ai-tribute.165970/).
		public const int RecentTradeTurns = 20;
		public const int TributeMemoryTurns = 20;

		// "-1 for each 10 gold worth of techs/gold you donate, up to a
		// maximum of -10 (100 gold). This bonus decays at about 10
		// gold/turn."
		public const int GiftGoldPerPoint = 10;
		public const int MaxGiftPoints = 10;
		public const int GiftGoldDecayPerTurn = 10;

		public static AttitudeLevel LevelOf(int points) {
			if (points <= GraciousAttitude) {
				return AttitudeLevel.Gracious;
			} else if (points <= PoliteAttitude) {
				return AttitudeLevel.Polite;
			} else if (points < AnnoyedAttitude) {
				return AttitudeLevel.Cautious;
			} else if (points < FuriousAttitude) {
				return AttitudeLevel.Annoyed;
			}
			return AttitudeLevel.Furious;
		}

		// The AI's attitude toward the other civ, or null if they haven't met.
		public static AttitudeLevel? Level(GameData gameData, Player ai, Player other) {
			int? points = Points(gameData, ai, other);
			return points == null ? null : LevelOf(points.Value);
		}

		// The study's difficulty bands, which change what the AI's
		// aggression adds: 0 for Chieftain and Warlord, 1 for Regent and
		// Monarch, 2 for Emperor and harder, or -1 if the game's difficulty
		// isn't one of its listed ones. UNVERIFIED: that Sid (Creator), which
		// the study doesn't cover, is in the hardest band.
		public static int DifficultyBand(GameData gameData) {
			string name = gameData?.gameDifficulty?.Name;
			int index = name == null ? -1 : gameData.difficulties.FindIndex(d => d.Name == name);
			if (index < 0) {
				return -1;
			}
			return index <= 1 ? 0 : index <= 3 ? 1 : 2;
		}

		// What the AI's aggression adds to its attitude toward everyone. The
		// study lists the aggression levels 1 to 5, which are the editor's -2
		// to 2: "Chieftain-Warlord: 5 = +2 ... 4 = +1 ... 3 = 0 ... 2 = -1
		// ... 1 = -1", "Regent-Monarch: 5 = +3, 4 = +2, 3 = +1, 2 = 0, 1 =
		// -1", "Emperor-Deity: 5 = +3, 4 = +3, 3 = +2, 2 = +1, 1 = 0".
		private static readonly int[][] AggressionPoints = [
			[-1, -1, 0, 1, 2],
			[-1, 0, 1, 2, 3],
			[0, 1, 2, 3, 3],
		];

		public static int AggressionAttitude(GameData gameData, Player ai) {
			int band = DifficultyBand(gameData);
			if (band < 0 || ai.civilization == null) {
				return 0;
			}
			int level = Math.Clamp(ai.civilization.aggressionLevel, -2, 2) + 2;
			return AggressionPoints[band][level];
		}

		// The gold worth of the gifts the other civ gave the AI that it still
		// remembers, after the decay since the last one.
		public static int RememberedGiftGold(PlayerRelationship ourView, int turn) {
			if (ourView.giftGold <= 0 || ourView.giftTurn < 0) {
				return 0;
			}
			return Math.Max(0, ourView.giftGold - GiftGoldDecayPerTurn * (turn - ourView.giftTurn));
		}

		// Records that the other civ gave the AI this much gold, or techs
		// worth it.
		public static void RecordGift(PlayerRelationship ourView, int gold, int turn) {
			if (gold <= 0) {
				return;
			}
			ourView.giftGold = RememberedGiftGold(ourView, turn) + gold;
			ourView.giftTurn = turn;
		}

		// The AI's attitude toward the other civ in the study's points (lower
		// is better), or null if they haven't met.
		public static int? Points(GameData gameData, Player ai, Player other) {
			if (!TryGetRelationship(ai, other, out PlayerRelationship pr)) {
				return null;
			}
			TryGetRelationship(other, ai, out PlayerRelationship theirs);
			int turn = gameData.turn;

			// Good things (negative points). The study: "Same culture group:
			// -1", "Same government: -1", "Right of Passage: -5", "Mutual
			// Protection Pact: -10", "Alliance signed: -2", "Trade embargo
			// signed: -1", "Trade/donate resource: -5", "Recent trades: -1",
			// "War with common enemy: -3", "Embassy: -2".
			int good = 0;
			string cultureGroup = ai.civilization?.cultureGroup?.name;
			if (cultureGroup != null && cultureGroup == other.civilization?.cultureGroup?.name) {
				good -= 1;
			}
			string ourGovernment = ai.government?.name;
			string theirGovernment = other.government?.name;
			if (ourGovernment != null && ourGovernment == theirGovernment) {
				good -= 1;
			}
			// "-5 Favorite government (only applies if both you and the AI
			// are in this government)".
			string favorite = ai.civilization?.favoriteGovernment;
			if (favorite != null && ourGovernment == favorite && theirGovernment == favorite) {
				good -= 5;
			}
			bool HasDeal(params DealSubType[] types) => pr.multiTurnDeals.Any(d => types.Contains(d.dealSubType));
			// "ROP is one of them that still gives you the full points no
			// matter who has the power lead", so it is added after the
			// halving below.
			int rightOfPassage = HasDeal(DealSubType.RightOfPassage) ? -5 : 0;
			if (HasDeal(DealSubType.MutualProtectionPact)) {
				good -= 10;
			}
			// UNVERIFIED: that a scenario's alliance counts as one signed.
			if (HasDeal(DealSubType.MilitaryAlliance) || (ai.alliance != null && ai.alliance == other.alliance)) {
				good -= 2;
			}
			if (HasDeal(DealSubType.TradeEmbargo)) {
				good -= 1;
			}
			// UNVERIFIED: that a resource deal counts whichever way the
			// resource goes, and that a gold-per-turn deal is a recent trade.
			if (HasDeal(DealSubType.ResourcePerTurn, DealSubType.LuxuryPerTurn)) {
				good -= 5;
			}
			// "-1 if you have done any recent trades (maps or techs) or
			// currently have a 20 turn deal with them".
			bool recentTrade = pr.lastTechTradeTurn >= 0 && turn - pr.lastTechTradeTurn <= RecentTradeTurns;
			if (HasDeal(DealSubType.GoldPerTurn) || recentTrade) {
				good -= 1;
			}
			if (gameData.players.Any(p => p != ai && p != other
				&& TryGetRelationship(ai, p, out PlayerRelationship v) && v.AtWar()
				&& TryGetRelationship(other, p, out PlayerRelationship c) && c.AtWar())) {
				good -= 3;
			}
			if (theirs?.hasEmbassy == true) {
				good -= 2;
			}
			// "-1 You pay tribute to the AI."
			if (pr.lastTributePaidTurn >= 0 && turn - pr.lastTributePaidTurn <= TributeMemoryTurns) {
				good -= 1;
			}
			// Gifts (see GiftGoldPerPoint).
			good -= Math.Min(MaxGiftPoints, RememberedGiftGold(pr, turn) / GiftGoldPerPoint);
			// "-1 If you have a culture lead. +1 if the AI has the culture
			// lead." UNVERIFIED: that a lead is any difference in the two
			// civs' total culture.
			int cultureLead = Math.Sign(TotalCulture(other) - TotalCulture(ai));
			if (cultureLead > 0) {
				good -= 1;
			}
			// "If you have a power lead, most of the good effects (negative
			// numbers) are halved." UNVERIFIED: that a power lead is being
			// stronger than the AI as the military advisor judges it
			// (Player.CompareMilitaryStrengthTo), that all the good effects
			// are halved, and that halves are rounded toward zero.
			if (other.CompareMilitaryStrengthTo(ai) == Player.MilitaryStrength.StrongTo) {
				good /= 2;
			}
			good += rightOfPassage;

			// Bad things (positive points), which the study counts for good:
			// "Declared war previously: +4", "Break ROP (no units in
			// territory): +4", "Failed espionage (permanent): +1", "Use nukes:
			// +32 victim (other civs: +16)", and "+6 If you have any units
			// in their territory when you break the ROP treaty, ... other
			// civs ... will add +2". UNVERIFIED: the engine doesn't record
			// the temporary +4 of a failed mission, or who a nuclear attack
			// hit, so each counts as given.
			int bad = 4 * pr.warDeclarationCount
				+ 4 * (pr.warDeclarationWithRoPActiveCount - pr.rightOfPassageBetrayals)
				+ 6 * pr.rightOfPassageBetrayals
				+ pr.espionageIncidents
				+ 16 * pr.nuclearAtrocityCount;

			// Reputation: what the other civ did to other civs. The study's
			// "Break peace treaty: +4 (other civs: +1)" and "Break ROP (no
			// units in territory): +4 (other civs: +1)", with units inside
			// "other civs ... will add +2" (one more here). UNVERIFIED: that
			// every war the other civ declared broke a peace treaty.
			foreach (Player third in gameData.players) {
				if (third != ai && third != other
					&& TryGetRelationship(third, other, out PlayerRelationship victim)) {
					bad += victim.warDeclarationCount + victim.warDeclarationWithRoPActiveCount
						+ victim.rightOfPassageBetrayals;
				}
			}
			// "Trade embargo victim: +10": the other civ signed an embargo
			// against the AI.
			if (other.playerRelationships.Values.Any(r => r.multiTurnDeals.Any(
				d => d.dealSubType == DealSubType.TradeEmbargo && d.againstPlayer == ai.id))) {
				bad += 10;
			}
			// "+4 Shunned government (only applies if you are in this
			// government and the AI is not)".
			string shunned = ai.civilization?.shunnedGovernment;
			if (shunned != null && theirGovernment == shunned && ourGovernment != shunned) {
				bad += 4;
			}
			if (cultureLead < 0) {
				bad += 1;
			}
			// "+1 If you call the AI's bluff when he demands tribute (and he
			// doesn't declare war of course)", and +1 for each of its workers
			// the other civ captured (UNVERIFIED: from a summary of the
			// study's table, not its own words).
			bad += pr.tributeBluffsCalled + pr.workersCaptured;

			return AggressionAttitude(gameData, ai) + good + bad;
		}

		private static int TotalCulture(Player player) {
			int total = 0;
			foreach (City city in player.cities) {
				total += city.GetCulture();
			}
			return total;
		}
	}
}

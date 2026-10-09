
using System.Collections.Generic;
using System.Linq;

namespace C7GameData {
	// A class representing one side of a diplomatic agreement between two civs.
	public class TradeOffer {
		// True if two players involved in the agreement were at war prior to
		// the agreement.
		public bool partOfPeaceTreaty = false;

		public int? gold = null;
		public List<Tech> techs = new();

		// Calculate how much this trade offer is worth for a given player. This
		// has to be per-player because tech costs vary based on how many civs
		// a player have already researched the tech.
		public int GoldEquivalentFor(GameData gameData, Player p) {
			int result = 0;
			if (gold.HasValue) {
				result += gold.Value;
			}
			foreach (Tech t in techs) {
				result += gameData.TechCostFor(t, p);
			}

			return result;
		}

		// Why the proposer and opponent can't make this deal, in which the
		// proposer gives proposerGives for proposerWants, or null if they
		// can. Offers may come from another machine, so nothing in them is
		// trusted: each side must have the gold and techs it gives, the
		// other side must be able to use the techs, the two must have met,
		// and while at war they can only make peace (and peace only then).
		public static string ProblemWithDeal(GameData gameData, Player proposer, Player opponent,
			TradeOffer proposerGives, TradeOffer proposerWants) {
			if (proposer == null || opponent == null || proposerGives == null || proposerWants == null) {
				return "the deal is incomplete";
			}
			if (proposer == opponent) {
				return "a player can't trade with themselves";
			}
			if (gameData.GetPlayer(proposer.id) != proposer || gameData.GetPlayer(opponent.id) != opponent) {
				return "a player isn't in the game";
			}
			if (proposer.isBarbarians || opponent.isBarbarians || proposer.defeated || opponent.defeated) {
				return "barbarians and defeated players can't trade";
			}
			if (!PlayerRelationship.TryGetRelationship(proposer, opponent, out _)
				|| !PlayerRelationship.TryGetRelationship(opponent, proposer, out _)) {
				return "the players haven't met";
			}

			bool peace = proposerGives.partOfPeaceTreaty || proposerWants.partOfPeaceTreaty;
			bool atWar = PlayerRelationship.AtWar(proposer, opponent);
			if (peace && !atWar) {
				return "the players aren't at war, so can't make peace";
			}
			if (atWar && !peace) {
				return "the players are at war, and can only make peace";
			}
			if (peace && gameData.AreInLockedWar(proposer, opponent)) {
				return "the players' alliances are at war";
			}

			return proposerGives.ProblemGiving(gameData, proposer, opponent)
				?? proposerWants.ProblemGiving(gameData, opponent, proposer);
		}

		// Why the giver can't give this side of a deal to the receiver, or
		// null if they can.
		private string ProblemGiving(GameData gameData, Player giver, Player receiver) {
			if (gold.HasValue && gold.Value < 0) {
				return $"{giver} can't give negative gold";
			}
			if (gold.HasValue && gold.Value > giver.gold) {
				return $"{giver} doesn't have {gold.Value} gold";
			}
			if (techs == null) {
				return "the techs are missing";
			}
			if (techs.Count > 0) {
				HashSet<ID> tradable = giver.GetTechsTradableTo(receiver, gameData.techs).Select(t => t.id).ToHashSet();
				HashSet<ID> offered = new();
				foreach (Tech t in techs) {
					if (t == null || !offered.Add(t.id)) {
						return "a tech is missing or offered twice";
					}
					// The giver knows it, and the receiver doesn't yet but
					// knows what it takes to use it.
					if (!tradable.Contains(t.id)) {
						return $"{giver} can't give {t.Name} to {receiver}";
					}
				}
			}
			return null;
		}

		public void Clear() {
			partOfPeaceTreaty = false;
			gold = null;
			techs.Clear();
		}

		public override string ToString() {
			List<string> pieces = new();
			if (partOfPeaceTreaty) {
				pieces.Add("peace treaty");
			}
			if (gold != null) {
				pieces.Add($"{gold.Value} gold");
			}
			foreach (Tech t in techs) {
				pieces.Add(t.Name);
			}
			return string.Join(",", pieces);
		}
	}
}

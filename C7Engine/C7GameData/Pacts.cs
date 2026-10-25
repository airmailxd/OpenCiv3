using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;

namespace C7GameData {
	// The rules of Civ3's mutual protection pacts, military alliances and
	// trade embargoes, made at the negotiating table (TradeOffer) and kept as
	// Exchange deals (DealSubType.MutualProtectionPact, MilitaryAlliance and
	// TradeEmbargo, the last two against a third civ, MultiTurnDeal.againstPlayer).
	//
	// Per the Conquests Civilopedia (GCON_Agreements):
	// - "When your civilization builds an embassy in a rival's capital city,
	//   or when a rival builds one in your capital, you can negotiate
	//   Diplomatic Agreements with that civilization", so either civ's
	//   embassy will do.
	// - Military Alliance: "You agree to declare war on a despised third
	//   party." Signing one makes each ally not yet at war with the enemy
	//   declare war on it.
	// - Trade Embargo: "An agreement that neither of you will trade with or
	//   allow trade with a third party."
	// - Mutual Protection Pact: "An agreement to declare war upon any
	//   civilization that attacks your partner" (see HonourPacts).
	// Per GCON_Embassies, with an embassy "you can sign ... Military
	// Alliances against third parties", and with "Nationalism, you can
	// engage in Mutual Protection Pacts and Trade Embargoes against third
	// parties"; TECH_Writing "Can now negotiate military alliance". These
	// are the BIQ's tech flags (SaveTech.Flag.EnablesMilitaryAlliances on
	// Writing, EnablesMutualProtection and EnablesTradeEmbargoes on
	// Nationalism). Rules in which no tech has the flag need none.
	//
	// Like every deal over time they last the rules' deal length (20 turns:
	// "The basic Military Alliance lasts for 20 turns",
	// https://forums.civfanatics.com/threads/how-to-form-a-strategic-alliance.154837/),
	// and breaking a military alliance or pact ruins the breaker's trading
	// reputation (see PlayerRelationship.BreaksReputation): "Accepting a
	// peace treaty before the MA expires means your reputation will be
	// trashed" (same thread; see BreakAlliancesAgainst).
	//
	// A mutual protection pact needs Nationalism on one side only: "If a
	// peace treaty and embassy are in place between two civilizations and
	// either has discovered Nationalism, they can negotiate a mutual
	// protection pact" (Civ3 manual, p.~160,
	// https://cdn.akamai.steamstatic.com/steam/apps/3910/manuals/manual.pdf),
	// and it needs "only one civ to know Nationalism" (cort,
	// https://codehappy.net/apolyton/threads/33796-1.htm).
	//
	// UNVERIFIED (no Civ3 source found), each the simplest rule that fits:
	// - For a military alliance or trade embargo both civs signing must
	//   know the tech, not just the proposer.
	// - A military alliance needs at least one of the two at war with the
	//   enemy already. Civ3 offers one against any known civ, but the AI
	//   answers "We don't think a deal can be worked out" unless war has
	//   been declared (https://forums.civfanatics.com/threads/how-to-make-alliances.249407/),
	//   and both must know the enemy ("they must have discovered the civ
	//   you want to war with", same thread).
	// - A trade embargo needs both to know the third civ, and can't be
	//   signed while either still has a resource or gold per turn deal with
	//   it. While it lasts, neither may make such a deal with it
	//   (TradeOffer.ProblemWithDeal); Civ3's "trade" is the trade in
	//   resources (Civilopedia, GCON_Trade), and gold per turn goes with it.
	// - A pact only calls in partners of the civ attacked, once: the wars
	//   the partners declare don't call in pacts of their own.
	public static class Pacts {
		private static readonly ILogger log = Log.ForContext(typeof(Pacts));

		// Whether the player knows a tech with the flag, or no tech in the
		// rules has it.
		public static bool KnowsTech(GameData gameData, Player player, Save.SaveTech.Flag flag) {
			return player.HasTechEnabling(flag) || !(gameData?.techs?.Exists(t => t.HasFlag(flag)) ?? false);
		}

		public static bool HasEmbassyEitherWay(Player a, Player b) {
			return (a.playerRelationships.TryGetValue(b.id, out PlayerRelationship ab) && ab.hasEmbassy)
				|| (b.playerRelationships.TryGetValue(a.id, out PlayerRelationship ba) && ba.hasEmbassy);
		}

		public static bool HasDeal(Player a, Player b, DealSubType type, ID against = null) {
			return PlayerRelationship.TryGetRelationship(a, b, out PlayerRelationship ab)
				&& ab.multiTurnDeals.Any(d => d.dealSubType == type && (against == null || d.againstPlayer == against));
		}

		// Whether the player has agreed with anyone not to trade with other.
		public static bool Embargoes(Player player, Player other) {
			return player.playerRelationships.Values.Any(r => r.multiTurnDeals.Any(
				d => d.dealSubType == DealSubType.TradeEmbargo && d.againstPlayer == other.id));
		}

		// Why a and b can't make an agreement now, or null if they can.
		// With eitherKnows, one of the two knowing the tech will do.
		private static string ProblemAgreeing(GameData gameData, Player a, Player b, Save.SaveTech.Flag tech, bool eitherKnows = false) {
			if (a == null || b == null || a == b) {
				return "a civ can't make an agreement with itself";
			}
			if (!PlayerRelationship.TryGetRelationship(a, b, out _) || !PlayerRelationship.TryGetRelationship(b, a, out _)) {
				return "the civs haven't met";
			}
			if (PlayerRelationship.AtWar(a, b)) {
				return "the civs are at war";
			}
			if (!HasEmbassyEitherWay(a, b)) {
				return "an agreement needs an embassy";
			}
			if (eitherKnows) {
				if (!KnowsTech(gameData, a, tech) && !KnowsTech(gameData, b, tech)) {
					return $"the agreement needs a tech neither civ knows ({tech})";
				}
			} else if (!KnowsTech(gameData, a, tech) || !KnowsTech(gameData, b, tech)) {
				return $"the agreement needs a tech the civs don't both know ({tech})";
			}
			return null;
		}

		// Why a and b can't make an agreement against the third civ, or null.
		private static string ProblemWithThird(GameData gameData, Player a, Player b, Player third) {
			if (third == null || third == a || third == b || third.IsIndependent || third.defeated
				|| gameData.GetPlayer(third.id) != third) {
				return "the agreement needs a third civ";
			}
			if (!PlayerRelationship.TryGetRelationship(a, third, out _) || !PlayerRelationship.TryGetRelationship(b, third, out _)) {
				return "both civs must know the third civ";
			}
			return null;
		}

		public static string ProblemSigningMutualProtectionPact(GameData gameData, Player a, Player b) {
			string problem = ProblemAgreeing(gameData, a, b, Save.SaveTech.Flag.EnablesMutualProtection, eitherKnows: true);
			if (problem != null) {
				return problem;
			}
			if (HasDeal(a, b, DealSubType.MutualProtectionPact)) {
				return "the civs already have a mutual protection pact";
			}
			return null;
		}

		public static string ProblemSigningMilitaryAlliance(GameData gameData, Player a, Player b, Player enemy) {
			string problem = ProblemAgreeing(gameData, a, b, Save.SaveTech.Flag.EnablesMilitaryAlliances) ?? ProblemWithThird(gameData, a, b, enemy);
			if (problem != null) {
				return problem;
			}
			if (HasDeal(a, b, DealSubType.MilitaryAlliance, enemy.id)) {
				return "the civs are already allied against that civ";
			}
			if (!PlayerRelationship.AtWar(a, enemy) && !PlayerRelationship.AtWar(b, enemy)) {
				return "neither civ is at war with that civ";
			}
			if (gameData.AreInLockedPeace(a, enemy) || gameData.AreInLockedPeace(b, enemy)) {
				return "a civ can't make war on its locked ally";
			}
			return null;
		}

		public static string ProblemSigningTradeEmbargo(GameData gameData, Player a, Player b, Player target) {
			string problem = ProblemAgreeing(gameData, a, b, Save.SaveTech.Flag.EnablesTradeEmbargoes) ?? ProblemWithThird(gameData, a, b, target);
			if (problem != null) {
				return problem;
			}
			if (HasDeal(a, b, DealSubType.TradeEmbargo, target.id)) {
				return "the civs already have an embargo against that civ";
			}
			if (TradesWith(a, target) || TradesWith(b, target)) {
				return "a civ still trades with that civ";
			}
			return null;
		}

		// Whether the two have a resource or gold per turn deal.
		public static bool TradesWith(Player a, Player b) {
			return PlayerRelationship.TryGetRelationship(a, b, out PlayerRelationship ab)
				&& ab.multiTurnDeals.Any(d => d.dealSubType is DealSubType.GoldPerTurn
					or DealSubType.ResourcePerTurn or DealSubType.LuxuryPerTurn);
		}

		// The civs the two could sign a military alliance or embargo against.
		public static List<Player> AllianceTargets(GameData gameData, Player a, Player b) {
			return gameData.players.Where(t => t != a && t != b && !t.IsIndependent && !t.defeated
				&& ProblemSigningMilitaryAlliance(gameData, a, b, t) == null).ToList();
		}

		public static List<Player> EmbargoTargets(GameData gameData, Player a, Player b) {
			return gameData.players.Where(t => t != a && t != b && !t.IsIndependent && !t.defeated
				&& ProblemSigningTradeEmbargo(gameData, a, b, t) == null).ToList();
		}

		private static void Register(GameData gameData, Player a, Player b, DealType type, DealSubType subType, ID against = null) {
			PlayerRelationship.RegisterMultiTurnDeal(a, b, new MultiTurnDeal(type, subType, DealDetails.Exchange, 0, null,
				TradeOffer.DealDuration(gameData), gameData.turn, against));
		}

		public static void SignMutualProtectionPact(GameData gameData, Player a, Player b) {
			Register(gameData, a, b, DealType.DiplomaticAgreement, DealSubType.MutualProtectionPact);
		}

		// Signs the alliance; each ally not yet at war with the enemy
		// declares war on it.
		public static void SignMilitaryAlliance(GameData gameData, Player a, Player b, Player enemy) {
			Register(gameData, a, b, DealType.Alliance, DealSubType.MilitaryAlliance, enemy.id);
			foreach (Player ally in new[] { a, b }) {
				if (!PlayerRelationship.AtWar(ally, enemy)) {
					log.Information("{Ally} joins the war on {Enemy} under a military alliance", ally, enemy);
					ally.DeclareWarOn(enemy, gameData.turn);
					MsgWarDeclaration.Announce(ally, enemy);
				}
			}
		}

		public static void SignTradeEmbargo(GameData gameData, Player a, Player b, Player target) {
			Register(gameData, a, b, DealType.Embargo, DealSubType.TradeEmbargo, target.id);
		}

		// Called when the aggressor declares war on the defender: each of the
		// defender's mutual protection partners at peace with the aggressor
		// declares war on it ("If you have MPPs with two civs that go to war
		// with each other, you will end up declaring war on whichever civ
		// attacks with a unit first",
		// https://forums.civfanatics.com/threads/mutual-protection-pact.12489/).
		// A pact is defensive: "I didn't think that an MPP became activated
		// if the player you have the agreement with attacks someone only if
		// they are attacked by a third party" (same thread), so the
		// aggressor's partners stay out.
		public static void HonourPacts(GameData gameData, Player aggressor, Player defender, int refuseContactUntilTurn, int turn) {
			if (gameData == null || aggressor.IsIndependent || defender.IsIndependent) {
				return;
			}
			List<Player> partners = gameData.players.Where(p => p != aggressor && p != defender && !p.IsIndependent && !p.defeated
				&& HasDeal(defender, p, DealSubType.MutualProtectionPact)
				&& PlayerRelationship.TryGetRelationship(p, aggressor, out _)
				&& !PlayerRelationship.AtWar(p, aggressor)
				&& !gameData.AreInLockedPeace(p, aggressor)).ToList();
			foreach (Player partner in partners) {
				log.Information("{Partner} declares war on {Aggressor} under its mutual protection pact with {Defender}",
					partner, aggressor, defender);
				PlayerRelationship.DeclareWar(partner, aggressor, false, refuseContactUntilTurn, turn, honourPacts: false,
					keepingPact: true);
				if (EngineStorage.gameData == gameData) {
					MsgWarDeclaration.Announce(partner, aggressor);
				}
			}
		}

		// Called when the two make peace: a military alliance either has
		// against the other is broken, ruining its trading reputation with
		// its ally (see above).
		public static void BreakAlliancesAgainst(GameData gameData, Player left, Player right) {
			foreach ((Player peacemaker, Player enemy) in new[] { (left, right), (right, left) }) {
				foreach (Player ally in gameData.players) {
					if (ally == peacemaker || ally == enemy
						|| !PlayerRelationship.TryGetRelationship(peacemaker, ally, out PlayerRelationship ours)) {
						continue;
					}
					List<MultiTurnDeal> broken = ours.multiTurnDeals
						.Where(d => d.dealSubType == DealSubType.MilitaryAlliance && d.againstPlayer == enemy.id).ToList();
					foreach (MultiTurnDeal deal in broken) {
						ours.multiTurnDeals.Remove(deal);
						if (ally.playerRelationships.TryGetValue(peacemaker.id, out PlayerRelationship theirs)) {
							theirs.multiTurnDeals.Remove(deal);
							theirs.brokeTradeDeal = true;
						}
						log.Information("{Peacemaker} broke its military alliance with {Ally} by making peace with {Enemy}",
							peacemaker, ally, enemy);
					}
				}
			}
		}
	}
}

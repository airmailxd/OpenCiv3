using System.Linq;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI {
	// Trial switch "prey-war": Advanced AI only. Once its land runs out, the
	// rule planner names a much weaker neighbour on its landmass as prey
	// (RulePlanner.PlanPeace: the Target stance, an aggression shift and an
	// Offense focus). Without this switch that plan almost never became a
	// war: a declaration in peacetime comes only from Civ3's plotting rolls
	// (StockWarPlotting.RollsToPlot), where the Target stance only picks the
	// victim once all the rolls pass, and the shift moves one roll of a
	// dozen. A civ ranked fifth in power, short of the units per city the
	// rolls ask for, low on gold and under a government with war weariness
	// passes them about one turn in 1600. And lean-military capped its
	// soldiers, so the Offense focus built none.
	//
	// With it on, the prey gets a war campaign at once (WarCampaign: gather
	// at the nearest city, declare when the stack is ready, march, siege,
	// city after city). At peace the stack waits for at least MinAttackers,
	// or goes after GoAfterTurns with two thirds of the attackers it wants,
	// as a campaign already at war does.
	// Its cities build the attackers it is short of, past lean-military's
	// cap. The campaign is dropped if, before the war, the plan calls the
	// prey a friend or names other prey, and given up (with the usual cooldown) once the war goes badly
	// (PeaceAI.Assess Losing or worse); peace is left to PeaceAI. The peace
	// cooldown and locked peace still bar a declaration.
	public static class PreyWar {
		public const string Switch = "prey-war";

		public static bool On(Player player) => player != null && player.IsAdvancedAi && AiExperiments.Has(player, Switch);

		// HEURISTIC: turns of gathering at peace after which the stack goes
		// with two thirds of the attackers it wants (WarCampaign gives up a
		// gathering after 25).
		public const int GoAfterTurns = 15;
		// HEURISTIC: the fewest attackers the stack gathers at peace. The
		// campaign's guess for a city it can't see is 2 or 3 defenders and
		// 4 or 5 attackers; on the seed 7530 replay the first prey city,
		// once seen, wanted 11, and a stack of 6 Archers fell back from it.
		public const int MinAttackers = 8;

		// The rival the plan names as prey: one we are at peace with, may
		// declare on, and have no protection pact with. Null if none.
		public static Player Prey(Player player, GameData gameData) {
			if (!On(player)) {
				return null;
			}
			return gameData.players.FirstOrDefault(p => p != player && !p.IsIndependent && !p.defeated && p.cities.Count > 0
				&& Advanced.PlanEffects.Stance(player, p) == RivalStance.Target
				&& WarCampaign.CanDeclareWarOn(player, p)
				&& !Pacts.HasDeal(player, p, DealSubType.MutualProtectionPact));
		}

		// Whether a campaign gathering on this prey before the war goes on:
		// it does while the plan neither calls them a friend nor names
		// other prey. A re-plan that sees a city site open up again and
		// turns back to expanding (it flips back and forth as sites open
		// and fill) doesn't call the stack off.
		public static bool StillPrey(Player player, GameData gameData, Player enemy) {
			if (Advanced.PlanEffects.Stance(player, enemy) == RivalStance.Friend
					|| Pacts.HasDeal(player, enemy, DealSubType.MutualProtectionPact)) {
				return false;
			}
			Player named = Prey(player, gameData);
			return named == null || named == enemy;
		}

		// Whether a war on the prey is going badly enough to stop pressing it.
		public static bool GoingBadly(Player player, Player enemy) {
			return PeaceAI.Assess(player, enemy) <= PeaceAI.WarOutlook.Losing;
		}
	}
}

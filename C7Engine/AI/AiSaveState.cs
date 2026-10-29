using System.Collections.Generic;
using C7GameData;
using C7GameData.Save;

namespace C7Engine.AI {
	// Saves and loads what an AI player remembers from turn to turn (see
	// SaveAiState). Without it a reloaded game played differently: the AI
	// forgot its war campaign, and its units gathering for it went back to
	// other work, and asked civs again for what they had just turned down.
	//
	// Not saved, since they are worked out again as needed: the caches of
	// paths, routes, sea reach, city sites and defense snapshots, and the
	// per-turn unit builders, road plans and worker searches. Also not
	// saved: the strategic priorities (PlayerAI works them out again on the
	// first turn after a load), the Kings & Nomads hordes (see
	// KingsAndNomadsAI), the deals waiting for renewal (see DealRenewal),
	// and the AiExperiments switches, which only trials set.
	public static class AiSaveState {
		// Null for a player that remembers nothing yet, as humans don't.
		public static SaveAiState Save(Player player) {
			SaveAiState state = new() {
				warCampaign = WarCampaign.Save(player),
				nextCampaignTurn = WarCampaign.CooldownUntil(player),
				dealRequestTurns = ByIdString(DealProposalAI.RequestTurns(player)),
				passageRequestTurns = ByIdString(RightOfPassageAI.RequestTurns(player)),
				lastDisbandedTurn = ObsoleteUnits.LastDisbandedTurn(player),
				marketPricePerShield = MarketAI.SavedPricePerShield(player),
				strategicPlan = player.strategicPlan,
			};
			bool empty = state.warCampaign == null && state.nextCampaignTurn == null && state.dealRequestTurns == null
				&& state.passageRequestTurns == null && state.lastDisbandedTurn == null && state.marketPricePerShield == null
				&& state.strategicPlan == null;
			return empty ? null : state;
		}

		// After the players, units and cities are loaded. A missing state
		// (an older save) leaves the AI as a new game would.
		public static void Restore(Player player, GameData gameData, SaveAiState state) {
			WarCampaign.Restore(player, gameData, state?.warCampaign, state?.nextCampaignTurn);
			DealProposalAI.RestoreRequestTurns(player, ById(state?.dealRequestTurns));
			RightOfPassageAI.RestoreRequestTurns(player, ById(state?.passageRequestTurns));
			ObsoleteUnits.RestoreLastDisbandedTurn(player, state?.lastDisbandedTurn);
			MarketAI.RestorePricePerShield(player, state?.marketPricePerShield);
			player.strategicPlan = state?.strategicPlan;
		}

		private static Dictionary<string, int> ByIdString(Dictionary<ID, int> turns) {
			if (turns == null || turns.Count == 0) {
				return null;
			}
			Dictionary<string, int> result = new();
			foreach ((ID id, int turn) in turns) {
				result[id.ToString()] = turn;
			}
			return result;
		}

		private static Dictionary<ID, int> ById(Dictionary<string, int> turns) {
			Dictionary<ID, int> result = new();
			foreach ((string id, int turn) in turns ?? []) {
				if (ID.FromString(id) is ID parsed) {
					result[parsed] = turn;
				}
			}
			return result;
		}
	}
}

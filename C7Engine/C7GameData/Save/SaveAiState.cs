using System.Collections.Generic;

namespace C7GameData.Save {

	// What an AI player remembers from turn to turn that changes how it
	// plays (see C7Engine.AI.AiSaveState), so that a game reloaded plays on
	// as it would have. Units, cities and players are kept by id. Null in
	// saves made before it was saved, and for human players; loading then
	// starts the AI afresh, with no campaign and nothing asked of anyone.
	public class SaveAiState {
		// The campaign the AI is running (see C7Engine.AI.WarCampaign), or
		// null.
		public SaveWarCampaign warCampaign;
		// The turn before which the AI starts no new campaign in a war it is
		// in, after giving one up; null for none.
		public int? nextCampaignTurn;
		// The turn the AI last asked each civ (by id) for an alliance or
		// embargo (see DealProposalAI), and for a right of passage (see
		// RightOfPassageAI).
		public Dictionary<string, int> dealRequestTurns;
		public Dictionary<string, int> passageRequestTurns;
		// The turn the AI last disbanded an obsolete unit (see
		// ObsoleteUnits); null if it hasn't.
		public int? lastDisbandedTurn;
		// The gold per shield the AI reckons wins a unit on the market (see
		// MarketAI); null if it has no reckoning yet.
		public double? marketPricePerShield;
		// An Advanced AI's strategy (see StrategicPlan); null for none.
		public StrategicPlan strategicPlan;
		// An Advanced AI's reviewed policy (see AdvancedPolicy); null for
		// none.
		public AdvancedPolicy advancedPolicy;
	}

	// See C7Engine.AI.WarCampaign.Campaign.
	public class SaveWarCampaign {
		public ID enemy;
		public ID target;
		public string phase;
		public int phaseStartTurn;
		public TileLocation? stackTile;
		public List<ID> units = new();
		public int failures;
		public double knownDefenderHitPoints = -1;
		public double knownHitsEach;
		public int shortTurns;
		public TileLocation? retreatTo;
		public bool cooldown = true;
	}
}

using System.Collections.Generic;
using C7GameData;

namespace C7Engine;

// Won by being elected Secretary General of the United Nations (see
// UnitedNations for the election itself).
public class DiplomaticVictory : IVictory {
	public string Header() => "Diplomatic";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		UnitedNationsState state = gameData.unitedNations;
		return new VictoryStatus {
			Player = player,
			ElectedSecretaryGeneral = state?.secretaryGeneral != null && state.secretaryGeneral == player.id?.ToString(),
			UnitedNationsOwner = UnitedNations.Owner(gameData),
			NextUnitedNationsVote = state?.votingTurn ?? -1,
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return status.ElectedSecretaryGeneral;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		Player owner = status.UnitedNationsOwner;
		yield return [
			"Win the United Nations vote",
			"",
			"United Nations:",
			owner == null ? "Not built" : owner.civilization?.noun ?? "",
			"Next vote:",
			owner == null || status.NextUnitedNationsVote < 0 ? "" : $"Turn {status.NextUnitedNationsVote}",
		];
	}
}

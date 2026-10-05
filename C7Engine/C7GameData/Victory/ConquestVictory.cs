using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// Won by being the last civilization standing.
public class ConquestVictory : IVictory {
	public string Header() => "Conquest";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		return new VictoryStatus {
			Player = player,
			RivalsRemaining = gameData.players.Count(p =>
				p != player && !p.isBarbarians && !p.defeated && p.isIncludedInGame),
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return status.RivalsRemaining == 0;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return [
			"Eliminate every rival",
			"",
			"Rivals remaining:",
			$"{status.RivalsRemaining}",
			"",
			"",
		];
	}
}

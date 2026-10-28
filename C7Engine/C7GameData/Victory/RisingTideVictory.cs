using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// Rising Tide's victory (see RisingTide): once the tide has left one civ
// standing, or none, the highest score wins (see RisingTide.ScoreBonus).
// It takes the place of the Conquest victory. TurnHandling.CheckVictory
// ends the game, as the winner may be a civ that has already fallen.
public class RisingTideVictory : IVictory {
	public string Header() => "Rising Tide";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		return new VictoryStatus {
			Player = player,
			RivalsRemaining = gameData.players.Count(p =>
				p != player && !p.IsIndependent && !p.defeated && p.isIncludedInGame && !gameData.AreTeammates(player, p)),
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return false;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return [
			"Outlast every rival; the best score wins",
			"",
			"Rivals remaining:",
			$"{status.RivalsRemaining}",
			"Turns survived:",
			$"{status.Player?.turnsSurvived ?? 0}",
		];
	}
}

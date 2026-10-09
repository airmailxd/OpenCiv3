using System.Collections.Generic;
using C7GameData;

namespace C7Engine;

public class TimeLimitVictory : IVictory {
	private readonly int _turnLimit;

	public TimeLimitVictory(int turnLimit) {
		_turnLimit = turnLimit;
	}

	public string Header() => "Time Limits";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		return new VictoryStatus {
			Player = player,
			CurrentTurn = gameData.turn
		};
	}

	// A turn limit of 0 or less means the game has none.
	public bool HasVictory(VictoryStatus status) {
		return _turnLimit > 0 && status.CurrentTurn >= _turnLimit;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return TurnLimitPrint(status, rivalStatuses);
	}

	private string[] TurnLimitPrint(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		return [
			"Turn in game:",
			$"{_turnLimit}",
			"",
			"",
			"Current turn:",
			$"{status.CurrentTurn}"
		];
	}
}

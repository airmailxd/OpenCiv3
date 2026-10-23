using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// Kings & Nomads' empire victory (owner's design, 2026-10-23): won by
// holding at least half of all the cities in the world, the independents'
// included.
public class EmpireVictory : IVictory {
	public string Header() => "Empire";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		return new VictoryStatus {
			Player = player,
			CitiesHeld = player.cities.Count,
			CitiesInWorld = gameData.cities.Count,
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return status.CitiesInWorld > 0 && status.CitiesHeld * 2 >= status.CitiesInWorld;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return [
			"Hold half the world's cities",
			"",
			"Cities held:",
			$"{status.CitiesHeld} of {status.CitiesInWorld}",
			"",
			"",
		];
	}
}

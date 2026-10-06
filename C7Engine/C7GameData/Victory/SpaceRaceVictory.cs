using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// Won by being the first to build every part of the spaceship. In Civ3 the
// ship launches as soon as it is complete, with no travel time (see
// SpaceRace).
public class SpaceRaceVictory : IVictory {
	public string Header() => "Space Race";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		(int built, int needed) = SpaceRace.Progress(gameData, player);
		return new VictoryStatus {
			Player = player,
			SpaceshipPartsBuilt = built,
			SpaceshipPartsNeeded = needed,
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return status.SpaceshipPartsNeeded > 0 && status.SpaceshipPartsBuilt >= status.SpaceshipPartsNeeded;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		VictoryStatus topRival = rivalStatuses.OrderByDescending(r => r.SpaceshipPartsBuilt).FirstOrDefault();
		yield return [
			"Spaceship parts needed:",
			$"{status.SpaceshipPartsNeeded}",
			"Parts built:",
			$"{status.SpaceshipPartsBuilt}",
			topRival?.Player?.civilization?.name ?? "",
			topRival == null ? "" : $"{topRival.SpaceshipPartsBuilt}",
		];
	}
}

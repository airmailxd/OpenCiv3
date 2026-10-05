using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// Won by controlling a large enough share of both the world's land and its
// population.
public class DominationVictory : IVictory {
	private readonly int territoryPercentNeeded;
	private readonly int populationPercentNeeded;

	public DominationVictory(int territoryPercentNeeded, int populationPercentNeeded) {
		this.territoryPercentNeeded = territoryPercentNeeded;
		this.populationPercentNeeded = populationPercentNeeded;
	}

	public string Header() => "Domination";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		int totalTerritory = gameData.map.tiles.Count(t => t.IsCountedForDomination());
		int ourTerritory = player.tileKnowledge.DominationTiles().Count;

		int totalPopulation = gameData.players.Sum(p => p.cities.Sum(c => c.residents.Count));
		int ourPopulation = player.cities.Sum(c => c.residents.Count);

		return new VictoryStatus {
			Player = player,
			TerritoryPercent = totalTerritory == 0 ? 0 : 100f * ourTerritory / totalTerritory,
			PopulationPercent = totalPopulation == 0 ? 0 : 100f * ourPopulation / totalPopulation,
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return status.TerritoryPercent >= territoryPercentNeeded
			&& status.PopulationPercent >= populationPercentNeeded;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		VictoryStatus topTerritory = rivalStatuses.OrderByDescending(r => r.TerritoryPercent).FirstOrDefault();
		VictoryStatus topPopulation = rivalStatuses.OrderByDescending(r => r.PopulationPercent).FirstOrDefault();

		yield return [
			"Territory needed:",
			$"{territoryPercentNeeded}%",
			"Territory:",
			$"{status.TerritoryPercent:0}%",
			topTerritory?.Player?.civilization?.name ?? "",
			topTerritory == null ? "" : $"{topTerritory.TerritoryPercent:0}%",
		];
		yield return [
			"Population needed:",
			$"{populationPercentNeeded}%",
			"Population:",
			$"{status.PopulationPercent:0}%",
			topPopulation?.Player?.civilization?.name ?? "",
			topPopulation == null ? "" : $"{topPopulation.PopulationPercent:0}%",
		];
	}
}

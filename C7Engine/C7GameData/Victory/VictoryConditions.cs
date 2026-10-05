namespace C7GameData;

public class VictoryConditions {
	// TODO: default/preferred victory conditions
	public bool AllowDominationVictory { get; set; }
	public bool AllowSpaceRaceVictory { get; set; }
	public bool AllowDiplomaticVictory { get; set; }
	public bool AllowConquestVictory { get; set; }
	public bool AllowCulturalVictory { get; set; }
	public bool AllowWonderVictory { get; set; }
	public bool CityElimination { get; set; }
	public bool Regicide { get; set; }
	public bool MassRegicide { get; set; }
	public bool VictoryLocations { get; set; }
	public bool CaptureTheFlag { get; set; }
	public bool ReverseCaptureTheFlag { get; set; }

	// The shares of the world's land and population needed for a
	// domination victory.
	public int DominationTerritoryPercent { get; set; } = 66;
	public int DominationPopulationPercent { get; set; } = 66;

	// The victory types a new game allows unless the player says otherwise.
	public static VictoryConditions NewGameDefaults() {
		return new VictoryConditions {
			AllowConquestVictory = true,
			AllowDominationVictory = true,
		};
	}
}

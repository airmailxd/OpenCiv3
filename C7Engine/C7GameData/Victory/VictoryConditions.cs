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

	// Whether the game ends once the time options' turn limit is reached.
	// Off unless chosen: the game goes on until someone wins (the project
	// owner's choice). Saves made before this was saved have no limit.
	public bool UseTurnLimit { get; set; }

	// The shares of the world's land and population needed for a
	// domination victory.
	public int DominationTerritoryPercent { get; set; } = 66;
	public int DominationPopulationPercent { get; set; } = 66;

	// The victory types a new game allows unless the player says otherwise,
	// and a BIQ that uses the default victory conditions gets.
	// UNVERIFIED (no Civ3 source found): which victory types Civ3 turns on
	// by default. conquests.biq doesn't say: it uses the default victory
	// conditions, and its own victory flags are all clear.
	public static VictoryConditions NewGameDefaults() {
		return new VictoryConditions {
			AllowConquestVictory = true,
			AllowDominationVictory = true,
			// Civ3 allows the space race by default.
			AllowSpaceRaceVictory = true,
			// Civ3 allows a diplomatic victory (through the United Nations)
			// by default.
			AllowDiplomaticVictory = true,
			// The project owner's choice: new games allow a cultural
			// victory.
			AllowCulturalVictory = true,
		};
	}
}

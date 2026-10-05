namespace C7GameData;

/// Shared status class for easy abstractions
public class VictoryStatus {
	public Player Player { get; set; }
	public int CurrentTurn { get; set; }
	public float TurnScore { get; set; }
	public float Score { get; set; }
	public int RivalsRemaining { get; set; }
	public float TerritoryPercent { get; set; }
	public float PopulationPercent { get; set; }
}

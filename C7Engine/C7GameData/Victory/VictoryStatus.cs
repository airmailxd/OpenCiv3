using System;

namespace C7GameData;

/// Shared status class for easy abstractions
public class VictoryStatus {
	public Player Player { get; set; }
	public int CurrentTurn { get; set; }

	// Either set directly, or computed from the source when first read.
	private float turnScore;
	private Func<float> turnScoreSource;
	public float TurnScore {
		get {
			Func<float> source = turnScoreSource;
			if (source != null) {
				turnScore = source();
				turnScoreSource = null;
			}
			return turnScore;
		}
		set {
			turnScore = value;
			turnScoreSource = null;
		}
	}

	// Makes TurnScore computed by the source when first read.
	public void SetTurnScoreSource(Func<float> source) {
		turnScoreSource = source;
	}
	public float Score { get; set; }
	public int RivalsRemaining { get; set; }
	public float TerritoryPercent { get; set; }
	public float PopulationPercent { get; set; }
}

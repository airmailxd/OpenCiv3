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
	public int SpaceshipPartsBuilt { get; set; }
	public int SpaceshipPartsNeeded { get; set; }

	// Diplomatic victory: whether the player has been elected Secretary
	// General of the United Nations, who owns the UN, and the turn of the
	// next vote (-1 if none is scheduled).
	public bool ElectedSecretaryGeneral { get; set; }
	public Player UnitedNationsOwner { get; set; }
	public int NextUnitedNationsVote { get; set; } = -1;

	// Cultural victory: the player's culture, the most any one of their
	// cities has, and the most culture any rival has.
	public int Culture { get; set; }
	public int TopCityCulture { get; set; }
	public int TopRivalCulture { get; set; }
}

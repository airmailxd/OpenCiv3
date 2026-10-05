namespace C7GameData;

public class HistTurnRecord {
	public int Date { get; set; }
	public int Power { get; set; }
	public int Score { get; set; }
	public int Culture { get; set; }
	public int VP { get; set; }

	// The sum of the turn scores of every turn up to and including this
	// one, which Score is the rounded average of. Keeping the exact sum
	// stops rounding errors from building up turn after turn. Null in
	// records from saves made before it was kept (and from Civ3 saves),
	// in which case it is taken to be Score times the number of turns.
	public double? TurnScoreSum { get; set; }
}

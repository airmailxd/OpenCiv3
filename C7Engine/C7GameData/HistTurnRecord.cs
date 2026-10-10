namespace C7GameData;

// A record is never changed once made, so a LAN host's snapshots can share
// them with the game (see C7Engine.Network.SnapshotDetacher).
public class HistTurnRecord {
	public int Date { get; init; }
	public int Power { get; init; }
	public int Score { get; init; }
	public int Culture { get; init; }
	public int VP { get; init; }

	// The sum of the turn scores of every turn up to and including this
	// one, which Score is the rounded average of. Keeping the exact sum
	// stops rounding errors from building up turn after turn. Null in
	// records from saves made before it was kept (and from Civ3 saves),
	// in which case it is taken to be Score times the number of turns.
	public double? TurnScoreSum { get; init; }
}

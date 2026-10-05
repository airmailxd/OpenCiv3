namespace C7GameData.AIData {
	public class CombatAIData : UnitAIData {
		public Tile destination;
		public TilePath path;

		// When CombatAI last looked for a better target, so that it doesn't
		// redo the search on every step while nothing around it changed.
		public int lastReevaluationTurn = -1;
		public Tile lastReevaluationLocation;
		public int lastReevaluationSignature;
	}
}

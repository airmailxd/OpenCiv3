namespace C7GameData.AIData {
	public class EscortAIData : UnitAIData {
		public MapUnit unitToEscort;

		// Our path to unitToEscort, reused while it still leads to its tile.
		public TilePath pathToEscortedUnit;

		public override string ToString() {
			return "escorting " + unitToEscort;
		}
	}
}

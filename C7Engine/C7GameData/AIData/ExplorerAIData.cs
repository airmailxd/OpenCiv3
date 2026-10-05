namespace C7GameData.AIData {
	public class ExplorerAIData : UnitAIData {
		public Tile destination;
		public TilePath pathToDestination;

		// The unit this plan was made for.
		public MapUnit explorer;

		public override string ToString() {
			return "exploring toward " + destination;
		}
	}
}

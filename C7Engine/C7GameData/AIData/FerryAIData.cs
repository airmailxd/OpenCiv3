namespace C7GameData.AIData {
	// A transport taking a settler and its escort across the water. Where to
	// board and land is in the settler's SettlerAIData.
	public class FerryAIData : UnitAIData {
		public MapUnit settler;

		// The way to the port, and then to the water by the landing.
		public TilePath path;

		public override string ToString() {
			return "ferrying " + settler;
		}
	}
}

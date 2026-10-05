namespace C7GameData.AIData {
	public class WorkerAIData : UnitAIData {
		public Terraform workerMove;
		public Tile destination;
		public TilePath pathToDestination;

		// The unit this plan was made for.
		public MapUnit worker;

		public override string ToString() {
			return workerMove.UIAction + " at " + destination;
		}
	}
}

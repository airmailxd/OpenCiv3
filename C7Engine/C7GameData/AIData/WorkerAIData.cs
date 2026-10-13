namespace C7GameData.AIData {
	public class WorkerAIData : UnitAIData {
		// The job to do at the destination, or null for a colony.
		public Terraform workerMove;
		public Tile destination;
		public TilePath pathToDestination;

		// Whether the worker is going to found a colony at the destination
		// (see WorkerHoldingsAI).
		public bool buildColony;

		// The unit this plan was made for.
		public MapUnit worker;

		public override string ToString() {
			return (buildColony ? "colony" : workerMove.UIAction) + " at " + destination;
		}
	}
}

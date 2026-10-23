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

		// Kings & Nomads: whether the plan takes the worker outside our
		// land, so that it goes with an escort, and the escort (see
		// CityStateRoadAI).
		public bool needsEscort;
		public MapUnit escort;

		// Kings & Nomads: whether the job is a road towards a city-state.
		public bool cityStateRoad;

		public override string ToString() {
			return (buildColony ? "colony" : workerMove.UIAction) + " at " + destination;
		}
	}
}

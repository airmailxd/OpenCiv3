using System.Collections.Generic;

namespace C7GameData {
	// Where an Advanced AI is in the owner's game script (switch
	// "phase-plan", see C7Engine.AI.Advanced.PhasePlan): expand to a few
	// cities toward a rival, muster an army of attackers, make the first
	// war, then consolidate before mustering again.
	public enum GamePhase { Expand, Muster, War, Consolidate }

	// A player's place in the script, saved with the AI's state. Null (an
	// older save, or the switch off) means no phase.
	public class GamePhaseState {
		public GamePhase phase;
		public int startTurn;
		// The turn the first war of the script began, or -1.
		public int firstWarTurn = -1;
		// The cities (by id) that build Barracks and the army while
		// mustering.
		public List<string> musterCities = new();
	}
}

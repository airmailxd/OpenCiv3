using C7GameData;
using C7GameData.AIData;
using System;
using System.Threading.Tasks;

namespace C7GameData {
	//Not-fully-fleshed out player unit AI.
	//Right now it's kind of random to just add some appearance
	//of stuff being done.  I.e. it kinda sucks.  But that's okay.
	//It has to start somewhere, right?
	public interface UnitAI {
		enum Result {
			Done,
			InProgress,
			Error,
		};

		public readonly record struct MoveResult(
			UnitAI.Result Result,
			Task<bool>? PendingMove = null
		) {
			public bool IsMoveRequested => PendingMove is not null;

			public static MoveResult MoveRequested(Task<bool> moveTask) =>
				new(UnitAI.Result.InProgress, moveTask);

			public static implicit operator MoveResult(UnitAI.Result result) =>
				new(result);
		}


		// How many steps in a row may leave the unit where it was with the
		// same movement points. Some steps legitimately do (e.g. repathing, or
		// an escort letting the unit it escorts move first), but a plan that
		// keeps doing so would never end the unit's turn.
		private const int MAX_STEPS_WITHOUT_PROGRESS = 4;

		public async Task<Result> PlayTurn(Player player, MapUnit unit) {
			int stepsWithoutProgress = 0;
			while (unit.movementPoints.canMove && !unit.isFortified) {
				Tile locationBefore = unit.location;
				float movementPointsBefore = unit.movementPoints.remaining;

				MoveResult result = PlayTurnImpl(player, unit);
				if (result == Result.Error || result == Result.Done) {
					return result.Result;
				}
				if (result.IsMoveRequested) {
					bool stillAlive = await result.PendingMove;
					if (!stillAlive) return Result.Error;
				}

				if (unit.location == locationBefore && unit.movementPoints.remaining == movementPointsBefore) {
					if (++stepsWithoutProgress >= MAX_STEPS_WITHOUT_PROGRESS) {
						// Try again next turn.
						break;
					}
				} else {
					stepsWithoutProgress = 0;
				}
			}
			return Result.InProgress;
		}

		// To be implemented by each AI subclass.
		protected abstract MoveResult PlayTurnImpl(Player player, MapUnit unit);

		// Provide a string representation of the current AI plan.
		string SummarizePlan();

		// Do any bookkeeping required when the unit is destroyed. For example,
		// if an escort is destroyed, update the unit being escorted to reflect
		// that it no longer has an escort.
		void UpdateOnDeath();
	}
}

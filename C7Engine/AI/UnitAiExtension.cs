using C7GameData;
using Serilog;
using C7Engine.Pathing;
using System;
using System.Threading.Tasks;
using static C7GameData.UnitAI;

namespace C7Engine {
	// This goofy class only exists because of the arbitrary separation between
	// C7GameData and C7Engine.
	public static class UnitAiExtension {
		private static ILogger log = Log.ForContext<UnitAI>();

		// After a successful repath we return InProgress without moving, and
		// are called again right away to take the first step. Remember the
		// step we just validated so that it isn't checked a second time.
		[ThreadStatic] private static TilePath validatedPath;
		[ThreadStatic] private static Tile validatedStep;
		[ThreadStatic] private static MapUnit validatedUnit;
		[ThreadStatic] private static Tile validatedFrom;
		[ThreadStatic] private static int validatedTurn;

		// Attempts to move the supplied unit along the given path.
		//
		// `path` is a ref so that the path can be recalculated if necessary.
		public static MoveResult TryToMoveAlongPath(this UnitAI unitAi, MapUnit unit, ref TilePath path) {
			if (!unit.movementPoints.canMove) {
				return UnitAI.Result.InProgress;
			}

			bool alreadyValidated = path != null && path == validatedPath && unit == validatedUnit
				&& unit.location == validatedFrom && path.PeekNext() == validatedStep
				&& validatedTurn == (EngineStorage.gameData?.turn ?? 0);
			validatedPath = null;
			validatedStep = null;
			validatedUnit = null;
			validatedFrom = null;

			Tile nextTile = path.Next();
			if (nextTile == Tile.NONE || (!alreadyValidated && !unit.CanEnterForcefully(nextTile))) {
				log.Information($"Attempting to repath {unit} from {unit.location} to {path.destination}");
				// Attempt to repath. If we succeed, return inprogress so we get
				// called again.
				path = PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, path.destination, unit);
				if ((path?.PathLength() ?? -1) == -1 || path.PeekNext() == Tile.NONE || !unit.CanEnterForcefully(path.PeekNext())) {
					return UnitAI.Result.Error;
				}

				validatedPath = path;
				validatedStep = path.PeekNext();
				validatedUnit = unit;
				validatedFrom = unit.location;
				validatedTurn = EngineStorage.gameData?.turn ?? 0;
				return UnitAI.Result.InProgress;
			}

			// Units without blitz can only attack once per turn. If the next
			// step is an attack we can't make, wait until next turn instead of
			// retrying the refused move. Only look for a defender when there
			// is someone on the tile we aren't at peace with.
			if (HasUnitsNotAtPeaceWith(nextTile, unit.owner)) {
				MapUnit defender = nextTile.FindTopDefender(unit);
				if (defender != MapUnit.NONE && !unit.owner.IsAtPeaceWith(defender.owner) && !unit.CanAttackAgainThisTurn()) {
					unit.movementPoints.onConsumeAll();
					return UnitAI.Result.InProgress;
				}
			}

			Task<bool> moveTask = unit.Move(unit.location.DirectionTo(nextTile));

			return MoveResult.MoveRequested(moveTask);
		}

		private static bool HasUnitsNotAtPeaceWith(Tile tile, Player owner) {
			foreach (MapUnit u in tile.unitsOnTile) {
				if (!owner.IsAtPeaceWith(u.owner)) {
					return true;
				}
			}
			return false;
		}
	}
}

using C7GameData;
using Serilog;
using C7Engine.Pathing;
using System;
using System.Runtime.CompilerServices;
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
		//
		// Kept per unit in a weak table, so that nothing here keeps a finished
		// game's units, tiles or paths alive.
		private sealed class ValidatedStep {
			public TilePath path;
			public Tile step;
			public Tile from;
			public int turn;
		}

		private static readonly ConditionalWeakTable<MapUnit, ValidatedStep> validatedSteps = new();

		// Attempts to move the supplied unit along the given path.
		//
		// `path` is a ref so that the path can be recalculated if necessary.
		// A step is only taken off the path when the move onto it is issued.
		public static MoveResult TryToMoveAlongPath(this UnitAI unitAi, MapUnit unit, ref TilePath path) {
			if (!unit.movementPoints.canMove) {
				return UnitAI.Result.InProgress;
			}

			int turn = EngineStorage.gameData?.turn ?? 0;
			bool alreadyValidated = false;
			if (validatedSteps.TryGetValue(unit, out ValidatedStep validated)) {
				alreadyValidated = path != null && path == validated.path && unit.location == validated.from
					&& path.PeekNext() == validated.step && validated.turn == turn;
				validatedSteps.Remove(unit);
			}

			// The next step must be next to us. It may not be if we were
			// stopped on the way, e.g. when combat didn't let us move in, or
			// if the path was made from somewhere else.
			//
			// The step must also be one Move accepts. A tile that someone we're
			// at peace with moved onto would take a war declaration to enter,
			// which Move refuses, so path around it instead.
			//
			// Nor should it be into water the path now avoids, as after the
			// Great Lighthouse is lost, or the ship could sink there.
			Tile nextTile = path?.PeekNext() ?? Tile.NONE;
			if (nextTile == Tile.NONE || !IsNeighbor(unit.location, nextTile)
				|| (!alreadyValidated && (!unit.CanEnter(nextTile) || unit.PathAvoids(nextTile, path.destination)))) {
				Tile destination = path?.destination ?? Tile.NONE;
				log.Information($"Attempting to repath {unit} from {unit.location} to {destination}");
				// Attempt to repath. If we succeed, return inprogress so we get
				// called again.
				path = destination == Tile.NONE ? null : PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, destination, unit);
				Tile first = path?.PeekNext() ?? Tile.NONE;
				if (first == Tile.NONE || !IsNeighbor(unit.location, first) || !unit.CanEnter(first)
					|| unit.PathAvoids(first, path.destination)) {
					return UnitAI.Result.Error;
				}

				validatedSteps.AddOrUpdate(unit, new ValidatedStep() {
					path = path,
					step = first,
					from = unit.location,
					turn = turn,
				});
				return UnitAI.Result.InProgress;
			}

			// Moving onto a tile with an enemy on it is an attack. Units that
			// can't attack at all would never get there, so give up on the
			// plan. Units without blitz can only attack once per turn, so if we
			// already did, wait until next turn instead of retrying the refused
			// move (and keep the step for then). Only look for a defender when
			// there is someone on the tile we aren't at peace with.
			if (HasUnitsNotAtPeaceWith(nextTile, unit.owner)) {
				MapUnit defender = nextTile.FindTopDefender(unit);
				if (defender != MapUnit.NONE && !unit.owner.IsAtPeaceWith(defender.owner)) {
					if (!unit.CanAttack()) {
						return UnitAI.Result.Error;
					}
					if (!unit.CanAttackAgainThisTurn()) {
						unit.movementPoints.onConsumeAll();
						return UnitAI.Result.InProgress;
					}
				}
			}

			path.Next();
			Task<bool> moveTask = unit.Move(unit.location.DirectionTo(nextTile));

			return MoveResult.MoveRequested(moveTask);
		}

		private static bool IsNeighbor(Tile tile, Tile other) {
			foreach (Tile n in tile.neighbors.Values) {
				if (n == other) {
					return true;
				}
			}
			return false;
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

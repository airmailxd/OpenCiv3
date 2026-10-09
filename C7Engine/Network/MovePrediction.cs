using System.Collections.Generic;
using C7GameData;

namespace C7Engine.Network;

// A LAN guest's own simple orders, carried out on its copy of the game as
// they're sent to the host, so that they show at once rather than a round
// trip and a snapshot later. Only what the guest can be sure of is
// predicted: its units' plain steps (see MapUnit.CanPredictStepTo), and
// fortifying, sentrying, waking and skipping a turn. Anything that involves
// what the guest can't see or chance, like a fight, or that the host would
// tell the player about, waits for the host as before.
//
// The host's next snapshot replaces the guest's game as usual, so a wrong
// guess lasts only until then. The predicted orders send no messages to the
// UI, besides the unit's animation: the host's own messages about them
// still arrive. A step's animation goes on through the snapshot that
// confirms it, since animations follow units by ID.
public static class MovePrediction {
	// Guests predict their orders unless C7.ini turns it off:
	//   [online]
	//   predictMoves=false
	public const string Setting = "predictMoves";

	public static bool Enabled =>
		!string.Equals(C7Settings.GetSettingsValueOrDefault(OnlineRelay.SettingsSection, Setting, "true")?.Trim(), "false",
			System.StringComparison.OrdinalIgnoreCase);

	// Carries out the order on this machine's game if it can be predicted,
	// and returns whether that changed the game. The order still has to be
	// sent to the host.
	public static bool Predict(MessageToEngine msg) {
		GameData gameData = EngineStorage.gameData;
		ID playerID = msg.playerID ?? EngineStorage.uiControllerID;
		Player player = gameData?.GetPlayer(playerID);
		if (player == null || !TurnHandling.IsPlayersTurn(gameData, playerID)) {
			return false;
		}
		MapUnit unit = msg switch {
			MsgMoveUnit m => gameData.GetUnit(m.unitID),
			MsgSetUnitPath m => gameData.GetUnit(m.unitID),
			MsgSetFortification m => gameData.GetUnit(m.unitID),
			MsgSentry m => gameData.GetUnit(m.unitID),
			MsgUnitCommand { command: MsgUnitCommand.Command.SkipTurn } m => gameData.GetUnit(m.unitID),
			MsgSelectUnit m => gameData.GetUnit(m.unitID),
			_ => null,
		};
		if (unit == null || unit.owner != player || unit.location == Tile.NONE) {
			return false;
		}

		bool predicted;
		EngineStorage.predicting = true;
		try {
			predicted = msg switch {
				MsgMoveUnit m => Step(unit, StepFor(gameData, unit, m.dir)),
				MsgSetUnitPath m => FollowPath(unit, m.path),
				MsgSetFortification m => SetFortification(unit, m.fortifyElseWake),
				MsgSentry m => Sentry(unit, m.enemyOnly),
				MsgUnitCommand => SkipTurn(unit),
				MsgSelectUnit => Select(unit),
				_ => false,
			};
		} finally {
			EngineStorage.predicting = false;
		}
		return predicted;
	}

	private static Tile StepFor(GameData gameData, MapUnit unit, TileDirection dir) {
		(int dx, int dy) = dir.ToCoordDiff();
		return gameData.map.tileAt(unit.location.XCoordinate + dx, unit.location.YCoordinate + dy);
	}

	private static bool Step(MapUnit unit, Tile tile) {
		if (!unit.CanPredictStepTo(tile)) {
			return false;
		}
		unit.PredictStepTo(tile);
		unit.owner.tileKnowledge.RecomputeActiveTiles();
		Animate(unit, MapUnit.AnimatedAction.RUN);
		return true;
	}

	// Follows the path as far as its steps are plain, which for most paths
	// is as far as the host goes this turn. The steps are taken at once,
	// leaving the unit where the host's snapshot will have it, and only the
	// last is animated.
	private static bool FollowPath(MapUnit unit, TilePath path) {
		if (path == null || !unit.IsFollowablePath(path)) {
			return false;
		}
		// The order's path is its own, as sent.
		Queue<Tile> steps = new(path.path);
		int taken = 0;
		while (unit.movementPoints.canMove && steps.Count > 0 && unit.CanPredictStepTo(steps.Peek())) {
			unit.PredictStepTo(steps.Dequeue());
			++taken;
		}
		if (taken == 0) {
			return false;
		}
		unit.path = new TilePath(path.destination, steps);
		unit.owner.tileKnowledge.RecomputeActiveTiles();
		Animate(unit, MapUnit.AnimatedAction.RUN);
		return true;
	}

	// These return whether they changed anything.
	private static bool SetFortification(MapUnit unit, bool fortify) {
		if (!fortify) {
			return Wake(unit);
		}
		bool changed = !unit.isFortified || unit.isSentried || unit.facingDirection != TileDirection.SOUTHEAST;
		// As MapUnit.Fortify, without the message to the UI that follows its
		// animation.
		unit.ResetFacingDirection();
		unit.isFortified = true;
		Animate(unit, MapUnit.AnimatedAction.FORTIFY);
		return changed;
	}

	private static bool Wake(MapUnit unit) {
		bool changed = unit.isFortified || unit.isSentried;
		unit.Wake();
		return changed;
	}

	private static bool Sentry(MapUnit unit, bool enemyOnly) {
		if (!unit.unitType.actions.Contains(UnitAction.Sentry)) {
			return false;
		}
		bool changed = !unit.isSentried || unit.sentryEnemyOnly != enemyOnly || unit.isFortified
			|| unit.facingDirection != TileDirection.SOUTHEAST;
		unit.Sentry(enemyOnly);
		return changed;
	}

	private static bool SkipTurn(MapUnit unit) {
		bool changed = unit.movementPoints.canMove;
		unit.SkipTurn();
		return changed;
	}

	// As MsgSelectUnit.
	private static bool Select(MapUnit unit) {
		bool changed = false;
		if ((unit.path?.PathLength() ?? -1) > 0) {
			unit.path = TilePath.NONE;
			changed = true;
		}
		if (unit.WorkerJob != null) {
			return changed;
		}
		if (unit.isAutomated) {
			unit.isAutomated = false;
			unit.currentAI = null;
			changed = true;
		}
		if (unit.movementPoints.canMove) {
			changed |= Wake(unit);
		}
		return changed;
	}

	// Starts the unit's animation, as the engine would, without waiting for
	// it. A step is shown from the tile the unit was on before it.
	private static void Animate(MapUnit unit, MapUnit.AnimatedAction action) {
		if (!EngineStorage.animationsEnabled || EngineStorage.gameData.observerMode) {
			return;
		}
		new MsgStartUnitAnimation(unit, action, AnimationEnding.Stop).send();
	}
}

using System;
using C7Engine.Pathing;
using C7GameData;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7GameData.AIData;
using C7Engine.AI;
using Serilog;

namespace C7Engine {
	public class EscortAI : UnitAI {
		private static ILogger log = Log.ForContext<EscortAI>();
		public EscortAIData data;

		public EscortAI(EscortAIData d) {
			data = d;
		}

		public void UpdateOnDeath() {
			if (data.unitToEscort == null) {
				return;
			}

			// When we're destroyed, clear out our reference to the unit we're
			// escorting, and clear our their reference to us.
			if (data.unitToEscort.currentAI is SettlerAI settlerAi) {
				settlerAi.data.escort = null;
			}
			data.unitToEscort = null;
		}

		public static EscortAIData? MaybeMakeAiData(MapUnit unit, Player player) {
			// Ensure we don't have a boat trying to escort a settler.
			if (!unit.CanDefendOnLand()) {
				return null;
			}

			foreach (MapUnit u in unit.location.unitsOnTile) {
				if (u.currentAI is SettlerAI settlerAi && settlerAi.data.escort == null) {
					settlerAi.data.escort = unit;
					EscortAIData result = new();
					result.unitToEscort = u;
					log.Information($"Having {unit.id} {unit} escort {u.id}, {u}");
					return result;
				}
			}

			// TODO: Check if there are nearby units to escort.
			return null;
		}

		public string SummarizePlan() {
			return "EscortAI: " + data.ToString();
		}

		// The turn in which we last had the escorted unit take its turn, and
		// the result it returned.
		private int escortedUnitPlayedTurn = int.MinValue;
		private UnitAI.Result escortedUnitResult;

		// Ensure the unit we're escorting moves first, in case we were first
		// in the unit ordering. Its turn is asynchronous (it may wait on move
		// animations), so it's handed to UnitAI.PlayTurn as a pending move to
		// await rather than being waited on here.
		private async Task<bool> PlayEscortedUnitTurn(Player player, MapUnit escorted) {
			escortedUnitResult = await escorted.currentAI.PlayTurn(player, escorted);
			// The escort itself is still alive; settler moves don't affect it.
			return true;
		}

		UnitAI.MoveResult UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			int turn = EngineStorage.gameData?.turn ?? 0;

			// If the unit we escort finished its plan this turn (e.g. a settler
			// founded its city and left play, which also clears our reference
			// to it), our job is done: report that so we get a new plan.
			if (data != null && escortedUnitPlayedTurn == turn && escortedUnitResult == UnitAI.Result.Done) {
				return UnitAI.Result.Done;
			}

			if (data == null || data.unitToEscort == null || data.unitToEscort.currentAI == null) {
				return UnitAI.Result.Error;
			}

			if (escortedUnitPlayedTurn != turn) {
				escortedUnitPlayedTurn = turn;
				return UnitAI.MoveResult.MoveRequested(PlayEscortedUnitTurn(player, data.unitToEscort));
			}

			UnitAI.Result result = escortedUnitResult;
			if (result == UnitAI.Result.Error) {
				// Their plan failed, so give them a new one, as PlayerAI would
				// if they were moving on their own. Keep escorting them: if
				// we gave up too, PlayerAI would re-plan for us right away,
				// and with them between plans we'd go off exploring instead.
				MapUnit escorted = data.unitToEscort;
				escorted.currentAI = PlayerAI.GetAIForUnit(escorted, player);
				if (escorted.currentAI is not SettlerAI settlerAi || settlerAi.data.escort != null) {
					return result;
				}
				settlerAi.data.escort = unit;
				// We're called again after each of our moves this turn; don't
				// re-plan for them each time.
				escortedUnitResult = UnitAI.Result.InProgress;
				// They head off next turn, when we have them move first again.
				escorted.movementPoints.onConsumeAll();
				log.Information($"Settler {escorted.id} has a new plan; {unit.id} keeps escorting it");
			}

			// If we're on the correct location, give up the rest of our MPs.
			if (unit.location == data.unitToEscort.location) {
				unit.movementPoints.onConsumeAll();
				return UnitAI.Result.InProgress;
			}

			// Move to the unit we're escorting, reusing our path while it still
			// leads there.
			Tile target = data.unitToEscort.location;
			if (data.pathToEscortedUnit == null || data.pathToEscortedUnit.destination != target) {
				data.pathToEscortedUnit = PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, target, unit);
			}
			UnitAI.MoveResult moveResult = this.TryToMoveAlongPath(unit, ref data.pathToEscortedUnit);
			if (moveResult.Result != UnitAI.Result.InProgress) {
				return moveResult;
			}
			if (moveResult.IsMoveRequested) {
				// We'll be called again once the move is done.
				return moveResult;
			}

			// If we're on the correct location, give up the rest of our MPs.
			if (unit.location == data.unitToEscort.location) {
				unit.movementPoints.onConsumeAll();
			}
			return UnitAI.Result.InProgress;
		}
	}
}

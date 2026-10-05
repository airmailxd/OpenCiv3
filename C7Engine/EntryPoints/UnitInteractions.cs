using System.Linq;
using Serilog;

namespace C7Engine {
	using C7GameData;
	using System.Collections.Generic;

	public class UnitInteractions {

		// The units waiting to be selected again, by id rather than by
		// reference: a LAN client replaces every unit object with each of the
		// host's snapshots, so the ids are what stay the same. Each id is
		// looked up in the current game when it comes off the queue.
		private static Queue<ID> waitQueue = new Queue<ID>();

		// How many times each unit appears in waitQueue, so that checking
		// whether a unit is waiting doesn't have to search the queue.
		private static readonly Dictionary<ID, int> waitingUnits = new();

		// The turn and the player the waiting units were waited by. Waiting
		// only lasts for the player's turn, so when either changes (in a
		// hotseat game, when the next human takes over) the queue is dropped.
		private static int waitTurn = -1;
		private static ID waitPlayerID;

		// Busy units already told to carry on. On a LAN client the units don't
		// change until the host's next snapshot, so don't ask again until then.
		private static readonly HashSet<ID> busyActionRequested = new();
		private static GameData busyActionGame;
		private static int busyActionTurn = -1;
		private static ILogger log = Log.ForContext<UnitInteractions>();

		public static MapUnit getNextSelectedUnit() {
			GameData gameData = EngineStorage.gameData;
			// In observer mode the UI controller is played by the AI, so there
			// are no units for the UI to select.
			Player player = gameData.GetUIControllerPlayer();
			IEnumerable<MapUnit> selectable = player != null && player.isHuman ? player.units : [];
			if (busyActionGame != gameData || busyActionTurn != gameData.turn) {
				busyActionRequested.Clear();
				busyActionGame = gameData;
				busyActionTurn = gameData.turn;
			}
			SyncWaitQueue(gameData);
			foreach (MapUnit unit in selectable.Where(u => u.movementPoints.canMove)) {
				// Units in a rigid army go where the army goes.
				if (unit.isFortified || unit.IsLockedInArmy()) {
					continue;
				}

				if (unit.IsBusy()) {
					if (busyActionRequested.Add(unit.id)) {
						new MsgPerformUnitAction(unit).send();
					}
					continue;
				}

				if (unit.id is null || !waitingUnits.ContainsKey(unit.id)) {
					return unit;
				}
			}
			while (waitQueue.Count > 0) {
				ID id = waitQueue.Dequeue();
				if (waitingUnits[id] <= 1) {
					waitingUnits.Remove(id);
				} else {
					--waitingUnits[id];
				}
				// The unit may have died, or changed hands, since it waited.
				MapUnit next = gameData.GetUnit(id);
				if (next == null || player == null || !player.isHuman || next.owner != player) {
					continue;
				}
				return next;
			}
			return MapUnit.NONE;
		}

		public static void ClearWaitQueue() {
			waitQueue.Clear();
			waitingUnits.Clear();
		}

		// Drops the waiting units if they were waited in another turn, or by
		// another player than the one the UI now shows.
		private static void SyncWaitQueue(GameData gameData) {
			ID playerID = EngineStorage.uiControllerID;
			if (waitTurn != gameData.turn || waitPlayerID != playerID) {
				ClearWaitQueue();
				waitTurn = gameData.turn;
				waitPlayerID = playerID;
			}
		}

		// Forgets everything about the previous game. Called when a game is
		// created or loaded.
		internal static void ResetForNewGame() {
			ClearWaitQueue();
			waitTurn = -1;
			waitPlayerID = null;
			OnGameReplaced();
		}

		// Called when a LAN client replaces its game with the host's snapshot.
		// The waiting units are kept, since they are found by id in whichever
		// game is current (and dropped once the turn or player changes), but
		// nothing here may keep the replaced game alive. Busy units are asked
		// to carry on again, as after any snapshot.
		internal static void OnGameReplaced() {
			busyActionRequested.Clear();
			busyActionGame = null;
			busyActionTurn = -1;
		}

		public static void waitUnit(ID id) {
			GameData gameData = EngineStorage.gameData;
			MapUnit unit = gameData.GetUnit(id);
			if (unit == null || unit.id is null) {
				log.Warning("Failed to find a matching unit with id {Id}", id);
				return;
			}
			SyncWaitQueue(gameData);
			log.Verbose("Found matching unit with id {Id} of type {Type}; adding it to the wait queue", id, unit.GetType().Name);
			waitQueue.Enqueue(unit.id);
			waitingUnits[unit.id] = waitingUnits.GetValueOrDefault(unit.id) + 1;
		}
	}
}

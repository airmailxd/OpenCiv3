using System.Linq;
using Serilog;

namespace C7Engine {
	using C7GameData;
	using System.Collections.Generic;

	public class UnitInteractions {

		private static Queue<MapUnit> waitQueue = new Queue<MapUnit>();

		// How many times each unit appears in waitQueue, so that checking
		// whether a unit is waiting doesn't have to search the queue.
		private static readonly Dictionary<MapUnit, int> waitingUnits = new();

		// Busy units already told to carry on. On a LAN client the units don't
		// change until the host's next snapshot, so don't ask again until then.
		private static readonly HashSet<ID> busyActionRequested = new();
		private static GameData busyActionGame;
		private static int busyActionTurn = -1;
		private static ILogger log = Log.ForContext<UnitInteractions>();

		public static MapUnit getNextSelectedUnit() {
			// In observer mode the UI controller is played by the AI, so there
			// are no units for the UI to select.
			Player player = EngineStorage.gameData.GetUIControllerPlayer();
			IEnumerable<MapUnit> selectable = player != null && player.isHuman ? player.units : [];
			if (busyActionGame != EngineStorage.gameData || busyActionTurn != EngineStorage.gameData.turn) {
				busyActionRequested.Clear();
				busyActionGame = EngineStorage.gameData;
				busyActionTurn = EngineStorage.gameData.turn;
			}
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

				if (!waitingUnits.ContainsKey(unit)) {
					return unit;
				}
			}
			if (waitQueue.Count > 0) {
				MapUnit next = waitQueue.Dequeue();
				if (waitingUnits[next] <= 1) {
					waitingUnits.Remove(next);
				} else {
					--waitingUnits[next];
				}
				return next;
			}
			return MapUnit.NONE;
		}

		public static void ClearWaitQueue() {
			waitQueue.Clear();
			waitingUnits.Clear();
		}

		public static void waitUnit(ID id) {
			MapUnit unit = EngineStorage.gameData.GetUnit(id);
			if (unit == null) {
				log.Warning("Failed to find a matching unit with id {Id}", id);
				return;
			}
			log.Verbose("Found matching unit with id {Id} of type {Type}; adding it to the wait queue", id, unit.GetType().Name);
			waitQueue.Enqueue(unit);
			waitingUnits[unit] = waitingUnits.GetValueOrDefault(unit) + 1;
		}
	}
}

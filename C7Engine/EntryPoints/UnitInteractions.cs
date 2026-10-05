using System.Linq;
using Serilog;

namespace C7Engine {
	using C7GameData;
	using System.Collections.Generic;

	public class UnitInteractions {

		private static Queue<MapUnit> waitQueue = new Queue<MapUnit>();

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

				if (!waitQueue.Contains(unit)) {
					return unit;
				}
			}
			if (waitQueue.Count > 0) {
				return waitQueue.Dequeue();
			}
			return MapUnit.NONE;
		}

		public static void ClearWaitQueue() {
			waitQueue.Clear();
		}

		public static void waitUnit(ID id) {
			foreach (MapUnit unit in EngineStorage.gameData.mapUnits) {
				if (unit.id == id) {
					log.Verbose("Found matching unit with id " + id + " of type " + unit.GetType().Name + "; adding it to the wait queue");
					waitQueue.Enqueue(unit);
				}
			}
			log.Warning("Failed to find a matching unit with id " + id);
		}
	}
}

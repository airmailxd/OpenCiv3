using System;
using System.Collections.Generic;
using System.Linq;

namespace C7GameData {
	// The world as Civ3's replay shows it at a turn, from the game's events
	// (see GameHistory): who owns each tile and where the cities stand, as
	// Civ3 FUN_00598f60 plays the events onto the replay's map. Cities are
	// placed by CityPlaced and CityFounded and removed by CityDestroyed and
	// CityDestroyedOwnerKeepsTile; tiles change hands by TerritoryChanged.
	public class ReplayPlayback {
		public readonly List<ReplayEvent> events;
		public readonly int firstTurn;
		public readonly int lastTurn;

		// gameTurn is the game's current turn, which the replay runs up to.
		public ReplayPlayback(GameHistory history, int gameTurn) {
			// Stable, so events of a turn stay in the order they happened.
			events = history.Events.OrderBy(e => e.Turn).ToList();
			firstTurn = events.Count > 0 ? Math.Min(events[0].Turn, gameTurn) : gameTurn;
			lastTurn = Math.Max(gameTurn, events.Count > 0 ? events[^1].Turn : gameTurn);
		}

		// Whether the replay's list of events shows the event: everything
		// but what only changes the map (Civ3 FUN_0059ba30 skips types 0, 6
		// and 15).
		public static bool IsListed(ReplayEventType type) {
			return type != ReplayEventType.CityPlaced && type != ReplayEventType.TerritoryChanged && type != ReplayEventType.Redraw;
		}

		public class State {
			public readonly Dictionary<(int x, int y), string> owners = new();
			// The cities standing, by tile, with their names.
			public readonly Dictionary<(int x, int y), string> cities = new();
			// The listed events so far, oldest first.
			public readonly List<ReplayEvent> listed = new();
		}

		// The world after every event of the turns up to and including turn.
		public State StateAt(int turn) {
			State state = new();
			foreach (ReplayEvent e in events) {
				if (e.Turn > turn) {
					break;
				}
				Apply(state, e);
			}
			return state;
		}

		// The events of just the one turn.
		public IEnumerable<ReplayEvent> EventsOf(int turn) => events.Where(e => e.Turn == turn);

		private static void Apply(State state, ReplayEvent e) {
			(int, int) at = (e.X, e.Y);
			switch (e.Type) {
				case ReplayEventType.CityPlaced:
				case ReplayEventType.CityFounded:
					state.cities[at] = e.Text;
					break;
				case ReplayEventType.CityDestroyed:
				case ReplayEventType.CityDestroyedOwnerKeepsTile:
					state.cities.Remove(at);
					break;
				case ReplayEventType.CityCaptured:
					// Kept as it was, in case the capture comes from a save
					// without the city's founding.
					state.cities[at] = e.Text;
					break;
				case ReplayEventType.TerritoryChanged:
					if (e.Civ == null) {
						state.owners.Remove(at);
					} else {
						state.owners[at] = e.Civ;
					}
					break;
			}
			if (IsListed(e.Type)) {
				state.listed.Add(e);
			}
		}
	}
}

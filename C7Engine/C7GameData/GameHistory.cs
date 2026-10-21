using System;
using System.Collections.Generic;
using System.Linq;

namespace C7GameData {
	// What Civ3 records of a game for its end-of-game replay ("SUMMARY
	// REPLAY", Conquests/Text/labels.txt), in the order it happened. Civ3
	// keeps these as the SAV's RPLS/RPLT/RPLE sections: a list of turns, each
	// with its events (Civ3 FUN_00598bf0 adds one, padding the list with
	// empty turns up to the current one). An event has a type, a civ, a tile
	// and a text, and a territory change also the tile's previous owner.
	//
	// The numbers are Civ3's own, so that events read from a SAV keep their
	// meaning. Each is named after what records it in Civ3 and what the
	// replay shows of it (Civ3 FUN_0059be00, which picks the labels.txt line
	// of the replay's event list, and FUN_00598f60/FUN_00599350, which play
	// it on and back on the replay's map).
	public enum ReplayEventType {
		// A city that is there without being founded, as when a map is set
		// up (FUN_004b5280, during setup). Shown on the map, not listed.
		CityPlaced = 0,
		// "$CITY0 founded in $DATE1" (FUN_004b5280).
		CityFounded = 1,
		// "$CITY0 destroyed in $DATE1" (FUN_004b5c80): a city whose tile no
		// longer belongs to its owner afterwards...
		CityDestroyed = 2,
		// ...and one whose owner still holds its tile.
		CityDestroyedOwnerKeepsTile = 3,
		// "$CITY0 captured in $DATE1" (FUN_00570bb0), for the captor.
		CityCaptured = 4,
		// "$WONDER0 created in $DATE1" (FUN_004b3f20), at the city.
		WonderBuilt = 5,
		// A tile's owner changed (FUN_005e2e50). Shown on the map, not listed.
		TerritoryChanged = 6,
		// "$KING0 of the $CIVNOUN1 killed in $DATE2" (FUN_005ca720): a King
		// lost in a Regicide game.
		KingKilled = 7,
		// The Capture the Princess events (FUN_005cbde0, FUN_005cf720,
		// FUN_005c5670), which OpenCiv3 doesn't have; kept so Civ3's are read.
		PrincessCaptured = 8,
		PrincessPickedUp = 9,
		PrincessRescued = 10,
		// "Victory point location secured by $CIV0 in $DATE1" and "...lost
		// by..." (FUN_00542ab0); not yet recorded by OpenCiv3.
		VictoryLocationSecured = 11,
		VictoryLocationLost = 12,
		// "A great event for $CIV0 has produced the Great Leader, $LEADER1, in
		// $DATE2" (FUN_0056d9e0, FUN_005cdb10).
		GreatLeader = 13,
		// "$CIV0 enters a Golden Age in $DATE1" (FUN_00568940), at the capital.
		GoldenAge = 14,
		// A marker Civ3 records around setting up a game (FUN_005e18f0), on
		// which the replay only redraws.
		Redraw = 15,
	}

	// One event of the replay. Civ and PreviousCiv are player IDs (the keys
	// of GameData.history), or null for none.
	public class ReplayEvent {
		public int Turn;
		public ReplayEventType Type;
		public string Civ;
		public int X;
		public int Y;
		// The city's, wonder's, leader's or King's name, or null.
		public string Text;
		// A territory change's previous owner.
		public string PreviousCiv;
	}

	public class GameHistory {
		public List<ReplayEvent> Events = new();

		// Each tile's owner as the events have it so far, by tile, which a
		// territory change is compared with. Built from the events (see
		// Begin), so it isn't saved.
		private readonly Dictionary<(int, int), string> territory = new();

		// Tiles whose owner may have changed since the territory was last
		// compared (see GameData.SetTileOwner).
		private readonly HashSet<Tile> pendingTerritory = new();

		// When the last of the Great Books was written and which historians
		// and titles have been used (see GreatBooks). As in Civ3, this lasts
		// only while the game is played: a game loaded starts the wait for the
		// next book afresh.
		[System.Text.Json.Serialization.JsonIgnore]
		public GreatBooks.State greatBooks = new();

		public static string KeyOf(Player player) => player?.id?.ToString();

		public ReplayEvent Record(int turn, ReplayEventType type, Player civ, Tile tile, string text = null, Player previous = null) {
			return Record(turn, type, KeyOf(civ), tile?.XCoordinate ?? 0, tile?.YCoordinate ?? 0, text, KeyOf(previous));
		}

		public ReplayEvent Record(int turn, ReplayEventType type, string civ, int x, int y, string text = null, string previous = null) {
			ReplayEvent e = new() { Turn = turn, Type = type, Civ = civ, X = x, Y = y, Text = text, PreviousCiv = previous };
			Events.Add(e);
			if (type == ReplayEventType.TerritoryChanged) {
				SetTerritory(x, y, civ);
			}
			return e;
		}

		private void SetTerritory(int x, int y, string civ) {
			if (civ == null) {
				territory.Remove((x, y));
			} else {
				territory[(x, y)] = civ;
			}
		}

		// Notes that the tile's owner may have changed.
		public void NoteTerritory(Tile tile) {
			if (tile != null && tile != Tile.NONE) {
				pendingTerritory.Add(tile);
			}
		}

		// Records a territory change for each noted tile whose owner differs
		// from the one the events last gave it. A tile that changes hands and
		// back while borders are worked out isn't recorded at all.
		public void RecordTerritoryChanges(int turn) {
			if (pendingTerritory.Count == 0) {
				return;
			}
			foreach (Tile tile in pendingTerritory.OrderBy(t => t.YCoordinate).ThenBy(t => t.XCoordinate)) {
				RecordTerritoryOf(turn, tile);
			}
			pendingTerritory.Clear();
		}

		private void RecordTerritoryOf(int turn, Tile tile) {
			string owner = KeyOf(tile.owningCity?.owner);
			territory.TryGetValue((tile.XCoordinate, tile.YCoordinate), out string was);
			if (owner != was) {
				Record(turn, ReplayEventType.TerritoryChanged, owner, tile.XCoordinate, tile.YCoordinate, null, was);
			}
		}

		// Readies the history of a game just loaded or begun: the territory is
		// read back from the events. A game that has no events yet but already
		// has cities (a scenario, or a save made before the replay was
		// recorded) gets its cities and borders recorded as they stand, as
		// Civ3 records the cities of a map being set up without announcing
		// them (ReplayEventType.CityPlaced).
		public void Begin(GameData gameData) {
			territory.Clear();
			pendingTerritory.Clear();
			foreach (ReplayEvent e in Events) {
				if (e.Type == ReplayEventType.TerritoryChanged) {
					SetTerritory(e.X, e.Y, e.Civ);
				}
			}
			if (Events.Count > 0 || gameData.cities.Count == 0) {
				return;
			}
			foreach (City city in gameData.cities) {
				Record(gameData.turn, ReplayEventType.CityPlaced, city.owner, city.location, city.name);
			}
			foreach (Tile tile in gameData.map.tiles) {
				if (tile.owningCity != null) {
					RecordTerritoryOf(gameData.turn, tile);
				}
			}
		}

		// The last turn with any event, at least the given one.
		public int LastTurn(int atLeast = 0) {
			int last = atLeast;
			foreach (ReplayEvent e in Events) {
				last = Math.Max(last, e.Turn);
			}
			return last;
		}
	}
}

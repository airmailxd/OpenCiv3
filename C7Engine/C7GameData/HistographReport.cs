using System;
using System.Collections.Generic;

namespace C7GameData {
	// One civ's line on the Histograph.
	public record HistographSeries(Player player, List<HistographPoint> points);

	// A turn on the Histograph: the turn's place in the civ's history (0 for
	// the first turn recorded), its date and the value shown.
	public record struct HistographPoint(int turn, int date, int value);

	// What Civ3's Histograph plots, from each civ's history of turns
	// (Player.UpdateHistory). Civ3 shows a civ's "Power", "Score" or
	// "Victory Points" over the game, scaled by "Turns" or "Years"
	// (Conquests/Text/labels.txt: "HISTOGRAPH", "Show", "Power", "Score",
	// "Victory Points", "Scale by", "Turns", "Years").
	public static class HistographReport {
		// Culture and VictoryPoints are shown by the replay's graph, which
		// Civ3 shows by "Power", "Score", "Culture" and, in a game with
		// victory points, "Victory Points" (Civ3 FUN_00599d00).
		public enum Measure {
			Score,
			Power,
			Culture,
			VictoryPoints,
		}

		public static int ValueOf(HistTurnRecord record, Measure measure) => measure switch {
			Measure.Power => record.Power,
			Measure.Culture => record.Culture,
			Measure.VictoryPoints => record.VP,
			_ => record.Score,
		};

		// Every civ's line, barbarians aside, whether met or not, up to and
		// including the date: what the replay at the end of the game shows.
		public static List<HistographSeries> AllSeries(GameData gameData, Measure measure, int upToDate) {
			List<HistographSeries> result = new();
			foreach (Player civ in gameData.players) {
				if (civ.IsIndependent || gameData.history == null || !gameData.history.TryGetValue(civ.HistoryKey, out List<HistTurnRecord> records)) {
					continue;
				}
				List<HistographPoint> points = new(records.Count);
				for (int i = 0; i < records.Count && records[i].Date <= upToDate; ++i) {
					points.Add(new HistographPoint(i, records[i].Date, ValueOf(records[i], measure)));
				}
				result.Add(new HistographSeries(civ, points));
			}
			return result;
		}

		// The lines of the viewer and the civs they have met, the viewer
		// first. UNVERIFIED: that civs the viewer hasn't met are left out,
		// as the other screens leave out what the viewer hasn't seen.
		public static List<HistographSeries> SeriesFor(GameData gameData, Player viewer, Measure measure) {
			List<Player> civs = [viewer, .. gameData.GetKnownRivals(viewer)];
			List<HistographSeries> result = new();
			foreach (Player civ in civs) {
				if (gameData.history == null || !gameData.history.TryGetValue(civ.HistoryKey, out List<HistTurnRecord> records)) {
					continue;
				}
				List<HistographPoint> points = new(records.Count);
				for (int i = 0; i < records.Count; ++i) {
					points.Add(new HistographPoint(i, records[i].Date, ValueOf(records[i], measure)));
				}
				result.Add(new HistographSeries(civ, points));
			}
			return result;
		}

		// The largest value of any line, at least 1, for the top of the graph.
		public static int MaxValue(List<HistographSeries> series) {
			int max = 1;
			foreach (HistographSeries s in series) {
				foreach (HistographPoint p in s.points) {
					max = Math.Max(max, p.value);
				}
			}
			return max;
		}
	}
}

using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class HistographReportTest {
	private static Player Civ(string id, string name) =>
		new() { id = ID.FromString(id), civilization = new Civilization(name) };

	private static C7GameData.GameData Game(Player viewer, params Player[] others) {
		C7GameData.GameData gameData = new() { history = new Dictionary<string, List<HistTurnRecord>>() };
		gameData.players.Add(viewer);
		gameData.players.AddRange(others);
		foreach (Player p in gameData.players) {
			gameData.history[p.HistoryKey] = new List<HistTurnRecord>();
		}
		return gameData;
	}

	[Fact]
	public void SeriesFor_GivesTheViewerAndTheCivsTheyHaveMet() {
		Player us = Civ("player-1", "Rome");
		Player met = Civ("player-2", "Greece");
		Player unmet = Civ("player-3", "Egypt");
		us.playerRelationships[met.id] = new PlayerRelationship();
		C7GameData.GameData gameData = Game(us, met, unmet);
		gameData.history[us.HistoryKey].AddRange([
			new HistTurnRecord { Date = -4000, Score = 1, Power = 10 },
			new HistTurnRecord { Date = -3950, Score = 3, Power = 12 },
		]);
		gameData.history[met.HistoryKey].Add(new HistTurnRecord { Date = -4000, Score = 2, Power = 30 });
		gameData.history[unmet.HistoryKey].Add(new HistTurnRecord { Date = -4000, Score = 9, Power = 90 });

		List<HistographSeries> score = HistographReport.SeriesFor(gameData, us, HistographReport.Measure.Score);

		Assert.Equal([us, met], score.Select(s => s.player));
		Assert.Equal([new HistographPoint(0, -4000, 1), new HistographPoint(1, -3950, 3)], score[0].points);
		Assert.Equal(3, HistographReport.MaxValue(score));

		List<HistographSeries> power = HistographReport.SeriesFor(gameData, us, HistographReport.Measure.Power);
		Assert.Equal([10, 12], power[0].points.Select(p => p.value));
		Assert.Equal(30, HistographReport.MaxValue(power));
	}

	[Fact]
	public void MaxValue_IsAtLeastOne() {
		Assert.Equal(1, HistographReport.MaxValue([]));
	}
}

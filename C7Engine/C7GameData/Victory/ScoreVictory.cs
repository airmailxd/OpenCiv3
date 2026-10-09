using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

public class ScoreVictory : IVictory {

	public string Header() => "Score";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		var history = gameData.history[player.HistoryKey];
		var lastTurn = history.LastOrDefault();
		var totalAccumulatedScore = lastTurn?.Score ?? 0;

		VictoryStatus status = new VictoryStatus {
			Player = player,
			Score = totalAccumulatedScore,
		};
		// The turn score is only shown in the victory status screen; the
		// end of turn victory check (which runs right after UpdateHistory
		// computed it) never reads it. So it is computed when first read.
		status.SetTurnScoreSource(() => ComputeTurnScore(player, gameData));
		return status;
	}

	public bool HasVictory(VictoryStatus status) {
		return false;
	}

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return TurnScorePrint(status, rivalStatuses);
		yield return ScorePrint(status, rivalStatuses);
	}

	private string[] TurnScorePrint(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		var topRivalByScore = rivalStatuses.OrderByDescending(r => r.TurnScore).FirstOrDefault();

		var topRival = topRivalByScore?.Player?.civilization?.name ?? "";
		var topRivalScore = topRivalByScore == null ? "" : $"{topRivalByScore.TurnScore}";

		return [
			"",
			"",
			"Turn Score:",
			$"{status.TurnScore}",
			topRival,
			topRivalScore
		];
	}

	private string[] ScorePrint(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		var topRivalByScore = rivalStatuses.OrderByDescending(r => r.Score).FirstOrDefault();

		var topRival = topRivalByScore?.Player?.civilization?.name ?? "";
		var topRivalScore = topRivalByScore == null ? "" : $"{topRivalByScore.Score}";

		return [
			"Tie-breaker at time limit",
			"",
			"Current score:",
			$"{status.Score}",
			topRival,
			topRivalScore
		];
	}

	public static float ComputeTurnScore(Player player, GameData gameData) {
		// A LAN guest isn't sent all of a rival's cities and territory, so
		// the host works it out.
		if (player.hostFacts != null) {
			return player.hostFacts.turnScore;
		}
		List<Tile> scoredTiles = player.tileKnowledge.ScoreTiles();

		int happyCitizens = 0;
		int contentCitizens = 0;
		int specialists = 0;
		foreach (City c in player.cities) {
			foreach (CityResident r in c.residents) {
				if (!r.citizenType.IsDefaultCitizen) {
					++specialists;
				} else if (r.mood == CityResident.Mood.Happy) {
					++happyCitizens;
				} else if (r.mood == CityResident.Mood.Content) {
					++contentCitizens;
				}
			}
		}

		int futureTechs = 0; // TODO: future techs

		// TODO: gameData.gameDifficulty.ScoreMultiplier
		float difficultyFactor = GetDifficultyScoreFactor(gameData);

		float turnScore = (scoredTiles.Count + (2 * happyCitizens) + contentCitizens + specialists + futureTechs);
		turnScore *= difficultyFactor;

		return turnScore;
	}

	// 1 for Chieftain, 2 for Warlord, 3 for Regent, etc.
	private static float GetDifficultyScoreFactor(GameData gameData) {
		int idx = 0;
		for (int i = 0; i < gameData.difficulties.Count; ++i) {
			if (gameData.difficulties[i] == gameData.gameDifficulty) {
				idx = i;
				break;
			}
		}
		return idx + 1;
	}
}

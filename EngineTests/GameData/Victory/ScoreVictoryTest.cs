using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData.Victory;

public class ScoreVictoryTest {

	[Fact]
	public void HasVictory_AlwaysFalse_ScoreIsATiebreakerNotAWinCondition() {
		var victory = new ScoreVictory();

		Assert.False(victory.HasVictory(new VictoryStatus { Score = 999999f, TurnScore = 999999f }));
	}

	private static CitizenType DefaultCitizen() => new() { IsDefaultCitizen = true };
	private static CitizenType Specialist() => new() { IsDefaultCitizen = false };

	[Fact]
	public void ComputeTurnScore_CombinesTilesCitizensAndSpecialistsThenAppliesDifficultyFactor() {
		Player player = new() { civilization = new Civilization("Rome") };

		City city = new City(Tile.NONE, player, "Roma", ID.None("city"));
		city.residents.Add(new CityResident { citizenType = DefaultCitizen(), mood = CityResident.Mood.Happy });
		city.residents.Add(new CityResident { citizenType = DefaultCitizen(), mood = CityResident.Mood.Content });
		city.residents.Add(new CityResident { citizenType = DefaultCitizen(), mood = CityResident.Mood.Unhappy });
		city.residents.Add(new CityResident { citizenType = Specialist() });
		player.cities.Add(city);

		TerrainType land = new TerrainType { Key = "grassland" };
		Tile scoredTile1 = new Tile(ID.None("tile")) { baseTerrainType = land, owningCity = city };
		Tile scoredTile2 = new Tile(ID.None("tile")) { baseTerrainType = land, owningCity = city };
		player.tileKnowledge.knownTiles.Add(scoredTile1);
		player.tileKnowledge.knownTiles.Add(scoredTile2);

		var lowDifficulty = new Difficulty();
		var highDifficulty = new Difficulty();
		C7GameData.GameData gameData = new() {
			difficulties = new List<Difficulty> { lowDifficulty, highDifficulty },
			gameDifficulty = highDifficulty // index 1 -> factor of 2
		};

		float turnScore = ScoreVictory.ComputeTurnScore(player, gameData);

		// 2 scored tiles + 2*1 happy + 1 content + 1 specialist = 6, times the
		// difficulty factor of (index 1 + 1) = 2.
		Assert.Equal(12f, turnScore);
	}

	[Fact]
	public void ComputeTurnScore_UnrecognizedDifficulty_FallsBackToFactorOfOne() {
		Player player = new() { civilization = new Civilization("Rome") };
		player.cities.Add(new City(Tile.NONE, player, "Roma", ID.None("city")));

		C7GameData.GameData gameData = new() {
			difficulties = new List<Difficulty> { new Difficulty(), new Difficulty() },
			gameDifficulty = new Difficulty() // not present in the difficulties list
		};

		float turnScore = ScoreVictory.ComputeTurnScore(player, gameData);

		Assert.Equal(0f, turnScore); // no tiles/citizens, factor of 1 => still 0
	}

	[Fact]
	public void Evaluate_ReadsAccumulatedScoreFromHistory() {
		Player player = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1") };
		player.cities.Add(new City(Tile.NONE, player, "Roma", ID.None("city")));

		C7GameData.GameData gameData = new() {
			difficulties = new List<Difficulty> { new Difficulty() },
			gameDifficulty = new Difficulty(),
			history = new Dictionary<string, List<HistTurnRecord>> {
				[player.id.ToString()] = new() {
					new HistTurnRecord { Score = 10 },
					new HistTurnRecord { Score = 25 }
				}
			}
		};
		gameData.gameDifficulty = gameData.difficulties[0];

		var victory = new ScoreVictory();
		VictoryStatus status = victory.Evaluate(player, gameData);

		Assert.Equal(25, status.Score);
	}

	private static C7GameData.GameData GameAtTurn(int turn, int difficultyIndex = 0, TimeOptions time = null) {
		var difficulties = new List<Difficulty> { new Difficulty(), new Difficulty(), new Difficulty() };
		return new C7GameData.GameData {
			turn = turn,
			difficulties = difficulties,
			gameDifficulty = difficulties[difficultyIndex],
			timeOptions = time ?? new TimeOptions(),
		};
	}

	// "(2050 - FinishYear) * Difficulty", with a year BC as a negative number
	// (https://civfanatics.com/civ3/strategy/game-mechanics/maximizing-your-score/).
	[Theory]
	[InlineData(0, 0, 6050)] // 4000 BC on Chieftain
	[InlineData(1, 0, 6000)] // 3950 BC
	[InlineData(0, 2, 18150)] // 4000 BC on Regent, three times as much
	[InlineData(540, 0, 0)] // 2050 AD, the end of a standard game
	[InlineData(600, 0, 0)] // after 2050 AD
	public void EarlyWinBonus_IsTheYearsLeftBefore2050TimesTheDifficulty(int turn, int difficultyIndex, int expected) {
		Assert.Equal(expected, ScoreVictory.EarlyWinBonus(GameAtTurn(turn, difficultyIndex)));
	}

	[Fact]
	public void EarlyWinBonus_NoneWhenTheGameIsNotCountedInYears() {
		Assert.Equal(0, ScoreVictory.EarlyWinBonus(GameAtTurn(10, time: new TimeOptions { baseUnit = TimeUnit.Months })));
	}

	[Fact]
	public void Evaluate_AddsTheBonusToTheWinnersScoreOnly() {
		Player winner = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1") };
		Player loser = new() { civilization = new Civilization("Greece"), id = ID.FromString("player-2") };
		C7GameData.GameData gameData = GameAtTurn(100);
		gameData.history = new Dictionary<string, List<HistTurnRecord>> {
			[winner.HistoryKey] = new() { new HistTurnRecord { Score = 40 } },
			[loser.HistoryKey] = new() { new HistTurnRecord { Score = 30 } },
		};
		gameData.winner = winner;
		gameData.gameOver = true;
		gameData.victoryBonus = 500;

		var victory = new ScoreVictory();
		Assert.Equal(540, victory.Evaluate(winner, gameData).Score);
		Assert.Equal(30, victory.Evaluate(loser, gameData).Score);
	}
}

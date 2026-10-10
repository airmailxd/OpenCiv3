using System;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The difficulty's cost factor shrinks the AI's food box as well as its
// shields and research
// (https://forums.civfanatics.com/threads/ai-difficulty-level-bonuses.37490/).
public sealed class AiDifficultyBonusTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public AiDifficultyBonusTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		gameData.gameDifficulty = new Difficulty { Name = "Emperor", AiCostFactor = 8 };
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	[Fact]
	public void AiFoodBoxIsScaledByTheCostFactor() {
		int baseFoodBox = player.rules.FoodNeededToGrowForLevel1Cities;
		Assert.Equal(baseFoodBox * 8 / 10, city.FoodNeededToGrow());
	}

	[Fact]
	public void HumanFoodBoxIsNotScaled() {
		player.isHuman = true;
		Assert.Equal(player.rules.FoodNeededToGrowForLevel1Cities, city.FoodNeededToGrow());
	}
}

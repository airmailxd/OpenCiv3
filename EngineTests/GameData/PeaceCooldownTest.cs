using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.AIData;
using EngineTests.Utils;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.GameData;

// The AI must not make peace and declare war again in the same round: the
// peace cooldown (compendium 08) holds for 8 turns.
public class PeaceCooldownTest : IClassFixture<SaveGameFixture> {
	C7GameData.GameData gameData;

	public PeaceCooldownTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[Fact]
	public void NoNewWarWithinEightTurnsOfPeace() {
		Player a = gameData.players[1];
		Player b = gameData.players[2];
		a.EnsureRelationshipExists(b);
		b.EnsureRelationshipExists(a);
		Assert.False(StockWarPlotting.InPeaceCooldown(gameData, a, b));

		DeclareWar(a, b, false, gameData.turn, gameData.turn);
		SignPeaceAfterWar(a, b, gameData);
		Assert.True(StockWarPlotting.InPeaceCooldown(gameData, a, b));
		Assert.True(StockWarPlotting.InPeaceCooldown(gameData, b, a));
		Assert.NotEqual(b, WarPriority.PickPlayerToFight(a));

		gameData.turn += StockWarPlotting.PeaceCooldownTurns - 1;
		Assert.True(StockWarPlotting.InPeaceCooldown(gameData, a, b));
		gameData.turn += 1;
		Assert.False(StockWarPlotting.InPeaceCooldown(gameData, a, b));
	}
}

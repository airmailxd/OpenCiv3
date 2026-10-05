using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class BarbarianCampTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public BarbarianCampTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Fact]
	public void EveryCampStartsWithABasicBarbarianDefender() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Assert.NotEmpty(gameData.map.barbarianCamps);

		foreach (Tile camp in gameData.map.barbarianCamps) {
			MapUnit defender = Assert.Single(camp.unitsOnTile);
			Assert.True(defender.owner.isBarbarians);
			Assert.Equal(gameData.barbarianInfo.basicBarbarian, defender.unitType);
			Assert.Equal(gameData.barbarianInfo.maxHitpoints, defender.hitPointsRemaining);
		}
	}

	[Fact]
	public void CampsStopSpawningOnceFull() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Raging;

		for (int turn = 0; turn < 500; ++turn) {
			BarbarianInteractions.SpawnBarbarians(gameData);
		}

		foreach (Tile camp in gameData.map.barbarianCamps) {
			Assert.Equal(BarbarianInteractions.MaxUnitsPerCamp, camp.unitsOnTile.Count);
		}
	}
}

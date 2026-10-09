using System.Collections.Generic;
using System.Linq;
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
		GiveEveryCivCities(gameData, 2);

		for (int turn = 0; turn < 500; ++turn) {
			BarbarianInteractions.SpawnBarbarians(gameData);
		}

		foreach (Tile camp in gameData.map.barbarianCamps) {
			Assert.Equal(BarbarianInteractions.MaxUnitsPerCamp, camp.unitsOnTile.Count);
		}
	}

	[Fact]
	public void CampsDontSpawnUntilACivHasASecondCity() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Raging;
		GiveEveryCivCities(gameData, 1);

		for (int turn = 0; turn < 500; ++turn) {
			Assert.Equal(0, BarbarianInteractions.SpawnBarbarians(gameData));
		}
	}

	[Fact]
	public void SpawnRateScalesWithTheCivsThatHaveASecondCity() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated && p.isIncludedInGame).ToList();
		Assert.True(civs.Count >= 2);

		Assert.Equal(0f, BarbarianInteractions.SettledCivShare(gameData));
		GiveCities(gameData, civs[0], 2);
		Assert.Equal(1f / civs.Count, BarbarianInteractions.SettledCivShare(gameData), 4);
		GiveEveryCivCities(gameData, 2);
		Assert.Equal(1f, BarbarianInteractions.SettledCivShare(gameData));
	}

	// Gives each civ placeholder cities, up to the count, which is all the
	// spawn rate looks at.
	private static void GiveEveryCivCities(C7GameData.GameData gameData, int count) {
		foreach (Player player in gameData.players.Where(p => !p.isBarbarians)) {
			GiveCities(gameData, player, count);
		}
	}

	private static void GiveCities(C7GameData.GameData gameData, Player player, int count) {
		while (player.cities.Count < count) {
			player.cities.Add(new City(Tile.NONE, player, "Test", gameData.ids.CreateID("city")));
		}
	}
}

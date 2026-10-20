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

	// "The game starts spawning advanced instead of regular barbs when there
	// are at least four cities per player on the map." (Flintlock, reading
	// the 1.22 code, C3X thread post-16644538)
	[Fact]
	public void AdvancedBarbariansWaitForFourCitiesPerCiv() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated && p.isIncludedInGame).ToList();
		GiveEveryCivCities(gameData, 3);
		Assert.False(BarbarianInteractions.AdvancedBarbariansAvailable(gameData));

		// The count is over the whole map, so one civ's extra cities make up
		// for another's.
		GiveCities(gameData, civs[0], 3 + civs.Count);
		Assert.True(BarbarianInteractions.AdvancedBarbariansAvailable(gameData));
	}

	[Fact]
	public void AdvancedBarbariansReplaceBasicOnes() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Tile inland = gameData.map.tiles.First(t => t.IsLand() && !t.NeighborsWater());
		for (int i = 0; i < 50; ++i) {
			Assert.Equal(gameData.barbarianInfo.basicBarbarian,
				BarbarianInteractions.SelectBarbarianUnitType(gameData.barbarianInfo, inland, advancedAllowed: false));
			Assert.Equal(gameData.barbarianInfo.advancedBarbarian,
				BarbarianInteractions.SelectBarbarianUnitType(gameData.barbarianInfo, inland, advancedAllowed: true));
		}
	}

	[Fact]
	public void CampsStopSpawningOnceFull() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Raging;
		GiveEveryCivCities(gameData, 2);
		// Few enough camps that filling them stays under the barbarians'
		// unit limit.
		while (gameData.map.barbarianCamps.Count * BarbarianInteractions.MaxUnitsPerCamp > BarbarianInteractions.MaxBarbarianUnits(gameData)) {
			Tile camp = gameData.map.barbarianCamps[^1];
			foreach (MapUnit unit in camp.unitsOnTile.ToList()) {
				gameData.RemoveUnit(unit);
			}
			camp.hasBarbarianCamp = false;
			gameData.map.barbarianCamps.Remove(camp);
		}

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

	// The barbarians field at most 2, 3 or 4 units per player on Roaming,
	// Restless and Raging (CFC 267101).
	[Theory]
	[InlineData(BarbarianActivity.Roaming, 2)]
	[InlineData(BarbarianActivity.Restless, 3)]
	[InlineData(BarbarianActivity.Raging, 4)]
	public void CampsStopSpawningAtTheBarbarianUnitLimit(BarbarianActivity activity, int perPlayer) {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		gameData.barbarianInfo.barbarianActivity = activity;
		GiveEveryCivCities(gameData, 2);
		int limit = perPlayer * BarbarianInteractions.NumPlayers(gameData);
		Assert.Equal(limit, BarbarianInteractions.MaxBarbarianUnits(gameData));
		Player barbarians = gameData.players.Single(p => p.isBarbarians);

		// Plenty of camps, so the camps' own room isn't what stops them.
		List<Tile> sites = BarbarianInteractions.PossibleNewCampSites(gameData);
		for (int i = 0; i < sites.Count && gameData.map.barbarianCamps.Count * BarbarianInteractions.MaxUnitsPerCamp <= limit + 3; ++i) {
			if (sites[i].neighbors.Values.All(n => !n.hasBarbarianCamp)) {
				BarbarianInteractions.AddCamp(gameData, sites[i]);
			}
		}
		Assert.True(gameData.map.barbarianCamps.Count * BarbarianInteractions.MaxUnitsPerCamp > limit);

		for (int turn = 0; turn < 2000; ++turn) {
			BarbarianInteractions.SpawnBarbarians(gameData);
		}
		Assert.Equal(limit, barbarians.units.Count);
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

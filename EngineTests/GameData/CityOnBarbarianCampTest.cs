using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class CityOnBarbarianCampTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;

	public CityOnBarbarianCampTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	// Founding a city whose initial borders take in a barbarian camp disperses
	// the camp and pays the founder, just like walking a unit into it.
	[Fact]
	public void FoundingACityDispersesCampsInItsBorders() {
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile site = player.units.First(u => u.unitType.isSettler).location;
		Tile camp = site.neighbors.Values.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && !t.hasBarbarianCamp);
		camp.hasBarbarianCamp = true;
		gameData.map.barbarianCamps.Add(camp);
		int goldBefore = player.gold;

		City city = CityInteractions.BuildCity(site, player, player.GetNextCityName());

		Assert.Equal(city, camp.owningCity);
		Assert.False(camp.hasBarbarianCamp);
		Assert.DoesNotContain(camp, gameData.map.barbarianCamps);
		Assert.Equal(goldBefore + BarbarianInteractions.CampDispersalGold, player.gold);
	}

	// Camps outside the new city's borders are left alone.
	[Fact]
	public void FoundingACityLeavesDistantCampsAlone() {
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile site = player.units.First(u => u.unitType.isSettler).location;
		Tile camp = gameData.map.tiles.First(t => t.IsLand() && t.DistanceTo(site) > 6 && t.owningCity == null && !t.hasBarbarianCamp);
		camp.hasBarbarianCamp = true;
		gameData.map.barbarianCamps.Add(camp);
		int goldBefore = player.gold;

		CityInteractions.BuildCity(site, player, player.GetNextCityName());

		Assert.True(camp.hasBarbarianCamp);
		Assert.Contains(camp, gameData.map.barbarianCamps);
		Assert.Equal(goldBefore, player.gold);
	}
}

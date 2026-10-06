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

	// A camp that ends up inside a civ's borders as they grow with culture is
	// dispersed too, paying the civ as founding a city over it would.
	[Fact]
	public void GrowingBordersDisperseCampsInThem() {
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile site = player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(site, player, player.GetNextCityName());
		// Just beyond the city's first borders, where its next ones reach.
		Tile camp = site.GetTilesWithinRankDistance(2)
			.First(t => Tile.IsTileValid(t) && t.IsLand() && t.RankDistanceTo(site) == 2
				&& t.owningCity == null && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp);
		camp.hasBarbarianCamp = true;
		gameData.map.barbarianCamps.Add(camp);
		int goldBefore = player.gold;

		// Bring the city to the edge of its next border expansion, so that
		// this turn's culture takes it over.
		int culturePerTurn = city.GetCulturePerTurn();
		Assert.InRange(culturePerTurn, 1, 9);
		city.perPlayerCulture[player] = 10 - culturePerTurn;
		player.HandleCityUpdates(gameData);

		Assert.Equal(city, camp.owningCity);
		Assert.False(camp.hasBarbarianCamp);
		Assert.DoesNotContain(camp, gameData.map.barbarianCamps);
		Assert.True(player.gold >= goldBefore + BarbarianInteractions.CampDispersalGold);
	}

	// Camps outside every civ's borders stay.
	[Fact]
	public void DispersingCampsInBordersLeavesUnclaimedCampsAlone() {
		Tile camp = gameData.map.tiles.First(t => t.IsLand() && t.owningCity == null && !t.hasBarbarianCamp);
		camp.hasBarbarianCamp = true;
		gameData.map.barbarianCamps.Add(camp);

		BarbarianInteractions.DisperseCampsWithinBorders(gameData);

		Assert.True(camp.hasBarbarianCamp);
		Assert.Contains(camp, gameData.map.barbarianCamps);
	}
}

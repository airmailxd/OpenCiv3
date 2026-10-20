using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// City economy rules checked against doc/civ3-compendium/02-city-economy.md.
public class CityEconomyRulesTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;

	public CityEconomyRulesTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		us = gameData.players.First(p => !p.isBarbarians && !p.isHuman);
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private TerrainType Terrain(string key) {
		return gameData.terrainTypes.First(t => t.Key == key);
	}

	private static void SetTerrain(Tile tile, TerrainType terrain) {
		tile.baseTerrainType = terrain;
		tile.overlayTerrainType = terrain;
		tile.isBonusShield = false;
		tile.Resource = Resource.NONE;
	}

	// A size 1 city on plain grassland, with no river, whose citizen works
	// the given neighbouring terrain.
	private City TownOnGrassland(TerrainType worked) {
		Tile site = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities() && !t.BordersRiver()
			&& t.neighbors.Values.All(n => n != Tile.NONE && IsEmptyLand(n) && !n.BordersRiver()));
		SetTerrain(site, Terrain("grassland"));
		Tile workedTile = site.neighbors.Values.First();
		SetTerrain(workedTile, worked);
		City city = CityInteractions.BuildCity(site, us, "Test Town");
		// The capital, so it loses nothing to corruption or waste.
		Assert.True(city.IsCapital());
		city.residents[0].tileWorked = workedTile;
		return city;
	}

	// A town's centre gets its minimum shield only if the terrain and the
	// resource together make none: "+Shields from bonus resources" comes
	// before "+1 if shields==0 so far" (DaveMcW,
	// https://codehappy.net/apolyton/threads/78438-1.htm).
	[Fact]
	public void TownCentreMinimumShieldIsCheckedAfterTheResource() {
		City city = TownOnGrassland(Terrain("grassland"));
		Tile centre = city.location;
		Assert.Equal(1, centre.ProductionYield(city).yield);

		Resource shieldResource = gameData.Resources.First(r => r.ShieldsBonus == 1 && r.Prerequisite == null);
		centre.Resource = shieldResource;
		Assert.Equal(1, centre.ProductionYield(city).yield);
	}

	// Wealth turns 4 shields into a gold in C3C, and 2 once Economics is
	// known (Conquests Civilopedia, BLDG_Wealth: "a ratio of four to one",
	// "reduced to two to one"), whole gold only, at least 1.
	[Fact]
	public void WealthPaysAGoldForEachFullShieldRatio() {
		us.government = gameData.governments.First(g => g.name == "Monarchy");
		City city = TownOnGrassland(Terrain("forest"));
		Inflow wealth = gameData.Inflows.First(i => i.name == "Wealth");
		city.SetItemBeingProduced(wealth);
		us.DoCorruptionCalculations(gameData);

		Assert.Equal(3, city.CurrentProductionYield().useful);
		Assert.Equal(4, gameData.rules.ShieldCostPerGold);
		Assert.Equal(1, city.CurrentCommerceYield().wealth);

		Tech economics = gameData.techs.First(t => t.DoublesWealthProduction);
		us.knownTechs.Add(economics.id);
		// 3 shields at 2 a gold is 1 gold, not 1.5 rounded up.
		Assert.Equal(1, city.CurrentCommerceYield().wealth);
	}

	// Cities as far from the capital as each other rank in founding order
	// (alexman, CFC 76619 #79: "founding date is a tie-breaker for cities at
	// the same distance"), whatever order the player's own list is in.
	[Fact]
	public void CorruptionRankTiesGoToTheOlderCity() {
		Tile capitalSite = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.GetTilesWithinRankDistance(6).Count(n => n != Tile.NONE && IsEmptyLand(n) && n.RankDistanceTo(t) == 5) >= 2);
		Tile[] ring = capitalSite.GetTilesWithinRankDistance(6)
			.Where(n => n != Tile.NONE && IsEmptyLand(n) && n.IsAllowCities() && n.RankDistanceTo(capitalSite) == 5)
			.ToArray();
		Tile first = ring[0];
		Tile second = ring.First(n => n.RankDistanceTo(first) >= 2);

		City capital = CityInteractions.BuildCity(capitalSite, us, "Capital");
		Assert.True(capital.IsCapital());
		City older = CityInteractions.BuildCity(first, us, "Older");
		City newer = CityInteractions.BuildCity(second, us, "Newer");

		// As after a capture, the newer city comes first in the player's list.
		us.cities.Remove(older);
		us.cities.Add(older);
		Assert.True(us.cities.IndexOf(newer) < us.cities.IndexOf(older));

		us.DoCorruptionCalculations(gameData);
		Assert.Equal(older.rankIndex + 1, newer.rankIndex);
		Assert.True(capital.rankIndex < older.rankIndex);
	}
}

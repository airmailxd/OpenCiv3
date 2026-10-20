using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Strategic resources in use run out, and a new source turns up elsewhere
// (see ResourceDepletion).
public class ResourceDepletionTest {
	private static readonly TerrainImprovement road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	private readonly TerrainType desert = new() { Key = "desert", allowedResources = { "Oil" } };
	private readonly TerrainType grassland = new() { Key = "grassland" };
	private readonly Resource oil = new() { Key = "Oil", Name = "Oil", Category = ResourceCategory.STRATEGIC, DisappearanceRatio = 200 };

	private readonly C7GameData.GameData gameData;
	private readonly GameMap map;
	private readonly Player us;
	private readonly City capital;
	private readonly Tile oilTile;

	public ResourceDepletionTest() {
		gameData = new C7GameData.GameData() { gameDifficulty = new Difficulty() };
		gameData.Resources.Add(oil);
		EngineStorage.InitializeGameDataForTests(gameData);
		C7GameData.GameData.rng = new Random(7);

		// Two rows of tiles: the top row is grassland, where oil can't be,
		// and the bottom row desert, where it can.
		map = new() { numTilesWide = 20, numTilesTall = 2, tiles = new List<Tile>() };
		for (int i = 0; i < 20; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			TerrainType terrain = y == 0 ? grassland : desert;
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y, baseTerrainType = terrain, overlayTerrainType = terrain });
		}
		map.computeNeighbors();
		gameData.map = map;

		us = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1"), government = new Government(), isHuman = true };
		gameData.players.Add(us);

		Tile cityTile = map.tileAt(0, 0);
		capital = new(cityTile, us, "Rome", ID.None("city")) { capital = true };
		cityTile.cityAtTile = capital;
		us.cities.Add(capital);

		// The oil is on desert inside our borders, next to the capital.
		oilTile = map.tileAt(1, 1);
		oilTile.Resource = oil;
		oilTile.ResourceKey = oil.Key;
		oilTile.owningCity = capital;
		gameData.InvalidateCachedTradeNetwork();
	}

	private void Connect() {
		oilTile.overlays.Add(road);
		gameData.InvalidateCachedTradeNetwork();
	}

	private List<Tile> OilTiles() => map.tiles.Where(t => t.Resource == oil).ToList();

	private List<string> TradeNews() => EngineStorage.messagesToUI.OfType<MsgShowTradeAdvisorPopup>().Select(m => m.message).ToList();

	[Fact]
	public void AConnectedSourceRunsOutAndANewOneAppearsOnValidTerrain() {
		Connect();
		Assert.True(ResourceDepletion.MayRunOut(gameData, oilTile));

		// Seeded, so this run is the same every time. A 1 in 200 chance a
		// turn comes up within a few hundred turns.
		int turns = 0;
		while (oilTile.Resource == oil) {
			Assert.True(++turns < 5000, "the oil never ran out");
			ResourceDepletion.ProcessEndOfRound(gameData);
		}

		Assert.Equal(Resource.NONE, oilTile.Resource);
		Assert.Equal(Resource.NONE.Key, oilTile.ResourceKey);
		Tile found = Assert.Single(OilTiles());
		Assert.NotSame(oilTile, found);
		Assert.Same(desert, found.overlayTerrainType);
		Assert.Null(found.cityAtTile);
		Assert.Equal(oil.Key, found.ResourceKey);
		Assert.Contains("This source of Oil has been exhausted!", TradeNews());
	}

	// "The code simply checks if a depleted resource is strategic before
	// respawning it" (Flintlock, C3X thread post-16784827).
	[Fact]
	public void ALuxuryThatRunsOutDoesNotComeBack() {
		Resource wine = new() { Key = "Wine", Name = "Wine", Category = ResourceCategory.LUXURY, DisappearanceRatio = 100 };
		gameData.Resources.Add(wine);
		desert.allowedResources.Add("Wine");
		oilTile.Resource = wine;
		oilTile.ResourceKey = wine.Key;

		Assert.Null(ResourceDepletion.RunOut(gameData, oilTile));
		Assert.Equal(Resource.NONE, oilTile.Resource);
		Assert.DoesNotContain(map.tiles, t => t.Resource == wine);
	}

	[Fact]
	public void TheNewSourceIsAnnouncedInsideTheFindersBorders() {
		Connect();
		oil.DisappearanceRatio = 1;
		// Every other desert tile is ours, so the new source lands in our
		// borders.
		foreach (Tile t in map.tiles.Where(t => t.overlayTerrainType == desert)) {
			t.owningCity = capital;
		}

		ResourceDepletion.ProcessEndOfRound(gameData);

		Assert.Equal(Resource.NONE, oilTile.Resource);
		Assert.Single(OilTiles());
		Assert.Equal(new[] { "This source of Oil has been exhausted!", "We have discovered a new source of Oil!" }, TradeNews());
	}

	[Fact]
	public void AnUnconnectedSourceNeverRunsOut() {
		oil.DisappearanceRatio = 1;
		Assert.False(ResourceDepletion.MayRunOut(gameData, oilTile));

		for (int i = 0; i < 100; ++i) {
			ResourceDepletion.ProcessEndOfRound(gameData);
		}

		Assert.Equal(new[] { oilTile }, OilTiles());
		Assert.Empty(TradeNews());
	}

	[Fact]
	public void ARoadedSourceOutsideOurBordersNeverRunsOut() {
		Connect();
		oilTile.owningCity = null;
		oil.DisappearanceRatio = 1;

		ResourceDepletion.ProcessEndOfRound(gameData);

		Assert.Equal(new[] { oilTile }, OilTiles());
	}

	[Fact]
	public void ARatioOfZeroNeverRunsOut() {
		Connect();
		oil.DisappearanceRatio = 0;
		Assert.False(ResourceDepletion.MayRunOut(gameData, oilTile));

		for (int i = 0; i < 1000; ++i) {
			ResourceDepletion.ProcessEndOfRound(gameData);
		}

		Assert.Equal(new[] { oilTile }, OilTiles());
	}

	[Fact]
	public void ASourceWeCantYetSeeNeverRunsOut() {
		Connect();
		oil.DisappearanceRatio = 1;
		oil.Prerequisite = ID.FromString("tech-1");

		ResourceDepletion.ProcessEndOfRound(gameData);

		Assert.Equal(new[] { oilTile }, OilTiles());
	}

	[Fact]
	public void TheStandaloneRulesetHasCiv3sRatios() {
		using System.Text.Json.JsonDocument doc = EngineTests.Utils.JsonUtils.LoadBaseRuleset();
		Dictionary<string, int> ratios = new();
		foreach (System.Text.Json.JsonElement r in doc.RootElement.GetProperty("resources").EnumerateArray()) {
			ratios[r.GetProperty("key").GetString()] = r.GetProperty("disappearanceRatio").GetInt32();
		}
		// https://forums.civfanatics.com/threads/when-do-resources-deplete-tested.329362/
		Assert.Equal(0, ratios["Horses"]);
		Assert.Equal(800, ratios["Iron"]);
		Assert.Equal(800, ratios["Saltpeter"]);
		Assert.Equal(400, ratios["Coal"]);
		Assert.Equal(200, ratios["Oil"]);
		Assert.Equal(0, ratios["Rubber"]);
		Assert.Equal(400, ratios["Aluminum"]);
		Assert.Equal(100, ratios["Uranium"]);
	}
}

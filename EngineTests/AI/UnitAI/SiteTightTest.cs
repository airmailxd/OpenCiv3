using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData.Save;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.UnitAI {
	// The site-tight-12 and site-tight-7 switches: sites near our own cities
	// score higher when they have enough good food tiles of their own. Off,
	// sites are scored as ever.
	public sealed class SiteTightTest : MapBase {
		private static Tile MakeLand(int food) {
			return new Tile(ID.None("")) {
				baseTerrainType = new() { Key = "grassland" },
				overlayTerrainType = new() { Key = "grassland", movementCost = 1, baseFoodProduction = food, baseShieldProduction = 20 }
			};
		}

		// A 30x30 map of 1-food land with our one city at (10, 10) and a site
		// 4 tiles east of it; `good` of the site's 20 workable tiles, none of
		// them within 2 tiles of the city, make 2 food. Returns the site's
		// score under the switch (or none).
		private float Score(string switchName, int good) {
			C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty(), rules = new Rules() };
			EngineStorage.InitializeGameDataForTests(gameData);
			GameMap map = new() { numTilesWide = 30, numTilesTall = 30, tiles = new List<Tile>() };
			for (int i = 0; i < 30 * 30 / 2; ++i) {
				map.tileIndexToCoords(i, out int x, out int y);
				map.tiles.Add(MakeLand(1));
				map.tiles[i].XCoordinate = x;
				map.tiles[i].YCoordinate = y;
			}
			map.computeNeighbors();
			gameData.map = map;

			Tile home = map.tileAt(10, 10);
			Tile site = map.tileAt(18, 10);
			Assert.Equal(4, site.DistanceTo(home));
			foreach (Tile t in site.GetTilesWithinRankDistance(2).Where(t => t != site && t.DistanceTo(home) > 2).Take(good)) {
				t.overlayTerrainType.baseFoodProduction = 2;
			}

			Player player = MakeTestPlayer(map.tiles);
			player.civilization = new Civilization();
			player.rules = gameData.rules;
			player.rules.MaxRankOfWorkableTiles = 2;
			gameData.players.Add(player);
			player.cities.Add(new City(home, player, "Home", ID.None("")));
			if (switchName != null) {
				AiExperiments.Enable(player, switchName);
			}
			return SettlerLocationAI.GetScoredSettlerCandidates(home, player)[site];
		}

		[Fact]
		private void ASiteWithEnoughFoodTilesScoresHigherWithTheSwitch() {
			float off = Score(null, 7);
			Assert.Equal(off + SettlerLocationAI.TightSiteBonus, Score(SettlerLocationAI.SiteTight7, 7), 3);
		}

		[Fact]
		private void Tight12NeedsMoreFoodThanTight7() {
			// 11 good tiles meet 7 but not 12; 12 meet both.
			float off = Score(null, 11);
			Assert.Equal(off, Score(SettlerLocationAI.SiteTight12, 11), 3);
			Assert.Equal(off + SettlerLocationAI.TightSiteBonus, Score(SettlerLocationAI.SiteTight7, 11), 3);
			Assert.Equal(Score(null, 12) + SettlerLocationAI.TightSiteBonus, Score(SettlerLocationAI.SiteTight12, 12), 3);
		}

		[Theory]
		[InlineData(SettlerLocationAI.SiteTight7)]
		[InlineData(SettlerLocationAI.SiteTight12)]
		private void ASiteWithTooLittleFoodScoresAsEver(string name) {
			Assert.Equal(Score(null, 3), Score(name, 3), 3);
		}

		[Fact]
		private void TheTargetsAre7And12() {
			Player player = MakeTestPlayer(new List<Tile>());
			Assert.Equal(0, SettlerLocationAI.TightTarget(player));
			AiExperiments.Enable(player, SettlerLocationAI.SiteTight7);
			Assert.Equal(7, SettlerLocationAI.TightTarget(player));
			AiExperiments.Enable(player, SettlerLocationAI.SiteTight12);
			Assert.Equal(12, SettlerLocationAI.TightTarget(player));
		}

		[Fact]
		private void FoodTilesCountFullHalfOrNothing() {
			Tile site = MakeLand(2);
			Tile good = MakeLand(2);
			Tile shared = MakeLand(2);
			Tile coast = MakeCoastTile();
			coast.overlayTerrainType.baseFoodProduction = 1;
			Tile poor = MakeLand(1);
			Player player = MakeTestPlayer(new List<Tile>());
			SettlerLocationAI.BigFatCross cross = new() { tiles = new[] { good, coast, poor }, adjustments = new float[3] };
			// A good tile 1 and a coast 1/2; the poor tile counts for nothing.
			Assert.Equal(1.5f, SettlerLocationAI.GoodFoodTiles(site, player, cross, new List<Tile>(), null));
		}
	}
}

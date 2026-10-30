using System;
using C7Engine;
using C7Engine.AI;
using C7GameData.Save;
using C7GameData;
using EngineTests.Utils;
using System.Collections.Generic;
using Xunit;

namespace EngineTests.AI.UnitAI {
	// The site-food switch: city sites scored food-first. Off, sites are
	// scored as ever.
	public sealed class SiteFoodTest : MapBase {
		private static Tile MakeSite() {
			Tile tile = new(ID.None("")) {
				baseTerrainType = new() { Key = "grassland" },
				overlayTerrainType = new() { Key = "grassland", movementCost = 1, baseFoodProduction = 2, baseShieldProduction = 20 }
			};
			return tile;
		}

		// The scores of two sites alike but for `change`, which is made to
		// the one in the east: (changed, plain).
		private (float changed, float plain) Score(bool on, Action<Tile> change) {
			InitilizeStartTile(MakeSite(), new TileLocation(53, 50));
			Tile changed = startTile;
			List<Tile> map = SurroundTile(changed, MakeSite);
			change(changed);

			InitilizeStartTile(MakeSite(), new TileLocation(47, 50));
			Tile plain = startTile;
			map.AddRange(SurroundTile(plain, MakeSite));

			InitilizeStartTile(MakeDesertTile(), new TileLocation(50, 50));
			Tile start = startTile;
			startTile.map = gameMap;

			Player player = MakeTestPlayer(map);
			player.cities.Add(new City(start, player, "Home", ID.None("")));
			if (on) {
				AiExperiments.Enable(player, SettlerLocationAI.SiteFood);
			}
			Dictionary<Tile, float> scores = SettlerLocationAI.GetScoredSettlerCandidates(start, player);
			return (scores[changed], scores[plain]);
		}

		[Fact]
		private void ARiverSiteBeatsADrySiteOnlyWithTheSwitch() {
			(float river, float dry) = Score(true, t => t.riverNorth = true);
			Assert.True(river > dry, $"{river} <= {dry}");
			float onGap = river - dry;
			// A river also adds its own commerce, which counts either way.
			(river, dry) = Score(false, t => t.riverNorth = true);
			Assert.True(onGap > river - dry + SettlerLocationAI.FreshWaterBonus / 2, $"{onGap} vs {river - dry}");
		}

		[Fact]
		private void BonusFoodUnderTheCityCountsForNothingWithTheSwitch() {
			Action<Tile> bonus = t => t.overlayTerrainType.baseFoodProduction = 4;
			(float rich, float plain) = Score(false, bonus);
			Assert.True(rich > plain, $"{rich} <= {plain}");
			(rich, plain) = Score(true, bonus);
			Assert.Equal(plain, rich);
		}

		[Fact]
		private void SitesWithoutTwoFoodTilesAreMarkedDown() {
			Tile site = MakeSite();
			Player player = MakeTestPlayer(new List<Tile> { site });
			SettlerLocationAI.BigFatCross Cross(params int[] foods) {
				List<Tile> tiles = new();
				foreach (int food in foods) {
					Tile t = MakeSite();
					t.overlayTerrainType.baseFoodProduction = food;
					tiles.Add(t);
				}
				return new SettlerLocationAI.BigFatCross() { tiles = tiles.ToArray() };
			}
			float two = SettlerLocationAI.SiteFoodAdjustment(site, player, Cross(2, 3, 1));
			float one = SettlerLocationAI.SiteFoodAdjustment(site, player, Cross(2, 1, 1));
			float none = SettlerLocationAI.SiteFoodAdjustment(site, player, Cross(1, 0));
			Assert.Equal(0, two);
			Assert.Equal(SettlerLocationAI.MissingFoodTilePenalty, one);
			Assert.Equal(2 * SettlerLocationAI.MissingFoodTilePenalty, none);
		}
	}
}

using System;
using System.Collections.Generic;
using C7GameData;
using Godot;

namespace C7.Map {
	public partial class TileAssignmentLayer : LooseLayer {
		ImageTexture foodTexture = TextureLoader.Load("icons.map_food");
		ImageTexture shieldTexture = TextureLoader.Load("icons.map_shield");
		ImageTexture wastedShieldTexture = TextureLoader.Load("icons.wasted_shield");
		ImageTexture goldTexture = TextureLoader.Load("icons.map_commerce");

		private const int tileWidth = 128;
		private const int tileHeight = 64;

		// The widest a tile's row of yield icons gets before they overlap.
		private const float MaxYieldRowWidth = tileWidth;
		// Like Civ3, icons sit a pixel apart so each one can be counted.
		private const float YieldIconGap = 1;
		// Tiles outside the city's radius are darkened, as in Civ3.
		private static readonly Color OutsideRadiusDim = new(0, 0, 0, 0.4f);

		// When non-null, the city whose tile assignments should be shown.
		public City city {
			get => shownCity;
			set {
				if (shownCity != value) {
					shownCity = value;
					RequestRedraw();
				}
			}
		}
		private City shownCity = null;

		// What we know about the shown city's tiles, worked out again when the city or the map changes.
		private readonly HashSet<Tile> workableTiles = new();
		private readonly Dictionary<Tile, TileYields> yields = new();
		private City cachedCity = null;
		private int cachedContentVersion;

		private readonly struct TileYields {
			public readonly bool hasPollution;
			public readonly int food, foodPenalty, shields, shieldPenalty, gold, goldPenalty;

			public TileYields(Tile tile, City city) {
				hasPollution = tile.HasPollution();
				if (hasPollution) {
					food = foodPenalty = shields = shieldPenalty = gold = goldPenalty = 0;
					return;
				}
				Tile.Yield foodYield = tile.FoodYield(city);
				Tile.Yield shieldYield = tile.ProductionYield(city);
				Tile.Yield goldYield = tile.CommerceYield(city);
				food = foodYield.yield;
				foodPenalty = foodYield.penalty;
				shields = shieldYield.yield;
				shieldPenalty = shieldYield.penalty;
				gold = goldYield.yield;
				goldPenalty = goldYield.penalty;
			}
		}

		public override void onBeginDraw(LooseView looseView, GameData gameData) {
			if (city == null) {
				return;
			}

			int contentVersion = looseView.mapView.contentVersion;
			if (city == cachedCity && contentVersion == cachedContentVersion) {
				return;
			}
			cachedCity = city;
			cachedContentVersion = contentVersion;
			yields.Clear();

			workableTiles.Clear();
			foreach (Tile t in city.GetWorkableTiles()) {
				workableTiles.Add(t);
			}

			// Include the city center in the "workable" tiles to avoid having
			// a border drawn there.
			workableTiles.Add(city.location);
		}

		public override void onGameDataReplaced(GameData gameData) {
			cachedCity = null;
			workableTiles.Clear();
			yields.Clear();
		}

		public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
			City city = this.city;
			if (city == null) {
				return;
			}

			// Draw a nice border around our city's workable tiles.
			//
			// TODO: use the fog of war layer to highlight the BFC.
			if (workableTiles.Contains(tile)) {
				DrawWorkableTileBorder(looseView, tile, tileCenter);

				// If one of our workable tiles is worked by another city, draw
				// a rectangle around it.
				if (tile.personWorkingTile != null && tile.personWorkingTile.city != city) {
					DrawOccupiedTileSquare(looseView, tileCenter);
				}
			} else {
				DimTile(looseView, tileCenter);
				return;
			}

			// Only draw yields for our citizens.
			if (tile.personWorkingTile?.city != city && tile.cityAtTile != city) {
				return;
			}

			if (!yields.TryGetValue(tile, out TileYields tileYields)) {
				tileYields = new TileYields(tile, city);
				yields[tile] = tileYields;
			}

			if (tileYields.hasPollution) {
				looseView.DrawTexture(wastedShieldTexture,
					tileCenter + new Vector2(-wastedShieldTexture.GetSize().X / 2, -15));
				return;
			}

			// The yield in one row: food, then shields, then commerce. Yield lost
			// to a penalty (e.g. despotism) is struck through in a
			// second row beneath it.
			// Like Civ3, a long row squeezes its icons together to stay within the tile.
			List<ImageTexture> kept = new();
			AddIcons(kept, foodTexture, tileYields.food);
			AddIcons(kept, shieldTexture, tileYields.shields);
			AddIcons(kept, goldTexture, tileYields.gold);
			List<ImageTexture> lost = new();
			AddIcons(lost, foodTexture, tileYields.foodPenalty);
			AddIcons(lost, shieldTexture, tileYields.shieldPenalty);
			AddIcons(lost, goldTexture, tileYields.goldPenalty);

			if (lost.Count == 0) {
				DrawYieldRow(looseView, kept, tileCenter, Colors.White);
			} else {
				float rowHeight = foodTexture.GetHeight() + YieldIconGap;
				DrawYieldRow(looseView, kept, tileCenter - new Vector2(0, rowHeight / 2), Colors.White);
				Vector2 lostCenter = tileCenter + new Vector2(0, rowHeight / 2);
				float lostWidth = DrawYieldRow(looseView, lost, lostCenter, Colors.White);
				looseView.DrawLine(lostCenter - new Vector2(lostWidth / 2, 0), lostCenter + new Vector2(lostWidth / 2, 0),
					Colors.Red, width: 2);
			}
		}

		private static void AddIcons(List<ImageTexture> icons, ImageTexture texture, int count) {
			for (int i = 0; i < count; i++) {
				icons.Add(texture);
			}
		}

		// Draws a row of icons centered on rowCenter and returns its width.
		private static float DrawYieldRow(LooseView looseView, List<ImageTexture> icons, Vector2 rowCenter, Color tint) {
			if (icons.Count == 0) {
				return 0;
			}
			// The distance from the first icon's left edge to the last one's.
			float naturalSpan = 0;
			for (int i = 0; i < icons.Count - 1; i++) {
				naturalSpan += icons[i].GetWidth() + YieldIconGap;
			}
			float lastWidth = icons[^1].GetWidth();
			float squeeze = naturalSpan + lastWidth > MaxYieldRowWidth && naturalSpan > 0
				? Math.Max(0, MaxYieldRowWidth - lastWidth) / naturalSpan : 1f;
			float rowWidth = naturalSpan * squeeze + lastWidth;

			float x = -rowWidth / 2;
			foreach (ImageTexture texture in icons) {
				looseView.DrawTexture(texture, (rowCenter + new Vector2(x, -texture.GetHeight() / 2f)).Round(), tint);
				x += (texture.GetWidth() + YieldIconGap) * squeeze;
			}
			return rowWidth;
		}

		private void DimTile(LooseView looseView, Vector2 tileCenter) {
			looseView.DrawColoredPolygon([
				tileCenter + new Vector2(-tileWidth / 2, 0),
				tileCenter + new Vector2(0, -tileHeight / 2),
				tileCenter + new Vector2(tileWidth / 2, 0),
				tileCenter + new Vector2(0, tileHeight / 2),
			], OutsideRadiusDim);
		}

		private void DrawWorkableTileBorder(LooseView looseView, Tile tile, Vector2 tileCenter) {
			if (!workableTiles.Contains(tile.neighbors[TileDirection.NORTHWEST])) {
				looseView.DrawLine(tileCenter + new Vector2(-tileWidth / 2, 0),
									tileCenter + new Vector2(0, -tileHeight / 2),
									Colors.White, width: 2);
			}

			if (!workableTiles.Contains(tile.neighbors[TileDirection.NORTHEAST])) {
				looseView.DrawLine(tileCenter + new Vector2(tileWidth / 2, 0),
									tileCenter + new Vector2(0, -tileHeight / 2),
									Colors.White, width: 2);
			}

			if (!workableTiles.Contains(tile.neighbors[TileDirection.SOUTHWEST])) {
				looseView.DrawLine(tileCenter + new Vector2(-tileWidth / 2, 0),
									tileCenter + new Vector2(0, tileHeight / 2),
									Colors.White, width: 2);
			}

			if (!workableTiles.Contains(tile.neighbors[TileDirection.SOUTHEAST])) {
				looseView.DrawLine(tileCenter + new Vector2(tileWidth / 2, 0),
									tileCenter + new Vector2(0, tileHeight / 2),
									Colors.White, width: 2);
			}
		}

		private void DrawOccupiedTileSquare(LooseView looseView, Vector2 tileCenter) {
			// Thick brown outline.
			{
				int lineWidth = 4;
				int width = tileWidth - lineWidth * 4;
				int height = tileHeight - lineWidth * 2;

				Color brown = new Color(166.0f/256, 116.0f/256, 87.0f/256);
				DrawSquare(looseView, tileCenter, brown, lineWidth, width, height);
			}

			// Black borders of the brown outline.
			{
				int lineWidth = 1;
				int width = tileWidth - 4;
				int height = tileHeight - 2;

				DrawSquare(looseView, tileCenter, Colors.Black, lineWidth, width, height);
			}
			{
				int lineWidth = 1;
				int width = tileWidth - 20;
				int height = tileHeight - 10;

				DrawSquare(looseView, tileCenter, Colors.Black, lineWidth, width, height);
			}
		}

		private void DrawSquare(LooseView looseView, Vector2 tileCenter, Color color, int lineWidth, int tileWidth, int tileHeight) {
			looseView.DrawLine(tileCenter + new Vector2(-tileWidth / 2, 0),
								tileCenter + new Vector2(0, -tileHeight / 2),
								color, lineWidth);
			looseView.DrawLine(tileCenter + new Vector2(tileWidth / 2, 0),
								tileCenter + new Vector2(0, -tileHeight / 2),
								color, lineWidth);
			looseView.DrawLine(tileCenter + new Vector2(-tileWidth / 2, 0),
								tileCenter + new Vector2(0, tileHeight / 2),
								color, lineWidth);
			looseView.DrawLine(tileCenter + new Vector2(tileWidth / 2, 0),
								tileCenter + new Vector2(0, tileHeight / 2),
								color, lineWidth);
		}
	}
}

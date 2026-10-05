using System;
using System.Collections.Generic;
using C7GameData;
using Godot;

namespace C7.Map {
	public partial class TileAssignmentLayer : LooseLayer {
		ImageTexture foodTexture = TextureLoader.Load("icons.food");
		ImageTexture shieldTexture = TextureLoader.Load("icons.shield");
		ImageTexture wastedShieldTexture = TextureLoader.Load("icons.wasted_shield");
		ImageTexture goldTexture = TextureLoader.Load("icons.commerce");

		private const int tileWidth = 128;
		private const int tileHeight = 64;

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

			int totalWidth = ((tileYields.foodPenalty + tileYields.food) * foodTexture.GetWidth()) +
						((tileYields.shieldPenalty + tileYields.shields) * shieldTexture.GetWidth()) +
						((tileYields.goldPenalty + tileYields.gold) * goldTexture.GetWidth());
			int currentXOffset = -totalWidth / 2;

			for (int i = 0; i < tileYields.food; ++i) {
				looseView.DrawTexture(foodTexture, tileCenter + new Vector2(currentXOffset, -15));
				currentXOffset += foodTexture.GetWidth();
			}
			for (int i = 0; i < tileYields.foodPenalty; ++i) {
				looseView.DrawTexture(foodTexture, tileCenter + new Vector2(currentXOffset, -15));
				DrawX(looseView, foodTexture, tileCenter + new Vector2(currentXOffset, -15));
				currentXOffset += foodTexture.GetWidth();
			}

			for (int i = 0; i < tileYields.shields; ++i) {
				looseView.DrawTexture(shieldTexture, tileCenter + new Vector2(currentXOffset, -15));
				currentXOffset += shieldTexture.GetWidth();
			}
			for (int i = 0; i < tileYields.shieldPenalty; ++i) {
				looseView.DrawTexture(shieldTexture, tileCenter + new Vector2(currentXOffset, -15));
				// Make the X wider by passing in the gold texture.
				DrawX(looseView, goldTexture, tileCenter + new Vector2(currentXOffset, -15));
				currentXOffset += shieldTexture.GetWidth();
			}

			for (int i = 0; i < tileYields.gold; ++i) {
				looseView.DrawTexture(goldTexture, tileCenter + new Vector2(currentXOffset, -15));
				currentXOffset += goldTexture.GetWidth();
			}
			for (int i = 0; i < tileYields.goldPenalty; ++i) {
				looseView.DrawTexture(goldTexture, tileCenter + new Vector2(currentXOffset, -15));
				DrawX(looseView, goldTexture, tileCenter + new Vector2(currentXOffset, -15));
				currentXOffset += goldTexture.GetWidth();
			}
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

		private void DrawX(LooseView looseView, ImageTexture texture, Vector2 upperLeft) {
			upperLeft += new Vector2(0, 5);
			Vector2 upperRight = upperLeft + new Vector2(texture.GetWidth() - 5, 0);
			Vector2 lowerLeft = upperLeft + new Vector2(0, texture.GetHeight() - 10);
			Vector2 lowerRight = upperRight + new Vector2(0, texture.GetHeight() - 10);

			Color color = Colors.Red;
			int lineWidth = 2;
			looseView.DrawLine(upperLeft, lowerRight, color, lineWidth);
			looseView.DrawLine(lowerLeft, upperRight, color, lineWidth);


			looseView.DrawString(ThemeDB.FallbackFont,
								 upperLeft + new Vector2(-8, 0), "Desp",
								 HorizontalAlignment.Left, -1, 13, Colors.Black);
		}
	}
}

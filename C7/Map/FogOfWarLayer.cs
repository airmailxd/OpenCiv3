using System;
using C7GameData;
using Godot;

namespace C7.Map {
	public partial class FogOfWarLayer : LooseLayer {

		private readonly ImageTexture fogOfWarTexture;
		private readonly Vector2 tileSize;

		// What the player sees of each tile, by tile index, worked out once per draw: each tile is a corner of the fog of four tiles, and
		// looking up what the player knows of a tile is costly. See Status.
		private const byte Unseen = 0, Known = 1, InView = 2, NotWorkedOut = 255;
		private byte[] statuses = [];
		private GameMap statusMap;
		private TileKnowledge knowledge;
		private Tile target;

		public FogOfWarLayer() {
			fogOfWarTexture = TextureLoader.Load("terrain.fog_of_war");
			tileSize = fogOfWarTexture.GetSize() / 9;
		}

		public override void onBeginDraw(LooseView looseView, GameData gameData) {
			statusMap = gameData.map;
			if (statuses.Length != statusMap.tiles.Count) {
				statuses = new byte[statusMap.tiles.Count];
			}
			Array.Fill(statuses, NotWorkedOut);
			knowledge = looseView.tileKnowledge;
			target = looseView.mapView.game.tileInfo?.targetTile;
		}

		public override void onEndDraw(LooseView looseView, GameData gameData) {
			statusMap = null;
			knowledge = null;
			target = null;
		}

		// Whether the player doesn't know the tile, knows it, or has it in view, which includes the tile the tile info box is about.
		private byte Status(Tile tile) {
			if (tile == null || tile == Tile.NONE) {
				return Unseen;
			}
			int index = statusMap.tileCoordsToIndex(tile.XCoordinate, tile.YCoordinate);
			if (index < 0 || index >= statuses.Length || statusMap.tiles[index] != tile) {
				return WorkOutStatus(tile);
			}
			byte status = statuses[index];
			if (status == NotWorkedOut) {
				status = WorkOutStatus(tile);
				statuses[index] = status;
			}
			return status;
		}

		private byte WorkOutStatus(Tile tile) {
			if (!knowledge.isTileKnown(tile)) {
				return Unseen;
			}
			return knowledge.isActiveTile(tile) || tile == target ? InView : Known;
		}

		public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {

			// Fog of war is computed from the squares' intersections.
			// Thus, for each iteration, we have to consider that we are at the north intersection of the iterated tile

			Tile north = tile.neighbors[TileDirection.NORTH];
			Tile south = tile;
			Tile east = tile.neighbors[TileDirection.NORTHEAST];
			Tile west = tile.neighbors[TileDirection.NORTHWEST];

			// Unseen, known and in view count 0, 1 and 2 at the north and east corners, and three times that at the west and south ones.
			int column = Status(north) + 3 * Status(west);
			int row = Status(east) + 3 * Status(south);

			// save a few draw calls when the tile is completely visible
			if (column == 8 && row == 8) {
				return;
			}

			var fogOrigin = new Vector2(tileCenter.X - tileSize.X/2, tileCenter.Y - tileSize.Y);
			var fogRect = new Rect2(fogOrigin, tileSize);
			var spriteRect = new Rect2(column * tileSize.X, row * tileSize.Y, tileSize * 0.999f);
			looseView.DrawTextureRectRegion(fogOfWarTexture, fogRect, spriteRect);
		}
	}
}

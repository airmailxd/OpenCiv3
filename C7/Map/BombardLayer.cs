using System.Collections.Generic;
using C7GameData;
using Godot;

// The layer responsible for drawing the cursor and tile effects relating to bombardment.
public partial class BombardLayer : LooseLayer {
	private readonly ImageTexture bombardCursorTexture;
	private readonly ImageTexture bombardDenyCursorTexture;

	private TextureRect bombardCursorRect = null;
	private TextureRect bombardDenyCursorRect = null;

	private Color bombardRed = Color.Color8(200, 0, 0, 225);
	private float bombardGridLineWidth = (float)1.0;

	private Dictionary<(Tile tile, int range), List<Tile>> tileSquareCache = new();

	// The tiles the unit can bombard, worked out again only when the bombarding unit, its tile or range, or the map changes.
	private readonly HashSet<Tile> bombardTiles = new();
	private (BombardInfo info, Tile tile, int range, int contentVersion) bombardTilesKey;

	// The cursor last set for the current bombard, so it's only set when it changes.
	private BombardInfo cursorInfo = null;
	private ImageTexture cursorTexture = null;

	public BombardLayer() {
		bombardCursorTexture = TextureLoader.Load("ui.cursor.bombard");
		bombardDenyCursorTexture = TextureLoader.Load("ui.cursor.bombard_deny");
	}

	public void DrawBombardCursor() {
		Input.SetCustomMouseCursor(bombardCursorTexture, hotspot: bombardCursorTexture.Center());
	}
	public void DrawBombardDenyCursor() {
		Input.SetCustomMouseCursor(bombardDenyCursorTexture, hotspot: bombardDenyCursorTexture.Center());
	}

	private void SetCursor(BombardInfo bombardInfo, ImageTexture texture) {
		if (cursorInfo == bombardInfo && cursorTexture == texture) {
			return;
		}
		cursorInfo = bombardInfo;
		cursorTexture = texture;
		if (texture == bombardCursorTexture) {
			DrawBombardCursor();
		} else {
			DrawBombardDenyCursor();
		}
	}

	public override void onBeginDraw(LooseView looseView, GameData gameData) {
		bombardCursorRect?.Hide();
		bombardDenyCursorRect?.Hide();
	}

	public override void onGameDataReplaced(GameData gameData) {
		tileSquareCache.Clear();
		bombardTiles.Clear();
		bombardTilesKey = default;
	}

	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
		var bombardInfo = looseView.mapView.game.bombardInfo;
		if (bombardInfo == null || bombardInfo.bombardingUnit.location != tile)
			return;

		MapView mapView = looseView.mapView;
		var unit = bombardInfo.bombardingUnit;
		var range = unit.unitType.bombardRange;
		var reachableTiles = GetTileSquare(tile, range);

		var key = (bombardInfo, tile, range, looseView.mapView.contentVersion);
		if (key != bombardTilesKey) {
			bombardTilesKey = key;
			bombardTiles.Clear();
			foreach (Tile t in reachableTiles) {
				if (unit.CanBombardTile(t)) {
					bombardTiles.Add(t);
				}
			}
		}

		// Choose one of two cursors depending on mouse tile hover
		if (bombardInfo.mouseTile != null) {
			var bombardable = bombardTiles.Contains(bombardInfo.mouseTile);
			if (bombardable) {
				SetCursor(bombardInfo, bombardCursorTexture);
				drawTargetBombardTile(looseView, mapView.NearestTileCenter(bombardInfo.mouseTile, tileCenter));
			} else
				SetCursor(bombardInfo, bombardDenyCursorTexture);
		}

		// Draw bombard grid. The map may wrap around, so each tile is drawn at its copy nearest the copy of the unit's tile being drawn.
		foreach (var bt in reachableTiles) {
			drawBombardTile(looseView, mapView.NearestTileCenter(bt, tileCenter));
		}
	}

	private List<Tile> GetTileSquare(Tile tile, int range) {
		var key = (tile, range);
		if (tileSquareCache.TryGetValue(key, out var square))
			return square;
		square = tile.GetTilesWithinTileSquare(range);
		tileSquareCache[key] = square;
		return square;
	}

	private void drawBombardTile(LooseView looseView, Vector2 tileCenter) {
		var cS = MapView.cellSize;
		var left = tileCenter + new Vector2(-cS.X, 0);
		var top = tileCenter + new Vector2(0, -cS.Y);
		var right = tileCenter + new Vector2(cS.X, 0);
		var bottom = tileCenter + new Vector2(0, cS.Y);
		DrawSquare(looseView, left, top, right, bottom);
	}

	private void drawTargetBombardTile(LooseView looseView, Vector2 tileCenter) {
		var cS = MapView.cellSize;
		var inset = 10;
		var left = tileCenter + new Vector2(-cS.X + inset, 0);
		var top = tileCenter + new Vector2(0, -cS.Y + (inset/2f));
		var right = tileCenter + new Vector2(cS.X - inset, 0);
		var bottom = tileCenter + new Vector2(0, cS.Y - (inset/2f));
		DrawSquare(looseView, left, top, right, bottom);
	}

	private void DrawSquare(LooseView looseView, Vector2 left, Vector2 top, Vector2 right, Vector2 bottom) {
		looseView.DrawLine(left, top, bombardRed, bombardGridLineWidth);
		looseView.DrawLine(top, right, bombardRed, bombardGridLineWidth);
		looseView.DrawLine(left, bottom, bombardRed, bombardGridLineWidth);
		looseView.DrawLine(bottom, right, bombardRed, bombardGridLineWidth);
	}
}

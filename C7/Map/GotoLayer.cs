using System;
using C7GameData;
using Godot;
using static C7GameData.MapUnit;

// The layer responsible for drawing the cursor when the player is selecting a
// move via the "goto" command.
public partial class GotoLayer : LooseLayer {
	// The font we'll use for the goto move counter, loaded once.
	//
	// We skip the cache so that we can change the size without affecting other
	// code using the same font.
	//
	// We use FixedSize so Godot can calculate the width of the text for centering.
	private static FontFile gotoLabelFont;
	private static Theme whiteFontTheme;
	private static Theme redFontTheme;

	public GotoLayer() {
		if (gotoLabelFont != null) {
			return;
		}

		gotoLabelFont = ResourceLoader.Load<FontFile>("res://Fonts/NotoSans-Regular.ttf", null, ResourceLoader.CacheMode.Ignore);
		gotoLabelFont.FixedSize = 20;

		whiteFontTheme = new();
		whiteFontTheme.DefaultFont = gotoLabelFont;
		whiteFontTheme.SetColor("font_color", "Label", Colors.White);
		whiteFontTheme.SetFontSize("font_size", "Label", 20);

		redFontTheme = new();
		redFontTheme.DefaultFont = gotoLabelFont;
		redFontTheme.SetColor("font_color", "Label", Colors.Red);
		redFontTheme.SetFontSize("font_size", "Label", 20);
	}

	// The GoTo cursor and label fields.
	private AnimatedSprite2D gotoCursorSprite = null;
	private TextureRect staticCursorRect = null;
	private ImageTexture staticCursor = null;
	private Label gotoLabel = null;
	private string gotoLabelText = null;
	private Vector2 gotoLabelSize;

	public void DrawStaticGoToCursor(LooseView looseView, Vector2 position, int moves, bool attackingMove) {
		gotoCursorSprite?.Hide();
		gotoLabel?.Hide();
		staticCursorRect?.Hide();
		if (staticCursor == null) {
			staticCursor = (ImageTexture)TextureLoader.LoadAnimation("animations.cursor", "cursor").GetFrameTexture("cursor", 1);
			TextureRect tr = new() { Texture = staticCursor};
			staticCursorRect = tr;

			gotoLabel = new() {
				Theme = whiteFontTheme
			};

			looseView.AddChild(staticCursorRect);
			looseView.AddChild(gotoLabel);
		}

		staticCursorRect.Position = position - new Vector2(staticCursor.GetWidth(), staticCursor.GetHeight()) / 2;

		string text = moves > 0 || !attackingMove ? moves.ToString() : " ";
		if (text != gotoLabelText) {
			gotoLabelText = text;
			gotoLabel.Text = text;
			gotoLabelSize = gotoLabelFont.GetStringSize(text);
		}
		gotoLabel.Position = position - gotoLabelSize / 2;

		staticCursorRect.Show();
		gotoLabel.Show();
	}

	public override void onBeginDraw(LooseView looseView, GameData gameData) {
		// Hide the cursor if it has been initialized
		gotoCursorSprite?.Hide();
		staticCursorRect?.Hide();
		gotoLabel?.Hide();
	}

	private GotoInfo lastGotoInfo = null;
	private Intent lastintent = Intent.Disabled;

	// The path and the cursor don't belong to any one tile, so they're drawn once, in onEndDraw.
	public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) { }

	public override void onEndDraw(LooseView looseView, GameData gameData) {
		MapView mapView = looseView.mapView;
		MapUnit unit = mapView.game.CurrentlySelectedUnit;
		// When no unit is selected, at the end of a turn for example, we don't need to draw anything
		if (mapView.game.gotoInfo == null || !MapUnit.IsMapUnitValid(unit)) {
			return;
		}

		GotoInfo gotoInfo = mapView.game.gotoInfo;
		Tile unitOriginTile = unit.location;

		// The map may wrap around, so draw at the copies of tiles nearest the middle of the screen.
		Vector2 screenCenter = mapView.CameraCenterInMap();

		Tile destination = gotoInfo.destinationTile;
		if (Tile.IsTileValid(destination) && looseView.IsTileKnown(destination)) {
			DrawStaticGoToCursor(looseView, mapView.NearestTileCenter(destination, screenCenter), 0, true);
		}

		Intent intent = lastintent;

		if (gotoInfo == lastGotoInfo) {
			intent = lastintent;
		} else {
			unit.CanEnterForcefully(gotoInfo.destinationTile, out var outIntent);
			lastintent = intent = outIntent;
			lastGotoInfo = gotoInfo;
		}

		if (gotoInfo.path == null) return;

		if (gotoInfo.path.path.Count <= 1
								   && (intent == Intent.Fight
									   || intent == Intent.WarDeclaration
									   || intent == Intent.NoticeAlliance
									   || intent == Intent.NoticeCity
									   || intent == Intent.NoticeUnit))
			return;

		// Variable width of the line to account for various camera zoom levels.
		// The end result should look pretty much the same to the player on any zoom level.
		float lineWidth = Math.Max(1f / mapView.cameraZoom, 1f);

		// Each step goes to the copy of the next tile nearest the previous one, so the path stays connected across the edges of the map.
		Vector2 currentTileCenter = mapView.NearestTileCenter(unitOriginTile, screenCenter);
		bool drewPath = false;
		foreach (Tile nextTile in gotoInfo.path.path) {
			Vector2 nextTileCenter = mapView.NearestTileCenter(nextTile, currentTileCenter);
			looseView.DrawLine(currentTileCenter, nextTileCenter, Colors.Red, width: lineWidth);
			currentTileCenter = nextTileCenter;
			drewPath = true;
		}

		if (drewPath) {
			DrawStaticGoToCursor(looseView, currentTileCenter, gotoInfo.moveCost, gotoInfo.attackingMove);
		}
	}
}

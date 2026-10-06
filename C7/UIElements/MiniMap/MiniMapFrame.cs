using C7Engine;
using C7GameData;
using Godot;

public partial class MiniMapFrame : TextureRect {
	private TextureRect mapFrameRect;

	private MapView mapView;
	private TextureRect mapTextureRect;
	private ImageTexture mapTexture;
	private Vector2I mapTextureSize;
	private MiniMapBoundsOverlay boundsOverlay;
	private Vector2 lastViewportSize = new(-1, -1);

	private Vector2I miniMapFrameSize = new (280, 130);
	private Vector2I miniMapSize = new (229, 105);
	private Vector2I frameOffset = new (7, -12 + -10); // overall control has 10px boundary, adjust for VP
	private Vector2I mapOffset = new (25, -13); // offset inside the frame

	private bool isDragging;

	// The size the map is shown at inside the frame, in UI pixels.
	public Vector2I MapDisplaySize => miniMapSize;

	public MiniMapFrame(MapView mapView) {
		this.mapView = mapView;

		// TODO: Draw frame on top of the map texture (figure out stencil alpha)

		// Draw frame
		ImageTexture boxLeft = TextureLoader.Load("lower_left_infobox.box");
		mapFrameRect = new TextureRect();
		mapFrameRect.Texture = boxLeft;
		AddChild(mapFrameRect);

		// Draw the map inside the frame
		// The rect scales the map to its size. (A size override on the
		// texture would also do that, but ImageTexture.Update then rejects
		// every image as the wrong size, freezing the minimap.)
		mapTextureRect = new TextureRect();
		mapTextureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		mapTextureRect.StretchMode = TextureRect.StretchModeEnum.Scale;
		mapTextureRect.SetSize(miniMapSize);
		AddChild(mapTextureRect);

		// Draw the viewport bounds over the map
		boundsOverlay = new MiniMapBoundsOverlay();
		boundsOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
		mapTextureRect.AddChild(boundsOverlay);
	}

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Pass;
	}

	public void SetViewportPosition() {
		// Position frame and map relative to viewport
		var vp = GetViewportRect().Size;
		if (vp == lastViewportSize)
			return;
		lastViewportSize = vp;
		mapFrameRect.SetPosition(frameOffset + new Vector2(0, vp.Y - miniMapFrameSize.Y));
		mapTextureRect.SetPosition(frameOffset + new Vector2(0, vp.Y - miniMapSize.Y) + mapOffset);
	}

	public void RenderImage(Image mapImage) {
		// Reuse the texture, only replacing its contents, unless the map
		// size changed.
		Vector2I imageSize = mapImage.GetSize();
		if (mapTexture != null && imageSize == mapTextureSize) {
			mapTexture.Update(mapImage);
			return;
		}

		mapTextureSize = imageSize;
		mapTexture = ImageTexture.CreateFromImage(mapImage);

		mapTextureRect.Texture = mapTexture;
	}

	public void SetViewportBounds(GameMap map, MapView.VisibleRegion vr) {
		boundsOverlay.SetBounds(map, vr);
	}

	public override void _GuiInput(InputEvent @event) {
		if (@event is InputEventMouseButton eventMouseButton) {
			Control uiHover = GetViewport().GuiGetHoveredControl();
			isDragging = eventMouseButton.IsPressed();
			if (eventMouseButton.IsPressed() && uiHover is TextureRect) {
				switch (eventMouseButton.ButtonIndex) {
					case MouseButton.Left:
						HandleLeftMouseButton(eventMouseButton);
						break;
				}
			}
		} else if (@event is InputEventMouseMotion eventMouseMotion) {
			Control uiHover = GetViewport().GuiGetHoveredControl();
			if (isDragging && uiHover is TextureRect) {
				HandleMouseMotionInput(eventMouseMotion);
			}
		}
	}

	private void HandleMouseMotionInput(InputEventMouseMotion eventMouseMotion) {
		CenterToMousePosition();
	}

	private void HandleLeftMouseButton(InputEventMouseButton eventMouseButton) {
		CenterToMousePosition();
	}

	// The map image covers the map the way MapView lays it out, so the
	// mouse's position on the image, scaled to the map's size, is a map
	// location. A drag past the image's edge stays on the edge.
	private void CenterToMousePosition() {
		var relativeMapPos = mapTextureRect.GetLocalMousePosition() / mapTextureRect.Size;
		relativeMapPos = relativeMapPos.Clamp(Vector2.Zero, Vector2.One);
		CenterToPosition(relativeMapPos);
	}

	private void CenterToPosition(Vector2 relativeMapPos) {
		EngineStorage.ReadGameData((GameData gameData) => {
			if (mapView == null)
				return;

			// Centre on the tile drawn under the mouse, which along an edge
			// that doesn't wrap may be the one next to the location.
			var mapSize = new Vector2(gameData.map.numTilesWide, gameData.map.numTilesTall);
			var mapLocation = relativeMapPos * mapSize;
			var tile = MiniMap.TileShownAt(gameData.map, mapLocation.X, mapLocation.Y);
			if (tile != Tile.NONE)
				mapView.centerCameraOnTile(tile);
		});
	}
}

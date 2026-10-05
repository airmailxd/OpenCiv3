using System;
using System.Collections.Generic;
using C7GameData;
using Godot;

public partial class MiniMapControls : Control {
	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Stop;
	}
}

/// Draws the viewport bounds as a rectangle over the minimap texture. The
/// lines are worked out in minimap pixel space, as if drawn onto the map
/// image, then scaled up to the size the map is shown at, so the map image
/// itself never has to change when the camera moves.
public partial class MiniMapBoundsOverlay : Control {
	private readonly List<(int x0, int y0, int x1, int y1)> lines = new();
	private int imageWidth, imageHeight;
	private bool hasBounds;
	private int lastWidth, lastHeight;
	private MapView.VisibleRegion lastRegion;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetBounds(GameMap map, MapView.VisibleRegion vr) {
		int width = map.numTilesWide, height = map.numTilesTall / 2;
		if (hasBounds && width == lastWidth && height == lastHeight
			&& vr.upperLeftX == lastRegion.upperLeftX && vr.upperLeftY == lastRegion.upperLeftY
			&& vr.lowerRightX == lastRegion.lowerRightX && vr.lowerRightY == lastRegion.lowerRightY) {
			return;
		}
		hasBounds = true;
		lastWidth = width;
		lastHeight = height;
		lastRegion = vr;

		imageWidth = width;
		imageHeight = height;
		lines.Clear();
		DrawBounds(map, vr);
		QueueRedraw();
	}

	public override void _Draw() {
		if (imageWidth <= 0 || imageHeight <= 0)
			return;
		Vector2 scale = Size / new Vector2(imageWidth, imageHeight);
		foreach (var (x0, y0, x1, y1) in lines) {
			// Every line is horizontal or vertical, so it covers a block of
			// whole pixels; clip it to the image like Image.SetPixel would.
			int minX = Math.Max(Math.Min(x0, x1), 0), maxX = Math.Min(Math.Max(x0, x1), imageWidth - 1);
			int minY = Math.Max(Math.Min(y0, y1), 0), maxY = Math.Min(Math.Max(y0, y1), imageHeight - 1);
			if (minX > maxX || minY > maxY)
				continue;
			DrawRect(new Rect2(new Vector2(minX, minY) * scale, new Vector2(maxX - minX + 1, maxY - minY + 1) * scale), Colors.White);
		}
	}

	/// Work out the viewport bounds as lines in minimap pixel space
	private void DrawBounds(GameMap map, MapView.VisibleRegion vr) {
		// TODO: Handle draws over map edges, maybe with Mathf.Wrap

		// Bounds, in minimap draw space
		var maxWidth = map.numTilesWide - 1;
		var maxHeight = (map.numTilesTall / 2) - 1;
		var maxPan = 100; // how many screenfuls one can pan the map

		// Wrapped coordinates, working around modulo operator limitations
		var wax = (maxPan * maxWidth + vr.upperLeftX) % maxWidth;
		var way = (maxPan * maxHeight + (vr.upperLeftY / 2)) % maxHeight;
		var wbx = (maxPan * maxWidth + vr.lowerRightX) % maxWidth;
		var wby = (maxPan * maxHeight + (vr.lowerRightY / 2)) % maxHeight;

		// Out of bounds
		var isOoBx = wbx < wax; // X coordinates increase right
		var isOoBy = wby < way; // Y coordinates increase down
		var fullZoom = (vr.lowerRightY / 2 - vr.upperLeftY / 2) > maxHeight
					   || (vr.lowerRightX - vr.upperLeftX) > maxWidth;

		// TODO: make use of GameMap's wrapHorizontally, wrapVertically

		// Handle the four cases
		if (fullZoom)
			DrawBoundsRegular(0, 0, maxWidth, maxHeight);
		else if (isOoBx && isOoBy)
			DrawBoundsOverCorner(wax, way, wbx, wby, maxWidth, maxHeight);
		else if (isOoBx)
			DrawBoundsOverSideEdges(wax, way, wbx, wby, maxWidth, maxHeight);
		else if (isOoBy)
			DrawBoundsOverPoles(wax, way, wbx, wby, maxWidth, maxHeight);
		else
			DrawBoundsRegular(wax, way, wbx, wby);
	}

	private void DrawBoundsOverCorner(int wax, int way, int wbx, int wby, int maxWidth, int maxHeight) {
		// Top Left
		AddLine(0, wby, wbx, wby); // bottom
		AddLine(wbx, wby, wbx, 0); // right

		// Top Right
		AddLine(wax, 0, wax, wby); // left
		AddLine(wax, wby, maxWidth, wby); // bottom

		// Bottom Left 
		AddLine(0, way, wbx, way); // top
		AddLine(wbx, way, wbx, maxHeight); // right

		// Bottom Right
		AddLine(wax, maxHeight, wax, way); // left
		AddLine(wax, way, maxWidth, way); // top
	}

	private void DrawBoundsOverSideEdges(int wax, int way, int wbx, int wby, int maxWidth, int maxHeight) {
		// Left half
		AddLine(0, way, wbx, way); // top
		AddLine(wbx, way, wbx, wby); // right
		AddLine(0, wby, wbx, wby); // bottom

		// Right half
		AddLine(maxWidth, way, wax, way); // top
		AddLine(wax, way, wax, wby); // left
		AddLine(wax, wby, maxWidth, wby); // bottom		
	}

	private void DrawBoundsOverPoles(int wax, int way, int wbx, int wby, int maxWidth, int maxHeight) {
		// Top half
		AddLine(wax, 0, wax, wby); // left
		AddLine(wax, wby, wbx, wby); // bottom
		AddLine(wbx, wby, wbx, 0); // right

		// Bottom half
		AddLine(wax, maxHeight, wax, way); // left
		AddLine(wax, way, wbx, way); // top
		AddLine(wbx, way, wbx, maxHeight); // right
	}

	private void DrawBoundsRegular(int wax, int way, int wbx, int wby) {
		AddLine(wax, way, wax, wby); // left
		AddLine(wax, wby, wbx, wby); // bottom
		AddLine(wbx, wby, wbx, way); // right
		AddLine(wbx, way, wax, way); // top
	}

	private void AddLine(int x0, int y0, int x1, int y1) {
		lines.Add((x0, y0, x1, y1));
	}
}

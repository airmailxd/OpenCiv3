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
/// lines are worked out in map locations, the half-tile units the map image
/// covers (the tile at (X, Y) is centred on (X + 1, Y + 1)), then scaled up
/// to the size the map is shown at, so the map image itself never has to
/// change when the camera moves.
public partial class MiniMapBoundsOverlay : Control {
	private readonly List<(int x0, int y0, int x1, int y1)> lines = new();
	private int mapWidth, mapHeight;
	private bool wrapHorizontally, wrapVertically;
	private bool hasBounds;
	private MapView.VisibleRegion lastRegion;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetBounds(GameMap map, MapView.VisibleRegion vr) {
		int width = map.numTilesWide, height = map.numTilesTall;
		if (hasBounds && width == mapWidth && height == mapHeight
			&& map.wrapHorizontally == wrapHorizontally && map.wrapVertically == wrapVertically
			&& vr.upperLeftX == lastRegion.upperLeftX && vr.upperLeftY == lastRegion.upperLeftY
			&& vr.lowerRightX == lastRegion.lowerRightX && vr.lowerRightY == lastRegion.lowerRightY) {
			return;
		}
		hasBounds = true;
		mapWidth = width;
		mapHeight = height;
		wrapHorizontally = map.wrapHorizontally;
		wrapVertically = map.wrapVertically;
		lastRegion = vr;

		lines.Clear();
		DrawBounds(vr);
		QueueRedraw();
	}

	public override void _Draw() {
		if (mapWidth <= 0 || mapHeight <= 0)
			return;
		Vector2 scale = Size / new Vector2(mapWidth, mapHeight);
		foreach (var (x0, y0, x1, y1) in lines) {
			// Every line is horizontal or vertical, one pixel thick, and kept
			// inside the image so the ones along its edges still show.
			float left = Mathf.Clamp(Math.Min(x0, x1) * scale.X, 0, Size.X - 1);
			float right = Mathf.Clamp(Math.Max(x0, x1) * scale.X, 0, Size.X - 1);
			float top = Mathf.Clamp(Math.Min(y0, y1) * scale.Y, 0, Size.Y - 1);
			float bottom = Mathf.Clamp(Math.Max(y0, y1) * scale.Y, 0, Size.Y - 1);
			DrawRect(new Rect2(left, top, right - left + 1, bottom - top + 1), Colors.White);
		}
	}

	/// Work out the viewport bounds as lines in map locations
	private void DrawBounds(MapView.VisibleRegion vr) {
		// The region runs from the tile at its upper left to the one before
		// its lower right, so the rectangle joins those tiles' centres.
		int ax = vr.upperLeftX + 1, ay = vr.upperLeftY + 1;
		int bx = vr.lowerRightX, by = vr.lowerRightY;

		// The image's edges
		var maxWidth = mapWidth;
		var maxHeight = mapHeight;
		var maxPan = 100; // how many screenfuls one can pan the map

		// Along a direction the map doesn't wrap in, the view past the edge
		// shows nothing, so the rectangle stops at the edge rather than
		// coming back in on the other side.
		if (!wrapHorizontally) {
			ax = Math.Clamp(ax, 0, maxWidth);
			bx = Math.Clamp(bx, 0, maxWidth);
		}
		if (!wrapVertically) {
			ay = Math.Clamp(ay, 0, maxHeight);
			by = Math.Clamp(by, 0, maxHeight);
		}

		// Wrapped coordinates, working around modulo operator limitations.
		var wax = wrapHorizontally ? (maxPan * mapWidth + ax) % mapWidth : ax;
		var way = wrapVertically ? (maxPan * mapHeight + ay) % mapHeight : ay;
		var wbx = wrapHorizontally ? (maxPan * mapWidth + bx) % mapWidth : bx;
		var wby = wrapVertically ? (maxPan * mapHeight + by) % mapHeight : by;

		// A view at least as wide (or tall) as the map covers all of it.
		if (bx - ax >= maxWidth) {
			wax = 0;
			wbx = maxWidth;
		}
		if (by - ay >= maxHeight) {
			way = 0;
			wby = maxHeight;
		}

		// Out of bounds
		var isOoBx = wbx < wax; // X coordinates increase right
		var isOoBy = wby < way; // Y coordinates increase down

		// Handle the four cases
		if (isOoBx && isOoBy)
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

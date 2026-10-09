using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using C7GameData;
using Serilog;
using C7Engine;

[GlobalClass]
[Tool]
public partial class MiniMap : Control {
	private ILogger log = LogManager.ForContext<MiniMap>();

	private MapView mapView;

	private MiniMapFrame frame;
	private List<MiniMapLayer> layers;
	private FogOfWarMiniLayer fogLayer;
	private MiniMapControls controls;

	// The minimap pixels, drawn in managed code and only uploaded to the GPU
	// when they change. `uploadedPixels` holds what the texture shows.
	private byte[] pixels = Array.Empty<byte>();
	private byte[] uploadedPixels = Array.Empty<byte>();
	private Image mapImage;
	private Vector2I lastImageSize;

	// Each tile's colour (RGBA8, by tile index), and which tile each pixel
	// shows (-1 for none), worked out again only when the map or the image
	// size changes.
	private byte[] tileColors = Array.Empty<byte>();
	private int[] pixelTiles = Array.Empty<int>();
	private GameMap pixelTilesMap;
	private int pixelTilesMapWidth, pixelTilesMapHeight;

	// A cheap summary of the game state the minimap depends on; the map is
	// only redrawn when it changes. Terrain and border changes aren't in it,
	// so every RECHECK_INTERVAL seconds a hash of each tile's terrain, city
	// and owning city is checked, and the map redrawn if it changed. As a
	// last safety net for anything else, the map is also redrawn (but only
	// re-uploaded if a pixel actually changed) every FULL_RECHECK_INTERVAL
	// seconds. (An engine counter of map changes would make both checks
	// unnecessary.)
	private MapStamp lastStamp;
	private bool hasDrawn;
	private double timeSinceRedraw;
	private double timeSinceRecheck;
	private int lastTileHash;
	private const double RECHECK_INTERVAL = 1.0;
	private const double FULL_RECHECK_INTERVAL = 5.0;

	private struct MapStamp : IEquatable<MapStamp> {
		public GameData gameData;
		public GameMap map;
		public Player controller;
		public TileKnowledge knowledge;
		public int turn;
		public bool observerMode;
		public int knownTiles;
		public int borderTiles;
		public int cityCount;
		public int cityHash;

		public bool Equals(MapStamp o) {
			return ReferenceEquals(gameData, o.gameData) && ReferenceEquals(map, o.map)
				&& ReferenceEquals(controller, o.controller) && ReferenceEquals(knowledge, o.knowledge)
				&& turn == o.turn && observerMode == o.observerMode
				&& knownTiles == o.knownTiles && borderTiles == o.borderTiles
				&& cityCount == o.cityCount && cityHash == o.cityHash;
		}
	}

	public MiniMap(MapView mapView) {
		this.mapView = mapView;
	}

	public override void _Ready() {
		frame = new MiniMapFrame(mapView);
		AddChild(frame);

		fogLayer = new FogOfWarMiniLayer();
		layers = new List<MiniMapLayer>
		{
			new BaseLandMiniLayer(),
			new TerrainMiniLayer(),
			new PlayerColorMiniLayer(),
			fogLayer
		};

		controls = new MiniMapControls();
		AddChild(controls);

		MouseFilter = MouseFilterEnum.Pass;
	}

	// TODO: Enable/disable via INI
	// TODO: Dynamic colours
	// TODO: Configurable colours
	// TODO: Resizing minimap
	// TODO: Configurable size via INI (absolute/relative)

	public override void _Process(double delta) {
		frame.SetViewportPosition();

		GameData gD = EngineStorage.gameData;
		if (gD == null)
			return;
		var map = gD.map;

		MapStamp stamp = ComputeStamp(gD);
		timeSinceRedraw += delta;
		timeSinceRecheck += delta;
		Vector2I imageSize = ComputeImageSize();
		bool stampChanged = !hasDrawn || !stamp.Equals(lastStamp) || imageSize != lastImageSize;
		bool redraw = stampChanged || timeSinceRedraw >= FULL_RECHECK_INTERVAL;
		if (!redraw && timeSinceRecheck >= RECHECK_INTERVAL) {
			timeSinceRecheck = 0;
			redraw = ComputeTileHash(map) != lastTileHash;
		}
		if (redraw) {
			RedrawMap(gD, forceUpload: stampChanged);
			lastStamp = stamp;
			lastImageSize = imageSize;
			lastTileHash = ComputeTileHash(map);
			hasDrawn = true;
			timeSinceRedraw = 0;
			timeSinceRecheck = 0;
		}

		// The viewport bounds are drawn over the map, so moving the camera
		// never touches the map image.
		if (mapView != null) {
			var vr = mapView.getVisibleRegion();
			frame.SetViewportBounds(map, vr);
		}
	}

	// Whether every tile is shown: in observer mode, or while the map view reveals the whole map.
	private bool ShowsWholeMap(GameData gD) {
		return mapView?.ShowsWholeMap(gD) ?? gD.observerMode;
	}

	private MapStamp ComputeStamp(GameData gD) {
		Player controller = gD.GetUIControllerPlayer();
		TileKnowledge knowledge = controller?.tileKnowledge;
		int cityHash = 17;
		foreach (City city in gD.cities) {
			cityHash = HashCode.Combine(cityHash, city, city.owner);
		}
		return new MapStamp {
			gameData = gD,
			map = gD.map,
			controller = controller,
			knowledge = knowledge,
			turn = gD.turn,
			observerMode = ShowsWholeMap(gD),
			knownTiles = knowledge?.knownTiles.Count ?? 0,
			borderTiles = knowledge?.borderTiles.Count ?? 0,
			cityCount = gD.cities.Count,
			cityHash = cityHash,
		};
	}

	// What each tile shows on the minimap besides the fog: its terrain, and
	// whose it is. Objects are hashed by identity, which is cheap.
	private static int ComputeTileHash(GameMap map) {
		HashCode hash = new();
		foreach (Tile t in map.tiles) {
			hash.Add(RuntimeHelpers.GetHashCode(t.baseTerrainType));
			hash.Add(RuntimeHelpers.GetHashCode(t.overlayTerrainType));
			hash.Add(RuntimeHelpers.GetHashCode(t.owningCity));
			hash.Add(RuntimeHelpers.GetHashCode(t.cityAtTile));
		}
		return hash.ToHashCode();
	}

	// The size the map image is drawn at: the minimap's size on screen in
	// physical pixels, so it's shown 1:1 instead of being stretched.
	private Vector2I ComputeImageSize() {
		float scale = GetTree()?.Root?.ContentScaleFactor ?? 1f;
		Vector2 size = (Vector2)frame.MapDisplaySize * Mathf.Max(scale, 1f);
		return new Vector2I(Mathf.RoundToInt(size.X), Mathf.RoundToInt(size.Y));
	}

	private void RedrawMap(GameData gD, bool forceUpload) {
		var map = gD.map;
		int mapWidth = map.numTilesWide, mapHeight = map.numTilesTall;
		Vector2I imageSize = ComputeImageSize();
		int width = imageSize.X, height = imageSize.Y;
		if (mapWidth <= 0 || mapHeight <= 0 || width <= 0 || height <= 0)
			return;

		int size = width * height * 4;
		if (pixels.Length != size) {
			pixels = new byte[size];
			uploadedPixels = new byte[size];
			mapImage = null;
			forceUpload = true;
		}
		if (pixelTiles.Length != width * height || pixelTilesMap != map
			|| pixelTilesMapWidth != mapWidth || pixelTilesMapHeight != mapHeight) {
			ComputePixelTiles(map, width, height);
		}

		// Work out each tile's colour, layer at a time
		fogLayer.visible = !ShowsWholeMap(gD);
		foreach (var layer in layers)
			layer.Configure(gD);
		int tileBytes = map.tiles.Count * 4;
		if (tileColors.Length != tileBytes)
			tileColors = new byte[tileBytes];
		else
			Array.Clear(tileColors);
		for (int i = 0; i < map.tiles.Count; i++) {
			foreach (var layer in layers)
				layer.DrawTile(tileColors, i * 4, map.tiles[i]);
		}

		// Paint every pixel with the colour of the tile under it
		for (int p = 0; p < pixelTiles.Length; p++) {
			int t = pixelTiles[p];
			if (t < 0) {
				pixels[p * 4] = pixels[p * 4 + 1] = pixels[p * 4 + 2] = 0;
				pixels[p * 4 + 3] = 255;
			} else {
				Buffer.BlockCopy(tileColors, t * 4, pixels, p * 4, 4);
			}
		}

		DrawCities(gD, width, height);

		if (!forceUpload && pixels.AsSpan().SequenceEqual(uploadedPixels))
			return;

		Buffer.BlockCopy(pixels, 0, uploadedPixels, 0, size);
		if (mapImage == null) {
			mapImage = Image.CreateFromData(width, height, false, Image.Format.Rgba8, uploadedPixels);
		} else {
			mapImage.SetData(width, height, false, Image.Format.Rgba8, uploadedPixels);
		}

		// Render the image
		frame.RenderImage(mapImage);
	}

	// Works out which tile each pixel shows. The image covers the map the way
	// MapView lays it out: a map location (x, y) is in half tiles, so tile
	// (X, Y) is the diamond centred on (X + 1, Y + 1). Clicks on the minimap
	// map back through the same layout, so they land on the tile drawn there.
	private void ComputePixelTiles(GameMap map, int width, int height) {
		pixelTiles = new int[width * height];
		pixelTilesMap = map;
		pixelTilesMapWidth = map.numTilesWide;
		pixelTilesMapHeight = map.numTilesTall;
		for (int py = 0; py < height; py++) {
			float my = (py + 0.5f) / height * map.numTilesTall;
			for (int px = 0; px < width; px++) {
				float mx = (px + 0.5f) / width * map.numTilesWide;
				var (x, y) = TileCoordsShownAt(map, mx, my);
				pixelTiles[py * width + px] = map.isTileAt(x, y)
					? map.tileCoordsToIndex(map.wrapTileX(x), map.wrapTileY(y))
					: -1;
			}
		}
	}

	// The tile the minimap shows at a map location, in half tiles. Along a
	// map edge that doesn't wrap, the half diamonds past the edge show the
	// tile next to them, so the edge is straight. Returns Tile.NONE for a
	// location off the map.
	public static Tile TileShownAt(GameMap map, float mapX, float mapY) {
		var (x, y) = TileCoordsShownAt(map, mapX, mapY);
		return map.tileAt(x, y);
	}

	private static (int, int) TileCoordsShownAt(GameMap map, float mapX, float mapY) {
		var (x, y) = TileCoordsForMapLocation(mapX, mapY);
		if (!map.isTileAt(x, y)) {
			if (map.isTileAt(x + 1, y + 1)) { x++; y++; }
			else if (map.isTileAt(x - 1, y - 1)) { x--; y--; }
			else if (map.isTileAt(x + 1, y - 1)) { x++; y--; }
			else if (map.isTileAt(x - 1, y + 1)) { x--; y++; }
		}
		return (x, y);
	}

	// The same as MapView.tileCoordsForMapLocation, which needs a MapView.
	private static (int, int) TileCoordsForMapLocation(float mapX, float mapY) {
		int X = Mathf.FloorToInt(mapX), Y = Mathf.FloorToInt(mapY);
		float fracX = mapX - X, fracY = mapY - Y;
		bool evenColumn = X % 2 == 0, evenRow = Y % 2 == 0;
		if (evenColumn ^ evenRow) {
			if (fracY > fracX)
				X -= 1;
			else
				Y -= 1;
		} else if (fracY < 1 - fracX) {
			X -= 1;
			Y -= 1;
		}
		return (X, Y);
	}

	// Cities are drawn as white squares about a tile in size, on top of the
	// map, so even a small minimap shows them clearly.
	private void DrawCities(GameData gD, int width, int height) {
		var map = gD.map;
		bool showsWholeMap = ShowsWholeMap(gD);
		TileKnowledge knowledge = showsWholeMap ? null : gD.GetUIControllerPlayer()?.tileKnowledge;
		float scaleX = (float)width / map.numTilesWide, scaleY = (float)height / map.numTilesTall;
		int side = Math.Max(3, Mathf.RoundToInt(Mathf.Min(scaleX, scaleY) * 2));
		foreach (City city in gD.cities) {
			Tile tile = city.location;
			if (tile == null || tile == Tile.NONE)
				continue;
			if (!showsWholeMap && (knowledge == null || !knowledge.knownTiles.Contains(tile)))
				continue;
			int x0 = Mathf.RoundToInt((tile.XCoordinate + 1) * scaleX - side / 2f);
			int y0 = Mathf.RoundToInt((tile.YCoordinate + 1) * scaleY - side / 2f);
			for (int dy = 0; dy < side; dy++) {
				int y = y0 + dy;
				if (y < 0 || y >= height)
					continue;
				for (int dx = 0; dx < side; dx++) {
					int x = x0 + dx;
					if (map.wrapHorizontally)
						x = ((x % width) + width) % width;
					else if (x < 0 || x >= width)
						continue;
					int o = (y * width + x) * 4;
					pixels[o] = pixels[o + 1] = pixels[o + 2] = pixels[o + 3] = 255;
				}
			}
		}
	}
}

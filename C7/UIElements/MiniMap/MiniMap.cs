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
	private MiniMapControls controls;

	// The minimap pixels, drawn in managed code and only uploaded to the GPU
	// when they change. `uploadedPixels` holds what the texture shows.
	private byte[] pixels = Array.Empty<byte>();
	private byte[] uploadedPixels = Array.Empty<byte>();
	private Image mapImage;

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

		layers = new List<MiniMapLayer>
		{
			new BaseLandMiniLayer(),
			new TerrainMiniLayer(),
			new PlayerColorMiniLayer(),
			new CityMiniLayer(),
			new WaterMiniLayer(),
			new FogOfWarMiniLayer()
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
		bool stampChanged = !hasDrawn || !stamp.Equals(lastStamp);
		bool redraw = stampChanged || timeSinceRedraw >= FULL_RECHECK_INTERVAL;
		if (!redraw && timeSinceRecheck >= RECHECK_INTERVAL) {
			timeSinceRecheck = 0;
			redraw = ComputeTileHash(map) != lastTileHash;
		}
		if (redraw) {
			RedrawMap(gD, forceUpload: stampChanged);
			lastStamp = stamp;
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

	private static MapStamp ComputeStamp(GameData gD) {
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
			observerMode = gD.observerMode,
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

	private void RedrawMap(GameData gD, bool forceUpload) {
		var map = gD.map;
		int width = map.numTilesWide;
		int height = map.numTilesTall / 2;
		if (width <= 0 || height <= 0)
			return;

		int size = width * height * 4;
		if (pixels.Length != size) {
			pixels = new byte[size];
			uploadedPixels = new byte[size];
			mapImage = null;
			forceUpload = true;
		} else {
			Array.Clear(pixels);
		}

		// Configure layers
		foreach (var layer in layers)
			layer.Configure(gD);

		// Draw tiles as pixels, layer at a time
		foreach (var t in map.tiles) {
			var (x, y) = ComputeIsoCoordinates(t);
			if (x < 0 || x >= width || y < 0 || y >= height)
				continue;
			int offset = (y * width + x) * 4;
			foreach (var layer in layers)
				layer.DrawTile(pixels, offset, t);
		}

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

	private (int x, int y) ComputeIsoCoordinates(Tile tile) {
		// Isometric tile dimensions - wider than tall for rhombus shape
		var x = tile.XCoordinate;
		var y = tile.YCoordinate / 2;
		return (x, y);
	}
}

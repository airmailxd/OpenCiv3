using System.Collections.Generic;
using C7.Textures;
using C7GameData;
using Godot;

/// Draws the minimap into a managed RGBA8 pixel buffer. Each tile owns the
/// pixel at `offset` (4 bytes: r, g, b, a). Layers read back what the
/// previous layers wrote, the same way Image.GetPixel would.
public abstract class MiniMapLayer {
	public virtual void Configure(GameData gD) { }
	public virtual void DrawTile(byte[] pixels, int offset, Tile tile) { }

	public bool visible = true;

	// Stores a Color the way Godot's Image.SetPixel does for an RGBA8 image.
	protected static void SetPixel(byte[] pixels, int offset, Color color) {
		pixels[offset] = ToByte(color.R);
		pixels[offset + 1] = ToByte(color.G);
		pixels[offset + 2] = ToByte(color.B);
		pixels[offset + 3] = ToByte(color.A);
	}

	// Reads a Color the way Godot's Image.GetPixel does for an RGBA8 image.
	protected static Color GetPixel(byte[] pixels, int offset) {
		return new Color(pixels[offset] / 255f, pixels[offset + 1] / 255f, pixels[offset + 2] / 255f, pixels[offset + 3] / 255f);
	}

	private static byte ToByte(float channel) {
		return (byte)Mathf.Clamp(channel * 255.0f, 0, 255);
	}
}

public class BaseLandMiniLayer : MiniMapLayer {
	public override void DrawTile(byte[] pixels, int offset, Tile tile) {
		if (visible && tile.IsLand())
			SetPixel(pixels, offset, Colors.DarkSeaGreen);
	}
}

public class TerrainMiniLayer : MiniMapLayer {
	public static readonly Dictionary<string, Color> TerrainColorMap = new()
	{
		{ "desert",      Colors.DarkGray },
		{ "plains",      Colors.DarkKhaki },
		{ "grassland",   Colors.DarkSeaGreen },
		{ "tundra",      Colors.LightGray },
		{ "flood plain", Colors.LightBlue },
		{ "hills",       Colors.Silver },
		{ "mountains",   Colors.RosyBrown },
		{ "forest",      Colors.DarkSeaGreen },
		{ "jungle",      Colors.CadetBlue },
		{ "marsh",       Colors.LightSlateGray },
		{ "volcano",     Colors.DimGray },
		{ "coast",       Colors.LightSteelBlue },
		{ "sea",         Colors.SteelBlue },
		{ "ocean",       Colors.RoyalBlue },
	};

	public override void DrawTile(byte[] pixels, int offset, Tile tile) {
		if (visible && TerrainColorMap.TryGetValue(tile.overlayTerrainType.Key, out Color value))
			SetPixel(pixels, offset, value);
	}
}

public class PlayerColorMiniLayer : MiniMapLayer {
	/// Returns the fully saturated version of the colour.
	public static Color Intensify(Color colour) => Color.FromHsv(colour.H, 1, colour.V, colour.A);

	// Each player's intensified colour, worked out once per redraw instead
	// of once per owned tile.
	private readonly Dictionary<Player, Color> intenseColors = new();

	public override void Configure(GameData gD) {
		intenseColors.Clear();
	}

	public override void DrawTile(byte[] pixels, int offset, Tile tile) {
		if (!visible)
			return;
		Player owner = tile.OwningPlayer();
		if (owner == null)
			return;
		if (!intenseColors.TryGetValue(owner, out Color intenseCivColor)) {
			intenseCivColor = Intensify(TextureLoader.LoadColor(owner.GetPlayerColor()));
			intenseColors[owner] = intenseCivColor;
		}
		var currentColor = GetPixel(pixels, offset);
		var newColor = intenseCivColor.Lerp(currentColor, 0.25f); // blend civ color with underlying map
		SetPixel(pixels, offset, newColor);
	}
}

public class CityMiniLayer : MiniMapLayer {
	public override void DrawTile(byte[] pixels, int offset, Tile tile) {
		if (visible && tile.HasCity())
			SetPixel(pixels, offset, Colors.White);
	}
}

public class WaterMiniLayer : MiniMapLayer {
	public override void DrawTile(byte[] pixels, int offset, Tile tile) {
		if (visible && tile.IsWater())
			SetPixel(pixels, offset, Colors.SteelBlue);
	}
}

public class FogOfWarMiniLayer : MiniMapLayer {
	private bool _observerMode;
	private TileKnowledge _tileKnowledge;

	public override void Configure(GameData gD) {
		_observerMode = gD.observerMode;
		_tileKnowledge = gD.GetUIControllerPlayer().tileKnowledge;
	}

	public override void DrawTile(byte[] pixels, int offset, Tile tile) {
		if (visible && !_observerMode) {
			if (_tileKnowledge.borderTiles.Contains(tile)) {
				var currentColor = GetPixel(pixels, offset);
				var newColor = Colors.Black.Lerp(currentColor, 0.50f); // blend with underlying map
				SetPixel(pixels, offset, newColor);
			} else if (!_tileKnowledge.knownTiles.Contains(tile)) {
				SetPixel(pixels, offset, Colors.Black);
			}
		}
	}
}

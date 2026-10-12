using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using ConvertCiv3Media;
using Godot;

// The "Graphics Overhaul": an optional set of remade map art (terrain, rivers,
// roads, mines, cities, resources and units), off by default and turned on in
// the settings.
//
// The remade art lives in the ModernArt folder, laid out like the game's own
// media: the remake of Art/Terrain/xtgc.pcx is ModernArt/Art/Terrain/xtgc.png.
// It's drawn at twice the resolution of the original, in the same layout, so
// each sprite sits exactly where the original's did. A remade texture reports
// the original's size, so the code that draws it (and the sprite sheet regions
// it draws from) work the same either way. Art with no remake falls back to
// the original.
//
// A unit animation's remake (for example of Art/Units/Warrior/warriorRun.flc)
// is a pair of sheets, warriorRun.png and warriorRun_tint.png, with a row per
// direction in the flic's order and a column per frame, each cell twice the
// size of the flic's frames. The tint sheet holds the parts colored with the
// civ color, shaded in white. A sheet with a single column is a still, which
// stands in for every frame.
//
// The ModernArt folder holds what's shown with animations off (units standing
// or fortified, as stills, and workers at work). The rest of the unit
// animations are big, so they're kept out of git in a ModernArtAnimations
// folder laid out the same way, which is used when it's there.
//
// In standalone mode there's no Civ3 art at all, so units are always drawn
// with the remade art, whatever the setting: everything under Art/Units comes
// from here, and a unit's .INI and the headers of its flics are replaced by
// the metadata saved with its sheets (UnitArtMetadata, <art>/<art>.json),
// and units_32.pcx by a thumbnail per unit art (<art>/<art>_32.png).
//
// The art is made by the scripts in the ArtGen folder at the root of the repo.
public static class ModernGraphics {
	private static readonly Serilog.ILogger log = Serilog.Log.ForContext(typeof(ModernGraphics));

	public const string SettingsSection = "graphics";
	public const string SettingsKey = "modernGraphics";

	// How many times the resolution of the original art the remade art has.
	public const int Scale = 2;

	private static bool? enabled;

	public static bool Enabled {
		get {
			enabled ??= C7Settings.GetSettingsValueOrDefault(SettingsSection, SettingsKey, "true") == "true";
			return enabled.Value;
		}
	}

	// Saves the setting and forgets every texture loaded so far, so the art is
	// loaded again in the chosen style.
	public static void SetEnabled(bool value) {
		if (value == Enabled) {
			return;
		}
		enabled = value;
		C7Settings.SetValue(SettingsSection, SettingsKey, value ? "true" : "false");
		C7Settings.SaveSettings();
		Util.ClearCaches();
	}

	// The filter for sprites drawn by their own nodes (units and cities). The
	// remade art has twice the pixels it's drawn with, so it's blended down
	// smoothly rather than by picking every other pixel. (The terrain is drawn
	// unfiltered, as blending would show seams between the tiles.)
	public static CanvasItem.TextureFilterEnum SpriteFilter =>
		Enabled ? CanvasItem.TextureFilterEnum.Linear : CanvasItem.TextureFilterEnum.ParentNode;

	// The filter for unit sprites, which in standalone mode are always the
	// remade art.
	public static CanvasItem.TextureFilterEnum UnitSpriteFilter =>
		Enabled || C7Settings.UseStandaloneMode() ? CanvasItem.TextureFilterEnum.Linear : CanvasItem.TextureFilterEnum.ParentNode;

	private const string UnitsFolder = "Art/Units/";

	// Whether the remade art is used for a media path: with the overhaul on,
	// or in standalone mode for unit art, which has nothing else to fall back on.
	private static bool UsedFor(string mediaPath) {
		if (Enabled) {
			return true;
		}
		return mediaPath.Replace('\\', '/').StartsWith(UnitsFolder, StringComparison.OrdinalIgnoreCase) && C7Settings.UseStandaloneMode();
	}

	private static string Root => FolderPath("ModernArt");
	private static string AnimationsRoot => FolderPath("ModernArtAnimations");

	private static string FolderPath(string name) => OS.HasFeature("editor")
		? "res://" + name
		: System.IO.Path.Combine(GamePaths.BaseDir, name);

	// The remade images by the media path they replace, null for art that
	// hasn't been remade.
	private static readonly Dictionary<string, Image> images = [];

	// The remade image for the art at a media path (such as
	// "Art/Terrain/xtgc.pcx"), or null if the overhaul is off or there's none.
	private static Image FindImage(string mediaPath, string suffix = "") {
		if (mediaPath == null || !UsedFor(mediaPath)) {
			return null;
		}
		string key = mediaPath + suffix;
		if (!images.TryGetValue(key, out Image image)) {
			string relPath = System.IO.Path.ChangeExtension(mediaPath, null) + suffix + ".png";
			string fullPath = Util.FileExistsIgnoringCase(AnimationsRoot, relPath)
				?? Util.FileExistsIgnoringCase(Root, relPath);
			image = fullPath != null ? Image.LoadFromFile(fullPath) : null;
			if (image != null && image.GetFormat() != Image.Format.Rgba8) {
				image.Convert(Image.Format.Rgba8);
			}
			images[key] = image;
		}
		return image;
	}

	// The remade texture for the art at a media path, cropped like the
	// original would be, or null if there's none.
	public static ImageTexture Load(string mediaPath, CropRegion? cropRegion) {
		Image image = FindImage(mediaPath);
		if (image == null) {
			return null;
		}
		Vector2I size;
		if (cropRegion is CropRegion crop) {
			size = new(crop.CroppedWidth, crop.CroppedHeight);
			image = image.GetRegion(new Rect2I(crop.LeftStart * Scale, crop.TopStart * Scale, size.X * Scale, size.Y * Scale));
		} else {
			size = new(image.GetWidth() / Scale, image.GetHeight() / Scale);
		}
		ImageTexture texture = ImageTexture.CreateFromImage(image);
		texture.SetSizeOverride(size);
		return texture;
	}

	// The remade base and tint textures of a frame of a unit animation, or
	// nulls if the animation hasn't been remade.
	public static (ImageTexture, ImageTexture) LoadFlicFrame(string flicPath, Flic flic, int row, int col) {
		return LoadFlicFrame(flicPath, flic.Width, flic.Height, flic.Images.GetLength(1), row, col);
	}

	// The same for a flic described by its frame size and number of frames
	// per direction rather than read from its file. A sheet may have fewer
	// columns than the flic has frames: a still has one, which stands for
	// every frame, and a move cycle has every other frame (see
	// UnitArtMetadata.SheetColumn).
	public static (ImageTexture, ImageTexture) LoadFlicFrame(string flicPath, int width, int height, int frames, int row, int col) {
		Image baseSheet = FindImage(flicPath);
		if (baseSheet == null) {
			return (null, null);
		}
		col = UnitArtMetadata.SheetColumn(col, frames, baseSheet.GetWidth() / (width * Scale));
		Rect2I cell = new(col * width * Scale, row * height * Scale, width * Scale, height * Scale);
		if (cell.End.X > baseSheet.GetWidth() || cell.End.Y > baseSheet.GetHeight()) {
			return (null, null);
		}
		Vector2I size = new(width, height);
		ImageTexture baseTexture = ImageTexture.CreateFromImage(baseSheet.GetRegion(cell));
		baseTexture.SetSizeOverride(size);

		// A unit with nothing in its civ's color has no tint sheet.
		Image tintSheet = FindImage(flicPath, "_tint");
		Image tint = tintSheet != null && cell.End.X <= tintSheet.GetWidth() && cell.End.Y <= tintSheet.GetHeight()
			? tintSheet.GetRegion(cell)
			: Image.CreateEmpty(cell.Size.X, cell.Size.Y, false, Image.Format.Rgba8);
		ImageTexture tintTexture = ImageTexture.CreateFromImage(tint);
		tintTexture.SetSizeOverride(size);
		return (baseTexture, tintTexture);
	}

	// Whether there's a remade sheet for a flic.
	public static bool HasFlicSheet(string flicPath) {
		return FindImage(flicPath) != null;
	}

	// The metadata of our own art for a unit art name (the art's folder), by
	// name ignoring case, null for those that couldn't be loaded.
	private static readonly Dictionary<string, UnitArtMetadata> unitMetadata = new(StringComparer.OrdinalIgnoreCase);

	// The metadata saved with our own art of a unit, which describes its
	// animations in place of Civ3's INI and flics, or null if there's none.
	public static UnitArtMetadata LoadUnitMetadata(string artName) {
		if (string.IsNullOrEmpty(artName)) {
			return null;
		}
		if (!unitMetadata.TryGetValue(artName, out UnitArtMetadata metadata)) {
			string path = Util.FileExistsIgnoringCase(Root, $"{UnitsFolder}{artName}/{artName}.json");
			if (path != null) {
				try {
					metadata = UnitArtMetadata.Load(path);
				} catch (Exception e) {
					log.Warning(e, "Couldn't read the unit art metadata {Path}", path);
				}
			}
			unitMetadata[artName] = metadata;
		}
		return metadata;
	}

	public static void ClearCache() {
		images.Clear();
		unitMetadata.Clear();
	}
}

using System.Collections.Generic;
using C7Engine;
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
// The art is made by the scripts in the ArtGen folder at the root of the repo.
public static class ModernGraphics {
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
		if (!Enabled || mediaPath == null) {
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
		Image baseSheet = FindImage(flicPath);
		if (baseSheet == null) {
			return (null, null);
		}
		if (baseSheet.GetWidth() == flic.Width * Scale) {
			col = 0;
		}
		Rect2I cell = new(col * flic.Width * Scale, row * flic.Height * Scale, flic.Width * Scale, flic.Height * Scale);
		if (cell.End.X > baseSheet.GetWidth() || cell.End.Y > baseSheet.GetHeight()) {
			return (null, null);
		}
		Vector2I size = new(flic.Width, flic.Height);
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

	public static void ClearCache() {
		images.Clear();
	}
}

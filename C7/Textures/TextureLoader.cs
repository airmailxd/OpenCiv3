using System;
using Godot;
using ConvertCiv3Media;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MoonSharp.Interpreter;
using Script = MoonSharp.Interpreter.Script;
using C7.Map;
using C7Engine.Lua;

public readonly record struct CropRegion(int LeftStart, int TopStart, int CroppedWidth, int CroppedHeight);

/// This class provides methods for loading PCX and PNG textures based on the metadata set up in the Lua configuration file.
///
/// Each Lua configuration file returns a table representing a tree.
/// Any leaf node in this structure can be one of the following:
/// 1) A string representing a path to the texture
///
/// 2) A table with following keys:
///    - Required: "path" (string) - Path to the texture file
///    - Optional: "crop_region" (table) - Sequential table matching the CropRegion record
///
///    For PCX files:
///    - Optional: "shadows" (boolean) - Whether the shadow effect should be simulated (defaults to true)
///    - Optional: "alpha" (string) - Path to an alpha channel texture file
///    - Optional: "alpha_row_offset" (number) - Row offset for alpha blending
///    - Optional: "transparent_color_indexes" (table) - List of color indexes to treat as transparent
///    - Optional: "pure_alpha" - The pcx file only contains transparency information.
///
///    For c7 files:
///    - Optional: "hex_color" (string) - A 6 character hex string for a civ color
///
///    For animations:
///    - Optional: "frame_duration" (float) - duration of each frame
///    - Optional: "animation_rows" (int) - number of rows in an animation sprite sheet. Must be >0 for png animations.
///    - Optional: "animation_cols" (int) - number of columns in an animation sprite sheet. Must be >0 for png animations.
///         - If a png animation is specified, the frame ordering is
///           (row 0, col 0), (row 0,col 1), ... (row 0, col animation_cols-1),
///           (row 1, col 0), ...
///           ...
///           (row animation_rows-1, col 0), ...
///
/// 3) A table containing key "map_object_to_sprite" holding a function that accepts a table it belongs to and a C# object.
/// This function should return a table similar to the one described in the point 2.
public static class TextureLoader {
	private struct ConfigEntry {
		public string Path;
		public CropRegion? CropRegion = null;

		// PCX-specific settings
		public string AlphaPath = null;
		public int AlphaRowOffset = 0;
		public bool PureAlpha = false;
		public PCXToGodot.ColorOptions ColorOptions = PCXToGodot.ColorOptions.Default;
		// Whether the config set "shadows" itself, rather than using the default.
		public bool ShadowsSpecified = false;

		// For civ-colors, a modern replacement for the 1x1 px pcx images.
		public string HexColor = null;

		// Animation-specific settings
		public float FrameDuration = 0.5f;
		public int AnimationRows = 0;
		public int AnimationCols = 0;

		public ConfigEntry() {
		}

		public readonly bool UseAlpha => AlphaPath != null;
	}

	private static Script lua;
	private static Table textureConfig;

	// What a texture was loaded from and how, which is what textures are cached by.
	private enum TextureKind { Png, Pcx, PcxWithAlphaBlend, PcxPureAlpha }

	private readonly record struct TextureCacheKey(
		TextureKind Kind,
		string Path,
		CropRegion? CropRegion,
		bool Shadows,
		IndexSet TransparentColorIndexes,
		string AlphaPath,
		int AlphaRowOffset
	);

	// A set of palette indexes (0 to 255), as a value that can be compared and
	// used as a key. Indexes outside of the palette are ignored, as they are
	// when loading textures.
	private readonly record struct IndexSet(ulong Bits0, ulong Bits1, ulong Bits2, ulong Bits3) {
		public static IndexSet From(IEnumerable<int> indexes) {
			Span<ulong> bits = stackalloc ulong[4];
			foreach (int index in indexes) {
				if (index >= 0 && index < 256)
					bits[index >> 6] |= 1UL << (index & 63);
			}
			return new IndexSet(bits[0], bits[1], bits[2], bits[3]);
		}

		public HashSet<int> ToHashSet() {
			HashSet<int> result = [];
			for (int index = 0; index < 256; index++) {
				ulong bits = (index >> 6) switch { 0 => Bits0, 1 => Bits1, 2 => Bits2, _ => Bits3 };
				if ((bits & (1UL << (index & 63))) != 0)
					result.Add(index);
			}
			return result;
		}
	}

	private static Dictionary<TextureCacheKey, ImageTexture> textureCache = [];
	private static Util.RecentlyUsedCache<Pcx> PcxCache = new(8);
	private static Dictionary<string, Image> PngCache = [];

	// Civ colors by civ index, and the same colors at full saturation.
	private static Color?[] colorCache = [];
	private static Color?[] intensifiedColorCache = [];
	private const int MaxCachedCivColorIndex = 1024;

	private static Dictionary<string, ImageTexture> configKeyCache = [];
	private static Dictionary<(string configKey, object obj), ImageTexture> objectMappingCache = [];
	private static Dictionary<(string configKey, string animationName), SpriteFrames> animationCache = [];

	// Textures for objects of value types whose fields are all values (see
	// IsPureValueType). Such an object can't change, and the Lua mapping
	// functions only look at the object they're given, so the texture for it
	// is always the same.
	private static Dictionary<(string configKey, object obj), ImageTexture> pureValueMappingCache = [];
	private static Dictionary<Type, bool> pureValueTypes = [];

	// The config entries by config key (null if there's none), and the parsed
	// config entries by the Lua table they were parsed from. Only tables that
	// are part of the config are cached, not ones made by mapping functions.
	private static Dictionary<string, object> entryByPathCache = [];
	private static Dictionary<Table, ConfigEntry> parsedConfigCache = new(ReferenceEqualityComparer.Instance);

	// The sets of transparent color indexes from configs, so that each set is
	// only allocated once.
	private static Dictionary<IndexSet, HashSet<int>> transparentIndexSets = [];
	private static readonly IndexSet DefaultTransparentColorIndexes = IndexSet.From(PCXToGodot.ColorOptions.Default.transparentColorIndexes);

	// The color options of an uncropped PCX texture that doesn't specify any:
	// unlike cropped ones, uncropped textures don't simulate shadows by default.
	private static readonly PCXToGodot.ColorOptions UncroppedDefaultColorOptions = new(false);

	static TextureLoader() {
		// Initialize the TextureLoader when running in the editor
		// In game it is done by GlobalSingleton, but it's not accessible in the editor
		if (Engine.IsEditorHint()) {
			GameMode gameMode = GameMode.Load(GamePaths.GameModesDir, GamePaths.basic);

			var (script, textureConfig) = gameMode.textures;
			TextureLoader.SetConfig(script, textureConfig);
		}
	}

	public static void SetConfig(Script lua, Table textureConfig) {
		ClearCache();

		TextureLoader.lua = lua;
		TextureLoader.textureConfig = textureConfig;
	}

	/// Returns a texture based on the config key.
	/// The config key should be a string separated by dots, representing the path through the
	/// configuration hierarchy (e.g., "icons.plus").
	public static ImageTexture Load(string configKey) {
		if (configKeyCache.TryGetValue(configKey, out ImageTexture cachedTexture))
			return cachedTexture;

		object entry = GetEntryByPath(configKey);
		if (entry == null)
			throw new Exception($"Texture config not found for key: {configKey}");

		ImageTexture texture = LoadFromLuaObject(entry);

		configKeyCache[configKey] = texture;

		return texture;
	}

	/// Returns a texture based on the config key and a C# object.
	/// This overload uses the "map_object_to_sprite" function in the
	/// config entry to dynamically determine which texture to load
	/// based on the provided object's properties.
	///
	/// This method optionally allows to cache the resulting texture,
	/// using (configKey, obj) as key.  Note, that caching shouldn't
	/// be used for objects whose texture-affecting properties can
	/// change.
	///
	/// Note that the type of the object passed to the method should
	/// be registered as Moonsharp userdata.
	public static ImageTexture Load(string configKey, object obj, bool useCache = false) {
		var cacheKey = (configKey, obj);

		if (useCache && objectMappingCache.TryGetValue(cacheKey, out ImageTexture cachedTexture))
			return cachedTexture;

		bool pureValue = !useCache && obj != null && IsPureValueType(obj.GetType());
		if (pureValue && pureValueMappingCache.TryGetValue(cacheKey, out cachedTexture))
			return cachedTexture;

		object entry = GetEntryByPath(configKey);
		if (entry is not Table table)
			throw new Exception($"Table expected for key: {configKey}");

		if (table["map_object_to_sprite"] is not Closure func)
			throw new Exception("Custom mapping function expected");

		object result = lua.SafeCall(func, table, DynValue.FromObject(lua, obj)).ToObject();

		// The result is a new table on every call, so its parsed form isn't cached.
		ImageTexture texture = LoadFromConfigEntry(ParseConfigEntry(result));

		if (useCache)
			objectMappingCache[cacheKey] = texture;
		else if (pureValue)
			pureValueMappingCache[cacheKey] = texture;

		return texture;
	}

	// Whether objects of a type are values that can't change and can only be
	// equal to objects with the same contents: value types whose fields are
	// all such values, primitives, enums or strings. For example a record
	// struct of enums and numbers, but not one holding a game object.
	private static bool IsPureValueType(Type type) {
		if (!pureValueTypes.TryGetValue(type, out bool pure)) {
			pure = type.IsValueType && IsPureValueField(type, 0);
			pureValueTypes[type] = pure;
		}
		return pure;
	}

	private static bool IsPureValueField(Type type, int depth) {
		if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
			return true;
		if (!type.IsValueType || depth > 8)
			return false;
		foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
			if (!IsPureValueField(field.FieldType, depth + 1))
				return false;
		}
		return true;
	}

	// Allows to load the texture directly by its file path, bypassing the Lua config.
	// Supports both PCX and PNG textures
	public static ImageTexture LoadByPath(string path) {
		string ext = Path.GetExtension(path).ToLowerInvariant();

		return ext switch {
			".png" => LoadFromPNG(path),
			".pcx" => LoadFromPCX(path),
			_ => throw new FormatException($"Unknown texture format: {path}"),
		};
	}

	/// Returns the list of textures making up an animation.
	///
	/// The config key should be a string separated by dots, representing the path through the
	/// configuration hierarchy (e.g., "animations.cursor").
	///
	/// The animation name is what the resulting animation will be stored as in the result.
	public static SpriteFrames LoadAnimation(string configKey, string animationName) {
		var cacheKey = (configKey, animationName);
		if (animationCache.TryGetValue(cacheKey, out SpriteFrames cachedAnimation))
			return cachedAnimation;

		object entry = GetEntryByPath(configKey);
		if (entry == null)
			throw new Exception($"Texture config not found for key: {configKey}");

		SpriteFrames animation = LoadAnimationFromConfigEntry(ParseConfigEntry(entry), animationName);

		animationCache[cacheKey] = animation;

		return animation;
	}

	/// Gets a color given a "civ index".
	///
	/// This exists in the TextureLoader because civ3 implements civ colors
	/// as 1x1 pixel pcx files.
	public static Color LoadColor(int civIndex) {
		if ((uint)civIndex < (uint)colorCache.Length && colorCache[civIndex] is Color cachedColor)
			return cachedColor;

		// Load the 1x1 pixel file and get the color, or use the modern hex color.
		string key = $"civ_colors.color_{civIndex}";
		ConfigEntry config = ParseConfigEntry(GetEntryByPath(key));
		Color color;
		if (config.HexColor != null) {
			color = new(code: config.HexColor);
		} else {
			ImageTexture texture = LoadFromConfigEntry(config);
			color = texture.GetImage().GetPixel(0, 0);
		}

		StoreCivColor(ref colorCache, civIndex, color);
		return color;
	}

	/// Gets the color for a "civ index" at full saturation, which stands out
	/// more, for example on the mini map.
	public static Color LoadIntensifiedColor(int civIndex) {
		if ((uint)civIndex < (uint)intensifiedColorCache.Length && intensifiedColorCache[civIndex] is Color cachedColor)
			return cachedColor;

		Color color = LoadColor(civIndex);
		Color intensified = Color.FromHsv(color.H, 1, color.V, color.A);

		StoreCivColor(ref intensifiedColorCache, civIndex, intensified);
		return intensified;
	}

	private static void StoreCivColor(ref Color?[] cache, int civIndex, Color color) {
		if (civIndex < 0 || civIndex >= MaxCachedCivColorIndex)
			return;
		if (civIndex >= cache.Length)
			Array.Resize(ref cache, Math.Max(civIndex + 1, 2 * cache.Length));
		cache[civIndex] = color;
	}

	/// An utility method for setting textures of a button node.
	/// Accepts a button and a config key. The config key should lead
	/// to a table containing config entries with "normal", "pressed"
	/// and "hover" keys.
	public static void SetButtonTextures(TextureButton button, string configKey) {
		object entry = GetEntryByPath(configKey);

		if (entry is not Table table)
			throw new Exception($"Table expected for key: {configKey}");

		button.TextureNormal = LoadFromLuaObject(table["normal"]);
		button.TexturePressed = LoadFromLuaObject(table["pressed"]);
		button.TextureHover = LoadFromLuaObject(table["hover"]);
	}

	// Loads the texture for an entry that's part of the texture config.
	private static ImageTexture LoadFromLuaObject(object entry) {
		if (entry is Table table) {
			if (!parsedConfigCache.TryGetValue(table, out ConfigEntry config)) {
				config = ParseConfigEntry(table);
				parsedConfigCache[table] = config;
			}
			return LoadFromConfigEntry(config);
		}
		return LoadFromConfigEntry(ParseConfigEntry(entry));
	}

	private static ConfigEntry ParseConfigEntry(object entry) {
		if (entry is string simplePath) {
			return new() {
				Path = simplePath,
			};
		}

		if (entry is Table table) {
			if (table["path"] == null) {
				throw new ArgumentException("Texture configuration missing required 'path' property");
			}

			IndexSet transparentColorIndexes = DefaultTransparentColorIndexes;
			object transparentConfig = table["transparent_color_indexes"];
			if (transparentConfig != null) {
				if (transparentConfig is not Table transparentTable) {
					throw new ArgumentException($"'transparent_color_indexes' must be a table.");
				}

				Span<ulong> bits = stackalloc ulong[4];
				foreach (DynValue d in transparentTable.Values) {
					// Note: Convert.ToInt32 doesn't work for DynValue.
					int index = (int)d.CastToNumber();
					if (index >= 0 && index < 256)
						bits[index >> 6] |= 1UL << (index & 63);
				}
				transparentColorIndexes = new IndexSet(bits[0], bits[1], bits[2], bits[3]);
			}

			object shadows = table["shadows"];

			return new() {
				Path = table["path"].ToString(),
				AlphaPath = table["alpha"]?.ToString(),
				PureAlpha = Convert.ToBoolean(table["pure_alpha"] ?? false),
				CropRegion = ExtractCropRegion(table),
				ColorOptions = new PCXToGodot.ColorOptions(
					shadows: Convert.ToBoolean(shadows ?? true),
					transparentColorIndexes: GetTransparentIndexSet(transparentColorIndexes)
				),
				ShadowsSpecified = shadows != null,
				AlphaRowOffset = Convert.ToInt32(table["alpha_row_offset"] ?? 0),
				HexColor = table["hex_color"]?.ToString(),

				FrameDuration = (float)Convert.ToDouble(table["frame_duration"] ?? 0.5),
				AnimationRows = Convert.ToInt32(table["animation_rows"] ?? 0),
				AnimationCols = Convert.ToInt32(table["animation_cols"] ?? 0),
			};
		}

		throw new ArgumentException($"Invalid texture config format: {entry?.GetType().Name ?? "null"}");
	}

	// The shared set of transparent color indexes with the given contents.
	private static HashSet<int> GetTransparentIndexSet(IndexSet indexes) {
		if (!transparentIndexSets.TryGetValue(indexes, out HashSet<int> set)) {
			set = indexes.ToHashSet();
			transparentIndexSets[indexes] = set;
		}
		return set;
	}

	private static ImageTexture LoadFromConfigEntry(ConfigEntry config) {
		string ext = Path.GetExtension(config.Path).ToLowerInvariant();

		return ext switch {
			".png" => LoadFromPNG(config.Path, config.CropRegion),
			".pcx" when config.PureAlpha => LoadPureAlpha(config.Path, config.ColorOptions.transparentColorIndexes),
			".pcx" when config.UseAlpha => LoadWithAlphaBlend(config.Path, config.AlphaPath!, config.CropRegion, config.AlphaRowOffset),
			// Uncropped textures don't simulate shadows unless the config asks for them.
			".pcx" when config.CropRegion is null && !config.ShadowsSpecified =>
				LoadFromPCX(config.Path, null, new PCXToGodot.ColorOptions(false, config.ColorOptions.transparentColorIndexes)),
			".pcx" => LoadFromPCX(config.Path, config.CropRegion, config.ColorOptions),
			_ => throw new FormatException($"Unknown texture format: {config.Path}"),
		};
	}

	private static SpriteFrames LoadAnimationFromConfigEntry(ConfigEntry config, string animationName) {
		string ext = Path.GetExtension(config.Path).ToLowerInvariant();
		SpriteFrames result = new();
		result.AddAnimation(animationName);

		if (ext == ".flc") {
			Flic flic = Util.LoadFlic(config.Path);

			const int row = 0;
			for (int col = 0; col < flic.Images.GetLength(1); col++) {
				byte[] frame = flic.Images[row, col];
				// The ignored variable is the "tint" image, which would get the civ
				// specific color applied to it if it was a unit animation.
				(ImageTexture bl, _) = Util.LoadTextureFromFlicData(frame, flic.Palette, flic.Width, flic.Height);
				result.AddFrame(animationName, bl, config.FrameDuration);
			}

			return result;
		}

		if (ext == ".png") {
			ImageTexture fullImage = LoadFromPNG(config.Path, config.CropRegion);
			if (config.AnimationRows == 0 || config.AnimationCols == 0) {
				throw new ArgumentException($"Expected non-zero anim rows and cols for {config.Path}");
			}

			int frameWidth = fullImage.GetWidth() / config.AnimationCols;
			int frameHeight = fullImage.GetHeight() / config.AnimationRows;
			for (int r = 0; r < config.AnimationRows; r++) {
				for (int c = 0; c < config.AnimationCols; c++) {
					Rect2I frameRegion = new(c * frameWidth, r * frameHeight, frameWidth, frameHeight);
					result.AddFrame(animationName,
						ImageTexture.CreateFromImage(fullImage.GetImage().GetRegion(frameRegion)),
						config.FrameDuration);
				}
			}

			return result;
		}

		return null;
	}

	// Helper method to extract crop region from a table
	private static CropRegion? ExtractCropRegion(Table table) {
		object cropRegionObj = table["crop_region"];
		if (cropRegionObj is Table cropRegion) {
			try {
				int x = Convert.ToInt32(cropRegion[1]);
				int y = Convert.ToInt32(cropRegion[2]);
				int w = Convert.ToInt32(cropRegion[3]);
				int h = Convert.ToInt32(cropRegion[4]);

				return new CropRegion(x, y, w, h);
			} catch (Exception ex) {
				throw new FormatException($"Invalid crop_region format: {ex.Message}", ex);
			}
		}

		return null;
	}

	// Helper method to handle alpha blend loading
	private static ImageTexture LoadWithAlphaBlend(string path, string alphaPath, CropRegion? cropRegion, int alphaRowOffset) {
		TextureCacheKey key = new(TextureKind.PcxWithAlphaBlend, path, cropRegion, false, default, alphaPath, alphaRowOffset);
		return GetOrAddTexture(key, () => {
			Pcx pcx = LoadPCX(path);
			Pcx alphaPcx = LoadPCX(alphaPath);

			return cropRegion.HasValue
				? PCXToGodot.getImageFromPCXWithAlphaBlend(pcx, alphaPcx, cropRegion.Value, alphaRowOffset)
				: PCXToGodot.getImageFromPCXWithAlphaBlend(pcx, alphaPcx);
		});
	}

	private static ImageTexture LoadPureAlpha(string path, HashSet<int> transparentColorIndexes) {
		TextureCacheKey key = new(TextureKind.PcxPureAlpha, path, null, false, IndexSet.From(transparentColorIndexes), null, 0);
		return GetOrAddTexture(key, () => PCXToGodot.getPureAlphaFromPCX(LoadPCX(path), transparentColorIndexes));
	}

	private static object GetEntryByPath(string configKey) {
		if (entryByPathCache.TryGetValue(configKey, out object cached))
			return cached;

		object entry = FindEntryByPath(configKey);
		entryByPathCache[configKey] = entry;
		return entry;
	}

	private static object FindEntryByPath(string configKey) {
		string[] parts = configKey.Split('.');
		object current = textureConfig;

		foreach (string part in parts) {
			if (current is Table table && table[part] != null) {
				current = table[part];
			} else {
				return null;
			}
		}

		return current;
	}

	// Loads a PCX texture. Without color options, an uncropped texture doesn't
	// simulate shadows while a cropped one does.
	private static ImageTexture LoadFromPCX(string relPath, CropRegion? cropRegion = null, PCXToGodot.ColorOptions? colorOptions = null) {
		PCXToGodot.ColorOptions options = colorOptions ?? (cropRegion is null ? UncroppedDefaultColorOptions : PCXToGodot.ColorOptions.Default);
		TextureCacheKey key = new(TextureKind.Pcx, relPath, cropRegion, options.shadows, IndexSet.From(options.transparentColorIndexes), null, 0);
		return GetOrAddTexture(key, () => {
			Pcx pcx = LoadPCX(relPath);
			if (cropRegion is not null)
				return PCXToGodot.getImageTextureFromPCX(pcx, cropRegion.Value, options);
			return PCXToGodot.getImageTextureFromPCX(pcx, options);
		});
	}

	private static ImageTexture LoadFromPNG(string relPath, CropRegion? cropRegion = null) {
		TextureCacheKey key = new(TextureKind.Png, relPath, cropRegion, false, default, null, 0);
		return GetOrAddTexture(key, () => {
			Image image = LoadPNG(relPath);
			if (cropRegion != null) {
				var region = cropRegion.Value;
				return ImageTexture.CreateFromImage(
					image.GetRegion(new Rect2I(region.LeftStart, region.TopStart, region.CroppedWidth, region.CroppedHeight))
				);
			}
			return ImageTexture.CreateFromImage(image);
		});
	}

	private static ImageTexture GetOrAddTexture(TextureCacheKey key, Func<ImageTexture> loader) {
		if (textureCache.TryGetValue(key, out ImageTexture cached))
			return cached;

		ImageTexture texture = loader();
		textureCache[key] = texture;
		return texture;
	}

	/**
	 * Utility method for loading PCX files that will cache them, so we don't have to load them from disk so often.
	 * Since the textures made from a file are cached, a decoded file is only kept while it's in recent use (for example
	 * while the crops of a sprite sheet are being loaded), and it's decoded again if it's needed again much later.
	 **/
	public static Pcx LoadPCX(string relPath) {
		if (PcxCache.TryGet(relPath, out Pcx value)) {
			return value;
		}
		Pcx thePcx = new(Util.Civ3MediaPath(relPath));
		PcxCache.Add(relPath, thePcx);
		return thePcx;
	}

	private static Image LoadPNG(string relPath) {
		if (PngCache.TryGetValue(relPath, out Image value)) {
			return value;
		}
		Image png = Image.LoadFromFile(Util.Civ3MediaPath(relPath));
		PngCache[relPath] = png;
		return png;
	}

	// Forgets the textures cached for game objects (tiles, techs and the
	// like), which hold on to the game they came from. A LAN client calls
	// this whenever it replaces its game with the host's snapshot; the
	// textures themselves stay cached by path.
	public static void ForgetGameObjects() {
		objectMappingCache.Clear();
	}

	public static void ClearCache() {
		PcxCache.Clear();
		PngCache.Clear();
		textureCache.Clear();
		configKeyCache.Clear();
		objectMappingCache.Clear();
		animationCache.Clear();
		colorCache = [];
		intensifiedColorCache = [];
		pureValueMappingCache.Clear();
		entryByPathCache.Clear();
		parsedConfigCache.Clear();
	}

	public static Vector2 Center(this ImageTexture tex) {
		return new Vector2(tex.GetWidth(), tex.GetHeight()) / 2;
	}
}

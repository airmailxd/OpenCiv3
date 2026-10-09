using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using C7.Textures;
using C7Engine;
using C7Engine.Lua;
using ConvertCiv3Media;
using Godot;
using QueryCiv3;

public partial class Util {
	private static string civ3Root = GetCiv3Path();

	// Changing the root invalidates every path resolved against it.
	static public string Civ3Root {
		get => civ3Root;
		set {
			civ3Root = value;
			ClearMediaPathCache();
		}
	}

	static public string GetCiv3Path() {
		string path = C7Settings.GetSettingValue("locations", "civ3InstallDir");
		if (path != null) return path;

		return Civ3Location.GetCiv3Path();
	}

	// Checks if a file exists ignoring case on the latter parts of its path. If the file is found, returns its full path re-capitalized as
	// necessary, otherwise returns null. This function is needed for the game to work on Linux & Mac with the .NET Core runtime. It's not needed
	// on Windows, which has a case insensitive filesystem, or when using the Mono runtime, which emulates case insensitivity out of the
	// box. Arguments:
	//   exactCaseRoot: The first part of the file path, not made case-insensitive. This is intended be the root Civ 3 path from GetCiv3Path().
	//   ignoredCaseExtension: The second part of the file path that will be searched ignoring case.
	public static string FileExistsIgnoringCase(string exactCaseRoot, string ignoredCaseExtension) {
		// In debug builds paths that are specified relative to the project directory
		// will start with res://. We want to turn those into real paths.
		if (exactCaseRoot.Contains("res://")) {
			exactCaseRoot = ProjectSettings.GlobalizePath(exactCaseRoot);
		}

		// If we aren't on windows, fix any windows path separators that may
		// appear in the path. This is particularly relevant for scenarios,
		//where the mod path frequently looks like  '..\conquests\Rise of Rome'.
		//
		// This is a bit of a hack, but it fixes scenario loading.
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
			ignoredCaseExtension = ignoredCaseExtension.Replace('\\', '/');
		}

		// First try the basic built-in File.Exists method since it's adequate
		// in most cases.
		//
		// We use GetFullPath to handle any ../Conquests/<scenario name> bits
		// in the path, which happens with scenario mod paths.
		string fullPath = System.IO.Path.Combine(exactCaseRoot, ignoredCaseExtension);
		fullPath = System.IO.Path.GetFullPath(fullPath);
		if (System.IO.File.Exists(fullPath))
			return fullPath;

		// If that didn't work, do a case-insensitive search starting at the root path and stepping through each piece of the extension. Skip
		// this step if the root directory doesn't exist or if running on Windows.
		string tr = null;
		if ((!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) &&
			System.IO.Directory.Exists(exactCaseRoot)) {
			tr = exactCaseRoot;

			// We need to update the ignored case extension before doing this
			// search, in case the ignored case extension previously had
			// ../Conquests/<scenario name> in it.
			//
			// If we didn't do this we'd end up with ".." as one of our steps
			// below, which derails the searching logic.
			//
			// We also strip any leading slashes, which can show up if the civ3
			// root doesn't end in a slash.
			ignoredCaseExtension = fullPath.Substring(exactCaseRoot.Length);
			ignoredCaseExtension = ignoredCaseExtension.TrimPrefix("\\").TrimPrefix("/");

			foreach (string step in ignoredCaseExtension.Replace('\\', '/').Split('/')) {
				string goal = System.IO.Path.Combine(tr, step);
				string match = null;
				foreach (string entry in ListDirectory(tr)) {
					if (entry.Equals(goal, StringComparison.CurrentCultureIgnoreCase)) {
						match = entry;
						break;
					}
				}

				if (match != null)
					tr = match;
				else {
					tr = null;
					break;
				}
			}
		}
		return tr;
	}

	// The entries of the directories searched by FileExistsIgnoringCase. Media
	// files don't change while the game runs, so a directory is listed only
	// once instead of once per path segment per lookup.
	private static readonly Dictionary<string, string[]> directoryListings = new();

	private static string[] ListDirectory(string directory) {
		if (!directoryListings.TryGetValue(directory, out string[] entries)) {
			entries = System.IO.Directory.GetFileSystemEntries(directory, "*");
			directoryListings[directory] = entries;
		}
		return entries;
	}

	/// <summary>
	/// Sets the Civ3 legacy mod path.
	/// This is here so Civ3MediaPath can refer to it, without having to grab it from all the places we might need to call
	/// it, which is in 25 places currently.
	/// </summary>
	private static string modPath;
	public static void setModPath(string modPathParam) {
		modPath = modPathParam;
		// Specifically fix up the conquests mod path for case sensitive
		// platforms. If we didn't do this then our path searching logic below
		// would find the default PediaIcons.txt instead of the scenario
		// specific file.
		modPath = modPath.Replace("\\conquests\\", "\\Conquests\\");
		ClearMediaPathCache();
	}

	// Civ3MediaPath results by media path. A null value means the media
	// couldn't be found. The results are only valid for the current roots,
	// mod path and standalone setting, so they're forgotten when any of those
	// change.
	private static readonly Dictionary<string, string> mediaPathCache = new();
	private static bool mediaPathCacheStandalone;

	private static void ClearMediaPathCache() {
		mediaPathCache.Clear();
		directoryListings.Clear();
	}

	/// <summary>
	/// Pass this function a relative path (e.g. Art/Terrain/xpgc.pcx) and it will grab the correct version
	/// Assumes Conquests/Complete
	/// </summary>
	/// <param name="mediaPath">The media path, e.g. Art/Units/units_32.pcx</param>
	/// <returns>The path to the media on the file system, or an exception if it cannot be found</returns>
	/// <exception cref="ApplicationException"></exception>
	public static string Civ3MediaPath(string mediaPath) {
		bool standalone = C7Settings.UseStandaloneMode();
		if (standalone != mediaPathCacheStandalone) {
			ClearMediaPathCache();
			mediaPathCacheStandalone = standalone;
		}

		if (!mediaPathCache.TryGetValue(mediaPath, out string result)) {
			result = FindCiv3MediaPath(mediaPath, standalone);
			mediaPathCache[mediaPath] = result;
		}

		if (result == null)
			throw new ApplicationException("Media path not found: " + mediaPath);
		return result;
	}

	// The uncached search behind Civ3MediaPath. Returns null if the media can't
	// be found.
	private static string FindCiv3MediaPath(string mediaPath, bool standalone) {
		//First, check if the file exists via a scenario's mod path
		//For now this is only checked relative to Civ3, not relative to C7.
		if (!string.IsNullOrEmpty(modPath)) {
			string[] paths = modPath.Split(";");
			foreach (string path in paths) {
				string[] tryPaths = new string[] {
					path,
					// Needed for some reason as Steam version at least puts some mod art in Extras instead of Scenarios
					//  Also, the case mismatch is intentional. C3C makes a capital C path, but it's lower-case on the filesystem
					"Conquests/Conquests/" + path, "Conquests/Scenarios/" + path, "civ3PTW/Scenarios/" + path
				};
				for (int i = 0; i < tryPaths.Length; i++) {
					string actualCasePath = CheckForCiv3Media(mediaPath, tryPaths[i]);
					if (actualCasePath != null)
						return actualCasePath;
				}
			}
		}

		// Next, before trying the base Civ paths, see if we have it packaged
		// with C7 and are in standalone mode.
		string c7BasePath = GetC7AssetsPath();
		string c7Path = FileExistsIgnoringCase(c7BasePath, mediaPath);

		if (standalone) {
			return c7Path;
		}

		//Next, check the base Civ paths
		string[] basePaths = new string[] {
			"Conquests",
			"civ3PTW",
			""
		};
		for (int i = 0; i < basePaths.Length; i++) {
			string actualCasePath = CheckForCiv3Media(mediaPath, basePaths[i]);
			if (actualCasePath != null)
				return actualCasePath;
		}

		// Finally, use a c7 path even if we aren't in standalone mode, but only
		// if we can't find the path in civ3 graphics. This allows us to toggle
		// to c7 art when we aren't in standalone mode.
		return c7Path;
	}

	private static string CheckForCiv3Media(string relPath, string rootPath) {
		// Combine TryPaths[i] and relPath. Make sure not to leave an erroneous forward slash at the start if TryPaths[i] is empty
		string fullPath = rootPath != "" ? rootPath + "/" + relPath : relPath;

		return FileExistsIgnoringCase(Civ3Root, fullPath);
	}

	static public (ImageTexture, ImageTexture) LoadTextureFromFlicData(byte[] image, byte[,] pallete, int width, int height) {
		var (baseImage, tintImage) = PCXToGodot.ByteArrayWithTintToImage(image, pallete, width, height, shadows: true);
		return (ImageTexture.CreateFromImage(baseImage), ImageTexture.CreateFromImage(tintImage));
	}

	// Decoded files are only needed until their textures have been built, so
	// a decoded file is only kept while it's in recent use (for example while
	// the frames of all of a unit's animations are being loaded).
	private static readonly RecentlyUsedCache<Flic> flicCache = new(4);

	static public Flic LoadFlic(string path) {
		if (flicCache.TryGet(path, out Flic result)) {
			return result;
		}
		result = new ConvertCiv3Media.Flic(Util.Civ3MediaPath(path));
		flicCache.Add(path, result);
		return result;
	}

	// A cache that keeps strong references to the few most recently used
	// values, and only weak references to the rest. Used for decoded media
	// files, which can be large, are expensive to decode and are typically
	// used a few times in quick succession and then not again once the
	// textures made from them exist.
	public sealed class RecentlyUsedCache<T> where T : class {
		// The recently used values, most recently used first.
		private readonly string[] recentKeys;
		private readonly T[] recentValues;
		private int recentCount = 0;
		private readonly Dictionary<string, WeakReference<T>> weak = new();

		// The weak references to collected values are dropped when there are
		// this many references, so they don't pile up.
		private const int MinPruneThreshold = 64;
		private int pruneThreshold = MinPruneThreshold;

		public RecentlyUsedCache(int recentCapacity) {
			recentKeys = new string[recentCapacity];
			recentValues = new T[recentCapacity];
		}

		public bool TryGet(string key, out T value) {
			for (int i = 0; i < recentCount; i++) {
				if (recentKeys[i] == key) {
					value = recentValues[i];
					MoveToFront(i);
					return true;
				}
			}
			if (weak.TryGetValue(key, out WeakReference<T> reference)) {
				if (reference.TryGetTarget(out value)) {
					KeepRecent(key, value);
					return true;
				}
				weak.Remove(key);
			}
			value = null;
			return false;
		}

		public void Add(string key, T value) {
			if (!weak.ContainsKey(key) && weak.Count >= pruneThreshold) {
				PruneCollected();
			}
			weak[key] = new WeakReference<T>(value);
			KeepRecent(key, value);
		}

		// Makes the value the most recently used one, dropping the least
		// recently used if there's no room.
		private void KeepRecent(string key, T value) {
			if (recentKeys.Length == 0) {
				return;
			}
			int index = Array.IndexOf(recentKeys, key, 0, recentCount);
			if (index < 0) {
				if (recentCount < recentKeys.Length) {
					recentCount++;
				}
				index = recentCount - 1;
			}
			recentKeys[index] = key;
			recentValues[index] = value;
			MoveToFront(index);
		}

		private void MoveToFront(int index) {
			if (index == 0) {
				return;
			}
			string key = recentKeys[index];
			T value = recentValues[index];
			Array.Copy(recentKeys, 0, recentKeys, 1, index);
			Array.Copy(recentValues, 0, recentValues, 1, index);
			recentKeys[0] = key;
			recentValues[0] = value;
		}

		// Drops the weak references whose values have been collected.
		private void PruneCollected() {
			List<string> collected = null;
			foreach ((string key, WeakReference<T> reference) in weak) {
				if (!reference.TryGetTarget(out _)) {
					(collected ??= new()).Add(key);
				}
			}
			if (collected != null) {
				foreach (string key in collected) {
					weak.Remove(key);
				}
			}
			// Prune again once the cache has doubled, so pruning stays cheap
			// on average even if most values are still alive.
			pruneThreshold = Math.Max(MinPruneThreshold, weak.Count * 2);
		}

		public void Clear() {
			Array.Clear(recentKeys);
			Array.Clear(recentValues);
			recentCount = 0;
			weak.Clear();
			pruneThreshold = MinPruneThreshold;
		}
	}

	static private string GetC7AssetsPath() {
		if (OS.HasFeature("editor")) {
			return "res://Assets";
		}

		return System.IO.Path.Combine(GamePaths.BaseDir, "Assets");
	}

	// Replaces image colors based on a given dictionary
	public static Image TransformColors(Image origin, Dictionary<Color, Color> colorReplacements) {
		if (origin.GetFormat() == Image.Format.Rgba8 && !origin.HasMipmaps()) {
			return TransformRgba8Colors(origin, colorReplacements);
		}

		Image result = (Image)origin.Duplicate();

		for (int y = 0; y < origin.GetHeight(); ++y) {
			for (int x = 0; x < origin.GetWidth(); ++x) {
				Color origin_color = origin.GetPixel(x, y);

				if (colorReplacements.TryGetValue(origin_color, out Color new_color)) {
					result.SetPixel(x, y, new_color);
				}
			}
		}

		return result;
	}

	// TransformColors for RGBA8 images, working on the image's bytes instead
	// of getting and setting each pixel through the engine. The colors are
	// converted to and from bytes by the engine itself, so the result is the
	// same as getting and setting the pixels one at a time.
	private static Image TransformRgba8Colors(Image origin, Dictionary<Color, Color> colorReplacements) {
		Image probe = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
		Dictionary<uint, uint> byteReplacements = new();
		foreach ((Color from, Color to) in colorReplacements) {
			// The only pixel value GetPixel could turn into `from` is the
			// nearest one, if GetPixel actually turns it into `from`.
			byte[] fromBytes = [ToByte(from.R), ToByte(from.G), ToByte(from.B), ToByte(from.A)];
			probe.SetData(1, 1, false, Image.Format.Rgba8, fromBytes);
			if (probe.GetPixel(0, 0) != from) {
				continue;
			}
			probe.SetPixel(0, 0, to);
			byteReplacements[BitConverter.ToUInt32(fromBytes)] = BitConverter.ToUInt32(probe.GetData());
		}

		byte[] data = origin.GetData();
		if (byteReplacements.Count > 0) {
			Span<uint> pixels = MemoryMarshal.Cast<byte, uint>(data.AsSpan());
			for (int i = 0; i < pixels.Length; i++) {
				if (byteReplacements.TryGetValue(pixels[i], out uint replacement)) {
					pixels[i] = replacement;
				}
			}
		}
		return Image.CreateFromData(origin.GetWidth(), origin.GetHeight(), false, Image.Format.Rgba8, data);
	}

	private static byte ToByte(float channel) {
		return (byte)Math.Clamp(Math.Round(channel * 255.0), 0, 255);
	}

	// Creates a texture from raw palette data. The data must be 256 pixels by 3 channels. Returns a 16x16 unfiltered RGB texture.
	public static ImageTexture createPaletteTexture(byte[,] raw) {
		if ((raw.GetLength(0) != 256) || (raw.GetLength(1) != 3))
			throw new Exception("Invalid palette dimensions. Palettes must be 256x3.");

		// Flatten palette data since CreateFromData can't accept two-dimensional arrays
		byte[] flatPalette = new byte[3*256];
		for (int n = 0; n < 256; n++)
			for (int k = 0; k < 3; k++)
				flatPalette[k + 3 * n] = raw[n, k];

		var img = Image.CreateFromData(16, 16, false, Image.Format.Rgb8, flatPalette);
		return ImageTexture.CreateFromImage(img);
	}

	// A FlicSheet holds the header values of a Flic file: the size, offset,
	// number of frames and timing of its animations. The frames themselves are
	// turned into SpriteFrames by the AnimationManager.
	public struct FlicSheet {
		public int spriteOriginalWidth, spriteOriginalHeight;
		public int spriteWidth, spriteHeight;
		public int offsetLeft, offsetTop;
		public int framesPerAnimation, numberOfAnimations;
		public int animationSpeed, animationTime;
	}

	// The size of a Flic file's header, and how much of it LoadFlicHeader needs.
	private const int FlicHeaderSize = 128;
	private const int FlicHeaderBytesUsed = 110;

	private static readonly Dictionary<string, FlicSheet> flicHeaderCache = new();

	// Reads the header values of a Flic file, without decoding its frames. The
	// values are the same as those of the Flic loaded from the same path.
	public static FlicSheet LoadFlicHeader(string filePath) {
		if (flicHeaderCache.TryGetValue(filePath, out FlicSheet sheet)) {
			return sheet;
		}

		byte[] header = new byte[FlicHeaderSize];
		using (var stream = System.IO.File.OpenRead(Util.Civ3MediaPath(filePath))) {
			stream.ReadAtLeast(header, FlicHeaderBytesUsed, throwOnEndOfStream: true);
		}

		// The offsets match those read by ConvertCiv3Media.Flic.Load.
		int fileFormat = BitConverter.ToUInt16(header, 4);
		if (fileFormat != 0xaf12) {
			throw new ApplicationException("Flic version # " + fileFormat.ToString("X4") + "does not match 0xaf12");
		}

		int numFrames = BitConverter.ToUInt16(header, 6);
		int numAnimations = BitConverter.ToUInt16(header, 0x60);
		int framesPerAnimation = BitConverter.ToUInt16(header, 0x62);
		// Leaderheads don't have the Civ3-specific values, and act like a regular Flic
		if (numAnimations == 0) {
			numAnimations = 1;
			framesPerAnimation = numFrames;
		}

		sheet = new FlicSheet {
			spriteOriginalWidth = BitConverter.ToUInt16(header, 104),
			spriteOriginalHeight = BitConverter.ToUInt16(header, 106),
			spriteWidth = BitConverter.ToUInt16(header, 8),
			spriteHeight = BitConverter.ToUInt16(header, 10),
			offsetLeft = BitConverter.ToUInt16(header, 100),
			offsetTop = BitConverter.ToUInt16(header, 102),
			framesPerAnimation = framesPerAnimation,
			numberOfAnimations = numAnimations,
			animationSpeed = BitConverter.ToInt32(header, 16),
			animationTime = BitConverter.ToUInt16(header, 108),
		};
		flicHeaderCache[filePath] = sheet;
		return sheet;
	}

	// Loads the header values of a Flic, along with the decoded Flic.
	public static (FlicSheet, Flic) loadFlicSheet(string filePath) {
		return (LoadFlicHeader(filePath), LoadFlic(filePath));
	}

	// Like LoadWAVFromDisk, but the path is a relative path, not the result of
	// calling Civ3MediaPath.
	//
	// The result may be null if modern graphics are active, as we do not yet
	// have sound replacements.
	public static AudioStreamWav? LoadCiv3WAVFromDisk(string path) {
		try {
			return LoadWAVFromDisk(Civ3MediaPath(path));
		} catch (Exception) {
			return null;
		}
	}

	public static AudioStreamWav LoadWAVFromDisk(string path) {
		FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);

		byte[] riffBytes = file.GetBuffer(4);
		if (!"RIFF"u8.SequenceEqual(riffBytes)) {
			throw new Exception("Unsupported file, missing 'RIFF' tag");
		}
		uint fileSize = file.Get32();   //minus 8 bytes

		byte[] waveBytes = file.GetBuffer(4);
		if (!"WAVE"u8.SequenceEqual(waveBytes)) {
			throw new Exception("Unsupported file, missing 'WAVE' tag");
		}

		bool formatFound = false;
		bool dataFound = false;

		AudioStreamWav wav = new();

		while (!file.EofReached()) {
			byte[] chunkBytes = file.GetBuffer(4);
			uint chunkSize = file.Get32();
			ulong position = file.GetPosition();

			if (file.EofReached()) {
				//May occur with e.g. an empty junk chunk
				break;
			}

			if ("fmt "u8.SequenceEqual(chunkBytes))    //format chunk
			{
				//There is some disagreement between the C++ and GDScript sources
				//as to which compression codes Godot supports.  The C++ has a comment
				//saying, "Consider revision for engine version 3.0", and noting other
				//formats are not supported in its importer.  The GDScript seems
				//to match up with the current FormatEnum.  I'm going to go out on
				//a limb and say the GDScript is probably more current relative
				//to what AudioStreamWAV supports.  But that could be wrong.
				ushort compressionCode = file.Get16();
				if (compressionCode == 1) {
					wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
				} else if (compressionCode == 0) {
					wav.Format = AudioStreamWav.FormatEnum.Format8Bits;
				} else if (compressionCode == 2) {
					wav.Format = AudioStreamWav.FormatEnum.ImaAdpcm;
				}

				ushort channels = file.Get16();
				if (channels == 2) {
					wav.Stereo = true;
				} else if (channels < 1 || channels > 5) {
					throw new Exception("Only mono and stream WAV files supported");
				}

				uint sampleRate = file.Get32();
				wav.MixRate = (int)sampleRate;

				uint averageBPS = file.Get32(); //unused
				ushort blockAlign = file.Get16();   //unused
				ushort formatBits = file.Get16();

				if (formatBits % 8 != 0 || formatBits == 0) {
					throw new Exception("Format bits must be a multiple of 8");
				}
				formatFound = true;
			} else if ("data"u8.SequenceEqual(chunkBytes)) {
				byte[] allTheData = file.GetBuffer(chunkSize);
				wav.Data = allTheData;
				dataFound = true;
			}

			file.Seek(position + chunkSize);
		}

		if (!formatFound || !dataFound) {
			throw new Exception("Failed to find both the format and data chunks");
		}

		return wav;
	}

	public static AudioStreamMP3? LoadCiv3Mp3FromDisk(string path) {
		try {
			return LoadMp3FromDisk(Civ3MediaPath(path));
		} catch (Exception) {
			return null;
		}
	}

	public static AudioStreamMP3 LoadMp3FromDisk(string path) {
		AudioStreamMP3 mp3 = AudioStreamMP3.LoadFromFile(path);
		mp3.Loop = true; // TODO: beyond simple path loading: add looping as a property
		return mp3;
	}

	public static AudioStreamOggVorbis? LoadCiv3OggFromDisk(string path) {
		try {
			return LoadOggFromDisk(Civ3MediaPath(path));
		} catch (Exception) {
			return null;
		}
	}

	public static AudioStreamOggVorbis LoadOggFromDisk(string path) {
		AudioStreamOggVorbis ogg = AudioStreamOggVorbis.LoadFromFile(path);
		ogg.Loop = true;
		return ogg;
	}


	// This method is intended for use within overrides of Godot object _ValidateProperty method.
	// Its purpose is to prevent values of properties listed in validProperties from being saved as
	// part of the scene.  It's useful when using [Tool] scripts to execute code in editor. It
	// allows to load Civ3 textures as part of such scripts, but prevents these textures from being
	// saved into the scene.
	//
	// See Godot docs on _ValidateProperty:
	// https://docs.godotengine.org/en/4.2/classes/class_object.html#class-object-private-method-validate-property
	public static void ApplyNoSaveFlag(Godot.Collections.Dictionary property, HashSet<StringName> validProperties) {
		StringName propertyName = property["name"].AsStringName();

		if (validProperties.Contains(propertyName)) {
			property["usage"] = (int)PropertyUsageFlags.NoInstanceState;
		}
	}

	// Shows an error in a plain dialog window over the given node, for screens
	// that have no popup overlay of their own, like the new game setup.
	public static void ShowErrorDialog(Node parent, string title, string message) {
		AcceptDialog dialog = new() {
			Title = title,
			DialogText = message,
			Exclusive = true,
		};
		dialog.Confirmed += dialog.QueueFree;
		dialog.Canceled += dialog.QueueFree;
		parent.AddChild(dialog);
		dialog.PopupCentered();
	}

	// Allow clearing the caches, so that scenarios with different files that
	// have the same name can be loaded independently.
	public static void ClearCaches() {
		flicCache.Clear();
		flicHeaderCache.Clear();
		ClearMediaPathCache();
		PCXToGodot.ClearCache();
		TextureLoader.ClearCache();
		ModernGraphics.ClearCache();
		AnimationManager.ClearCache();
		PlayerTextureUtil.ClearCache();
		UICaches.Clear();
		C7.Map.CityScene.ClearTextureCache();
	}
}

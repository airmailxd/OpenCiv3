
// AnimationManager's purpose is to store the data associated with Civ 3 animations, for example the contents of each folder in Art/Units. It does lazy
// loading & memoization so each file is loaded only when it's needed and then stored so it is only ever loaded once per game. The main (and only)
// instance of AnimationManager is kept in Game, AnimationTracker holds a reference to it.

// It would be nice to load this data close to where it's used, e.g., have UnitLayer load the FlicSheets, instead of putting all the loading code in
// one detached class like this. That's how things originally worked but I created AnimationManager to solve two issues:
// 1. AnimationTracker and UnitLayer both need to load the unit INIs. So we either have a common place to store the INIs or duplication of work, and I
// think the former is the better choice.
// 2. AnimationTracker needs to know the duration of animations, which awkwardly cannot be determined based on the INI files alone. In order to know
// the duration of an anim you must know how many frames it has, and the only way to know that is to read its flic file.

// The intended usage is to access the animation data through a Civ3Anim object obtained through the "forUnit" or "forEffect" methods. For example:
//   AnimationManager.forUnit("Warrior", MapUnit.AnimatedAction.FORTIFY).playSound()
// To play the warrior's foritfy sound effect.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IniParser;
using IniParser.Model;
using C7GameData;
using ConvertCiv3Media;
using Serilog;

public partial class AnimationManager {

	private static readonly ILogger log = LogManager.ForContext<AnimationManager>();

	// Whether we have art for a unit art name. Missing art is remembered so
	// that it's only reported once.
	private static readonly Dictionary<string, bool> unitArtExists = new();

	// What a unit's own art name (MapUnit.GetArtName) depends on: its type,
	// its owner's era and, for workers, whether it's a captive.
	private readonly record struct ArtNameKey(UnitPrototype unitType, string era, bool captiveWorker);

	// The results of ArtNameFor that don't depend on anything but the key.
	private static readonly Dictionary<ArtNameKey, string> artNames = new();

	// The art to draw a unit with. If its own art can't be found, like army
	// art that isn't available, an army is drawn with the art of the member
	// that would defend it, and otherwise the prototype's default art is
	// tried, rather than failing to load the same art every frame.
	public static string ArtNameFor(MapUnit unit) {
		UnitPrototype unitType = unit.unitType;
		bool captiveWorker = unitType.isWorker && unitType.art.mainArt.variations != null && unit.IsCaptive();
		ArtNameKey key = new(unitType, unit.owner?.eraCivilopediaName, captiveWorker);
		if (artNames.TryGetValue(key, out string cached)) {
			return cached;
		}

		string name = unit.GetArtName();
		if (HasUnitArt(name)) {
			artNames[key] = name;
			return name;
		}

		if (unit.IsArmy()) {
			// The member changes during the game, so this isn't cached.
			MapUnit member = unit.Combatant(CombatRole.Defense);
			if (member != unit && HasUnitArt(member.GetArtName())) {
				return member.GetArtName();
			}
		}
		string defaultName = unitType.art.mainArt.defaultName;
		string result = HasUnitArt(defaultName) ? defaultName : name;
		if (!unit.IsArmy()) {
			artNames[key] = result;
		}
		return result;
	}

	private static bool HasUnitArt(string name) {
		if (string.IsNullOrEmpty(name)) {
			return false;
		}
		if (!unitArtExists.TryGetValue(name, out bool exists)) {
			try {
				Util.Civ3MediaPath(string.Format("Art/Units/{0}/{0}.INI", name));
				exists = true;
			} catch (ApplicationException) {
				log.Warning($"No unit art found for {name}");
				exists = false;
			}
			unitArtExists[name] = exists;
		}
		return exists;
	}

	public static string BaseAnimationKey(string unitName, MapUnit.AnimatedAction action) {
		return String.Format("{0}_{1}", unitName, action.ToString());
	}

	public static string BaseAnimationKey(MapUnit unit, MapUnit.AnimatedAction action) {
		return BaseAnimationKey(ArtNameFor(unit), action);
	}

	public static string AnimationKey(string baseKey, TileDirection direction) {
		return String.Format("{0}_{1}", baseKey, direction.ToString());
	}

	public static string AnimationKey(MapUnit unit, MapUnit.AnimatedAction action, TileDirection direction) {
		return AnimationKey(BaseAnimationKey(unit, action), direction);
	}

	public static string AnimationKey(AnimatedEffect effect, MapUnit.AnimatedAction action) {
		return $"{effect.ToString()}_{action.ToString()}";
	}

	public static readonly Dictionary<string, ImageTexture> AnimationThumbnails = new();
	public static readonly Dictionary<string, ImageTexture> AnimationTintThumbnails = new();

	private const int thumbnailFrame = 0;
	private const TileDirection thumbnailDirection = TileDirection.SOUTHEAST;
	private const MapUnit.AnimatedAction thumbnailAction = MapUnit.AnimatedAction.DEFAULT;

	private AudioStreamPlayer audioPlayer;

	public SpriteFrames spriteFrames;
	public SpriteFrames tintFrames;

	private Dictionary<string, IniData> iniDatas = new Dictionary<string, IniData>();

	// What's needed to draw an animation: its name in the SpriteFrames, its
	// frames and, for unit animations, the placement values from its flic.
	// These are looked up once per animation instead of once per frame.
	public sealed class AnimationInfo {
		public readonly string name;
		public readonly StringName animationName;
		public readonly int frameCount;
		// The size of the animation's frames, zero if the animation has no frames.
		public readonly Vector2 frameSize;
		public readonly Vector2I flicOffset;
		public readonly Vector2I flicOriginalSize;

		public AnimationInfo(SpriteFrames frames, string name, Util.FlicSheet flicSheet) {
			this.name = name;
			animationName = new StringName(name);
			frameCount = frames.GetFrameCount(animationName);
			frameSize = frameCount > 0 ? frames.GetFrameTexture(animationName, 0).GetSize() : Vector2.Zero;
			flicOffset = new Vector2I(flicSheet.offsetLeft, flicSheet.offsetTop);
			flicOriginalSize = new Vector2I(flicSheet.spriteOriginalWidth, flicSheet.spriteOriginalHeight);
		}

		// The frame to show at a given progress (0 to 1) through the animation.
		public int FrameAtProgress(float progress) {
			// AnimatedSprite2D has a settable FrameProgress field, which I expected to
			// update the current frame of the animation upon setting, but it did not
			// when I tried it, so instead, calculate what the next frame should be
			// based on the progress.
			int nextFrame = (int)((float)frameCount * progress);
			return nextFrame >= frameCount ? frameCount - 1 : (nextFrame < 0 ? 0 : nextFrame);
		}
	}

	private static readonly int animatedActionCount = (int)Enum.GetValues<MapUnit.AnimatedAction>().Max() + 1;
	private static readonly int tileDirectionCount = (int)Enum.GetValues<TileDirection>().Max() + 1;

	// The unit animations by art name, each an array indexed by action and direction.
	private readonly Dictionary<string, AnimationInfo[]> unitAnimations = new();
	private readonly Dictionary<(AnimatedEffect, MapUnit.AnimatedAction), AnimationInfo> effectAnimations = new();

	// Returns the animation for drawing a unit doing an action facing a
	// direction, loading the animation if it hasn't been loaded yet.
	public AnimationInfo GetUnitAnimation(MapUnit unit, MapUnit.AnimatedAction action, TileDirection direction) {
		string artName = ArtNameFor(unit);
		int index = (int)action * tileDirectionCount + (int)direction;
		bool indexable = (uint)action < (uint)animatedActionCount && (uint)direction < (uint)tileDirectionCount;

		if (!unitAnimations.TryGetValue(artName, out AnimationInfo[] animations)) {
			animations = new AnimationInfo[animatedActionCount * tileDirectionCount];
			unitAnimations.Add(artName, animations);
		}
		if (indexable && animations[index] is AnimationInfo cached) {
			return cached;
		}

		LoadAnimation(artName, action);
		string folderPath = "Art/Units/" + artName;
		IniData iniData = getINIData(folderPath + "/" + artName + ".ini");
		AnimationInfo info = new(spriteFrames, AnimationKey(BaseAnimationKey(artName, action), direction), getFlicSheet(folderPath, iniData, action));
		if (indexable) {
			animations[index] = info;
		}
		return info;
	}

	// Returns the animation for drawing an effect, loading it if it hasn't been loaded yet.
	public AnimationInfo GetEffectAnimation(C7Animation anim) {
		if (effectAnimations.TryGetValue((anim.effect, anim.action), out AnimationInfo cached)) {
			return cached;
		}

		anim.loadEffectAnimation();
		AnimationInfo info = new(spriteFrames, AnimationKey(anim.effect, anim.action), anim.getFlicSheet());
		effectAnimations[(anim.effect, anim.action)] = info;
		return info;
	}

	public AnimationManager(AudioStreamPlayer audioPlayer) {
		this.audioPlayer = audioPlayer;
		this.spriteFrames = new SpriteFrames();
		this.tintFrames = new SpriteFrames();
	}

	public IniData getINIData(string pathKey) {
		if (!iniDatas.TryGetValue(pathKey, out IniData tr)) {
			string fullPath = Util.Civ3MediaPath(pathKey);
			tr = C7Engine.Util.GetFileIniDataParser().ReadFile(fullPath);
			iniDatas.Add(pathKey, tr);
		}
		return tr;
	}

	public static string GetUnitDefaultThumbnailKey(MapUnit unit) {
		return $"{ArtNameFor(unit)}_{thumbnailDirection}_{thumbnailAction}_{thumbnailFrame}";
	}

	public (ImageTexture baseFrame, ImageTexture tintFrame) GetAnimationFrameAndTintTextures(MapUnit unit) {

		string key = GetUnitDefaultThumbnailKey(unit);

		if (AnimationThumbnails.TryGetValue(key, out ImageTexture baseImage)
		   && AnimationTintThumbnails.TryGetValue(key, out ImageTexture tintImage)) {
			return (baseImage, tintImage);
		}

		string filepath = getUnitFlicFilepath(unit, thumbnailAction);

		Flic flic = Util.LoadFlic(filepath);

		byte[] rawFrame = flic.Images[flicAnimationDirectionToRow(thumbnailDirection), thumbnailFrame];
		// This actually doesn't return the tint frame with the civ color applied. The shader still needs to be applied.
		(ImageTexture baseFrame, ImageTexture tintFrame) = Util.LoadTextureFromFlicData(rawFrame, flic.Palette, flic.Width, flic.Height);
		AnimationThumbnails[key] = baseFrame;
		AnimationTintThumbnails[key] = tintFrame;

		return (baseFrame, tintFrame);
	}

	// Looks up the name of the flic file associated with a given action in an animation INI. If there is no flic file listed for the action,
	// returns instead the file name for the default action, and if that's missing too, throws an exception.
	public string getFlicFileName(IniData iniData, MapUnit.AnimatedAction action) {
		string fileName = iniData["Animations"][action.ToString()];
		if (!string.IsNullOrEmpty(fileName)) {
			return fileName;
		} else if (action != MapUnit.AnimatedAction.DEFAULT) {
			return getFlicFileName(iniData, MapUnit.AnimatedAction.DEFAULT);
		} else {
			throw new Exception("Missing default animation"); // TODO: Add the INI's file name to the error message
		}
	}

	public IniData getUnitINIData(string unitTypeName) {
		return getINIData(string.Format("Art/Units/{0}/{0}.INI", unitTypeName));
	}

	public string GetFlicFilePath(string rootPath, IniData iniData, MapUnit.AnimatedAction action) {
		return rootPath + "/" + getFlicFileName(iniData, action);
	}

	public string getUnitFlicFilepath(MapUnit unit, MapUnit.AnimatedAction action) {
		return getUnitFlicFilepath(ArtNameFor(unit), action);
	}

	public string getUnitFlicFilepath(string artName, MapUnit.AnimatedAction action) {
		string directory = string.Format("Art/Units/{0}", artName);
		IniData ini = getUnitINIData(artName);
		string filename = getFlicFileName(ini, action);
		return directory.PathJoin(filename);
	}

	// The flic loading code parses the animations into a 2D array, where each row is an animation
	// corresponding to a tile direction. flicRowToAnimationDirection maps row number -> direction.
	private static TileDirection flicRowToAnimationDirection(int row) {
		switch (row) {
			case 0: return TileDirection.SOUTHWEST;
			case 1: return TileDirection.SOUTH;
			case 2: return TileDirection.SOUTHEAST;
			case 3: return TileDirection.EAST;
			case 4: return TileDirection.NORTHEAST;
			case 5: return TileDirection.NORTH;
			case 6: return TileDirection.NORTHWEST;
			case 7: return TileDirection.WEST;
		}
		// Rows beyond the 8 real directions are unexpected. Return INVALID rather
		// than guessing a direction, so they don't interfere with the others.
		return TileDirection.INVALID;
	}

	private static int flicAnimationDirectionToRow(TileDirection tileDirection) {
		switch (tileDirection) {
			case TileDirection.SOUTHWEST: return 0;
			case TileDirection.SOUTH: return 1;
			case TileDirection.SOUTHEAST: return 2;
			case TileDirection.EAST: return 3;
			case TileDirection.NORTHEAST: return 4;
			case TileDirection.NORTH: return 5;
			case TileDirection.NORTHWEST: return 6;
			case TileDirection.WEST: return 7;
		}
		return 2;
	}

	public static void loadFlicAnimation(string path, string name, ref SpriteFrames frames, ref SpriteFrames tint) {
		Flic flic = Util.LoadFlic(path);

		for (int row = 0; row < flic.Images.GetLength(0); row++) {
			string direction = flicRowToAnimationDirection(row).ToString();
			string animationName = name + "_" + direction;
			frames.AddAnimation(animationName);
			tint.AddAnimation(animationName);

			for (int col = 0; col < flic.Images.GetLength(1); col++) {
				byte[] frame = flic.Images[row,col];
				(ImageTexture bl, ImageTexture tl) = Util.LoadTextureFromFlicData(frame, flic.Palette, flic.Width, flic.Height);
				frames.AddFrame(animationName, bl, 0.5f); // TODO: frame duration is controlled by .ini
				tint.AddFrame(animationName, tl, 0.5f);   // TODO: frame duration is controlled by .ini
			}
		}
	}

	public static void loadFlicEffectAnimation(string path, string name, ref SpriteFrames frames, ref SpriteFrames tint) {
		Flic flic = Util.LoadFlic(path);

		for (int row = 0; row < flic.Images.GetLength(0); row++) {
			string animationName = name;
			frames.AddAnimation(animationName);
			tint.AddAnimation(animationName);

			for (int col = 0; col < flic.Images.GetLength(1); col++) {
				byte[] frame = flic.Images[row,col];
				(ImageTexture bl, ImageTexture tl) = Util.LoadTextureFromFlicData(frame, flic.Palette, flic.Width, flic.Height);
				frames.AddFrame(animationName, bl, 0.5f); // TODO: frame duration is controlled by .ini
				tint.AddFrame(animationName, tl, 0.5f);   // TODO: frame duration is controlled by .ini
			}
		}
	}

	public bool LoadAnimation(MapUnit unit, MapUnit.AnimatedAction action) {
		return LoadAnimation(ArtNameFor(unit), action);
	}

	public bool LoadAnimation(string artName, MapUnit.AnimatedAction action) {
		string name = BaseAnimationKey(artName, action);
		string testName = AnimationKey(name, TileDirection.NORTH);
		if (spriteFrames.HasAnimation(testName) && tintFrames.HasAnimation(testName)) {
			return false;
		}
		string filepath = getUnitFlicFilepath(artName, action);
		loadFlicAnimation(filepath, name, ref this.spriteFrames, ref this.tintFrames);
		return true;
	}

	public bool LoadAnimation(AnimatedEffect effect, MapUnit.AnimatedAction action, string flicFilePath) {
		string name = AnimationKey(effect, action);
		if (spriteFrames.HasAnimation(name) && tintFrames.HasAnimation(name)) {
			return false;
		}
		loadFlicEffectAnimation(flicFilePath, name, ref this.spriteFrames, ref this.tintFrames);
		return true;
	}

	// Returns the header values of the flic for an action. The values are
	// cached by Util.LoadFlicHeader, which reads them without decoding the flic.
	public Util.FlicSheet getFlicSheet(string rootPath, IniData iniData, MapUnit.AnimatedAction action) {
		return Util.LoadFlicHeader(GetFlicFilePath(rootPath, iniData, action));
	}

	private Dictionary<string, AudioStreamWav> wavs = new Dictionary<string, AudioStreamWav>();

	public void playSound(string rootPath, IniData iniData, MapUnit.AnimatedAction action) {
		string fileName = iniData["Sound Effects"][action.ToString()];
		if (fileName.EndsWith(".WAV", StringComparison.CurrentCultureIgnoreCase)) {
			AudioStreamWav wav;
			string pathKey = rootPath + "/" + fileName;
			if (!wavs.TryGetValue(pathKey, out wav)) {
				wav = Util.LoadCiv3WAVFromDisk(pathKey);
				wavs.Add(pathKey, wav);
			}
			if (wav != null) {
				audioPlayer.Stream = wav;
				audioPlayer.Play();
			}
		}
	}

	public C7Animation forUnit(MapUnit unit, MapUnit.AnimatedAction action) {
		return new C7Animation(this, unit, action);
	}

	public C7Animation forEffect(AnimatedEffect effect) {
		return new C7Animation(this, effect);
	}

	public static void ClearCache() {
		AnimationThumbnails.Clear();
		AnimationTintThumbnails.Clear();
		// Which art exists depends on the media paths, which may have changed.
		artNames.Clear();
		unitArtExists.Clear();
	}
}

public partial class C7Animation {
	public AnimationManager animationManager { get; private set; }
	public string folderPath { get; private set; } // For example "Art/Units/Warrior" or "Art/Animations/Trajectory"
	public string iniFileName { get; private set; }
	private MapUnit unit;
	public AnimatedEffect effect;
	public MapUnit.AnimatedAction action { get; private set; }

	public C7Animation(AnimationManager civ3AnimData, MapUnit unit, MapUnit.AnimatedAction action) {
		string artName = AnimationManager.ArtNameFor(unit);
		this.animationManager = civ3AnimData;
		this.folderPath = "Art/Units/" + artName;
		this.iniFileName = artName + ".ini";
		this.action = action;
		this.unit = unit;
	}

	public static readonly Dictionary<AnimatedEffect, string> effectCategories = new Dictionary<AnimatedEffect, string>
	{
		{ AnimatedEffect.Hit      , "Trajectory" },
		{ AnimatedEffect.Hit2     , "Trajectory" },
		{ AnimatedEffect.Hit3     , "Trajectory" },
		{ AnimatedEffect.Hit5     , "Trajectory" },
		{ AnimatedEffect.Miss     , "Trajectory" },
		{ AnimatedEffect.WaterMiss, "Trajectory" }
	};

	public static readonly Dictionary<AnimatedEffect, string> effectINIFileNames = new Dictionary<AnimatedEffect, string>
	{
		{ AnimatedEffect.Hit      , "hit.ini" },
		{ AnimatedEffect.Hit2     , "hit2.ini" },
		{ AnimatedEffect.Hit3     , "hit3.ini" },
		{ AnimatedEffect.Hit5     , "hit5.ini" },
		{ AnimatedEffect.Miss     , "miss.ini" },
		{ AnimatedEffect.WaterMiss, "water miss.ini" }
	};

	public C7Animation(AnimationManager civ3AnimData, AnimatedEffect effect) {
		this.animationManager = civ3AnimData;
		this.folderPath = "Art/Animations/" + effectCategories[effect];
		this.iniFileName = effectINIFileNames[effect];
		this.action = MapUnit.AnimatedAction.DEATH;
		this.effect = effect;
	}

	public IniData getINIData() {
		return animationManager.getINIData(folderPath + "/" + iniFileName);
	}

	private Util.FlicSheet? flicSheet = null;

	public Util.FlicSheet getFlicSheet() {
		flicSheet ??= animationManager.getFlicSheet(folderPath, getINIData(), action);
		return flicSheet.Value;
	}

	public void loadSpriteAnimation() {
		this.animationManager.LoadAnimation(this.unit, this.action);
	}

	public void loadEffectAnimation() {
		var path = animationManager.GetFlicFilePath(folderPath, getINIData(), action);
		this.animationManager.LoadAnimation(this.effect, this.action, path);
	}

	public void playSound() {
		animationManager.playSound(folderPath, getINIData(), action);
	}

	public double getDuration() {
		Util.FlicSheet flicSheet = getFlicSheet();
		return flicSheet.animationSpeed * flicSheet.framesPerAnimation;
	}

	public Vector2I GetFlicAnimationOffset() {
		Util.FlicSheet flicSheet = getFlicSheet();
		return new Vector2I(flicSheet.offsetLeft, flicSheet.offsetTop);
	}

	public Vector2I GetFlicAnimationOriginalSize() {
		Util.FlicSheet flicSheet = getFlicSheet();
		return new Vector2I(flicSheet.spriteOriginalWidth, flicSheet.spriteOriginalHeight);
	}
}

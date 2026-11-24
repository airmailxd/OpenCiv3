using Godot;
using System.IO;
using C7Engine.Lua;

public static class GamePaths {
	// Base directory for finding data files (Lua/, Text/, Assets/).
	// In the editor this is "res://". In exports, it's the directory
	// containing the executable — on macOS, navigated out of the .app bundle.
	private static string _baseDir;
	public static string BaseDir {
		get {
			if (_baseDir == null) {
				if (OS.HasFeature("editor")) {
					_baseDir = "";
				} else {
					string exeDir = OS.GetExecutablePath().GetBaseDir();
					// On macOS the exe is inside *.app/Contents/MacOS/;
					// data files live alongside the .app bundle.
					if (exeDir.Contains(".app/Contents/MacOS")) {
						_baseDir = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..")) + "/";
					} else {
						_baseDir = exeDir + "/";
					}
				}
			}
			return _baseDir;
		}
	}

	// This version is standalone only: every game is played with the game's
	// own art and rules.
	public static GameMode.Config GameMode {
		get => standalone;
	}

	public static string GameModesDir => Path.Combine(BaseDir, "Lua");
	public static GameMode.Config standalone = new("civ3", ["standalone"]);

	// The given game mode as this version plays it: with the standalone
	// addon, which a game made elsewhere with other art may lack.
	public static GameMode.Config AsStandalone(GameMode.Config config) {
		if (config.addonPaths.Contains("standalone")) {
			return config;
		}
		return new(config.baseModeDir, [.. config.addonPaths, "standalone"], config.rulesetDir);
	}

	// The default rules file a .sav or .biq is imported against. This
	// version loads only its own saves, so there is none.
	public static string DefaultBicPath { get => null; }
}

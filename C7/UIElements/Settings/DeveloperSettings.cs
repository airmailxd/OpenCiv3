using C7Engine;

// Settings for testing the game, under Developer on the settings page,
// saved to C7.ini.
public static class DeveloperSettings {
	public const string SettingsSection = "developer";
	public const string AIFogOfWarKey = "aiFogOfWar";
	public const string RecordPlayLogKey = "recordPlayLog";

	// Whether the AI plans only with what it knows (see
	// C7Engine.AI.AIFogOfWar). Off by default: the AI sees the whole map.
	public static bool AIFogOfWar {
		get => C7Settings.GetSettingsValueOrDefault(SettingsSection, AIFogOfWarKey, "false") == "true";
		set {
			C7Settings.SetValue(SettingsSection, AIFogOfWarKey, value ? "true" : "false");
			C7Settings.SaveSettings();
		}
	}

	// Whether games write a play log of what their humans do, and what the
	// Advanced AI would have done (see C7Engine.PlayLog.PlayRecorder and
	// doc/play-log.md). Off by default.
	public static bool RecordPlayLog {
		get => C7Settings.GetSettingsValueOrDefault(SettingsSection, RecordPlayLogKey, "false") == "true";
		set {
			C7Settings.SetValue(SettingsSection, RecordPlayLogKey, value ? "true" : "false");
			C7Settings.SaveSettings();
		}
	}

	// Where play logs went before games had their own folders, and still go
	// for a game whose folder can't be made: play-logs beside C7.ini.
	public static string PlayLogDirectory => System.IO.Path.Combine(C7Settings.WritableDirectory, "play-logs");

	// Hands the settings to the engine as a game starts or loads.
	public static void Apply() {
		EngineStorage.aiFogOfWar = AIFogOfWar;
		C7Engine.PlayLog.PlayRecorder.enabled = RecordPlayLog;
		C7Engine.PlayLog.PlayRecorder.directory = PlayLogDirectory;
		// The game asks for its name, and its log goes in its folder (see
		// Game.StartPlayLog).
		C7Engine.PlayLog.PlayRecorder.waitForLogFile = true;
	}
}

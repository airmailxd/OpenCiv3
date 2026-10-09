using C7Engine;

// Settings for testing the game, under Developer on the settings page,
// saved to C7.ini.
public static class DeveloperSettings {
	public const string SettingsSection = "developer";
	public const string AIFogOfWarKey = "aiFogOfWar";

	// Whether the AI plans only with what it knows (see
	// C7Engine.AI.AIFogOfWar). Off by default: the AI sees the whole map.
	public static bool AIFogOfWar {
		get => C7Settings.GetSettingsValueOrDefault(SettingsSection, AIFogOfWarKey, "false") == "true";
		set {
			C7Settings.SetValue(SettingsSection, AIFogOfWarKey, value ? "true" : "false");
			C7Settings.SaveSettings();
		}
	}
}

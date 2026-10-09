using System.IO;
using C7Engine;

// Whether maps that can't be played (see GameSetup.MaxMapAttempts) are saved
// for looking at, set on the settings page and kept in C7.ini. On unless
// turned off.
public static class InvalidMaps {
	private const string Section = "mapGeneration";
	private const string Key = "saveInvalidMaps";

	// Where they are saved: the Invalid Maps folder next to the saves.
	public static string Folder => Path.Combine(C7Settings.WritableDirectory, "Invalid Maps");

	public static bool Enabled => C7Settings.GetSettingsValueOrDefault(Section, Key, "true") != "false";

	public static void SetEnabled(bool on) {
		C7Settings.SetValue(Section, Key, on ? "true" : "false");
		C7Settings.SaveSettings();
	}

	// The folder to hand to GameSetup.invalidMapsDirectory: null when they
	// aren't saved.
	public static string FolderIfEnabled => Enabled ? Folder : null;
}

using C7Engine;
using Godot;

// On a Mac the F keys control the computer (brightness, Mission Control and
// so on) and only reach the game with Fn held, so the advisors and report
// screens can't be opened by their Civ3 keys (F1 to F11). There they also
// open with Cmd and a number key: Cmd+1 to Cmd+0 for F1 to F10, and Cmd+-
// for F11. Not a Civ3 binding; the owner asked for Mac shortcuts
// (2026-10-12). Other platforms keep only the F keys.
public static class MacShortcuts {
	public static bool IsMac => OS.GetName() == "macOS";

	// The number keys, in the order of C7Action.AdvisorScreens.
	private static readonly Key[] Keys = [
		Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8, Key.Key9, Key.Key0, Key.Minus,
	];

	private static bool installed;

	// Adds the Cmd shortcuts to the advisor and report screen actions, on a
	// Mac, once.
	public static void Install() {
		if (installed || !IsMac) {
			return;
		}
		installed = true;
		for (int i = 0; i < C7Action.AdvisorScreens.Length && i < Keys.Length; i++) {
			InputMap.ActionAddEvent(C7Action.AdvisorScreens[i].action, new InputEventKey { MetaPressed = true, PhysicalKeycode = Keys[i] });
		}
	}
}

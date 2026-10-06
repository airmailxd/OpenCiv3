// The UI's static caches, some of which hold on to game objects. Cleared
// between games so a new game's textures are used and the old game can be
// let go.
public static class UICaches {
	public static void Clear() {
		Popup.ClearBackgroundCache();
		AdvisorHead.ClearCache();
		TechEffectLookup.ClearCache();
		TechBox.ClearCache();
	}
}

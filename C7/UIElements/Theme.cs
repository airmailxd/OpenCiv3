using Godot;

public static class ThemeFactory {

	static ThemeFactory() {
		defaultTheme = new Theme();
		defaultTheme.SetColor("caret_color", "LineEdit", Colors.Black);
	}

	private static Theme defaultTheme;

	public static Theme DefaultTheme {
		get => defaultTheme;
	}
}

// Fonts rendered at a fixed size. Setting FixedSize on a font changes it for
// everything that uses it, so each size gets its own copy of the font file,
// loaded once and shared by every screen that wants that size.
public static class FixedSizeFonts {
	private static readonly System.Collections.Generic.Dictionary<(string path, int size), FontFile> fonts = new();

	public static FontFile Get(string path, int fixedSize) {
		if (fonts.TryGetValue((path, fixedSize), out FontFile font) && GodotObject.IsInstanceValid(font)) {
			return font;
		}
		// Skip the resource cache, so changing the size doesn't affect other
		// code using the same font.
		font = ResourceLoader.Load<FontFile>(path, null, ResourceLoader.CacheMode.Ignore);
		font.FixedSize = fixedSize;
		fonts[(path, fixedSize)] = font;
		return font;
	}
}

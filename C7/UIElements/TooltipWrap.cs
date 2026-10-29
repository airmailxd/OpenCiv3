using Godot;

// Long tooltips wrap at a readable width instead of running across the
// screen on one line (owner's report, 2026-10-29: some menus' tooltips were
// excessively wide). Godot makes each tooltip a Label of the TooltipLabel
// theme type and sizes its popup from it, so a label wider than the limit is
// given the limit as its width and wraps, before the popup is sized.
public static class TooltipWrap {
	public const float MaxWidth = 420;

	public static void Install(SceneTree tree) {
		tree.NodeAdded += Wrap;
	}

	private static void Wrap(Node node) {
		if (node is not Label label || label.ThemeTypeVariation != "TooltipLabel" || string.IsNullOrEmpty(label.Text)) {
			return;
		}
		Font font = label.GetThemeFont("font");
		int size = label.GetThemeFontSize("font_size");
		if (font == null || font.GetMultilineStringSize(label.Text, HorizontalAlignment.Left, -1, size).X <= MaxWidth) {
			return;
		}
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(MaxWidth, 0);
	}
}

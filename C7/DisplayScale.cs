using System;
using System.Globalization;
using C7Engine;
using Godot;

// Scales the whole UI (and the map with it) by a user-chosen factor. The window
// renders at its full resolution while the game lays itself out in logical
// pixels of window size / scale, so a bigger scale means bigger, still-crisp art
// and text. The scale is kept in C7.ini and adjusted in-game with Ctrl + / - / 0.
// It also puts the window in the display mode chosen on the settings page,
// windowed (maximized, as project.godot opens it) or fullscreen.
public partial class DisplayScale : Node {
	private const string Section = "display";
	private const string Key = "uiScale";
	private const string FullscreenKey = "fullscreen";
	public const float Step = 0.25f;
	public const float Min = 0.5f;
	public const float Max = 4f;

	// The game's screens (the advisors, the city screen) are laid out for at
	// least this many logical pixels. In a window with less room than the
	// chosen scale leaves (a small window, or the game shown inside another
	// program's 1280 by 720 one), the scale applied is lowered to what fits,
	// and comes back when there is room again.
	private const float LayoutWidth = 1024f;
	private const float LayoutHeight = 768f;

	// The scale the player chose (or the screen's default); the window's own
	// may be lower, to fit.
	private float wanted;

	public override void _Ready() {
		Window window = GetTree().Root;
		window.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
		wanted = LoadScale();
		window.SizeChanged += Apply;
		Apply();
		if (Fullscreen) {
			window.Mode = Window.ModeEnum.Fullscreen;
		}
	}

	public override void _ExitTree() {
		GetTree().Root.SizeChanged -= Apply;
	}

	// Gives the window the chosen scale, or the largest that leaves the
	// screens their room if that is less.
	private void Apply() {
		Window window = GetTree().Root;
		Vector2I size = window.Size;
		float fits = size.X > 0 && size.Y > 0 ? Mathf.Min(size.X / LayoutWidth, size.Y / LayoutHeight) : wanted;
		float scale = Mathf.Clamp(Mathf.Min(wanted, fits), Min, Max);
		if (!Mathf.IsEqualApprox(window.ContentScaleFactor, scale)) {
			window.ContentScaleFactor = scale;
		}
	}

	// Whether the game fills the screen, with no window frame or taskbar
	// (Godot's borderless fullscreen), rather than a maximized window.
	public static bool Fullscreen => C7Settings.GetSettingsValueOrDefault(Section, FullscreenKey, "false") == "true";

	// Switches the window at once and remembers the choice for next time.
	// The UI scale is the window's, so it carries over: the layout just
	// gains or loses the room of the frame and taskbar.
	public void SetFullscreen(bool on) {
		C7Settings.SetValue(Section, FullscreenKey, on ? "true" : "false");
		C7Settings.SaveSettings();
		GetTree().Root.Mode = on ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Maximized;
	}

	// With no saved scale, fit the game's 1080-line layout to the screen height,
	// so a 1440p screen starts at 1.25 and a 4K screen at 2.
	private static float DefaultScale() {
		int screenHeight = DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen()).Y;
		return Mathf.Clamp(Mathf.Floor(screenHeight / 1080f / Step) * Step, 1f, Max);
	}

	private static float LoadScale() {
		string saved = C7Settings.GetSettingsValueOrDefault(Section, Key, null);
		if (saved != null && float.TryParse(saved, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale)) {
			return Mathf.Clamp(scale, Min, Max);
		}
		return DefaultScale();
	}

	public void SetScale(float scale) {
		scale = Mathf.Clamp(scale, Min, Max);
		wanted = scale;
		Apply();
		C7Settings.SetValue(Section, Key, scale.ToString(CultureInfo.InvariantCulture));
		C7Settings.SaveSettings();
	}

	// Goes back to the scale that fits the screen and forgets the saved one, so
	// the scale follows the screen again.
	private void ResetScale() {
		wanted = DefaultScale();
		Apply();
		C7Settings.RemoveValue(Section, Key);
		C7Settings.SaveSettings();
	}

	public override void _Input(InputEvent @event) {
		if (@event is not InputEventKey { Pressed: true, Echo: false, CtrlPressed: true } key) {
			return;
		}
		// From the scale in the window, so that a step is always seen (the
		// chosen scale may be more than fits).
		float current = GetTree().Root.ContentScaleFactor;
		switch (key.Keycode) {
			case Godot.Key.Equal or Godot.Key.Plus or Godot.Key.KpAdd:
				SetScale(current + Step);
				break;
			case Godot.Key.Minus or Godot.Key.KpSubtract:
				SetScale(current - Step);
				break;
			case Godot.Key.Key0 or Godot.Key.Kp0:
				ResetScale();
				break;
			default:
				return;
		}
		GetViewport().SetInputAsHandled();
	}
}

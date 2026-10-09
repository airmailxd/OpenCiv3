using System;
using System.Globalization;
using C7.UIElements;
using C7Engine.Network;
using Godot;
using Serilog;

// The settings page, opened from the main menu. Settings are saved to C7.ini
// as they're changed.
public partial class SettingsMenu : Control {
	private static readonly ILogger log = Log.ForContext<SettingsMenu>();

	private const string MainMenuScene = "res://UIElements/MainMenu/main_menu.tscn";

	private static readonly Color HeadingColor = new(0.55f, 0.08f, 0f);
	private static readonly Color TextColor = new(0f, 0f, 0f);
	private static readonly Color NoteColor = new(0.22f, 0.2f, 0.16f);

	private AudioManager audioManager;
	private DisplayScale displayScale;

	public override void _Ready() {
		audioManager = GetNodeOrNull<AudioManager>("/root/GlobalAudioManager");
		displayScale = GetNodeOrNull<DisplayScale>("/root/DisplayScale");
		try {
			Build();
		} catch (Exception ex) {
			log.Error(ex, "Could not set up the settings page");
			ShowBuildError(ex);
		}
	}

	// Replaces a page that couldn't be built with what went wrong and a way
	// back, rather than leaving it half built.
	private void ShowBuildError(Exception ex) {
		foreach (Node child in GetChildren()) {
			RemoveChild(child);
			child.QueueFree();
		}
		ColorRect black = new() { Color = Colors.Black };
		black.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(black);

		VBoxContainer box = new();
		box.SetAnchorsPreset(LayoutPreset.Center);
		AddChild(box);
		box.AddChild(new Label { Text = $"The settings couldn't be shown:\n{ex.Message}" });
		Button back = new() { Text = "Back to the menu" };
		back.Pressed += ReturnToMenu;
		box.AddChild(back);
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event.IsActionPressed("ui_cancel")) {
			ReturnToMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Build() {
		SetAnchorsPreset(LayoutPreset.FullRect);

		// The same backdrop as the main menu, letterboxed in black.
		ColorRect black = new() { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
		black.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(black);
		TextureRect background = new() {
			Texture = GD.Load<Texture2D>("res://title-screen.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);

		CenterContainer center = new();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(center);

		// A parchment panel, so the text reads clearly over the painting.
		PanelContainer panel = new();
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
			BgColor = new Color(0.93f, 0.88f, 0.76f, 0.97f),
			BorderColor = new Color(0.42f, 0.27f, 0.12f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ShadowColor = new Color(0, 0, 0, 0.5f),
			ShadowSize = 8,
		});
		center.AddChild(panel);

		MarginContainer margin = new();
		foreach (string side in new[] { "left", "right", "top", "bottom" }) {
			margin.AddThemeConstantOverride($"margin_{side}", 24);
		}
		panel.AddChild(margin);

		VBoxContainer column = new() { CustomMinimumSize = new Vector2(560, 0) };
		column.AddThemeConstantOverride("separation", 10);
		margin.AddChild(column);

		column.AddChild(MakeLabel("Settings", 28, HeadingColor, HorizontalAlignment.Center));

		AddGraphicsSettings(column);
		AddAudioSettings(column);
		AddOnlineSettings(column);
		AddMapGenerationSettings(column);

		column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
		Civ3MenuButton back = new() { Text = "Back to Main Menu", SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		back.Pressed += ReturnToMenu;
		column.AddChild(back);
	}

	private void AddGraphicsSettings(VBoxContainer column) {
		column.AddChild(MakeHeading("Graphics"));

		Civ3Checkbox modernGraphics = new() {
			Text = "Modern graphics (Graphics Overhaul)",
			FontSize = 16,
			ToggleMode = true,
			ButtonPressed = ModernGraphics.Enabled,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		modernGraphics.Toggled += (bool on) => {
			PlayClick();
			ModernGraphics.SetEnabled(on);
			log.Information("Modern graphics turned {State}", on ? "on" : "off");
		};
		column.AddChild(modernGraphics);
		column.AddChild(MakeNote(
			"Uses the remade, higher resolution map art: terrain, rivers, roads, mines, cities, resources " +
			"and units, redrawn with a modern look in the spirit of the originals. Art that hasn't been " +
			"remade, and the interface, keep the original look. Takes effect for the next game you start or load."));

		if (displayScale != null) {
			Window root = GetTree().Root;
			Label value = MakeLabel(FormatScale(root.ContentScaleFactor), 14, TextColor, HorizontalAlignment.Right);
			value.CustomMinimumSize = new Vector2(60, 0);
			Civ3HSlider slider = MakeSlider(DisplayScale.Min, DisplayScale.Max, DisplayScale.Step, root.ContentScaleFactor);

			// Rescaling the window while the slider is dragged would move the
			// slider out from under the mouse, so the scale is applied once it's
			// let go of.
			bool dragging = false;
			slider.DragStarted += () => dragging = true;
			slider.DragEnded += (bool changed) => {
				dragging = false;
				displayScale.SetScale((float)slider.Value);
			};
			slider.ValueChanged += (double v) => {
				value.Text = FormatScale((float)v);
				if (!dragging) {
					displayScale.SetScale((float)v);
				}
			};
			column.AddChild(MakeRow("Interface scale", slider, value));
			column.AddChild(MakeNote("Ctrl + and Ctrl - also change the scale, and Ctrl 0 fits it to the screen."));
		}
	}

	private void AddAudioSettings(VBoxContainer column) {
		if (audioManager == null) {
			return;
		}
		column.AddChild(MakeHeading("Audio"));

		foreach ((string key, string bus, string name) in AudioManager.VolumeSettings) {
			int volume = AudioManager.GetVolume(key);
			Label value = MakeLabel($"{volume}%", 14, TextColor, HorizontalAlignment.Right);
			value.CustomMinimumSize = new Vector2(60, 0);
			Civ3HSlider slider = MakeSlider(0, 100, 5, volume);
			slider.ValueChanged += (double v) => {
				value.Text = $"{(int)v}%";
				audioManager.SetVolume(key, bus, (int)v);
			};
			// Let the player hear the new interface volume.
			if (bus == AudioManager.UIAudioBus) {
				slider.DragEnded += (bool changed) => PlayClick();
			}
			column.AddChild(MakeRow(name, slider, value));
		}
	}

	private static string FormatScale(float scale) {
		return scale.ToString("0.00", CultureInfo.InvariantCulture) + "x";
	}

	// The relay server that games hosted and joined online go through.
	private void AddOnlineSettings(VBoxContainer column) {
		column.AddChild(MakeHeading("Online"));

		LineEdit relay = new() {
			Text = OnlineRelay.SameRelay(OnlineRelay.Url, OnlineRelay.DefaultUrl) ? "" : OnlineRelay.Url,
			PlaceholderText = "relay.example.org",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		Label result = MakeLabel("", 14, TextColor, HorizontalAlignment.Left);
		result.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		result.CustomMinimumSize = new Vector2(540, 0);

		void Save() {
			if (!string.IsNullOrWhiteSpace(relay.Text) && OnlineRelay.Problem(relay.Text) is string problem) {
				result.Text = problem;
				return;
			}
			OnlineRelay.SetUrl(relay.Text);
			log.Information("Online relay set to {Url}", OnlineRelay.Url);
			result.Text = string.IsNullOrWhiteSpace(relay.Text) ? "Using the default relay server." : "Saved.";
		}
		relay.TextSubmitted += _ => Save();
		relay.FocusExited += Save;

		Civ3MenuButton test = new() { Text = "Test", SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		test.Pressed += async () => {
			// An async void handler's exceptions would otherwise go unseen.
			try {
				PlayClick();
				Save();
				string url = OnlineRelay.Url;
				result.Text = "Checking...";
				string problem = await OnlineRelay.CheckAsync(url);
				if (IsInstanceValid(result)) {
					result.Text = problem ?? "The relay server answered. Games can be hosted and joined online through it.";
				}
			} catch (Exception ex) {
				log.Warning(ex, "Couldn't check the relay server");
				if (IsInstanceValid(result)) {
					result.Text = $"Couldn't check the relay server: {ex.Message}";
				}
			}
		};
		column.AddChild(MakeRow("Relay server", relay, test));
		column.AddChild(result);
		column.AddChild(MakeNote(
			"Games hosted online go through this server, which gives each a join code. A host can also choose " +
			"one when hosting; players joining that host's game are told it along with the code. Leave it blank " +
			"for the default."));
	}

	private void AddMapGenerationSettings(VBoxContainer column) {
		column.AddChild(MakeHeading("Map Generation"));

		Civ3Checkbox saveInvalidMaps = new() {
			Text = "Save invalid maps",
			FontSize = 16,
			ToggleMode = true,
			ButtonPressed = InvalidMaps.Enabled,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		saveInvalidMaps.Toggled += (bool on) => {
			PlayClick();
			InvalidMaps.SetEnabled(on);
			log.Information("Saving invalid maps turned {State}", on ? "on" : "off");
		};
		column.AddChild(saveInvalidMaps);
		column.AddChild(MakeNote(
			"When a new map has no room for every civilization to start far enough apart, another is generated. " +
			"With this on, each map turned down is saved to the Invalid Maps folder, with a note saying why, " +
			"so it can be loaded and looked at."));
	}

	private static Civ3HSlider MakeSlider(double min, double max, double step, double value) {
		Civ3HSlider slider = new() {
			MinValue = min,
			MaxValue = max,
			Step = step,
			Value = value,
			CustomMinimumSize = new Vector2(260, 24),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		return slider;
	}

	private static HBoxContainer MakeRow(string name, Control control, Control value) {
		HBoxContainer row = new();
		row.AddThemeConstantOverride("separation", 12);
		Label label = MakeLabel(name, 16, TextColor, HorizontalAlignment.Left);
		label.CustomMinimumSize = new Vector2(170, 0);
		row.AddChild(label);
		row.AddChild(control);
		row.AddChild(value);
		return row;
	}

	private static Label MakeHeading(string text) {
		Label heading = MakeLabel(text, 20, HeadingColor, HorizontalAlignment.Left);
		heading.CustomMinimumSize = new Vector2(0, 34);
		heading.VerticalAlignment = VerticalAlignment.Bottom;
		return heading;
	}

	private static Label MakeNote(string text) {
		Label note = MakeLabel(text, 12, NoteColor, HorizontalAlignment.Left);
		note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		note.CustomMinimumSize = new Vector2(540, 0);
		return note;
	}

	private static Label MakeLabel(string text, int size, Color color, HorizontalAlignment alignment) {
		Label label = new() {
			Text = text,
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private void PlayClick() {
		audioManager?.PlayUIAudio("buttons.button_1");
	}

	private void ReturnToMenu() {
		PlayClick();
		GetTree().ChangeSceneToFile(MainMenuScene);
	}
}

using System;
using System.Collections.Generic;
using Godot;

// The look of Civ3's Multiplayer Setup screen in OpenCiv3's style, shared by
// the screen the host sets a game up on and the lobby everyone waits in: a
// stone frame round the parchment, panels with spaced capital headings,
// parchment lists and buttons, and dark text fields.
public static class SetupStyle {
	// The frame grows with the window up to the largest size, and below
	// Civ3's own 1024x768 is laid out at that size and scaled down to fit.
	public static readonly Vector2 MinSize = new(1024, 768);
	public static readonly Vector2 MaxSize = new(1280, 960);

	// Where the frame's stone border ends (credits.background's), which the
	// title and the chat line sit on.
	public const int BorderSide = 56;
	public const int BorderTop = 84;
	public const int BorderBottom = 84;

	public static readonly Color Ink = Colors.Black;
	public static readonly Color Brown = new(0.47f, 0.39f, 0.27f);
	public static readonly Color DarkField = new(0.25f, 0.2f, 0.14f);
	public static readonly Color FieldText = new(0.98f, 0.95f, 0.85f);
	public static readonly Color ProblemColor = new(0.6f, 0.1f, 0.05f);
	public static readonly Color Faded = new(0.35f, 0.3f, 0.25f);

	// The stone frame, which Fit sizes to the window.
	public static Panel MakeFrame() {
		Panel frame = new();
		frame.AddThemeStyleboxOverride("panel", new StyleBoxTexture {
			Texture = TextureLoader.Load("credits.background"),
			TextureMarginLeft = BorderSide,
			TextureMarginRight = BorderSide,
			TextureMarginTop = BorderTop,
			TextureMarginBottom = BorderBottom,
		});
		return frame;
	}

	// Sizes the frame to the window, as described at MinSize.
	public static void Fit(Control frame, Vector2 view) {
		Vector2 size = new(Mathf.Clamp(view.X - 32, MinSize.X, MaxSize.X), Mathf.Clamp(view.Y - 32, MinSize.Y, MaxSize.Y));
		float scale = Mathf.Min(1, Mathf.Min(view.X / size.X, view.Y / size.Y));
		frame.Size = size;
		frame.Scale = new Vector2(scale, scale);
		frame.Position = ((view - size * scale) / 2).Floor();
	}

	// The panels' row inside the frame's border.
	public static HBoxContainer MakeBody(Control frame) {
		HBoxContainer body = new();
		body.AddThemeConstantOverride("separation", 10);
		body.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		body.OffsetLeft = BorderSide + 8;
		body.OffsetRight = -BorderSide - 8;
		body.OffsetTop = BorderTop + 6;
		body.OffsetBottom = -BorderBottom - 6;
		frame.AddChild(body);
		return body;
	}

	// Civ3's headings are capitals spaced wide.
	public static Font SpacedFont(Control fontSource, int spacing) {
		return new FontVariation { BaseFont = fontSource.GetThemeDefaultFont(), SpacingGlyph = spacing };
	}

	public static Label MakeHeading(Control fontSource, string text, int size = 15) {
		Label label = new() { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
		label.AddThemeFontOverride("font", SpacedFont(fontSource, 3));
		label.AddThemeFontSizeOverride("font_size", size);
		return label;
	}

	public static StyleBoxFlat Box(Color background, Color border, int borderWidth = 1, int margin = 4) {
		return new StyleBoxFlat {
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			ContentMarginLeft = margin + 2,
			ContentMarginRight = margin + 2,
			ContentMarginTop = margin,
			ContentMarginBottom = margin,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
		};
	}

	// A parchment banner on the stone above the panels, as Civ3's title has.
	public static void AddTitle(Control frame, Control fontSource, string text) {
		PanelContainer banner = new();
		StyleBoxFlat style = Box(new Color(0.97f, 0.93f, 0.8f), Brown, 2, 2);
		style.ContentMarginLeft = 28;
		style.ContentMarginRight = 28;
		style.ShadowColor = new Color(0, 0, 0, 0.35f);
		style.ShadowSize = 3;
		banner.AddThemeStyleboxOverride("panel", style);
		Label title = new() { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontOverride("font", SpacedFont(fontSource, 6));
		title.AddThemeFontSizeOverride("font_size", 26);
		banner.AddChild(title);
		CenterContainer center = new();
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		center.OffsetBottom = BorderTop;
		center.AddChild(banner);
		frame.AddChild(center);
	}

	// A panel of the screen, with its heading: lighter parchment on the
	// frame's, bordered and shadowed like the world setup screen's boxes.
	public static (PanelContainer panel, VBoxContainer box) MakePanel(Control fontSource, string heading) {
		PanelContainer panel = new();
		StyleBoxFlat style = Box(new Color(0.99f, 0.96f, 0.88f, 0.92f), Brown, 2, 6);
		style.ShadowColor = new Color(0.2f, 0.14f, 0.06f, 0.35f);
		style.ShadowSize = 3;
		style.ShadowOffset = new Vector2(2, 2);
		panel.AddThemeStyleboxOverride("panel", style);
		VBoxContainer box = new();
		box.AddThemeConstantOverride("separation", 3);
		panel.AddChild(box);
		box.AddChild(MakeHeading(fontSource, heading));
		return (panel, box);
	}

	// A list to choose from, in parchment with a dark arrow, as Civ3's are.
	public static OptionButton MakeDropdown(string tooltip = null) {
		OptionButton dropdown = new() {
			TooltipText = tooltip ?? "",
			ClipText = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 22),
		};
		StyleBoxFlat normal = Box(new Color(0.93f, 0.88f, 0.75f), Brown, 1, 2);
		StyleBoxFlat hover = Box(new Color(0.97f, 0.93f, 0.82f), Brown, 1, 2);
		StyleBoxFlat disabled = Box(new Color(0.9f, 0.87f, 0.8f, 0.6f), new Color(Brown, 0.5f), 1, 2);
		dropdown.AddThemeStyleboxOverride("normal", normal);
		dropdown.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		dropdown.AddThemeStyleboxOverride("hover", hover);
		dropdown.AddThemeStyleboxOverride("pressed", hover);
		dropdown.AddThemeStyleboxOverride("disabled", disabled);
		foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) {
			dropdown.AddThemeColorOverride(color, Ink);
		}
		dropdown.AddThemeColorOverride("font_disabled_color", Faded);
		// The arrow takes the text's color, dark on the parchment.
		dropdown.AddThemeConstantOverride("modulate_arrow", 1);
		dropdown.AddThemeFontSizeOverride("font_size", 13);
		PopupMenu popup = dropdown.GetPopup();
		popup.SetTransparentBackground(false);
		popup.MaxSize = new Vector2I(popup.MaxSize.X, 320);
		return dropdown;
	}

	public static void AddItems(OptionButton dropdown, IEnumerable<string> items, int selected) {
		foreach (string item in items) {
			dropdown.AddItem(item);
		}
		for (int i = 0; i < dropdown.ItemCount; ++i) {
			dropdown.GetPopup().SetItemAsRadioCheckable(i, false);
		}
		dropdown.Select(Math.Clamp(selected, 0, dropdown.ItemCount - 1));
	}

	// A value that can't be changed here, in a box shaped like a list's, as
	// Civ3 shows a setting to those who can't change it.
	public static Label MakeValue(string text, string tooltip = null) {
		Label value = new() {
			Text = text,
			TooltipText = tooltip ?? "",
			ClipText = true,
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 22),
			MouseFilter = Control.MouseFilterEnum.Pass,
		};
		value.AddThemeStyleboxOverride("normal", Box(new Color(0.93f, 0.88f, 0.75f, 0.7f), new Color(Brown, 0.6f), 1, 2));
		value.AddThemeColorOverride("font_color", Ink);
		value.AddThemeFontSizeOverride("font_size", 13);
		return value;
	}

	// A dark text field, as Civ3's game name and chat line are.
	public static LineEdit MakeDarkField() {
		LineEdit field = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		StyleBoxFlat style = Box(DarkField, Brown, 1, 3);
		field.AddThemeStyleboxOverride("normal", style);
		field.AddThemeStyleboxOverride("focus", style);
		field.AddThemeStyleboxOverride("read_only", Box(new Color(DarkField, 0.75f), Brown, 1, 3));
		field.AddThemeColorOverride("font_color", FieldText);
		field.AddThemeColorOverride("font_uneditable_color", new Color(FieldText, 0.55f));
		field.AddThemeColorOverride("font_placeholder_color", new Color(FieldText, 0.45f));
		field.AddThemeColorOverride("caret_color", FieldText);
		field.AddThemeFontSizeOverride("font_size", 14);
		return field;
	}

	public static Button MakeButton(string text, string tooltip, bool highlighted = false) {
		Button button = new() { Text = text, TooltipText = tooltip ?? "", CustomMinimumSize = new Vector2(96, 26) };
		// Launch has the light border Civ3 gives it.
		Color border = highlighted ? new Color(1f, 0.98f, 0.9f) : Brown;
		int width = highlighted ? 2 : 1;
		Color background = highlighted ? new Color(0.83f, 0.74f, 0.55f) : new Color(0.9f, 0.84f, 0.7f);
		button.AddThemeStyleboxOverride("normal", Box(background, border, width, 3));
		button.AddThemeStyleboxOverride("hover", Box(background.Lightened(0.15f), border, width, 3));
		button.AddThemeStyleboxOverride("pressed", Box(background.Darkened(0.1f), border, width, 3));
		button.AddThemeStyleboxOverride("disabled", Box(new Color(background, 0.5f), new Color(border, 0.5f), width, 3));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.35f, 0.3f));
		button.AddThemeFontSizeOverride("font_size", 14);
		return button;
	}

	// A small button, for what can be done to one player's row.
	public static Button MakeSmallButton(string text, string tooltip, Action onPressed) {
		Button button = MakeButton(text, tooltip);
		button.CustomMinimumSize = new Vector2(0, 22);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		button.Pressed += onPressed;
		return button;
	}

	public static Civ3Checkbox MakeRule(string text, bool on, string tooltip, bool available = true) {
		Civ3Checkbox checkbox = new() {
			Text = text,
			FontSize = 13,
			ButtonPressed = on,
			TooltipText = tooltip ?? "",
			Disabled = !available,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		if (!available) {
			checkbox.Modulate = new Color(1, 1, 1, 0.5f);
		}
		return checkbox;
	}

	// A setting's name, over its list or value.
	public static Label MakeSettingLabel(string text) {
		Label label = new() { Text = text, MouseFilter = Control.MouseFilterEnum.Pass };
		label.AddThemeFontSizeOverride("font_size", 12);
		return label;
	}

	// The messages box's text.
	public static RichTextLabel MakeMessages() {
		RichTextLabel messages = new() {
			BbcodeEnabled = true,
			ScrollFollowing = true,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 60),
			SelectionEnabled = true,
		};
		messages.AddThemeColorOverride("default_color", Ink);
		messages.AddThemeFontSizeOverride("normal_font_size", 13);
		return messages;
	}

	public static void AddMessage(RichTextLabel messages, string text, Color? color = null) {
		string escaped = text.Replace("[", "[lb]");
		messages.AppendText(color is Color c ? $"[color=#{c.ToHtml(false)}]{escaped}[/color]\n" : escaped + "\n");
	}

	// The chat line and the exit button, on the frame's bottom border. Chat
	// isn't in OpenCiv3 yet, so the line can't be typed in.
	public static void AddBottomStrip(Control frame, Action exit) {
		HBoxContainer strip = new();
		strip.AddThemeConstantOverride("separation", 12);
		strip.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		strip.OffsetLeft = BorderSide + 8;
		strip.OffsetRight = -BorderSide + 24;
		strip.OffsetTop = -BorderBottom + 24;
		strip.OffsetBottom = -22;
		frame.AddChild(strip);

		LineEdit chat = MakeDarkField();
		chat.Editable = false;
		chat.PlaceholderText = "Chat isn't in OpenCiv3 yet.";
		chat.TooltipText = "Players can't chat before or during a game yet. Messages above show what happens on this screen.";
		chat.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		strip.AddChild(chat);

		TextureButton exitButton = new() {
			TooltipText = "Back to the main menu",
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			Shortcut = new Shortcut { Events = [new InputEventKey { Keycode = Key.Escape }] },
		};
		TextureLoader.SetButtonTextures(exitButton, "ui.exit");
		exitButton.Pressed += exit;
		strip.AddChild(exitButton);
	}
}

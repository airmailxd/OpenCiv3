using System;
using Godot;

// A full-screen curtain shown between human players' turns in a hotseat
// game. It hides the map so the next player doesn't see the previous
// player's view, and waits for them to say they're at the screen.
public partial class HotseatHandoff : ColorRect {
	private readonly string title;
	private readonly string message;
	private readonly string buttonText;
	private readonly Action onContinue;

	public HotseatHandoff(string title, string message, string buttonText, Action onContinue) {
		this.title = title;
		this.message = message;
		this.buttonText = buttonText;
		this.onContinue = onContinue;
	}

	public override void _Ready() {
		Color = Colors.Black;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		CenterContainer center = new();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(center);

		VBoxContainer box = new();
		box.AddThemeConstantOverride("separation", 24);
		center.AddChild(box);

		Label titleLabel = new() {
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 36);
		box.AddChild(titleLabel);

		Label messageLabel = new() {
			Text = message,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		messageLabel.AddThemeFontSizeOverride("font_size", 20);
		box.AddChild(messageLabel);

		Button button = new() {
			Text = buttonText,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(200, 48),
		};
		button.AddThemeFontSizeOverride("font_size", 20);
		button.Pressed += Continue;
		box.AddChild(button);

		button.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void Continue() {
		QueueFree();
		// Defer the callback until the end of the frame. Otherwise the key
		// press that activated the button (e.g. Enter) would still read as
		// "just pressed" by the game's input polling once the player's turn
		// has started, and could immediately trigger an action such as
		// ending the turn.
		Callable.From(onContinue).CallDeferred();
	}
}

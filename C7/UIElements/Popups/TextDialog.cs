using Godot;
using System;

public partial class TextDialog : Popup {
	LineEdit textEditBox = new LineEdit();
	private string header;
	private string prompt;
	private string defaultText;
	// How the prompt and text box line up inside the dialog; the base
	// `alignment` is where the dialog itself sits on the screen.
	private BoxContainer.AlignmentMode textAlignment;
	Action<string> handleText;
	// Run on Cancel or Escape, if given; otherwise they just close the
	// dialog.
	Action cancelled;

	public TextDialog(string header,
					  string prompt,
					  string defaultText,
					  BoxContainer.AlignmentMode alignment,
					  Action<string> handleText,
					  Action cancelled = null) {
		this.cancelled = cancelled;
		this.defaultText = defaultText;
		this.prompt = prompt;
		this.header = header;
		this.handleText = handleText;
		textAlignment = alignment;

		textEditBox.Theme = ThemeFactory.DefaultTheme;
		textEditBox.CaretBlink = true;
		base.alignment = BoxContainer.AlignmentMode.End;
		margins = new Margins(right: -10); // 10px margin from the right
	}

	public override void _Ready() {
		base._Ready();

		AddTexture(530, 260);
		AddBackground(530, 150, 110);
		AddHeader(header, 120);

		HBoxContainer labelAndTextBox = new HBoxContainer();
		labelAndTextBox.Alignment = textAlignment;
		labelAndTextBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		labelAndTextBox.SizeFlagsStretchRatio = 1;
		labelAndTextBox.AnchorLeft = 0.0f;
		labelAndTextBox.AnchorRight = 0.85f;
		labelAndTextBox.SetPosition(new Vector2(30, 170));

		Label promptLabel = new Label();
		promptLabel.Text = prompt;
		labelAndTextBox.AddChild(promptLabel);

		textEditBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		textEditBox.SizeFlagsStretchRatio = 1;
		textEditBox.Text = defaultText;
		labelAndTextBox.AddChild(textEditBox);

		this.AddChild(labelAndTextBox);

		textEditBox.SelectAll();
		textEditBox.GrabFocus();

		textEditBox.TextSubmitted += HandleTextInput;

		AddConfirmButton(new Vector2(475, 213), () => { HandleTextInput(textEditBox.Text); });
		if (cancelled == null) {
			AddCancelButton(new Vector2(500, 213));
		} else {
			AddCancelButton(new Vector2(500, 213), Cancel);
		}
	}

	public override bool OnEscape() {
		if (cancelled == null) {
			return true;
		}
		Cancel();
		return false;
	}

	private void Cancel() {
		GetViewport().SetInputAsHandled();
		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
		cancelled();
	}

	private void HandleTextInput(string text) {
		GetViewport().SetInputAsHandled();
		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
		handleText(text);
	}
}

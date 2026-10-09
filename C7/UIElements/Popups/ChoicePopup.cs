using Godot;
using System;
using System.Collections.Generic;

// A popup with a header, a message and a list of choices. A choice without an
// action is shown greyed out, as an option that isn't available.
public partial class ChoicePopup : Popup {
	public class Choice {
		public string label;
		public Action action;

		public Choice(string label, Action action) {
			this.label = label;
			this.action = action;
		}
	}

	private readonly string header;
	private readonly string message;
	private readonly List<Choice> choices;
	private readonly bool cancellable;

	// A popup that isn't cancellable must be answered, so Escape picks its last
	// available choice, which should be the most neutral one (like abstaining).
	public ChoicePopup(string header, string message, List<Choice> choices, bool cancellable = true) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins(top: 100);
		this.header = header;
		this.message = message;
		this.choices = choices;
		this.cancellable = cancellable;
	}

	public override bool OnEscape() {
		if (cancellable) {
			return true;
		}
		Choice last = choices.FindLast(c => c.action != null);
		if (last != null) {
			GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
			last.action();
			return false;
		}
		return true;
	}

	public override void _Ready() {
		base._Ready();

		const int width = 560;
		const int rowHeight = 25;
		int messageLines = string.IsNullOrEmpty(message) ? 0 : message.Split('\n').Length;
		int height = 110 + 20 * messageLines + rowHeight * choices.Count + (cancellable ? 30 : 0);
		AddTexture(width, height);
		AddBackground(width, height);
		AddHeader(header, 10);

		int vOffset = 55;
		if (messageLines > 0) {
			Label messageLabel = new() { Text = message };
			messageLabel.SetPosition(new Vector2(25, vOffset));
			AddChild(messageLabel);
			vOffset += 20 * messageLines + 10;
		}

		foreach (Choice choice in choices) {
			if (choice.action == null) {
				Label unavailable = new() {
					Text = choice.label,
					Modulate = new Color(0.4f, 0.4f, 0.4f),
				};
				unavailable.SetPosition(new Vector2(30, vOffset));
				AddChild(unavailable);
			} else {
				Action action = choice.action;
				AddButton(choice.label, vOffset, () => {
					GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
					action();
				});
			}
			vOffset += rowHeight;
		}

		if (cancellable) {
			AddCancelButton(new Vector2(width - 30, height - 47));
		}
	}
}

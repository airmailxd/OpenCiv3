using Godot;
using System;
using System.Diagnostics;
using C7GameData;
using Serilog;

// A generic popup for some sort of information.
public partial class InformationalPopup : Popup {
	string message;
	AdvisorHead.Advisor advisor;
	AdvisorHead.Mood mood;

	public InformationalPopup(string message, AdvisorHead.Advisor advisor = AdvisorHead.Advisor.Foreign, AdvisorHead.Mood mood = AdvisorHead.Mood.Happy) {
		alignment = BoxContainer.AlignmentMode.End;
		margins = new Margins(right: 10);
		this.message = message;
		this.advisor = advisor;
		this.mood = mood;
	}

	public override void _Ready() {
		base._Ready();

		int width = 430;
		int height = 230;

		// The message wraps to the box's width, and the box grows taller
		// for messages longer than two lines.
		Label messageLabel = new();
		messageLabel.Text = message;
		messageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		messageLabel.SetPosition(new Vector2(25, 160));
		messageLabel.Size = new Vector2(width - 50, 0);
		// In the tree, so it's measured in the theme's font.
		AddChild(messageLabel);
		int extraLines = Math.Max(0, messageLabel.GetLineCount() - 2);
		height += extraLines * messageLabel.GetLineHeight();

		TextureRect advisorHead = new();
		advisorHead.Texture = AdvisorHead.GetPopupImage(advisor, mood, eraIndex: 0);
		advisorHead.SetPosition(new Vector2(275, 0));
		AddChild(advisorHead);

		AddTexture(width, height);
		AddBackground(width, height - 110, 110);
		AddHeader(HeaderFor(advisor), 120);

		// Drawn over the background.
		MoveChild(messageLabel, -1);

		AddConfirmButton(new Vector2(width - 40, height - 40), () => {
			GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
		});
	}

	private static string HeaderFor(AdvisorHead.Advisor advisor) {
		return advisor switch {
			AdvisorHead.Advisor.Culture => "Cultural Advisor",
			_ => $"{advisor} Advisor",
		};
	}
}

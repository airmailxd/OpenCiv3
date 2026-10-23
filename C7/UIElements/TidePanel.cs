using C7Engine;
using C7GameData;
using Godot;

// Rising Tide's warning (see C7Engine.RisingTide): when the next Tidal Wave
// strikes, in a box in the top right corner under the toolbar, to the left
// of the scoreboard when there is one.
public partial class TidePanel : PanelContainer {
	// How often the countdown is read again, in seconds.
	private const double RefreshInterval = 0.25;
	private const float Gap = 8;

	private static readonly Color PanelColor = new(0.62f, 0.78f, 0.86f);
	private static readonly Color BorderColor = new(0.12f, 0.24f, 0.34f);
	private static readonly Color TextColor = new(0.04f, 0.08f, 0.14f);
	private static readonly Color UrgentColor = new(0.70f, 0.10f, 0.08f);

	private Label label;
	private double sinceRefresh = RefreshInterval;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Pass;
		StyleBoxFlat box = new() { BgColor = PanelColor, BorderColor = BorderColor };
		box.SetBorderWidthAll(2);
		box.SetContentMarginAll(6);
		AddThemeStyleboxOverride("panel", box);
		label = new Label { TooltipText = "The sea turns every tile of shore to Coast when a Tidal Wave strikes; cities there are lost.", MouseFilter = MouseFilterEnum.Pass };
		label.AddThemeColorOverride("font_color", TextColor);
		label.AddThemeFontSizeOverride("font_size", 15);
		AddChild(label);
	}

	public override void _Process(double delta) {
		PlaceBesideScoreboard();
		sinceRefresh += delta;
		if (sinceRefresh < RefreshInterval) {
			return;
		}
		sinceRefresh = 0;
		EngineStorage.ReadGameData(Refresh);
	}

	private void Refresh(GameData gameData) {
		int turns = RisingTide.TurnsUntilNextWave(gameData);
		Visible = turns >= 0;
		label.Text = turns switch {
			0 => "Tidal Wave this turn!",
			1 => "Next Tidal Wave: next turn",
			_ => $"Next Tidal Wave: {turns} turns",
		};
		label.AddThemeColorOverride("font_color", turns <= 1 ? UrgentColor : TextColor);
	}

	// Stays to the left of the scoreboard, which may grow or fold.
	private void PlaceBesideScoreboard() {
		Scoreboard scoreboard = null;
		foreach (Node sibling in GetParent().GetChildren()) {
			if (sibling is Scoreboard s && s.Visible) {
				scoreboard = s;
			}
		}
		float right = scoreboard == null ? 0 : -(scoreboard.Size.X + Gap);
		OffsetRight = right;
		OffsetLeft = right;
	}
}

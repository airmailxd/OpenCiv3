using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// Kings & Nomads' city-states at a glance (owner's request, 2026-10-25): how
// strong our units are against them (see CityStates.StrengthAgainstCityStates),
// and our trade routes with them and what each brings (see
// CityStateTrade.Routes). In the top right corner under the toolbar, left of
// the scoreboard when there is one; open at the start, and it folds down to
// its heading.
public partial class CityStatesPanel : PanelContainer {
	// How often it is read again, in seconds.
	private const double RefreshInterval = 0.5;
	private const float Gap = 8;
	private const int Width = 300;

	private static readonly Color PanelColor = new(0.80f, 0.76f, 0.58f);
	private static readonly Color BorderColor = new(0.35f, 0.28f, 0.15f);
	private static readonly Color TextColor = new(0.12f, 0.09f, 0.04f);
	private static readonly Color MutedColor = new(0.35f, 0.29f, 0.18f);

	private Button foldButton;
	private VBoxContainer body;
	private Label strength;
	private VBoxContainer routes;
	private bool folded = false;
	private double sinceRefresh = RefreshInterval;
	// What was last shown, so the rows are only rebuilt when it changes.
	private string shown;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Stop;
		StyleBoxFlat box = new() { BgColor = PanelColor, BorderColor = BorderColor };
		box.SetBorderWidthAll(2);
		box.SetContentMarginAll(6);
		AddThemeStyleboxOverride("panel", box);

		VBoxContainer layout = new() { CustomMinimumSize = new Vector2(Width, 0) };
		layout.AddThemeConstantOverride("separation", 4);
		AddChild(layout);

		HBoxContainer header = new();
		layout.AddChild(header);
		Label title = MakeLabel("City-States", 15, TextColor);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		// Beside the fold button, so not the panel's full width.
		title.CustomMinimumSize = Vector2.Zero;
		header.AddChild(title);
		foldButton = new Button { CustomMinimumSize = new Vector2(28, 22), FocusMode = FocusModeEnum.None };
		foldButton.AddThemeFontSizeOverride("font_size", 12);
		foldButton.Pressed += ToggleFolded;
		header.AddChild(foldButton);

		body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 3);
		layout.AddChild(body);
		strength = MakeLabel("", 13, TextColor);
		strength.TooltipText = "How strong your units are against city-states' units, attacking or defending. "
			+ "It falls with each city-state you take: 2x before your first, 1.5x, 0.9x, then 0.5x.";
		body.AddChild(strength);
		body.AddChild(new HSeparator());
		routes = new VBoxContainer();
		routes.AddThemeConstantOverride("separation", 2);
		body.AddChild(routes);
		ShowFolded();
		// Filled in at once: a popup open at the start (the scenario's
		// introduction) holds back _Process until it closes.
		EngineStorage.ReadGameData(Refresh);
	}

	private static Label MakeLabel(string text, int size, Color color) {
		// The panel's width, so a wrapping label new to the panel isn't
		// first measured a word to a line, which stretched the panel down
		// the screen when the rows were rebuilt (as when a city-state was
		// taken).
		Label label = new() {
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Pass,
			CustomMinimumSize = new Vector2(Width, 0),
		};
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeFontSizeOverride("font_size", size);
		return label;
	}

	private void ToggleFolded() {
		folded = !folded;
		ShowFolded();
	}

	private void ShowFolded() {
		body.Visible = !folded;
		foldButton.Text = folded ? "▼" : "▲";
		foldButton.TooltipText = folded ? "Show your strength against city-states and your trade with them" : "Fold";
		// Shrink to fit when folded.
		Size = Vector2.Zero;
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
		Player player = gameData.GetPlayer(EngineStorage.uiControllerID);
		Visible = CityStateTrade.IsOn(gameData) && player != null && !player.IsIndependent;
		if (!Visible) {
			return;
		}
		int taken = player.cityStatesCaptured;
		double factor = CityStates.StrengthAgainstCityStates(taken);
		string strengthText = $"Strength against city-states: {factor:0.##}x ({(taken == 0 ? "none taken yet" : $"{taken} taken")})";

		List<CityStateTrade.Route> list = CityStateTrade.Routes(gameData, player);
		List<string> lines = list.Select(r => {
			string yields = $"+{r.gold} gold, +{r.beakers} beakers, +{r.Shields} shields";
			string resources = r.resources.Count == 0 ? "" : $"; {string.Join(", ", r.resources.Select(x => x.Name))}";
			return $"{r.cityState.name} (size {r.cityState.residents.Count}): {yields}{resources}";
		}).ToList();
		string total = list.Count == 0 ? "" : $"Total: +{list.Sum(r => r.gold)} gold, +{list.Sum(r => r.beakers)} beakers, +{list.Sum(r => r.Shields)} shields a turn";

		string now = strengthText + "|" + string.Join("|", lines) + "|" + total;
		if (now == shown) {
			return;
		}
		shown = now;
		strength.Text = strengthText;
		foreach (Node child in routes.GetChildren()) {
			child.QueueFree();
		}
		if (list.Count == 0) {
			routes.AddChild(MakeLabel("No trade routes. A road from one of your cities to a city-state makes one.", 12, MutedColor));
		} else {
			routes.AddChild(MakeLabel("Trade routes, each turn:", 13, TextColor));
			foreach (string line in lines) {
				routes.AddChild(MakeLabel(line, 12, TextColor));
			}
			routes.AddChild(MakeLabel(total, 12, TextColor));
		}
		Size = Vector2.Zero;
		// Again once the new rows are laid out, and the old ones gone.
		Callable.From(() => Size = Vector2.Zero).CallDeferred();
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

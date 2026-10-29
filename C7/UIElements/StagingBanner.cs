using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Godot;

// Staging (see C7Engine.Staging): while other humans place the units they
// won in the Mercenary Market and this player has none left to place, a
// banner on Civ3's popup parchment at the top of the screen says so, and who
// it waits for (owner's request, 2026-10-28). No one moves until they're done.
public partial class StagingBanner : Control {
	// How often it is read again, in seconds.
	private const double RefreshInterval = 0.5;
	private const int Width = 460;
	private const int Height = 96;
	// Below the toolbar and the LAN waiting strip.
	private const int Top = 120;

	private static readonly Color TextColor = new(0.12f, 0.09f, 0.04f);

	private Label waitingFor;
	private double sinceRefresh = RefreshInterval;

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(Width, Height);
		Size = new Vector2(Width, Height);
		SetAnchorsPreset(LayoutPreset.CenterTop);
		OffsetLeft = -Width / 2;
		OffsetRight = Width / 2;
		OffsetTop = Top;
		OffsetBottom = Top + Height;

		TextureRect background = Popup.CreateBackground(Width, Height);
		background.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(background);

		VBoxContainer text = new() { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		text.SetAnchorsPreset(LayoutPreset.FullRect);
		text.AddThemeConstantOverride("separation", 2);
		AddChild(text);
		text.AddChild(MakeLabel("Staging in Progress", 22));
		waitingFor = MakeLabel("", 14);
		text.AddChild(waitingFor);
		Visible = false;
	}

	private static Label MakeLabel(string text, int size) {
		Label label = new() {
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(Width - 40, 0),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeColorOverride("font_color", TextColor);
		label.AddThemeFontSizeOverride("font_size", size);
		return label;
	}

	public override void _Process(double delta) {
		sinceRefresh += delta;
		if (sinceRefresh < RefreshInterval) {
			return;
		}
		sinceRefresh = 0;
		EngineStorage.ReadGameData(Refresh);
	}

	private void Refresh(GameData gameData) {
		List<Player> placing = Placing(gameData);
		Player player = gameData.GetPlayer(EngineStorage.uiControllerID);
		Visible = placing.Count > 0 && (player == null || !placing.Contains(player));
		if (!Visible) {
			return;
		}
		string names = string.Join(", ", placing.Select(p => $"{p.name ?? p.civilization.leader} of the {p.civilization.noun}"));
		waitingFor.Text = $"Waiting for {names} to place their units";
	}

	// The humans yet to place a unit they won, once the market has closed.
	private static List<Player> Placing(GameData gameData) {
		if (gameData.rules?.StagingUnits != true || gameData.staging == null || gameData.staging.bidding.Count > 0) {
			return [];
		}
		return gameData.players.Where(p => Staging.IsWaitingFor(gameData, p)).ToList();
	}
}

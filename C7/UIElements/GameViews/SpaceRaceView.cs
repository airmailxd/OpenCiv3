using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Godot;

[GlobalClass]
[Tool]
public partial class SpaceRaceView : Control {

	[Export] public TextureRect background;

	private TextureButton _close;

	public SpaceRaceView() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		this.CreateUI();
	}

	private void CreateUI() {
		background.Texture = TextureLoader.Load("screens.space_race.background");

		_close = AdvisorUtils.CreateExitButton(background);
		_close.Pressed += () => { this.GetParent<GameViews>().Hide(); };

		AdvisorUtils.CreateAdvisorTitle(background, background.Texture.GetWidth(), "SPACESHIP");

		// The summary sits on a sheet of paper of its own: the standalone
		// background is a dark, busy blueprint the text couldn't be read on.
		PanelContainer sheet = new() { Position = new Vector2(60, 80) };
		sheet.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
			BgColor = new Color(0.96f, 0.93f, 0.84f, 0.93f),
			BorderColor = new Color(0.35f, 0.27f, 0.18f),
			BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
			ContentMarginLeft = 14, ContentMarginTop = 10, ContentMarginRight = 14, ContentMarginBottom = 10,
		});
		_status = new Label {
			CustomMinimumSize = new Vector2(480, 0),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_status.AddThemeFontSizeOverride("font_size", 16);
		_status.AddThemeColorOverride("font_color", new Color(0.1f, 0.08f, 0.05f));
		sheet.AddChild(_status);
		background.AddChild(sheet);
	}

	private Label _status;

	public void ShowView() {
		Show();

		EngineStorage.ReadGameData((GameData gameData) => {
			Player player = gameData.GetUIControllerPlayer();
			_status.Text = DescribeSpaceRace(gameData, player);
		});
	}

	// A text summary of the player's spaceship and the rivals' progress.
	private static string DescribeSpaceRace(GameData gameData, Player player) {
		System.Text.StringBuilder sb = new();
		if (!SpaceRace.SpaceRaceAllowed(gameData)) {
			sb.AppendLine("The space race victory is disabled in this game.");
			return sb.ToString();
		}
		if (!SpaceRace.ApolloProgramBuilt(gameData)) {
			sb.AppendLine("No civilization has completed the Apollo Program yet. Spaceship parts can be built once it is.");
			sb.AppendLine();
		}

		(int built, int needed) = SpaceRace.Progress(gameData, player);
		sb.AppendLine($"Our spaceship: {built} of {needed} parts");
		foreach (Building part in SpaceRace.PartTypes(gameData)) {
			int have = SpaceRace.PartsBuilt(player, part.spaceshipPart);
			int need = SpaceRace.PartsNeeded(gameData, part.spaceshipPart);
			sb.AppendLine($"    {part.name}: {have} / {need}");
		}

		List<Player> rivals = gameData.GetKnownRivals(player);
		if (rivals.Count > 0) {
			sb.AppendLine();
			sb.AppendLine("Rival spaceships:");
			foreach (Player rival in rivals) {
				(int rivalBuilt, int rivalNeeded) = SpaceRace.Progress(gameData, rival);
				sb.AppendLine($"    {rival.civilization.name}: {rivalBuilt} of {rivalNeeded} parts");
			}
		}
		return sb.ToString();
	}
}

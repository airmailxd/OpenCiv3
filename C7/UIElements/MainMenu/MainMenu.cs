using Godot;
using System;
using C7Engine;
using Serilog;

public partial class MainMenu : Node {
	private ILogger log;

	[Export]
	Civ3FileDialog LoadDialog;
	[Export]
	Control NoCiv3Options;
	[Export]
	FileDialog SetCiv3HomeDialog;
	[Export]
	Civ3FileDialog LoadScenarioDialog;
	[Export]
	MenuButtonContainer ButtonContainer;

	GlobalSingleton Global;
	AudioManager AudioManager;

	public override void _Ready() {
		log = LogManager.ForContext<MainMenu>();
		log.Debug("enter MainMenu._Ready");

		DisplayServer.WindowSetTitle((string)ProjectSettings.GetSetting("application/config/name"));

		try {
			DisplayTitleScreen();

			// A load given up on hosts nothing: otherwise the next game
			// started from the menu would be hosted on the LAN.
			LoadDialog.Canceled += () => LanSession.HostNextGame = false;

			AudioManager = GetNode<AudioManager>("/root/GlobalAudioManager");
			PlayMusic();

			if (LanSession.TakeDevStart()) {
				CallDeferred(nameof(StartDevLanGame));
			}
		} catch (Exception ex) {
			log.Error(ex, "Could not set up the main menu");
		}
	}

	private void DisplayTitleScreen() {
		// To pass data between scenes, putting path string in a global singleton and reading it later in createGame
		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		Global.ResetLoadGameFields();

		// Let go of anything the UI cached from the last game.
		UICaches.Clear();

		// Back at the menu, any LAN game is over.
		LanSession.End();
		LanSession.HostNextGame = false;
		LanSession.PendingGame = null;

		LoadDialog.SetDirectoryForLoading(@"Conquests/Saves");
		LoadScenarioDialog.SetDirectoryForLoading(@"Conquests/Scenarios");
		LoadScenarioDialog.GoToScenarioSetupAfterLoading = true;

		// Without a Civ3 install, play in standalone mode rather than asking.
		if (!C7Settings.UseStandaloneMode() && !ClassicGraphicsAvailable()) {
			log.Information("No Civ3 install found, switching to standalone mode");
			Global.ActivateGameMode(GamePaths.standalone);
		}

		ButtonContainer.Visible = true;
		ButtonContainer.CreateButtons();

		// TODO: enable buttons are features are implemented
		ButtonContainer.NewGame.Pressed += () => {
			LanSession.HostNextGame = false;
			GoToWorldSetup();
		};
		ButtonContainer.QuickStart.Pressed += QuickStartGame;
		ButtonContainer.Tutorial.Pressed += QuickStartGame;
		ButtonContainer.Tutorial.Visible = false;
		ButtonContainer.LoadGame.Pressed += LoadGame;
		ButtonContainer.LoadScenario.Pressed += LoadScenario;
		ButtonContainer.HostLan.Pressed += HostLanGame;
		ButtonContainer.JoinLan.Pressed += JoinLanGame;
		ButtonContainer.HallOfFame.Pressed += HallOfFame;
		ButtonContainer.HallOfFame.Visible = false;
		ButtonContainer.Settings.Pressed += ShowSettings;
		ButtonContainer.Preferences.Pressed += Preferences;
		ButtonContainer.Preferences.Visible = false;
		ButtonContainer.AudioPreferences.Pressed += Preferences;
		ButtonContainer.AudioPreferences.Visible = false;
		ButtonContainer.Credits.Pressed += showCredits;
		ButtonContainer.Exit.Pressed += _on_Exit_pressed;

		ButtonContainer.ToggleGraphics.Pressed += () => {
			Global.ToggleStandaloneMode();
			GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
		};
		SetToggleGraphicsText();

		// We can't toggle to using civ3 graphics in standalone mode.
		if (C7Settings.UseStandaloneMode() && !ClassicGraphicsAvailable()) {
			ButtonContainer.ToggleGraphics.Visible = false;
		}

		// Hide if valid path is present as proven by reaching this point in code
		NoCiv3Options.Visible = false;
	}

	private bool ClassicGraphicsAvailable() {
		if (string.IsNullOrEmpty(Util.Civ3Root)) {
			return false;
		}

		string[] basePaths = ["Conquests", "civ3PTW", ""];
		foreach (string basePath in basePaths) {
			string relPath = string.IsNullOrEmpty(basePath) ? "Art/buttonsFINAL.pcx" : $"{basePath}/Art/buttonsFINAL.pcx";
			if (Util.FileExistsIgnoringCase(Util.Civ3Root, relPath) != null) {
				return true;
			}
		}

		return false;
	}

	private void SetToggleGraphicsText() {
		if (C7Settings.UseStandaloneMode()) {
			ButtonContainer.ToggleGraphics.Text = "Use Civilization III Media";
		} else {
			ButtonContainer.ToggleGraphics.Text = "Use OpenCiv3 Media";
		}
	}

	public void GoToWorldSetup() {
		PlayButtonPressedSound();
		GetTree().ChangeSceneToFile("res://UIElements/NewGame/world_setup.tscn");
	}

	public void QuickStartGame() {
		log.Information("start game button pressed");
		PlayButtonPressedSound();
		QuickStartSetup.Init(Global);
		GetTree().ChangeSceneToFile("res://C7Game.tscn");
	}

	public void LoadGame() {
		log.Information("load game button pressed");
		LanSession.HostNextGame = false;
		OpenLoadDialog();
	}

	private void OpenLoadDialog() {
		PlayButtonPressedSound();
		LoadDialog.Popup();
	}

	// Asks whether to host a new game or a saved one; either way the game
	// opens the LAN lobby once it's ready.
	public void HostLanGame() {
		PlayButtonPressedSound();
		AcceptDialog dialog = new() {
			Title = "Host LAN Game",
			DialogText = "Set up a game for players on your local network.\n" +
				"For a new game, mark a rival as Human for each of them on the\n" +
				"player setup screen; they choose their own civilizations when they join.\n" +
				"Or load a game saved with several human players.",
			OkButtonText = "New Game",
		};
		dialog.AddButton("Load Game", true, "load");
		dialog.AddCancelButton("Cancel");
		dialog.Confirmed += () => {
			LanSession.HostNextGame = true;
			GoToWorldSetup();
		};
		dialog.CustomAction += action => {
			if (action == "load") {
				dialog.Hide();
				LanSession.HostNextGame = true;
				OpenLoadDialog();
			}
		};
		dialog.Canceled += () => LanSession.HostNextGame = false;
		// The dialog is made for each click, so free it once it closes.
		dialog.VisibilityChanged += () => {
			if (!dialog.Visible) {
				dialog.QueueFree();
			}
		};
		AddChild(dialog);
		dialog.PopupCentered();
	}

	private void StartDevLanGame() {
		if (LanSession.DevHostSave != null) {
			Global.LoadGamePath = LanSession.DevHostSave;
			LanSession.HostNextGame = true;
			LanSession.StartGame(GetTree());
		} else {
			JoinLanGame();
		}
	}

	public void JoinLanGame() {
		PlayButtonPressedSound();
		LanLobby.joining = true;
		GetTree().ChangeSceneToFile(LanSession.LobbyScene);
	}

	public void LoadScenario() {
		log.Information("load scenario button pressed");
		PlayButtonPressedSound();
		LoadScenarioDialog.Popup();
	}

	public void showCredits() {
		log.Information("credits button pressed");
		GetTree().ChangeSceneToFile("res://Credits.tscn");
	}

	public void HallOfFame() {
		PlayButtonPressedSound();
	}

	public void ShowSettings() {
		log.Information("settings button pressed");
		PlayButtonPressedSound();
		GetTree().ChangeSceneToFile("res://UIElements/Settings/settings_menu.tscn");
	}

	public void Preferences() {
		PlayButtonPressedSound();
	}

	public void _on_Exit_pressed() {
		GetTree().Quit(); // no need to notify the scene tree
	}

	private void _on_SetCiv3Home_pressed() {
		SetCiv3HomeDialog.Popup();
	}

	private void _on_SetCiv3HomeDialog_dir_selected(string path) {
		Util.Civ3Root = path;
		C7Settings.SetValue("locations", "civ3InstallDir", path);
		C7Settings.SaveSettings();
		// This function should only be reachable if DisplayTitleScreen failed on previous runs, so should be OK to run here
		DisplayTitleScreen();
	}

	private void UseStandaloneModePressed() {
		Global.ActivateGameMode(GamePaths.standalone);
		DisplayTitleScreen();
	}

	private void PlayMusic() {
		AudioManager.PlayMusic("menu.main_menu_1");
	}

	private void PlayButtonPressedSound() {
		AudioManager.PlayUIAudio("buttons.button_1");
	}
}

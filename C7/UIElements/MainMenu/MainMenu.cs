using Godot;
using System;
using C7Engine;
using C7Engine.Network;
using Serilog;

public partial class MainMenu : Node {
	private ILogger log;

	[Export]
	Civ3FileDialog LoadDialog;
	[Export]
	MenuButtonContainer ButtonContainer;

	GlobalSingleton Global;

	private static bool devWatchUsed = false;
	AudioManager AudioManager;

	public override void _Ready() {
		log = LogManager.ForContext<MainMenu>();
		log.Debug("enter MainMenu._Ready");

		DisplayServer.WindowSetTitle((string)ProjectSettings.GetSetting("application/config/name"));

		try {
			ShowVersion();
			DisplayTitleScreen();

			// A load given up on hosts nothing: otherwise the next game
			// started from the menu would be hosted on the LAN.
			LoadDialog.Canceled += () => LanSession.HostNextGame = false;

			AudioManager = GetNode<AudioManager>("/root/GlobalAudioManager");
			LadderSubmit.SendPendingOnStart();

			if (LanSession.TakeDevStart()) {
				PlayMusic();
				CallDeferred(nameof(StartDevLanGame));
			} else if (ServerHandoff.TakeGameStart()) {
				// Handed a server game by the program that started us (see
				// ServerHandoff): straight into it, with no menu.
				ButtonContainer.Visible = false;
				PlayMusic();
				CallDeferred(nameof(JoinHandedOffGame));
			} else if (ServerHandoff.TakeGamesStart()) {
				PlayMusic();
				CallDeferred(nameof(OpenServerGames));
			} else if (!devWatchUsed && Array.IndexOf(OS.GetCmdlineUserArgs(), "--watch-ai") >= 0) {
				// The --watch-ai developer option goes straight into a game
				// to watch, the first time the menu shows.
				devWatchUsed = true;
				PlayMusic();
				CallDeferred(nameof(WatchAiQuickStart));
			} else {
				PlayIntroThenMusic();
			}
		} catch (Exception ex) {
			log.Error(ex, "Could not set up the main menu");
		}
	}

	// The logo reads just OpenCiv: the label under it ("Air" in the scene,
	// once the version) is hidden.
	private void ShowVersion() {
		GetNode<RichTextLabel>("Control/Background/RichTextLabel").Visible = false;
	}

	private void DisplayTitleScreen() {
		// To pass data between scenes, putting path string in a global singleton and reading it later in createGame
		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		Global.ResetLoadGameFields();

		// Let go of anything the UI cached from the last game, including the
		// art of a scenario it was.
		UICaches.Clear();
		Util.setModPath(null);

		// Back at the menu, any LAN game is over.
		LanSession.End();
		LanSession.HostNextGame = false;
		LanSession.PendingGame = null;
		LanSession.ResumeGame = null;
		Global.WatchNextGame = false;
		Global.Scenario = GameScenario.None;

		LoadDialog.SetDirectoryForLoadingSaves();

		ButtonContainer.Visible = true;
		ButtonContainer.CreateButtons();

		// TODO: enable buttons are features are implemented
		ButtonContainer.NewGame.Pressed += () => {
			LanSession.HostNextGame = false;
			GoToWorldSetup();
		};
		ButtonContainer.QuickStart.Pressed += QuickStartGame;
		ButtonContainer.WatchAi.Pressed += WatchAiGame;
		ButtonContainer.Tutorial.Pressed += QuickStartGame;
		// Civ3's original main menu had a Tutorial (Text\labels.txt,
		// #TOP_MENU), but Conquests' has none (Conquests\Text\labels.txt) and
		// its executable has no tutorial game setup, so how that game was set
		// up can't be matched. The tutorial is turned on in a game instead,
		// with Tutorial Mode in the Preferences (see TutorialGuide).
		ButtonContainer.Tutorial.Visible = false;
		ButtonContainer.LoadGame.Pressed += LoadGame;
		// The game ships no scenario files to load, so Load Scenario isn't
		// offered; its scenarios are game types on the New Game screen.
		ButtonContainer.LoadScenario.Visible = false;
		ButtonContainer.HostLan.Pressed += HostLanGame;
		ButtonContainer.JoinLan.Pressed += JoinLanGame;
		ButtonContainer.ServerGames.Pressed += ShowServerGames;
		ButtonContainer.HallOfFame.Pressed += HallOfFame;
		ButtonContainer.Settings.Pressed += ShowSettings;
		ButtonContainer.Preferences.Pressed += Preferences;
		ButtonContainer.Preferences.Visible = false;
		ButtonContainer.AudioPreferences.Pressed += Preferences;
		ButtonContainer.AudioPreferences.Visible = false;
		ButtonContainer.Credits.Pressed += showCredits;
		ButtonContainer.Exit.Pressed += _on_Exit_pressed;

		// Back from setting up a server game, to the screen it was set up from.
		if (ServerGames.OpenOnMenu) {
			ServerGames.OpenOnMenu = false;
			CallDeferred(nameof(OpenServerGames));
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

	// Sets up a game as New Game does, with the map, rivals and rules the
	// user chooses, for the AIs to play while the user watches.
	public void WatchAiGame() {
		Global.WatchNextGame = true;
		LanSession.HostNextGame = false;
		GoToWorldSetup();
	}

	// Starts a game to watch straight away, as Quick Start would (the
	// --watch-ai developer option).
	public void WatchAiQuickStart() {
		Global.WatchNextGame = true;
		QuickStartGame();
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
	// opens the LAN lobby once it's ready. A new game is set up on the
	// Multiplayer Setup screen, as in Civ3 (owner's request, 2026-10-28;
	// the world and player setup screens' way was then dropped for it).
	public void HostLanGame() {
		PlayButtonPressedSound();
		AcceptDialog dialog = new() {
			Title = "Host LAN Game",
			DialogText = "Set up a game for players on your local network, or a hotseat game.\n" +
				"For a new game, set a slot to Open for each other player on the\n" +
				"Multiplayer Setup screen; they choose their own civilizations when they join.\n" +
				"Or load a game saved with several human players.",
			OkButtonText = "New Game",
		};
		dialog.AddButton("Load Game", true, "load");
		// The game this machine last hosted, saved as its last turn began.
		Button resume = dialog.AddButton("Resume Last LAN Game", true, "resume");
		resume.Disabled = !LanAutosave.Exists(LanAutosave.DefaultDirectory);
		resume.TooltipText = resume.Disabled
			? "No LAN game hosted here has been saved yet."
			: "Host the last LAN game hosted here again, from the start of its last turn. Its players get their seats back when they reconnect.";
		dialog.AddCancelButton("Cancel");
		dialog.Confirmed += () => {
			// That screen says whether the game is hosted, when it's launched.
			LanSession.HostNextGame = false;
			PlayButtonPressedSound();
			GetTree().ChangeSceneToFile("res://UIElements/NewGame/multiplayer_setup.tscn");
		};
		dialog.CustomAction += action => {
			if (action == "load") {
				dialog.Hide();
				LanSession.HostNextGame = true;
				OpenLoadDialog();
			} else if (action == "resume") {
				dialog.Hide();
				ResumeLanGame();
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

	private void ResumeLanGame() {
		LanResumeInfo resume = LanAutosave.ReadResumeInfo(LanAutosave.DefaultDirectory);
		if (resume == null) {
			log.Warning("There is no LAN game to resume");
			return;
		}
		LanSession.ResumeGame = resume;
		LanSession.HostNextGame = true;
		LanSession.StartGame(GetTree());
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

	// Server games: logging in to the game server, the player's games and
	// the open ones, over the menu until closed.
	public void ShowServerGames() {
		PlayButtonPressedSound();
		OpenServerGames();
	}

	private void OpenServerGames() {
		ServerGamesScreen screen = new();
		ShowOverMenu(screen, h => screen.Closed += h);
	}

	private void JoinHandedOffGame() {
		ServerHandoff.JoinGame(this);
	}

	public void showCredits() {
		log.Information("credits button pressed");
		GetTree().ChangeSceneToFile("res://Credits.tscn");
	}

	// The main menu's Hall of Fame. The game ships no campaigns, so there
	// is no Campaign Record before it.
	public void HallOfFame() {
		PlayButtonPressedSound();
		ShowHallOfFame();
	}

	// Civ3's Hall of Fame, over the menu until it's closed.
	private void ShowHallOfFame() {
		HallOfFameScreen screen = new();
		ShowOverMenu(screen, h => screen.Closed += h);
	}

	private void ShowOverMenu(Control screen, Action<Action> onClosed) {
		CanvasLayer layer = new() { Layer = 10 };
		onClosed(() => layer.QueueFree());
		layer.AddChild(screen);
		AddChild(layer);
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

	// The intro movie the first time the menu shows, if there is one and it
	// isn't turned off, with the menu's music once it's over.
	private void PlayIntroThenMusic() {
		IntroVideo intro = IntroVideo.TakeForLaunch();
		if (intro == null) {
			PlayMusic();
			return;
		}
		log.Information("Playing the intro movie");
		intro.Finished += PlayMusic;
		AddChild(intro);
	}

	private void PlayMusic() {
		AudioManager.PlayMusic("menu.main_menu_1");
	}

	private void PlayButtonPressedSound() {
		AudioManager.PlayUIAudio("buttons.button_1");
	}
}

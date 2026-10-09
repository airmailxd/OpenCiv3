using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7GameData;
using C7Engine;
using C7Engine.Lua;
using C7GameData.Save;
using Serilog;

public partial class PlayerSetup : Control {
	private static ILogger log = LogManager.ForContext<PlayerSetup>();

	[Export] TextureRect background;

	[Export] GridContainer playerListContainer;
	ButtonGroup playerListButtonGroup = new();
	TextureRect leaderHead = new();
	[Export] Label civLabel;

	[Export] GridContainer opponentListContainer;
	List<OptionButton> opponentSelectors = new();
	// The items every opponent selector offers, and, parallel to
	// opponentSelectors, which of each selector's items are disabled, so
	// only the items that change need updating.
	List<string> opponentItemTexts = new();
	List<bool[]> opponentItemsDisabled = new();
	// Parallel to opponentSelectors: whether that slot is played by another
	// person sharing this computer (hotseat) rather than by the AI, and that
	// person's name.
	List<CheckBox> humanToggles = new();
	List<LineEdit> humanNames = new();
	// Shown instead of the civilization for a human slot in a game hosted on
	// the LAN, whose player chooses in the lobby.
	List<Label> playersChoiceLabels = new();
	// Whether this game will be hosted on the LAN, where human slots are for
	// the people who join.
	readonly bool hostingOnLan = LanSession.HostNextGame;
	// The first player's name, shown once there is more than one human.
	LineEdit playerNameEdit;
	const int MaxPlayerNameLength = 24;

	[Export] GridContainer rulesContainer;

	[Export] GridContainer difficultyContainer;

	ButtonGroup difficultyButtonGroup = new();

	CheckBox showScoreboard;
	CheckBox coreCitiesFreeOfCorruption;
	CheckBox acceleratedProduction;
	CheckBox turnLimit;

	[Export] TextureButton confirm;
	[Export] TextureButton cancel;

	[Export] Label loadingLabel;

	SaveGame save;

	Civilization selectedCivilization;
	Difficulty selectedDifficulty;
	VictoryConditions victoryConditions;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		GlobalSingleton global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		save = global.SaveGame;

		// Set up buttons for the civs the player can play as.
		playerListContainer.Columns = (int)Math.Ceiling(save.Civilizations.Count / 12.0);
		string initiallySelectedCiv = save.Civilizations.Any(x => x.name == "Netherlands") ? "Netherlands" : save.Civilizations[1].name;
		foreach (Civilization civ in save.Civilizations) {
			if (civ.isBarbarian) {
				continue;
			}

			Civ3MenuButton button = new() {
				Text = civ.name,
				FontSize = 12,
				textPosition = Civ3MenuButton.TextPosition.TextLeftOfIcon,
				ButtonGroup = playerListButtonGroup,
				ToggleMode = true,
			};
			button.Pressed += () => {
				selectedCivilization = civ;
				UpdateOpponentSelectors();
				DisplaySelectedLeader();
			};
			playerListContainer.AddChild(button);

			if (civ.name == initiallySelectedCiv) {
				button.ButtonPressed = true;
				selectedCivilization = civ;
			}
		}
		background.AddChild(leaderHead);
		DisplaySelectedLeader();

		// Below the leader's name and traits.
		playerNameEdit = new() {
			MaxLength = MaxPlayerNameLength,
			Visible = false,
			Position = new Vector2(421, 372),
			CustomMinimumSize = new Vector2(187, 0),
			TooltipText = "Your name, shown when the screen is handed to you.",
		};
		background.AddChild(playerNameEdit);

		// Set up the options for opponents.
		AddOpponentSelectors(global.WorldCharacteristics.worldSize.numberOfCivs);

		// Set up game rules
		AddRules();

		// Set up the difficulty buttons
		difficultyContainer.Columns = save.Difficulties.Count;
		string initiallySelectedDifficulty = save.Difficulties.Any(x => x.Name == "Regent") ? "Regent" : save.Difficulties[0].Name;
		foreach (Difficulty difficulty in save.Difficulties) {
			CenterContainer container = new();
			difficultyContainer.AddChild(container);

			Civ3MenuButton button = new(Civ3MenuButton.TextPosition.TextAboveIcon) {
				Text = difficulty.Name,
				ButtonGroup = difficultyButtonGroup,
				ToggleMode = true,
			};
			button.Pressed += () => { selectedDifficulty = difficulty; };
			container.AddChild(button);
			if (difficulty.Name == initiallySelectedDifficulty) {
				button.ButtonPressed = true;
				selectedDifficulty = difficulty;
			}
			container.CustomMinimumSize = new Vector2(843.0f / difficultyContainer.Columns, 0);
		}

		confirm.Pressed += CreateGame;
		cancel.Pressed += BackToMainMenu;
	}

	private void AddRules() {
		victoryConditions = VictoryConditions.NewGameDefaults();
		rulesContainer.Columns = 2;
		rulesContainer.AddThemeConstantOverride("v_separation", 0);

		// TODO: Add Civ3Checkbox in rulesContainer for each victory condition, wire up to victoryConditions

		turnLimit = new Civ3Checkbox {
			Text = $"Turn limit ({save.TimeOptions.turnLimit} turns)",
			FontSize = 14,
			ButtonPressed = false,
			TooltipText = $"End the game after turn {save.TimeOptions.turnLimit}, scoring who leads. Without it, the game goes on until someone wins.",
		};
		rulesContainer.AddChild(turnLimit);

		showScoreboard = new Civ3Checkbox {
			Text = "Show scoreboard",
			FontSize = 14,
			ButtonPressed = true,
			TooltipText = "With more than one human player, show everyone's score and how long the current turn has taken.",
		};
		rulesContainer.AddChild(showScoreboard);

		coreCitiesFreeOfCorruption = new Civ3Checkbox {
			Text = "No corruption near capital",
			FontSize = 14,
			// Off unless it was chosen for the last game, like the rule itself.
			ButtonPressed = C7Settings.GetSettingsValueOrDefault(C7Settings.LastGame.SectionName, C7Settings.LastGame.CoreCitiesFreeOfCorruption, "false") == "true",
			TooltipText = "Custom rule: the capital and the 5 cities nearest it have no corruption or waste.",
		};
		rulesContainer.AddChild(coreCitiesFreeOfCorruption);

		acceleratedProduction = new Civ3Checkbox {
			Text = "Accelerated production",
			FontSize = 14,
			// As the scenario or ruleset sets it, like in Civ3.
			ButtonPressed = save.Rules.AcceleratedProduction,
			TooltipText = "Cities generate double the food, shields and commerce each turn, speeding up growth, research and production.",
		};
		rulesContainer.AddChild(acceleratedProduction);
	}

	private void BackToMainMenu() {
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}

	private void AddOpponentSelectors(int numberOfCivs) {
		int numOpponents = numberOfCivs - 1;

		for (int i = 0; i < numOpponents; ++i) {
			OptionButton optionButton = new();
			StyleBoxFlat styleBox = new() {
				BorderColor = Color.Color8(150, 150, 150, 220),
				BgColor = Color.Color8(255, 255, 255, 0),
				BorderWidthBottom = 2,
				BorderWidthLeft = 2,
				BorderWidthRight = 2,
				BorderWidthTop = 2,
				ContentMarginLeft = 4,
				ContentMarginRight = 4,
				ContentMarginTop = 4,
				ContentMarginBottom = 4
			};
			optionButton.AddThemeStyleboxOverride("normal", styleBox);
			optionButton.AddThemeStyleboxOverride("hover", styleBox); ;
			optionButton.AddThemeStyleboxOverride("pressed", styleBox);

			PopupMenu popup = optionButton.GetPopup();
			popup.AddThemeStyleboxOverride("panel", styleBox);
			popup.MaxSize = new Vector2I(popup.MaxSize.X, 300);
			popup.SetTransparentBackground(false);
			opponentSelectors.Add(optionButton);

			CheckBox humanToggle = new() {
				Text = "Human",
				TooltipText = hostingOnLan
					? "Played by someone joining over the network, who chooses their own civilization in the lobby."
					: "Played by another person on this computer, taking turns (hotseat).",
			};
			humanToggles.Add(humanToggle);

			Label playersChoice = new() {
				Text = "Player's choice",
				Visible = false,
				VerticalAlignment = VerticalAlignment.Center,
				TooltipText = "Whoever takes this seat chooses their civilization in the LAN lobby.",
				MouseFilter = MouseFilterEnum.Pass,
			};
			playersChoiceLabels.Add(playersChoice);
			if (hostingOnLan) {
				humanToggle.Toggled += (bool on) => {
					// The slot doesn't hold a civilization for the AI any more.
					optionButton.Select(optionButton.ItemCount - 1);
					optionButton.Visible = !on;
					playersChoice.Visible = on;
					UpdateOpponentSelectors();
				};
			}

			LineEdit humanName = new() {
				MaxLength = MaxPlayerNameLength,
				Visible = false,
				TooltipText = "This player's name, shown when the screen is handed to them.",
			};
			humanNames.Add(humanName);
			humanToggle.Toggled += (bool _) => UpdateHumanNameFields();

			CenterContainer container = new();
			opponentListContainer.AddChild(container);
			VBoxContainer slot = new();
			container.AddChild(slot);
			HBoxContainer row = new();
			slot.AddChild(row);
			row.AddChild(optionButton);
			row.AddChild(playersChoice);
			row.AddChild(humanToggle);
			slot.AddChild(humanName);

			const float humanToggleWidth = 80.0f;
			container.CustomMinimumSize = new Vector2(312.0f / opponentListContainer.Columns, 315.0f / numOpponents);
			optionButton.CustomMinimumSize = new Vector2(290.0f / opponentListContainer.Columns - humanToggleWidth, optionButton.CustomMinimumSize.Y);
			playersChoice.CustomMinimumSize = optionButton.CustomMinimumSize;

			foreach (Civilization civ in save.Civilizations) {
				if (civ.isBarbarian) {
					continue;
				}
				optionButton.AddItem(civ.name);
			}

			// Add (and default to) random for each opponent.
			optionButton.AddItem("Random");
			optionButton.Select(popup.ItemCount - 1);

			for (int k = 0; k < popup.ItemCount; ++k) {
				popup.SetItemAsRadioCheckable(k, false);
				popup.SetItemAsCheckable(k, false);
			}

			if (opponentItemTexts.Count == 0) {
				for (int k = 0; k < optionButton.ItemCount; ++k) {
					opponentItemTexts.Add(optionButton.GetItemText(k));
				}
			}
			opponentItemsDisabled.Add(new bool[optionButton.ItemCount]);

			optionButton.ItemSelected += (long i) => { UpdateOpponentSelectors(); };
		}

		UpdateOpponentSelectors();
	}

	// Shows a name field for every human player once there is more than one,
	// with "Player N" (in turn order) as the default name. Players on the LAN
	// go by the names they join with instead.
	private void UpdateHumanNameFields() {
		if (hostingOnLan) {
			return;
		}
		int playerNumber = 1;
		playerNameEdit.PlaceholderText = $"Player {playerNumber}";
		for (int i = 0; i < humanToggles.Count; ++i) {
			humanNames[i].Visible = humanToggles[i].ButtonPressed;
			if (humanToggles[i].ButtonPressed) {
				humanNames[i].PlaceholderText = $"Player {++playerNumber}";
			}
		}
		playerNameEdit.Visible = playerNumber > 1;
	}

	private static string PlayerName(LineEdit nameEdit) {
		string name = nameEdit.Text.Trim();
		return name != "" ? name : nameEdit.PlaceholderText;
	}

	private void UpdateOpponentSelectors() {
		HashSet<string> civsTaken = new();
		civsTaken.Add(selectedCivilization.name);

		for (int s = 0; s < opponentSelectors.Count; ++s) {
			OptionButton ob = opponentSelectors[s];
			bool[] disabled = opponentItemsDisabled[s];
			int selected = ob.Selected;
			string selection = selected >= 0 && selected < opponentItemTexts.Count ? opponentItemTexts[selected] : ob.GetItemText(selected);

			// If the player decides to play as civ X and one of the opponent
			// selectors has X selected, change it to random. Similarly if one
			// of the previous option buttons has selected this civ.
			if (civsTaken.Contains(selection)) {
				ob.Select(disabled.Length - 1);
			} else if (selection != "Random") {
				civsTaken.Add(selection);
			}

			// Only touch the items whose state changed.
			for (int i = 0; i < disabled.Length; ++i) {
				bool disable = civsTaken.Contains(opponentItemTexts[i]);
				if (disable != disabled[i]) {
					ob.SetItemDisabled(i, disable);
					disabled[i] = disable;
				}
			}
		}
	}

	private void DisplaySelectedLeader() {
		Civilization civilization = selectedCivilization;

		leaderHead.Texture = LeaderHeadTextures.Get(civilization);
		leaderHead.Scale = new Vector2(1.7f, 1.7f);
		leaderHead.SetPosition(new Vector2(414, 46));

		string traits = string.Join(", ", civilization.traits);
		civLabel.Text = $"{civilization.leader} of the {civilization.noun}\n({traits})";
	}

	private List<SelectedOpponent> CollectSelectedOpponents() {
		List<SelectedOpponent> opponents = [];

		for (int i = 0; i < opponentSelectors.Count; ++i) {
			if (humanToggles[i].ButtonPressed) {
				continue;
			}

			OptionButton ob = opponentSelectors[i];
			string selectedName = ob.GetItemText(ob.Selected);

			if (selectedName == "Random") {
				opponents.Add(new SelectedOpponent { isRandom = true });
			} else {
				opponents.Add(new SelectedOpponent { isRandom = false, Name = selectedName });
			}
		}

		return opponents;
	}

	// The additional human players. A human slot left on "Random" gets a
	// random civilization nobody else has picked.
	private List<HotseatPlayer> CollectHotseatPlayers() {
		List<Civilization> playable = save.Civilizations.Where(c => !c.isBarbarian).ToList();
		HashSet<string> taken = [selectedCivilization.name];
		foreach (OptionButton ob in opponentSelectors) {
			taken.Add(ob.GetItemText(ob.Selected));
		}

		List<HotseatPlayer> players = [];
		Random rand = new();
		for (int i = 0; i < opponentSelectors.Count; ++i) {
			if (!humanToggles[i].ButtonPressed) {
				continue;
			}

			string selectedName = opponentSelectors[i].GetItemText(opponentSelectors[i].Selected);
			Civilization civ = playable.Find(c => c.name == selectedName);
			if (civ == null) {
				List<Civilization> available = playable.Where(c => !taken.Contains(c.name)).ToList();
				civ = available[rand.Next(available.Count)];
				taken.Add(civ.name);
			}
			players.Add(new HotseatPlayer { civilization = civ, name = PlayerName(humanNames[i]) });
		}
		return players;
	}

	// Whether a game is being created, so a second click on the confirm
	// button doesn't start a second one.
	private bool creatingGame = false;

	private void CreateGame() {
		if (creatingGame) {
			return;
		}
		SetCreatingGame(true);
		try {
			StartCreatingGame();
		} catch (Exception e) {
			OnCreateGameFailed(e);
		}
	}

	private void SetCreatingGame(bool creating) {
		creatingGame = creating;
		loadingLabel.Visible = creating;
		confirm.Disabled = creating;
		cancel.Disabled = creating;
	}

	private void OnCreateGameFailed(Exception e) {
		log.Error(e, "Couldn't create the game");
		if (!IsInstanceValid(this) || !IsInsideTree()) {
			return;
		}
		SetCreatingGame(false);
		Util.ShowErrorDialog(this, "Couldn't create the game", e.Message);
	}

	private void StartCreatingGame() {

		GlobalSingleton global = GetNode<GlobalSingleton>("/root/GlobalSingleton");

		victoryConditions.UseTurnLimit = turnLimit.ButtonPressed;

		int guestSeats = humanToggles.Count(t => t.ButtonPressed);
		if (hostingOnLan && guestSeats > 0) {
			// The guests choose their civilizations in the lobby, and the
			// game is created once the host starts it.
			GameSetup lanSetup = new() {
				playerCivilization = selectedCivilization,
				playerName = LanSession.PlayerName,
				difficulty = selectedDifficulty,
				worldCharacteristics = global.WorldCharacteristics,
				opponents = CollectSelectedOpponents(),
				victoryConditions = victoryConditions,
				showScoreboard = showScoreboard.ButtonPressed,
				coreCitiesFreeOfCorruption = coreCitiesFreeOfCorruption.ButtonPressed,
				acceleratedProduction = acceleratedProduction.ButtonPressed,
				invalidMapsDirectory = InvalidMaps.FolderIfEnabled,
			};
			PersistGameSettings(lanSetup);
			LanSession.PendingGame = new PendingLanGame(lanSetup, save, guestSeats);
			StartGame();
			return;
		}

		List<HotseatPlayer> hotseatPlayers = CollectHotseatPlayers();
		GameSetup gameSetup = new() {
			playerCivilization = selectedCivilization,
			// Names only matter when the screen is handed between players.
			playerName = hotseatPlayers.Count > 0 ? PlayerName(playerNameEdit) : null,
			hotseatPlayers = hotseatPlayers,
			difficulty = selectedDifficulty,
			worldCharacteristics = global.WorldCharacteristics,
			opponents = CollectSelectedOpponents(),
			victoryConditions = victoryConditions,
			showScoreboard = showScoreboard.ButtonPressed,
			coreCitiesFreeOfCorruption = coreCitiesFreeOfCorruption.ButtonPressed,
			acceleratedProduction = acceleratedProduction.ButtonPressed,
			invalidMapsDirectory = InvalidMaps.FolderIfEnabled,
		};

		PersistGameSettings(gameSetup);

		// World generation can take a bit of time if multiple attempts are
		// needed, so we don't want to tie up the UI thread.
		Task.Run(() => {
			try {
				gameSetup.Populate(save);
			} catch (Exception e) {
				Callable.From(() => OnCreateGameFailed(e)).CallDeferred();
				return;
			}

			log.Information("opening map");
			Callable.From(StartGame).CallDeferred();
		});
	}

	private void PersistGameSettings(GameSetup gameSetup) {
		try {
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.WorldSize, gameSetup.worldCharacteristics.worldSize.name);
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.BarbarianActivity, gameSetup.worldCharacteristics.barbarianActivity.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Landform, gameSetup.worldCharacteristics.landform.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.OceanCoverage, gameSetup.worldCharacteristics.oceanCoverage.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Climate, gameSetup.worldCharacteristics.climate.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Temperature, gameSetup.worldCharacteristics.temperature.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Age, gameSetup.worldCharacteristics.age.ToString());
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Civilization, gameSetup.playerCivilization.name);
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Difficulty, gameSetup.difficulty.Name);

			List<SelectedOpponent> ops = gameSetup.opponents;
			string opponentsValue = string.Join("|", ops.Select(o => o.isRandom ? "Random" : o.Name));
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.Opponents, opponentsValue);
			C7Settings.SetValue(C7Settings.LastGame.SectionName, C7Settings.LastGame.CoreCitiesFreeOfCorruption, gameSetup.coreCitiesFreeOfCorruption ? "true" : "false");

			C7Settings.SaveSettings();
		} catch (Exception e) {
			log.Error(e, "Failed to persist game settings to C7.ini");
		}
	}

	private void StartGame() {
		if (!IsInstanceValid(this) || !IsInsideTree()) {
			return;
		}
		try {
			LanSession.StartGame(GetTree());
		} catch (Exception e) {
			OnCreateGameFailed(e);
		}
	}
}

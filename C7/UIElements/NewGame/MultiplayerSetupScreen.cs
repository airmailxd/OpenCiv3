using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using Serilog;

// Civ3's Multiplayer Setup screen, in OpenCiv3's own look (owner's request,
// 2026-10-28): the game's settings on the left; the players, the rules and
// the messages on the right; a chat line along the bottom. The host sets up
// a hotseat or networked game here in one go, in place of the world and
// player setup screens. Launch starts a hotseat game straight away, and
// opens the LAN lobby for a networked one, where the people joining take
// the Open slots and choose their civilizations.
public partial class MultiplayerSetupScreen : Control {
	private static readonly ILogger log = LogManager.ForContext<MultiplayerSetupScreen>();

	// The screen grows with the window up to the largest size, and below
	// Civ3's own 1024x768 is laid out at that size and scaled down to fit.
	private static readonly Vector2 MinSize = new(1024, 768);
	private static readonly Vector2 MaxSize = new(1280, 960);

	// Where the frame's stone border ends (credits.background's), which the
	// title and the chat line sit on.
	private const int BorderSide = 56;
	private const int BorderTop = 84;
	private const int BorderBottom = 84;

	private static readonly Color Ink = Colors.Black;
	private static readonly Color Brown = new(0.47f, 0.39f, 0.27f);
	private static readonly Color DarkField = new(0.25f, 0.2f, 0.14f);
	private static readonly Color FieldText = new(0.98f, 0.95f, 0.85f);
	private static readonly Color ProblemColor = new(0.6f, 0.1f, 0.05f);

	private GlobalSingleton global;
	private SaveGame save;
	private NewGameChoices choices;
	private bool hasKings;

	private Control frame;
	private RichTextLabel messages;

	// GAME SETTINGS.
	private LineEdit gameNameEdit;
	private OptionButton gameType;
	private OptionButton gameMode;
	private OptionButton turnTimer;
	private OptionButton worldSize;
	private OptionButton barbarians;
	private OptionButton landMass;
	private OptionButton waterCoverage;
	private OptionButton climate;
	private OptionButton temperature;
	private OptionButton age;
	private OptionButton difficulty;
	private AcceptDialog gameLimits;
	private CheckBox turnLimit;

	// The game types, in the order listed, and which of them OpenCiv3 has.
	private static readonly (string text, MultiplayerGameType? type, string tooltip)[] GameTypes = [
		("Hotseat", MultiplayerGameType.Hotseat, "Everyone plays at this computer, taking turns."),
		("LAN", MultiplayerGameType.Lan, "Players on your local network join from \"Join LAN Game\"."),
		("Internet", MultiplayerGameType.Internet,
			"Players on your network, and anywhere else with the join code the lobby shows, through the online relay."),
		("Play By Email", null, "Not in OpenCiv3 yet."),
	];

	private static readonly (string text, MultiplayerGameMode mode)[] GameModes = [
		("Standard", MultiplayerGameMode.Standard),
		("Elimination", MultiplayerGameMode.Elimination),
		("Regicide", MultiplayerGameMode.Regicide),
		("Mass Regicide", MultiplayerGameMode.MassRegicide),
		("Victory Point Scoring", MultiplayerGameMode.VictoryPointScoring),
		("Custom", MultiplayerGameMode.Custom),
	];

	private static readonly WorldCharacteristics.Landform[] Landforms = [
		WorldCharacteristics.Landform.Pangaea, WorldCharacteristics.Landform.Continents, WorldCharacteristics.Landform.Archipelago];
	private static readonly WorldCharacteristics.OceanCoverage[] Oceans = [
		WorldCharacteristics.OceanCoverage.Percent_60, WorldCharacteristics.OceanCoverage.Percent_70, WorldCharacteristics.OceanCoverage.Percent_80];
	private static readonly WorldCharacteristics.Climate[] Climates = [
		WorldCharacteristics.Climate.Arid, WorldCharacteristics.Climate.Normal, WorldCharacteristics.Climate.Wet];
	private static readonly WorldCharacteristics.Temperature[] Temperatures = [
		WorldCharacteristics.Temperature.Warm, WorldCharacteristics.Temperature.Temperate, WorldCharacteristics.Temperature.Cool];
	private static readonly WorldCharacteristics.Age[] Ages = [
		WorldCharacteristics.Age.Billion_3, WorldCharacteristics.Age.Billion_4, WorldCharacteristics.Age.Billion_5];
	private static readonly BarbarianActivity[] BarbarianLevels = Enum.GetValues<BarbarianActivity>().OrderBy(b => b).ToArray();

	// PLAYER SETUP: the host's row, then a row for each rival's slot.
	private Button hostNameButton;
	private Panel hostColor;
	private OptionButton hostCivilization;
	private VBoxContainer slotRows;
	private readonly List<(OptionButton kind, OptionButton civ)> rows = [];
	private List<PlayerSlot> slots = [];
	// The row Rename renames: -1 for the host's.
	private int selectedRow = -1;
	private Button renameButton;
	private Button launchButton;
	// The civilizations every Civilizations list offers, then Random.
	private List<string> civNames;

	// GAME RULES.
	private CheckBox dominationVictory;
	private CheckBox spaceRaceVictory;
	private CheckBox diplomaticVictory;
	private CheckBox conquestVictory;
	private CheckBox culturalVictory;
	private CheckBox wonderVictory;
	private CheckBox acceleratedProduction;
	private CheckBox elimination;
	private CheckBox regicide;
	private CheckBox massRegicide;
	private CheckBox victoryPointScoring;
	private CheckBox simultaneousMoves;
	private CheckBox showScoreboard;
	private CheckBox cultureFlips;
	private CheckBox scientificLeaders;
	private CheckBox coreCitiesFreeOfCorruption;
	private VictoryConditions victoryConditions;
	private bool settingGameMode;

	private bool launching;

	private MultiplayerGameType GameType => GameTypes[gameType.Selected].type ?? MultiplayerGameType.Lan;
	private string HostName => LanSession.PlayerName;

	public override void _Ready() {
		global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		choices = NewGameChoices.Load();
		// A new game's rules, loaded here as the world setup screen does,
		// so this screen needs nothing set up before it.
		save = GameMode.Load(GamePaths.GameModesDir, GamePaths.GameMode).GetSave();
		hasKings = save.UnitPrototypes.Any(p => p.flags.Contains(SaveUnitPrototype.Flag.King));
		civNames = save.Civilizations.Where(c => !c.isBarbarian).Select(c => c.name).Append(NewGameChoices.Random).ToList();
		victoryConditions = choices.ChosenVictoryConditions(save.VictoryConditions);

		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		ColorRect backdrop = new() { Color = Colors.Black };
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(backdrop);

		frame = new Panel();
		frame.AddThemeStyleboxOverride("panel", new StyleBoxTexture {
			Texture = TextureLoader.Load("credits.background"),
			TextureMarginLeft = BorderSide,
			TextureMarginRight = BorderSide,
			TextureMarginTop = BorderTop,
			TextureMarginBottom = BorderBottom,
		});
		AddChild(frame);

		AddTitle();
		HBoxContainer body = new();
		body.AddThemeConstantOverride("separation", 10);
		body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		body.OffsetLeft = BorderSide + 8;
		body.OffsetRight = -BorderSide - 8;
		body.OffsetTop = BorderTop + 6;
		body.OffsetBottom = -BorderBottom - 6;
		frame.AddChild(body);

		body.AddChild(MakeGameSettings());
		VBoxContainer right = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		right.AddThemeConstantOverride("separation", 8);
		body.AddChild(right);
		right.AddChild(MakePlayerSetup());
		right.AddChild(MakeGameRules());
		right.AddChild(MakeMessages());
		AddBottomStrip();

		ShowGameType();
		GetViewport().SizeChanged += Fit;
		Fit();
	}

	public override void _ExitTree() {
		GetViewport().SizeChanged -= Fit;
	}

	// Sizes the frame to the window, as described at MinSize.
	private void Fit() {
		Vector2 view = GetViewportRect().Size;
		Vector2 size = new(Mathf.Clamp(view.X - 32, MinSize.X, MaxSize.X), Mathf.Clamp(view.Y - 32, MinSize.Y, MaxSize.Y));
		float scale = Mathf.Min(1, Mathf.Min(view.X / size.X, view.Y / size.Y));
		frame.Size = size;
		frame.Scale = new Vector2(scale, scale);
		frame.Position = ((view - size * scale) / 2).Floor();
	}

	// ---- Building blocks ----

	// Civ3's headings are capitals spaced wide.
	private Font SpacedFont(int spacing) {
		return new FontVariation { BaseFont = GetThemeDefaultFont(), SpacingGlyph = spacing };
	}

	private Label MakeHeading(string text, int size = 15) {
		Label label = new() { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
		label.AddThemeFontOverride("font", SpacedFont(3));
		label.AddThemeFontSizeOverride("font_size", size);
		return label;
	}

	private static StyleBoxFlat Box(Color background, Color border, int borderWidth = 1, int margin = 4) {
		return new StyleBoxFlat {
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			ContentMarginLeft = margin + 2,
			ContentMarginRight = margin + 2,
			ContentMarginTop = margin,
			ContentMarginBottom = margin,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
		};
	}

	// A panel of the screen, with its heading: lighter parchment on the
	// frame's, bordered and shadowed like the world setup screen's boxes.
	private (PanelContainer panel, VBoxContainer box) MakePanel(string heading) {
		PanelContainer panel = new();
		StyleBoxFlat style = Box(new Color(0.99f, 0.96f, 0.88f, 0.92f), Brown, 2, 6);
		style.ShadowColor = new Color(0.2f, 0.14f, 0.06f, 0.35f);
		style.ShadowSize = 3;
		style.ShadowOffset = new Vector2(2, 2);
		panel.AddThemeStyleboxOverride("panel", style);
		VBoxContainer box = new();
		box.AddThemeConstantOverride("separation", 3);
		panel.AddChild(box);
		box.AddChild(MakeHeading(heading));
		return (panel, box);
	}

	// A list to choose from, in parchment with a dark arrow, as Civ3's are.
	private static OptionButton MakeDropdown(string tooltip = null) {
		OptionButton dropdown = new() {
			TooltipText = tooltip ?? "",
			ClipText = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 22),
		};
		StyleBoxFlat normal = Box(new Color(0.93f, 0.88f, 0.75f), Brown, 1, 2);
		StyleBoxFlat hover = Box(new Color(0.97f, 0.93f, 0.82f), Brown, 1, 2);
		StyleBoxFlat disabled = Box(new Color(0.9f, 0.87f, 0.8f, 0.6f), new Color(Brown, 0.5f), 1, 2);
		dropdown.AddThemeStyleboxOverride("normal", normal);
		dropdown.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		dropdown.AddThemeStyleboxOverride("hover", hover);
		dropdown.AddThemeStyleboxOverride("pressed", hover);
		dropdown.AddThemeStyleboxOverride("disabled", disabled);
		foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) {
			dropdown.AddThemeColorOverride(color, Ink);
		}
		dropdown.AddThemeColorOverride("font_disabled_color", new Color(0.35f, 0.3f, 0.25f));
		// The arrow takes the text's color, dark on the parchment.
		dropdown.AddThemeConstantOverride("modulate_arrow", 1);
		dropdown.AddThemeFontSizeOverride("font_size", 13);
		PopupMenu popup = dropdown.GetPopup();
		popup.SetTransparentBackground(false);
		popup.MaxSize = new Vector2I(popup.MaxSize.X, 320);
		return dropdown;
	}

	private static void AddItems(OptionButton dropdown, IEnumerable<string> items, int selected) {
		foreach (string item in items) {
			dropdown.AddItem(item);
		}
		for (int i = 0; i < dropdown.ItemCount; ++i) {
			dropdown.GetPopup().SetItemAsRadioCheckable(i, false);
		}
		dropdown.Select(Math.Clamp(selected, 0, dropdown.ItemCount - 1));
	}

	// A dark text field, as Civ3's game name and chat line are.
	private static LineEdit MakeDarkField() {
		LineEdit field = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		StyleBoxFlat style = Box(DarkField, Brown, 1, 3);
		field.AddThemeStyleboxOverride("normal", style);
		field.AddThemeStyleboxOverride("focus", style);
		field.AddThemeStyleboxOverride("read_only", Box(new Color(DarkField, 0.75f), Brown, 1, 3));
		field.AddThemeColorOverride("font_color", FieldText);
		field.AddThemeColorOverride("font_uneditable_color", new Color(FieldText, 0.55f));
		field.AddThemeColorOverride("font_placeholder_color", new Color(FieldText, 0.45f));
		field.AddThemeColorOverride("caret_color", FieldText);
		field.AddThemeFontSizeOverride("font_size", 14);
		return field;
	}

	private static Button MakeButton(string text, string tooltip, bool highlighted = false) {
		Button button = new() { Text = text, TooltipText = tooltip, CustomMinimumSize = new Vector2(96, 26) };
		// Launch has the light border Civ3 gives it.
		Color border = highlighted ? new Color(1f, 0.98f, 0.9f) : Brown;
		int width = highlighted ? 2 : 1;
		Color background = highlighted ? new Color(0.83f, 0.74f, 0.55f) : new Color(0.9f, 0.84f, 0.7f);
		button.AddThemeStyleboxOverride("normal", Box(background, border, width, 3));
		button.AddThemeStyleboxOverride("hover", Box(background.Lightened(0.15f), border, width, 3));
		button.AddThemeStyleboxOverride("pressed", Box(background.Darkened(0.1f), border, width, 3));
		button.AddThemeStyleboxOverride("disabled", Box(new Color(background, 0.5f), new Color(border, 0.5f), width, 3));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.35f, 0.3f));
		button.AddThemeFontSizeOverride("font_size", 14);
		return button;
	}

	private Civ3Checkbox MakeRule(string text, bool on, string tooltip, bool available = true) {
		Civ3Checkbox checkbox = new() {
			Text = text,
			FontSize = 13,
			ButtonPressed = on,
			TooltipText = tooltip,
			Disabled = !available,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		if (!available) {
			checkbox.Modulate = new Color(1, 1, 1, 0.5f);
		}
		return checkbox;
	}

	// ---- The screen's parts ----

	private void AddTitle() {
		// A parchment banner on the stone, as Civ3's title has.
		PanelContainer banner = new();
		StyleBoxFlat style = Box(new Color(0.97f, 0.93f, 0.8f), Brown, 2, 2);
		style.ContentMarginLeft = 28;
		style.ContentMarginRight = 28;
		style.ShadowColor = new Color(0, 0, 0, 0.35f);
		style.ShadowSize = 3;
		banner.AddThemeStyleboxOverride("panel", style);
		Label title = new() { Text = "MULTIPLAYER SETUP", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontOverride("font", SpacedFont(6));
		title.AddThemeFontSizeOverride("font_size", 26);
		banner.AddChild(title);
		CenterContainer center = new();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		center.OffsetBottom = BorderTop;
		center.AddChild(banner);
		frame.AddChild(center);
	}

	private Control MakeGameSettings() {
		(PanelContainer panel, VBoxContainer box) = MakePanel("GAME SETTINGS");
		panel.CustomMinimumSize = new Vector2(268, 0);

		gameNameEdit = MakeDarkField();
		gameNameEdit.Text = MultiplayerSlots.DefaultGameName(HostName);
		gameNameEdit.MaxLength = C7Relay.RelayProtocol.MaxGameNameLength;
		gameNameEdit.TooltipText = "The game's name, as the online game list shows it.";
		box.AddChild(gameNameEdit);

		ScrollContainer scroll = new() {
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		box.AddChild(scroll);
		VBoxContainer settings = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		settings.AddThemeConstantOverride("separation", 2);
		scroll.AddChild(settings);

		Enum.TryParse(choices.MultiplayerGameType, out MultiplayerGameType lastType);
		if (choices.MultiplayerGameType == null) {
			lastType = MultiplayerGameType.Lan;
		}
		gameType = AddSetting(settings, "Game Type:", GameTypes.Select(t => t.text),
			Array.FindIndex(GameTypes, t => t.type == lastType), "Who plays, and where.");
		for (int i = 0; i < GameTypes.Length; ++i) {
			gameType.SetItemTooltip(i, GameTypes[i].tooltip);
			gameType.SetItemDisabled(i, GameTypes[i].type == null);
		}
		gameType.ItemSelected += _ => ShowGameType();

		gameMode = AddSetting(settings, "Game Mode:", GameModes.Select(m => m.text), 0,
			"Sets the special rules below: Elimination, Regicide, Mass Regicide or Victory Point Scoring, or none of them for Standard.");
		for (int i = 0; i < GameModes.Length; ++i) {
			MultiplayerGameMode mode = GameModes[i].mode;
			bool needsKings = mode is MultiplayerGameMode.Regicide or MultiplayerGameMode.MassRegicide;
			// Custom is what a mix of the rules is called, not a choice.
			gameMode.SetItemDisabled(i, mode == MultiplayerGameMode.Custom || (needsKings && !hasKings));
			if (needsKings && !hasKings) {
				gameMode.SetItemTooltip(i, "These rules have no King units.");
			}
		}
		gameMode.ItemSelected += index => ChooseGameMode(GameModes[index].mode);

		List<string> timers = LanLobby.TurnTimeChoices.Select(m => m == 0 ? "None" : $"{m} Minutes").ToList();
		turnTimer = AddSetting(settings, "Turn Timer:", timers, Math.Max(0, Array.IndexOf(LanLobby.TurnTimeChoices, choices.TurnTimerMinutes ?? 0)),
			"How long each player has for their turn before it ends for them. With simultaneous moves, how long everyone has for the round.");

		WorldSize lastSize = NewGameChoices.DefaultWorldSize(save.WorldSizes);
		int sizeIndex = choices.WorldSize == NewGameChoices.Random
			? save.WorldSizes.Count
			: save.WorldSizes.IndexOf(choices.ChosenWorldSize(save.WorldSizes, new Random()));
		worldSize = AddSetting(settings, "World Size:", save.WorldSizes.Select(w => w.name).Append(NewGameChoices.Random),
			sizeIndex < 0 ? save.WorldSizes.IndexOf(lastSize) : sizeIndex,
			"The world's size, which sets how many civilizations it has room for: a slot for each.");
		worldSize.ItemSelected += _ => MakeSlotRows();

		barbarians = AddSetting(settings, "Barbarian Activity:", BarbarianLevels.Select(b => b.ToString()).Append(NewGameChoices.Random),
			choices.Barbarians is BarbarianActivity b ? Array.IndexOf(BarbarianLevels, b) : BarbarianLevels.Length);
		landMass = AddSetting(settings, "Land Mass:", ["Pangaea", "Continents", "Archipelago"], Array.IndexOf(Landforms, choices.Landform));
		waterCoverage = AddSetting(settings, "Water Coverage:", ["60% Water", "70% Water", "80% Water"], Array.IndexOf(Oceans, choices.OceanCoverage));
		climate = AddSetting(settings, "Climate:", ["Arid", "Normal", "Wet"], Array.IndexOf(Climates, choices.Climate));
		temperature = AddSetting(settings, "Temperature:", ["Warm", "Temperate", "Cool"], Array.IndexOf(Temperatures, choices.Temperature));
		age = AddSetting(settings, "Age:", ["3 Billion", "4 Billion", "5 Billion"], Array.IndexOf(Ages, choices.Age));
		difficulty = AddSetting(settings, "Difficulty:", save.Difficulties.Select(d => d.Name),
			save.Difficulties.IndexOf(choices.ChosenDifficulty(save.Difficulties)), "How hard the computer's civilizations play.");

		// Civ3's Game Limits: OpenCiv3's limit is the turn limit.
		Button limits = MakeButton("Game Limits", "When the game ends if no one has won: the turn limit.");
		limits.CustomMinimumSize = new Vector2(110, 24);
		limits.AddThemeFontSizeOverride("font_size", 12);
		limits.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		limits.Pressed += () => gameLimits.PopupCentered();
		box.AddChild(limits);
		MakeGameLimits();
		return panel;
	}

	private OptionButton AddSetting(VBoxContainer box, string label, IEnumerable<string> items, int selected, string tooltip = null) {
		Label name = new() { Text = label };
		name.AddThemeFontSizeOverride("font_size", 12);
		box.AddChild(name);
		OptionButton dropdown = MakeDropdown(tooltip);
		AddItems(dropdown, items, selected);
		box.AddChild(dropdown);
		return dropdown;
	}

	private void MakeGameLimits() {
		gameLimits = new AcceptDialog { Title = "Game Limits", OkButtonText = "OK" };
		VBoxContainer box = new();
		turnLimit = MakeRule($"Turn limit ({save.TimeOptions.turnLimit} turns)", victoryConditions.UseTurnLimit,
			$"End the game after turn {save.TimeOptions.turnLimit}, scoring who leads. Without it, the game goes on until someone wins.");
		box.AddChild(turnLimit);
		Label note = new() {
			Text = "Civ3's time and score limits aren't in OpenCiv3 yet.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(280, 0),
		};
		note.AddThemeFontSizeOverride("font_size", 12);
		box.AddChild(note);
		gameLimits.AddChild(box);
		AddChild(gameLimits);
	}

	private Control MakePlayerSetup() {
		(PanelContainer panel, VBoxContainer box) = MakePanel("PLAYER SETUP");
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.SizeFlagsStretchRatio = 1.4f;

		HBoxContainer headings = new();
		headings.AddThemeConstantOverride("separation", 10);
		foreach (string heading in new[] { "Players", "Civilizations" }) {
			Label label = new() { Text = heading, SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center };
			label.AddThemeFontSizeOverride("font_size", 13);
			headings.AddChild(label);
		}
		box.AddChild(headings);

		// Room for eight rows, as Civ3's screen has; a bigger world's
		// slots scroll.
		ScrollContainer scroll = new() {
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 8 * 29),
		};
		box.AddChild(scroll);
		VBoxContainer list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		list.AddThemeConstantOverride("separation", 3);
		scroll.AddChild(list);

		// The host's row: their name, with their civilization's color.
		HBoxContainer hostRow = new();
		hostRow.AddThemeConstantOverride("separation", 10);
		HBoxContainer nameCell = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		hostNameButton = new Button {
			Text = HostName,
			Alignment = HorizontalAlignment.Left,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ToggleMode = true,
			ButtonPressed = true,
			ClipText = true,
			TooltipText = "You, hosting. Rename changes the name you play under.",
		};
		hostNameButton.AddThemeStyleboxOverride("pressed", Box(new Color(0.85f, 0.78f, 0.6f), Brown, 1, 2));
		hostNameButton.AddThemeStyleboxOverride("normal", Box(new Color(0, 0, 0, 0), new Color(Brown, 0.4f), 1, 2));
		hostNameButton.AddThemeStyleboxOverride("hover", Box(new Color(0.93f, 0.88f, 0.75f), Brown, 1, 2));
		hostNameButton.AddThemeColorOverride("font_pressed_color", Ink);
		hostNameButton.Pressed += () => SelectRow(-1);
		nameCell.AddChild(hostNameButton);
		hostColor = new Panel { CustomMinimumSize = new Vector2(16, 16), SizeFlagsVertical = SizeFlags.ShrinkCenter };
		nameCell.AddChild(hostColor);
		hostRow.AddChild(nameCell);
		hostCivilization = MakeDropdown("The civilization you play.");
		string hostCiv = choices.ChoseRandomCivilization ? NewGameChoices.Random : choices.ChosenCivilization(save.Civilizations).name;
		AddItems(hostCivilization, civNames, civNames.IndexOf(hostCiv));
		hostCivilization.ItemSelected += _ => UpdateCivilizations();
		hostRow.AddChild(hostCivilization);
		list.AddChild(hostRow);

		slotRows = new VBoxContainer();
		slotRows.AddThemeConstantOverride("separation", 3);
		list.AddChild(slotRows);
		slots = MultiplayerSlots.SlotsAsLastTime(choices, GameType, SlotCount());
		MakeSlotRows();

		HBoxContainer buttons = new() { Alignment = BoxContainer.AlignmentMode.Center };
		buttons.AddThemeConstantOverride("separation", 24);
		renameButton = MakeButton("Rename", "Rename the selected player: you, or a player at this computer in hotseat.");
		renameButton.Pressed += Rename;
		buttons.AddChild(renameButton);
		launchButton = MakeButton("Launch", "Start the game: in hotseat straight away; on the network, in the lobby, once a player takes each Open slot.", true);
		launchButton.Pressed += Launch;
		buttons.AddChild(launchButton);
		box.AddChild(buttons);
		return panel;
	}

	// A slot for each civilization the world has room for, but the host's.
	private int SlotCount() {
		WorldSize size = worldSize.Selected < save.WorldSizes.Count ? save.WorldSizes[worldSize.Selected]
			: NewGameChoices.DefaultWorldSize(save.WorldSizes);
		return Math.Max(1, size.numberOfCivs - 1);
	}

	// Makes the rivals' rows, keeping what was set for the slots that are
	// still there.
	private void MakeSlotRows() {
		int count = SlotCount();
		while (slots.Count < count) {
			slots.Add(new PlayerSlot(SlotKind.Computer, NewGameChoices.Random));
		}
		foreach ((OptionButton kind, OptionButton civ) in rows) {
			kind.GetParent().QueueFree();
		}
		rows.Clear();
		for (int i = 0; i < count; ++i) {
			int slot = i;
			HBoxContainer row = new();
			row.AddThemeConstantOverride("separation", 10);
			OptionButton kind = MakeDropdown();
			AddItems(kind, ["Open", "Closed", "Computer"], (int)slots[i].kind);
			kind.ItemSelected += index => {
				slots[slot] = slots[slot] with { kind = (SlotKind)index };
				SelectRow(slot);
				ShowSlots();
			};
			kind.Pressed += () => SelectRow(slot);
			row.AddChild(kind);
			OptionButton civ = MakeDropdown();
			AddItems(civ, civNames, civNames.IndexOf(slots[i].civilization ?? NewGameChoices.Random) is int c && c >= 0 ? c : civNames.Count - 1);
			civ.ItemSelected += index => {
				slots[slot] = slots[slot] with { civilization = civNames[(int)index] };
				UpdateCivilizations();
			};
			row.AddChild(civ);
			slotRows.AddChild(row);
			rows.Add((kind, civ));
		}
		if (selectedRow >= count) {
			SelectRow(-1);
		}
		ShowSlots();
	}

	// Shows each slot as the game type has it: in hotseat an Open slot is a
	// player at this computer, under their name; on the network, a seat
	// whose player chooses their civilization in the lobby.
	private void ShowSlots() {
		if (rows.Count == 0 || gameType == null) {
			return;
		}
		bool hotseat = GameType == MultiplayerGameType.Hotseat;
		int playerNumber = 1;
		for (int i = 0; i < rows.Count; ++i) {
			(OptionButton kind, OptionButton civ) = rows[i];
			PlayerSlot slot = slots[i];
			bool open = slot.kind == SlotKind.Open;
			if (open && hotseat) {
				++playerNumber;
			}
			kind.SetItemText(0, open && hotseat ? NewGameChoices.HotseatName(slot.name, playerNumber) : "Open");
			kind.TooltipText = hotseat
				? "Open: another player at this computer. Closed: no civilization. Computer: a civilization the computer plays."
				: "Open: a seat for a player joining over the network. Closed: no civilization. Computer: a civilization the computer plays.";
			bool networkSeat = open && !hotseat;
			civ.Disabled = slot.kind == SlotKind.Closed || networkSeat;
			civ.TooltipText = networkSeat ? "Whoever takes this seat chooses their civilization in the lobby; Random if they don't."
				: slot.kind == SlotKind.Closed ? "No one plays this slot."
				: "This player's civilization.";
			civ.Select(civ.Disabled ? civNames.Count - 1 : Math.Max(0, civNames.IndexOf(slot.civilization ?? NewGameChoices.Random)));
		}
		UpdateCivilizations();
		renameButton?.SetDeferred(BaseButton.PropertyName.Disabled, !CanRename(selectedRow));
	}

	// Each civilization can be chosen once: those chosen for one slot are
	// greyed out in the others.
	private void UpdateCivilizations() {
		List<OptionButton> lists = [hostCivilization, .. rows.Where((r, i) => !r.civ.Disabled).Select(r => r.civ)];
		Dictionary<string, OptionButton> chosenBy = [];
		foreach (OptionButton list in lists) {
			string name = civNames[list.Selected];
			if (name != NewGameChoices.Random) {
				chosenBy.TryAdd(name, list);
			}
		}
		foreach (OptionButton list in lists.Concat(rows.Select(r => r.civ)).Distinct()) {
			for (int i = 0; i < civNames.Count - 1; ++i) {
				list.SetItemDisabled(i, chosenBy.TryGetValue(civNames[i], out OptionButton by) && by != list);
			}
		}
		string host = civNames[hostCivilization.Selected];
		Civilization civ = save.Civilizations.Find(c => c.name == host);
		StyleBoxFlat square = Box(civ == null ? new Color(0.6f, 0.6f, 0.6f) : TextureLoader.LoadColor(civ.primaryColorIndex), Ink, 1, 0);
		hostColor.AddThemeStyleboxOverride("panel", square);
		hostColor.TooltipText = civ == null ? "Your color, once your civilization is drawn." : $"Your color, as the {civ.noun}.";
	}

	private void SelectRow(int row) {
		selectedRow = row;
		hostNameButton.SetPressedNoSignal(row == -1);
		renameButton?.SetDeferred(BaseButton.PropertyName.Disabled, !CanRename(row));
	}

	// The host can always be renamed, and in hotseat the other players at
	// this computer; people joining bring their own names.
	private bool CanRename(int row) {
		return row == -1 || (row < slots.Count && slots[row].kind == SlotKind.Open && GameType == MultiplayerGameType.Hotseat);
	}

	private void Rename() {
		int row = selectedRow;
		if (!CanRename(row)) {
			return;
		}
		ConfirmationDialog dialog = new() { Title = "Rename", OkButtonText = "OK", CancelButtonText = "Cancel" };
		LineEdit name = new() {
			Text = row == -1 ? HostName : slots[row].name ?? "",
			PlaceholderText = row == -1 ? LanSession.DefaultPlayerName : rows[row].kind.GetItemText(0),
			MaxLength = C7Engine.Network.LanHost.MaxPlayerNameLength,
			CustomMinimumSize = new Vector2(260, 0),
		};
		dialog.AddChild(name);
		dialog.RegisterTextEnter(name);
		dialog.Confirmed += () => {
			string text = name.Text.Trim();
			if (row == -1) {
				string oldDefault = MultiplayerSlots.DefaultGameName(HostName);
				LanSession.PlayerName = text;
				hostNameButton.Text = HostName;
				// The game keeps its host's name, unless it was renamed.
				if (gameNameEdit.Text == oldDefault) {
					gameNameEdit.Text = MultiplayerSlots.DefaultGameName(HostName);
				}
				AddMessage($"You are now {HostName}.");
			} else {
				slots[row] = slots[row] with { name = text == "" ? null : text };
				ShowSlots();
			}
		};
		dialog.VisibilityChanged += () => {
			if (!dialog.Visible) {
				dialog.QueueFree();
			}
		};
		AddChild(dialog);
		dialog.PopupCentered();
		name.GrabFocus();
	}

	private Control MakeGameRules() {
		(PanelContainer panel, VBoxContainer box) = MakePanel("GAME RULES");
		ScrollContainer scroll = new() {
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.ShowAlways,
			CustomMinimumSize = new Vector2(0, 5 * 24),
		};
		box.AddChild(scroll);
		GridContainer grid = new() { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 0);
		scroll.AddChild(grid);

		VictoryConditions vc = victoryConditions;
		dominationVictory = MakeRule("Allow Domination Victory", vc.AllowDominationVictory,
			$"Win by controlling {vc.DominationTerritoryPercent}% of the world's land and {vc.DominationPopulationPercent}% of its population.");
		spaceRaceVictory = MakeRule("Allow Space Race Victory", vc.AllowSpaceRaceVictory,
			"Win by being the first to build every part of the spaceship. Without it, the spaceship can't be built.");
		diplomaticVictory = MakeRule("Allow Diplomatic Victory", vc.AllowDiplomaticVictory,
			"Win by being elected Secretary General in a United Nations vote. Without it, the United Nations holds no votes.");
		conquestVictory = MakeRule("Allow Conquest Victory", vc.AllowConquestVictory, "Win by eliminating every rival civilization.");
		culturalVictory = MakeRule("Allow Cultural Victory", vc.AllowCulturalVictory,
			$"Win when one of your cities has {vc.CultureOneCityWin:N0} culture, or your civilization has {vc.CultureAllCitiesWin:N0} culture "
			+ "(on a standard map; less on smaller worlds, more on larger) and twice as much as any rival.");
		wonderVictory = MakeRule("Allow Wonder Victory", vc.AllowWonderVictory,
			"The game ends as soon as every great wonder has been built, and the civilization with the most victory points wins.");
		acceleratedProduction = MakeRule("Accelerated Production", choices.AcceleratedProduction ?? save.Rules.AcceleratedProduction,
			"Cities generate double the food, shields and commerce each turn, speeding up growth, research and production.");
		elimination = MakeRule("Elimination", vc.CityElimination,
			vc.CityEliminationCount == 1
				? "A civilization that loses a city to an enemy is eliminated: its units and cities are destroyed."
				: $"A civilization that loses {vc.CityEliminationCount} cities to enemies is eliminated: its units and cities are destroyed.");
		const string noKings = "These rules have no King units.";
		regicide = MakeRule("Regicide", hasKings && vc.Regicide,
			hasKings ? "Each civilization has a King. A civilization whose King is killed is eliminated: its units and cities are destroyed." : noKings,
			hasKings);
		massRegicide = MakeRule("Mass Regicide", hasKings && vc.MassRegicide,
			hasKings ? $"Each civilization has {InstantElimination.MassRegicideKings} Kings. A civilization whose Kings are all killed is eliminated: "
				+ "its units and cities are destroyed." : noKings,
			hasKings);
		victoryPointScoring = MakeRule("Victory Point Scoring", vc.VictoryLocations,
			$"Win by being the first to {vc.VictoryPointLimit:N0} victory points, earned by building wonders, defeating enemy units, discovering "
			+ "advances, conquering cities and holding Victory Locations (each civilization's starting location) with a defending unit. "
			+ "At the turn limit, the most victory points wins.");
		// Civ3's rules OpenCiv3 doesn't have, shown as Civ3 lists them.
		Civ3Checkbox linkedStarts = MakeRule("Culturally Linked Start Loc.", false, "Not in OpenCiv3 yet: civilizations start anywhere.", false);
		Civ3Checkbox noRestart = MakeRule("Don't Restart Eliminated Civilizations", true,
			"OpenCiv3 never restarts an eliminated civilization.", false);
		Civ3Checkbox noGoodyHuts = MakeRule("No Goody Huts", false, "Not in OpenCiv3 yet.", false);
		// OpenCiv3's own rules, which its player setup screen has too.
		simultaneousMoves = MakeRule("Simultaneous Moves", true,
			"Every human plays their turn at the same time, rather than waiting for each other. The computer's civilizations move after them.");
		showScoreboard = MakeRule("Show Scoreboard", choices.ShowScoreboard ?? true,
			"With more than one human player, show everyone's score and how long the current turn has taken.");
		cultureFlips = MakeRule("Culture Flipping", choices.CultureFlips ?? save.Rules.AllowCultureFlips,
			"Cities may defect to a neighboring civilization whose culture outweighs their owner's. A garrison makes it less likely.");
		scientificLeaders = MakeRule("Scientific Leaders", choices.ScientificLeaders ?? save.Rules.ScientificLeaders,
			"The first civilization to discover an advance may gain a scientific leader (3% chance, 5% for scientific civilizations), "
			+ "who can complete anything a city is building, wonders included.");
		coreCitiesFreeOfCorruption = MakeRule("No Corruption Near Capital", choices.CoreCitiesFreeOfCorruption ?? false,
			"Custom rule: the capital and the 5 cities nearest it have no corruption or waste.");

		// Two columns, as Civ3's: the victories on the left, the other rules
		// on the right; then the rest.
		foreach (Control rule in new Control[] {
			dominationVictory, acceleratedProduction,
			spaceRaceVictory, elimination,
			diplomaticVictory, regicide,
			conquestVictory, massRegicide,
			culturalVictory, victoryPointScoring,
			wonderVictory, linkedStarts,
			noRestart, noGoodyHuts,
			simultaneousMoves, showScoreboard,
			cultureFlips, scientificLeaders,
			coreCitiesFreeOfCorruption,
		}) {
			grid.AddChild(rule);
		}
		foreach (CheckBox special in new[] { elimination, regicide, massRegicide, victoryPointScoring }) {
			special.Toggled += _ => ShowGameMode();
		}
		ShowGameMode();
		return panel;
	}

	private void ChooseGameMode(MultiplayerGameMode mode) {
		VictoryConditions rules = new() {
			CityElimination = elimination.ButtonPressed,
			Regicide = regicide.ButtonPressed,
			MassRegicide = massRegicide.ButtonPressed,
			VictoryLocations = victoryPointScoring.ButtonPressed,
		};
		MultiplayerSlots.ApplyGameMode(rules, mode);
		settingGameMode = true;
		elimination.ButtonPressed = rules.CityElimination;
		regicide.ButtonPressed = hasKings && rules.Regicide;
		massRegicide.ButtonPressed = hasKings && rules.MassRegicide;
		victoryPointScoring.ButtonPressed = rules.VictoryLocations;
		settingGameMode = false;
		ShowGameMode();
	}

	// The game mode the special rules make.
	private void ShowGameMode() {
		if (settingGameMode || gameMode == null) {
			return;
		}
		MultiplayerGameMode mode = MultiplayerSlots.GameModeOf(new VictoryConditions {
			CityElimination = elimination.ButtonPressed,
			Regicide = regicide.ButtonPressed,
			MassRegicide = massRegicide.ButtonPressed,
			VictoryLocations = victoryPointScoring.ButtonPressed,
		});
		gameMode.Select(Array.FindIndex(GameModes, m => m.mode == mode));
	}

	private Control MakeMessages() {
		(PanelContainer panel, VBoxContainer box) = MakePanel("MESSAGES");
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		messages = new RichTextLabel {
			BbcodeEnabled = true,
			ScrollFollowing = true,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 60),
			SelectionEnabled = true,
		};
		messages.AddThemeColorOverride("default_color", Ink);
		messages.AddThemeFontSizeOverride("normal_font_size", 13);
		box.AddChild(messages);
		return panel;
	}

	private void AddMessage(string text, Color? color = null) {
		string escaped = text.Replace("[", "[lb]");
		messages.AppendText(color is Color c ? $"[color=#{c.ToHtml(false)}]{escaped}[/color]\n" : escaped + "\n");
	}

	// The chat line and the exit button, on the frame's bottom border.
	private void AddBottomStrip() {
		HBoxContainer strip = new();
		strip.AddThemeConstantOverride("separation", 12);
		strip.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		strip.OffsetLeft = BorderSide + 8;
		strip.OffsetRight = -BorderSide + 24;
		strip.OffsetTop = -BorderBottom + 24;
		strip.OffsetBottom = -22;
		frame.AddChild(strip);

		// The lobby has no chat yet, so neither does this screen.
		LineEdit chat = MakeDarkField();
		chat.Editable = false;
		chat.PlaceholderText = "Chat isn't in OpenCiv3 yet.";
		chat.TooltipText = "Players can't chat before or during a game yet. Messages above show what happens on this screen.";
		chat.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		strip.AddChild(chat);

		TextureButton exit = new() {
			TooltipText = "Back to the main menu",
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Shortcut = new Shortcut { Events = [new InputEventKey { Keycode = Key.Escape }] },
		};
		TextureLoader.SetButtonTextures(exit, "ui.exit");
		exit.Pressed += BackToMenu;
		strip.AddChild(exit);
	}

	// ---- Game type ----

	private void ShowGameType() {
		MultiplayerGameType type = GameType;
		bool hotseat = type == MultiplayerGameType.Hotseat;
		// Only a networked game has a turn clock and simultaneous turns (see
		// LanHost), and a name to list it under.
		turnTimer.Disabled = hotseat;
		simultaneousMoves.Disabled = hotseat;
		simultaneousMoves.Modulate = new Color(1, 1, 1, hotseat ? 0.5f : 1);
		gameNameEdit.Editable = !hotseat;
		string networkOnly = hotseat ? " Only in a networked game." : "";
		turnTimer.TooltipText = "How long each player has for their turn before it ends for them. With simultaneous moves, how long everyone "
			+ "has for the round." + networkOnly;
		ShowSlots();
		messages.Clear();
		AddMessage($"Setting up {(hotseat ? "a hotseat game" : gameNameEdit.Text)}, hosted by {HostName}.");
		AddMessage(type switch {
			MultiplayerGameType.Hotseat => "Each Open slot is another player at this computer, taking turns with you. Rename gives them their names.",
			MultiplayerGameType.Internet => "Launch opens the lobby and hosts the game online too: players anywhere join with the join code it shows, "
				+ "and players on your network from \"Join LAN Game\". They choose their civilizations there.",
			_ => "Launch opens the lobby, where players on your network join the Open slots from \"Join LAN Game\" and choose their civilizations. "
				+ "You start the game there once every Open slot is taken.",
		});
	}

	// ---- Launch ----

	private void Launch() {
		if (launching) {
			return;
		}
		MultiplayerGameType type = GameType;
		Random random = new();
		MultiplayerPlan plan = MultiplayerSlots.Plan(type, civNames[hostCivilization.Selected], slots, save.Civilizations, random);
		if (plan.problem != null) {
			AddMessage(plan.problem, ProblemColor);
			return;
		}

		VictoryConditions vc = victoryConditions;
		vc.AllowDominationVictory = dominationVictory.ButtonPressed;
		vc.AllowSpaceRaceVictory = spaceRaceVictory.ButtonPressed;
		vc.AllowDiplomaticVictory = diplomaticVictory.ButtonPressed;
		vc.AllowConquestVictory = conquestVictory.ButtonPressed;
		vc.AllowCulturalVictory = culturalVictory.ButtonPressed;
		vc.AllowWonderVictory = wonderVictory.ButtonPressed;
		vc.CityElimination = elimination.ButtonPressed;
		vc.Regicide = regicide.ButtonPressed;
		vc.MassRegicide = massRegicide.ButtonPressed;
		vc.VictoryLocations = victoryPointScoring.ButtonPressed;
		vc.UseTurnLimit = turnLimit.ButtonPressed;

		WorldCharacteristics world = new(save) {
			worldSize = ChosenWorldSize(random, plan),
			barbarianActivity = barbarians.Selected < BarbarianLevels.Length ? BarbarianLevels[barbarians.Selected]
				: BarbarianLevels[random.Next(BarbarianLevels.Length)],
			landform = Landforms[landMass.Selected],
			oceanCoverage = Oceans[waterCoverage.Selected],
			climate = Climates[climate.Selected],
			temperature = Temperatures[temperature.Selected],
			age = Ages[age.Selected],
			mapSeed = random.Next(),
		};

		GameSetup setup = new() {
			playerCivilization = plan.playerCivilization,
			// Names only matter when there's more than one human.
			playerName = HostName,
			hotseatPlayers = plan.hotseatPlayers,
			difficulty = save.Difficulties[difficulty.Selected],
			worldCharacteristics = world,
			opponents = plan.opponents,
			victoryConditions = vc,
			showScoreboard = showScoreboard.ButtonPressed,
			coreCitiesFreeOfCorruption = coreCitiesFreeOfCorruption.ButtonPressed,
			acceleratedProduction = acceleratedProduction.ButtonPressed,
			allowCultureFlips = cultureFlips.ButtonPressed,
			scientificLeaders = scientificLeaders.ButtonPressed,
			// The rate chosen on the player setup screen, or the rules'.
			corruptionRate = choices.CorruptionRate,
			invalidMapsDirectory = InvalidMaps.FolderIfEnabled,
		};
		RememberChoices(type);

		global.ResetLoadGameFields();
		global.Scenario = GameScenario.None;
		global.WatchNextGame = false;
		global.WorldCharacteristics = world;
		global.SaveGame = save;
		global.NewGameChoices = choices;
		SetLaunching(true);

		if (type == MultiplayerGameType.Hotseat) {
			LanSession.HostNextGame = false;
			AddMessage("Creating the world...");
			// World generation can take a while, so not on the UI thread.
			Task.Run(() => {
				try {
					setup.Populate(save);
				} catch (Exception e) {
					Callable.From(() => LaunchFailed(e)).CallDeferred();
					return;
				}
				Callable.From(StartGame).CallDeferred();
			});
			return;
		}

		// The guests choose their civilizations in the lobby, and the game is
		// created once the host starts it there.
		int minutes = LanLobby.TurnTimeChoices[turnTimer.Selected];
		string name = gameNameEdit.Text.Trim();
		LanSession.HostNextGame = true;
		LanSession.PendingGame = new PendingLanGame(setup, save, plan.guestSeats,
			gameName: name == "" ? null : name,
			turnTimeLimit: minutes == 0 ? null : TimeSpan.FromMinutes(minutes),
			simultaneousTurns: simultaneousMoves.ButtonPressed,
			hostOnline: type == MultiplayerGameType.Internet);
		StartGame();
	}

	// The world size chosen; for Random, one with room for everyone if there
	// is one.
	private WorldSize ChosenWorldSize(Random random, MultiplayerPlan plan) {
		if (worldSize.Selected < save.WorldSizes.Count) {
			return save.WorldSizes[worldSize.Selected];
		}
		int civs = 1 + plan.opponents.Count + plan.hotseatPlayers.Count + plan.guestSeats;
		List<WorldSize> roomy = save.WorldSizes.Where(w => w.numberOfCivs >= civs).ToList();
		List<WorldSize> pool = roomy.Count > 0 ? roomy : save.WorldSizes;
		return pool[random.Next(pool.Count)];
	}

	// Remembers this screen's choices for next time and Quick Start, as
	// the world and player setup screens do.
	private void RememberChoices(MultiplayerGameType type) {
		try {
			choices.WorldSize = worldSize.Selected < save.WorldSizes.Count ? save.WorldSizes[worldSize.Selected].name : NewGameChoices.Random;
			choices.Barbarians = barbarians.Selected < BarbarianLevels.Length ? BarbarianLevels[barbarians.Selected] : null;
			choices.Landform = Landforms[landMass.Selected];
			choices.OceanCoverage = Oceans[waterCoverage.Selected];
			choices.Climate = Climates[climate.Selected];
			choices.Temperature = Temperatures[temperature.Selected];
			choices.Age = Ages[age.Selected];
			choices.Civilization = civNames[hostCivilization.Selected];
			choices.Difficulty = save.Difficulties[difficulty.Selected].Name;
			MultiplayerSlots.RememberSlots(choices, type, slots.Take(rows.Count).ToList());
			choices.TurnTimerMinutes = LanLobby.TurnTimeChoices[turnTimer.Selected];
			choices.SetVictoryConditions(victoryConditions);
			choices.ShowScoreboard = showScoreboard.ButtonPressed;
			choices.CoreCitiesFreeOfCorruption = coreCitiesFreeOfCorruption.ButtonPressed;
			choices.AcceleratedProduction = acceleratedProduction.ButtonPressed;
			choices.CultureFlips = cultureFlips.ButtonPressed;
			choices.ScientificLeaders = scientificLeaders.ButtonPressed;
			choices.Save();
		} catch (Exception e) {
			log.Error(e, "Could not remember the multiplayer setup");
		}
	}

	private void SetLaunching(bool on) {
		launching = on;
		launchButton.Disabled = on;
		renameButton.Disabled = on || !CanRename(selectedRow);
	}

	private void LaunchFailed(Exception e) {
		log.Error(e, "Couldn't create the game");
		if (!IsInstanceValid(this) || !IsInsideTree()) {
			return;
		}
		SetLaunching(false);
		AddMessage($"Couldn't create the game: {e.Message}", ProblemColor);
		// The rules were changed making the world; start again from fresh.
		save = GameMode.Load(GamePaths.GameModesDir, GamePaths.GameMode).GetSave();
	}

	private void StartGame() {
		if (!IsInstanceValid(this) || !IsInsideTree()) {
			return;
		}
		try {
			LanSession.StartGame(GetTree());
		} catch (Exception e) {
			LaunchFailed(e);
		}
	}

	private void BackToMenu() {
		LanSession.HostNextGame = false;
		LanSession.PendingGame = null;
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}
}

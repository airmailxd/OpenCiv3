using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using C7Engine;
using C7GameData;
using Godot;
using Serilog;

// Conquests' Campaign Record: each of its campaigns, with the best win at it
// or "Not Yet Victorious" (Civ3 FUN_004b0ba0 sets it up, FUN_004b1140 draws
// it). Civ3 shows it from the main menu's Hall of Fame, with a "Next" button
// for the next campaign to play and a button for the Hall of Fame itself
// (Civ3 FUN_0055a5e0, menu item 6), and after a campaign is won, with
// neither (Civ3 FUN_004f9fb0). Its wording is Civ3's (labels.txt,
// #CAMPAIGN_RECORD).
public partial class CampaignRecordScreen : Control {
	private static readonly ILogger log = Log.ForContext<CampaignRecordScreen>();

	// Where the record is kept.
	public const string FilePath = "user://" + CampaignRecord.FileName;

	public static string GlobalPath => ProjectSettings.GlobalizePath(FilePath);

	public event Action Closed;
	// The Hall of Fame button.
	public event Action HallOfFameRequested;
	// The Next button, with the campaign's scenario file.
	public event Action<string> NextCampaignRequested;

	// Whether it's shown from the main menu, with its Next and Hall of Fame
	// buttons (Civ3 FUN_004b0ba0's argument).
	private readonly bool fromMainMenu;

	// The columns' left edges on Civ3's 1024x768 screen (Civ3 FUN_004b1140):
	// the headings at y 125 in an 18 point font, a double rule under them at
	// y 149 and 152 from x 176 to 816, and a row every 24 pixels from y 173
	// in a 12 point font.
	private const int CampaignX = 0xc4;
	private const int ScoreX = 0x19c;
	private const int CivilizationX = 0x20e;
	private const int DifficultyX = 0x2c2;
	private const int HeadingY = 0x7d;
	private const int FirstRowY = 0xad;
	private const int RowHeight = 0x18;

	public CampaignRecordScreen(bool fromMainMenu) {
		this.fromMainMenu = fromMainMenu;
		MouseFilter = MouseFilterEnum.Stop;
	}

	public CampaignRecordScreen() : this(true) { }

	// The record, made if there's none yet from the campaigns beside the
	// game. Empty without Civ3's Conquests.
	public static List<CampaignRecordEntry> LoadRecord() {
		Dictionary<string, string> scenarios = CampaignScenarios();
		string civ3Record = Civ3File(Path.Combine("Conquests", CampaignRecord.Civ3FileName));
		return CampaignRecord.LoadOrCreate(GlobalPath, civ3Record, scenarios.Keys, CampaignNames());
	}

	// Records the player's win, if the game is one of the campaigns.
	// Returns whether it was recorded, for the screen to be shown.
	public static bool RecordVictory(GameData gameData, Player player, IVictory victory, int score) {
		if (string.IsNullOrEmpty(gameData?.scenarioName)) {
			return false;
		}
		List<CampaignRecordEntry> entries = LoadRecord();
		if (!CampaignRecord.RecordVictory(entries, gameData, player, victory, score, DateTime.UtcNow)) {
			return false;
		}
		CampaignRecord.Save(GlobalPath, entries);
		return true;
	}

	// The campaigns' names, from Conquests' labels.txt.
	private static List<string> CampaignNames() {
		string labels = Civ3File(Path.Combine("Conquests", "Text", "labels.txt"));
		if (labels == null) {
			return [];
		}
		try {
			return CampaignRecord.CampaignNames(File.ReadAllText(labels, Encoding.Latin1));
		} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
			log.Warning(e, "Couldn't read the campaigns' names from {Path}", labels);
			return [];
		}
	}

	// The scenario files beside Conquests, by name without the extension, in
	// the order Civ3 finds them (Civ3 FUN_004afd50 looks for "*.BIQ" there).
	private static Dictionary<string, string> CampaignScenarios() {
		Dictionary<string, string> result = new();
		string directory = Civ3File("Conquests");
		if (directory == null || !Directory.Exists(directory)) {
			return result;
		}
		try {
			foreach (string file in Directory.GetFiles(directory)
					.Where(f => Path.GetExtension(f).Equals(".biq", StringComparison.OrdinalIgnoreCase))
					.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)) {
				result.TryAdd(Path.GetFileNameWithoutExtension(file), file);
			}
		} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
			log.Warning(e, "Couldn't list the scenarios in {Path}", directory);
		}
		return result;
	}

	// A file or folder in the Civ3 install, or null without one.
	private static string Civ3File(string relativePath) {
		if (C7Settings.UseStandaloneMode() || !GamePaths.Civ3Available()) {
			return null;
		}
		string path = Path.Combine(Util.Civ3Root, relativePath);
		return File.Exists(path) || Directory.Exists(path) ? path : null;
	}

	public override void _Ready() {
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		CenterContainer center = new();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);
		ColorRect dimmer = new() { Color = new Color(0, 0, 0, 0.75f), MouseFilter = MouseFilterEnum.Ignore };
		dimmer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dimmer);
		MoveChild(dimmer, 0);

		Control screen = new() { CustomMinimumSize = new Vector2(1024, 768) };
		center.AddChild(screen);

		ImageTexture art = TryLoadTexture("screens.campaign_record.background");
		if (art != null) {
			screen.AddChild(new TextureRect { Texture = art });
		} else {
			// Without Civ3's art, a parchment with the screen's title.
			screen.AddChild(new ColorRect { Color = Color.Color8(196, 180, 140), Size = new Vector2(1024, 768) });
			screen.AddChild(new ColorRect { Color = Color.Color8(240, 226, 190), Position = new Vector2(140, 60), Size = new Vector2(744, 630) });
			Label title = MakeLabel("CAMPAIGNS", 28, Colors.Black);
			title.Position = new Vector2(0, 70);
			title.Size = new Vector2(1024, 40);
			title.HorizontalAlignment = HorizontalAlignment.Center;
			screen.AddChild(title);
		}

		AddText(screen, "Campaign", CampaignX, HeadingY, 18, Colors.Black);
		AddText(screen, "Score", ScoreX, HeadingY, 18, Colors.Black);
		AddText(screen, "Civilization", CivilizationX, HeadingY, 18, Colors.Black);
		AddText(screen, "Difficulty", DifficultyX, HeadingY, 18, Colors.Black);
		screen.AddChild(new ColorRect { Color = Colors.Black, Position = new Vector2(0xb0, 0x95), Size = new Vector2(0x330 - 0xb0, 1) });
		screen.AddChild(new ColorRect { Color = Colors.Black, Position = new Vector2(0xb0, 0x98), Size = new Vector2(0x330 - 0xb0, 1) });

		List<CampaignRecordEntry> entries = LoadRecord();
		Dictionary<string, string> scenarios = fromMainMenu ? CampaignScenarios() : new();
		CampaignRecordEntry next = fromMainMenu ? CampaignRecord.NextCampaign(entries, scenarios.ContainsKey) : null;
		// Not yet won is greyed (Civ3 draws it in 0x8410, mid grey).
		Color grey = Color.Color8(128, 128, 128);
		int y = FirstRowY;
		foreach (CampaignRecordEntry entry in entries) {
			AddText(screen, entry.Campaign, CampaignX, y, 13, Colors.Black);
			if (entry.Victorious) {
				AddText(screen, entry.AdjustedScore.ToString(), ScoreX, y, 13, Colors.Black);
				AddText(screen, entry.Civilization, CivilizationX, y, 13, Colors.Black);
				AddText(screen, entry.Difficulty, DifficultyX, y, 13, Colors.Black);
			} else {
				AddText(screen, "Not Yet Victorious", ScoreX, y, 13, grey);
			}
			if (ReferenceEquals(entry, next)) {
				// The Next button sits in the Difficulty column of the first
				// campaign not yet won that can be played.
				string path = scenarios[entry.Campaign];
				Control button = MakeNextButton();
				button.Position = new Vector2(DifficultyX, y - 0xb);
				button.TooltipText = "Play the Next Campaign";
				screen.AddChild(button);
				((BaseButton)button).Pressed += () => {
					NextCampaignRequested?.Invoke(path);
					Close();
				};
			}
			y += RowHeight;
		}

		// Civ3's Hall of Fame and Exit buttons are at the bottom right, at
		// x 960 and 990, y 730.
		HBoxContainer buttons = new() { Position = new Vector2(820, 726) };
		buttons.AddThemeConstantOverride("separation", 8);
		if (fromMainMenu) {
			Button hallOfFame = new() { Text = "Hall of Fame", TooltipText = "Hall of Fame" };
			hallOfFame.Pressed += () => {
				HallOfFameRequested?.Invoke();
				Close();
			};
			buttons.AddChild(hallOfFame);
		}
		Button exit = new() { Text = "Exit", TooltipText = "Exit", CustomMinimumSize = new Vector2(60, 0) };
		exit.Pressed += Close;
		buttons.AddChild(exit);
		screen.AddChild(buttons);
	}

	// The Next button: Civ3's from campRec.pcx with "Next" on it, or a plain
	// one without the art.
	private static Control MakeNextButton() {
		ImageTexture normal = TryLoadTexture("screens.campaign_record.next.normal");
		if (normal == null) {
			return new Button { Text = "Next", Size = new Vector2(86, 24) };
		}
		TextureButton button = new() {
			TextureNormal = normal,
			TextureHover = TryLoadTexture("screens.campaign_record.next.hover"),
			TexturePressed = TryLoadTexture("screens.campaign_record.next.pressed"),
		};
		Label label = MakeLabel("Next", 13, Colors.Black);
		label.Size = new Vector2(86, 24);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.MouseFilter = MouseFilterEnum.Ignore;
		button.AddChild(label);
		return button;
	}

	private static ImageTexture TryLoadTexture(string key) {
		try {
			return TextureLoader.TryLoad(key);
		} catch (Exception e) {
			log.Warning(e, "Couldn't load {Key}", key);
			return null;
		}
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape }) {
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	private void Close() {
		Closed?.Invoke();
		QueueFree();
	}

	private static void AddText(Control parent, string text, int x, int y, int fontSize, Color color) {
		Label label = MakeLabel(text, fontSize, color);
		label.Position = new Vector2(x, y);
		parent.AddChild(label);
	}

	private static Label MakeLabel(string text, int fontSize, Color color) {
		Label label = new() { Text = text };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}
}

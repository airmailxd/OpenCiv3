using System;
using System.Collections.Generic;
using C7GameData;
using Godot;

// Civ3's Hall of Fame, from the main menu: the games played to the end on
// this computer, best score first. Its wording is Civ3's (labels.txt,
// #HALL_OF_FAME_WIN), but for the Date column, which is ours.
public partial class HallOfFameScreen : Control {
	// Where the Hall of Fame is kept.
	public const string FilePath = "user://" + HallOfFame.FileName;

	public static string GlobalPath => ProjectSettings.GlobalizePath(FilePath);

	public event Action Closed;

	private OptionButton _leaderChoice;
	private VBoxContainer _rows;
	private List<HallOfFameEntry> _entries = [];

	// The columns: their heading, width and alignment.
	private static readonly (string heading, float width, HorizontalAlignment align)[] Columns = [
		("Rank", 50, HorizontalAlignment.Center),
		("Score", 60, HorizontalAlignment.Right),
		("Name", 120, HorizontalAlignment.Left),
		("Civilization", 105, HorizontalAlignment.Left),
		("Difficulty", 80, HorizontalAlignment.Left),
		("Victory Type", 135, HorizontalAlignment.Left),
		("Date", 90, HorizontalAlignment.Left),
	];

	// Where the list is on the background (Civ3's
	// hall_o_fame_background.pcx), inside its scalloped border.
	private static readonly Rect2 ListArea = new(165, 264, 700, 400);

	public HallOfFameScreen() {
		MouseFilter = MouseFilterEnum.Stop;
	}

	public override void _Ready() {
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		// The screen is Civ3's 1024x768, centered over the menu.
		CenterContainer center = new();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);
		ColorRect dimmer = new() { Color = new Color(0, 0, 0, 0.75f), MouseFilter = MouseFilterEnum.Ignore };
		dimmer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dimmer);
		MoveChild(dimmer, 0);

		Control screen = new() { CustomMinimumSize = new Vector2(1024, 768) };
		center.AddChild(screen);

		ImageTexture art = TextureLoader.TryLoad("screens.hall_of_fame.background");
		if (art != null) {
			screen.AddChild(new TextureRect { Texture = art });
		} else {
			// Without Civ3's art, a parchment in a frame.
			screen.AddChild(new ColorRect { Color = Color.Color8(196, 180, 140), Size = new Vector2(1024, 768) });
			screen.AddChild(new ColorRect { Color = Color.Color8(240, 226, 190), Position = new Vector2(140, 60), Size = new Vector2(744, 630) });
		}

		Label title = MakeLabel("HALL OF FAME", 28, 1024, HorizontalAlignment.Center);
		title.Position = new Vector2(0, 8);
		screen.AddChild(title);

		HBoxContainer filter = new() { Position = new Vector2(175, 130) };
		filter.AddThemeConstantOverride("separation", 8);
		filter.AddChild(MakeLabel("List high scores for:", 15));
		_leaderChoice = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
		_leaderChoice.ItemSelected += _ => Fill();
		filter.AddChild(_leaderChoice);
		screen.AddChild(filter);

		// The headings, in the band across the background.
		HBoxContainer headings = MakeRow(Columns, c => c.heading, 14);
		headings.Position = new Vector2(ListArea.Position.X, 218);
		screen.AddChild(headings);

		ScrollContainer scroll = new() {
			Position = ListArea.Position,
			Size = ListArea.Size,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rows.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(_rows);
		screen.AddChild(scroll);

		Button exit = new() { Text = "Exit", Position = new Vector2(472, 700), CustomMinimumSize = new Vector2(80, 30) };
		exit.Pressed += Close;
		screen.AddChild(exit);

		_entries = HallOfFame.Load(GlobalPath);
		_leaderChoice.AddItem(HallOfFame.Everyone);
		foreach (string leader in HallOfFame.Leaders(_entries)) {
			_leaderChoice.AddItem(leader);
		}
		Fill();
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

	private void Fill() {
		foreach (Node child in _rows.GetChildren()) {
			_rows.RemoveChild(child);
			child.QueueFree();
		}
		string leader = _leaderChoice.Selected <= 0 ? HallOfFame.Everyone : _leaderChoice.GetItemText(_leaderChoice.Selected);
		List<(int rank, HallOfFameEntry entry)> ranked = HallOfFame.Ranked(_entries, leader);
		if (ranked.Count == 0) {
			_rows.AddChild(MakeLabel("None", 14, ListArea.Size.X, HorizontalAlignment.Center));
			return;
		}
		foreach ((int rank, HallOfFameEntry entry) in ranked) {
			string[] cells = [
				rank.ToString(), entry.Score.ToString(), entry.Leader, entry.Civilization,
				entry.Difficulty, entry.Outcome, entry.RecordedAt.ToLocalTime().ToString("yyyy-MM-dd"),
			];
			int column = 0;
			HBoxContainer row = MakeRow(Columns, _ => cells[column++], 13);
			row.TooltipText = string.IsNullOrEmpty(entry.GameDate)
				? $"Turn {entry.Turn}"
				: $"Turn {entry.Turn}, {entry.GameDate}" + (entry.Winner.Length > 0 && !entry.Won ? $". Won by the {entry.Winner}." : "");
			row.MouseFilter = MouseFilterEnum.Pass;
			_rows.AddChild(row);
		}
	}

	private static HBoxContainer MakeRow((string heading, float width, HorizontalAlignment align)[] columns,
			Func<(string heading, float width, HorizontalAlignment align), string> text, int fontSize) {
		HBoxContainer row = new();
		row.AddThemeConstantOverride("separation", 10);
		foreach (var column in columns) {
			Label label = MakeLabel(text(column), fontSize, column.width, column.align);
			label.MouseFilter = MouseFilterEnum.Pass;
			row.AddChild(label);
		}
		return row;
	}

	private static Label MakeLabel(string text, int fontSize, float width = 0, HorizontalAlignment align = HorizontalAlignment.Left) {
		Label label = new() {
			Text = text,
			HorizontalAlignment = align,
			ClipText = width > 0,
			TextOverrunBehavior = width > 0 ? TextServer.OverrunBehavior.TrimEllipsis : TextServer.OverrunBehavior.NoTrimming,
			CustomMinimumSize = new Vector2(width, 0),
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", Colors.Black);
		return label;
	}
}

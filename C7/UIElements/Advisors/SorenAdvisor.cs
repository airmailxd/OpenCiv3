using System;
using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Godot;

// Civ3's hidden "Soren" advisor, a developer screen: every other civ in the
// game, eight at a time, with its government and its ties (see SorenReport
// for what Civ3 has of it). Civ3 has no art for it, so it is a plain page
// with the eight rows ruled off (Civ3 FUN_005b1260) and its two buttons in
// the bottom corners (Civ3 FUN_005b0d00: Next at the window's width - 25,
// height - 30; Previous at 10, height - 30), labelled from labels.txt
// (#SOREN_ADVISOR_BUTTONS: "->" "Next", "<-" "Previous").
public partial class SorenAdvisor : Control {
	// Whether the screen may be opened. Civ3 1.22 never opens it at all (see
	// SorenReport), so OpenCiv3 keeps it behind a developer key
	// (Ctrl+Shift+Alt+S, see Game.cs), and not in a LAN game, where it would
	// show a player what the other civs have between them. This is the
	// check to tie to OpenCiv3's debug mode once there is one.
	public static Func<bool> IsAvailable = () => !LanSession.IsActive;

	private static readonly Vector2 WindowSize = new(1024, 768);
	private static readonly Color Paper = Color.Color8(222, 206, 166);
	private static readonly Color Rule = Color.Color8(120, 96, 60);

	private readonly Label[] _headings = new Label[SorenReport.RowsShown];
	private readonly Label[] _rows = new Label[SorenReport.RowsShown];
	private Button _next;
	private Button _previous;

	private int _first;
	private int _count;

	public SorenAdvisor() {
		MouseFilter = MouseFilterEnum.Stop;
		CustomMinimumSize = WindowSize;
		Size = WindowSize;
	}

	public override void _Ready() {
		AdvisorUtils.CreateUI(this, CreateUI);
	}

	private void CreateUI() {
		AddChild(new ColorRect { Color = Paper, Size = WindowSize, MouseFilter = MouseFilterEnum.Ignore });

		// Eight rows of (height - 20) / 8, below a 9 pixel margin, ruled off
		// by the seven lines between them (Civ3 FUN_005b1260).
		float rowHeight = (WindowSize.Y - 20) / SorenReport.RowsShown;
		for (int i = 0; i < SorenReport.RowsShown; i++) {
			float top = 9 + i * rowHeight;
			if (i > 0) {
				AddChild(new ColorRect { Color = Rule, Position = new Vector2(0, top), Size = new Vector2(WindowSize.X, 2) });
			}
			_headings[i] = AdvisorUtils.CreateLabel(this, "", new Rect2(16, top + 4, 300, 20), 15);
			_rows[i] = AdvisorUtils.CreateLabel(this, "", new Rect2(16, top + 24, WindowSize.X - 120, rowHeight - 28), 12);
			_rows[i].VerticalAlignment = VerticalAlignment.Top;
			_rows[i].AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_rows[i].TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		}

		_next = MakeButton("->", "Next", new Vector2(WindowSize.X - 25 - 40, WindowSize.Y - 30));
		_next.Pressed += () => Step(1);
		_previous = MakeButton("<-", "Previous", new Vector2(10, WindowSize.Y - 30));
		_previous.Pressed += () => Step(-1);

		TextureButton close = AdvisorUtils.CreateExitButton(this);
		close.Pressed += () => { this.GetParent<Advisors>().Hide(); };
	}

	private Button MakeButton(string text, string tooltip, Vector2 position) {
		Button button = new() { Text = text, TooltipText = tooltip, Position = position, CustomMinimumSize = new Vector2(40, 24) };
		AddChild(button);
		return button;
	}

	// Opening starts again from the first civ (Civ3 FUN_005b0ef0).
	public void ShowAdvisor() {
		_first = 0;
		Show();
		Fill();
	}

	// One civ on or back (Civ3 FUN_005b0ae0, FUN_005b0c60).
	private void Step(int by) {
		_first = SorenReport.ClampFirst(_first + by, _count);
		Fill();
	}

	private void Fill() {
		EngineStorage.ReadGameData((GameData gameData) => {
			Player viewer = gameData.GetUIControllerPlayer();
			List<Player> civs = SorenReport.Civs(gameData, viewer);
			_count = civs.Count;
			_first = SorenReport.ClampFirst(_first, _count);
			for (int i = 0; i < SorenReport.RowsShown; i++) {
				int index = _first + i;
				if (index < civs.Count) {
					_headings[i].Text = SorenReport.Heading(civs[index]);
					_rows[i].Text = string.Join(".  ", SorenReport.Lines(gameData, viewer, civs[index])) + ".";
				} else {
					_headings[i].Text = "";
					_rows[i].Text = "";
				}
			}
		});
		_previous.Visible = SorenReport.HasPrevious(_first);
		_next.Visible = SorenReport.HasNext(_first, _count);
	}

	// Left and Right, or 4 and 6 on the numpad, step through the civs, as
	// does the mouse wheel (Civ3 FUN_005b0ae0, FUN_005b0c60).
	public override void _GuiInput(InputEvent @event) {
		if (@event is InputEventMouseButton { Pressed: true } mouse) {
			if (mouse.ButtonIndex == MouseButton.WheelDown) {
				Step(1);
				AcceptEvent();
			} else if (mouse.ButtonIndex == MouseButton.WheelUp) {
				Step(-1);
				AcceptEvent();
			}
		}
	}

	public override void _UnhandledKeyInput(InputEvent @event) {
		if (!IsVisibleInTree() || @event is not InputEventKey { Pressed: true } key) {
			return;
		}
		if (key.Keycode is Key.Left or Key.Kp4) {
			Step(-1);
			GetViewport().SetInputAsHandled();
		} else if (key.Keycode is Key.Right or Key.Kp6) {
			Step(1);
			GetViewport().SetInputAsHandled();
		}
	}
}

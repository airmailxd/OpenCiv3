using System;
using System.Collections.Generic;
using System.Linq;
using C7.Textures;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using Godot;

// The scoreboard in the corner of a hotseat or LAN game, as in Civ3's
// multiplayer: the time left in the current turn, and each player's score,
// whether they are connected (on a LAN), and whether they have finished their
// turn. It can be folded down to just the clock and this machine's own
// player. A spectator also chooses here how to watch the game, from the ways
// the host allows (see SpectatorViewMode).
public partial class Scoreboard : PanelContainer {
	// How often the clock and scores are read again, in seconds.
	private const double RefreshInterval = 0.25;

	private static readonly Color PanelColor = new(0.80f, 0.76f, 0.58f);
	private static readonly Color RowColor = new(0.89f, 0.86f, 0.70f);
	private static readonly Color ActiveRowColor = new(0.98f, 0.94f, 0.66f);
	private static readonly Color BorderColor = new(0.30f, 0.26f, 0.16f);
	private static readonly Color TextColor = new(0.10f, 0.08f, 0.04f);
	private static readonly Color DoneBoxColor = new(0.96f, 0.96f, 0.94f);
	private static readonly Color WaitingBoxColor = new(0.30f, 0.30f, 0.30f);
	private static readonly Color PlentyOfTime = new(0.20f, 0.70f, 0.25f);
	private static readonly Color RunningLow = new(0.85f, 0.70f, 0.15f);
	private static readonly Color AlmostOut = new(0.80f, 0.20f, 0.15f);

	private Button foldButton;
	private ProgressBar timeBar;
	private StyleBoxFlat timeBarFill;
	private Label timeLabel;
	private VBoxContainer rowList;

	// A spectator's ways to watch the game, and what the choice shows them
	// for, to make it again only when that changes.
	private OptionButton viewChoice;
	private readonly List<SpectatorViewInfo> viewChoices = [];
	private string shownViewChoices;
	private double sinceRefresh = RefreshInterval;
	private bool folded = false;

	// The rows and the player each shows.
	private readonly List<(Control control, Player player)> shownRowControls = new();

	// What the rows last showed, so they are rebuilt only when it changes.
	private List<RowSummary> shownRows;

	// Everything a row shows, so the rows are only made again when it changes.
	private readonly record struct RowSummary(ID playerID, int rank, int score, bool connected, bool away, bool done, bool active,
		string displayedName, bool isHuman, bool lanActive);

	// The style boxes every row uses, shared by all rows. They must not be
	// changed.
	private static readonly StyleBoxFlat RowBox = Box(RowColor, BorderColor, 1, 2);
	private static readonly StyleBoxFlat ActiveRowBox = Box(ActiveRowColor, BorderColor, 1, 2);
	private static readonly StyleBoxFlat DoneBox = Box(DoneBoxColor, BorderColor, 1, 0);
	private static readonly StyleBoxFlat WaitingBox = Box(WaitingBoxColor, BorderColor, 1, 0);

	// Without a LAN host keeping time (in a hotseat game), the scoreboard
	// times the turns itself.
	private ID localClockPlayerID;
	private int localClockTurn = -1;
	private readonly System.Diagnostics.Stopwatch localClock = new();

	private record Row(Player player, int rank, int score, bool connected, bool away, bool done, bool active);

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Stop;
		AddThemeStyleboxOverride("panel", Box(PanelColor, BorderColor, 2, 4));

		VBoxContainer layout = new();
		layout.AddThemeConstantOverride("separation", 3);
		AddChild(layout);

		HBoxContainer header = new() { Alignment = BoxContainer.AlignmentMode.End };
		header.AddThemeConstantOverride("separation", 6);
		layout.AddChild(header);

		foldButton = new Button {
			Text = "▲",
			TooltipText = "Hide the other players",
			CustomMinimumSize = new Vector2(28, 22),
			FocusMode = FocusModeEnum.None,
		};
		foldButton.AddThemeFontSizeOverride("font_size", 12);
		foldButton.Pressed += ToggleFolded;
		header.AddChild(foldButton);

		timeBarFill = Box(PlentyOfTime, BorderColor, 0, 0);
		timeBar = new ProgressBar {
			MinValue = 0,
			MaxValue = 1,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(70, 18),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		timeBar.AddThemeStyleboxOverride("background", Box(WaitingBoxColor, BorderColor, 1, 0));
		timeBar.AddThemeStyleboxOverride("fill", timeBarFill);
		header.AddChild(timeBar);

		PanelContainer timeBox = new();
		timeBox.AddThemeStyleboxOverride("panel", Box(new Color(0.12f, 0.35f, 0.18f), BorderColor, 1, 2));
		timeLabel = new Label {
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(56, 0),
		};
		timeLabel.AddThemeFontSizeOverride("font_size", 14);
		timeLabel.AddThemeColorOverride("font_color", Colors.White);
		timeBox.AddChild(timeLabel);
		header.AddChild(timeBox);

		if (LanSession.IsSpectator) {
			HBoxContainer watching = new() { Alignment = BoxContainer.AlignmentMode.End };
			watching.AddThemeConstantOverride("separation", 6);
			Label label = MakeText("Watching:");
			watching.AddChild(label);
			viewChoice = new OptionButton {
				FocusMode = FocusModeEnum.None,
				TooltipText = "How you see the game: as one civilization or all of them, which shows only what they know, "
					+ "or the whole game, as the host allows.",
			};
			viewChoice.AddThemeFontSizeOverride("font_size", 14);
			viewChoice.ItemSelected += index => {
				if (index >= 0 && index < viewChoices.Count) {
					LanSession.Client?.ChooseSpectatorView(viewChoices[(int)index]);
				}
			};
			watching.AddChild(viewChoice);
			layout.AddChild(watching);
		}

		rowList = new VBoxContainer();
		rowList.AddThemeConstantOverride("separation", 2);
		layout.AddChild(rowList);
	}

	public override void _Process(double delta) {
		sinceRefresh += delta;
		if (sinceRefresh < RefreshInterval) {
			return;
		}
		sinceRefresh = 0;
		EngineStorage.ReadGameData(Refresh);
	}

	private void ToggleFolded() {
		folded = !folded;
		foldButton.Text = folded ? "▼" : "▲";
		foldButton.TooltipText = folded ? "Show the other players" : "Hide the other players";
		ShowFoldedRows();
	}

	// Folded, only our own player's row stays, so our score is always in
	// view. A spectator has no player of their own.
	private void ShowFoldedRows() {
		ID ownID = LanSession.IsSpectator ? null : EngineStorage.uiControllerID;
		foreach ((Control control, Player player) in shownRowControls) {
			control.Visible = !folded || player.id == ownID;
		}
		ShrinkToFit();
	}

	private void Refresh(GameData gameData) {
		if (gameData == null) {
			return;
		}
		TurnClockInfo clock = LanSession.TurnClock ?? LocalClock(gameData);
		ShowClock(gameData, clock);
		ShowViewChoices(gameData);

		List<Row> rows = ReadRows(gameData, clock);
		List<RowSummary> summary = new(rows.Count);
		foreach (Row r in rows) {
			summary.Add(new RowSummary(r.player.id, r.rank, r.score, r.connected, r.away, r.done, r.active,
				DisplayedName(r), r.player.isHuman, LanSession.IsActive));
		}
		if (shownRows != null && summary.SequenceEqual(shownRows)) {
			return;
		}
		shownRows = summary;
		foreach (Node child in rowList.GetChildren()) {
			child.QueueFree();
		}
		shownRowControls.Clear();
		foreach (Row row in rows) {
			Control control = MakeRow(row);
			rowList.AddChild(control);
			shownRowControls.Add((control, row.player));
		}
		ShowFoldedRows();
	}

	// A spectator's ways to watch, with the one it watches by chosen.
	private void ShowViewChoices(GameData gameData) {
		LanClient client = LanSession.Client;
		if (viewChoice == null || client == null) {
			return;
		}
		SpectatorViews allowed = client.AllowedSpectatorViews;
		SpectatorViewInfo current = client.SpectatorView;
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated).ToList();
		string summary = $"{allowed}|{current}|{string.Join(",", civs.Select(p => p.id.ToString()))}";
		if (summary == shownViewChoices) {
			return;
		}
		shownViewChoices = summary;
		viewChoice.Clear();
		viewChoices.Clear();
		if (allowed.HasFlag(SpectatorViews.AllCivs)) {
			viewChoices.Add(new SpectatorViewInfo(SpectatorViewMode.AllCivs));
			viewChoice.AddItem("All civilizations");
		}
		if (allowed.HasFlag(SpectatorViews.OneCiv)) {
			foreach (Player civ in civs) {
				viewChoices.Add(new SpectatorViewInfo(SpectatorViewMode.OneCiv, civ.id));
				viewChoice.AddItem($"The {civ.civilization.name}");
			}
		}
		if (allowed.HasFlag(SpectatorViews.Omniscient)) {
			viewChoices.Add(new SpectatorViewInfo(SpectatorViewMode.Omniscient));
			viewChoice.AddItem("The whole game");
		}
		int selected = viewChoices.IndexOf(current);
		if (selected < 0 && current != null) {
			// A civilization no longer in the game, say.
			viewChoices.Add(current);
			viewChoice.AddItem(current.mode == SpectatorViewMode.OneCiv
				? $"The {gameData.GetPlayer(current.playerID)?.civilization.name ?? "civilization"}" : current.mode.ToString());
			selected = viewChoices.Count - 1;
		}
		viewChoice.Select(selected);
		viewChoice.Disabled = viewChoices.Count < 2;
	}

	// How long the human whose turn it is has had it, timed from when this
	// machine first saw their turn begin. Hotseat turns have no time limit.
	private TurnClockInfo LocalClock(GameData gameData) {
		ID active = EngineStorage.activePlayerID;
		if (active != localClockPlayerID || gameData.turn != localClockTurn) {
			localClockPlayerID = active;
			localClockTurn = gameData.turn;
			localClock.Restart();
		}
		return new TurnClockInfo(active, gameData.turn, localClock.Elapsed.TotalSeconds, null, []);
	}

	// Collapses to the smallest size that fits, keeping the top right corner
	// where it is: the panel grows left and down from there.
	private void ShrinkToFit() {
		OffsetLeft = OffsetRight;
		OffsetBottom = OffsetTop;
	}

	// While a human plays, the time they have left, or how long they've
	// taken when there is no limit. Between humans, while the AIs move, it
	// stops. With simultaneous turns, it times the humans' round together.
	private void ShowClock(GameData gameData, TurnClockInfo clock) {
		bool running;
		string timeLeftTip, timeTakenTip;
		if (gameData.simultaneousTurns && clock?.playersToMove != null) {
			running = clock.playersToMove.Count > 0;
			timeLeftTip = "Time left for everyone's turn";
			timeTakenTip = "This turn has taken this long; turns have no time limit";
		} else {
			Player active = clock == null ? null : gameData.GetPlayer(clock.activePlayerID);
			running = active != null && active.isHuman && !active.hasPlayedThisTurn;
			timeLeftTip = running ? $"Time left for {NameOf(active)}'s turn" : null;
			timeTakenTip = running ? $"{NameOf(active)} has taken this long; turns have no time limit" : null;
		}
		if (!running) {
			timeLabel.Text = "--:--";
			timeBar.Value = 0;
			return;
		}

		if (clock.secondsAllowed is double allowed && allowed > 0) {
			double left = Math.Max(0, allowed - clock.secondsElapsed);
			double fraction = left / allowed;
			timeLabel.Text = FormatTime(left);
			timeBar.Value = fraction;
			timeBarFill.BgColor = fraction > 0.25 ? PlentyOfTime : fraction > 0.1 ? RunningLow : AlmostOut;
			timeBar.TooltipText = timeLeftTip;
		} else {
			timeLabel.Text = FormatTime(clock.secondsElapsed);
			timeBar.Value = 0;
			timeBar.TooltipText = timeTakenTip;
		}
	}

	private static string FormatTime(double seconds) {
		TimeSpan time = TimeSpan.FromSeconds(Math.Ceiling(seconds));
		return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time:mm\\:ss}" : $"{time:mm\\:ss}";
	}

	// Everyone still in the game, highest score first.
	private static List<Row> ReadRows(GameData gameData, TurnClockInfo clock) {
		HashSet<ID> connected = clock?.connectedPlayers?.ToHashSet() ?? [];
		HashSet<ID> away = clock?.awayPlayers?.ToHashSet() ?? [];
		ID activeID = clock?.activePlayerID ?? EngineStorage.activePlayerID;
		// Everyone moving, when the host says; with simultaneous turns, that
		// can be several players at once.
		HashSet<ID> moving = clock?.playersToMove?.ToHashSet() ?? (activeID == null ? [] : [activeID]);
		return gameData.players
			.Where(p => !p.isBarbarians && !p.defeated)
			.Select(p => (player: p, score: ScoreOf(gameData, p)))
			.OrderByDescending(ps => ps.score)
			.Select((ps, i) => new Row(
				ps.player,
				i + 1,
				ps.score,
				ps.player.isHuman && connected.Contains(ps.player.id),
				ps.player.isHuman && away.Contains(ps.player.id),
				ps.player.hasPlayedThisTurn,
				ps.player.isHuman && moving.Contains(ps.player.id) && !ps.player.hasPlayedThisTurn))
			.ToList();
	}

	private static int ScoreOf(GameData gameData, Player player) {
		return gameData.history != null && gameData.history.TryGetValue(player.id.ToString(), out List<HistTurnRecord> turns)
			? turns.LastOrDefault()?.Score ?? 0
			: 0;
	}

	private static string NameOf(Player player) {
		// Games without player names (e.g. older saves) fall back to the leader.
		return player.name ?? player.civilization.leader;
	}

	private static string DisplayedName(Row row) {
		string who = row.player.isHuman ? NameOf(row.player) : row.player.civilization.leader;
		return $"{row.rank}. {who} ({row.player.civilization.name})";
	}

	private Control MakeRow(Row row) {
		PanelContainer panel = new() { MouseFilter = MouseFilterEnum.Pass };
		panel.AddThemeStyleboxOverride("panel", row.active ? ActiveRowBox : RowBox);

		HBoxContainer cells = new();
		cells.AddThemeConstantOverride("separation", 6);
		panel.AddChild(cells);

		ColorRect swatch = new() {
			Color = TextureLoader.LoadColor(row.player.GetPlayerColor()),
			CustomMinimumSize = new Vector2(22, 16),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		cells.AddChild(swatch);

		Label name = MakeText(DisplayedName(row));
		name.MouseFilter = MouseFilterEnum.Pass;
		name.TooltipText = row.player.isHuman ? "" : "Played by the computer";
		name.CustomMinimumSize = new Vector2(220, 0);
		name.ClipText = true;
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		cells.AddChild(name);

		Label score = MakeText(row.score.ToString());
		score.CustomMinimumSize = new Vector2(36, 0);
		score.HorizontalAlignment = HorizontalAlignment.Right;
		cells.AddChild(score);

		// On a LAN, the squiggle shows a human is at their machine, and a z
		// one the game goes on without.
		Label link = MakeText(row.connected ? "~" : row.away ? "z" : "");
		link.Visible = LanSession.IsActive;
		link.CustomMinimumSize = new Vector2(16, 0);
		link.HorizontalAlignment = HorizontalAlignment.Center;
		link.AddThemeFontSizeOverride("font_size", 20);
		link.MouseFilter = MouseFilterEnum.Pass;
		link.TooltipText = row.connected ? "Connected"
			: row.away ? "Away: not connected, and their turns end by themselves"
			: row.player.isHuman ? "Not connected" : "";
		cells.AddChild(link);

		// The box turns white once the player has finished their turn.
		Panel turnBox = new() {
			CustomMinimumSize = new Vector2(16, 16),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Pass,
			TooltipText = row.done ? "Done: finished this turn" : row.active ? "Moving: playing their turn" : "Yet to play this turn",
		};
		turnBox.AddThemeStyleboxOverride("panel", row.done ? DoneBox : WaitingBox);
		cells.AddChild(turnBox);

		// The host can remove or ban the guest playing a human player.
		if (LanSession.Host?.Seats.FirstOrDefault(s => s.playerID == row.player.id) is SeatInfo { takenBy: string guest }) {
			name.TooltipText = $"{guest}. Right-click to remove or ban them.";
			panel.GuiInput += e => {
				if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) {
					ShowHostMenu(row.player.id, guest);
					AcceptEvent();
				}
			};
		}

		return panel;
	}

	// What the host can do about a guest, at the mouse.
	private void ShowHostMenu(ID playerID, string guest) {
		PopupMenu menu = new();
		menu.AddItem($"Remove {guest} from the game", 0);
		menu.AddItem($"Ban {guest}...", 1);
		menu.IdPressed += id => {
			if (id == 0) {
				HostActions.Remove(playerID);
			} else {
				HostActions.ConfirmBan(GetTree().Root, guest, playerID);
			}
		};
		menu.PopupHide += menu.QueueFree;
		AddChild(menu);
		menu.Position = (Vector2I)GetViewport().GetMousePosition();
		menu.Popup();
	}


	private static Label MakeText(string text) {
		Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", TextColor);
		return label;
	}

	private static StyleBoxFlat Box(Color fill, Color border, int borderWidth, int padding) {
		StyleBoxFlat box = new() { BgColor = fill, BorderColor = border };
		box.SetBorderWidthAll(borderWidth);
		box.SetContentMarginAll(padding);
		return box;
	}
}

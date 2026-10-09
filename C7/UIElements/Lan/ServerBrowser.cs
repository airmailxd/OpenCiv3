using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using C7Engine.Network;
using C7Relay;
using Godot;

// The server browser: the games hosts list publicly on the online relay
// chosen in the settings, to sort, filter and join, and the public games
// joined lately. It sits over the join screen, which does the joining.
public partial class ServerBrowser : Control {
	// Asked to join a game: its code, the relay, its name, whether to watch,
	// and the password, if the game needs one.
	public event Action<string, string, string, bool, string> JoinRequested;
	public event Action Closed;

	private string relayUrl;
	private Tree table;
	private Label status;
	private CheckBox hideFull;
	private CheckBox hidePassword;
	private CheckBox hideIncompatible;
	private Button joinButton;
	private Button watchButton;
	private HBoxContainer passwordRow;
	private LineEdit passwordEdit;
	private ItemList recentList;
	private TabContainer tabs;
	private Godot.Timer refreshTimer;

	// The games as last fetched, how long the relay takes to answer, and the
	// games shown, in the table's order.
	private List<PublicGame> games = [];
	private int? relayPingMs;
	private List<PublicGame> shown = [];
	private List<RecentOnlineGames.RecentGame> recent = [];

	private BrowserColumn sortColumn = BrowserColumn.Name;
	private bool sortAscending = true;

	// The fetch under way, called off by closing.
	private CancellationTokenSource fetching;

	private static readonly (BrowserColumn column, string title, int width)[] Columns = [
		(BrowserColumn.Name, "Game", 190),
		(BrowserColumn.Host, "Host", 100),
		(BrowserColumn.Players, "Players", 64),
		(BrowserColumn.OpenSeats, "Open", 50),
		(BrowserColumn.Turn, "Turn", 70),
		(BrowserColumn.MapSize, "Map", 70),
		(BrowserColumn.Simultaneous, "Simult.", 58),
		(BrowserColumn.TurnTimer, "Timer", 58),
		(BrowserColumn.Password, "Lock", 44),
		(BrowserColumn.Fog, "Fog", 40),
		(BrowserColumn.Ping, "Ping", 54),
	];

	private static readonly Color Incompatible = new(0.55f, 0.55f, 0.55f);

	public override void _Ready() {
		relayUrl = OnlineRelay.Url;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;

		ColorRect dimmer = new() { Color = new Color(0, 0, 0, 0.6f) };
		dimmer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dimmer);

		CenterContainer center = new();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		PanelContainer panel = new() { CustomMinimumSize = new Vector2(960, 600) };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
			BgColor = new Color(0.12f, 0.11f, 0.09f),
			BorderColor = new Color(0.55f, 0.48f, 0.30f),
			BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
			ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10,
		});
		center.AddChild(panel);

		VBoxContainer layout = new();
		layout.AddThemeConstantOverride("separation", 8);
		panel.AddChild(layout);

		HBoxContainer header = new();
		header.AddThemeConstantOverride("separation", 12);
		Uri relay = RelayConnection.BaseUri(relayUrl);
		Label title = new() {
			Text = $"Online Games on {relay?.Authority ?? relayUrl}",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipText = true,
		};
		title.AddThemeFontSizeOverride("font_size", 22);
		header.AddChild(title);
		header.AddChild(MakeButton("Refresh", Refresh));
		header.AddChild(MakeButton("Close", Close));
		layout.AddChild(header);

		HBoxContainer filters = new();
		filters.AddThemeConstantOverride("separation", 16);
		hideFull = MakeFilter(filters, "Hide full");
		hidePassword = MakeFilter(filters, "Hide password-protected");
		hideIncompatible = MakeFilter(filters, "Hide other versions");
		layout.AddChild(filters);

		tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		layout.AddChild(tabs);

		table = new Tree {
			Name = "Public Games",
			Columns = Columns.Length,
			ColumnTitlesVisible = true,
			HideRoot = true,
			SelectMode = Tree.SelectModeEnum.Row,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		for (int i = 0; i < Columns.Length; ++i) {
			table.SetColumnTitle(i, Columns[i].title);
			table.SetColumnCustomMinimumWidth(i, Columns[i].width);
			table.SetColumnExpand(i, i == 0);
			table.SetColumnClipContent(i, true);
		}
		table.ColumnTitleClicked += (column, _) => SortBy(Columns[(int)column].column);
		table.ItemSelected += ShowSelected;
		table.ItemActivated += () => JoinSelected(false);
		tabs.AddChild(table);

		recentList = new ItemList { Name = "Recent", SizeFlagsVertical = SizeFlags.ExpandFill };
		recentList.ItemActivated += index => JoinRecent((int)index);
		recentList.ItemSelected += _ => ShowSelected();
		tabs.AddChild(recentList);
		tabs.TabChanged += _ => ShowSelected();

		passwordRow = new HBoxContainer { Visible = false };
		passwordRow.AddThemeConstantOverride("separation", 12);
		Label passwordLabel = new() { Text = "This game needs a password:" };
		passwordLabel.AddThemeFontSizeOverride("font_size", 16);
		passwordRow.AddChild(passwordLabel);
		passwordEdit = new LineEdit { Secret = true, MaxLength = GamePassword.MaxLength, CustomMinimumSize = new Vector2(220, 0) };
		passwordEdit.TextSubmitted += _ => JoinSelected(false);
		passwordRow.AddChild(passwordEdit);
		layout.AddChild(passwordRow);

		HBoxContainer footer = new();
		footer.AddThemeConstantOverride("separation", 12);
		status = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		status.AddThemeFontSizeOverride("font_size", 15);
		footer.AddChild(status);
		joinButton = MakeButton("Join", () => JoinSelected(false));
		watchButton = MakeButton("Watch", () => JoinSelected(true));
		footer.AddChild(joinButton);
		footer.AddChild(watchButton);
		layout.AddChild(footer);

		refreshTimer = new Godot.Timer { WaitTime = PublicGames.RefreshInterval.TotalSeconds, Autostart = true };
		refreshTimer.Timeout += Refresh;
		AddChild(refreshTimer);

		ShowRecent();
		ShowSelected();
		Refresh();
	}

	private static Button MakeButton(string text, Action onPressed) {
		Button button = new() { Text = text, CustomMinimumSize = new Vector2(110, 32) };
		button.AddThemeFontSizeOverride("font_size", 16);
		button.Pressed += onPressed;
		return button;
	}

	private CheckBox MakeFilter(HBoxContainer row, string text) {
		CheckBox box = new() { Text = text };
		box.AddThemeFontSizeOverride("font_size", 16);
		box.Toggled += _ => ShowGames();
		row.AddChild(box);
		return box;
	}

	private void Close() {
		fetching?.Cancel();
		Closed?.Invoke();
		QueueFree();
	}

	// Before the lobby's own Escape, which leaves it.
	public override void _Input(InputEvent e) {
		if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) {
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	// Fetches the list again, and times the relay.
	private async void Refresh() {
		if (OnlineRelay.Problem(relayUrl) is string problem) {
			status.Text = problem;
			return;
		}
		fetching?.Cancel();
		CancellationTokenSource cancel = new();
		fetching = cancel;
		status.Text = "Asking the relay for its games...";
		PublicGameList list;
		int? ping;
		try {
			list = await PublicGames.FetchAsync(relayUrl, cancel.Token);
			ping = await PublicGames.PingRelayAsync(relayUrl, cancel.Token);
		} catch (OperationCanceledException) {
			return;
		} catch (RelayException e) {
			if (!cancel.IsCancellationRequested && IsInstanceValid(status)) {
				status.Text = e.Message;
			}
			return;
		}
		if (cancel.IsCancellationRequested || !IsInstanceValid(table)) {
			return;
		}
		fetching = null;
		games = list.games;
		relayPingMs = ping;
		ShowGames();
	}

	private void SortBy(BrowserColumn column) {
		sortAscending = column != sortColumn || !sortAscending;
		sortColumn = column;
		ShowGames();
	}

	// Shows the games as filtered and sorted, keeping the one selected.
	private void ShowGames() {
		string selectedCode = SelectedGame()?.code;
		BrowserFilter filter = new(hideFull.ButtonPressed, hidePassword.ButtonPressed, hideIncompatible.ButtonPressed);
		shown = PublicGames.Sort(PublicGames.Filter(games, filter), sortColumn, sortAscending, relayPingMs);

		table.Clear();
		TreeItem root = table.CreateItem();
		for (int i = 0; i < Columns.Length; ++i) {
			string arrow = Columns[i].column != sortColumn ? "" : sortAscending ? " ▲" : " ▼";
			table.SetColumnTitle(i, Columns[i].title + arrow);
		}
		foreach (PublicGame game in shown) {
			TreeItem item = table.CreateItem(root);
			GameListing listing = game.game;
			string[] cells = [
				listing.name,
				listing.hostName ?? "",
				$"{listing.seatsTaken}/{listing.seatsTotal}",
				game.seatsOpen.ToString(),
				listing.started ? listing.turn.ToString() : "in lobby",
				listing.mapSize ?? "",
				listing.simultaneousTurns ? "yes" : "no",
				listing.turnSeconds is double seconds ? FormatTime(seconds) : "none",
				listing.hasPassword ? "yes" : "",
				listing.hideUnseen ? "on" : "off",
				PublicGames.PingOf(game, relayPingMs) is int ping ? $"{ping} ms" : "?",
			];
			bool compatible = PublicGames.IsCompatible(game);
			for (int i = 0; i < cells.Length; ++i) {
				item.SetText(i, cells[i]);
				if (!compatible) {
					item.SetCustomColor(i, Incompatible);
				}
			}
			string tip = listing.description ?? "";
			if (listing.locale != null) {
				tip += (tip == "" ? "" : "\n") + $"Host's language: {listing.locale}";
			}
			if (!compatible) {
				tip += (tip == "" ? "" : "\n") + "This game runs another version of OpenCiv3, so you can't join it.";
			}
			item.SetTooltipText(0, tip);
			if (game.code == selectedCode) {
				item.Select(0);
			}
		}
		int hidden = games.Count - shown.Count;
		status.Text = games.Count == 0 ? "No games are listed on this relay right now. Refresh to look again."
			: $"{shown.Count} {(shown.Count == 1 ? "game" : "games")}" + (hidden > 0 ? $" ({hidden} hidden by the filters)" : "")
				+ ". Double-click a game to join it.";
		ShowSelected();
	}

	private static string FormatTime(double seconds) {
		return seconds >= 60 && seconds % 60 == 0 ? $"{seconds / 60:0} min" : $"{seconds:0} s";
	}

	private void ShowRecent() {
		recent = RecentOnlineGames.Load();
		recentList.Clear();
		foreach (RecentOnlineGames.RecentGame game in recent) {
			Uri relay = RelayConnection.BaseUri(game.relayUrl);
			recentList.AddItem($"{game.name}  ({RelayProtocol.FormatCode(game.code)} on {relay?.Authority ?? game.relayUrl})");
		}
		if (recent.Count == 0) {
			recentList.AddItem("Public games you join are listed here.", selectable: false);
		}
	}

	private bool OnRecentTab => tabs.CurrentTab == 1;

	private PublicGame SelectedGame() {
		TreeItem item = table.GetSelected();
		int index = item == null ? -1 : item.GetIndex();
		return index >= 0 && index < shown.Count ? shown[index] : null;
	}

	// Shows what can be done with the selected game.
	private void ShowSelected() {
		if (OnRecentTab) {
			bool any = recent.Count > 0 && recentList.GetSelectedItems().Length > 0;
			joinButton.Disabled = !any;
			watchButton.Disabled = !any;
			passwordRow.Visible = false;
			return;
		}
		PublicGame game = SelectedGame();
		bool compatible = game != null && PublicGames.IsCompatible(game);
		joinButton.Disabled = !compatible || game.seatsOpen == 0;
		watchButton.Disabled = !compatible || !game.game.spectatorsAllowed;
		joinButton.TooltipText = game != null && !compatible ? "This game runs another version of OpenCiv3." : "";
		watchButton.TooltipText = game != null && !game.game.spectatorsAllowed ? "The host doesn't let anyone watch this game." : "";
		bool needsPassword = compatible && game.game.hasPassword;
		if (passwordRow.Visible != needsPassword) {
			passwordRow.Visible = needsPassword;
			passwordEdit.Text = "";
		}
	}

	private void JoinSelected(bool watch) {
		if (OnRecentTab) {
			int[] selected = recentList.GetSelectedItems();
			if (selected.Length > 0) {
				JoinRecent(selected[0], watch);
			}
			return;
		}
		PublicGame game = SelectedGame();
		if (game == null || !PublicGames.IsCompatible(game)) {
			return;
		}
		if (game.game.hasPassword && passwordEdit.Text == "") {
			status.Text = "Type the game's password first: the host gives it out.";
			passwordEdit.GrabFocus();
			return;
		}
		string password = game.game.hasPassword ? passwordEdit.Text : null;
		RecentOnlineGames.Remember(new(game.game.name, game.code, relayUrl));
		JoinRequested?.Invoke(game.code, relayUrl, game.game.name, watch, password);
		Close();
	}

	// A recent game asks for its password itself, if it has one.
	private void JoinRecent(int index, bool watch = false) {
		if (index < 0 || index >= recent.Count) {
			return;
		}
		RecentOnlineGames.RecentGame game = recent[index];
		RecentOnlineGames.Remember(game);
		JoinRequested?.Invoke(game.code, game.relayUrl, game.name, watch, null);
		Close();
	}

	public override void _ExitTree() {
		fetching?.Cancel();
	}
}

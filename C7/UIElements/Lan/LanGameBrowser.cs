using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using C7Engine.Network;
using Godot;
using Serilog;

// The Join LAN Game screen's list of games, laid out like Steam's server
// browser in OpenCiv3's parchment colors (owner's request, 2026-10-28): tabs
// for where the games were found, a table sorted by clicking its headers, a
// filter row, joining by address, and Refresh, Watch and Join at the bottom.
// Double-clicking a game joins it. The lobby does the joining.
public partial class LanGameBrowser : VBoxContainer {
	private readonly ILogger log = LogManager.ForContext<LanGameBrowser>();

	// Asked to join a host: where, whether to watch, and the token to have
	// our seats back with when rejoining our last game.
	public event Action<LanAddressEndpoint, bool, string> JoinRequested;

	// Colors from the parchment menus: ink, the panels' brown border, the
	// panels and headers, and the dark brown of a chosen field.
	private static readonly Color Ink = new(0.13f, 0.09f, 0.05f);
	private static readonly Color Border = new(0.47f, 0.39f, 0.27f);
	private static readonly Color PanelColor = new(0.98f, 0.96f, 0.89f);
	private static readonly Color HeaderColor = new(0.84f, 0.77f, 0.60f);
	private static readonly Color HeaderHover = new(0.90f, 0.84f, 0.69f);
	private static readonly Color Chosen = new(0.33f, 0.25f, 0.15f);
	private static readonly Color ChosenText = new(0.99f, 0.96f, 0.87f);
	private static readonly Color Faded = new(0.50f, 0.45f, 0.38f);
	private static readonly Color Warning = new(0.62f, 0.12f, 0.06f);
	private static readonly Color Star = new(0.72f, 0.50f, 0.05f);
	private static readonly Color LockInk = new(0.30f, 0.22f, 0.12f);

	private static readonly (LanGameColumn column, string title, int width, HorizontalAlignment align)[] Columns = [
		(LanGameColumn.Favorite, "☆", 28, HorizontalAlignment.Center),
		(LanGameColumn.Password, "", 28, HorizontalAlignment.Center),
		(LanGameColumn.Game, "Games", 146, HorizontalAlignment.Left),
		(LanGameColumn.Players, "Players", 64, HorizontalAlignment.Center),
		(LanGameColumn.GameType, "Game Type", 124, HorizontalAlignment.Left),
		(LanGameColumn.Map, "Map", 84, HorizontalAlignment.Center),
		(LanGameColumn.Turn, "Turn", 60, HorizontalAlignment.Center),
		(LanGameColumn.Latency, "Latency", 66, HorizontalAlignment.Center),
		(LanGameColumn.Address, "Address", 194, HorizontalAlignment.Left),
		(LanGameColumn.Version, "Version", 62, HorizontalAlignment.Center),
	];

	private static readonly (LanGameTab tab, string title)[] Tabs = [
		(LanGameTab.All, "All Games"),
		(LanGameTab.Lan, "LAN"),
		(LanGameTab.Tailscale, "Tailscale"),
		(LanGameTab.Favorites, "Favorites"),
		(LanGameTab.History, "History"),
	];

	private HBoxContainer topRow;
	private TabBar tabBar;
	private Tree table;
	private Label emptyLabel;
	private Control tablePanel;
	private ScrollContainer helpView;
	private LineEdit filterEdit;
	private CheckBox hideFull, hideStarted, hidePassword, hideOtherVersions;
	private HBoxContainer bottomRow;
	private Control statusSlot;
	private Button helpButton, refreshButton, watchButton, joinButton, rejoinButton;

	public LineEdit AddressEdit { get; private set; }

	// The hosts that answered the last search, the names of the starred
	// and recent hosts as they resolved, and the rows shown, in order.
	private List<FoundHost> found = [];
	private Dictionary<string, List<string>> resolved = new();
	private List<string> favorites = [];
	private List<string> recent = [];
	private List<LanGameRow> shown = [];
	private bool searchedOnce;

	private LanGameColumn sortColumn = LanGameColumn.Game;
	private bool sortAscending = true;

	// The search under way, which Stop ends early.
	private CancellationTokenSource searching;

	private Texture2D lockIcon;

	public override void _Ready() {
		SizeFlagsVertical = SizeFlags.ExpandFill;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddThemeConstantOverride("separation", 6);
		Theme = MakeTheme();
		lockIcon = MakeLockIcon();

		topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 12);
		tabBar = new TabBar { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkEnd };
		foreach ((LanGameTab _, string title) in Tabs) {
			tabBar.AddTab(title);
		}
		tabBar.TabChanged += _ => ShowRows();
		topRow.AddChild(tabBar);
		AddChild(topRow);

		// The table, with a note over it while it's empty, or the tips for
		// finding a host in its place.
		tablePanel = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 200) };
		AddChild(tablePanel);
		table = new Tree {
			Columns = Columns.Length,
			ColumnTitlesVisible = true,
			HideRoot = true,
			SelectMode = Tree.SelectModeEnum.Row,
			HideFolding = true,
			ScrollHorizontalEnabled = false,
		};
		table.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		for (int i = 0; i < Columns.Length; ++i) {
			table.SetColumnTitle(i, Columns[i].title);
			table.SetColumnTitleAlignment(i, Columns[i].align);
			table.SetColumnCustomMinimumWidth(i, Columns[i].width);
			table.SetColumnExpand(i, Columns[i].column == LanGameColumn.Game);
			table.SetColumnClipContent(i, true);
		}
		table.ColumnTitleClicked += (column, _) => SortBy(Columns[(int)column].column);
		table.ItemSelected += ShowSelected;
		table.NothingSelected += () => {
			table.DeselectAll();
			ShowSelected();
		};
		table.ItemActivated += () => JoinSelected(false);
		table.ItemMouseSelected += (position, button) => {
			if (button == (long)MouseButton.Left && table.GetColumnAtPosition(position) == 0) {
				ToggleFavorite(SelectedRow());
			}
		};
		tablePanel.AddChild(table);

		emptyLabel = new Label {
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		emptyLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		emptyLabel.OffsetTop = 40;
		emptyLabel.OffsetLeft = 40;
		emptyLabel.OffsetRight = -40;
		emptyLabel.AddThemeFontSizeOverride("font_size", 16);
		emptyLabel.AddThemeColorOverride("font_color", Faded);
		tablePanel.AddChild(emptyLabel);

		helpView = MakeHelp();
		helpView.Visible = false;
		tablePanel.AddChild(helpView);

		// Below the table, as Steam has its tag filter: what to hide.
		HBoxContainer filterRow = new();
		filterRow.AddThemeConstantOverride("separation", 10);
		filterRow.AddChild(MakeLabel("Filter:"));
		filterEdit = new LineEdit {
			PlaceholderText = "game, host or address",
			CustomMinimumSize = new Vector2(190, 0),
			ClearButtonEnabled = true,
		};
		filterEdit.TextChanged += _ => ShowRows();
		filterRow.AddChild(filterEdit);
		hideFull = MakeFilter(filterRow, "Hide full");
		hideStarted = MakeFilter(filterRow, "Hide started");
		hidePassword = MakeFilter(filterRow, "Hide locked");
		hidePassword.TooltipText = "Hide the games that need a password";
		hideOtherVersions = MakeFilter(filterRow, "Hide other versions");
		AddChild(filterRow);

		// Joining a host the search doesn't find, as on another network.
		HBoxContainer addressRow = new();
		addressRow.AddThemeConstantOverride("separation", 10);
		addressRow.AddChild(MakeLabel("Join by address:"));
		AddressEdit = new LineEdit {
			PlaceholderText = "192.168.1.20 or mybox:47778",
			CustomMinimumSize = new Vector2(260, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		AddressEdit.TextSubmitted += _ => JoinAddress(false);
		addressRow.AddChild(AddressEdit);
		addressRow.AddChild(MakeButton("Join", () => JoinAddress(false)));
		addressRow.AddChild(MakeButton("Watch", () => JoinAddress(true)));
		Button star = MakeButton("Add to Favorites", AddAddressToFavorites);
		star.TooltipText = "Keep this address under Favorites, where it's looked for each time";
		addressRow.AddChild(star);
		AddChild(addressRow);

		// Steam's bottom row: the status, then refreshing and connecting.
		bottomRow = new HBoxContainer();
		bottomRow.AddThemeConstantOverride("separation", 10);
		statusSlot = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		bottomRow.AddChild(statusSlot);
		helpButton = MakeButton("Game Not Listed?", ToggleHelp);
		bottomRow.AddChild(helpButton);
		refreshButton = MakeButton("Refresh", () => {
			if (searching != null) {
				StopRefresh();
			} else {
				Refresh();
			}
		});
		refreshButton.CustomMinimumSize = new Vector2(130, 32);
		bottomRow.AddChild(refreshButton);
		rejoinButton = MakeButton("Rejoin", RejoinSelected);
		rejoinButton.TooltipText = "Take your seats back in your last game";
		bottomRow.AddChild(rejoinButton);
		watchButton = MakeButton("Watch", () => JoinSelected(true));
		bottomRow.AddChild(watchButton);
		joinButton = MakeButton("Join", () => JoinSelected(false));
		bottomRow.AddChild(joinButton);
		AddChild(bottomRow);

		favorites = FavoriteLanHosts.Load();
		recent = LoadRecent();
		ShowRows();
		Refresh();
	}

	// ---- What the lobby puts in ----

	// Beside the tabs, such as the name to play under.
	public void AddToTopRow(Control control) => topRow.AddChild(control);

	// A row above the bottom one, such as joining online with a code.
	public void AddRow(Control row) {
		AddChild(row);
		MoveChild(row, bottomRow.GetIndex());
	}

	// A button in the bottom row, after the tips.
	public void AddBottomButton(Control button) {
		bottomRow.AddChild(button);
		bottomRow.MoveChild(button, helpButton.GetIndex() + 1);
	}

	// Where the lobby's status line goes while the list is shown.
	public Control StatusSlot => statusSlot;

	// ---- Searching ----

	// Looks for games again: by broadcast on the local networks, and by
	// asking the tailnet's machines and the starred and recent hosts
	// directly. The list stays as it was until the answers are in.
	public async void Refresh() {
		if (searching != null) {
			return;
		}
		CancellationTokenSource stop = new();
		searching = stop;
		favorites = FavoriteLanHosts.Load();
		recent = LoadRecent();
		ShowSearching();
		List<string> known = favorites.Concat(recent).Select(e => LanGameList.ParseEntry(e).host).ToList();
		Task<Dictionary<string, List<string>>> resolving = LanGameList.ResolveAsync(favorites.Concat(recent), TimeSpan.FromSeconds(2));
		List<FoundHost> hosts;
		try {
			hosts = await LanDiscovery.FindHosts(TimeSpan.FromSeconds(1.5), known, stop.Token);
		} catch (Exception e) when (e is System.Net.Sockets.SocketException or ObjectDisposedException) {
			log.Warning("Couldn't look for LAN games: {Error}", e.Message);
			hosts = [];
		}
		// Stopped, the names not yet looked up can wait for the next search.
		Dictionary<string, List<string>> names = stop.IsCancellationRequested && !resolving.IsCompleted ? resolved : await resolving;
		if (!IsInstanceValid(this) || searching != stop) {
			return;
		}
		searching = null;
		Show(hosts, names);
	}

	// Ends the search early; what it found so far is listed, as Steam's
	// Stop Refresh keeps the servers found.
	public void StopRefresh() => searching?.Cancel();

	// Lists these hosts in place of the search's, as if found, with the
	// names of the starred and recent hosts as resolved to addresses.
	public void ShowHosts(List<FoundHost> hosts, Dictionary<string, List<string>> names) {
		searching?.Cancel();
		searching = null;
		Show(hosts, names);
	}

	private void Show(List<FoundHost> hosts, Dictionary<string, List<string>> names) {
		found = hosts;
		resolved = names;
		searchedOnce = true;
		ShowSearching();
		ShowRows();
	}

	private void ShowSearching() {
		refreshButton.Text = searching != null ? "Stop Refresh" : "Refresh";
		refreshButton.TooltipText = searching != null ? "Stop looking, and list what was found so far" : "Look for games again";
	}

	private List<string> LoadRecent() {
		List<string> hosts = RecentLanHosts.Load();
		if (LanSession.LastJoinedGame?.address is string last && !hosts.Contains(last, StringComparer.OrdinalIgnoreCase)) {
			hosts.Add(last);
		}
		return hosts;
	}

	// ---- The table ----

	private LanGameTab CurrentTab => Tabs[Math.Max(0, tabBar.CurrentTab)].tab;

	public void SelectTab(int index) => tabBar.CurrentTab = index;

	public void SelectRow(int index) {
		TreeItem item = table.GetRoot()?.GetChild(index);
		item?.Select(0);
		ShowSelected();
	}

	private void SortBy(LanGameColumn column) {
		sortAscending = column != sortColumn || !sortAscending;
		sortColumn = column;
		ShowRows();
	}

	private LanGameFilter Filter() {
		return new LanGameFilter(filterEdit.Text, hideFull.ButtonPressed, hideStarted.ButtonPressed, hidePassword.ButtonPressed,
			hideOtherVersions.ButtonPressed);
	}

	// Fills the table with the current tab's rows, filtered and sorted,
	// keeping the selected one.
	private void ShowRows() {
		LanGameRow selected = SelectedRow();
		for (int i = 0; i < Tabs.Length; ++i) {
			int count = LanGameList.Rows(Tabs[i].tab, found, favorites, recent, resolved).Count;
			tabBar.SetTabTitle(i, count > 0 ? $"{Tabs[i].title} ({count})" : Tabs[i].title);
		}
		List<LanGameRow> rows = LanGameList.Rows(CurrentTab, found, favorites, recent, resolved);
		shown = LanGameList.Sort(LanGameList.Filter(rows, Filter()), sortColumn, sortAscending);

		for (int i = 0; i < Columns.Length; ++i) {
			string title = Columns[i].column == LanGameColumn.Game ? $"Games ({shown.Count})" : Columns[i].title;
			string arrow = Columns[i].column != sortColumn ? "" : sortAscending ? " ▲" : " ▼";
			table.SetColumnTitle(i, title + arrow);
		}

		table.Clear();
		TreeItem root = table.CreateItem();
		foreach (LanGameRow row in shown) {
			TreeItem item = table.CreateItem(root);
			FillRow(item, row);
			if (selected != null && row.entry == selected.entry && row.reply?.hostName == selected.reply?.hostName) {
				item.Select(0);
			}
		}

		int hidden = rows.Count - shown.Count;
		emptyLabel.Visible = shown.Count == 0 && !helpView.Visible;
		emptyLabel.Text = EmptyText(hidden);
		ShowSelected();
	}

	private void FillRow(TreeItem item, LanGameRow row) {
		DiscoveryReply reply = row.reply;
		bool compatible = reply == null || LanGameList.IsCompatible(reply);
		string[] cells = [
			row.favorite ? "★" : "☆",
			"",
			LanGameList.GameText(row),
			LanGameList.PlayersText(reply),
			reply?.gameType ?? "",
			reply?.mapSize ?? "",
			LanGameList.TurnText(reply),
			LanGameList.LatencyText(row.found),
			LanGameList.AddressText(row),
			LanGameList.VersionText(reply),
		];
		for (int i = 0; i < cells.Length; ++i) {
			item.SetText(i, cells[i]);
			item.SetTextAlignment(i, Columns[i].align);
			if (!compatible || !row.answered) {
				item.SetCustomColor(i, Faded);
			}
		}
		item.SetCustomColor(0, row.favorite ? Star : Faded);
		item.SetTooltipText(0, row.favorite ? "In your favorites. Click to take it out." : "Click to add to your favorites.");
		if (reply?.hasPassword == true) {
			item.SetIcon(1, lockIcon);
			item.SetIconModulate(1, compatible ? LockInk : Faded);
			item.SetTooltipText(1, "Joining takes the game's password, which the host gives out.");
		}
		if (reply != null && !compatible) {
			item.SetCustomColor(9, Warning);
			item.SetTooltipText(9, $"This host runs another version of OpenCiv3 (yours is {LanProtocol.Version}), so you can't join it.");
		}
		string tip = row.answered
			? $"Hosted by {reply.hostName}" + (reply.started ? "" : ", waiting in its lobby")
				+ (reply.seatsTotal > 0 ? $", {reply.openSeats} open {(reply.openSeats == 1 ? "seat" : "seats")}" : "")
			: "This host didn't answer the search; it may not be hosting now. Join tries it anyway.";
		item.SetTooltipText(2, tip);
		if (!row.answered) {
			item.SetText(6, "");
			item.SetText(7, "—");
		}
	}

	private string EmptyText(int hidden) {
		if (hidden > 0) {
			return $"The filters hide {(hidden == 1 ? "the one game" : $"all {hidden} games")} here.";
		}
		if (!searchedOnce) {
			return "Looking for games...";
		}
		return CurrentTab switch {
			LanGameTab.Tailscale when !LanDiscovery.LocalAddressesByNetwork().Any(a => a.network == "Tailscale") =>
				"This computer isn't on a Tailscale network. With Tailscale running, games on your tailnet's machines are listed here.",
			LanGameTab.Tailscale => "No games on your Tailscale network's machines right now.",
			LanGameTab.Favorites => "Click the ☆ beside a game, or add an address below, to keep a host here.",
			LanGameTab.History => "The hosts you join are listed here.",
			_ => "No games found. Check the host is in its lobby, or join by address below. "
				+ "\"Game Not Listed?\" has tips for finding a host.",
		};
	}

	private LanGameRow SelectedRow() {
		TreeItem item = table?.GetSelected();
		int index = item == null ? -1 : item.GetIndex();
		return index >= 0 && index < shown.Count ? shown[index] : null;
	}

	// Steam's Connect is greyed until a server is chosen; so are these.
	private void ShowSelected() {
		LanGameRow row = SelectedRow();
		for (TreeItem item = table.GetRoot()?.GetFirstChild(); item != null; item = item.GetNext()) {
			if (item.GetIcon(1) != null) {
				int index = item.GetIndex();
				bool joinable = shown[index].reply == null || LanGameList.IsCompatible(shown[index].reply);
				item.SetIconModulate(1, item.IsSelected(0) ? ChosenText : joinable ? LockInk : Faded);
			}
		}
		DiscoveryReply reply = row?.reply;
		bool compatible = reply == null || LanGameList.IsCompatible(reply);
		joinButton.Disabled = row == null || !compatible || reply?.openSeats == 0;
		watchButton.Disabled = row == null || !compatible || reply?.spectatorsAllowed == false;
		joinButton.TooltipText = row == null ? "Choose a game first, or double-click one"
			: !compatible ? "This host runs another version of OpenCiv3."
			: reply?.openSeats == 0 ? "Every seat is taken. You may still watch, if the host allows it." : "";
		watchButton.TooltipText = reply?.spectatorsAllowed == false ? "The host doesn't let anyone watch this game." : "Watch without taking a seat";
		rejoinButton.Visible = LastGameToken(row) != null;
	}

	// The token to rejoin our last game with, if the row is that game; the
	// host may have moved to another address since.
	private static string LastGameToken(LanGameRow row) {
		LanSession.LastGame last = LanSession.LastJoinedGame;
		if (row?.reply == null || last == null || last.endpoint is not LanAddressEndpoint) {
			return null;
		}
		return last.hostName == row.reply.hostName && last.port == row.reply.port ? last.token : null;
	}

	private void ToggleFavorite(LanGameRow row) {
		if (row == null) {
			return;
		}
		FavoriteLanHosts.Toggle(row.entry);
		favorites = FavoriteLanHosts.Load();
		ShowRows();
	}

	// ---- Joining ----

	private void JoinSelected(bool watch) {
		LanGameRow row = SelectedRow();
		if (row == null || (watch ? watchButton.Disabled : joinButton.Disabled)) {
			return;
		}
		JoinRequested?.Invoke(new LanAddressEndpoint(row.address, row.port), watch, null);
	}

	private void RejoinSelected() {
		LanGameRow row = SelectedRow();
		if (LastGameToken(row) is string token) {
			JoinRequested?.Invoke(new LanAddressEndpoint(row.address, row.port), false, token);
		}
	}

	public void JoinAddress(bool watch) {
		(string host, int port) = LanGameList.ParseEntry(AddressEdit.Text);
		if (host == "") {
			AddressEdit.GrabFocus();
			return;
		}
		JoinRequested?.Invoke(new LanAddressEndpoint(host, port), watch, null);
	}

	private void AddAddressToFavorites() {
		if (FavoriteLanHosts.Tidy(AddressEdit.Text) is not string entry) {
			AddressEdit.GrabFocus();
			return;
		}
		FavoriteLanHosts.Add(entry);
		favorites = FavoriteLanHosts.Load();
		tabBar.CurrentTab = Array.FindIndex(Tabs, t => t.tab == LanGameTab.Favorites);
		ShowRows();
		Refresh();
	}

	// ---- Tips ----

	public void ToggleHelp() {
		helpView.Visible = !helpView.Visible;
		table.Visible = !helpView.Visible;
		helpButton.Text = helpView.Visible ? "Back to the List" : "Game Not Listed?";
		ShowRows();
	}

	// Tips for finding the host when it isn't listed.
	private ScrollContainer MakeHelp() {
		ScrollContainer scroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		PanelContainer panel = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
		panel.AddThemeStyleboxOverride("panel", Box(PanelColor, Border, 1, 14));
		scroll.AddChild(panel);
		VBoxContainer tips = new();
		tips.AddThemeConstantOverride("separation", 8);
		panel.AddChild(tips);

		Label heading = MakeLabel("GAME NOT LISTED? FINDING THE HOST'S ADDRESS", 17);
		heading.HorizontalAlignment = HorizontalAlignment.Center;
		tips.AddChild(heading);
		string[] lines = [
			"The host's screen shows its addresses under \"Hosting a LAN Game\". Ask the host to read one out and type it under \"Join by address\".",
			"Or look it up on the host's computer. Windows: open Command Prompt, run ipconfig and use the \"IPv4 Address\" line. "
				+ "macOS: System Settings > Network, choose the connection, then Details. Linux: run hostname -I in a terminal.",
			"Addresses on a home network usually start with 192.168. or 10. One starting with 127. or 169.254. won't work from another computer.",
			"Both computers must be on the same network, such as the same router or Wi-Fi. Guest Wi-Fi often keeps devices from seeing each other.",
			$"If the address is right but you still can't connect, the host's firewall may be blocking the game. Allow OpenCiv3 through it "
				+ $"(TCP port {LanProtocol.DefaultPort}, and UDP port {LanProtocol.DiscoveryPort} for the list of games). "
				+ "Windows counts Tailscale and other virtual networks as Public networks, so allow it on Public networks too.",
			$"If the host uses a different port, add it after the address, like 192.168.1.20:{LanProtocol.DefaultPort + 1}.",
			"Playing over the internet? Ask the host to choose \"Host Online\", then type the join code it shows under \"Online game code\".",
			"Or both players can join the same virtual network (such as Tailscale or ZeroTier) and use the host's address on it. "
				+ "Games on the machines of your Tailscale network are listed under Tailscale, and the hosts you've joined under History.",
			"A host you add to Favorites is asked directly each time the list is refreshed, even on a network that doesn't carry the search.",
		];
		foreach (string line in lines) {
			Label label = MakeLabel("•  " + line, 15);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			tips.AddChild(label);
		}
		return scroll;
	}

	// ---- Looks ----

	private static Label MakeLabel(string text, int fontSize = 16) {
		Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", Ink);
		return label;
	}

	// The boxed buttons of the parchment setup screens.
	public static Button MakeButton(string text, Action onPressed) {
		Button button = new() { Text = text, CustomMinimumSize = new Vector2(96, 32), FocusMode = FocusModeEnum.None };
		button.AddThemeFontSizeOverride("font_size", 16);
		button.AddThemeStyleboxOverride("normal", Box(new Color(0.93f, 0.88f, 0.76f), Border, 1, 10));
		button.AddThemeStyleboxOverride("hover", Box(new Color(0.96f, 0.92f, 0.82f), Border, 1, 10));
		button.AddThemeStyleboxOverride("pressed", Box(new Color(0.84f, 0.77f, 0.60f), Border, 1, 10));
		button.AddThemeStyleboxOverride("disabled", Box(new Color(0.90f, 0.86f, 0.77f), new Color(0.66f, 0.60f, 0.50f), 1, 10));
		button.AddThemeColorOverride("font_disabled_color", new Color(0.58f, 0.53f, 0.46f));
		button.Pressed += onPressed;
		return button;
	}

	private CheckBox MakeFilter(HBoxContainer row, string text) {
		// Civ3's checkbox art on a plain CheckBox, which sizes itself to its
		// text in a row (Civ3Checkbox doesn't).
		CheckBox box = new() { Text = text, FocusMode = FocusModeEnum.None };
		box.AddThemeIconOverride("unchecked", TextureLoader.Load("ui.checkbox.inactive"));
		box.AddThemeIconOverride("checked", TextureLoader.Load("ui.checkbox.pressed"));
		box.AddThemeFontSizeOverride("font_size", 15);
		box.AddThemeColorOverride("font_color", Ink);
		box.AddThemeColorOverride("font_pressed_color", Ink);
		box.AddThemeColorOverride("font_hover_pressed_color", new Color(0.71f, 0.35f, 0f));
		box.AddThemeConstantOverride("h_separation", 5);
		box.Toggled += _ => ShowRows();
		row.AddChild(box);
		return box;
	}

	private static StyleBoxFlat Box(Color fill, Color border, int borderWidth, int margin, bool bottom = true) {
		return new StyleBoxFlat {
			BgColor = fill,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthBottom = bottom ? borderWidth : 0,
			ContentMarginLeft = margin,
			ContentMarginRight = margin,
			ContentMarginTop = 3,
			ContentMarginBottom = 3,
		};
	}

	// The table, its tabs and its scroll bar in the parchment colors.
	internal static Theme MakeTheme() {
		Theme theme = new();
		theme.SetStylebox("panel", "Tree", Box(PanelColor, Border, 2, 0));
		theme.SetStylebox("focus", "Tree", new StyleBoxEmpty());
		theme.SetStylebox("selected", "Tree", Box(Chosen, Chosen, 0, 0));
		theme.SetStylebox("selected_focus", "Tree", Box(Chosen, Chosen, 0, 0));
		theme.SetStylebox("hovered", "Tree", Box(new Color(0.91f, 0.86f, 0.73f), Border, 0, 0));
		theme.SetStylebox("hovered_dimmed", "Tree", Box(new Color(0.94f, 0.90f, 0.80f), Border, 0, 0));
		theme.SetStylebox("cursor", "Tree", new StyleBoxEmpty());
		theme.SetStylebox("cursor_unfocused", "Tree", new StyleBoxEmpty());
		StyleBoxFlat header = Box(HeaderColor, Border, 0, 6);
		header.BorderWidthBottom = 1;
		header.BorderWidthRight = 1;
		StyleBoxFlat headerHover = (StyleBoxFlat)header.Duplicate();
		headerHover.BgColor = HeaderHover;
		theme.SetStylebox("title_button_normal", "Tree", header);
		theme.SetStylebox("title_button_hover", "Tree", headerHover);
		theme.SetStylebox("title_button_pressed", "Tree", headerHover);
		theme.SetColor("title_button_color", "Tree", Ink);
		theme.SetColor("font_color", "Tree", Ink);
		theme.SetColor("font_hovered_color", "Tree", Ink);
		theme.SetColor("font_selected_color", "Tree", ChosenText);
		theme.SetColor("guide_color", "Tree", new Color(0, 0, 0, 0));
		theme.SetFontSize("font_size", "Tree", 14);
		theme.SetFontSize("title_button_font_size", "Tree", 14);
		theme.SetConstant("v_separation", "Tree", 6);
		theme.SetConstant("h_separation", "Tree", 4);
		theme.SetConstant("item_margin", "Tree", 0);
		theme.SetConstant("inner_item_margin_left", "Tree", 6);
		theme.SetConstant("inner_item_margin_right", "Tree", 4);
		theme.SetConstant("draw_guides", "Tree", 0);
		theme.SetConstant("draw_relationship_lines", "Tree", 0);
		theme.SetConstant("scroll_border", "Tree", 0);

		// Folder tabs that run into the table, as Steam's do.
		StyleBoxFlat tabOn = Box(PanelColor, Border, 2, 14, bottom: false);
		tabOn.ContentMarginTop = 5;
		tabOn.ContentMarginBottom = 4;
		tabOn.CornerRadiusTopLeft = 4;
		tabOn.CornerRadiusTopRight = 4;
		StyleBoxFlat tabOff = Box(HeaderColor, Border, 1, 14);
		tabOff.ContentMarginTop = 4;
		tabOff.ContentMarginBottom = 3;
		tabOff.CornerRadiusTopLeft = 4;
		tabOff.CornerRadiusTopRight = 4;
		StyleBoxFlat tabHover = (StyleBoxFlat)tabOff.Duplicate();
		tabHover.BgColor = HeaderHover;
		theme.SetStylebox("tab_selected", "TabBar", tabOn);
		theme.SetStylebox("tab_unselected", "TabBar", tabOff);
		theme.SetStylebox("tab_hovered", "TabBar", tabHover);
		theme.SetStylebox("tab_focus", "TabBar", new StyleBoxEmpty());
		theme.SetColor("font_selected_color", "TabBar", Ink);
		theme.SetColor("font_unselected_color", "TabBar", new Color(0.30f, 0.24f, 0.16f));
		theme.SetColor("font_hovered_color", "TabBar", new Color(0.71f, 0.35f, 0f));
		theme.SetFontSize("font_size", "TabBar", 16);
		theme.SetConstant("h_separation", "TabBar", 2);

		StyleBoxFlat track = Box(new Color(0.90f, 0.85f, 0.72f), Border, 0, 0);
		track.ContentMarginLeft = 5;
		track.ContentMarginRight = 5;
		StyleBoxFlat grabber = Box(new Color(0.62f, 0.52f, 0.36f), Border, 1, 0);
		StyleBoxFlat grabberHot = Box(new Color(0.50f, 0.40f, 0.26f), Border, 1, 0);
		theme.SetStylebox("scroll", "VScrollBar", track);
		theme.SetStylebox("scroll_focus", "VScrollBar", track);
		theme.SetStylebox("grabber", "VScrollBar", grabber);
		theme.SetStylebox("grabber_highlight", "VScrollBar", grabberHot);
		theme.SetStylebox("grabber_pressed", "VScrollBar", grabberHot);
		return theme;
	}

	// Steam's padlock, drawn white to be tinted: ink, or light on the
	// selected row's dark brown.
	private static Texture2D MakeLockIcon() {
		const int w = 12, h = 15;
		Image image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		Color ink = Colors.White;
		for (int y = 6; y < h; ++y) {
			for (int x = 0; x < w; ++x) {
				image.SetPixel(x, y, ink);
			}
		}
		// The shackle: an arch two pixels thick.
		for (int y = 0; y < 6; ++y) {
			for (int x = 0; x < w; ++x) {
				float dx = x + 0.5f - w / 2f, dy = Math.Max(0, 4.5f - y);
				float r = MathF.Sqrt(dx * dx + dy * dy);
				if (r >= 2.6f && r <= 4.6f) {
					image.SetPixel(x, y, ink);
				}
			}
		}
		// The keyhole.
		image.SetPixel(5, 9, Colors.Transparent);
		image.SetPixel(6, 9, Colors.Transparent);
		image.SetPixel(5, 10, Colors.Transparent);
		image.SetPixel(6, 10, Colors.Transparent);
		image.SetPixel(5, 11, Colors.Transparent);
		image.SetPixel(6, 11, Colors.Transparent);
		return ImageTexture.CreateFromImage(image);
	}

	public override void _ExitTree() {
		searching?.Cancel();
	}
}

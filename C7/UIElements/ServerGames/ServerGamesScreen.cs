using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using C7Engine.Network;
using C7GameServer;
using Godot;
using Serilog;

// The Server Games screen, over the main menu (see doc/server-games.md):
// logging in to the game server chosen in the settings, or signing up; then
// My Games, the player's games in two lists, Your Turn and Another Player's
// Turn, to Play or View; and Open Games, the games with seats free, to claim
// one. New Server Game sets one up on the Multiplayer Setup screen. In the
// parchment colors of the server browser.
public partial class ServerGamesScreen : Control {
	private static readonly ILogger log = Log.ForContext<ServerGamesScreen>();

	public event Action Closed;

	private const string SetupScene = "res://UIElements/NewGame/multiplayer_setup.tscn";

	private static readonly Color Ink = new(0.13f, 0.09f, 0.05f);
	private static readonly Color Faded = new(0.42f, 0.37f, 0.30f);
	private static readonly Color Warning = new(0.62f, 0.12f, 0.06f);

	// The client for the server chosen, and its session once logged in.
	private GameServerClient client;

	// Called off on closing, so that no answer arrives to a screen gone.
	private readonly CancellationTokenSource closing = new();
	// A call is under way that mustn't be made twice at once, like logging
	// in or claiming a seat.
	private bool busy;

	private Label title;
	private Label account;
	private Button logOutButton;
	private Label status;

	// Logging in.
	private VBoxContainer loginPage;
	private Label loginHeading;
	private LineEdit serverEdit;
	private LineEdit usernameEdit;
	private LineEdit passwordEdit;
	private HBoxContainer confirmRow;
	private LineEdit confirmEdit;
	private Button loginButton;
	private Label switchNote;
	private Button switchButton;
	private bool signingUp;

	// The player's games and the open ones.
	private VBoxContainer gamesPage;
	private TabContainer tabs;
	private Tree yourTurnTable;
	private Tree othersTurnTable;
	private Button playButton;
	private Button viewButton;
	// Plays or views the game chosen in either list, as double-clicking it
	// does; dim until one is chosen.
	private Button joinButton;
	private readonly Dictionary<TreeItem, GameSummary> gameRows = new();

	private Tree openTable;
	private ItemList seatList;
	private HBoxContainer openPasswordRow;
	private LineEdit openPasswordEdit;
	private Button claimButton;
	private List<OpenGame> openGames = [];
	private readonly Dictionary<TreeItem, OpenGame> openRows = new();
	// The seats listed, and the game they're of: the list is made again only
	// for another game, or the same game as the server last told it.
	private List<SeatSummary> seatsShown = [];
	private OpenGame seatsGame;
	private bool seatsListed;

	private static readonly (string title, int width)[] GameColumns = [
		("Era", 60),
		("Game", 220),
		("You Play", 120),
		("Turn", 56),
		("Whose Turn", 250),
	];

	private static readonly (string title, int width)[] OpenColumns = [
		("Lock", 48),
		("Game", 200),
		("Host", 120),
		("Turn", 56),
		("Free Seats", 90),
	];

	public override void _Ready() {
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
			BgColor = new Color(0.968627f, 0.937255f, 0.85098f),
			BorderColor = new Color(0.55f, 0.48f, 0.30f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 14,
			ContentMarginRight = 14,
			ContentMarginTop = 10,
			ContentMarginBottom = 10,
		});
		panel.Theme = MakeTheme();
		center.AddChild(panel);

		VBoxContainer layout = new();
		layout.AddThemeConstantOverride("separation", 8);
		panel.AddChild(layout);

		HBoxContainer header = new();
		header.AddThemeConstantOverride("separation", 12);
		title = new Label { Text = "Server Games", SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
		title.AddThemeFontSizeOverride("font_size", 22);
		header.AddChild(title);
		account = new Label { VerticalAlignment = VerticalAlignment.Center };
		account.AddThemeFontSizeOverride("font_size", 15);
		header.AddChild(account);
		logOutButton = LanGameBrowser.MakeButton("Log Out", LogOut);
		header.AddChild(logOutButton);
		header.AddChild(LanGameBrowser.MakeButton("Close", Close));
		layout.AddChild(header);

		loginPage = MakeLoginPage();
		layout.AddChild(loginPage);
		gamesPage = MakeGamesPage();
		layout.AddChild(gamesPage);

		status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 22) };
		status.AddThemeFontSizeOverride("font_size", 15);
		layout.AddChild(status);

		Start();
	}

	// To My Games with the session last logged in with, or else to log in.
	private void Start() {
		try {
			client = ServerGames.Client();
		} catch (GameServerException e) {
			client = null;
			ShowLogin(e.Message);
			return;
		}
		if (client.Session == null) {
			ShowLogin();
			return;
		}
		ShowGames();
		RefreshAll();
	}

	// ---- Logging in ----

	private VBoxContainer MakeLoginPage() {
		VBoxContainer page = new() { SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
		page.AddThemeConstantOverride("separation", 10);

		loginHeading = MakeLabel("", 20);
		loginHeading.HorizontalAlignment = HorizontalAlignment.Center;
		page.AddChild(loginHeading);

		GridContainer grid = new() { Columns = 2, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 8);
		page.AddChild(grid);

		serverEdit = AddField(grid, "Game server:", secret: false);
		serverEdit.PlaceholderText = "games.example.org";
		serverEdit.TooltipText = "The address of the game server, which whoever runs it gives out. Settings has it too.";
		usernameEdit = AddField(grid, "Username:", secret: false);
		usernameEdit.MaxLength = GameServerProtocol.MaxUsernameLength;
		usernameEdit.TooltipText = $"{GameServerProtocol.MinUsernameLength} to {GameServerProtocol.MaxUsernameLength} letters, digits, '_', '-' or '.'.";
		passwordEdit = AddField(grid, "Password:", secret: true);
		passwordEdit.MaxLength = GameServerProtocol.MaxPasswordLength;
		usernameEdit.TextSubmitted += _ => passwordEdit.GrabFocus();
		passwordEdit.TextSubmitted += _ => {
			if (signingUp) {
				confirmEdit.GrabFocus();
			} else {
				Submit();
			}
		};

		// Signing up asks for the password twice: with no email, a password
		// mistyped can't be reset but by whoever runs the server.
		confirmRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		confirmRow.AddThemeConstantOverride("separation", 12);
		Label confirmLabel = MakeLabel("Password again:", 16);
		confirmLabel.HorizontalAlignment = HorizontalAlignment.Right;
		confirmLabel.CustomMinimumSize = new Vector2(130, 0);
		confirmRow.AddChild(confirmLabel);
		confirmEdit = new LineEdit { Secret = true, MaxLength = GameServerProtocol.MaxPasswordLength, CustomMinimumSize = new Vector2(300, 0) };
		confirmEdit.TextSubmitted += _ => Submit();
		confirmRow.AddChild(confirmEdit);
		page.AddChild(confirmRow);

		loginButton = LanGameBrowser.MakeButton("Log In", Submit);
		loginButton.CustomMinimumSize = new Vector2(140, 34);
		loginButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		page.AddChild(loginButton);

		HBoxContainer switchRow = new() { Alignment = BoxContainer.AlignmentMode.Center };
		switchRow.AddThemeConstantOverride("separation", 12);
		switchNote = MakeLabel("", 15);
		switchRow.AddChild(switchNote);
		switchButton = LanGameBrowser.MakeButton("", OpenSignUpPage);
		switchRow.AddChild(switchButton);
		page.AddChild(switchRow);

		// The password is reset on the website (accounts have an email): a click opens it.
		Label note = MakeLabel("Forgot your password? Reset it at roma-online.net/forgot-password.", 14);
		note.AddThemeColorOverride("font_color", Faded);
		note.MouseFilter = MouseFilterEnum.Stop;
		note.MouseDefaultCursorShape = CursorShape.PointingHand;
		note.GuiInput += e => {
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) {
				OS.ShellOpen("https://roma-online.net/forgot-password");
			}
		};
		note.HorizontalAlignment = HorizontalAlignment.Center;
		note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		page.AddChild(note);
		return page;
	}

	private static LineEdit AddField(GridContainer grid, string label, bool secret) {
		Label name = MakeLabel(label, 16);
		name.HorizontalAlignment = HorizontalAlignment.Right;
		name.CustomMinimumSize = new Vector2(130, 0);
		grid.AddChild(name);
		LineEdit edit = new() { Secret = secret, CustomMinimumSize = new Vector2(300, 0) };
		grid.AddChild(edit);
		return edit;
	}

	// Accounts are made on the website now (they need an email): Sign Up
	// opens its page in the browser and says so; logging in is unchanged.
	private const string SignUpUrl = "https://roma-online.net/signup";

	private void OpenSignUpPage() {
		OS.ShellOpen(SignUpUrl);
		ShowStatus("Create your account at roma-online.net, then log in here.");
	}

	private void SetSigningUp(bool on) {
		signingUp = on;
		loginHeading.Text = on ? "Sign up for an account on the game server" : "Log in to the game server";
		loginButton.Text = on ? "Sign Up" : "Log In";
		confirmRow.Visible = on;
		confirmEdit.Text = "";
		switchNote.Text = on ? "Have an account already?" : "No account yet?";
		switchButton.Text = on ? "Log In" : "Sign Up";
		status.Text = "";
	}

	private void ShowLogin(string message = null) {
		loginPage.Visible = true;
		gamesPage.Visible = false;
		account.Text = "";
		logOutButton.Visible = false;
		title.Text = "Server Games";
		serverEdit.Text = GameServerSettings.Url;
		usernameEdit.Text = GameServerSettings.LastUsername;
		passwordEdit.Text = "";
		SetSigningUp(false);
		ShowStatus(message, message != null);
		(serverEdit.Text == "" ? serverEdit : usernameEdit.Text == "" ? usernameEdit : passwordEdit).CallDeferred(Control.MethodName.GrabFocus);
	}

	// Logs in or signs up, with the server typed, which is kept as the one
	// chosen from now on.
	private async void Submit() {
		if (busy) {
			return;
		}
		string server = serverEdit.Text;
		if (GameServerSettings.Problem(server) is string problem) {
			ShowStatus(problem, true);
			serverEdit.GrabFocus();
			return;
		}
		string username = GameServerProtocol.TidyUsername(usernameEdit.Text);
		if (username == null) {
			ShowStatus($"A username has {GameServerProtocol.MinUsernameLength} to {GameServerProtocol.MaxUsernameLength} letters, digits, '_', '-' or '.'.", true);
			usernameEdit.GrabFocus();
			return;
		}
		string password = passwordEdit.Text;
		if (password == "") {
			ShowStatus("Type your password.", true);
			passwordEdit.GrabFocus();
			return;
		}
		if (signingUp) {
			if (GameServerProtocol.PasswordProblem(password) is string weak) {
				ShowStatus(weak, true);
				passwordEdit.GrabFocus();
				return;
			}
			if (confirmEdit.Text != password) {
				ShowStatus("The passwords don't match. Type the same password twice.", true);
				confirmEdit.Text = "";
				confirmEdit.GrabFocus();
				return;
			}
		}

		GameServerSettings.SetUrl(server);
		bool signUp = signingUp;
		busy = true;
		loginButton.Disabled = true;
		ShowStatus(signUp ? "Signing up..." : "Logging in...");
		try {
			client = ServerGames.Client();
			Session session = signUp
				? await client.SignUpAsync(username, password, closing.Token)
				: await client.LogInAsync(username, password, closing.Token);
			if (closing.IsCancellationRequested) {
				return;
			}
			GameServerSettings.SaveSession(GameServerSettings.Url, session);
			log.Information("{Action} to the game server as {Username}", signUp ? "Signed up" : "Logged in", session.Username);
			ShowGames();
			RefreshAll(signUp ? $"Welcome, {session.Username}. Your games will be listed here." : null);
		} catch (GameServerException e) {
			if (!closing.IsCancellationRequested) {
				ShowStatus(e.Message, true);
			}
		} catch (OperationCanceledException) {
		} finally {
			busy = false;
			if (IsInstanceValid(loginButton)) {
				loginButton.Disabled = false;
			}
		}
	}

	private async void LogOut() {
		GameServerClient ending = client;
		GameServerSettings.ForgetSession();
		ShowLogin("Logged out.");
		if (ending?.Session == null) {
			return;
		}
		// The server is told, so the session can't be used again; if it
		// can't be reached, the session is forgotten here all the same.
		try {
			await ending.LogOutAsync(closing.Token);
		} catch (Exception e) when (e is GameServerException or OperationCanceledException) {
			log.Information("Couldn't tell the game server of logging out: {Message}", e.Message);
		}
	}

	// The server says the session is over (it was logged out elsewhere, or
	// expired): back to logging in.
	private void SessionEnded() {
		GameServerSettings.ForgetSession();
		Start();
		ShowStatus("Your session on the game server has ended. Log in again.", true);
	}

	// ---- My Games and Open Games ----

	private VBoxContainer MakeGamesPage() {
		VBoxContainer page = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
		page.AddThemeConstantOverride("separation", 8);

		tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		page.AddChild(tabs);

		VBoxContainer myGames = new() { Name = "My Games" };
		myGames.AddThemeConstantOverride("separation", 6);
		tabs.AddChild(myGames);
		playButton = LanGameBrowser.MakeButton("Play", () => JoinSelected(yourTurnTable));
		playButton.TooltipText = "Connect to the game and take your turn.";
		yourTurnTable = MakeGameTable();
		myGames.AddChild(MakeListHeading("Your Turn", playButton));
		myGames.AddChild(yourTurnTable);
		viewButton = LanGameBrowser.MakeButton("View", () => JoinSelected(othersTurnTable));
		viewButton.TooltipText = "Connect to the game and look at your cities, units and advisors while another player moves.";
		othersTurnTable = MakeGameTable();
		myGames.AddChild(MakeListHeading("Another Player's Turn", viewButton));
		myGames.AddChild(othersTurnTable);
		yourTurnTable.ItemSelected += () => {
			othersTurnTable.DeselectAll();
			ShowSelectedGame();
		};
		othersTurnTable.ItemSelected += () => {
			yourTurnTable.DeselectAll();
			ShowSelectedGame();
		};

		tabs.AddChild(MakeOpenGames());
		tabs.TabChanged += _ => ShowSelectedGame();

		HBoxContainer footer = new();
		footer.AddThemeConstantOverride("separation", 12);
		Button newGame = LanGameBrowser.MakeButton("New Server Game", NewServerGame);
		newGame.CustomMinimumSize = new Vector2(160, 32);
		newGame.TooltipText = "Set up a new game for the server: a slot set to Open for each other human player, who claim them from Open Games.";
		footer.AddChild(newGame);
		footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		footer.AddChild(LanGameBrowser.MakeButton("Refresh", () => RefreshAll()));
		joinButton = LanGameBrowser.MakeButton("Join Game", JoinSelectedGame);
		joinButton.CustomMinimumSize = new Vector2(140, 32);
		joinButton.Disabled = true;
		Highlight(joinButton);
		footer.AddChild(joinButton);
		page.AddChild(footer);
		return page;
	}

	private static HBoxContainer MakeListHeading(string text, Button button) {
		HBoxContainer row = new();
		Label label = MakeLabel(text, 18);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(label);
		row.AddChild(button);
		return row;
	}

	private Tree MakeGameTable() {
		Tree table = MakeTable(GameColumns, expand: 1);
		table.ItemActivated += () => JoinSelected(table);
		return table;
	}

	private static Tree MakeTable((string title, int width)[] columns, int expand) {
		Tree table = new() {
			Columns = columns.Length,
			ColumnTitlesVisible = true,
			HideRoot = true,
			SelectMode = Tree.SelectModeEnum.Row,
			HideFolding = true,
			ScrollHorizontalEnabled = false,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 120),
		};
		for (int i = 0; i < columns.Length; ++i) {
			table.SetColumnTitle(i, columns[i].title);
			table.SetColumnCustomMinimumWidth(i, columns[i].width);
			table.SetColumnExpand(i, i == expand);
			table.SetColumnClipContent(i, true);
		}
		return table;
	}

	private void ShowGames() {
		loginPage.Visible = false;
		gamesPage.Visible = true;
		// Started by another program with its session (see ServerHandoff),
		// logging out here would end that program's session too.
		logOutButton.Visible = !ServerHandoff.Active;
		account.Text = $"Logged in as {client.Session.Username}";
		title.Text = $"Server Games on {client.ServerUrl.Authority}";
		ShowStatus("");
	}

	// Asks the server for both lists again.
	private async void RefreshAll(string message = null) {
		if (client?.Session == null) {
			return;
		}
		ShowStatus("Asking the server for your games...");
		try {
			MyGames mine = await client.MyGamesAsync(closing.Token);
			if (closing.IsCancellationRequested) {
				return;
			}
			ShowMyGames(mine);
			List<OpenGame> open = await client.OpenGamesAsync(closing.Token);
			if (closing.IsCancellationRequested) {
				return;
			}
			openGames = open;
			ShowOpenGames();
			int yours = mine.YourTurn?.Count ?? 0;
			ShowStatus(message ?? (yours == 0 ? "It isn't your turn in any of your games."
				: $"It's your turn in {yours} {(yours == 1 ? "game" : "games")}."));
		} catch (GameServerException e) {
			Failed(e);
		} catch (OperationCanceledException) {
		}
	}

	private void Failed(GameServerException e) {
		if (closing.IsCancellationRequested) {
			return;
		}
		if (e.SessionEnded) {
			SessionEnded();
		} else {
			ShowStatus(e.Message, true);
		}
	}

	private void ShowMyGames(MyGames mine) {
		string selected = SelectedGame()?.Id;
		gameRows.Clear();
		FillGames(yourTurnTable, mine.YourTurn ?? [], "No games are waiting for your turn.", selected);
		FillGames(othersTurnTable, mine.OthersTurn ?? [],
			"None. Start a game with New Server Game, or claim a seat in one under Open Games.", selected);
		ShowSelectedGame();
	}

	private void FillGames(Tree table, List<GameSummary> games, string empty, string selected) {
		table.Clear();
		TreeItem root = table.CreateItem();
		if (games.Count == 0) {
			AddNote(table, root, empty, 1);
			return;
		}
		string me = client.Session?.Username;
		foreach (GameSummary game in games) {
			TreeItem item = table.CreateItem(root);
			item.SetIcon(0, EraIcons.For(game.Era));
			item.SetIconMaxWidth(0, EraIcons.Width);
			item.SetTooltipText(0, EraIcons.Name(game.Era));
			item.SetText(1, game.Title);
			item.SetTooltipText(1, $"{game.Title}\nHosted by {game.Host}. Last played {game.Updated.ToLocalTime():g}.");
			item.SetText(2, game.YourCivilization ?? "");
			item.SetText(3, game.WaitingForPlayers ? "" : game.Turn.ToString());
			string whose = WhoseTurn(game, me);
			item.SetText(4, whose);
			item.SetTooltipText(4, whose);
			if (game.WaitingForPlayers) {
				item.SetCustomColor(4, Faded);
			}
			gameRows[item] = game;
			if (game.Id == selected) {
				item.Select(0);
			}
		}
	}

	// A line in an empty table, saying why it's empty.
	private static void AddNote(Tree table, TreeItem root, string text, int column) {
		TreeItem note = table.CreateItem(root);
		note.SetText(column, text);
		note.SetCustomColor(column, Faded);
		for (int i = 0; i < table.Columns; ++i) {
			note.SetSelectable(i, false);
		}
	}

	// Whose turn it is: each civilization to move, with its player; or what
	// the game waits for.
	public static string WhoseTurn(GameSummary game, string me) {
		if (game.WaitingForPlayers) {
			int taken = game.Seats?.Count(s => s.Username != null) ?? 0;
			int seats = game.Seats?.Count ?? 0;
			return $"Waiting for players ({taken} of {seats} seats claimed)";
		}
		if (game.ToMove == null || game.ToMove.Count == 0) {
			return "The computer's civilizations";
		}
		return string.Join(", ", game.ToMove.Select(s =>
			$"{s.Civilization} ({(s.Username == null ? "no player" : s.Username == me ? "you" : s.Username)})"));
	}

	private GameSummary SelectedGame() {
		foreach (Tree table in new[] { yourTurnTable, othersTurnTable }) {
			if (table.GetSelected() is TreeItem item && gameRows.TryGetValue(item, out GameSummary game)) {
				return game;
			}
		}
		return null;
	}

	// Play is for a game in Your Turn, View for one in Another Player's
	// Turn; neither for a game still waiting for its players.
	private void ShowSelectedGame() {
		GameSummary yours = yourTurnTable.GetSelected() is TreeItem a && gameRows.TryGetValue(a, out GameSummary g1) ? g1 : null;
		GameSummary others = othersTurnTable.GetSelected() is TreeItem b && gameRows.TryGetValue(b, out GameSummary g2) ? g2 : null;
		playButton.Disabled = yours == null || yours.WaitingForPlayers;
		viewButton.Disabled = others == null || others.WaitingForPlayers;
		const string waiting = "The game begins once every seat is claimed.";
		playButton.TooltipText = yours?.WaitingForPlayers == true ? waiting : "Connect to the game and take your turn.";
		viewButton.TooltipText = others?.WaitingForPlayers == true ? waiting
			: "Connect to the game and look at your cities, units and advisors while another player moves.";
		// Join Game is for My Games; Open Games has Claim Seat.
		GameSummary chosen = yours ?? others;
		if (joinButton == null) {
			ShowSelectedOpenGame();
			return;
		}
		joinButton.Visible = tabs.CurrentTab == 0;
		joinButton.Disabled = chosen == null || chosen.WaitingForPlayers;
		joinButton.TooltipText = chosen == null ? "Choose one of your games first."
			: chosen.WaitingForPlayers ? waiting
			: chosen == yours ? "Connect to the game and take your turn."
			: "Connect to the game and look at it while another player moves.";
		ShowSelectedOpenGame();
	}

	// The game chosen in either list, joined as double-clicking it would.
	private void JoinSelectedGame() {
		if (SelectedGame() is GameSummary game && !game.WaitingForPlayers) {
			ServerGames.Join(game);
		}
	}

	private void JoinSelected(Tree table) {
		if (table.GetSelected() is TreeItem item && gameRows.TryGetValue(item, out GameSummary game) && !game.WaitingForPlayers) {
			ServerGames.Join(game);
		}
	}

	// ---- Open Games ----

	private Control MakeOpenGames() {
		HBoxContainer page = new() { Name = "Open Games" };
		page.AddThemeConstantOverride("separation", 12);

		openTable = MakeTable(OpenColumns, expand: 1);
		openTable.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		openTable.ItemSelected += ShowSelectedOpenGame;
		page.AddChild(openTable);

		VBoxContainer side = new() { CustomMinimumSize = new Vector2(300, 0) };
		side.AddThemeConstantOverride("separation", 8);
		page.AddChild(side);
		side.AddChild(MakeLabel("Seats", 18));
		seatList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
		seatList.ItemSelected += _ => ShowSelectedOpenGame();
		seatList.ItemActivated += _ => Claim();
		side.AddChild(seatList);

		openPasswordRow = new HBoxContainer { Visible = false };
		openPasswordRow.AddThemeConstantOverride("separation", 8);
		openPasswordRow.AddChild(MakeLabel("Password:", 16));
		openPasswordEdit = new LineEdit { Secret = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		openPasswordEdit.TextSubmitted += _ => Claim();
		openPasswordRow.AddChild(openPasswordEdit);
		side.AddChild(openPasswordRow);

		claimButton = LanGameBrowser.MakeButton("Claim Seat", Claim);
		claimButton.CustomMinimumSize = new Vector2(140, 32);
		claimButton.Disabled = true;
		Highlight(claimButton);
		claimButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		side.AddChild(claimButton);
		return page;
	}

	private void ShowOpenGames() {
		string selected = SelectedOpenGame()?.Id;
		openTable.Clear();
		openRows.Clear();
		TreeItem root = openTable.CreateItem();
		if (openGames.Count == 0) {
			AddNote(openTable, root, "No games have seats free right now.", 1);
		}
		foreach (OpenGame game in openGames) {
			TreeItem item = openTable.CreateItem(root);
			item.SetText(0, game.HasPassword ? "yes" : "");
			item.SetTooltipText(0, game.HasPassword ? "Claiming a seat in this game needs its password, which the host gives out." : "");
			item.SetText(1, game.Title);
			item.SetTooltipText(1, game.Title);
			item.SetText(2, game.Host ?? "");
			item.SetText(3, game.Turn.ToString());
			int free = game.Seats?.Count(s => s.Username == null) ?? 0;
			item.SetText(4, $"{free} of {game.Seats?.Count ?? 0}");
			openRows[item] = game;
			if (game.Id == selected) {
				item.Select(0);
			}
		}
		ShowSelectedOpenGame();
	}

	private OpenGame SelectedOpenGame() {
		return openTable.GetSelected() is TreeItem item && openRows.TryGetValue(item, out OpenGame game) ? game : null;
	}

	private SeatSummary SelectedSeat() {
		int[] chosen = seatList.GetSelectedItems();
		return chosen.Length > 0 && chosen[0] < seatsShown.Count ? seatsShown[chosen[0]] : null;
	}

	// The selected game's seats, the free ones to choose from.
	private void ShowSelectedOpenGame() {
		OpenGame game = SelectedOpenGame();
		string seatChosen = SelectedSeat()?.PlayerId;
		if (!seatsListed || !ReferenceEquals(game, seatsGame)) {
			seatsListed = true;
			seatsGame = game;
			seatsShown = game?.Seats ?? [];
			List<SeatSummary> seats = seatsShown;
			seatList.Clear();
			foreach (SeatSummary seat in seats) {
				string leader = string.IsNullOrEmpty(seat.Leader) ? "" : $", {seat.Leader}";
				string who = seat.Username ?? "free";
				int index = seatList.AddItem($"{seat.Civilization}{leader}  ({who})");
				seatList.SetItemSelectable(index, seat.Username == null);
				if (seat.Username != null) {
					seatList.SetItemCustomFgColor(index, Faded);
				}
				if (seat.PlayerId == seatChosen && seat.Username == null) {
					seatList.Select(index);
				}
			}
			if (game == null) {
				seatList.AddItem("Choose a game to see its seats.", selectable: false);
			}
		}
		bool needsPassword = game?.HasPassword == true;
		if (openPasswordRow.Visible != needsPassword) {
			openPasswordRow.Visible = needsPassword;
			openPasswordEdit.Text = "";
		}
		claimButton.Disabled = busy || game == null || SelectedSeat() == null;
		claimButton.TooltipText = game == null ? "Choose a game, then the civilization you'll play in it."
			: SelectedSeat() == null ? "Choose the civilization you'll play: one of the free seats."
			: "Claim the seat: it's yours from then on.";
	}

	private async void Claim() {
		OpenGame game = SelectedOpenGame();
		SeatSummary seat = SelectedSeat();
		if (busy || game == null || seat == null || client?.Session == null) {
			return;
		}
		string password = game.HasPassword ? openPasswordEdit.Text : null;
		if (game.HasPassword && password == "") {
			ShowStatus("Type the game's password first: the host gives it out.", true);
			openPasswordEdit.GrabFocus();
			return;
		}
		busy = true;
		claimButton.Disabled = true;
		ShowStatus($"Claiming {seat.Civilization} in \"{game.Title}\"...");
		try {
			GameSummary joined = await client.ClaimSeatAsync(game.Id, seat.PlayerId, password, closing.Token);
			if (closing.IsCancellationRequested) {
				return;
			}
			log.Information("Claimed seat {Seat} in server game {Id}", seat.PlayerId, game.Id);
			busy = false;
			tabs.CurrentTab = 0;
			string begins = joined.WaitingForPlayers ? " The game begins once every seat is claimed." : "";
			RefreshAll($"The seat is yours: {joined.YourCivilization ?? seat.Civilization} in \"{joined.Title}\".{begins}");
		} catch (GameServerException e) {
			Failed(e);
		} catch (OperationCanceledException) {
		} finally {
			busy = false;
			if (IsInstanceValid(claimButton)) {
				ShowSelectedOpenGame();
			}
		}
	}

	// ---- New Server Game ----

	// To the Multiplayer Setup screen, to set the game up with a slot set to
	// Open for each human player; it comes back here once the game is made.
	private void NewServerGame() {
		if (client?.Session == null) {
			return;
		}
		MultiplayerSetupScreen.ServerGame = true;
		closing.Cancel();
		GetTree().ChangeSceneToFile(SetupScene);
	}

	// ---- Looks ----

	// A button that stands out once it can be pressed: dark, with light
	// text, as a chosen row is; dim like the others while it can't.
	private static void Highlight(Button button) {
		Color border = new(0.33f, 0.25f, 0.15f);
		button.AddThemeStyleboxOverride("normal", ButtonBox(new Color(0.45f, 0.30f, 0.12f), border));
		button.AddThemeStyleboxOverride("hover", ButtonBox(new Color(0.55f, 0.37f, 0.15f), border));
		button.AddThemeStyleboxOverride("pressed", ButtonBox(new Color(0.36f, 0.24f, 0.10f), border));
		Color light = new(0.99f, 0.96f, 0.87f);
		button.AddThemeColorOverride("font_color", light);
		button.AddThemeColorOverride("font_hover_color", light);
		button.AddThemeColorOverride("font_pressed_color", light);
		button.AddThemeColorOverride("font_focus_color", light);
	}

	private static StyleBoxFlat ButtonBox(Color fill, Color border) {
		return new StyleBoxFlat {
			BgColor = fill,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 10,
			ContentMarginRight = 10,
			ContentMarginTop = 3,
			ContentMarginBottom = 3,
		};
	}

	private void ShowStatus(string text, bool problem = false) {
		status.Text = text ?? "";
		status.AddThemeColorOverride("font_color", problem ? Warning : Ink);
	}

	private static Label MakeLabel(string text, int fontSize) {
		Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", Ink);
		return label;
	}

	// The LAN game list's parchment theme, with tabs and lists as the server
	// browser has them.
	private static Theme MakeTheme() {
		Theme theme = LanGameBrowser.MakeTheme();
		StyleBoxFlat listPanel = new() {
			BgColor = new Color(0.98f, 0.96f, 0.89f),
			BorderColor = new Color(0.47f, 0.39f, 0.27f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			ContentMarginLeft = 6,
			ContentMarginRight = 6,
			ContentMarginTop = 6,
			ContentMarginBottom = 6,
		};
		theme.SetStylebox("panel", "TabContainer", listPanel);
		theme.SetStylebox("panel", "ItemList", listPanel);
		theme.SetStylebox("focus", "ItemList", new StyleBoxEmpty());
		theme.SetColor("font_color", "ItemList", Ink);
		theme.SetColor("font_hovered_color", "ItemList", Ink);
		theme.SetColor("font_selected_color", "ItemList", new Color(0.99f, 0.96f, 0.87f));
		theme.SetStylebox("selected", "ItemList", new StyleBoxFlat { BgColor = new Color(0.33f, 0.25f, 0.15f) });
		theme.SetStylebox("selected_focus", "ItemList", new StyleBoxFlat { BgColor = new Color(0.33f, 0.25f, 0.15f) });
		theme.SetFontSize("font_size", "ItemList", 15);
		foreach (string style in new[] { "tab_selected", "tab_unselected", "tab_hovered", "tab_focus" }) {
			theme.SetStylebox(style, "TabContainer", theme.GetStylebox(style, "TabBar"));
		}
		foreach (string color in new[] { "font_selected_color", "font_unselected_color", "font_hovered_color" }) {
			theme.SetColor(color, "TabContainer", theme.GetColor(color, "TabBar"));
		}
		theme.SetColor("font_color", "Label", Ink);
		return theme;
	}

	// ---- Closing ----

	private void Close() {
		closing.Cancel();
		Closed?.Invoke();
		QueueFree();
	}

	// Before the menu's own keys.
	public override void _Input(InputEvent e) {
		if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) {
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	public override void _ExitTree() {
		closing.Cancel();
	}
}

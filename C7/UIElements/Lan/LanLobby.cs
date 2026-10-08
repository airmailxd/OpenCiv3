using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using Godot;
using Serilog;

// Where players gather before a LAN game. The host waits here for a player to
// take each human seat, then starts the game; the others find the host, take
// a seat (or just watch), and wait for the host to start. A guest may take
// several seats, for players taking turns at their machine as in a hotseat
// game. In a new game the guests also choose their civilizations here, and
// the host creates the game when starting it.
public partial class LanLobby : Control {
	private ILogger log = LogManager.ForContext<LanLobby>();

	// Whether we came here to join a game rather than host one.
	public static bool joining = false;

	private GlobalSingleton Global;

	private Label title;
	private VBoxContainer content;
	private Label status;
	private VBoxContainer seatList;
	private Button startButton;

	// Hosting a new game: whether it is being created, and why that failed.
	private bool creatingGame = false;
	private string createFailure;

	// Joining: the name to play under, the hosts found and where to connect.
	private LineEdit nameEdit;
	private LineEdit addressEdit;
	private VBoxContainer hostList;
	private VBoxContainer addressHelp;
	private bool searching = false;

	// Joining a new game: the civilizations to choose from, and the chosen
	// one's leader.
	private VBoxContainer civPicker;
	private readonly Dictionary<string, Civ3MenuButton> civButtons = new();
	private Civ3MenuButton randomCivButton;
	private List<CivilizationChoice> civChoices;
	private TextureRect leaderHead;
	private Label civDescription;
	private Label civHeading;

	// Joining: which of our seats the civ picker chooses for, and in a game
	// in progress, the open seats ticked to rejoin with.
	private ID choosingSeat;
	private readonly HashSet<ID> rejoinSeats = new();

	// Joining: names typed for our seats' players but not yet sent, and the
	// seat whose name is being typed, kept as the seats are redrawn.
	private readonly Dictionary<ID, string> seatNameDrafts = new();
	private ID editingSeatName;

	public override void _Ready() {
		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		// The 1024x768 parchment frame, centered like the other setup screens.
		ColorRect backdrop = new() { Color = Colors.Black };
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(backdrop);

		CenterContainer center = new();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		TextureRect background = new() { Texture = TextureLoader.Load("credits.background") };
		center.AddChild(background);

		// The title sits on the stone band above the parchment.
		title = new Label {
			Position = new Vector2(60, 14),
			Size = new Vector2(904, 52),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
		};
		title.AddThemeFontSizeOverride("font_size", 30);
		background.AddChild(title);

		// The parchment's inner panel; scrolls if the lobby outgrows it.
		ScrollContainer scroll = new() {
			Position = new Vector2(64, 90),
			Size = new Vector2(896, 590),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		background.AddChild(scroll);

		MarginContainer margin = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		foreach (string side in new[] { "left", "right", "top", "bottom" }) {
			margin.AddThemeConstantOverride($"margin_{side}", 12);
		}
		scroll.AddChild(margin);

		content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

		TextureButton exit = new() {
			Position = new Vector2(952, 720),
			TooltipText = "Back to the main menu",
			Shortcut = new Shortcut { Events = [new InputEventKey { Keycode = Key.Escape }] },
		};
		TextureLoader.SetButtonTextures(exit, "ui.exit");
		exit.Pressed += BackToMenu;
		background.AddChild(exit);

		if (joining) {
			BuildJoinScreen();
		} else {
			BuildHostScreen();
		}
	}

	private Label AddLabel(string text, int fontSize = 18) {
		Label label = new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		content.AddChild(label);
		return label;
	}

	private Button MakeButton(string text, Action onPressed) {
		Button button = new() { Text = text, CustomMinimumSize = new Vector2(160, 36) };
		button.AddThemeFontSizeOverride("font_size", 18);
		button.Pressed += onPressed;
		return button;
	}

	private void BackToMenu() {
		LanSession.End();
		LanSession.HostNextGame = false;
		LanSession.PendingGame = null;
		Global.ResetLoadGameFields();
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}

	// ---- Hosting ----

	private void BuildHostScreen() {
		title.Text = "Hosting a LAN Game";

		SaveGame save;
		try {
			if (LanSession.PendingGame is PendingLanGame pending) {
				// A new game, created once the guests have chosen.
				save = pending.save;
				Global.SaveGame = save;
				LanSession.BeginHosting(new LanHost(LanSession.PlayerName, pending.setup.playerCivilization.name,
					pending.guestSeats, save.Civilizations));
			} else {
				save = Global.SaveGame ?? LoadSavedGame(Global.LoadGamePath);
				Global.SaveGame = save;
				Global.LoadGamePath = null;
				LanSession.BeginHosting(new LanHost(LanSession.PlayerName, save));
			}
		} catch (Exception e) when (e is SocketException or ArgumentException or InvalidOperationException) {
			log.Error(e, "Could not host the LAN game");
			AddLabel($"Could not host the game: {e.Message}");
			return;
		}

		AddLabel("Players on your network should see this game listed under \"Join LAN Game\". If it isn't listed for them, they can type in one of this computer's addresses:");
		List<string> addresses = LanDiscovery.LocalAddresses();
		string port = LanSession.Host.Port == LanProtocol.DefaultPort ? "" : $":{LanSession.Host.Port}";
		AddLabel(addresses.Count == 0 ? "(No network connection found.)" : string.Join("    ", addresses.Select(a => a + port)), 22);
		AddLabel("Use the one on the same network as the other players: usually it starts with 192.168. or 10. Each human player in the game needs someone to take their seat before the game can start.");

		seatList = new VBoxContainer();
		seatList.AddThemeConstantOverride("separation", 6);
		content.AddChild(seatList);

		content.AddChild(MakeTurnTimeRow());

		status = AddLabel("");

		startButton = MakeButton("Start Game", StartHostedGame);
		startButton.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		content.AddChild(startButton);

		LanSession.Host.LobbyChanged += ShowHostSeats;
		ShowHostSeats();
	}

	// The choices for how long each player has for their turn, in minutes;
	// 0 means no limit.
	private static readonly int[] TurnTimeChoices = [0, 2, 5, 10, 15, 20, 30, 45, 60];

	private HBoxContainer MakeTurnTimeRow() {
		HBoxContainer row = new();
		row.AddThemeConstantOverride("separation", 12);
		Label label = new() { Text = "Time per turn:" };
		label.AddThemeFontSizeOverride("font_size", 18);
		row.AddChild(label);

		OptionButton choice = new();
		choice.AddThemeFontSizeOverride("font_size", 18);
		foreach (int minutes in TurnTimeChoices) {
			choice.AddItem(minutes == 0 ? "No limit" : $"{minutes} minutes");
		}
		choice.ItemSelected += index => {
			int minutes = TurnTimeChoices[index];
			LanSession.Host.TurnTimeLimit = minutes == 0 ? null : TimeSpan.FromMinutes(minutes);
		};
		row.AddChild(choice);

		if (LanSession.DevTurnSeconds is int seconds) {
			LanSession.Host.TurnTimeLimit = TimeSpan.FromSeconds(seconds);
			choice.Disabled = true;
			choice.AddItem($"{seconds} seconds");
			choice.Select(choice.ItemCount - 1);
		}

		// Everyone moves at once, and the computer players after them.
		CheckBox simultaneous = new() {
			Text = "Simultaneous turns",
			ButtonPressed = true,
			TooltipText = "Every human plays their turn at the same time, rather than waiting for each other.",
		};
		simultaneous.AddThemeFontSizeOverride("font_size", 18);
		simultaneous.Toggled += on => LanSession.Host.SimultaneousTurns = on;
		LanSession.Host.SimultaneousTurns = true;
		row.AddChild(simultaneous);
		return row;
	}

	private SaveGame LoadSavedGame(string path) {
		if (path == null) {
			throw new InvalidOperationException("There is no game to host");
		}
		return SaveManager.LoadSave(path, GamePaths.DefaultBicPath, scenarioSearchPath => {
			Util.setModPath(scenarioSearchPath);
			return Util.Civ3MediaPath("Text/PediaIcons.txt");
		});
	}

	private void ShowHostSeats() {
		foreach (Node child in seatList.GetChildren()) {
			child.QueueFree();
		}
		LanHost host = LanSession.Host;

		AddSeatRow(seatList, $"{LanSession.PlayerName} (you, hosting), {host.HostCivilization}");
		foreach (SeatInfo seat in host.Seats) {
			AddSeatRow(seatList, Describe(seat));
		}
		if (host.Spectators.Count > 0) {
			AddSeatRow(seatList, $"Watching: {string.Join(", ", host.Spectators)}");
		}

		if (createFailure != null) {
			status.Text = $"Could not create the game: {createFailure}";
			startButton.Disabled = true;
		} else if (creatingGame) {
			status.Text = "Creating the world...";
			startButton.Disabled = true;
		} else if (host.Seats.Count == 0) {
			status.Text = "This game has only one human player. Start a new game and add players on the player setup screen, or load a game with more human players.";
			startButton.Disabled = true;
		} else if (!host.AllSeatsTaken) {
			int open = host.Seats.Count(s => s.takenBy == null);
			status.Text = $"Waiting for {open} more {(open == 1 ? "player" : "players")} to join...";
			startButton.Disabled = true;
		} else {
			status.Text = host.GuestsChooseCivilizations
				? "Everyone is here. Players can change their civilization until you start; anyone who hasn't chosen gets a random one."
				: "Everyone is here.";
			startButton.Disabled = false;
			if (LanSession.DevHostSave != null) {
				CallDeferred(nameof(StartHostedGame));
			}
		}
	}

	private static string Describe(SeatInfo seat) {
		string who = seat.takenBy == null ? "open"
			: seat.disconnected ? $"{seat.takenBy} (disconnected)"
			: seat.takenBy;
		if (seat.away) {
			who += ", away";
		}
		string name = seat.playerName == null ? "" : $"{seat.playerName}, ";
		string civilization = seat.civilization ?? "civilization not chosen (random)";
		return $"{name}{civilization}: {who}";
	}

	private static void AddSeatRow(VBoxContainer list, string text, params Control[] buttons) {
		HBoxContainer row = new();
		row.AddThemeConstantOverride("separation", 12);
		Label label = new() { Text = text, CustomMinimumSize = new Vector2(420, 0) };
		label.AddThemeFontSizeOverride("font_size", 18);
		row.AddChild(label);
		foreach (Control button in buttons) {
			if (button != null) {
				row.AddChild(button);
			}
		}
		list.AddChild(row);
	}

	private void StartHostedGame() {
		if (!LanSession.Host.AllSeatsTaken || creatingGame || createFailure != null) {
			return;
		}
		if (LanSession.Host.GuestsChooseCivilizations) {
			CreatePendingGame();
			return;
		}
		LanSession.Host.LobbyChanged -= ShowHostSeats;
		LanSession.HostNextGame = false;
		GetTree().ChangeSceneToFile(LanSession.GameScene);
	}

	// Creates the new game with the civilizations the guests chose, then
	// starts it. World generation can take a while, so it runs off the UI
	// thread while the lobby keeps going.
	private void CreatePendingGame() {
		PendingLanGame pending = LanSession.PendingGame;
		creatingGame = true;
		pending.setup.hotseatPlayers = LanSession.Host.BeginCreatingGame();
		Thread thread = new(() => {
			string failure = "";
			try {
				pending.setup.Populate(pending.save);
			} catch (Exception e) {
				log.Error(e, "Could not create the LAN game");
				failure = e.Message;
			}
			try {
				CallDeferred(nameof(PendingGameCreated), failure);
			} catch (ObjectDisposedException) {
				// The host left the lobby meanwhile.
			}
		});
		thread.Start();
	}

	private void PendingGameCreated(string failure) {
		creatingGame = false;
		if (LanSession.Host == null) {
			return;
		}
		if (failure != "") {
			// The save is half made, so there's no trying again.
			createFailure = failure;
			ShowHostSeats();
			return;
		}
		PendingLanGame pending = LanSession.PendingGame;
		LanSession.PendingGame = null;
		LanSession.Host.GameCreated(pending.save);
		Global.SaveGame = pending.save;
		// If someone left while the world was made, this waits for their
		// seat to be taken again.
		StartHostedGame();
	}

	// ---- Joining ----

	private void BuildJoinScreen() {
		title.Text = "Join a LAN Game";

		HBoxContainer nameRow = new();
		nameRow.AddThemeConstantOverride("separation", 12);
		Label nameLabel = new() { Text = "Your name:" };
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameRow.AddChild(nameLabel);
		nameEdit = new LineEdit { Text = LanSession.PlayerName, CustomMinimumSize = new Vector2(260, 0) };
		nameRow.AddChild(nameEdit);
		content.AddChild(nameRow);

		HBoxContainer foundRow = new();
		foundRow.AddThemeConstantOverride("separation", 12);
		Label foundLabel = new() { Text = "Games on your network:" };
		foundLabel.AddThemeFontSizeOverride("font_size", 18);
		foundRow.AddChild(foundLabel);
		foundRow.AddChild(MakeButton("Search Again", SearchForHosts));
		content.AddChild(foundRow);

		hostList = new VBoxContainer();
		hostList.AddThemeConstantOverride("separation", 6);
		content.AddChild(hostList);

		HBoxContainer addressRow = new();
		addressRow.AddThemeConstantOverride("separation", 12);
		Label addressLabel = new() { Text = "Or join by address:" };
		addressLabel.AddThemeFontSizeOverride("font_size", 18);
		addressRow.AddChild(addressLabel);
		addressEdit = new LineEdit { PlaceholderText = "192.168.1.20", CustomMinimumSize = new Vector2(260, 0) };
		addressEdit.TextSubmitted += _ => ConnectToAddress(false);
		addressRow.AddChild(addressEdit);
		addressRow.AddChild(MakeButton("Join", () => ConnectToAddress(false)));
		addressRow.AddChild(MakeButton("Watch", () => ConnectToAddress(true)));
		content.AddChild(addressRow);

		seatList = new VBoxContainer();
		seatList.AddThemeConstantOverride("separation", 6);
		content.AddChild(seatList);

		civPicker = new VBoxContainer { Visible = false };
		civPicker.AddThemeConstantOverride("separation", 8);
		content.AddChild(civPicker);

		status = AddLabel("");

		AddAddressHelp();

		SearchForHosts();

		if (LanSession.DevJoinAddress != null) {
			addressEdit.Text = LanSession.DevJoinAddress;
			ConnectToAddress(LanSession.DevWatch);
		}
	}

	// Tips for finding the host when it isn't listed, hidden once connected.
	private void AddAddressHelp() {
		addressHelp = new VBoxContainer();
		addressHelp.AddThemeConstantOverride("separation", 6);
		content.AddChild(addressHelp);

		addressHelp.AddChild(new HSeparator());
		Label heading = new() { Text = "Game not listed? Finding the host's address" };
		heading.AddThemeFontSizeOverride("font_size", 20);
		addressHelp.AddChild(heading);

		string[] tips = [
			"The host's screen shows its addresses under \"Hosting a LAN Game\". Ask the host to read one out and type it in above.",
			"Or look it up on the host's computer. Windows: open Command Prompt, run ipconfig and use the \"IPv4 Address\" line. "
				+ "macOS: System Settings > Network, choose the connection, then Details. Linux: run hostname -I in a terminal.",
			"Addresses on a home network usually start with 192.168. or 10. One starting with 127. or 169.254. won't work from another computer.",
			"Both computers must be on the same network, such as the same router or Wi-Fi. Guest Wi-Fi often keeps devices from seeing each other.",
			$"If the address is right but you still can't connect, the host's firewall may be blocking the game. Allow OpenCiv3 through it "
				+ $"(TCP port {LanProtocol.DefaultPort}, and UDP port {LanProtocol.DiscoveryPort} for the list of games).",
			$"If the host uses a different port, add it after the address, like 192.168.1.20:{LanProtocol.DefaultPort + 1}.",
			"Playing over the internet? Both players can join the same virtual network (such as Tailscale or ZeroTier) and use the host's address on it.",
		];
		foreach (string tip in tips) {
			Label label = new() { Text = "•  " + tip, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			label.AddThemeFontSizeOverride("font_size", 15);
			addressHelp.AddChild(label);
		}
	}

	private async void SearchForHosts() {
		if (searching) {
			return;
		}
		searching = true;
		foreach (Node child in hostList.GetChildren()) {
			child.QueueFree();
		}
		Label searchingLabel = new() { Text = "Searching..." };
		hostList.AddChild(searchingLabel);

		List<FoundHost> hosts = await LanDiscovery.FindHosts(TimeSpan.FromSeconds(1.5));
		searching = false;
		if (!IsInstanceValid(hostList)) {
			return;
		}

		foreach (Node child in hostList.GetChildren()) {
			child.QueueFree();
		}
		if (hosts.Count == 0) {
			hostList.AddChild(new Label { Text = "No games found yet. Check the host is in its lobby, or join by address (see the tips below)." });
		}
		foreach (FoundHost found in hosts) {
			DiscoveryReply reply = found.reply;
			string state = reply.started ? "in progress" : "in the lobby";
			string seats = $"{reply.openSeats} open {(reply.openSeats == 1 ? "seat" : "seats")}";
			Button join = MakeButton("Join", () => Connect(found.address, reply.port, false));
			join.Disabled = reply.openSeats == 0;
			Button watch = MakeButton("Watch", () => Connect(found.address, reply.port, true));
			AddSeatRow(hostList, $"{reply.hostName} at {found.address}: {state}, {seats}", join, watch);
		}
	}

	private void ConnectToAddress(bool watch) {
		string text = addressEdit.Text.Trim();
		if (text == "") {
			return;
		}
		int port = LanProtocol.DefaultPort;
		int colon = text.LastIndexOf(':');
		if (colon > 0 && int.TryParse(text[(colon + 1)..], out int parsedPort)) {
			port = parsedPort;
			text = text[..colon];
		}
		Connect(text, port, watch);
	}

	private void Connect(string address, int port, bool watch) {
		LanSession.PlayerName = string.IsNullOrWhiteSpace(nameEdit.Text) ? LanSession.PlayerName : nameEdit.Text.Trim();
		try {
			LanSession.BeginJoining(LanClient.Connect(address, port, LanSession.PlayerName));
		} catch (Exception e) when (e is SocketException or ArgumentException) {
			status.Text = $"Could not connect to {address}: {e.Message}";
			return;
		}
		status.Text = $"Connected to {address}. Waiting for the host...";
		addressHelp.Visible = false;
		ClearCivPicker();
		civPicker.Visible = false;
		LanSession.Client.LobbyChanged += ShowJoinedSeats;
		if (watch) {
			LanSession.Client.Watch();
		}
	}

	private void ShowJoinedSeats() {
		LanClient client = LanSession.Client;
		if (client.RejectedReason != null) {
			status.Text = client.RejectedReason;
			return;
		}
		if (client.StartingGame != null) {
			// The host started the game; it's ours from here.
			client.LobbyChanged -= ShowJoinedSeats;
			Global.SaveGame = client.StartingGame;
			GetTree().ChangeSceneToFile(LanSession.GameScene);
			return;
		}

		LobbyInfo lobby = client.Lobby;
		if (lobby == null) {
			return;
		}
		foreach (Node child in seatList.GetChildren()) {
			child.QueueFree();
		}
		string turns = lobby.simultaneousTurns ? " (simultaneous turns)" : "";
		Label header = new() { Text = $"Seats in {lobby.hostName}'s game{turns}:" };
		header.AddThemeFontSizeOverride("font_size", 20);
		seatList.AddChild(header);

		IReadOnlyList<ID> mine = client.YourSeats;
		bool canChoose = lobby.civilizations != null && mine.Count > 0 && !client.IsSpectator;
		if (!mine.Contains(choosingSeat)) {
			choosingSeat = mine.FirstOrDefault();
		}
		rejoinSeats.RemoveWhere(id => !lobby.seats.Any(s => s.playerID == id && s.takenBy == null));

		foreach (SeatInfo seat in lobby.seats) {
			if (seat.isHost) {
				AddSeatRow(seatList, $"{seat.playerName} (hosting), {seat.civilization}");
				continue;
			}
			List<Control> buttons = [];
			bool open = seat.takenBy == null && !client.IsSpectator && !lobby.creatingGame;
			if (open && !lobby.started) {
				buttons.Add(MakeButton("Take Seat", () => client.ClaimSeat(seat.playerID, NameForAnotherSeat(lobby, mine))));
			} else if (open) {
				// In a game in progress, taking a seat takes us into it, so
				// every seat to take is ticked first.
				CheckBox tick = new() { Text = "Take", ButtonPressed = rejoinSeats.Contains(seat.playerID) };
				tick.AddThemeFontSizeOverride("font_size", 18);
				tick.Toggled += on => {
					if (on) {
						rejoinSeats.Add(seat.playerID);
					} else {
						rejoinSeats.Remove(seat.playerID);
					}
					ShowJoinedSeats();
				};
				buttons.Add(tick);
			}
			if (mine.Contains(seat.playerID)) {
				if (canChoose && mine.Count > 1 && seat.playerID != choosingSeat) {
					buttons.Add(MakeButton("Choose Civilization", () => {
						choosingSeat = seat.playerID;
						ShowJoinedSeats();
					}));
				}
				if (!lobby.started && !lobby.creatingGame) {
					buttons.Add(MakeButton("Leave Seat", () => client.LeaveSeat(seat.playerID)));
				}
			}
			if (mine.Contains(seat.playerID) && !lobby.started && !lobby.creatingGame) {
				// Each player at this computer has their own name.
				string place = seat.playerName == null ? "" : $"{seat.playerName}, ";
				string civilization = seat.civilization ?? "civilization not chosen (random)";
				AddSeatRow(seatList, $"{place}{civilization} (you):", [MakeSeatNameEdit(client, seat), .. buttons]);
				continue;
			}
			string you = mine.Contains(seat.playerID) ? " (you)" : "";
			AddSeatRow(seatList, Describe(seat) + you, [.. buttons]);
		}
		foreach (ID gone in seatNameDrafts.Keys.Where(id => !mine.Contains(id)).ToList()) {
			seatNameDrafts.Remove(gone);
		}

		if (lobby.started && !client.IsSpectator && lobby.seats.Any(s => !s.isHost && s.takenBy == null)) {
			Button rejoin = MakeButton("Join Game", () => {
				foreach (SeatInfo seat in lobby.seats.Where(s => rejoinSeats.Contains(s.playerID))) {
					client.ClaimSeat(seat.playerID, seat.playerName ?? NameForAnotherSeat(lobby, mine));
				}
				rejoinSeats.Clear();
			});
			rejoin.Disabled = rejoinSeats.Count == 0;
			rejoin.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
			seatList.AddChild(rejoin);
		}

		if (lobby.spectators?.Count > 0) {
			AddSeatRow(seatList, $"Watching: {string.Join(", ", lobby.spectators)}");
		}

		UpdateCivPicker(lobby, canChoose);

		string hotseatTip = " To play several people at this computer, taking turns, take a seat for each of them"
			+ " and type each player's name beside their seat.";
		status.Text = lobby.creatingGame ? $"{lobby.hostName} is creating the world..."
			: client.IsSpectator ? "Watching. Waiting for the host to start the game..."
			: lobby.started && mine.Count == 0 ? "This game is in progress. Tick the seats to play, then join it." + hotseatTip
			: mine.Count == 0 ? "Take an open seat to play." + hotseatTip
			: canChoose ? "Choose your civilization, then wait for the host to start the game. If you don't choose, you get a random one."
			: "Waiting for the host to start the game...";

		SeatInfo firstOpen = lobby.seats.FirstOrDefault(s => !s.isHost && s.takenBy == null);
		if (LanSession.DevJoinAddress != null && !client.IsSpectator && mine.Count == 0 && firstOpen != null) {
			client.ClaimSeat(firstOpen.playerID);
		}
	}

	// A box for the name of the player in one of our seats, sent to the host
	// on Enter or on leaving the box.
	private LineEdit MakeSeatNameEdit(LanClient client, SeatInfo seat) {
		ID id = seat.playerID;
		LineEdit edit = new() {
			Text = seatNameDrafts.TryGetValue(id, out string draft) ? draft : seat.takenBy,
			PlaceholderText = "Player's name",
			MaxLength = 40,
			CustomMinimumSize = new Vector2(200, 0),
		};
		edit.TextChanged += text => seatNameDrafts[id] = text;
		void Send() {
			if (seatNameDrafts.Remove(id, out string name) && !string.IsNullOrWhiteSpace(name) && name.Trim() != seat.takenBy) {
				client.ClaimSeat(id, name.Trim());
			}
		}
		edit.TextSubmitted += _ => {
			Send();
			edit.ReleaseFocus();
		};
		edit.FocusEntered += () => editingSeatName = id;
		edit.FocusExited += () => {
			// Redrawing the seats frees the box; the one replacing it takes
			// over the typing.
			if (edit.IsQueuedForDeletion()) {
				return;
			}
			editingSeatName = null;
			Send();
		};
		if (editingSeatName == id) {
			Callable.From(() => {
				if (IsInstanceValid(edit) && edit.IsInsideTree()) {
					edit.GrabFocus();
					edit.CaretColumn = edit.Text.Length;
				}
			}).CallDeferred();
		}
		return edit;
	}

	// The name for the player in another seat we take: the one typed in,
	// numbered if one of our seats already has it.
	private string NameForAnotherSeat(LobbyInfo lobby, IReadOnlyList<ID> mine) {
		string name = string.IsNullOrWhiteSpace(nameEdit.Text) ? LanSession.PlayerName : nameEdit.Text.Trim();
		HashSet<string> used = lobby.seats.Where(s => mine.Contains(s.playerID)).Select(s => s.takenBy).ToHashSet();
		if (!used.Contains(name)) {
			return name;
		}
		for (int n = 2; ; ++n) {
			if (!used.Contains($"{name} {n}")) {
				return $"{name} {n}";
			}
		}
	}

	// The civilizations a guest can choose from, in the style of the player
	// setup screen: the ones other players have are greyed out.
	private void UpdateCivPicker(LobbyInfo lobby, bool choosing) {
		civPicker.Visible = choosing;
		if (!choosing) {
			return;
		}
		// Built once per host, and again if the host's choices change (e.g.
		// after joining another host).
		if (civChoices == null || pickerClient != LanSession.Client || !SameChoices(civChoices, lobby.civilizations)) {
			ClearCivPicker();
			pickerClient = LanSession.Client;
			BuildCivPicker(lobby.civilizations);
		}

		SeatInfo mine = lobby.seats.Find(s => s.playerID == choosingSeat);
		civHeading.Text = LanSession.Client.YourSeats.Count > 1
			? $"Choose a civilization for {mine?.takenBy}:"
			: "Choose your civilization:";
		HashSet<string> takenByOthers = lobby.seats
			.Where(s => s.playerID != choosingSeat && s.civilization != null)
			.Select(s => s.civilization)
			.ToHashSet();
		foreach ((string name, Civ3MenuButton button) in civButtons) {
			bool taken = takenByOthers.Contains(name);
			button.Disabled = taken || lobby.creatingGame;
			button.Modulate = taken ? new Color(1, 1, 1, 0.35f) : Colors.White;
			button.TooltipText = taken ? "Another player has this civilization." : "";
		}
		randomCivButton.Disabled = lobby.creatingGame;

		Civ3MenuButton chosen = randomCivButton;
		if (mine?.civilization != null && civButtons.TryGetValue(mine.civilization, out Civ3MenuButton mineButton)) {
			chosen = mineButton;
		}
		if (!chosen.ButtonPressed) {
			chosen.ButtonPressed = true;
		}
		ShowCivilization(mine?.civilization);
	}

	// The connection the civ picker was built for.
	private LanClient pickerClient;

	private static bool SameChoices(List<CivilizationChoice> a, List<CivilizationChoice> b) {
		if (a == null || b == null || a.Count != b.Count) {
			return false;
		}
		for (int i = 0; i < a.Count; ++i) {
			CivilizationChoice x = a[i], y = b[i];
			if (x.name != y.name || x.leader != y.leader || x.noun != y.noun || x.leaderArtFile != y.leaderArtFile
				|| !(x.traits ?? []).SequenceEqual(y.traits ?? [])) {
				return false;
			}
		}
		return true;
	}

	private void ClearCivPicker() {
		foreach (Node child in civPicker.GetChildren()) {
			civPicker.RemoveChild(child);
			child.QueueFree();
		}
		civButtons.Clear();
		randomCivButton = null;
		civChoices = null;
		leaderHead = null;
		civDescription = null;
		civHeading = null;
		pickerClient = null;
		// Another host's civilizations may have the same names but other art.
		leaderHeadCache.Clear();
	}

	private void BuildCivPicker(List<CivilizationChoice> choices) {
		civChoices = choices;
		civHeading = new() { Text = "Choose your civilization:" };
		civHeading.AddThemeFontSizeOverride("font_size", 20);
		civPicker.AddChild(civHeading);

		HBoxContainer body = new();
		body.AddThemeConstantOverride("separation", 24);
		civPicker.AddChild(body);

		GridContainer grid = new() { Columns = 4 };
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 2);
		body.AddChild(grid);

		ButtonGroup group = new();
		Civ3MenuButton MakeCivButton(string text, string civilization) {
			Civ3MenuButton button = new() {
				Text = text,
				FontSize = 14,
				ToggleMode = true,
				ButtonGroup = group,
			};
			button.Pressed += () => {
				LanSession.Client?.ChooseCivilization(civilization, choosingSeat);
				ShowCivilization(civilization);
			};
			grid.AddChild(button);
			return button;
		}
		foreach (CivilizationChoice choice in choices) {
			civButtons[choice.name] = MakeCivButton(choice.name, choice.name);
		}
		randomCivButton = MakeCivButton("Random", null);

		VBoxContainer leader = new() { CustomMinimumSize = new Vector2(200, 0) };
		body.AddChild(leader);
		leaderHead = new TextureRect {
			CustomMinimumSize = new Vector2(172, 172),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		};
		leader.AddChild(leaderHead);
		civDescription = new Label {
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		civDescription.AddThemeFontSizeOverride("font_size", 15);
		leader.AddChild(civDescription);
	}

	// The leader heads already shown, by civilization name and art file.
	private readonly Dictionary<(string, string), ImageTexture> leaderHeadCache = new();

	// Shows the civilization's leader and traits, or null for a random one.
	private void ShowCivilization(string name) {
		CivilizationChoice choice = civChoices?.Find(c => c.name == name);
		if (choice == null) {
			leaderHead.Texture = null;
			civDescription.Text = "A random civilization nobody else has.";
			return;
		}
		civDescription.Text = $"{choice.leader} of the {choice.noun}\n({string.Join(", ", choice.traits)})";
		try {
			var key = (choice.name, choice.leaderArtFile);
			if (!leaderHeadCache.TryGetValue(key, out ImageTexture texture) || !IsInstanceValid(texture)) {
				Civilization civ = new(choice.name) { leader = choice.leader, noun = choice.noun, leaderArtFile = choice.leaderArtFile };
				texture = TextureLoader.Load("leader_heads", civ);
				leaderHeadCache[key] = texture;
			}
			leaderHead.Texture = texture;
		} catch (Exception e) {
			// The host may have art this computer doesn't.
			log.Warning("No leader head for {Civilization}: {Error}", choice.name, e.Message);
			leaderHead.Texture = null;
		}
	}

	public override void _Process(double delta) {
		LanSession.Host?.Poll();

		LanClient client = LanSession.Client;
		if (client != null) {
			client.Poll();
			if (!client.IsConnected && client.StartingGame == null && client.RejectedReason == null) {
				status.Text = "Lost the connection to the host.";
				if (addressHelp != null) {
					addressHelp.Visible = true;
				}
				LanSession.End();
			}
		}
	}
}

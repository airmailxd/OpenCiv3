using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using C7Engine;
using C7Engine.Network;
using C7GameData.Save;
using Godot;
using Serilog;

// Where players gather before a LAN game. The host waits here for a player to
// take each human seat, then starts the game; the others find the host, take
// a seat, and wait for the host to start.
public partial class LanLobby : Control {
	private ILogger log = LogManager.ForContext<LanLobby>();

	// Whether we came here to join a game rather than host one.
	public static bool joining = false;

	private GlobalSingleton Global;

	private VBoxContainer content;
	private Label status;
	private VBoxContainer seatList;
	private Button startButton;

	// Joining: the name to play under, the hosts found and where to connect.
	private LineEdit nameEdit;
	private LineEdit addressEdit;
	private VBoxContainer hostList;
	private bool searching = false;

	public override void _Ready() {
		Global = GetNode<GlobalSingleton>("/root/GlobalSingleton");
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		ColorRect background = new() { Color = new Color(0.08f, 0.1f, 0.14f) };
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(background);

		MarginContainer margin = new();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		foreach (string side in new[] { "left", "right", "top", "bottom" }) {
			margin.AddThemeConstantOverride($"margin_{side}", 60);
		}
		AddChild(margin);

		content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);

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
		Global.ResetLoadGameFields();
		GetTree().ChangeSceneToFile("res://UIElements/MainMenu/main_menu.tscn");
	}

	// ---- Hosting ----

	private void BuildHostScreen() {
		AddLabel("Hosting a LAN Game", 32);

		SaveGame save;
		try {
			save = Global.SaveGame ?? LoadSavedGame(Global.LoadGamePath);
			Global.SaveGame = save;
			Global.LoadGamePath = null;
			LanSession.BeginHosting(new LanHost(LanSession.PlayerName, save));
		} catch (Exception e) when (e is SocketException or ArgumentException or InvalidOperationException) {
			log.Error(e, "Could not host the LAN game");
			AddLabel($"Could not host the game: {e.Message}");
			content.AddChild(MakeButton("Back", BackToMenu));
			return;
		}

		List<string> addresses = LanDiscovery.LocalAddresses();
		string where = addresses.Count == 0 ? "this computer's address" : string.Join(" or ", addresses);
		AddLabel($"Players on your network will find this game under \"Join LAN Game\", or can join at {where} (port {LanSession.Host.Port}).");
		AddLabel("Each human player in the game needs someone to take their seat before the game can start.");

		seatList = new VBoxContainer();
		seatList.AddThemeConstantOverride("separation", 6);
		content.AddChild(seatList);

		status = AddLabel("");

		HBoxContainer buttons = new();
		buttons.AddThemeConstantOverride("separation", 12);
		startButton = MakeButton("Start Game", StartHostedGame);
		buttons.AddChild(startButton);
		buttons.AddChild(MakeButton("Cancel", BackToMenu));
		content.AddChild(buttons);

		LanSession.Host.LobbyChanged += ShowHostSeats;
		ShowHostSeats();
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

		AddSeatRow(seatList, $"{LanSession.PlayerName} (you, hosting)", null);
		foreach (SeatInfo seat in host.Seats) {
			AddSeatRow(seatList, Describe(seat), null);
		}

		if (host.Seats.Count == 0) {
			status.Text = "This game has only one human player. Start a new game and add players on the player setup screen, or load a game with more human players.";
			startButton.Disabled = true;
		} else if (!host.AllSeatsTaken) {
			int open = host.Seats.Count(s => s.takenBy == null);
			status.Text = $"Waiting for {open} more {(open == 1 ? "player" : "players")} to join...";
			startButton.Disabled = true;
		} else {
			status.Text = "Everyone is here.";
			startButton.Disabled = false;
			if (LanSession.DevHostSave != null) {
				CallDeferred(nameof(StartHostedGame));
			}
		}
	}

	private static string Describe(SeatInfo seat) {
		string who = seat.takenBy ?? "open";
		string name = seat.playerName == null ? "" : $"{seat.playerName}, ";
		return $"{name}{seat.civilization}: {who}";
	}

	private static void AddSeatRow(VBoxContainer list, string text, Button button) {
		HBoxContainer row = new();
		row.AddThemeConstantOverride("separation", 12);
		Label label = new() { Text = text, CustomMinimumSize = new Vector2(420, 0) };
		label.AddThemeFontSizeOverride("font_size", 18);
		row.AddChild(label);
		if (button != null) {
			row.AddChild(button);
		}
		list.AddChild(row);
	}

	private void StartHostedGame() {
		if (!LanSession.Host.AllSeatsTaken) {
			return;
		}
		LanSession.Host.LobbyChanged -= ShowHostSeats;
		LanSession.HostNextGame = false;
		GetTree().ChangeSceneToFile(LanSession.GameScene);
	}

	// ---- Joining ----

	private void BuildJoinScreen() {
		AddLabel("Join a LAN Game", 32);

		HBoxContainer nameRow = new();
		nameRow.AddThemeConstantOverride("separation", 12);
		Label nameLabel = new() { Text = "Your name:" };
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameRow.AddChild(nameLabel);
		nameEdit = new LineEdit { Text = LanSession.PlayerName, CustomMinimumSize = new Vector2(260, 0) };
		nameRow.AddChild(nameEdit);
		content.AddChild(nameRow);

		AddLabel("Games on your network:");
		hostList = new VBoxContainer();
		hostList.AddThemeConstantOverride("separation", 6);
		content.AddChild(hostList);

		HBoxContainer addressRow = new();
		addressRow.AddThemeConstantOverride("separation", 12);
		Label addressLabel = new() { Text = "Or join by address:" };
		addressLabel.AddThemeFontSizeOverride("font_size", 18);
		addressRow.AddChild(addressLabel);
		addressEdit = new LineEdit { PlaceholderText = "192.168.1.20", CustomMinimumSize = new Vector2(260, 0) };
		addressEdit.TextSubmitted += _ => ConnectToAddress();
		addressRow.AddChild(addressEdit);
		addressRow.AddChild(MakeButton("Join", ConnectToAddress));
		content.AddChild(addressRow);

		seatList = new VBoxContainer();
		seatList.AddThemeConstantOverride("separation", 6);
		content.AddChild(seatList);

		status = AddLabel("");

		HBoxContainer buttons = new();
		buttons.AddThemeConstantOverride("separation", 12);
		buttons.AddChild(MakeButton("Search Again", SearchForHosts));
		buttons.AddChild(MakeButton("Back", BackToMenu));
		content.AddChild(buttons);

		SearchForHosts();

		if (LanSession.DevJoinAddress != null) {
			addressEdit.Text = LanSession.DevJoinAddress;
			ConnectToAddress();
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
			hostList.AddChild(new Label { Text = "No games found. Check the host is in its lobby, or join by address." });
		}
		foreach (FoundHost found in hosts) {
			DiscoveryReply reply = found.reply;
			string state = reply.started ? "in progress" : "in the lobby";
			string seats = $"{reply.openSeats} open {(reply.openSeats == 1 ? "seat" : "seats")}";
			Button join = MakeButton("Join", () => Connect(found.address, reply.port));
			join.Disabled = reply.openSeats == 0;
			AddSeatRow(hostList, $"{reply.hostName} at {found.address}: {state}, {seats}", join);
		}
	}

	private void ConnectToAddress() {
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
		Connect(text, port);
	}

	private void Connect(string address, int port) {
		LanSession.PlayerName = string.IsNullOrWhiteSpace(nameEdit.Text) ? LanSession.PlayerName : nameEdit.Text.Trim();
		try {
			LanSession.BeginJoining(LanClient.Connect(address, port, LanSession.PlayerName));
		} catch (Exception e) when (e is SocketException or ArgumentException) {
			status.Text = $"Could not connect to {address}: {e.Message}";
			return;
		}
		status.Text = $"Connected to {address}. Waiting for the host...";
		LanSession.Client.LobbyChanged += ShowJoinedSeats;
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
		Label header = new() { Text = $"Seats in {lobby.hostName}'s game:" };
		header.AddThemeFontSizeOverride("font_size", 20);
		seatList.AddChild(header);

		foreach (SeatInfo seat in lobby.seats) {
			if (seat.isHost) {
				AddSeatRow(seatList, $"{seat.playerName} (hosting), {seat.civilization}", null);
				continue;
			}
			Button take = null;
			if (lobby.yourSeat == null && seat.takenBy == null) {
				take = MakeButton("Take Seat", () => client.ClaimSeat(seat.playerID));
			}
			string you = seat.playerID == lobby.yourSeat ? " (you)" : "";
			AddSeatRow(seatList, Describe(seat) + you, take);
		}

		status.Text = lobby.yourSeat == null
			? "Take an open seat to play."
			: "Waiting for the host to start the game...";

		SeatInfo open = lobby.seats.FirstOrDefault(s => !s.isHost && s.takenBy == null);
		if (LanSession.DevJoinAddress != null && lobby.yourSeat == null && open != null) {
			client.ClaimSeat(open.playerID);
		}
	}

	public override void _Process(double delta) {
		LanSession.Host?.Poll();

		LanClient client = LanSession.Client;
		if (client != null) {
			client.Poll();
			if (!client.IsConnected && client.StartingGame == null && client.RejectedReason == null) {
				status.Text = "Lost the connection to the host.";
				LanSession.End();
			}
		}
	}
}

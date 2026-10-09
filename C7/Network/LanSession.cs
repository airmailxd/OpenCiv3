using System.Linq;
using Godot;
using C7Engine;
using C7Engine.Network;
using C7GameData;

// The LAN game this machine is hosting or has joined, if any. It lives across
// scene changes, from the lobby into the game.
public static class LanSession {
	public static LanHost Host { get; private set; }
	public static LanClient Client { get; private set; }

	public static bool IsActive => Host != null || Client != null;
	public static bool IsClient => Client != null;
	public static bool IsSpectator => Client?.IsSpectator == true;

	// The name this player goes by in LAN and online games, which everyone
	// in them sees, kept in the settings. It's never taken from the
	// computer, whose user name is often the player's own.
	public static string PlayerName {
		get {
			string name = C7Settings.GetSettingsValueOrDefault(SettingsSection, "playerName", null);
			return string.IsNullOrWhiteSpace(name) ? DefaultPlayerName : name.Trim();
		}
		set {
			if (string.IsNullOrWhiteSpace(value) || value.Trim() == PlayerName) {
				return;
			}
			C7Settings.SetValue(SettingsSection, "playerName", value.Trim());
			C7Settings.SaveSettings();
		}
	}

	public const string DefaultPlayerName = "Player";
	private const string SettingsSection = "lan";

	// Developer options for trying LAN games from the command line, after "--":
	//   --lan-host=<save>     host the saved game, starting once every seat is taken
	//   --lan-join=<address>  join the host there (or the online game with that
	//                         join code) and take the first open seat
	//   --lan-watch=<address> join the host there as a spectator
	//   --lan-online          also host the game online, through the relay
	//   --lan-autoplay        end each of this machine's turns soon after it starts
	//   --lan-turn-time=<s>   give each player this many seconds for their turn
	public static readonly string DevHostSave;
	public static readonly string DevJoinAddress;
	public static readonly bool DevWatch;
	public static readonly bool DevHostOnline;
	public static readonly bool DevAutoplay;
	public static readonly int? DevTurnSeconds;
	private static bool devStartUsed = false;

	static LanSession() {
		foreach (string arg in OS.GetCmdlineUserArgs()) {
			if (arg.StartsWith("--lan-host=")) {
				DevHostSave = arg["--lan-host=".Length..];
			} else if (arg.StartsWith("--lan-join=")) {
				DevJoinAddress = arg["--lan-join=".Length..];
			} else if (arg.StartsWith("--lan-watch=")) {
				DevJoinAddress = arg["--lan-watch=".Length..];
				DevWatch = true;
			} else if (arg == "--lan-online") {
				DevHostOnline = true;
			} else if (arg == "--lan-autoplay") {
				DevAutoplay = true;
			} else if (arg.StartsWith("--lan-turn-time=") && int.TryParse(arg["--lan-turn-time=".Length..], out int seconds)) {
				DevTurnSeconds = seconds;
			}
		}
	}

	// True the first time the main menu asks, if a developer option says to
	// go straight into a LAN game.
	public static bool TakeDevStart() {
		if (devStartUsed || (DevHostSave == null && DevJoinAddress == null)) {
			return false;
		}
		devStartUsed = true;
		return true;
	}

	public const string LobbyScene = "res://UIElements/Lan/lan_lobby.tscn";
	public const string GameScene = "res://C7Game.tscn";

	// Set from the main menu: the game being set up will be hosted for
	// players on the LAN, so it opens the lobby rather than the game.
	public static bool HostNextGame = false;

	// A new game to host whose guests choose their civilizations in the
	// lobby: the player setup screen's choices, with the save to create the
	// game in once the host starts it. Null otherwise.
	public static PendingLanGame PendingGame;

	// Set from the main menu to host the last LAN game hosted here again,
	// from its autosave. Null otherwise.
	public static LanResumeInfo ResumeGame;

	// The LAN game this machine last joined, kept in the settings so that
	// its player can rejoin it after closing their game: where it was, on
	// the network or online.
	public record LastGame(string hostName, LanEndpoint endpoint, string token) {
		public string address => (endpoint as LanAddressEndpoint)?.Address;
		public int port => (endpoint as LanAddressEndpoint)?.Port ?? 0;
	}

	private const string LastGameSection = "LastLanGame";

	public static LastGame LastJoinedGame {
		get {
			string token = C7Settings.GetSettingsValueOrDefault(LastGameSection, "Token", null);
			if (string.IsNullOrEmpty(token)) {
				return null;
			}
			string code = C7Settings.GetSettingsValueOrDefault(LastGameSection, "JoinCode", null);
			string relayUrl = C7Settings.GetSettingsValueOrDefault(LastGameSection, "RelayUrl", null);
			string address = C7Settings.GetSettingsValueOrDefault(LastGameSection, "Address", null);
			LanEndpoint endpoint;
			if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(relayUrl)) {
				endpoint = new RelayEndpoint(relayUrl, code);
			} else if (!string.IsNullOrEmpty(address)
				&& int.TryParse(C7Settings.GetSettingsValueOrDefault(LastGameSection, "Port", ""), out int port)) {
				endpoint = new LanAddressEndpoint(address, port);
			} else {
				return null;
			}
			return new LastGame(C7Settings.GetSettingsValueOrDefault(LastGameSection, "HostName", endpoint.Description), endpoint, token);
		}
	}

	// Remembers the game the client is playing in, to rejoin it later.
	public static void RememberJoinedGame(LanClient client) {
		if (client.IsSpectator || client.ReconnectToken == null) {
			return;
		}
		C7Settings.SetValue(LastGameSection, "HostName", client.Lobby?.hostName ?? client.HostAddress);
		if (client.Endpoint is RelayEndpoint online) {
			C7Settings.SetValue(LastGameSection, "RelayUrl", online.RelayUrl);
			C7Settings.SetValue(LastGameSection, "JoinCode", online.Code);
			C7Settings.RemoveValue(LastGameSection, "Address");
			C7Settings.RemoveValue(LastGameSection, "Port");
		} else {
			C7Settings.SetValue(LastGameSection, "Address", client.Address);
			C7Settings.SetValue(LastGameSection, "Port", client.Port.ToString());
			C7Settings.RemoveValue(LastGameSection, "RelayUrl");
			C7Settings.RemoveValue(LastGameSection, "JoinCode");
		}
		C7Settings.SetValue(LastGameSection, "Token", client.ReconnectToken);
		C7Settings.SaveSettings();
	}

	// Goes to the game once it has been set up, by way of the lobby if it
	// will be hosted.
	public static void StartGame(SceneTree tree) {
		if (HostNextGame) {
			LanLobby.joining = false;
			tree.ChangeSceneToFile(LobbyScene);
		} else {
			tree.ChangeSceneToFile(GameScene);
		}
	}

	// Whose turn it is and how long they have had it, or null outside a
	// LAN game.
	public static TurnClockInfo TurnClock => Host?.CurrentClock() ?? Client?.CurrentClock();

	public static void BeginHosting(LanHost host) {
		End();
		Host = host;
	}

	public static void BeginJoining(LanClient client) {
		End();
		Client = client;
		client.PredictMoves = MovePrediction.Enabled;
		EngineStorage.remoteEngine = client.SendCommand;
	}

	// True when the player plays at this machine: the host's own player, or
	// one of the seats this client took.
	public static bool IsLocalPlayer(Player player) {
		if (Host != null) {
			return player.id == Host.HostPlayerID;
		}
		return Client != null && Client.PlayerIDs.Contains(player.id);
	}

	// Shows a spectator's game the way the host sends it (see
	// SpectatorViewMode): the whole map in observer mode, or the map as the
	// civilization it watches as knows it, or as all of them do together.
	// The player the UI looks through stays the same where it can, for the
	// city screen and the advisors.
	public static void ShowSpectatorView(GameData gameData) {
		if (!IsSpectator) {
			return;
		}
		SpectatorViewInfo view = Client.SpectatorView;
		// A host that doesn't say sends the whole game.
		SpectatorViewMode mode = view?.mode ?? SpectatorViewMode.Omniscient;
		Player watched = mode == SpectatorViewMode.OneCiv ? gameData.GetPlayer(view.playerID) : null;
		if (mode == SpectatorViewMode.OneCiv && watched == null) {
			mode = SpectatorViewMode.AllCivs;
		}
		gameData.observerMode = mode == SpectatorViewMode.Omniscient;
		Player shown = watched ?? gameData.GetPlayer(EngineStorage.uiControllerID);
		if (shown == null || shown.isBarbarians) {
			shown = gameData.players.FirstOrDefault(p => p.isHuman && !p.defeated)
				?? gameData.players.FirstOrDefault(p => !p.isBarbarians) ?? gameData.players[0];
		}
		EngineStorage.uiControllerID = shown.id;
		if (mode == SpectatorViewMode.AllCivs) {
			shown.tileKnowledge.ShowAlso(gameData.players.Where(p => !p.isBarbarians).Select(p => p.tileKnowledge));
		}
	}

	// Whether a spectator may look at the city screen and advisors as this
	// player: not when it watches as another civilization.
	public static bool SpectatorMayLookAs(Player player) {
		SpectatorViewInfo view = Client?.SpectatorView;
		return view == null || view.mode != SpectatorViewMode.OneCiv || view.playerID == player.id;
	}

	// True when this client took several seats, whose players take turns at
	// this machine as in a hotseat game.
	public static bool HasSeveralLocalPlayers => Client?.PlayerIDs.Count > 1;

	// True when the player plays at another machine, so they must be asked
	// over the network rather than at this screen.
	public static bool IsRemotePlayer(Player player) {
		return IsActive && player.isHuman && !IsLocalPlayer(player);
	}

	public static void End() {
		Host?.Dispose();
		Client?.Dispose();
		Host = null;
		Client = null;
		EngineStorage.ResetNetworking();
	}
}

public record PendingLanGame(GameSetup setup, C7GameData.Save.SaveGame save, int guestSeats);

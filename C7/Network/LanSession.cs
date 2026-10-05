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

	// The name this player goes by on the LAN.
	public static string PlayerName = System.Environment.UserName;

	// Developer options for trying LAN games from the command line, after "--":
	//   --lan-host=<save>     host the saved game, starting once every seat is taken
	//   --lan-join=<address>  join the host there and take the first open seat
	//   --lan-autoplay        end each of this machine's turns soon after it starts
	public static readonly string DevHostSave;
	public static readonly string DevJoinAddress;
	public static readonly bool DevAutoplay;
	private static bool devStartUsed = false;

	static LanSession() {
		foreach (string arg in OS.GetCmdlineUserArgs()) {
			if (arg.StartsWith("--lan-host=")) {
				DevHostSave = arg["--lan-host=".Length..];
			} else if (arg.StartsWith("--lan-join=")) {
				DevJoinAddress = arg["--lan-join=".Length..];
			} else if (arg == "--lan-autoplay") {
				DevAutoplay = true;
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

	public static void BeginHosting(LanHost host) {
		End();
		Host = host;
	}

	public static void BeginJoining(LanClient client) {
		End();
		Client = client;
		EngineStorage.remoteEngine = client.SendCommand;
	}

	// True when the player plays at another machine, so they must be asked
	// over the network rather than at this screen.
	public static bool IsRemotePlayer(Player player) {
		return IsActive && player.isHuman && player.id != EngineStorage.uiControllerID;
	}

	public static void End() {
		Host?.Dispose();
		Client?.Dispose();
		Host = null;
		Client = null;
		EngineStorage.ResetNetworking();
	}
}

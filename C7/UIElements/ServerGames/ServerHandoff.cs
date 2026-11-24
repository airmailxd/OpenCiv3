using System;
using System.Linq;
using C7Engine.Network;
using C7GameServer;
using Godot;
using Serilog;

// Server games handed to us by another program that starts this one (a hub
// the player has logged in to already), from the command line, after "--":
//   --server=<url>      the game server for this run, in place of the one in
//                       the settings, which isn't changed
//   --server-game=<id>  join that game at once (Play or View, as the server
//                       says), with no menu; leaving it, or a join that fails
//                       or is given up on, quits the game
//   --server-games      open the Server Games screen at once
// and in the environment C7_SERVER_USER and C7_SERVER_TOKEN, the session to
// use. Without --server= the others are ignored. For the run, GameServerSettings
// takes the server and the session from here, and nothing of them is saved in
// C7.ini or written to the log (see doc/server-games.md).
public static class ServerHandoff {
	private static readonly ILogger log = Log.ForContext(typeof(ServerHandoff));

	public static readonly bool Active;
	public static readonly string GameId;
	public static readonly bool OpenGames;
	private static bool startUsed = false;

	// The server and the session for this run: those given, or those the
	// player chose or logged in with on the Server Games screen since.
	public static string Url { get; private set; }
	private static string sessionServer;
	private static Session session;

	// The username given or last logged in with, to fill in when logging in.
	public static string Username { get; private set; } = "";

	static ServerHandoff() {
		foreach (string arg in OS.GetCmdlineUserArgs()) {
			if (arg.StartsWith("--server=")) {
				Url = GameServerSettings.Tidy(arg["--server=".Length..]);
				Active = true;
			} else if (arg.StartsWith("--server-game=")) {
				GameId = arg["--server-game=".Length..].Trim();
			} else if (arg == "--server-games") {
				OpenGames = true;
			}
		}
		if (!Active) {
			return;
		}
		string username = System.Environment.GetEnvironmentVariable("C7_SERVER_USER");
		string token = System.Environment.GetEnvironmentVariable("C7_SERVER_TOKEN");
		if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(token)) {
			KeepSession(Url, new Session(username, token));
		}
		log.Information("Started for the game server {Url}, {Session}", Url, session != null ? $"as {session.Username}" : "with no session");
	}

	// True the first time the main menu asks, if the game is to go straight
	// into a server game.
	public static bool TakeGameStart() {
		if (startUsed || !Active || string.IsNullOrEmpty(GameId)) {
			return false;
		}
		startUsed = true;
		return true;
	}

	// True the first time the main menu asks, if it's to open Server Games.
	public static bool TakeGamesStart() {
		if (startUsed || !Active || !OpenGames) {
			return false;
		}
		startUsed = true;
		return true;
	}

	public static void SetUrl(string url) {
		Url = url;
		if (!GameServerSettings.SameServer(sessionServer, url)) {
			session = null;
		}
	}

	public static Session SessionOn(string url) {
		return session != null && GameServerSettings.SameServer(sessionServer, url) ? session : null;
	}

	public static void KeepSession(string url, Session kept) {
		sessionServer = GameServerSettings.Tidy(url);
		session = kept;
		Username = kept.Username;
	}

	public static void ForgetSession() {
		session = null;
	}

	// Joins the game given, once it's found among the player's games (which
	// say whether to Play or View it). Whatever stops it joining is shown,
	// and then the game quits, as it does when the player leaves the game.
	public static async void JoinGame(Node parent) {
		AcceptDialog dialog = new() {
			Title = "Server Game",
			DialogText = "Connecting to the game server...",
			OkButtonText = "Cancel",
			Exclusive = true,
		};
		dialog.Confirmed += Quit;
		dialog.Canceled += Quit;
		parent.AddChild(dialog);
		dialog.PopupCentered();

		log.Information("Joining server game {Id}", GameId);
		GameSummary game;
		try {
			MyGames mine = await ServerGames.Client().MyGamesAsync();
			game = (mine.YourTurn ?? []).Concat(mine.OthersTurn ?? []).FirstOrDefault(g => g.Id == GameId);
			if (game == null) {
				throw new GameServerException("That game isn't one of yours on the game server.");
			}
		} catch (Exception e) {
			log.Warning(e, "Couldn't find server game {Id}", GameId);
			dialog.Title = "Couldn't connect";
			dialog.DialogText = $"Couldn't join the game: {e.Message}";
			dialog.OkButtonText = "OK";
			return;
		}
		if (!GodotObject.IsInstanceValid(dialog) || !dialog.Visible) {
			return;
		}
		dialog.Confirmed -= Quit;
		dialog.Canceled -= Quit;
		// Hidden now, not at the frame's end when it's freed: the joining
		// dialog that follows is exclusive too, and a window can have only
		// one exclusive child showing.
		dialog.Hide();
		dialog.QueueFree();

		// ServerGameJoining says why if it can't join, and goes; and the game
		// scene goes when the player leaves the game.
		SceneTree tree = parent.GetTree();
		tree.Root.ChildExitingTree += node => {
			if ((node is ServerGameJoining && !ServerGames.InServerGame) || node is Game) {
				Quit();
			}
		};
		ServerGames.Join(game);
	}

	private static void Quit() {
		log.Information("Done with the server game handed to us: quitting, as the program that started us waits for that");
		(Engine.GetMainLoop() as SceneTree)?.Quit();
	}
}

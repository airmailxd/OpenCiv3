using System.Linq;
using C7Engine.Network;
using C7GameServer;
using Godot;
using Serilog;

// Server games (see doc/server-games.md): games kept on a game server, which
// players come back to over days or weeks. The screens are ServerGamesScreen
// (log in, My Games, Open Games) over the main menu, and the Multiplayer
// Setup screen for a new one (MultiplayerSetupScreen.ServerGame).
public static class ServerGames {
	private static readonly ILogger log = Log.ForContext(typeof(ServerGames));

	// Show the Server Games screen when the main menu next shows: on coming
	// back from setting up a server game, whether it was made or not.
	public static bool OpenOnMenu;

	// The client for the game server chosen in the settings, with the session
	// last logged in with there, if any. Throws a GameServerException if no
	// server is chosen or its address isn't one.
	public static GameServerClient Client() {
		string url = GameServerSettings.Url;
		if (GameServerSettings.Problem(url) is string problem) {
			throw new GameServerException(problem);
		}
		return new GameServerClient(GameServerSettings.Tidy(url), GameServerSettings.SavedSession(url));
	}

	// The connection to the server game last joined (see ServerGameJoining).
	public static LanClient JoinedClient;

	// Whether the game being played is a server game: the one joined is the
	// one being played still.
	public static bool InServerGame => JoinedClient != null && LanSession.Client == JoinedClient;

	// Whether the player is to move in the game, so plays rather than views.
	public static bool YourTurn(GameSummary game) {
		return !game.WaitingForPlayers && (game.ToMove?.Any(s => s.PlayerId == game.YourPlayerId) ?? false);
	}

	// Play or View: connects to the game, to take a turn if it's the
	// player's (YourTurn) or else to look at their civilization. Either way
	// the player joins their own seat, and the server refuses their commands
	// until it's their turn, so a viewer whose turn comes plays on without
	// joining again (see doc/server-games.md).
	public static void Join(GameSummary game) {
		if (Engine.GetMainLoop() is SceneTree tree) {
			ServerGameJoining.Begin(tree, game);
		}
	}
}

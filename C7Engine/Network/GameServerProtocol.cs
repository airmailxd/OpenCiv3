using System;
using System.Collections.Generic;
using System.Text.Json;

namespace C7GameServer;

// What the game and an OpenCiv3 game server say to each other over HTTP. The
// server, which is kept in a repository of its own, builds with this engine
// and takes the protocol from here, so it uses nothing beyond the base
// library. See doc/server-games.md.
//
// Requests and answers are JSON. Signing up (POST AccountsPath) or logging
// in (POST SessionsPath) with Credentials answers with a Session, whose token
// the game then sends with each request as "Authorization: Bearer <token>".
// A request the server refuses is answered with a ServerError saying why,
// for the game to show the player.
//
// A host uploads a game (POST GamesPath) as a form: its title, an optional
// password, the ID of the human player the host plays, and the save as the
// file "save". Each human player in the save is a seat; the host's is theirs.
// Players claim the others (POST SeatPath) with ClaimSeat, from the games
// with seats free (GET OpenGamesPath). Once every seat is claimed the game
// begins. GET MyGamesPath lists the player's games, theirs to play first.
//
// To play or view a game that has begun, a player connects to it (POST
// ConnectPath): the server starts the game's worker if it isn't running, and
// answers with a GameConnection, which says where to join the game through
// the relay and with what token, the seat's for this player alone.
public static class GameServerProtocol {
	// The version of this protocol. The game sends it as ?v= on every
	// request; a server that doesn't speak it says so.
	public const int Version = 1;

	public const string AccountsPath = "/api/v1/accounts";
	public const string SessionsPath = "/api/v1/sessions";
	public const string GamesPath = "/api/v1/games";
	public const string MyGamesPath = "/api/v1/games/mine";
	public const string OpenGamesPath = "/api/v1/games/open";
	public const string HealthPath = "/health";

	public static string ConnectPath(string gameId) {
		return $"{GamesPath}/{Uri.EscapeDataString(gameId)}/connect";
	}

	public static string SeatPath(string gameId, string playerId) {
		return $"{GamesPath}/{Uri.EscapeDataString(gameId)}/seats/{Uri.EscapeDataString(playerId)}";
	}

	// The form fields of an uploaded game.
	public const string TitleField = "title";
	public const string PasswordField = "password";
	public const string HostPlayerField = "hostPlayer";
	public const string SaveField = "save";

	public const int MinUsernameLength = 3;
	public const int MaxUsernameLength = 24;
	public const int MinPasswordLength = 8;
	public const int MaxPasswordLength = 256;
	public const int MaxTitleLength = 60;

	public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

	// The username as the server keeps it (trimmed), or null if it can't be
	// one: letters, digits, '_', '-' and '.', of a sensible length.
	public static string TidyUsername(string username) {
		string tidy = username?.Trim();
		if (tidy == null || tidy.Length < MinUsernameLength || tidy.Length > MaxUsernameLength) {
			return null;
		}
		foreach (char c in tidy) {
			if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.') {
				return null;
			}
		}
		return tidy;
	}

	// What's wrong with the password as a new one, or null if nothing.
	public static string PasswordProblem(string password) {
		if (password == null || password.Length < MinPasswordLength) {
			return $"A password needs at least {MinPasswordLength} characters.";
		}
		if (password.Length > MaxPasswordLength) {
			return $"A password can have at most {MaxPasswordLength} characters.";
		}
		return null;
	}

	// The title as the server keeps it, or null if it can't be one.
	public static string TidyTitle(string title) {
		string tidy = title?.Trim();
		if (string.IsNullOrEmpty(tidy) || tidy.Length > MaxTitleLength) {
			return null;
		}
		foreach (char c in tidy) {
			if (char.IsControl(c)) {
				return null;
			}
		}
		return tidy;
	}
}

public sealed record Credentials(string Username, string Password);

public sealed record Session(string Username, string Token);

public sealed record ServerError(string Error);

public sealed record ClaimSeat(string Password);

// A seat in a game: a human player, and the account that plays them, if
// claimed. Rating is that account's rating (MMR) in the game's rules set,
// rounded; null for a seat nobody has claimed, and from servers before it.
public sealed record SeatSummary(string PlayerId, string Civilization, string Leader, string Username, bool Defeated,
	int? Rating = null);

// A game as one of its players sees it in My Games.
//   Era: the player's own era, as its Civilopedia key (like
//     "ERAS_Ancient_Times"), for the game to show its icon.
//   ToMove: the seats whose turn it is; empty while the computer players
//     move, or while the game waits for players.
public sealed record GameSummary(string Id, string Title, string Host, int Turn, string Era,
	string YourPlayerId, string YourCivilization, bool WaitingForPlayers, List<SeatSummary> ToMove,
	List<SeatSummary> Seats, DateTimeOffset Updated);

public sealed record MyGames(List<GameSummary> YourTurn, List<GameSummary> OthersTurn);

// A game with seats free, as Open Games lists it.
public sealed record OpenGame(string Id, string Title, string Host, bool HasPassword, int Turn, List<SeatSummary> Seats);

public sealed record OpenGameList(List<OpenGame> Games);

// How to join a game being played on the server: through the relay at
// RelayUrl with JoinCode, saying hello with SeatToken, which has the game
// give the player their seat (PlayerId) and nothing else. Play is whether
// it's their turn now, so they join to play; otherwise they join to view the
// game as their civilization, and may act once it's their turn, without
// joining again. Simultaneous is whether this turn's humans move at once.
public sealed record GameConnection(string GameId, string Title, string RelayUrl, string JoinCode, string SeatToken,
	string PlayerId, bool Play, bool Simultaneous);

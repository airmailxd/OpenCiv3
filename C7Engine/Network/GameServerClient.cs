using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using C7GameServer;

namespace C7Engine.Network;

// Why the game server didn't do what the game asked, in words to show the
// player.
public sealed class GameServerException(string message, HttpStatusCode? status = null, Exception inner = null)
	: Exception(message, inner) {
	public HttpStatusCode? Status { get; } = status;

	// Whether the player needs to log in again.
	public bool SessionEnded => Status == HttpStatusCode.Unauthorized;
}

// A server game joined: how (see GameConnection), and the client in the
// player's seat.
public sealed record ServerGameJoin(GameConnection Connection, LanClient Client);

// The game's side of an online game server (see GameServerProtocol and
// doc/server-games.md): signing up and logging in, the player's games, and
// joining one to play or view it (JoinGameAsync). Every call throws a
// GameServerException when the server can't be reached or refuses.
public sealed class GameServerClient {
	private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };

	public Uri ServerUrl { get; }

	// The session the calls are made with, once signed up or logged in.
	public Session Session { get; private set; }

	public GameServerClient(string serverUrl, Session session = null) {
		if (!Uri.TryCreate(serverUrl?.Trim(), UriKind.Absolute, out Uri uri) || (uri.Scheme != "https" && uri.Scheme != "http")) {
			throw new GameServerException($"\"{serverUrl}\" isn't the address of a game server.");
		}
		ServerUrl = uri;
		Session = session;
	}

	public async Task<Session> SignUpAsync(string username, string password, CancellationToken cancel = default) {
		Session = await Send<Session>(HttpMethod.Post, GameServerProtocol.AccountsPath, JsonContent(new Credentials(username, password)), cancel, signedIn: false);
		return Session;
	}

	public async Task<Session> LogInAsync(string username, string password, CancellationToken cancel = default) {
		Session = await Send<Session>(HttpMethod.Post, GameServerProtocol.SessionsPath, JsonContent(new Credentials(username, password)), cancel, signedIn: false);
		return Session;
	}

	public async Task LogOutAsync(CancellationToken cancel = default) {
		if (Session != null) {
			await Send<object>(HttpMethod.Delete, GameServerProtocol.SessionsPath, null, cancel);
			Session = null;
		}
	}

	public Task<MyGames> MyGamesAsync(CancellationToken cancel = default) {
		return Send<MyGames>(HttpMethod.Get, GameServerProtocol.MyGamesPath, null, cancel);
	}

	public async Task<List<OpenGame>> OpenGamesAsync(CancellationToken cancel = default) {
		return (await Send<OpenGameList>(HttpMethod.Get, GameServerProtocol.OpenGamesPath, null, cancel)).Games ?? [];
	}

	// Puts the game on the server, with the host playing hostPlayerId and the
	// game's other human players' seats free for others to claim.
	public Task<GameSummary> CreateGameAsync(string title, string password, string hostPlayerId, byte[] save, CancellationToken cancel = default) {
		MultipartFormDataContent form = new() {
			{ new StringContent(title ?? ""), GameServerProtocol.TitleField },
			{ new StringContent(password ?? ""), GameServerProtocol.PasswordField },
			{ new StringContent(hostPlayerId ?? ""), GameServerProtocol.HostPlayerField },
		};
		ByteArrayContent file = new(save);
		file.Headers.ContentType = new MediaTypeHeaderValue("application/json");
		form.Add(file, GameServerProtocol.SaveField, "game.json");
		return Send<GameSummary>(HttpMethod.Post, GameServerProtocol.GamesPath, form, cancel);
	}

	public Task<GameSummary> ClaimSeatAsync(string gameId, string playerId, string password, CancellationToken cancel = default) {
		return Send<GameSummary>(HttpMethod.Post, GameServerProtocol.SeatPath(gameId, playerId), JsonContent(new ClaimSeat(password)), cancel);
	}

	// Where and how to join the game, with its worker started if it wasn't
	// running (see GameConnection). That can take a while, as the server
	// loads the game.
	public Task<GameConnection> ConnectAsync(string gameId, CancellationToken cancel = default) {
		return Send<GameConnection>(HttpMethod.Post, GameServerProtocol.ConnectPath(gameId), null, cancel);
	}

	// Joins the game through the relay into the player's seat: connected,
	// with the hello said, as LanClient.ConnectAsync leaves it. The host
	// gives the seat back at once, with the game, without a lobby (as for a
	// guest coming back to its seats); hand the client to LanSession as for
	// any game joined. With Play false the player views the game as their
	// civilization until it's their turn, when they may play on without
	// joining again. Throws a GameServerException if the server refuses,
	// and as LanClient.ConnectAsync does if the relay can't be reached.
	public async Task<ServerGameJoin> JoinGameAsync(string gameId, CancellationToken cancel = default) {
		GameConnection connection = await ConnectAsync(gameId, cancel).ConfigureAwait(false);
		LanClient client = await JoinAsync(connection, Session.Username, cancel).ConfigureAwait(false);
		return new ServerGameJoin(connection, client);
	}

	// Joins a game the server said how to join, as JoinGameAsync does.
	public static Task<LanClient> JoinAsync(GameConnection connection, string playerName, CancellationToken cancel = default) {
		return LanClient.ConnectAsync(new RelayEndpoint(connection.RelayUrl, connection.JoinCode), playerName, connection.SeatToken,
			cancel: cancel);
	}

	// The JSON is written and read with System.Text.Json itself rather than
	// System.Net.Http.Json's helpers: inside the game those bind to the
	// framework's System.Text.Json, not the newer one the game ships with, and
	// on a machine with only the .NET 8 runtime they fail as "method not found".
	private static HttpContent JsonContent<T>(T value) {
		ByteArrayContent content = new(JsonSerializer.SerializeToUtf8Bytes(value, GameServerProtocol.Json));
		content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
		return content;
	}

	private static async Task<T> ReadJson<T>(HttpContent content, CancellationToken cancel) {
		using System.IO.Stream stream = await content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
		return await JsonSerializer.DeserializeAsync<T>(stream, GameServerProtocol.Json, cancel).ConfigureAwait(false);
	}

	private async Task<T> Send<T>(HttpMethod method, string path, HttpContent content, CancellationToken cancel, bool signedIn = true) {
		if (signedIn && Session == null) {
			throw new GameServerException("Log in to the game server first.", HttpStatusCode.Unauthorized);
		}
		UriBuilder uri = new(ServerUrl) { Query = $"v={GameServerProtocol.Version}" };
		uri.Path = uri.Path.TrimEnd('/') + path;
		using HttpRequestMessage request = new(method, uri.Uri) { Content = content };
		if (signedIn) {
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.Token);
		}
		request.Headers.TryAddWithoutValidation(OpenCivVersion.Header, OpenCivVersion.Sent);
		try {
			using HttpResponseMessage response = await http.SendAsync(request, cancel).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode) {
				throw new GameServerException(await ErrorOf(response, cancel).ConfigureAwait(false), response.StatusCode);
			}
			if (response.StatusCode == HttpStatusCode.NoContent) {
				return default;
			}
			return await ReadJson<T>(response.Content, cancel).ConfigureAwait(false);
		} catch (OperationCanceledException) when (!cancel.IsCancellationRequested) {
			throw new GameServerException("The game server didn't answer in time.");
		} catch (HttpRequestException e) {
			throw new GameServerException($"Couldn't reach the game server: {e.Message}", null, e);
		} catch (JsonException e) {
			throw new GameServerException("The game server's answer couldn't be read.", null, e);
		}
	}

	// What the player is told when the server needs a newer OpenCiv (426)
	// and doesn't say so itself.
	public const string UpdateNeeded = "This version of OpenCiv is out of date. Update it in AirGames, then try again.";

	private static async Task<string> ErrorOf(HttpResponseMessage response, CancellationToken cancel) {
		if (response.StatusCode == HttpStatusCode.TooManyRequests) {
			return "Too many tries from your address. Wait a minute, then try again.";
		}
		try {
			ServerError error = await ReadJson<ServerError>(response.Content, cancel).ConfigureAwait(false);
			if (!string.IsNullOrWhiteSpace(error?.Error)) {
				return error.Error;
			}
		} catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException) {
		}
		if (response.StatusCode == HttpStatusCode.UpgradeRequired) {
			return UpdateNeeded;
		}
		return $"The game server refused ({(int)response.StatusCode}).";
	}
}

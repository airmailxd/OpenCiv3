using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7GameServer;

// Which game server server games are played on, and the session the player
// last logged in to it with, kept in C7.ini so they needn't log in each time
// the game starts (see doc/server-games.md):
//   [online]
//   gameServerUrl=https://games.example.org
//   [gameServer]
//   server=https://games.example.org
//   username=alice
//   token=...
// The token is kept in plain text, as the Advanced AI's API key is; it's a
// session the server can end, never the password, which isn't kept at all.
// In a run another program started with a server of its own, ServerHandoff
// keeps both instead, for that run only.
public static class GameServerSettings {
	public const string UrlSection = "online";
	public const string UrlSetting = "gameServerUrl";

	public const string SessionSection = "gameServer";
	private const string SessionServerKey = "server";
	private const string UsernameKey = "username";
	private const string TokenKey = "token";

	// The game server used unless the player chooses another: Roma's public
	// server, which Roma's hand-off also names.
	public const string DefaultUrl = "https://play.roma-online.net";

	// The game server chosen, or DefaultUrl if none is.
	public static string Url {
		get {
			if (ServerHandoff.Active) {
				return ServerHandoff.Url;
			}
			string url = C7Settings.GetSettingsValueOrDefault(UrlSection, UrlSetting, null);
			return string.IsNullOrWhiteSpace(url) ? DefaultUrl : url.Trim();
		}
	}

	// Chooses the game server to use from now on; blank goes back to the
	// default. A session on another server is forgotten, as it's no good on
	// this one.
	public static void SetUrl(string url) {
		string tidy = Tidy(url);
		if (ServerHandoff.Active) {
			ServerHandoff.SetUrl(tidy);
			return;
		}
		if (tidy == "" || SameServer(tidy, DefaultUrl)) {
			C7Settings.RemoveValue(UrlSection, UrlSetting);
		} else {
			C7Settings.SetValue(UrlSection, UrlSetting, tidy);
		}
		if (!SameServer(C7Settings.GetSettingsValueOrDefault(SessionSection, SessionServerKey, null), Url)) {
			RemoveSession();
		}
		C7Settings.SaveSettings();
	}

	// The address as typed, with https:// put in front if no scheme was
	// given, as players will mostly type just the server's name.
	public static string Tidy(string url) {
		string tidy = url?.Trim() ?? "";
		if (tidy != "" && !tidy.Contains("://")) {
			tidy = "https://" + tidy;
		}
		return tidy.TrimEnd('/');
	}

	// Why the server can't be used, to tell the player, or null if it may.
	public static string Problem(string url) {
		string tidy = Tidy(url);
		if (tidy == "") {
			return "No game server is chosen yet. Type the address you were given, like games.example.org.";
		}
		if (!Uri.TryCreate(tidy, UriKind.Absolute, out Uri uri) || (uri.Scheme != "https" && uri.Scheme != "http")) {
			return $"\"{url.Trim()}\" isn't a game server's address. Type one like games.example.org.";
		}
		return null;
	}

	public static bool SameServer(string a, string b) {
		return Uri.TryCreate(Tidy(a), UriKind.Absolute, out Uri ua) && Uri.TryCreate(Tidy(b), UriKind.Absolute, out Uri ub) && ua == ub;
	}

	// The session last logged in with on this server, or null.
	public static Session SavedSession(string url) {
		if (ServerHandoff.Active) {
			return ServerHandoff.SessionOn(url);
		}
		string server = C7Settings.GetSettingsValueOrDefault(SessionSection, SessionServerKey, null);
		string username = C7Settings.GetSettingsValueOrDefault(SessionSection, UsernameKey, null);
		string token = C7Settings.GetSettingsValueOrDefault(SessionSection, TokenKey, null);
		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(token) || !SameServer(server, url)) {
			return null;
		}
		return new Session(username, token);
	}

	// The username last logged in with, to fill in when logging in again.
	public static string LastUsername => ServerHandoff.Active ? ServerHandoff.Username : C7Settings.GetSettingsValueOrDefault(SessionSection, UsernameKey, "");

	public static void SaveSession(string url, Session session) {
		if (ServerHandoff.Active) {
			ServerHandoff.KeepSession(url, session);
			return;
		}
		C7Settings.SetValue(SessionSection, SessionServerKey, Tidy(url));
		C7Settings.SetValue(SessionSection, UsernameKey, session.Username);
		C7Settings.SetValue(SessionSection, TokenKey, session.Token);
		C7Settings.SaveSettings();
	}

	// Forgets the session, keeping the username to log in with again.
	public static void ForgetSession() {
		if (ServerHandoff.Active) {
			ServerHandoff.ForgetSession();
			return;
		}
		RemoveSession();
		C7Settings.SaveSettings();
	}

	private static void RemoveSession() {
		C7Settings.RemoveValue(SessionSection, SessionServerKey);
		C7Settings.RemoveValue(SessionSection, TokenKey);
	}

	// Whether a game server answers, for the player to check the address
	// they chose: null if it does, or what went wrong.
	public static async Task<string> CheckAsync(string url, CancellationToken cancel = default) {
		if (Problem(url) is string problem) {
			return problem;
		}
		UriBuilder health = new(Tidy(url));
		health.Path = health.Path.TrimEnd('/') + GameServerProtocol.HealthPath;
		try {
			using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };
			using HttpResponseMessage response = await http.GetAsync(health.Uri, cancel);
			return response.IsSuccessStatusCode ? null : $"The server answered, but not as a game server ({(int)response.StatusCode}).";
		} catch (OperationCanceledException) when (!cancel.IsCancellationRequested) {
			return "The server didn't answer in time.";
		} catch (HttpRequestException e) {
			return $"Couldn't reach the server: {e.Message}";
		}
	}
}

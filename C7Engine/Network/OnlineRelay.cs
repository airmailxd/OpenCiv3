using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using C7Relay;

namespace C7Engine.Network;

// Which online relay players host and join games through, by join code. It's
// chosen in the game's Settings, or by the host when hosting online, and
// kept in C7.ini:
//   [online]
//   relayUrl=wss://relay.example.org
public static class OnlineRelay {
	// The relay used unless one is chosen. This is a placeholder: set it to a
	// running relay (see Relay/README.md) for everyone to use it.
	public const string DefaultUrl = "wss://relay.example.com";

	public const string SettingsSection = "online";
	public const string UrlSetting = "relayUrl";

	public static string Url {
		get {
			string url = C7Settings.GetSettingsValueOrDefault(SettingsSection, UrlSetting, null);
			return string.IsNullOrWhiteSpace(url) ? DefaultUrl : url.Trim();
		}
	}

	// Chooses the relay to use from now on; a blank one goes back to the
	// default.
	public static void SetUrl(string url) {
		if (string.IsNullOrWhiteSpace(url) || SameRelay(url, DefaultUrl)) {
			C7Settings.RemoveValue(SettingsSection, UrlSetting);
		} else {
			C7Settings.SetValue(SettingsSection, UrlSetting, url.Trim());
		}
		C7Settings.SaveSettings();
	}

	// Why the relay can't be used, to tell the player, or null if it may.
	public static string Problem(string url) {
		if (RelayConnection.BaseUri(url) is not Uri uri) {
			return $"\"{url}\" isn't a relay server's address. Choose one like relay.example.org in Settings, or when hosting online.";
		}
		if (uri.Host == "relay.example.com") {
			return "No online relay server is chosen yet. Choose one in Settings, or when hosting online.";
		}
		return null;
	}

	public static bool SameRelay(string a, string b) {
		return RelayConnection.BaseUri(a) is Uri ua && RelayConnection.BaseUri(b) is Uri ub && ua == ub;
	}

	// What a host gives players to join with: the code alone when the game
	// is on the default relay, and otherwise the code and the relay, like
	// KQ7-4MZ@relay.example.org, so that players needn't choose it first.
	public static string Invite(string formattedCode, string relayUrl) {
		if (formattedCode == null) {
			return null;
		}
		if (SameRelay(relayUrl, DefaultUrl) || RelayConnection.BaseUri(relayUrl) is not Uri uri) {
			return formattedCode;
		}
		// wss is assumed when none is given, so only ws needs saying.
		string scheme = uri.Scheme == "ws" ? "ws://" : "";
		string path = uri.AbsolutePath.TrimEnd('/');
		return $"{formattedCode}@{scheme}{uri.Authority}{path}";
	}

	// Reads what a player typed to join: a code, or a code and a relay as
	// Invite gives them. Returns false if the code isn't one. Without a
	// relay, the one chosen in the settings is used.
	public static bool TryParseInvite(string typed, out string code, out string relayUrl) {
		relayUrl = Url;
		code = null;
		if (string.IsNullOrWhiteSpace(typed)) {
			return false;
		}
		string text = typed.Trim();
		int at = text.IndexOf('@');
		if (at >= 0) {
			string relay = text[(at + 1)..].Trim();
			if (relay != "") {
				relayUrl = relay;
			}
			text = text[..at];
		}
		code = RelayProtocol.NormalizeCode(text);
		return code != null;
	}

	// Whether a relay answers, for the player to check the address they
	// chose: null if it does, or what went wrong.
	public static async Task<string> CheckAsync(string url, CancellationToken cancel = default) {
		if (Problem(url) is string problem) {
			return problem;
		}
		Uri health = PublicGames.HttpUri(url, RelayProtocol.HealthPath);
		try {
			using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };
			using HttpResponseMessage response = await http.GetAsync(health, cancel);

			return response.IsSuccessStatusCode ? null : $"The server answered, but not as a relay ({(int)response.StatusCode}).";
		} catch (OperationCanceledException) when (!cancel.IsCancellationRequested) {
			return "The server didn't answer in time.";
		} catch (HttpRequestException e) {
			return $"Couldn't reach the server: {e.Message}";
		}
	}
}

using System;

namespace C7Engine.Network;

// Which online relay players host and join games through, by join code. It
// can be changed in C7.ini:
//   [online]
//   relayUrl=wss://relay.example.org
public static class OnlineRelay {
	// The relay used unless C7.ini says otherwise. This is a placeholder:
	// set it to a running relay (see Relay/README.md) for everyone to use it.
	public const string DefaultUrl = "wss://relay.example.com";

	public const string SettingsSection = "online";
	public const string UrlSetting = "relayUrl";

	public static string Url {
		get {
			string url = C7Settings.GetSettingsValueOrDefault(SettingsSection, UrlSetting, null);
			return string.IsNullOrWhiteSpace(url) ? DefaultUrl : url.Trim();
		}
	}

	// Why the relay can't be used, to tell the player, or null if it may.
	public static string Problem(string url) {
		if (RelayConnection.BaseUri(url) is not Uri uri) {
			return $"The relay address \"{url}\" isn't valid. Set relayUrl under [online] in C7.ini to one like wss://relay.example.org.";
		}
		if (uri.Host == "relay.example.com") {
			return "No online relay is set up yet. Set relayUrl under [online] in C7.ini to a relay's address (see Relay/README.md).";
		}
		return null;
	}
}

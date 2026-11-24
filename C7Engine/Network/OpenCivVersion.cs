using System;
using System.IO;
using System.Reflection;

namespace C7GameServer;

// The version of OpenCiv this is: the one line of C7Engine/VERSION, which a
// release bumps before it's tagged (the tag is "v" and the same number; see
// .github/workflows/readme.md). It's built into the engine, so the game and
// a game server built with this engine both know it, and the game sends it
// (Header) with every request to a game server, which refuses an older one.
public static class OpenCivVersion {
	// The request header the game sends its version in.
	public const string Header = "X-OpenCiv-Version";

	// The name VERSION is built into the engine under (see C7Engine.csproj).
	public const string ResourceName = "OpenCiv.VERSION";

	// What a build that isn't a release (run from the editor) adds to the
	// version it sends, so a server can tell it from the release.
	public const string DevelopmentSuffix = "-dev";

	// This version, like "0.16.1"; "dev" if the engine was built without it.
	public static string Current { get; } = Read(typeof(OpenCivVersion).Assembly) ?? "dev";

	// Whether this is a development build rather than a release: the game
	// sets it when it's run from the editor rather than exported.
	public static bool Development { get; set; }

	// The version the game sends a game server.
	public static string Sent => Development ? Current + DevelopmentSuffix : Current;

	// The version built into an assembly, or null if it has none.
	public static string Read(Assembly assembly) {
		using Stream stream = assembly.GetManifestResourceStream(ResourceName);
		if (stream == null) {
			return null;
		}
		using StreamReader reader = new(stream);
		string version = reader.ReadToEnd().Trim();
		return version.Length == 0 ? null : version;
	}

	// A release's version ("0.16.1") as numbers to compare, or null if it
	// isn't one (missing, "dev", or a development build's "0.16.1-dev").
	public static Version Parse(string version) {
		string[] parts = version?.Trim().Split('.');
		if (parts == null || parts.Length != 3) {
			return null;
		}
		int[] numbers = new int[3];
		for (int i = 0; i < 3; i++) {
			if (parts[i].Length == 0 || parts[i].Length > 6 || !int.TryParse(parts[i], System.Globalization.NumberStyles.None,
					System.Globalization.CultureInfo.InvariantCulture, out numbers[i])) {
				return null;
			}
		}
		return new Version(numbers[0], numbers[1], numbers[2]);
	}
}

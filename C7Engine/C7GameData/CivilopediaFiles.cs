using System;
using System.IO;

namespace C7GameData {

	// Where the Civilopedia's files come from. OpenCiv3 ships a Civilopedia
	// of its own in a Civilopedia folder laid out like a Civ3 install
	// (Text/Civilopedia.txt, Text/PediaIcons.txt, Art/Civilopedia/...), so it
	// works without Civ3. Standalone mode reads ours; otherwise Civ3's files
	// come first, and ours fill in for any that are missing.
	public static class CivilopediaFiles {
		// A file found for a media path, and whether it's one of ours.
		public readonly record struct Found(string Path, bool IsOurs);

		// The path in our folder of a media path. Our art is PNG with real
		// alpha, so Art/Civilopedia/civilopediaA.pcx is
		// Art/Civilopedia/civilopediaA.png there. Paths in our own
		// PediaIcons.txt already name our files, and stay as they are.
		public static string OurPath(string mediaPath) {
			if (string.IsNullOrWhiteSpace(mediaPath)) {
				return null;
			}
			string path = mediaPath.Trim().Replace('\\', '/');
			if (Path.GetExtension(path).Equals(".pcx", StringComparison.OrdinalIgnoreCase)) {
				path = Path.ChangeExtension(path, ".png");
			}
			return path;
		}

		// Finds the file for a media path, like Text/Civilopedia.txt or
		// Art/Civilopedia/bar-red.pcx, or returns null if there's none.
		// inCiv3 looks a media path up the way the rest of the game's media
		// is found (Civ3's folders, or in standalone mode C7's own assets) and
		// inOurs looks a path up in our folder; both return null for a file
		// that isn't there.
		public static Found? Find(string mediaPath, bool standalone, Func<string, string> inCiv3, Func<string, string> inOurs) {
			if (string.IsNullOrWhiteSpace(mediaPath)) {
				return null;
			}
			string civ3Path = mediaPath.Trim().Replace('\\', '/');
			if (!standalone && inCiv3(civ3Path) is string civ3) {
				return new Found(civ3, false);
			}
			if (inOurs(OurPath(civ3Path)) is string ours) {
				return new Found(ours, true);
			}
			// In standalone mode, art we don't have yet may still be among
			// C7's own assets (like the ruleset's placeholder unit icons).
			if (standalone && inCiv3(civ3Path) is string asset) {
				return new Found(asset, false);
			}
			return null;
		}
	}
}

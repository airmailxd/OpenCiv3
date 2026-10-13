using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// In standalone mode the art comes from C7's assets (the C7/Assets
// submodule) and the remade art of C7/ModernArt. The standalone texture config
// lists the art it takes from there in place of Civ3's; art missing from both
// throws when it's loaded. The advisor faces and the war and peace icons
// once were only on one machine, so the release's advisor screens, all but
// the Domestic advisor's, failed to open (owner's report, 2026-10-12).
public class StandaloneArtTest {
	// Art the game draws itself when it's missing: the faces of the advisors'
	// sidebar (AdvisorUtils) and the Military advisor's war and peace icons.
	private static readonly string[] drawnWhenMissing = [
		"Art/SmallHeads/advisor_tab.png",
		"Art/Advisors/militaryNAV.png",
	];

	[Fact]
	public void TheArtStandaloneModeListsIsThere() {
		string c7 = PathUtils.getBasePath("../C7");
		string assets = Path.Combine(c7, "Assets");
		// The submodule isn't checked out everywhere the tests run.
		if (!Directory.Exists(Path.Combine(assets, "Art"))) {
			return;
		}

		string config = File.ReadAllText(Path.Combine(c7, "Lua", "standalone", "textures.lua"));
		Match list = Regex.Match(config, @"local c7_texture_list = \{(.*?)\}", RegexOptions.Singleline);
		Assert.True(list.Success, "The standalone texture config has no c7_texture_list");
		string[] paths = Regex.Matches(list.Groups[1].Value, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
		Assert.NotEmpty(paths);

		string[] missing = paths
			.Where(p => !drawnWhenMissing.Contains(p))
			.Where(p => !ExistsIgnoringCase(assets, p) && !ExistsIgnoringCase(Path.Combine(c7, "ModernArt"), p))
			.ToArray();
		Assert.Empty(missing);
	}

	// Whether a file is under a folder, ignoring the case of its path, as
	// the game finds media.
	private static bool ExistsIgnoringCase(string root, string relPath) {
		string current = root;
		foreach (string part in relPath.Split('/')) {
			if (!Directory.Exists(current)) {
				return false;
			}
			string match = Directory.EnumerateFileSystemEntries(current)
				.FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, System.StringComparison.OrdinalIgnoreCase));
			if (match == null) {
				return false;
			}
			current = match;
		}
		return File.Exists(current);
	}
}

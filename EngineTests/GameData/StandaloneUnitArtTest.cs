using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Standalone mode keeps every unit of the bundled rules and draws them with
// our own art (C7/ModernArt/Art/Units/<art>): sheets, the metadata that
// stands in for Civ3's INI and flics, and a thumbnail. Their Civilopedia
// icons are in C7/Civilopedia.
public class StandaloneUnitArtTest {
	private static string UnitsFolder => PathUtils.getBasePath("../C7/ModernArt/Art/Units/");
	private static string CivilopediaFolder => PathUtils.getBasePath("../C7/Civilopedia/");

	// The actions whose sheets must be committed: what's shown with
	// animations off (standing, fortified, workers at work), the move cycle
	// and what combat plays (the attack and the death with every frame, the
	// victory with every other). See ArtGen/units/flic.py.
	private static readonly string[] StillActions = ["DEFAULT", "FORTIFY"];
	private static readonly string[] LoopActions = ["ROAD", "MINE", "IRRIGATE", "FORTRESS", "JUNGLE", "FOREST", "PLANT"];
	private const string MoveAction = "RUN";
	private static readonly string[] WholeCombatActions = ["ATTACK1", "DEATH"];
	private const string VictoryAction = "VICTORY";

	private static readonly Lazy<SaveGame> standaloneRules = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3", ["standalone"])).GetSave());
	private static readonly Lazy<SaveGame> basicRules = new(() => SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave());

	// A file or folder under a folder, found ignoring case, as the game does.
	private static string FindIgnoringCase(string root, string relative) {
		string path = root;
		foreach (string part in relative.Split('/', '\\')) {
			if (!Directory.Exists(path)) {
				return null;
			}
			string match = Directory.EnumerateFileSystemEntries(path)
				.FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, StringComparison.OrdinalIgnoreCase));
			if (match == null) {
				return null;
			}
			path = match;
		}
		return path;
	}

	private static (int, int) PngSize(string path) {
		byte[] header = new byte[24];
		using (FileStream stream = File.OpenRead(path)) {
			Assert.Equal(24, stream.Read(header, 0, 24));
		}
		Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, header[0..4]);
		int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
		int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
		return (width, height);
	}

	// The art names the standalone rules draw units with.
	private static IEnumerable<(string unit, string art)> ArtNames() {
		foreach (SaveUnitPrototype proto in standaloneRules.Value.UnitPrototypes) {
			yield return (proto.name, proto.art.mainArt.defaultName);
			foreach (string art in proto.art.mainArt.variations?.Values ?? Enumerable.Empty<string>()) {
				yield return (proto.name, art);
			}
		}
	}

	[Fact]
	public void StandaloneKeepsEveryUnitAndItsUpgrades() {
		List<SaveUnitPrototype> basic = basicRules.Value.UnitPrototypes;
		List<SaveUnitPrototype> standalone = standaloneRules.Value.UnitPrototypes;
		Assert.Equal(basic.Select(p => p.name), standalone.Select(p => p.name));
		foreach ((SaveUnitPrototype b, SaveUnitPrototype s) in basic.Zip(standalone)) {
			Assert.Equal(b.upgradesTo ?? [], s.upgradesTo ?? []);
			Assert.Equal(b.art.mainArt.defaultName, s.art.mainArt.defaultName);
			Assert.Equal(b.art.mainArt.variations, s.art.mainArt.variations);
		}
	}

	[Fact]
	public void EveryUnitArtOfTheStandaloneRulesHasItsMetadataSheetsAndThumbnail() {
		List<string> problems = new();
		HashSet<string> checkedArt = new(StringComparer.OrdinalIgnoreCase);
		foreach ((string unit, string art) in ArtNames()) {
			if (!checkedArt.Add(art)) {
				continue;
			}
			string folder = FindIgnoringCase(UnitsFolder, art);
			if (folder == null) {
				problems.Add($"{unit}: no art folder {art}");
				continue;
			}
			string metadataPath = FindIgnoringCase(folder, art + ".json");
			if (metadataPath == null) {
				problems.Add($"{unit}: no metadata {art}/{art}.json");
				continue;
			}
			UnitArtMetadata metadata;
			try {
				metadata = UnitArtMetadata.Load(metadataPath);
			} catch (FormatException e) {
				problems.Add($"{unit}: {e.Message}");
				continue;
			}

			foreach ((string action, string flicName) in metadata.Actions) {
				string upper = action.ToUpperInvariant();
				bool still = StillActions.Contains(upper);
				bool loop = LoopActions.Contains(upper);
				bool move = upper == MoveAction || upper == VictoryAction;
				bool whole = WholeCombatActions.Contains(upper);
				if (!still && !loop && !move && !whole) {
					continue; // only made with --animations, not committed
				}
				UnitArtMetadata.Flic flic = metadata.GetFlic(flicName);
				int cellWidth = flic.Width * 2, cellHeight = flic.Height * 2;
				// A still (one column, the remastered units' one pose) stands for every
				// frame; an animated sheet may have fewer columns than the flic has
				// frames (the remastered animations render at most 8), which
				// UnitArtMetadata.SheetColumn spreads over the frames.
				int[] columns = still ? [1] : Enumerable.Range(1, Math.Max(1, flic.Frames)).ToArray();
				foreach (string suffix in new[] { "", "_tint" }) {
					string sheet = FindIgnoringCase(folder, flicName + suffix + ".png");
					if (sheet == null) {
						// A unit with nothing in its civ's color has no tint sheet.
						if (suffix == "") {
							problems.Add($"{unit}: no {action} sheet {art}/{flicName}.png");
						}
						continue;
					}
					(int width, int height) = PngSize(sheet);
					if (height != flic.Directions * cellHeight || !columns.Any(c => width == c * cellWidth)) {
						problems.Add($"{unit}: {art}/{Path.GetFileName(sheet)} is {width}x{height}, expected "
							+ $"{string.Join(" or ", columns.Select(c => c * cellWidth))}x{flic.Directions * cellHeight}");
					}
				}
			}
			foreach (string action in new[] { MoveAction, "DEATH" }) {
				if (metadata.FlicNameOf(action) == null) {
					problems.Add($"{unit}: {art} has no {action} animation");
				}
			}

			string thumbnail = FindIgnoringCase(folder, art + "_32.png");
			if (thumbnail == null) {
				problems.Add($"{unit}: no thumbnail {art}/{art}_32.png");
			} else if (PngSize(thumbnail) != (64, 64)) {
				problems.Add($"{unit}: thumbnail {art}/{art}_32.png is not 64x64");
			}
		}
		Assert.True(checkedArt.Count > 100, $"only {checkedArt.Count} unit arts");
		Assert.True(problems.Count == 0, string.Join("\n", problems));
	}

	[Fact]
	public void EveryUnitOfTheStandaloneRulesHasItsCivilopediaIcons() {
		List<string> problems = new();
		foreach (SaveUnitPrototype proto in standaloneRules.Value.UnitPrototypes) {
			foreach ((string path, int size) in new[] { (proto.art.pediaArt.large, 128), (proto.art.pediaArt.small, 32) }) {
				string file = path == null ? null : FindIgnoringCase(CivilopediaFolder, path);
				if (file == null) {
					problems.Add($"{proto.name}: no Civilopedia icon {path}");
				} else if (PngSize(file) != (size, size)) {
					problems.Add($"{proto.name}: {path} is not {size}x{size}");
				}
			}
		}
		Assert.True(problems.Count == 0, string.Join("\n", problems));
	}
}

using System;
using System.Collections.Generic;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Finding the Civilopedia's files in Civ3 or in OpenCiv3's own Civilopedia
// folder.
public class CivilopediaFilesTest {
	// Lookups that find the paths they're given in a set of files.
	private static Func<string, string> In(string root, params string[] files) {
		HashSet<string> set = new(files, StringComparer.OrdinalIgnoreCase);
		return path => set.Contains(path) ? $"{root}/{path}" : null;
	}

	private static readonly Func<string, string> Nowhere = _ => null;

	[Theory]
	[InlineData("Art/Civilopedia/civilopediaA.pcx", "Art/Civilopedia/civilopediaA.png")]
	[InlineData("Art/Civilopedia/Icons/menu_red large.PCX", "Art/Civilopedia/Icons/menu_red large.png")]
	[InlineData(@"Art\Civilopedia\Icons\Units\Warrior_large.pcx", "Art/Civilopedia/Icons/Units/Warrior_large.png")]
	[InlineData("Art/Civilopedia/Icons/Units/Warrior_large.png", "Art/Civilopedia/Icons/Units/Warrior_large.png")]
	[InlineData(" Text/Civilopedia.txt ", "Text/Civilopedia.txt")]
	public void OurPathIsThePngOfTheSameName(string mediaPath, string expected) {
		Assert.Equal(expected, CivilopediaFiles.OurPath(mediaPath));
	}

	[Fact]
	public void NoPathFindsNothing() {
		Assert.Null(CivilopediaFiles.OurPath(null));
		Assert.Null(CivilopediaFiles.Find("", false, In("civ3", ""), In("ours", "")));
	}

	[Fact]
	public void Civ3sFilesComeFirstOutsideStandaloneMode() {
		CivilopediaFiles.Found? found = CivilopediaFiles.Find("Art/Civilopedia/bar-red.pcx", standalone: false,
			In("civ3", "Art/Civilopedia/bar-red.pcx"), In("ours", "Art/Civilopedia/bar-red.png"));
		Assert.Equal(new CivilopediaFiles.Found("civ3/Art/Civilopedia/bar-red.pcx", false), found);
	}

	[Fact]
	public void OursFillInForWhatCiv3IsMissing() {
		CivilopediaFiles.Found? found = CivilopediaFiles.Find(@"Art\Civilopedia\bar-red.pcx", standalone: false,
			Nowhere, In("ours", "Art/Civilopedia/bar-red.png"));
		Assert.Equal(new CivilopediaFiles.Found("ours/Art/Civilopedia/bar-red.png", true), found);
	}

	[Fact]
	public void StandaloneModeReadsOursEvenWhenCiv3HasTheFile() {
		CivilopediaFiles.Found? found = CivilopediaFiles.Find("Text/Civilopedia.txt", standalone: true,
			In("civ3", "Text/Civilopedia.txt"), In("ours", "Text/Civilopedia.txt"));
		Assert.Equal(new CivilopediaFiles.Found("ours/Text/Civilopedia.txt", true), found);
	}

	[Fact]
	public void StandaloneModeFallsBackToC7sAssets() {
		// The standalone ruleset's placeholder unit icons are among C7's
		// assets, which is where standalone mode looks for media.
		string icon = "art/civilopedia/icons/units/unit_large.png";
		CivilopediaFiles.Found? found = CivilopediaFiles.Find(icon, standalone: true, In("assets", icon), Nowhere);
		Assert.Equal(new CivilopediaFiles.Found("assets/" + icon, false), found);
	}

	[Fact]
	public void AFileNoOneHasIsntFound() {
		Assert.Null(CivilopediaFiles.Find("Art/Civilopedia/F-upper.pcx", false, Nowhere, Nowhere));
		Assert.Null(CivilopediaFiles.Find("Art/Civilopedia/F-upper.pcx", true, Nowhere, Nowhere));
	}
}

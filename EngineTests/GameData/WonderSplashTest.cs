using System.IO;
using System.Linq;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// Civ3's wonder splash (FUN_005de750): which buildings bring it up, and what
// it says.
public class WonderSplashTest {
	private static Building MakeBuilding(SaveBuilding building) {
		return new Building(building, new C7GameData.GameData());
	}

	[Fact]
	public void GreatAndSmallWondersBringUpTheSplash() {
		Building pyramids = MakeBuilding(new SaveBuilding { name = "Pyramids", greatWonderProperties = new() });
		Building academy = MakeBuilding(new SaveBuilding { name = "Military Academy", isSmallWonder = true });
		Building granary = MakeBuilding(new SaveBuilding { name = "Granary" });

		Assert.True(WonderSplash.IsShownFor(pyramids));
		Assert.True(WonderSplash.IsShownFor(academy));
		Assert.False(WonderSplash.IsShownFor(granary));
		Assert.False(WonderSplash.IsShownFor(null));
	}

	// PediaIcons.txt lists each wonder's picture under WON_SPLASH_ and its
	// Civilopedia entry, e.g. #WON_SPLASH_BLDG_Pyramids.
	[Fact]
	public void TheArtIsKeyedByTheCivilopediaEntry() {
		Assert.Equal("WON_SPLASH_BLDG_Pyramids", WonderSplash.ArtKey(MakeBuilding(new SaveBuilding { name = "Pyramids", civilopediaEntry = "BLDG_Pyramids" })));
		Assert.Null(WonderSplash.ArtKey(MakeBuilding(new SaveBuilding { name = "Pyramids" })));
	}

	// script.txt, WONDERSPLASH and MP_WONDER_COMPLETION.
	[Fact]
	public void TheMessageIsCiv3s() {
		Assert.Equal("Lord, we have completed Pyramids in Rome.", WonderSplash.Message("Lord", "Pyramids", "Rome"));
		Assert.Equal("We have completed Pyramids in Rome.", WonderSplash.Message(null, "Pyramids", "Rome"));
		Assert.Equal("We have completed Pyramids in Rome.", WonderSplash.PlainMessage("Pyramids", "Rome"));
	}

	[Fact]
	public void AFemaleLeaderIsAddressedByTheFemaleTitle() {
		Government government = new() {
			maleRulerTitles = ["King", "Emperor"],
			femaleRulerTitles = ["Queen", ""],
		};

		Assert.Equal("King", government.RulerTitle(Gender.Male, 0));
		Assert.Equal("Queen", government.RulerTitle(Gender.Female, 0));
		Assert.Equal("Emperor", government.RulerTitle(Gender.Male, 1));
		// A pair without a female title uses the male one, as in Civ3.
		Assert.Equal("Emperor", government.RulerTitle(Gender.Female, 1));
		// The index picks among the pairs used.
		Assert.Equal("King", government.RulerTitle(Gender.Male, 2));
	}

	[Fact]
	public void AGovernmentWithoutTitlesHasNone() {
		Assert.Null(new Government().RulerTitle(Gender.Male, 0));

		Player player = new() { government = new Government(), civilization = new Civilization("Rome") };
		Assert.Null(WonderSplash.RulerTitle(player, new System.Random(1)));
	}

	// The titles come from the BIQ's governments, only the pairs it uses.
	[SkippableFact]
	public void TheTitlesAreImportedFromTheBiq() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		BiqData biq = BiqData.LoadFile(PathUtils.defaultBicPath);
		SaveGame game = ImportCiv3.ImportBiq(PathUtils.defaultBicPath, PathUtils.defaultBicPath, _ => PathUtils.defaultPediaIconsPath);

		Assert.Equal(biq.Govt.Length, game.Governments.Count);
		for (int i = 0; i < biq.Govt.Length; i++) {
			Government government = game.Governments[i];
			Assert.Equal(System.Math.Clamp(biq.Govt[i].RulerTitlePairsUsed, 1, 4), government.maleRulerTitles.Count);
			Assert.Equal(biq.Govt[i].MaleRulerTitle1.Trim(), government.RulerTitle(Gender.Male, 0));
		}
		Assert.All(game.Governments, g => Assert.False(string.IsNullOrWhiteSpace(g.RulerTitle(Gender.Female, 0))));
	}
}

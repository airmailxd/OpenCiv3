using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The Civilopedia icons made for standalone mode (ArtGen/civilopedia_icons):
// C7/Civilopedia/Text/PediaIcons.txt and the icons it lists.
public class StandalonePediaIconsTest : IClassFixture<SaveGameFixture> {
	private static string CivilopediaFolder => PathUtils.getBasePath("../C7/Civilopedia/");
	private static string PediaIconsPath => Path.Combine(CivilopediaFolder, "Text", "PediaIcons.txt");

	private readonly C7GameData.GameData gameData;

	// A game of the bundled rules, which standalone mode keeps every unit of.
	public StandalonePediaIconsTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
	}

	// The articles of Civ3's Civilopedia that the rules have nothing for, but
	// that the Civilopedia shows: terrain features, building colonies and
	// Wealth.
	private static readonly string[] OtherKeys = [
		"TERR_River", "TERR_Fresh_Water_Lake", "TERR_Grassland_with_Shield", "TFRM_Build_Colony", "BLDG_Wealth",
	];

	private IEnumerable<string> EntryKeys() {
		CivilopediaCatalog catalog = CivilopediaCatalog.Build(gameData, new CivilopediaText());
		return catalog.All.Where(e => e.Section != CivilopediaSection.GameConcepts).Select(e => e.Key).Concat(OtherKeys);
	}

	// The width and height of a PNG, from its header.
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

	[Fact]
	public void EveryEntryOfTheBundledRulesHasALargeAndASmallIcon() {
		PediaIcons icons = new(PediaIconsPath);
		List<string> problems = new();
		int count = 0;
		foreach (string key in EntryKeys()) {
			count++;
			foreach ((bool large, int size) in new[] { (true, 128), (false, 32) }) {
				string icon = icons.GetCivilopediaIcon(key, large);
				if (icon == null) {
					problems.Add($"{key}: no {(large ? "large" : "small")} icon");
					continue;
				}
				string path = Path.Combine(CivilopediaFolder, icon);
				if (!File.Exists(path)) {
					problems.Add($"{key}: {icon} is missing");
					continue;
				}
				if (PngSize(path) != (size, size)) {
					problems.Add($"{key}: {icon} is {PngSize(path)}, not {size}x{size}");
				}
			}
		}
		Assert.Empty(problems);
		// Units, advances, buildings, civilizations and the rest.
		Assert.True(count > 380, $"only {count} entries");
	}

	[Fact]
	public void TheIconsOfDifferentEntriesAreDifferentFiles() {
		PediaIcons icons = new(PediaIconsPath);
		List<string> large = EntryKeys().Select(key => icons.GetCivilopediaIcon(key, true)).ToList();
		Assert.Equal(large.Count, large.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());
	}

	[Fact]
	public void TheFileReadsLikeCiv3s() {
		PediaIcons icons = new(PediaIconsPath);

		// Advances: the small icon under #TECH_..., the large under #TECH_..._LARGE.
		Assert.Equal("Art/Civilopedia/Icons/Advances/Bronze_Working_small.png", icons.GetTechIconPath("TECH_Bronze_Working"));
		Assert.Equal("Art/Civilopedia/Icons/Advances/Bronze_Working_large.png", icons.GetCivilopediaIcon("TECH_Bronze_Working", true));

		// Units, also through the unit art's own lookup.
		Assert.Equal("Art/Civilopedia/Icons/Units/Warrior_large.png", icons.GetUnitCivilopediaArt("PRTO_Warrior").large);
		Assert.Equal("Art/Civilopedia/Icons/Units/Warrior_small.png", icons.GetUnitCivilopediaArt("PRTO_Warrior").small);

		// Buildings keep their row in the city screen's art; keys are found whatever their case.
		foreach (Building building in gameData.Buildings.Where(b => b != null)) {
			Assert.Equal(building.iconRowIndex, icons.buildingToRowNumberMapping[building.civilopediaEntry]);
		}
		Assert.Equal("Art/Civilopedia/Icons/Buildings/Barracks_small.png", icons.GetCivilopediaIcon("bldg_barracks", false, era: 2, cultureGroup: 3));
		Assert.Equal("Art/Civilopedia/Icons/Governments/Monarchy_large.png", icons.GetCivilopediaIcon("GOVT_Monarchy", true));
		Assert.Equal("Art/Civilopedia/Icons/Races/ROMANS_small.png", icons.GetCivilopediaIcon("RACE_ROMANS", false));
		Assert.Null(icons.GetCivilopediaIcon("GCON_Combat", true));

		// No leader art: it isn't Civ3's file.
		Assert.Null(icons.GetLeaderArtName("RACE_ROMANS"));
	}
}

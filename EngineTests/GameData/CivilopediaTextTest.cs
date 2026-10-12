using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// Reading Civ3's Civilopedia.txt and PediaIcons.txt for the Civilopedia.
public class CivilopediaTextTest {
	private static string Civ3Text(string file) => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", file);

	[Fact]
	public void LinesStartAtCaretsAndContinueWithoutThem() {
		CivilopediaText text = CivilopediaText.Parse("""
			#PRTO_Warrior
			^
			^
			^The [warrior] is late Stone Age
			infantry, armed with stone axes.
			^
			^Second line.

			#DESC_PRTO_Warrior
			^History.
			""".Replace("\t", ""));

		CivilopediaArticle warrior = text.Get("PRTO_Warrior");
		Assert.NotNull(warrior);
		Assert.Null(warrior.Name);
		Assert.Equal(new List<string> { "The [warrior] is late Stone Age infantry, armed with stone axes.", "", "Second line." }, warrior.Lines);
		Assert.Equal(new List<string> { "History." }, warrior.DescriptionLines);
		Assert.True(warrior.HasDescription);
	}

	[Fact]
	public void GameConceptsHaveTheirNamesInTheFile() {
		CivilopediaText text = CivilopediaText.Parse("""
			#GAME_CONCEPTS_KEYS
			GCON_Combat
			GCON_Enslavement

			#GAME_CONCEPTS
			#GCON_Combat
			Combat
			^There are four types of combat.
			#GCON_Enslavement
			Enslavement
			^Some units capture.
			""".Replace("\t", ""));

		Assert.Equal(new List<string> { "GCON_Combat", "GCON_Enslavement" }, text.GameConceptKeys);
		Assert.Equal("Combat", text.Get("GCON_Combat").Name);
		Assert.Equal(new List<string> { "There are four types of combat." }, text.Get("GCON_Combat").Lines);
		// Keys are found whatever their case and the spaces around them.
		Assert.Equal("Enslavement", text.Get("gcon_enslavement").Name);
		Assert.False(text.Has("GAME_CONCEPTS"));
	}

	[Fact]
	public void CommentsAreSkipped() {
		CivilopediaText text = CivilopediaText.Parse("""
			; A comment
			#TFRM_Mine
			^A mine.
			; Terrain Improvements____
			#TFRM_Road
			^A road.
			""".Replace("\t", ""));

		Assert.Equal(new List<string> { "A mine." }, text.Get("TFRM_Mine").Lines);
		Assert.Equal(new List<string> { "A road." }, text.Get("TFRM_Road").Lines);
	}

	[Fact]
	public void MarkupIsSplitIntoBoldTextAndLinks() {
		List<CivilopediaMarkup.Span> spans = CivilopediaMarkup.Tokenize(
			"A [Barracks] makes $LINK<veteran=GCON_Experience> units. {Requires:} none");

		Assert.Equal(new List<CivilopediaMarkup.Span> {
			new("A ", false, null),
			new("Barracks", true, null),
			new(" makes ", false, null),
			new("veteran", false, "GCON_Experience"),
			new(" units. ", false, null),
			new("Requires:", true, null),
			new(" none", false, null),
		}, spans);
		Assert.Equal("A Barracks makes veteran units. Requires: none",
			CivilopediaMarkup.PlainText("A [Barracks] makes $LINK<veteran=GCON_Experience> units. {Requires:} none"));
	}

	[Fact]
	public void LinksInsideBoldAreBold() {
		List<CivilopediaMarkup.Span> spans = CivilopediaMarkup.Tokenize("[$LINK<Iron=GOOD_Iron >] and $LINK<a=b=TECH_X>");
		Assert.Equal(new CivilopediaMarkup.Span("Iron", true, "GOOD_Iron"), spans[0]);
		// The key is what follows the last =.
		Assert.Equal(new CivilopediaMarkup.Span("a=b", false, "TECH_X"), spans[^1]);
		Assert.Equal(new List<string> { "GOOD_Iron", "TECH_X" }, CivilopediaMarkup.LinkedKeys(["[$LINK<Iron=GOOD_Iron >] and $LINK<a=b=TECH_X>", "$LINK<iron=good_iron>"]));
	}

	[Fact]
	public void AnUnfinishedLinkIsText() {
		Assert.Equal("$LINK<broken", CivilopediaMarkup.PlainText("$LINK<broken"));
	}

	[Fact]
	public void AMissingFileGivesAnEmptyCivilopedia() {
		CivilopediaText text = CivilopediaText.Load(Path.Combine(Path.GetTempPath(), "no such folder", "Civilopedia.txt"));
		Assert.Equal(0, text.Count);
		Assert.Null(text.Get("PRTO_Warrior"));
		Assert.Null(text.Get(null));
	}

	[SkippableFact]
	public void TheConquestsCivilopediaReads() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		CivilopediaText text = CivilopediaText.Load(Civ3Text("Civilopedia.txt"));

		Assert.True(text.Count > 400);
		Assert.Contains("GCON_Combat", text.GameConceptKeys);
		Assert.All(text.GameConceptKeys, key => Assert.NotNull(text.Get(key)?.Name));
		Assert.Contains(text.Get("PRTO_Warrior").Lines, l => l.Contains("[warrior]"));
		Assert.True(text.Get("TECH_Bronze_Working").HasDescription);
		Assert.Contains(text.Get("GOVT_Despotism").Lines, l => l.Contains("Worker efficiency"));
		// The file is Windows-1252, not UTF-8.
		Assert.DoesNotContain(text.Articles.SelectMany(a => a.Lines.Concat(a.DescriptionLines)), l => l.Contains('�'));
	}

	[SkippableFact]
	public void TheConquestsLinksLeadToArticles() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		CivilopediaText text = CivilopediaText.Load(Civ3Text("Civilopedia.txt"));
		List<string> keys = CivilopediaMarkup.LinkedKeys(text.Articles.SelectMany(a => a.Lines.Concat(a.DescriptionLines)));

		Assert.True(keys.Count > 100);
		// Civ3's own text has a few links to articles it doesn't have; the
		// Civilopedia shows those as plain text.
		List<string> broken = keys.Where(k => !text.Has(k)).ToList();
		Assert.True(broken.Count < keys.Count / 20, "Broken links: " + string.Join(", ", broken));
	}

	[SkippableFact]
	public void PediaIconsHasTheLargeIconOfEachKindOfEntry() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		PediaIcons icons = new(Civ3Text("PediaIcons.txt"));

		Assert.Equal("Art/tech chooser/Icons/06-bronzeWorking-large.pcx", icons.GetCivilopediaIcon("TECH_Bronze_Working", large: true));
		Assert.Equal("Art/tech chooser/Icons/06-bronzeWorking-small.pcx", icons.GetCivilopediaIcon("TECH_Bronze_Working", large: false));
		Assert.Equal("art/civilopedia/icons/resources/ironlarge.pcx", icons.GetCivilopediaIcon("GOOD_Iron", large: true));
		Assert.Equal("art/civilopedia/icons/resources/ironsmall.pcx", icons.GetCivilopediaIcon("GOOD_Iron", large: false));
		Assert.Equal("art/civilopedia/icons/terrain/minelarge.pcx", icons.GetCivilopediaIcon("TFRM_Mine", large: true));
		Assert.Equal("art/civilopedia/icons/races/romanslarge.pcx", icons.GetCivilopediaIcon("RACE_Romans", large: true));
		Assert.Equal("art/civilopedia/icons/despotism large.pcx", icons.GetCivilopediaIcon("GOVT_Despotism", large: true));
		Assert.Null(icons.GetCivilopediaIcon("GOVT_Monarchy", large: true));
		Assert.Null(icons.GetCivilopediaIcon(null, large: true));
	}

	[SkippableFact]
	public void BuildingIconsChangeWithTheEraOrCulture() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		PediaIcons icons = new(Civ3Text("PediaIcons.txt"));

		Assert.Equal("art/civilopedia/icons/buildings/pyramidlarge.pcx", icons.GetCivilopediaIcon("BLDG_Pyramids", large: true, era: 3));
		Assert.Equal("art/civilopedia/icons/buildings/pyramidsmall.pcx", icons.GetCivilopediaIcon("BLDG_Pyramids", large: false));
		Assert.Equal("art/civilopedia/icons/buildings/barracksanclarge.pcx", icons.GetCivilopediaIcon("BLDG_Barracks", large: true, era: 0));
		Assert.Equal("art/civilopedia/icons/buildings/barracksrenlarge.pcx", icons.GetCivilopediaIcon("BLDG_Barracks", large: true, era: 1));
		Assert.Equal("art/civilopedia/icons/buildings/barracksindmodsmall.pcx", icons.GetCivilopediaIcon("BLDG_Barracks", large: false, era: 3));
		Assert.Equal("art/civilopedia/icons/buildings/templeeurlarge.pcx", icons.GetCivilopediaIcon("BLDG_Temple", large: true, cultureGroup: 1));
		Assert.Equal("art/civilopedia/icons/buildings/templefeasmall.pcx", icons.GetCivilopediaIcon("BLDG_Temple", large: false, cultureGroup: 4));
		// Eras past what the file has use its last icon.
		Assert.Equal("art/civilopedia/icons/buildings/templefealarge.pcx", icons.GetCivilopediaIcon("BLDG_Temple", large: true, cultureGroup: 9));
	}

	// Each unit, building, civilization, advance, government, resource and
	// worker job of the bundled rules has an article and a large icon.
	[SkippableFact]
	public void TheBundledRulesHaveAnArticleForEverything() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		CivilopediaText text = CivilopediaText.Load(Civ3Text("Civilopedia.txt"));
		PediaIcons icons = new(Civ3Text("PediaIcons.txt"));
		using JsonDocument rules = JsonUtils.LoadBaseRuleset();

		List<string> missing = new();
		void Check(string section, bool needsIcon, System.Func<JsonElement, bool> skip = null) {
			foreach (JsonElement item in rules.RootElement.GetProperty(section).EnumerateArray()) {
				if (skip != null && skip(item)) {
					continue;
				}
				string name = item.TryGetProperty("name", out JsonElement n) ? n.GetString() : "?";
				if (!item.TryGetProperty("civilopediaEntry", out JsonElement key)) {
					missing.Add($"{section} {name}: no key");
					continue;
				}
				if (!text.Has(key.GetString())) {
					missing.Add($"{section} {name}: no article {key.GetString()}");
				}
				if (needsIcon && icons.GetCivilopediaIcon(key.GetString(), large: true) == null) {
					missing.Add($"{section} {name}: no icon {key.GetString()}");
				}
			}
		}
		Check("unitPrototypes", needsIcon: true);
		Check("buildings", needsIcon: true);
		Check("civilizations", needsIcon: true, skip: c => c.TryGetProperty("isBarbarian", out JsonElement b) && b.GetBoolean());
		Check("techs", needsIcon: false);
		Check("resources", needsIcon: true);

		Assert.Empty(missing);
	}

	[SkippableFact]
	public void AnImportedGameKnowsEachUnitsBuildingsAndCivsArticle() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		SaveGame game = ImportCiv3.ImportBiq(PathUtils.defaultBicPath, PathUtils.defaultBicPath, _ => PathUtils.defaultPediaIconsPath);

		Assert.Equal("PRTO_Warrior", game.UnitPrototypes.Single(u => u.name == "Warrior").civilopediaEntry);
		Assert.Equal("BLDG_Barracks", game.Buildings.Single(b => b.name == "Barracks").civilopediaEntry);
		Assert.Equal("RACE_Romans", game.Civilizations.Single(c => c.name == "Rome").civilopediaEntry, ignoreCase: true);
		Assert.All(game.UnitPrototypes, u => Assert.False(string.IsNullOrEmpty(u.civilopediaEntry)));
	}
}

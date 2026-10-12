using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// OpenCiv3's own Civilopedia text, C7/Civilopedia/Text/Civilopedia.txt,
// which the Civilopedia uses when there are no Civ3 files.
public class OpenCiv3CivilopediaTest : IClassFixture<SaveGameFixture> {
	private static readonly string TextPath = PathUtils.getBasePath("../C7/Civilopedia/Text/Civilopedia.txt");

	private readonly SaveGameFixture fixture;

	public OpenCiv3CivilopediaTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private static CivilopediaText Text() {
		Assert.True(File.Exists(TextPath), $"No Civilopedia text at {TextPath}");
		return CivilopediaText.Load(TextPath);
	}

	public static IEnumerable<object[]> Modes() {
		yield return new object[] { false };
		yield return new object[] { true };
	}

	// Every entry the Civilopedia shows for a game of the bundled rules, in
	// both the Civ3 and standalone modes, has an article and a history.
	[Theory]
	[MemberData(nameof(Modes))]
	public void EveryEntryHasAnArticle(bool standalone) {
		CivilopediaText text = Text();
		C7GameData.GameData gameData = (standalone ? fixture.standaloneSaveGame : fixture.saveGame).ToGameData(fixture.behaviors);
		CivilopediaCatalog catalog = CivilopediaCatalog.Build(gameData, text);

		List<string> missing = new();
		foreach (CivilopediaEntry entry in catalog.All) {
			if (entry.Article == null || entry.Article.Lines.All(l => l.Trim().Length == 0)) {
				missing.Add($"{entry.Section} {entry.Name} ({entry.Key}): no article");
			} else if (entry.Section != CivilopediaSection.GameConcepts && !entry.Article.HasDescription) {
				missing.Add($"{entry.Section} {entry.Name} ({entry.Key}): no description");
			}
		}
		Assert.Empty(missing);

		foreach (CivilopediaSection section in CivilopediaCatalog.MenuSections.Where(s => s != CivilopediaSection.Index)) {
			Assert.NotEmpty(catalog.In(section));
		}
		Assert.NotNull(catalog.Get("BLDG_Wealth"));
		Assert.Equal(CivilopediaSection.Terrain, catalog.Get("TERR_River").Section);
		Assert.Equal(CivilopediaSection.WorkerActions, catalog.Get("TFRM_Build_Colony").Section);
	}

	// Only game concepts are named in the file, and every game concept is.
	[Fact]
	public void GameConceptsAreNamed() {
		CivilopediaText text = Text();

		Assert.Contains("GCON_Combat", text.GameConceptKeys);
		Assert.Contains("GCON_Enslavement", text.GameConceptKeys);
		Assert.All(text.GameConceptKeys, key => Assert.False(string.IsNullOrWhiteSpace(text.Get(key)?.Name), $"{key} has no name"));
		Assert.All(text.GameConceptKeys, key => Assert.NotEmpty(text.Get(key).Lines));
		Assert.All(text.Articles.Where(a => a.Name != null), a => Assert.StartsWith("GCON_", a.Key));
		Assert.All(text.Articles.Where(a => a.Key.StartsWith("GCON_")), a => Assert.Contains(a.Key, text.GameConceptKeys));
	}

	[Fact]
	public void EveryLinkLeadsToAnArticle() {
		CivilopediaText text = Text();
		List<string> keys = CivilopediaMarkup.LinkedKeys(text.Articles.SelectMany(a => a.Lines.Concat(a.DescriptionLines)));

		Assert.True(keys.Count > 100);
		List<string> broken = keys.Where(k => !text.Has(k)).ToList();
		Assert.True(broken.Count == 0, "Broken links: " + string.Join(", ", broken));
	}

	// The file is read as Windows-1252; keeping it to plain ASCII means it
	// reads the same whatever the encoding.
	[Fact]
	public void TheFileIsPlainAscii() {
		byte[] bytes = File.ReadAllBytes(TextPath);

		// Git may check it out with Windows line endings.
		Assert.DoesNotContain(bytes, b => b > 126 || (b < 32 && b != '\t' && b != '\n' && b != '\r'));
	}
}

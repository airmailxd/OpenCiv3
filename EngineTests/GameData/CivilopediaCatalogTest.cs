using System.IO;
using System.Linq;
using C7GameData;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// The Civilopedia's sections and the links between its entries, for a game
// of the bundled rules.
public class CivilopediaCatalogTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;

	public CivilopediaCatalogTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
	}

	private static CivilopediaText ConquestsText() {
		return CivilopediaText.Load(Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "Civilopedia.txt"));
	}

	private Tech Tech(string name) => gameData.techs.Single(t => t.Name == name);

	[Fact]
	public void EverythingInTheRulesHasAnEntryInItsSection() {
		CivilopediaCatalog catalog = CivilopediaCatalog.Build(gameData, new CivilopediaText());

		Assert.Equal(CivilopediaSection.Units, catalog.For(gameData.unitPrototypes.Single(u => u.name == "Warrior")).Section);
		Assert.Equal(CivilopediaSection.GreatWonders, catalog.Get("BLDG_Pyramids").Section);
		Assert.Equal(CivilopediaSection.SmallWonders, catalog.Get("BLDG_Epic").Section);
		Assert.Equal(CivilopediaSection.Improvements, catalog.Get("BLDG_Barracks").Section);
		Assert.Equal(CivilopediaSection.Advances, catalog.Get("TECH_Bronze_Working").Section);
		Assert.Equal(CivilopediaSection.Governments, catalog.Get("GOVT_Monarchy").Section);
		Assert.Equal(CivilopediaSection.WorkerActions, catalog.Get("TFRM_Mine").Section);
		Assert.Equal(CivilopediaSection.Terrain, catalog.Get("TERR_Flood_Plain").Section);
		Assert.Equal(CivilopediaSection.Resources, catalog.Get("GOOD_Iron").Section);
		Assert.Equal(CivilopediaSection.Civilizations, catalog.Get("race_romans").Section);
		Assert.Equal("Romans", catalog.Get("RACE_ROMANS").Name);

		// No barbarians, and no hidden advances.
		Assert.DoesNotContain(catalog.In(CivilopediaSection.Civilizations), e => e.Civilization.isBarbarian);
		Assert.DoesNotContain(catalog.In(CivilopediaSection.Advances), e => e.Tech.EraCivilopediaName == "Hidden");

		// The index has everything, in alphabetical order.
		Assert.Equal(catalog.All.Count, catalog.In(CivilopediaSection.Index).Count);
		Assert.Equal(catalog.All.OrderBy(e => e.Name, System.StringComparer.OrdinalIgnoreCase).Select(e => e.Name), catalog.All.Select(e => e.Name));
		Assert.Equal(catalog.In(CivilopediaSection.Units).Select(e => e.Name).OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase), catalog.In(CivilopediaSection.Units).Select(e => e.Name));
	}

	[Fact]
	public void AnAdvanceLeadsToTheAdvancesThatNeedIt() {
		CivilopediaCatalog catalog = CivilopediaCatalog.Build(gameData, new CivilopediaText());

		Assert.Contains(Tech("Iron Working"), catalog.LeadsTo(Tech("Bronze Working")));
		Assert.DoesNotContain(Tech("Writing"), catalog.LeadsTo(Tech("Bronze Working")));
		Assert.DoesNotContain(catalog.LeadsTo(Tech("Bronze Working")), t => !t.Prerequisites.Contains(Tech("Bronze Working")));
	}

	[Fact]
	public void AnAdvanceAllowsItsUnitsBuildingsGovernmentsJobsAndResources() {
		CivilopediaCatalog catalog = CivilopediaCatalog.Build(gameData, new CivilopediaText());

		Assert.Contains(catalog.AllowedBy(Tech("Bronze Working")), e => e.Name == "The Colossus");
		Assert.Contains(catalog.AllowedBy(Tech("Bronze Working")), e => e.Unit?.name == "Spearman");
		Assert.Contains(catalog.AllowedBy(Tech("Monarchy")), e => e.Government?.name == "Monarchy");
		Assert.Contains(catalog.AllowedBy(Tech("Iron Working")), e => e.Resource?.Name == "Iron");
		Assert.Contains(catalog.AllowedBy(Tech("Construction")), e => e.Terraform?.Name == "Fortress");
		Building greatLibrary = gameData.Buildings.Single(b => b.name == "The Great Library");
		Assert.Contains(catalog.ObsoletedBy(greatLibrary.renderedObsoleteBy), e => e.Building == greatLibrary);
	}

	[Fact]
	public void UnitsKnowTheirUpgradesAndJobs() {
		CivilopediaCatalog catalog = CivilopediaCatalog.Build(gameData, new CivilopediaText());
		UnitPrototype warrior = gameData.unitPrototypes.Single(u => u.name == "Warrior");
		UnitPrototype swordsman = gameData.unitPrototypes.Single(u => u.name == "Swordsman");
		Terraform mine = gameData.Terraforms.Single(t => t.Name == "Mine");

		Assert.Contains(warrior, catalog.UpgradesFrom(swordsman));
		Assert.Contains(catalog.UnitsThatCanDo(mine), u => u.name == "Worker");
		Assert.Contains(catalog.TerrainsWith(gameData.Resources.Single(r => r.Name == "Iron")), t => t.DisplayName == "Hills");
	}

	[Fact]
	public void ACivilizationsUniqueUnitIsOnlyItsOwn() {
		CivilopediaCatalog catalog = CivilopediaCatalog.Build(gameData, new CivilopediaText());
		Civilization rome = catalog.Get("RACE_ROMANS").Civilization;

		Assert.Equal(["Legionary"], catalog.UniqueUnitsOf(rome).Select(u => u.name));
	}

	[SkippableFact]
	public void TheArticlesOnlyCivilopediaTextHasAreAdded() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		CivilopediaCatalog catalog = CivilopediaCatalog.Build(gameData, ConquestsText());

		Assert.Equal("Combat", catalog.Get("GCON_Combat").Name);
		Assert.Equal(CivilopediaSection.GameConcepts, catalog.Get("GCON_Combat").Section);
		Assert.Equal(CivilopediaSection.Terrain, catalog.Get("TERR_River").Section);
		Assert.Equal("River", catalog.Get("TERR_River").Name);
		Assert.Equal(CivilopediaSection.WorkerActions, catalog.Get("TFRM_Build_Colony").Section);
		Assert.Equal(CivilopediaSection.Improvements, catalog.Get("BLDG_Wealth").Section);
		// Every entry for something in the rules has its article.
		Assert.All(catalog.All.Where(e => e.Section != CivilopediaSection.Terrain || e.Terrain != null), e => Assert.NotNull(e.Article));
	}
}

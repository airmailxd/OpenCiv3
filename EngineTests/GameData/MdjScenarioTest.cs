using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// MDJ's rules (see GameSetup.Mdj.cs), bundled from MPTournamentMDJ.biq,
// which ships with Civ3 Complete.
public class MdjScenarioTest {
	private static GameMode.Config Mode(bool standalone) => new("civ3", standalone ? ["standalone"] : []);

	private static C7GameData.GameData NewGame(GameScenario scenario, int seed, bool standalone = true) {
		SaveGame save = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone));
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = 80, height = 80, numberOfCivs = 4, distanceBetweenCivs = 12, techRate = 240, optimalNumberOfCities = 16 },
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.IsIndependent),
			difficulty = save.Difficulties[2],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 3).ToList(),
			victoryConditions = new VictoryConditions(),
			scenario = scenario,
		}.Populate(save);
		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(save);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		return gameData;
	}

	// Every civ knows every advance and is in Feudalism, for good; the
	// units are Medieval Japan's.
	[Fact]
	public void EveryCivKnowsEverythingUnderFeudalism() {
		C7GameData.GameData gameData = NewGame(GameScenario.RisingTide, 1);
		Assert.Contains(gameData.unitPrototypes, p => p.name == "Ashigaru");
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			Assert.Equal(gameData.techs.Count, civ.knownTechs.Count);
			Assert.Equal(GameSetup.MdjGovernment, civ.government.name);
			Assert.Equal(EraUtils.MIDDLE_AGES_CVLPD, civ.eraCivilopediaName);
			Assert.False(civ.CanStartRevolution(gameData));
			Assert.Single(civ.cities);
			Assert.True(civ.units.Count(u => u.unitType.isWorker) >= 3);
			Assert.True(civ.units.Count(u => u.unitType.attack > 0) >= 2);
		}
	}

	// With nothing to research, the AI puts nothing into science.
	[Fact]
	public void TheAiSpendsNothingOnScience() {
		C7GameData.GameData gameData = NewGame(GameScenario.RisingTide, 3);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		civ.isHuman = false;
		PlayerAI.AdjustSliders(civ);
		Assert.Equal(0, civ.scienceRate);
		Assert.Equal(10, civ.taxRate + civ.luxuryRate);
	}

	// A resource a civ has had stays its own once it is cut off.
	[Fact]
	public void AResourceOnceHadIsKept() {
		C7GameData.GameData gameData = NewGame(GameScenario.None, 2);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		City city = civ.cities.FirstOrDefault() ?? CityInteractions.BuildCity(civ.units.First().location, civ, "Edo");
		Resource horses = gameData.Resources.First(r => r.Category == ResourceCategory.STRATEGIC && civ.KnowsAboutResource(r));
		// On the city's own tile, which needs no road.
		Tile tile = city.location;
		tile.Resource = horses;
		gameData.UpdateTileOwners();
		gameData.InvalidateCachedTradeNetwork();
		Assert.True(city.GetAvailableResources(gameData).ContainsKey(horses));
		civ.RecordObtainedResources(gameData);

		tile.Resource = Resource.NONE;
		gameData.InvalidateCachedTradeNetwork();
		Assert.False(city.GetCurrentResources(gameData).ContainsKey(horses));
		Assert.True(city.GetAvailableResources(gameData).ContainsKey(horses));

		SaveGame loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.Contains(horses.Key, loaded.Players.First(p => p.id == civ.id).obtainedResources);
	}

	// The rules need no Civ3 install, and standalone mode draws them with
	// its own art: no scenario folders to look in, and the units'
	// Civilopedia icons its own.
	[Fact]
	public void StandaloneHasTheRulesWithItsOwnArt() {
		SaveGame save = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: true));
		Assert.Null(save.ScenarioSearchPath);
		Assert.True(save.Rules.AllTechsAtStart && save.Rules.GovernmentLocked && save.Rules.PersistentResources);
		Assert.Equal(GameSetup.MdjGovernment, save.Rules.OnlyGovernment);
		Assert.Contains(save.Civilizations, c => c.name.StartsWith("Oda"));
		Assert.All(save.UnitPrototypes, u => Assert.StartsWith("Art/Civilopedia/Icons/Units/", u.art.pediaArt.large));
		SaveGame withCiv3Art = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: false));
		Assert.False(string.IsNullOrEmpty(withCiv3Art.ScenarioSearchPath));
		// Jade and Sake are pictured in our own resource sheet's extra cells;
		// with Civ3's art, in the Sengoku conquest's sheet's.
		Assert.Equal(26, save.Resources.First(r => r.Key == "Jade").Icon);
		Assert.Equal(27, save.Resources.First(r => r.Key == "Sake").Icon);
		Assert.Equal(7, withCiv3Art.Resources.First(r => r.Key == "Jade").Icon);
		Assert.Equal(4, save.Resources.First(r => r.Key == "Olive Oil").Icon);
	}

	// The bundled rules are what RegenerateRuleset --mdj makes of the BIQ
	// today: its units, advances, buildings, clans and resources.
	[SkippableFact]
	public void TheBundledRulesAreTheBiqs() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		SaveGame biq = GameSetup.ImportMdjBiq(Civ3Location.GetCiv3Path());
		Skip.If(biq == null, "No MPTournamentMDJ.biq");
		SaveGame bundled = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: false));
		Assert.Equal(biq.UnitPrototypes.Select(u => (u.name, u.attack, u.defense, u.movement, u.shieldCost)),
			bundled.UnitPrototypes.Select(u => (u.name, u.attack, u.defense, u.movement, u.shieldCost)));
		Assert.Equal(biq.Techs.Select(t => t.Name), bundled.Techs.Select(t => t.Name));
		Assert.Equal(biq.Buildings.Select(b => b.name), bundled.Buildings.Select(b => b.name));
		Assert.Equal(biq.Civilizations.Select(c => c.name), bundled.Civilizations.Select(c => c.name));
		Assert.Equal(biq.Resources.Select(r => r.Key), bundled.Resources.Select(r => r.Key));
	}

	// Standalone mode's own Civilopedia (C7/Civilopedia) has a page, with its
	// history, for every unit, advance (but the hidden ones), building,
	// resource and clan of MDJ's, and its links lead to pages that are there.
	[Fact]
	public void TheCivilopediaHasEveryMdjEntry() {
		SaveGame save = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: true));
		CivilopediaText text = CivilopediaText.Load(PathUtils.getBasePath("../C7/Civilopedia/Text/Civilopedia.txt"));
		IEnumerable<string> keys = save.UnitPrototypes.Select(u => u.civilopediaEntry)
			.Concat(save.Techs.Where(t => t.EraCivilopediaName != "Hidden").Select(t => t.CivilopediaEntry))
			.Concat(save.Buildings.Select(b => b.civilopediaEntry))
			.Concat(save.Resources.Select(r => r.CivilopediaEntry))
			.Concat(save.Civilizations.Where(c => !c.isBarbarian).Select(c => c.civilopediaEntry));
		List<string> problems = [];
		foreach (string key in keys.Distinct()) {
			CivilopediaArticle article = text.Get(key);
			if (article == null) {
				problems.Add($"{key}: no page");
				continue;
			}
			if (!article.HasDescription) {
				problems.Add($"{key}: no history");
			}
			foreach (string link in CivilopediaMarkup.LinkedKeys(article.Lines)) {
				if (!text.Has(link)) {
					problems.Add($"{key}: links to {link}, which has no page");
				}
			}
		}
		Assert.True(problems.Count == 0, string.Join("\n", problems));
	}

	// Every worker job a unit has is one the rules define.
	[Fact]
	public void EveryUnitsWorkerJobsAreDefined() {
		SaveGame save = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: true));
		var terraforms = save.TerraForms.Select(t => t.Id).ToHashSet();
		Assert.All(save.UnitPrototypes, u => Assert.All(u.terraformActions, id => Assert.Contains(id, terraforms)));
	}
}

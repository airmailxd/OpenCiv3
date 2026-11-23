using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// MDJ's rules (see GameSetup.Mdj.cs), bundled from the Civ Players League's
// MPTournamentMDJ.biq, a newer version of the one that ships with Civ3
// Complete.
public class MdjScenarioTest {
	private static GameMode.Config Mode(bool standalone) => new("civ3", standalone ? ["standalone"] : []);

	// Where the League's MPTournamentMDJ.biq is, to check the bundled rules
	// against: with the League's other mods (see BundledScenariosTest), in
	// the folder C7_LEAGUE_MODS names, or the user's Downloads/Scenarios.
	// The game never reads it.
	private static string LeagueBiqPath => Path.Combine(Environment.GetEnvironmentVariable("C7_LEAGUE_MODS")
		?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Scenarios"), GameSetup.MdjBiqName);

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

	// Every civ knows every advance but MDJ's lock (Place-Holder Tech, which
	// keeps railroads, airfields, radar towers, barricades and a new Palace
	// out of the game) and is in Feudalism, for good; the units are
	// Medieval Japan's.
	[Fact]
	public void EveryCivKnowsEverythingUnderFeudalism() {
		C7GameData.GameData gameData = NewGame(GameScenario.RisingTide, 1);
		Assert.Contains(gameData.unitPrototypes, p => p.name == "Ashigaru");
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			Assert.Equal(gameData.techs.Count - 1, civ.knownTechs.Count);
			Assert.DoesNotContain(gameData.techs.Single(t => t.Name == GameSetup.MdjLockTech).id, civ.knownTechs);
			Assert.DoesNotContain(gameData.Terraforms, t => t.Name == "Railroad" && t.RequiredTech != null && civ.knownTechs.Contains(t.RequiredTech));
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
		Assert.True(save.Rules.GovernmentLocked && save.Rules.PersistentResources);
		// Every advance at the start is Rising Tide's only.
		Assert.False(save.Rules.AllTechsAtStart);
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

	// The bundled rules are what RegenerateRuleset --mdj makes of the
	// League's BIQ today: its units, advances, buildings, clans, resources,
	// world sizes and AI rules (skipped without the BIQ, or without the Civ3
	// install whose conquests.biq the import fills in from).
	[SkippableFact]
	public void TheBundledRulesAreTheBiqs() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		Skip.If(!File.Exists(LeagueBiqPath), $"No {LeagueBiqPath}");
		SaveGame biq = GameSetup.ImportMdjBiq(Civ3Location.GetCiv3Path(), LeagueBiqPath);
		SaveGame bundled = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: false));
		Assert.Equal(biq.UnitPrototypes.Select(u => (u.name, u.attack, u.defense, u.movement, u.shieldCost, u.civilopediaEntry, string.Join(",", u.upgradesTo ?? []))),
			bundled.UnitPrototypes.Select(u => (u.name, u.attack, u.defense, u.movement, u.shieldCost, u.civilopediaEntry, string.Join(",", u.upgradesTo ?? []))));
		Assert.Equal(biq.Techs.Select(t => t.Name), bundled.Techs.Select(t => t.Name));
		Assert.Equal(biq.Buildings.Select(b => b.name), bundled.Buildings.Select(b => b.name));
		Assert.Equal(biq.Civilizations.Select(c => (c.name, string.Join(",", c.buildOften.Order()))),
			bundled.Civilizations.Select(c => (c.name, string.Join(",", c.buildOften.Order()))));
		Assert.Equal(biq.Resources.Select(r => (r.Key, r.FoodBonus, r.ShieldsBonus, r.CommerceBonus)),
			bundled.Resources.Select(r => (r.Key, r.FoodBonus, r.ShieldsBonus, r.CommerceBonus)));
		Assert.Equal(biq.WorldSizes.Select(w => (w.name, w.width, w.height)), bundled.WorldSizes.Select(w => (w.name, w.width, w.height)));
		Assert.Equal(biq.Rules.AiBuildingMaintenancePercent, bundled.Rules.AiBuildingMaintenancePercent);
	}

	// The League's MDJ, against the one Civ3 ships: the Myoshi Ronin
	// stronger in attack (7/3, was 6/4) and the Ashigaru's upgrade; the
	// Mori's Infiltrator for their Ninja; the Scout drawn and described as
	// a Horseman; Incense that feeds (+2 food), found with Cattle on desert
	// but no longer on hills; Double Worker Speed renamed 2x Worker Speed;
	// Plant Forest with Bridge Building (was Contact : Ronin); the bigger
	// maps 64 and 66 high; the AI paying all its buildings' upkeep; and
	// every clan's AI building offense and navies often.
	[Fact]
	public void TheRulesAreTheLeagues() {
		SaveGame save = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: false));
		SaveUnitPrototype ronin = save.UnitPrototypes.Single(u => u.name == "Myoshi Ronin (Amphib)");
		Assert.Equal((7, 3), (ronin.attack, ronin.defense));
		Assert.Equal(["Myoshi Ronin (Amphib)"], save.UnitPrototypes.Single(u => u.name == "Ashigaru").upgradesTo);
		Assert.DoesNotContain(save.UnitPrototypes, u => u.name == "Mori Ninja (Amphib)");
		SaveUnitPrototype infiltrator = save.UnitPrototypes.Single(u => u.name == "Infiltrator (Amphib)");
		Assert.Equal((10, 2, 2), (infiltrator.attack, infiltrator.defense, infiltrator.movement));
		Assert.Equal(["Mori (Mil/Exp)"], infiltrator.producibleBy);
		Assert.Equal("PRTO_Horseman", save.UnitPrototypes.Single(u => u.name == "Scout").civilopediaEntry);
		Resource incense = save.Resources.Single(r => r.Key == "Incense");
		Assert.Equal(2, incense.FoodBonus);
		Assert.Contains("Cattle", save.TerrainTypes.Single(t => t.Key == "desert").allowedResources);
		Assert.DoesNotContain("Incense", save.TerrainTypes.Single(t => t.Key == "hills").allowedResources);
		Assert.Contains(save.Techs, t => t.Name == "2x Worker Speed");
		Assert.DoesNotContain(save.Techs, t => t.Name == "Double Worker Speed");
		SaveTerraform plantForest = save.TerraForms.Single(t => t.Name == "Plant Forest");
		Assert.Equal("Bridge Building", save.Techs.Single(t => t.id == plantForest.RequiredTech).Name);
		Assert.Equal(100, save.Rules.AiBuildingMaintenancePercent);
		Assert.All(save.Civilizations.Where(c => !c.isBarbarian),
			c => Assert.Equal([PlanFocus.Offense, PlanFocus.Naval], c.buildOften.Order()));
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
				// MDJ's own keys still waiting for their articles are LeagueArtTest's.
				if (LeagueArtTest.AwaitsArticle(key, text.Has)) {
					continue;
				}
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

	// MDJ's world sizes are its own, by player count, the middle one first.
	[Fact]
	public void MdjHasItsOwnWorldSizes() {
		SaveGame save = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: true));
		Assert.Equal(["1v1", "4 Player", "6 Player", "7 Player", "8 Player"], save.WorldSizes.Select(w => w.name));
		Assert.Equal("6 Player", Assert.Single(save.WorldSizes, w => w.isDefault).name);
		WorldSize seven = save.WorldSizes[3];
		Assert.Equal((64, 64, 7, 20, 9), (seven.width, seven.height, seven.numberOfCivs, seven.distanceBetweenCivs, seven.optimalNumberOfCities));
		WorldSize eight = save.WorldSizes[4];
		Assert.Equal((66, 66, 8, 22, 9), (eight.width, eight.height, eight.numberOfCivs, eight.distanceBetweenCivs, eight.optimalNumberOfCities));
	}

	// The resources MDJ's units need are all on its maps: placed before its
	// many strategic resources nothing needs, they aren't crowded out. Each
	// is as many as Civ3 places (Civ3 0x6020d0): the world size's six civs
	// times its appearance ratio of 150, on four terrains or more.
	[Fact]
	public void TheResourcesMdjsUnitsNeedAreAllPlaced() {
		SaveGame save = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: true));
		WorldSize size = save.WorldSizes.First(w => w.isDefault);
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Continents,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = size,
			mapSeed = 1,
		};
		GameMap map = MapGenerator.GenerateMap(wc);
		foreach (string key in new[] { "Horses", "Iron", "Saltpeter", "Jade" }) {
			Assert.Equal(9, map.tiles.Count(t => t.Resource?.Key == key));
		}
	}

	// Every worker job a unit has is one the rules define.
	[Fact]
	public void EveryUnitsWorkerJobsAreDefined() {
		SaveGame save = GameSetup.LoadMdjRules(PathUtils.GameModesDir, Mode(standalone: true));
		var terraforms = save.TerraForms.Select(t => t.Id).ToHashSet();
		Assert.All(save.UnitPrototypes, u => Assert.All(u.terraformActions, id => Assert.Contains(id, terraforms)));
	}
}

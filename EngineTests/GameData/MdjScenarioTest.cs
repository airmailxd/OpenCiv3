using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// MDJ's rules (see GameSetup.Mdj.cs), from MPTournamentMDJ.biq, which ships
// with Civ3 Complete.
public class MdjScenarioTest {
	private static C7GameData.GameData NewGame(GameScenario scenario, int seed) {
		SaveGame save = GameSetup.LoadMdjRules(Civ3Location.GetCiv3Path());
		Skip.If(save == null, "No MPTournamentMDJ.biq");
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
	[SkippableFact]
	public void EveryCivKnowsEverythingUnderFeudalism() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		C7GameData.GameData gameData = NewGame(GameScenario.RisingTide, 1);
		Assert.Contains(gameData.unitPrototypes, p => p.name == "Ashigaru");
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			Assert.Equal(gameData.techs.Count, civ.knownTechs.Count);
			Assert.Equal(GameSetup.MdjGovernment, civ.government.name);
			Assert.False(civ.CanStartRevolution(gameData));
			Assert.Single(civ.cities);
			Assert.True(civ.units.Count(u => u.unitType.isWorker) >= 3);
			Assert.True(civ.units.Count(u => u.unitType.attack > 0) >= 2);
		}
	}

	// With nothing to research, the AI puts nothing into science.
	[SkippableFact]
	public void TheAiSpendsNothingOnScience() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		C7GameData.GameData gameData = NewGame(GameScenario.RisingTide, 3);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		civ.isHuman = false;
		PlayerAI.AdjustSliders(civ);
		Assert.Equal(0, civ.scienceRate);
		Assert.Equal(10, civ.taxRate + civ.luxuryRate);
	}

	// A resource a civ has had stays its own once it is cut off.
	[SkippableFact]
	public void AResourceOnceHadIsKept() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
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
}

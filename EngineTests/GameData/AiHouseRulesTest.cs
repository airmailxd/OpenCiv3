using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The house rules that favor AI players (Rules.AiBuildingMaintenancePercent
// and the rest) help the AI and never the human, and are saved with the game.
public class AiHouseRulesTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public AiHouseRulesTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	// A city with every building that has upkeep, for a player who is human
	// or not.
	private (C7GameData.GameData, City) CityWithBuildings(bool human) {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		player.isHuman = human;
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		City city = player.cities[0];
		foreach (Building b in gameData.Buildings.Where(b => b.maintenanceCost > 0 && !b.IsGreatWonder() && !b.isSmallWonder).Take(5)) {
			city.AddBuilding(b);
		}
		return (gameData, city);
	}

	[Fact]
	public void MaintenanceIsScaledForTheAiOnly() {
		(C7GameData.GameData gameData, City aiCity) = CityWithBuildings(human: false);
		int full = aiCity.MaintenanceCostsRaw();
		Assert.True(full > 0);
		gameData.rules.AiBuildingMaintenancePercent = 50;
		Assert.Equal((full * 50 + 50) / 100, aiCity.MaintenanceCostsRaw());
		gameData.rules.AiBuildingMaintenancePercent = 0;
		Assert.Equal(0, aiCity.MaintenanceCostsRaw());

		(gameData, City humanCity) = CityWithBuildings(human: true);
		int humanFull = humanCity.MaintenanceCostsRaw();
		gameData.rules.AiBuildingMaintenancePercent = 0;
		Assert.Equal(humanFull, humanCity.MaintenanceCostsRaw());
	}

	[Fact]
	public void ATrialSwitchOverridesTheRule() {
		(_, City aiCity) = CityWithBuildings(human: false);
		AiExperiments.Enable(aiCity.owner, "ai-maintenance-0");
		Assert.Equal(0, aiCity.MaintenanceCostsRaw());
	}

	[Fact]
	public void OtherBonusesLeaveHumansAlone() {
		(C7GameData.GameData gameData, City humanCity) = CityWithBuildings(human: true);
		gameData.rules.AiExtraFreeUnitsPerCity = 3;
		gameData.rules.AiCorruptionPercent = 0;
		gameData.rules.AiExtraContentCitizens = 2;
		gameData.rules.AiCostPercent = 50;
		Player human = humanCity.owner;
		Assert.Equal(0, AiHouseRules.ExtraFreeUnitsPerCity(human));
		Assert.Equal(1f, AiHouseRules.CorruptionFactor(human));
		Assert.Equal(0, AiHouseRules.ExtraContentCitizens(human));
		Assert.Equal(1f, AiHouseRules.CostFactor(human));

		Player ai = gameData.players.First(p => p != human && !p.isBarbarians);
		ai.isHuman = false;
		Assert.Equal(3, AiHouseRules.ExtraFreeUnitsPerCity(ai));
		Assert.Equal(0f, AiHouseRules.CorruptionFactor(ai));
		Assert.Equal(2, AiHouseRules.ExtraContentCitizens(ai));
		Assert.Equal(.5f, AiHouseRules.CostFactor(ai));
	}

	[Fact]
	public void TheRulesDefaultToCiv3AndAreSaved() {
		Rules rules = new();
		Assert.Equal(100, rules.AiBuildingMaintenancePercent);
		Assert.Equal(0, rules.AiExtraFreeUnitsPerCity);
		Assert.Equal(100, rules.AiCorruptionPercent);
		Assert.Equal(0, rules.AiExtraContentCitizens);
		Assert.Equal(100, rules.AiCostPercent);

		SaveGame save = fixture.saveGame.Clone();
		save.Rules.AiBuildingMaintenancePercent = 25;
		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		Assert.Equal(25, loaded.Rules.AiBuildingMaintenancePercent);
	}
}

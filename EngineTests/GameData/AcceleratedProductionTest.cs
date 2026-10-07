using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class AcceleratedProductionTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly City city;

	public AcceleratedProductionTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		player.DoCorruptionCalculations(gameData);
	}

	[Fact]
	public void CitiesGenerateDoubleFoodShieldsAndCommerce() {
		gameData.rules.AcceleratedProduction = false;
		int food = city.CurrentFoodYield();
		int shields = Shields(city);
		int consumed = city.FoodConsumedPerTurn();
		int commerce = TotalCommerce(city.CurrentCommerceYieldRaw());

		gameData.rules.AcceleratedProduction = true;
		Assert.Equal(food * 2, city.CurrentFoodYield());
		Assert.Equal(shields * 2, Shields(city));
		Assert.Equal(commerce * 2, TotalCommerce(city.CurrentCommerceYieldRaw()));
		// Citizens eat as much as before, so the surplus grows by the extra food.
		Assert.Equal(consumed, city.FoodConsumedPerTurn());
		Assert.Equal(food * 2 - consumed, city.FoodGrowthPerTurn());
	}

	private static int Shields(City c) {
		CorruptableValue shields = c.CurrentProductionYield();
		return shields.useful + shields.corrupt;
	}

	private static int TotalCommerce(CommerceBreakdown c) {
		return c.corrupted + c.taxes + c.beakers + c.happiness;
	}

	[Fact]
	public void TheChoiceAtSetupIsSaved() {
		SaveGame save = fixture.saveGame.Clone();
		new GameSetup { acceleratedProduction = true, difficulty = save.GameDifficulty }.Populate(save);
		Assert.True(save.Rules.AcceleratedProduction);

		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		Assert.True(loaded.Rules.AcceleratedProduction);
	}

	[Fact]
	public void SetupWithoutAChoiceKeepsTheScenarioSetting() {
		SaveGame save = fixture.saveGame.Clone();
		save.Rules.AcceleratedProduction = true;
		new GameSetup { difficulty = save.GameDifficulty }.Populate(save);
		Assert.True(save.Rules.AcceleratedProduction);
	}
}

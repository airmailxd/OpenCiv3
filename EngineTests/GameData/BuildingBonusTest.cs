using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

public class BuildingBonusTest {
	private static City MakeCity(int shields, int commerce) {
		C7Engine.EngineStorage.InitializeGameDataForTests(new C7GameData.GameData() {
			gameDifficulty = new Difficulty(),
		});
		Player player = new() { isHuman = true, scienceRate = 6, luxuryRate = 2, taxRate = 2 };
		player.civilization = new Civilization();
		player.government = new Government();

		City city = new(Tile.NONE, player, "Alexandria", ID.None("city"));
		player.cities.Add(city);

		Tile workedTile = new(ID.None("tile")) {
			overlayTerrainType = new TerrainType() {
				baseShieldProduction = shields,
				baseCommerceProduction = commerce,
			},
		};
		city.residents.Add(new CityResident() { tileWorked = workedTile, citizenType = new CitizenType() });
		return city;
	}

	private static Building MakeBuilding(SaveBuilding.Flag? flag = null, int productionBonusPercent = 0) {
		SaveBuilding sb = new() { name = "Test building", productionBonusPercent = productionBonusPercent };
		if (flag is SaveBuilding.Flag f) {
			sb.flags.Add(f);
		}
		return new Building(sb, new C7GameData.GameData());
	}

	[Fact]
	public void LibraryAddsHalfToResearch() {
		City city = MakeCity(shields: 0, commerce: 10);
		int before = city.CurrentCommerceYieldRaw().beakers;
		Assert.True(before > 1);

		city.AddBuilding(MakeBuilding(SaveBuilding.Flag.Plus50PercentResearch));

		Assert.Equal(before + before / 2, city.CurrentCommerceYieldRaw().beakers);
	}

	[Fact]
	public void ResearchWondersStackWithLibraries() {
		City city = MakeCity(shields: 0, commerce: 10);
		int before = city.CurrentCommerceYieldRaw().beakers;

		// Library, university, Copernicus and Newton's: +50% +50% +100% +100%.
		city.AddBuilding(MakeBuilding(SaveBuilding.Flag.Plus50PercentResearch));
		city.AddBuilding(MakeBuilding(SaveBuilding.Flag.Plus50PercentResearch));
		city.AddBuilding(MakeBuilding(SaveBuilding.Flag.DoublesResearchOutput));
		city.AddBuilding(MakeBuilding(SaveBuilding.Flag.DoublesResearchOutput));

		Assert.Equal(before * 4, city.CurrentCommerceYieldRaw().beakers);
	}

	[Fact]
	public void MarketplaceAddsHalfToTaxOnly() {
		City city = MakeCity(shields: 0, commerce: 10);
		CommerceBreakdown before = city.CurrentCommerceYieldRaw();
		Assert.True(before.taxes > 1 && before.happiness > 1);

		city.AddBuilding(MakeBuilding(SaveBuilding.Flag.Plus50PercentCommerce));
		CommerceBreakdown after = city.CurrentCommerceYieldRaw();

		Assert.Equal(before.taxes + before.taxes / 2, after.taxes);
		Assert.Equal(before.happiness, after.happiness);
		Assert.Equal(before.beakers, after.beakers);
	}

	[Fact]
	public void LuxuryFlagAddsHalfToLuxury() {
		City city = MakeCity(shields: 0, commerce: 10);
		CommerceBreakdown before = city.CurrentCommerceYieldRaw();

		city.AddBuilding(MakeBuilding(SaveBuilding.Flag.Plus50PercentLuxury));
		CommerceBreakdown after = city.CurrentCommerceYieldRaw();

		Assert.Equal(before.happiness + before.happiness / 2, after.happiness);
		Assert.Equal(before.taxes, after.taxes);
	}

	[Fact]
	public void FactoryAddsItsPercentageToProduction() {
		City city = MakeCity(shields: 8, commerce: 0);
		int before = city.CurrentProductionYield().useful;
		Assert.True(before > 1);

		city.AddBuilding(MakeBuilding(productionBonusPercent: 50));

		Assert.Equal(before + before / 2, city.CurrentProductionYield().useful);
	}
}

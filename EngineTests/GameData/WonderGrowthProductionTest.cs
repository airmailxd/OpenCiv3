using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData;

// Wonder and building effects on city growth, production and income:
// Longevity, power plants (and the Hoover Dam), the Iron Works and tourism.
public class WonderGrowthProductionTest {
	private static City MakeCity(int shields) {
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
			},
		};
		city.residents.Add(new CityResident() { tileWorked = workedTile, citizenType = new CitizenType() });
		return city;
	}

	private static Building MakeBuilding(string name, int productionBonusPercent = 0, Building requiredBuilding = null,
			params SaveBuilding.Flag[] flags) {
		SaveBuilding sb = new() { name = name, productionBonusPercent = productionBonusPercent };
		foreach (SaveBuilding.Flag f in flags) {
			sb.flags.Add(f);
		}
		return new Building(sb, new C7GameData.GameData()) { requiredBuilding = requiredBuilding };
	}

	private static Building MakeWonder(string name, params SaveBuilding.Flag[] flags) {
		SaveBuilding sb = new() { name = name, greatWonderProperties = new() };
		foreach (SaveBuilding.Flag f in flags) {
			sb.flags.Add(f);
		}
		return new Building(sb, new C7GameData.GameData());
	}

	private static Building MakePlant(Building factory, int percent = 50) {
		return MakeBuilding("Plant " + percent, percent, factory, SaveBuilding.Flag.ReplacesOtherBuildings);
	}

	[Fact]
	public void PowerPlantNeedsAFactory() {
		City city = MakeCity(shields: 8);
		Building factory = MakeBuilding("Factory", 50);
		city.AddBuilding(MakePlant(factory));
		Assert.Equal(8, city.CurrentProductionYield().useful);

		city.AddBuilding(factory);
		Assert.Equal(16, city.CurrentProductionYield().useful);
	}

	[Fact]
	public void OnlyTheBestPowerPlantCounts() {
		Building factory = MakeBuilding("Factory", 50);
		Building coal = MakePlant(factory, 50);
		Building nuclear = MakePlant(factory, 100);
		Building manufacturing = MakeBuilding("Manufacturing Plant", 50, factory);
		City city = MakeCity(shields: 8);
		city.constructed_buildings.Add(new CityBuilding() { building = factory });
		city.constructed_buildings.Add(new CityBuilding() { building = coal });
		city.constructed_buildings.Add(new CityBuilding() { building = nuclear });
		city.constructed_buildings.Add(new CityBuilding() { building = manufacturing });

		// Factory 50 + manufacturing plant 50 + the nuclear plant's 100.
		Assert.Equal(200, City.ProductionBonusPercent(city.constructed_buildings));
	}

	[Fact]
	public void BuildingAPowerPlantReplacesTheOldOne() {
		City city = MakeCity(shields: 8);
		Building factory = MakeBuilding("Factory", 50);
		Building coal = MakePlant(factory, 50);
		Building nuclear = MakePlant(factory, 100);
		city.AddBuilding(factory);
		city.AddBuilding(coal);
		city.AddBuilding(nuclear);

		Assert.Contains(city.constructed_buildings, cb => cb.building == factory);
		Assert.Contains(city.constructed_buildings, cb => cb.building == nuclear);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == coal);
	}

	[Fact]
	public void HooverDamPlantOnlyHelpsCitiesWithAFactory() {
		City city = MakeCity(shields: 8);
		Building factory = MakeBuilding("Factory", 50);
		Building hydro = MakePlant(factory);
		Building hoover = MakeWonder("Hoover Dam");
		hoover.greatWonderProperties.buildingGainedInEveryCityOnContinent = hydro;
		city.AddBuilding(hoover);

		Assert.True(city.HasEffectiveBuilding(hydro));
		Assert.Equal(8, city.CurrentProductionYield().useful);

		city.AddBuilding(factory);
		Assert.Equal(16, city.CurrentProductionYield().useful);

		// A built coal plant on top of the dam's hydro plant adds nothing.
		city.AddBuilding(MakePlant(factory, 50));
		Assert.Equal(16, city.CurrentProductionYield().useful);
	}

	[Fact]
	public void IronWorksDoublesProduction() {
		City city = MakeCity(shields: 8);
		SaveBuilding sb = new() { name = "Iron Works", isSmallWonder = true, productionBonusPercent = 100, pollution = 4 };
		city.AddBuilding(new Building(sb, new C7GameData.GameData()));

		Assert.Equal(16, city.CurrentProductionYield().useful);
		Assert.Equal(4, city.BuildingPollution());
	}

	private static City MakeGrowingCity(bool longevity, int size, int maxLevel1 = 6) {
		C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty() };
		gameData.citizenTypes.Add(new CitizenType() { IsDefaultCitizen = true });
		// With no tiles to work, new citizens become specialists.
		gameData.citizenTypes.Add(new CitizenType() { IsDefaultCitizen = false });
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);
		Player player = new() { isHuman = true };
		player.civilization = new Civilization();
		player.government = new Government();
		player.rules = new() {
			MaximumLevel1CitySize = maxLevel1,
			MaximumLevel2CitySize = 12,
			FoodNeededToGrowForLevel1Cities = 20,
			FoodNeededToGrowForLevel2Cities = 20,
			FoodNeededToGrowForLevel3Cities = 20,
		};
		gameData.rules = player.rules;

		// With no workable tiles (rank 0), new citizens only need the city
		// tile's map for the distance check.
		Tile tile = new(ID.None("tile")) { map = new GameMap() { numTilesWide = 10, numTilesTall = 10 } };
		City city = new(tile, player, "Gotham", ID.None("city"));
		tile.cityAtTile = city;
		player.cities.Add(city);
		for (int i = 0; i < size; ++i) {
			city.residents.Add(new CityResident() { city = city, citizenType = gameData.citizenTypes[0] });
		}
		city.foodStored = 1000;
		if (longevity) {
			city.AddBuilding(MakeWonder("Longevity", SaveBuilding.Flag.DoublesCityGrowthEverywhere));
		}
		return city;
	}

	[Fact]
	public void CitiesGrowByOneWithoutLongevity() {
		City city = MakeGrowingCity(longevity: false, size: 2);
		city.HandleCityGrowth(C7Engine.EngineStorage.gameData);
		Assert.Equal(3, city.residents.Count);
		Assert.Equal(0, city.foodStored);
	}

	[Fact]
	public void LongevityGrowsCitiesByTwo() {
		City city = MakeGrowingCity(longevity: true, size: 2);
		city.HandleCityGrowth(C7Engine.EngineStorage.gameData);
		Assert.Equal(4, city.residents.Count);
		Assert.Equal(0, city.foodStored);
	}

	[Fact]
	public void LongevityStillNeedsRoomForTheSecondCitizen() {
		// A town one short of the size cap, with no fresh water or aqueduct.
		City city = MakeGrowingCity(longevity: true, size: 5, maxLevel1: 6);
		city.HandleCityGrowth(C7Engine.EngineStorage.gameData);
		Assert.Equal(6, city.residents.Count);
	}

	[Theory]
	[InlineData(999, 0)]
	[InlineData(1000, 2)]
	[InlineData(1500, 2)]
	[InlineData(1501, 4)]
	[InlineData(1750, 4)]
	[InlineData(1800, 6)]
	[InlineData(2000, 8)]
	[InlineData(2100, 10)]
	[InlineData(2500, 12)]
	[InlineData(4000, 14)]
	public void TourismFollowsTheCivilopediaTable(int years, int gold) {
		Assert.Equal(gold, City.TourismGoldForAge(years));
	}

	[Fact]
	public void OldTouristWondersEarnGold() {
		City city = MakeCity(shields: 1);
		city.owner.rules = new Rules();
		// With no calendar the current year is 0.
		Building pyramids = MakeWonder("Pyramids", SaveBuilding.Flag.TouristAttraction);
		Building young = MakeWonder("Young Wonder", SaveBuilding.Flag.TouristAttraction);
		Building noTourists = MakeWonder("Dull Wonder");
		city.constructed_buildings.Add(new CityBuilding() { building = pyramids, year = -3000 });
		city.constructed_buildings.Add(new CityBuilding() { building = young, year = -500 });
		city.constructed_buildings.Add(new CityBuilding() { building = noTourists, year = -3000 });

		Assert.Equal(14, city.TourismGold());

		PlayerCommerceBreakdown flows = city.owner.AggregateFlows();
		Assert.Equal(14, flows.tourism);
		Assert.Equal(flows.Inflows() - flows.Outflows(), flows.Netflows());
	}
}

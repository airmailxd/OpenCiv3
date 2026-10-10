using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Which small wonders (and the palace) a city may build, and what happens to
// them when the city changes hands.
public class SmallWonderTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public SmallWonderTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	// Captures send messages to the UI; don't leave them for other tests.
	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private Building BuildingNamed(string name) {
		return gameData.Buildings.Single(b => b.name == name);
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	private void Learn(Building building) {
		if (building.requiredTech != null) {
			us.knownTechs.Add(building.requiredTech.id);
		}
	}

	private Resource ResourceNamed(string name) {
		return gameData.Resources.Single(r => r.Key == name);
	}

	// Both ways of checking: the production list (which shares facts about
	// the city between buildings) and a lone CanProduce.
	private void AssertCanProduce(bool expected, Building building, City city, HashSet<Resource> resources = null) {
		Assert.Equal(expected, building.CanProduce(city, resources ?? []));
		if (resources == null) {
			Assert.Equal(expected, city.ListProductionOptions(gameData).Contains(building));
		}
	}

	[Theory]
	[InlineData("Wall Street", "Stock Exchange")]
	[InlineData("Battlefield Medicine", "Hospital")]
	[InlineData("Strategic Missile Defense", "SAM Missile Battery")]
	public void NeedsFiveCitiesWithTheRequiredBuilding(string wonderName, string requiredName) {
		Building wonder = BuildingNamed(wonderName);
		Building required = BuildingNamed(requiredName);
		Assert.Equal(required, wonder.requiredBuilding);
		Assert.Equal(5, wonder.requiredBuildingCount);
		Learn(wonder);

		List<City> cities = Enumerable.Range(0, 6).Select(_ => BuildCity(us)).ToList();
		City builder = cities[5];
		for (int i = 0; i < 4; ++i) {
			cities[i].AddBuilding(required);
		}
		AssertCanProduce(false, wonder, builder);

		// The fifth can be anywhere; the building city needn't have one.
		cities[4].AddBuilding(required);
		AssertCanProduce(true, wonder, builder);
		AssertCanProduce(true, wonder, cities[0]);

		// Another civ's buildings don't count.
		Assert.False(wonder.CanProduce(BuildCity(them), []));
	}

	[Fact]
	public void OrdinaryRequiredBuildingsMustBeInTheCity() {
		Building bank = BuildingNamed("Bank");
		Assert.True(bank.requiredBuildingCount <= 1);
		Learn(bank);
		City withMarket = BuildCity(us);
		City without = BuildCity(us);
		withMarket.AddBuilding(bank.requiredBuilding);

		AssertCanProduce(true, bank, withMarket);
		AssertCanProduce(false, bank, without);
	}

	[Fact]
	public void SecretPoliceHqIsImportedWithItsGovernment() {
		Building hq = BuildingNamed("Secret Police HQ");
		Assert.True(hq.isSmallWonder);
		Assert.True(hq.isForbiddenPalace);
		Assert.Equal("Communism", hq.requiredGovernment?.name);
		Assert.Null(BuildingNamed("Forbidden Palace").requiredGovernment);
	}

	[Fact]
	public void SecretPoliceHqNeedsCommunism() {
		Building hq = BuildingNamed("Secret Police HQ");
		Learn(hq);
		BuildCity(us);
		City city = BuildCity(us);
		us.government = gameData.governments.Single(g => g.name == "Monarchy");
		AssertCanProduce(false, hq, city);

		us.government = gameData.governments.Single(g => g.name == "Communism");
		AssertCanProduce(true, hq, city);
	}

	[Fact]
	public void SecretPoliceHqIsASecondForbiddenPalace() {
		Building hq = BuildingNamed("Secret Police HQ");
		Building fp = BuildingNamed("Forbidden Palace");
		Learn(hq);
		Learn(fp);
		us.government = gameData.governments.Single(g => g.name == "Communism");
		BuildCity(us);
		City fpCity = BuildCity(us);
		City other = BuildCity(us);
		fpCity.AddBuilding(fp);

		// Both can be had at once, but not in the same city.
		AssertCanProduce(true, hq, other);
		AssertCanProduce(false, hq, fpCity);
	}

	[Fact]
	public void ForbiddenPalaceIsNotBuiltInTheCapital() {
		Building fp = BuildingNamed("Forbidden Palace");
		Learn(fp);
		City capital = BuildCity(us);
		City other = BuildCity(us);
		Assert.True(capital.IsCapital());

		AssertCanProduce(false, fp, capital);
		AssertCanProduce(true, fp, other);

		// One per civ.
		other.AddBuilding(fp);
		AssertCanProduce(false, fp, BuildCity(us));
	}

	[Fact]
	public void SmallWondersAreBuiltInOneCityAtATime() {
		Building fp = BuildingNamed("Forbidden Palace");
		Learn(fp);
		BuildCity(us);
		City a = BuildCity(us);
		City b = BuildCity(us);
		a.SetItemBeingProduced(fp);

		AssertCanProduce(true, fp, a);
		AssertCanProduce(false, fp, b);
	}

	[Theory]
	[InlineData("Iron Works")]
	[InlineData("Apollo Program")]
	[InlineData("Intelligence Agency")]
	[InlineData("Heroic Epic")]
	public void ConquestsSmallWondersAreBuildable(string name) {
		Building wonder = BuildingNamed(name);
		Assert.True(wonder.isSmallWonder);
		Learn(wonder);
		us.hasVictoriousArmy = true;
		City city = BuildCity(us);

		Assert.True(wonder.CanProduce(city, wonder.requiredResources.ToHashSet()));
		if (wonder.requiredResources.Count > 0) {
			Assert.False(wonder.CanProduce(city, []));
		}
	}

	[Fact]
	public void IronWorksNeedsIronAndCoal() {
		Building ironWorks = BuildingNamed("Iron Works");
		Resource iron = ResourceNamed("Iron");
		Resource coal = ResourceNamed("Coal");
		Assert.Equal(new HashSet<Resource> { iron, coal }, ironWorks.requiredResources);
		City city = BuildCity(us);

		Assert.False(ironWorks.CanProduce(city, [iron]));
		Assert.True(ironWorks.CanProduce(city, [iron, coal]));
	}

	[Fact]
	public void SomeBuildingsNeedTheirResourcesInTheCityRadius() {
		Building ironWorks = BuildingNamed("Iron Works");
		Assert.False(ironWorks.goodsMustBeInCityRadius);
		Resource iron = ResourceNamed("Iron");
		Resource coal = ResourceNamed("Coal");
		City city = BuildCity(us);
		ironWorks.goodsMustBeInCityRadius = true;

		Assert.False(ironWorks.CanProduce(city, [iron, coal]));

		List<Tile> tiles = city.GetWorkableTiles();
		tiles[0].Resource = iron;
		tiles[1].Resource = coal;
		Assert.True(ironWorks.CanProduce(city, [iron, coal]));
	}

	[Fact]
	public void ThePalaceCanBeRebuiltToMoveTheCapital() {
		Building palace = gameData.Buildings.Single(b => b.isCenterOfEmpire);
		Learn(palace);
		// The AI is never offered the palace (see AiPalaceTest); this is
		// the rule for a human.
		us.isHuman = true;
		City capital = BuildCity(us);
		City other = BuildCity(us);

		AssertCanProduce(false, palace, capital);
		AssertCanProduce(true, palace, other);

		// Not in a city with a Forbidden Palace.
		City fpCity = BuildCity(us);
		fpCity.AddBuilding(BuildingNamed("Forbidden Palace"));
		AssertCanProduce(false, palace, fpCity);

		other.SetItemBeingProduced(palace);
		other.SetStoredShields(us.ShieldCost(palace));
		other.HandleCityProduction(gameData);

		Assert.True(other.IsCapital());
		Assert.False(capital.IsCapital());
		Assert.Contains(other.constructed_buildings, cb => cb.building == palace);
		Assert.DoesNotContain(capital.constructed_buildings, cb => cb.building == palace);
		Assert.Single(us.cities, c => c.IsCapital());
	}

	[Fact]
	public void CapturedCitiesLoseTheirSmallWonders() {
		BuildCity(us);
		BuildCity(them);
		City city = BuildCity(them);
		while (city.residents.Count < 3) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
			});
		}
		city.AddBuilding(BuildingNamed("Forbidden Palace"));
		city.AddBuilding(BuildingNamed("Heroic Epic"));

		CityInteractions.CaptureCity(city, us);

		Assert.Equal(us, city.owner);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building.isSmallWonder);
	}
}

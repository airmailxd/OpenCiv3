using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Civ3 rules fixed after a review: hurrying, wonder costs, empire size
// unhappiness, resistance, corruption, research and so on.
public class RulesReviewFixTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public RulesReviewFixTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
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

	// ---- Hurrying ----

	[Fact]
	public void WondersCannotBeHurried() {
		City city = BuildCity(us);
		us.gold = 100000;
		us.government.hurryingType = Government.HurryProductionType.PaidLabor;

		city.SetItemBeingProduced(BuildingNamed("The Pyramids"));
		city.SetStoredShields(10);
		Assert.NotNull(city.GetHurryProductionDetails().errorMessage);

		city.SetItemBeingProduced(BuildingNamed("Heroic Epic"));
		Assert.NotNull(city.GetHurryProductionDetails().errorMessage);

		us.government.hurryingType = Government.HurryProductionType.ForcedLabor;
		Assert.NotNull(city.GetHurryProductionDetails().errorMessage);
	}

	[Fact]
	public void BuyingAnImprovementCostsTwoGoldAShield() {
		City city = BuildCity(us);
		us.gold = 100000;
		us.government.hurryingType = Government.HurryProductionType.PaidLabor;
		Building temple = BuildingNamed("Temple");
		city.SetItemBeingProduced(temple);
		city.SetStoredShields(10);

		int remaining = us.ShieldCost(temple) - 10;
		Assert.Equal(2 * remaining, city.GetHurryProductionDetails().goldCost);

		// Nothing built yet: double.
		city.SetStoredShields(0);
		Assert.Equal(4 * us.ShieldCost(temple), city.GetHurryProductionDetails().goldCost);
	}

	[Fact]
	public void BuyingAUnitCostsMoreForBigUnits() {
		City city = BuildCity(us);
		us.gold = 100000;
		us.government.hurryingType = Government.HurryProductionType.PaidLabor;
		UnitPrototype settler = Prototype("Settler");
		city.SetItemBeingProduced(settler);
		city.SetStoredShields(1);

		int s = us.ShieldCost(settler) - 1;
		Assert.Equal(2 * s + s * s / 20, city.GetHurryProductionDetails().goldCost);
	}

	// ---- Gold ----

	[Fact]
	public void OverspendingEmptiesTheTreasuryInsteadOfThrowing() {
		us.gold = 10;
		us.gold -= 25;
		Assert.Equal(0, us.gold);

		us.SetGold(10);
		us.SetGold(-30, add: true);
		Assert.Equal(0, us.gold);
	}

	// ---- Research ----

	[Fact]
	public void UnfundedTurnsDontCountTowardsTheMaximumResearchTime() {
		Tech tech = us.GetAvailableTechsToResearch(gameData.techs).First();
		us.freeTechsRemaining = 0;
		us.SetCurrentlyResearchedTech(tech.id);
		us.taxRate = 10;
		us.scienceRate = 0;
		us.luxuryRate = 0;

		for (int i = 0; i < 3; ++i) {
			us.DoPerTurnFinanceUpdates(gameData);
			us.DoPerTurnScienceUpdates(gameData);
		}
		Assert.Equal(0, us.turnsResearched);
		Assert.Equal(tech.id, us.currentlyResearchedTech);
	}

	// ---- Specialists ----

	// Turns the city's only citizen into the given specialist.
	private static void MakeSpecialist(City city, CitizenType type) {
		CityResident resident = city.residents[0];
		resident.tileWorked.personWorkingTile = null;
		resident.tileWorked = Tile.NONE;
		resident.citizenType = type;
	}

	[Fact]
	public void SpecialistsProduceNothingInDisorder() {
		City city = BuildCity(us);
		MakeSpecialist(city, gameData.citizenTypes.First(c => c.Research > 0));
		Assert.True(city.CurrentCommerceYieldRaw().beakers > 0);

		city.isInCivilDisorder = true;
		CommerceBreakdown commerce = city.CurrentCommerceYieldRaw();
		Assert.Equal(0, commerce.beakers);
		Assert.Equal(0, commerce.taxes);
	}

	[Fact]
	public void PolicemenWinBackWaste() {
		City city = BuildCity(us);
		CitizenType entertainer = gameData.citizenTypes.First(c => c.Luxuries > 0);
		CitizenType policeman = gameData.citizenTypes.First(c => c.Corruption > 0);
		MakeSpecialist(city, entertainer);
		city.corruption = 0.9f;
		CorruptableValue withoutPolice = city.CurrentProductionYield();

		city.residents[0].citizenType = policeman;
		CorruptableValue withPolice = city.CurrentProductionYield();
		Assert.Equal(System.Math.Max(0, withoutPolice.corrupt - policeman.Corruption), withPolice.corrupt);
	}

	// ---- Empire size ----

	[Fact]
	public void SprawlingEmpiresHaveFewerContentCitizens() {
		Assert.Equal(0, us.EmpireSizeUnhappiness(gameData));

		// Shrink the map's optimal number so a couple of cities is too many.
		gameData.map.optimalNumberOfCities = 1;
		BuildCity(us);
		BuildCity(us);
		Assert.True(us.EmpireSizeUnhappiness(gameData) >= 1);
	}

	// ---- Trait discounts ----

	[Fact]
	public void TraitDiscountSkipsWonders() {
		HashSet<Civilization.Trait> religious = [Civilization.Trait.Religious];
		Building temple = BuildingNamed("Temple");
		Building oracle = BuildingNamed("The Oracle");
		Building forbiddenPalace = BuildingNamed("Forbidden Palace");
		Assert.Equal((int)(temple.shieldCost * gameData.rules.BuildingDiscountForCivTraits), temple.ShieldCost(religious, 1));
		Assert.Equal(oracle.shieldCost, oracle.ShieldCost(religious, 1));
		Assert.Equal(forbiddenPalace.shieldCost, forbiddenPalace.ShieldCost(religious, 1));
	}
}

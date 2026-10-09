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

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

	// Civ3 charges the BIQ's shield value in gold (4) for every shield still
	// needed, units and improvements alike, and double from an empty box.
	[Fact]
	public void BuyingAnImprovementCostsTheShieldValueInGoldAShield() {
		City city = BuildCity(us);
		us.gold = 100000;
		us.government.hurryingType = Government.HurryProductionType.PaidLabor;
		Building temple = BuildingNamed("Temple");
		city.SetItemBeingProduced(temple);
		city.SetStoredShields(10);

		int remaining = us.ShieldCost(temple) - 10;
		Assert.Equal(4, gameData.rules.ShieldValueInGold);
		Assert.Equal(4 * remaining, city.GetHurryProductionDetails().goldCost);

		// Nothing built yet: double.
		city.SetStoredShields(0);
		Assert.Equal(8 * us.ShieldCost(temple), city.GetHurryProductionDetails().goldCost);
	}

	[Fact]
	public void BuyingAUnitCostsTheSameAShieldAsAnImprovement() {
		City city = BuildCity(us);
		us.gold = 100000;
		us.government.hurryingType = Government.HurryProductionType.PaidLabor;
		UnitPrototype settler = Prototype("Settler");
		city.SetItemBeingProduced(settler);
		city.SetStoredShields(1);

		int s = us.ShieldCost(settler) - 1;
		Assert.Equal(gameData.rules.ShieldValueInGold * s, city.GetHurryProductionDetails().goldCost);
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
	public void SpecialistsStillWorkUnderAnarchy() {
		City city = BuildCity(us);
		CitizenType scientist = gameData.citizenTypes.First(c => c.Research > 0);
		MakeSpecialist(city, scientist);

		// The city's commerce is lost, but not the scientist's beakers.
		Government government = us.government;
		try {
			us.government = gameData.governments.First(g => g.transitionType);
			Assert.Equal(scientist.Research, city.CurrentCommerceYieldRaw().beakers);
		} finally {
			us.government = government;
		}
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

	[Fact]
	public void CivilEngineersOnlyHelpBuildings() {
		City city = BuildCity(us);
		CitizenType entertainer = gameData.citizenTypes.First(c => c.Luxuries > 0);
		CitizenType engineer = new() { SingularName = "Civil Engineer", Construction = 2 };
		MakeSpecialist(city, entertainer);
		city.corruption = 0;
		city.SetItemBeingProduced(BuildingNamed("Temple"));
		int without = city.CurrentProductionYield().useful;

		city.residents[0].citizenType = engineer;
		Assert.Equal(without + 2, city.CurrentProductionYield().useful);

		city.SetItemBeingProduced(Prototype("Warrior"));
		Assert.Equal(without, city.CurrentProductionYield().useful);
	}

	[Fact]
	public void TheSecretPoliceOnlyWorkUnderTheirGovernment() {
		Building forbiddenPalace = BuildingNamed("Forbidden Palace");
		Assert.True(forbiddenPalace.WorksAsForbiddenPalaceFor(us));

		Government other = gameData.governments.First(g => g.id != us.government.id);
		Government? required = forbiddenPalace.requiredGovernment;
		try {
			forbiddenPalace.requiredGovernment = other;
			Assert.False(forbiddenPalace.WorksAsForbiddenPalaceFor(us));
			forbiddenPalace.requiredGovernment = us.government;
			Assert.True(forbiddenPalace.WorksAsForbiddenPalaceFor(us));
		} finally {
			forbiddenPalace.requiredGovernment = required;
		}
	}

	// ---- Resistance ----

	// A city of `them`'s with four of their citizens, taken by us.
	private City CapturedCity() {
		// A second city keeps them in the game.
		BuildCity(them);
		City city = BuildCity(them);
		for (int i = 1; i < 4; ++i) {
			city.AddCitizen(new CityResident() {
				city = city,
				nationality = them.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			});
		}
		CityInteractions.CaptureCity(city, us);
		// Who resists is random; make it everyone.
		city.resisters = city.residents.Count;
		city.resistanceFrom = them;
		return city;
	}

	[Fact]
	public void ConqueredCitiesResist() {
		City city = CapturedCity();
		Assert.True(city.IsInResistance);
		Assert.Equal(them, city.resistanceFrom);

		// Resisters work no tile and eat nothing.
		Assert.Equal(0, city.FoodConsumedPerTurn());
		Assert.Empty(city.WorkingResidents());
		int resisting = city.residents.Count;
		city.resisters = 0;
		Assert.Equal(2 * resisting, city.FoodConsumedPerTurn());
		Assert.Equal(resisting, city.WorkingResidents().Count());
	}

	[Fact]
	public void AnUngarrisonedCityGoesOnResisting() {
		City city = CapturedCity();
		int resisters = city.resisters;
		foreach (MapUnit u in city.location.unitsOnTile.ToList()) {
			u.RemoveFromPlay();
		}
		for (int i = 0; i < 10; ++i) {
			city.UpdateResistance(gameData);
		}
		Assert.Equal(resisters, city.resisters);
	}

	[Fact]
	public void ResistanceFollowsTheCultureComparison() {
		City city = CapturedCity();
		// Without any culture of our own, they are disdainful of us: 90%,
		// less 10% for carrying on resisting (both despotisms here).
		foreach (City c in us.cities) {
			c.perPlayerCulture[us] = 0;
		}
		Assert.Equal(us.government, them.government);
		Assert.Equal(90, city.ResistancePercent(them, continuing: false));
		Assert.Equal(80, city.ResistancePercent(them, continuing: true));

		// With three times their culture we hold them in awe: 40%.
		int theirs = them.cities.Sum(c => c.GetCulture());
		us.cities[0].perPlayerCulture[us] = 3 * System.Math.Max(1, theirs);
		Assert.Equal(40, city.ResistancePercent(them, continuing: false));
	}

	[Fact]
	public void ResistanceSurvivesASave() {
		City city = CapturedCity();
		int resisters = city.resisters;

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);
		City loadedCity = loaded.cities.Single(c => c.id == city.id);
		Assert.Equal(resisters, loadedCity.resisters);
		Assert.Equal(them.id, loadedCity.resistanceFrom.id);
	}

	// Per the project owner, resistance goes on after the old owner is
	// destroyed.
	[Fact]
	public void ResistanceGoesOnAfterTheOldOwnerIsGone() {
		City city = CapturedCity();
		int resisters = city.resisters;
		them.defeated = true;
		foreach (MapUnit u in city.location.unitsOnTile.ToList()) {
			u.RemoveFromPlay();
		}
		city.UpdateResistance(gameData);
		Assert.Equal(resisters, city.resisters);
		Assert.Equal(them, city.resistanceFrom);
	}

	// Makes every random roll come out as 0, so every chance comes true.
	private class ZeroRandom : System.Random {
		protected override double Sample() => 0.0;
	}

	private void AddResident(City city, Civilization nationality) {
		city.AddCitizen(new CityResident() {
			city = city,
			nationality = nationality,
			citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
		});
	}

	// Per the project owner, every citizen not of the captor's nationality
	// may resist, not only the old owner's.
	[Fact]
	public void EveryForeignCitizenMayResist() {
		Player third = gameData.players.First(p => !p.isBarbarians && p != us && p != them);
		BuildCity(them);
		City city = BuildCity(them);
		AddResident(city, third.civilization);
		AddResident(city, us.civilization);
		AddResident(city, us.civilization);
		AddResident(city, them.civilization);
		// Capturing takes the last citizen.
		AddResident(city, them.civilization);
		CityInteractions.CaptureCity(city, us);
		Assert.Equal(5, city.residents.Count);

		gameData.random = new ZeroRandom();
		city.StartResistance(them);
		// Two of theirs and one of the third civ's; ours don't.
		Assert.Equal(3, city.resisters);
		Assert.All(city.WorkingResidents(), r => Assert.Equal(us.civilization, r.nationality));
		Assert.All(city.residents.Skip(2), r => Assert.NotEqual(us.civilization, r.nationality));

		// A new citizen doesn't resist.
		AddResident(city, us.civilization);
		Assert.Equal(3, city.resisters);
		Assert.Equal(3, city.WorkingResidents().Count());
		Assert.All(city.WorkingResidents(), r => Assert.Equal(us.civilization, r.nationality));
	}

	// Per the project owner, a civ's last city doesn't resist when taken.
	[Fact]
	public void ALastCityDoesNotResist() {
		foreach (City c in them.cities.ToList()) {
			CityInteractions.DestroyCity(c);
		}
		City city = BuildCity(them);
		AddResident(city, them.civilization);
		AddResident(city, them.civilization);
		AddResident(city, them.civilization);
		CityInteractions.CaptureCity(city, us);
		Assert.Empty(them.cities);

		gameData.random = new ZeroRandom();
		city.StartResistance(them);
		Assert.False(city.IsInResistance);
	}

	// ---- Difficulty ----

	[Fact]
	public void HumansGetTheDifficultyBonusAgainstBarbarians() {
		Player human = gameData.players.First(p => p.isHuman);
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		Tile tile = gameData.map.tiles.First(IsEmptyLand);
		MapUnit ours = gameData.SpawnUnit(human, Prototype("Warrior"), tile);
		MapUnit theirs = gameData.SpawnUnit(barbarians, Prototype("Warrior"), gameData.map.tiles.Where(IsEmptyLand).Skip(1).First());
		MapUnit aiUnit = gameData.SpawnUnit(us, Prototype("Warrior"), tile);

		gameData.gameDifficulty.AttackBonusAgainstBarbarians = 100;
		Assert.Contains(ours.ListStrengthBonusesVersus(theirs, CombatRole.Attack, null), b => b.amount == 1.0);
		Assert.DoesNotContain(aiUnit.ListStrengthBonusesVersus(theirs, CombatRole.Attack, null), b => b.amount == 1.0);
		// It counts when defending too, but not in bombardment.
		Assert.Contains(ours.ListStrengthBonusesVersus(theirs, CombatRole.Defense, null), b => b.amount == 1.0);
		Assert.DoesNotContain(ours.ListStrengthBonusesVersus(theirs, CombatRole.BombardDefense, null), b => b.amount == 1.0);
	}

	// ---- Workers ----

	[Fact]
	public void WorkersWorkAtTheGovernmentRateTimesTheirStrength() {
		us.civilization.traits.Remove(Civilization.Trait.Industrious);
		Tile tile = gameData.map.tiles.First(IsEmptyLand);
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), tile);

		us.government.workerRate = 3;
		Assert.Equal(3f, worker.workerSpeed());

		worker.unitType.workerStrength = 2;
		try {
			Assert.Equal(6f, worker.workerSpeed());
		} finally {
			worker.unitType.workerStrength = 1;
		}

		// A tech like Replaceable Parts doubles it.
		Tech tech = gameData.techs.First(t => !us.knownTechs.Contains(t.id));
		tech.DoublesWorkerRate = true;
		us.knownTechs.Add(tech.id);
		Assert.Equal(6f, worker.workerSpeed());
	}

	// ---- Anarchy ----

	[Fact]
	public void AnarchyFollowsTheConquestsFormula() {
		gameData.gameDifficulty.MaxAiGovernmentTransitionTime = 0;
		us.civilization.traits.Remove(Civilization.Trait.Religious);
		for (int i = 0; i < 50; ++i) {
			int turns = us.GetTurnsOfAnarchyForTransition(gameData);
			Assert.InRange(turns, 2, 9);
		}

		us.civilization.traits.Add(Civilization.Trait.Religious);
		Assert.Equal(2, us.GetTurnsOfAnarchyForTransition(gameData));
	}

	// ---- Barbarians ----

	[Fact]
	public void BarbariansPlunderGoldOrElseACitizen() {
		City city = BuildCity(us);
		for (int i = 0; i < 3; ++i) {
			city.AddCitizen(new CityResident() { city = city, nationality = us.civilization, citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen) });
		}
		us.gold = 500;
		Assert.Equal(125, city.SackedByBarbarians());
		Assert.Equal(375, us.gold);
		Assert.Equal(4, city.residents.Count);

		// Nothing to plunder: the city loses a citizen.
		us.gold = 0;
		Assert.Equal(0, city.SackedByBarbarians());
		Assert.Equal(3, city.residents.Count);
	}

	// ---- Captives ----

	[Fact]
	public void CaptivesAreKnownByNationNotName() {
		Tile tile = gameData.map.tiles.First(IsEmptyLand);
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), tile);
		Assert.False(worker.IsCaptive());

		worker.nationality = them.civilization;
		Assert.True(worker.IsCaptive());

		// A different nation with the same name is still foreign.
		worker.nationality = new Civilization(us.civilization.name);
		Assert.True(worker.IsCaptive());
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

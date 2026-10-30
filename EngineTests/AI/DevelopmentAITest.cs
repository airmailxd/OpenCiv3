using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

public sealed class DevelopmentAITest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public DevelopmentAITest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private Tech TechNamed(string name) => gameData.techs.First(t => t.Name == name);

	private Government Government(string name) => gameData.governments.First(g => g.name == name);

	private Building BuildingNamed(string name) => gameData.Buildings.First(b => b.name == name);

	// "The reserve amount is 16 gold per city plus enough to earn the AI
	// full interest income" (Flintlock, C3X thread post-16937054), the
	// latter "1000/number_of_interest_generating_wonders" (post-16787843).
	[Fact]
	public void TheStockGoldReserveIsSixteenACityPlusFullInterest() {
		Assert.Equal(16 * player.cities.Count, DevelopmentAI.StockGoldReserve(player));
		city.AddBuilding(BuildingNamed("Wall Street"));
		Assert.Equal(16 * player.cities.Count + 1000, DevelopmentAI.StockGoldReserve(player));
	}

	[Fact]
	public void UnitsAreWorthWhatTheyAreToTheCiv() {
		Civilization own = player.civilization;
		try {
			double ValueFor(string civ, string tech) {
				player.civilization = gameData.civilizations.First(c => c.name == civ);
				return DevelopmentAI.TechValue(gameData, player, TechNamed(tech), player.BeakersPerTurn());
			}
			Assert.Equal(70, ValueFor("Rome", "Iron Working") - ValueFor("Egypt", "Iron Working"), 6);
			// The Impi is both an attacker and a defender.
			Assert.Equal(134, ValueFor("Zululand", "Bronze Working") - ValueFor("Egypt", "Bronze Working"), 6);
		} finally {
			player.civilization = own;
		}
	}

	// alexman: "Other tech: 1" is a point for each tech that has this one as
	// a direct prerequisite.
	[Fact]
	public void EachTechItLeadsToAddsAPoint() {
		Tech bronze = TechNamed("Bronze Working");
		double before = DevelopmentAI.TechValue(gameData, player, bronze, player.BeakersPerTurn());
		Tech extra = new() { id = ID.FromString("tech-9999"), Name = "Extra", Cost = 10, EraCivilopediaName = bronze.EraCivilopediaName };
		extra.Prerequisites.Add(bronze);
		gameData.techs.Add(extra);
		try {
			double after = DevelopmentAI.TechValue(gameData, player, bronze, player.BeakersPerTurn());
			Assert.Equal(bronze.RequiredForEraAdvancement ? 1 : 1 / 1.5, after - before, 6);
		} finally {
			gameData.techs.Remove(extra);
		}
	}

	[Fact]
	public void Civ3ResearchPicksAnAvailableTech() {
		List<Tech> available = player.GetAvailableTechsToResearch(gameData.techs).ToList();
		Assert.Contains(DevelopmentAI.ChooseResearchAsCiv3(player, available), available);
	}

	private void StartTemple(int shields) {
		city.SetItemBeingProduced(BuildingNamed("Temple"));
		city.SetStoredShields(shields);
	}

	private UnitPrototype UnitNamed(string name) => gameData.unitPrototypes.First(p => p.name == name);

	private void StartUnit(string name, int shields) {
		city.SetItemBeingProduced(UnitNamed(name));
		city.SetStoredShields(shields);
	}

	// A guarded city building a worker, which is wanted: the player has
	// fewer workers than cities, and the city a citizen to spare.
	private void StartWantedWorker(int shields) {
		gameData.SpawnUnit(player, UnitNamed("Warrior"), city.location);
		player.units.RemoveAll(u => u.unitType.isWorker);
		city.AddCitizen(new CityResident { city = city, nationality = player.civilization, citizenType = gameData.citizenTypes.First(c => c.IsDefaultCitizen) });
		StartUnit("Worker", shields);
	}

	[Fact]
	public void HurryBuysADefenderForAnUnguardedCity() {
		player.government = Government("Monarchy");
		player.gold = 1000;
		StartUnit("Spearman", 1);

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(player.ShieldCost(city.itemBeingProduced), city.shieldsStored);
		Assert.True(player.gold < 1000);
	}

	[Fact]
	public void HurryBuysAnUnguardedCitysDefenderEvenFromAnEmptyBox() {
		player.government = Government("Monarchy");
		player.gold = 1000;
		StartUnit("Spearman", 0);

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(player.ShieldCost(city.itemBeingProduced), city.shieldsStored);
	}

	[Fact]
	public void HurryBuysAWorkerWhenShortOfThem() {
		player.government = Government("Monarchy");
		player.gold = 1000;
		StartWantedWorker(1);

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(player.ShieldCost(city.itemBeingProduced), city.shieldsStored);
	}

	[Fact]
	public void HurryKeepsAReserve() {
		player.government = Government("Monarchy");
		StartWantedWorker(1);
		// Just enough for the worker, but nothing would be left over.
		player.gold = city.GetHurryProductionDetails().goldCost;

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(1, city.shieldsStored);
	}

	[Fact]
	public void HurryDoesNotPayDoubleForAWorker() {
		player.government = Government("Monarchy");
		player.gold = 1000;
		StartWantedWorker(0);

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(0, city.shieldsStored);
		Assert.Equal(1000, player.gold);
	}

	// Civ3's AI rushes improvements above its gold reserve (owner's
	// decision, 2026-10-21; see DevelopmentAI.IsHurriableImprovement).
	[Fact]
	public void HurryBuysImprovementsAboveTheReserve() {
		player.government = Government("Monarchy");
		player.gold = 1000;
		StartTemple(1);

		DevelopmentAI.HurryProduction(player);

		Assert.True(city.hurriedThisTurn);
		Assert.True(player.gold < 1000);
		Assert.True(player.gold >= DevelopmentAI.StockGoldReserve(player));
	}

	[Fact]
	public void HurrySacrificesNoCitizensUnderDespotism() {
		player.government = Government("Despotism");
		StartUnit("Spearman", 1);
		int residents = city.residents.Count;

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(1, city.shieldsStored);
		Assert.Equal(residents, city.residents.Count);
	}

	[Fact]
	public void HurryLeavesHumansAlone() {
		player.isHuman = true;
		try {
			player.government = Government("Monarchy");
			player.gold = 1000;
			StartUnit("Spearman", 1);

			DevelopmentAI.HurryProduction(player);

			Assert.Equal(1, city.shieldsStored);
		} finally {
			player.isHuman = false;
		}
	}

	[Fact]
	public void NoExtraTaxesForHurryingInPeacetime() {
		player.government = Government("Monarchy");
		player.gold = 0;
		Assert.False(PlayerRelationship.IsInAnyWar(player, gameData.players));

		Assert.Equal(int.MinValue, DevelopmentAI.WantedGoldPerTurn(player));
	}

	[Fact]
	public void FoodFirstAimsForMoreFoodInASmallCity() {
		Assert.True(DevelopmentAI.DesiredFoodSurplus(city, 2) > 2);
	}

	[Fact]
	public void FoodFirstLeavesHumansCitiesAlone() {
		player.isHuman = true;
		try {
			Assert.Equal(2, DevelopmentAI.DesiredFoodSurplus(city, 2));
		} finally {
			player.isHuman = false;
		}
	}

	private float WarriorScore() {
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");
		List<IProducible> options = [warrior];
		ChooseProducible.ProducibleStats stats = ChooseProducible.CalculateStats(city, player, options);
		return ChooseProducible.ScoreUnit(stats, city, player, warrior);
	}

	[Fact]
	public void LeanMilitaryStopsAddingSoldiersThatCostUpkeep() {
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");
		for (int i = 0; i < 30; ++i) {
			gameData.SpawnUnit(player, warrior, city.location);
		}
		Assert.True(player.TotalUnitsAllowedUnitsAndSupportCost().Item3 > 0);
		float before = WarriorScore();

		AiExperiments.Enable(player, DevelopmentAI.LeanMilitary);

		Assert.True(WarriorScore() < before - 10);
	}

	[Fact]
	public void LeanMilitaryStillGuardsACity() {
		AiExperiments.Enable(player, DevelopmentAI.LeanMilitary);
		Assert.DoesNotContain(city.location.unitsOnTile, u => u.CanDefendOnLand());
		Assert.Equal(0, DevelopmentAI.LeanMilitaryAdjustment(
			ChooseProducible.CalculateStats(city, player, []), gameData.unitPrototypes.First(p => p.name == "Warrior")));
	}

	// The stock AI gives workers "a point value of zero ... when it feels it
	// has enough of them": about 80-90% of its cities.
	[Fact]
	public void TheAiBuildsNoWorkerOnceItHasEnough() {
		UnitPrototype worker = gameData.unitPrototypes.First(p => p.isWorker);
		// No other units: an unescorted settler would also rule workers out.
		foreach (MapUnit u in player.units.ToList()) {
			player.units.Remove(u);
			u.location.unitsOnTile.Remove(u);
		}
		// Big enough to spare a citizen, and guarded, or it would build
		// only a defender.
		while (city.residents.Count < 3) {
			CityResident r = new() {
				city = city,
				nationality = player.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			};
			city.AddCitizen(r);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
		}
		gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Warrior"), city.location);
		Assert.Equal(1, ChooseProducible.EnoughWorkers(player));
		float WorkerScore() => ChooseProducible.ScoreUnit(ChooseProducible.CalculateStats(city, player, [worker]), city, player, worker);
		Assert.True(WorkerScore() > int.MinValue);
		gameData.SpawnUnit(player, worker, city.location);
		Assert.Equal(int.MinValue, WorkerScore());
	}

	[Theory]
	[InlineData("Steam Power", DevelopmentAI.RailroadFirst, DevelopmentAI.PhilosophyFirst)]
	[InlineData("Philosophy", DevelopmentAI.PhilosophyFirst, DevelopmentAI.RailroadFirst)]
	public void TheTechSwitchesRaiseOnlyTheirOwnTech(string techName, string own, string other) {
		Tech tech = TechNamed(techName);
		double off = DevelopmentAI.TechValue(gameData, player, tech, player.BeakersPerTurn());

		AiExperiments.Enable(player, other);
		Assert.Equal(off, DevelopmentAI.TechValue(gameData, player, tech, player.BeakersPerTurn()), 6);

		AiExperiments.Enable(player, own);
		Assert.Equal(off + DevelopmentAI.SwitchedTechBonus, DevelopmentAI.TechValue(gameData, player, tech, player.BeakersPerTurn()), 6);
	}
}

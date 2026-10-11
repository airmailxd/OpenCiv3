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

	private static HashSet<Tech> UnknownPathTo(Player p, Tech goal) {
		HashSet<Tech> result = new();
		void Collect(Tech t) {
			if (p.knownTechs.Contains(t.id) || !result.Add(t)) {
				return;
			}
			foreach (Tech pre in t.Prerequisites) {
				Collect(pre);
			}
		}
		Collect(goal);
		return result;
	}

	[Fact]
	public void ResearchHeadsForMonarchy() {
		AiExperiments.Enable(player, DevelopmentAI.ResearchMonarchy);
		player.SetCurrentlyResearchedTech(null);
		HashSet<Tech> path = UnknownPathTo(player, TechNamed("Monarchy"));
		Assert.NotEmpty(path);

		PlayerAI.MaybePickTechToResearch(player, gameData.techs);

		Tech chosen = Tech.FindById(gameData.techs, player.currentlyResearchedTech);
		Assert.Contains(chosen, path);
	}

	[Fact]
	public void ResearchFallsBackOnceTheGoalIsKnown() {
		AiExperiments.Enable(player, DevelopmentAI.ResearchMonarchy);
		foreach (Tech t in UnknownPathTo(player, TechNamed("Monarchy"))) {
			player.knownTechs.Add(t.id);
		}
		Assert.Null(DevelopmentAI.ChooseResearch(player, gameData.techs, player.GetAvailableTechsToResearch(gameData.techs)));
	}

	[Fact]
	public void ResearchIsUnchangedWithoutTheSwitch() {
		Assert.Null(DevelopmentAI.ChooseResearch(player, gameData.techs, player.GetAvailableTechsToResearch(gameData.techs)));
	}

	private void StartTemple(int shields) {
		city.SetItemBeingProduced(BuildingNamed("Temple"));
		city.SetStoredShields(shields);
	}

	[Fact]
	public void RushBuyFinishesAnImprovementUnderMonarchy() {
		AiExperiments.Enable(player, DevelopmentAI.RushBuy);
		player.government = Government("Monarchy");
		player.gold = 1000;
		StartTemple(1);

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(player.ShieldCost(city.itemBeingProduced), city.shieldsStored);
		Assert.True(player.gold < 1000);
	}

	[Fact]
	public void RushBuyKeepsAReserve() {
		AiExperiments.Enable(player, DevelopmentAI.RushBuy);
		player.government = Government("Monarchy");
		StartTemple(1);
		// Just enough for the temple, but nothing would be left over.
		player.gold = city.GetHurryProductionDetails().goldCost;

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(1, city.shieldsStored);
	}

	[Fact]
	public void RushBuyDoesNotPayFromAnEmptyBox() {
		AiExperiments.Enable(player, DevelopmentAI.RushBuy);
		player.government = Government("Monarchy");
		player.gold = 1000;
		StartTemple(0);

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(0, city.shieldsStored);
		Assert.Equal(1000, player.gold);
	}

	[Fact]
	public void RushBuyNeedsTheSwitch() {
		player.government = Government("Monarchy");
		player.gold = 1000;
		StartTemple(1);

		DevelopmentAI.HurryProduction(player);

		Assert.Equal(1, city.shieldsStored);
	}

	[Fact]
	public void FoodFirstAimsForMoreFoodInASmallCity() {
		Assert.Equal(2, DevelopmentAI.DesiredFoodSurplus(city, 2));
		AiExperiments.Enable(player, DevelopmentAI.FoodFirst);
		Assert.True(DevelopmentAI.DesiredFoodSurplus(city, 2) > 2);
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
}

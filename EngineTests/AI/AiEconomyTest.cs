using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

public sealed class AiEconomyTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public AiEconomyTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		player.government = Government("Despotism");
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private Government Government(string name) => gameData.governments.First(g => g.name == name);

	private void LearnTechFor(string government) {
		player.knownTechs.Add(Government(government).prerequisiteTech);
	}

	// A Republic at war whose people are weary of it (level 3 against the
	// enemy) leaves for a government without war weariness, as players saw
	// the Civ3 AI do after a long war (compendium 08 section 3.6).
	private Player WearyRepublicAtWar() {
		LearnTechFor("Monarchy");
		LearnTechFor("Republic");
		player.government = Government("Republic");
		Player enemy = gameData.players.First(p => p != player && !p.isBarbarians);
		player.DeclareWarOn(enemy, gameData.turn);
		player.playerRelationships[enemy.id].warWearinessPoints = 91;
		Assert.Equal(3, Player.WarWearinessLevel(91));
		return enemy;
	}

	[Fact]
	public void AWearyAiChangesGovernmentMidWar() {
		WearyRepublicAtWar();
		Assert.True(GovernmentAI.WarPressure(player, gameData) >= GovernmentAI.SignificantWarWearinessLevel);
		GovernmentAI.PlayTurn(player, gameData);
		Assert.True(player.government.transitionType);

		gameData.turn = player.inAnarchyUntilTurn;
		GovernmentAI.PlayTurn(player, gameData);
		Assert.Equal(0, player.government.warWeariness);
	}

	[Fact]
	public void AnAiWithoutWarWearinessKeepsItsGovernmentMidWar() {
		Player enemy = WearyRepublicAtWar();
		player.playerRelationships[enemy.id].warWearinessPoints = 20;
		Assert.Equal(0, GovernmentAI.WarPressure(player, gameData));
		GovernmentAI.PlayTurn(player, gameData);
		Assert.Equal("Republic", player.government.name);
	}

	[Fact]
	public void ALongWarPressesTheAi() {
		Player enemy = WearyRepublicAtWar();
		player.playerRelationships[enemy.id].warWearinessPoints = 0;
		player.playerRelationships[enemy.id].warStartTurn = gameData.turn;
		gameData.turn += GovernmentAI.LongWarTurns;
		Assert.Equal(1, GovernmentAI.WarPressure(player, gameData));
	}

	// The project owner's decision, 2026-10-20: in Despotism the AI almost
	// never runs a gold surplus, but spends it on research.
	[Theory]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(100)]
	public void ADespotAiSpendsItsSurplusOnResearch(int gold) {
		player.gold = gold;
		PlayerAI.AdjustSliders(player);
		int gpt = player.CalculateGoldPerTurn();
		Assert.True(player.scienceRate == player.maxScienceRate || gpt <= 0 || gold < 10,
			$"science {player.scienceRate}, gold per turn {gpt}");
		Assert.True(gold + gpt >= 0);
	}

	// Before GovernmentAI, the AI stayed in Despotism all game.
	[Fact]
	public void AiRevoltsOutOfDespotismOnceMonarchyIsKnown() {
		LearnTechFor("Monarchy");
		GovernmentAI.PlayTurn(player, gameData);
		Assert.True(player.government.transitionType);
		Assert.True(player.inAnarchyUntilTurn > gameData.turn);

		gameData.turn = player.inAnarchyUntilTurn;
		GovernmentAI.PlayTurn(player, gameData);
		Assert.Equal("Monarchy", player.government.name);
	}

	[Fact]
	public void AiStaysInAnarchyUntilItEnds() {
		LearnTechFor("Monarchy");
		GovernmentAI.PlayTurn(player, gameData);
		Assert.True(player.government.transitionType);

		gameData.turn = player.inAnarchyUntilTurn - 1;
		GovernmentAI.PlayTurn(player, gameData);
		Assert.True(player.government.transitionType);
	}

	[Fact]
	public void AiDoesNotRevoltWithNothingBetterAvailable() {
		GovernmentAI.PlayTurn(player, gameData);
		Assert.Equal("Despotism", player.government.name);
	}

	// script.txt, #REVOLUTIONNOTAVAILABLE: "We cannot have a revolution
	// until we discover different forms of government."
	[Fact]
	public void ARevolutionNeedsAnotherGovernmentToChangeTo() {
		Assert.False(player.CanStartRevolution(gameData));
		LearnTechFor("Monarchy");
		Assert.True(player.CanStartRevolution(gameData));
	}

	// Having switched to the best government, the AI stays with it.
	[Fact]
	public void AiDoesNotRevoltBackAndForth() {
		LearnTechFor("Monarchy");
		LearnTechFor("Republic");
		Government best = GovernmentAI.BestGovernment(player, gameData);
		player.government = best;
		foreach (Government other in player.GetAvailableGovernments(gameData).Where(g => g != best)) {
			Assert.True(GovernmentAI.Score(player, other) < GovernmentAI.Score(player, best) + GovernmentAI.RevolutionMargin);
		}
		GovernmentAI.PlayTurn(player, gameData);
		Assert.Equal(best, player.government);
	}

	// Wealth used to win in every city whenever the science slider was at
	// zero and the treasury under 100, and a city never left it.
	[Fact]
	public void AiDoesNotPickWealthWithAHealthyTreasury() {
		foreach (Tech tech in gameData.techs) {
			player.knownTechs.Add(tech.id);
		}
		player.gold = 50;
		player.scienceRate = 0;
		player.taxRate = 10;
		Assert.True(player.CalculateGoldPerTurn() >= 0);
		for (int i = 0; i < 20; ++i) {
			Assert.IsNotType<Inflow>(ChooseProducible.Choose(city, player));
		}
	}

	[Fact]
	public void AiNeverPicksWealthEvenWhenAboutToGoBankrupt() {
		// With the house rule that spares the AI building upkeep turned off,
		// so that it can run short.
		player.rules.AiBuildingMaintenancePercent = 100;
		foreach (Tech tech in gameData.techs) {
			player.knownTechs.Add(tech.id);
		}
		foreach (Building b in gameData.Buildings.Where(b => b.maintenanceCost > 0 && !b.isSmallWonder && !b.IsGreatWonder()).Take(10)) {
			city.AddBuilding(b);
		}
		player.gold = 0;
		Assert.True(player.CalculateGoldPerTurn() < 0);
		Assert.DoesNotContain(ChooseProducible.Choose(city, player), gameData.Inflows);
	}

	// The owner's choice: the AI never builds Explorers, even knowing how.
	[Fact]
	public void AiNeverBuildsExplorers() {
		foreach (Tech tech in gameData.techs) {
			player.knownTechs.Add(tech.id);
		}
		UnitPrototype explorer = gameData.unitPrototypes.First(p => p.name == "Explorer");
		Assert.Contains(explorer, city.ListProductionOptions(gameData));
		for (int i = 0; i < 20; ++i) {
			Assert.NotEqual<IProducible>(explorer, ChooseProducible.Choose(city, player));
		}
	}
}

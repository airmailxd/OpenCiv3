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
	public void AiPicksWealthWhenAboutToGoBankrupt() {
		foreach (Tech tech in gameData.techs) {
			player.knownTechs.Add(tech.id);
		}
		foreach (Building b in gameData.Buildings.Where(b => b.maintenanceCost > 0 && !b.isSmallWonder && !b.IsGreatWonder()).Take(10)) {
			city.AddBuilding(b);
		}
		player.gold = 0;
		Assert.True(player.CalculateGoldPerTurn() < 0);
		Assert.Contains(ChooseProducible.Choose(city, player), gameData.Inflows);
	}
}

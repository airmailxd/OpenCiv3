using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

public sealed class GranaryPumpTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public GranaryPumpTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private float GranaryScore() {
		Building granary = gameData.Buildings.First(b => b.name == "Granary");
		return ChooseProducible.ScoreBuilding(ChooseProducible.CalculateStats(city, player, [granary]), city, player, granary);
	}

	[Fact]
	public void TheBestFoodCityIsThePump() {
		Assert.True(city.FoodGrowthPerTurn() > 0);
		Assert.Equal([city], GranaryPump.Pumps(player));
	}

	[Fact]
	public void TheSwitchRaisesTheGranaryInThePumpCityWhileExpanding() {
		ChooseProducible.ProducibleStats stats = ChooseProducible.CalculateStats(city, player, []);
		Assert.True(stats.InExpansionPhase);
		float before = GranaryScore();
		Assert.Equal(0, GranaryPump.BuildingBonus(stats, city, player, gameData.Buildings.First(b => b.name == "Granary")));

		AiExperiments.Enable(player, GranaryPump.Name);

		Assert.Equal(before + GranaryPump.GranaryBonus, GranaryScore());
	}

	[Fact]
	public void OnlyTheGranaryCityGetsTheSettlerAndWorkerBonus() {
		AiExperiments.Enable(player, GranaryPump.Name);
		ChooseProducible.ProducibleStats stats = ChooseProducible.CalculateStats(city, player, []);
		UnitPrototype settler = gameData.unitPrototypes.First(p => p.actions.Contains(UnitAction.BuildCity));
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");

		Assert.Equal(0, GranaryPump.UnitAdjustment(stats, city, player, settler));

		city.AddBuilding(gameData.Buildings.First(b => b.name == "Granary"));

		Assert.Equal(GranaryPump.UnitBonus, GranaryPump.UnitAdjustment(stats, city, player, settler));
		Assert.Equal(0, GranaryPump.UnitAdjustment(stats, city, player, warrior));
	}
}

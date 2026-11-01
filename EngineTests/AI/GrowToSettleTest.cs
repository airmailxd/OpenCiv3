using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The grow-to-settle trial switch (ZUR-72): Advanced AI only, off by
// default.
public sealed class GrowToSettleTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;
	private readonly UnitPrototype settler;
	private readonly UnitPrototype worker;

	public GrowToSettleTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		settler = gameData.unitPrototypes.First(p => p.name == "Settler");
		worker = gameData.unitPrototypes.First(p => p.isWorker);
		// Only a defender of the city's own: an unescorted settler or an
		// unguarded city would rule settlers and workers out.
		foreach (MapUnit u in player.units.ToList()) {
			player.units.Remove(u);
			u.location.unitsOnTile.Remove(u);
		}
		gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Spearman"), city.location);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private void GrowTo(int size) {
		while (city.residents.Count < size) {
			CityResident r = new() {
				city = city,
				nationality = player.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			};
			city.AddCitizen(r);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
		}
	}

	private float Score(UnitPrototype unit) =>
		ChooseProducible.ScoreUnit(ChooseProducible.CalculateStats(city, player, [unit]), city, player, unit);

	private bool Sheds() => GrowToSettle.Sheds(ChooseProducible.CalculateStats(city, player, [settler]), city, player);

	[Fact]
	public void ACityAboutToHitItsSizeCapBuildsSettlersForAdvancedOnly() {
		// One step from the Aqueduct size, still growing, with land left.
		GrowTo(player.rules.GrowthLimitWithoutAqueduct - 1);
		Assert.True(city.FoodGrowthPerTurn() > 0);
		Assert.True(ChooseProducible.NumberOfReachableOpenCitySpots(city) > 0);
		Assert.True(GrowToSettle.AtSizeCap(city, player));

		float standard = Score(settler);
		float standardWorker = Score(worker);
		player.aiType = AiType.Advanced;
		float advanced = Score(settler);
		float advancedWorker = Score(worker);

		// Standard, switch on: as before.
		player.aiType = AiType.Standard;
		AiExperiments.Enable(player, GrowToSettle.Switch);
		Assert.False(Sheds());
		Assert.Equal(standard, Score(settler));
		Assert.Equal(standardWorker, Score(worker));

		// Advanced, switch on: settlers and workers come first.
		player.aiType = AiType.Advanced;
		Assert.True(Sheds());
		Assert.Equal(advanced + GrowToSettle.SettlerBonus, Score(settler), 3);
		Assert.Equal(advancedWorker + GrowToSettle.WorkerBonus, Score(worker), 3);
	}

	[Fact]
	public void ASmallContentCityGrowsAsBefore() {
		GrowTo(3);
		player.aiType = AiType.Advanced;
		float off = Score(settler);
		AiExperiments.Enable(player, GrowToSettle.Switch);
		Assert.False(GrowToSettle.AtSizeCap(city, player));
		Assert.False(GrowToSettle.AtHappyLimit(city));
		Assert.False(Sheds());
		Assert.Equal(off, Score(settler));
	}

	[Fact]
	public void SuchACityMayBuildAWorkerPastTheUsualCap() {
		GrowTo(player.rules.GrowthLimitWithoutAqueduct - 1);
		player.aiType = AiType.Advanced;
		while (player.units.Count(u => u.unitType.isWorker) < ChooseProducible.EnoughWorkers(player)) {
			gameData.SpawnUnit(player, worker, city.location);
		}
		Assert.Equal(int.MinValue, Score(worker));
		AiExperiments.Enable(player, GrowToSettle.Switch);
		Assert.True(Score(worker) > int.MinValue);
	}
}

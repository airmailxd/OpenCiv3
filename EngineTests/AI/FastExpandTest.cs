using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The fast-expand trial switch: Advanced AI only, off by default. The save
// has one city in the world, ours, with the whole map known, so there are
// open sites and the civ is expanding.
public sealed class FastExpandTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public FastExpandTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		foreach (MapUnit u in player.units.ToList()) {
			player.units.Remove(u);
			u.location.unitsOnTile.Remove(u);
		}
		foreach (Tile t in gameData.map.tiles) {
			player.tileKnowledge.knownTiles.Add(t);
		}
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Unit(string name) => gameData.unitPrototypes.First(p => p.name == name);

	private ChooseProducible.ProducibleStats Stats(params IProducible[] options) => ChooseProducible.CalculateStats(city, player, options.ToList());

	private MapUnit Spearman() {
		MapUnit u = gameData.SpawnUnit(player, Unit("Spearman"), city.location);
		u.currentAI = null;
		return u;
	}

	[Fact]
	public void OnlyTheAdvancedAiWithTheSwitchOnExpandsFast() {
		player.aiType = AiType.Advanced;
		Assert.False(FastExpand.On(player));
		Assert.False(FastExpand.Active(Stats(Unit("Settler")), city, player));
		player.aiType = AiType.Standard;
		AiExperiments.Enable(player, FastExpand.Switch);
		Assert.False(FastExpand.On(player));
		Assert.False(FastExpand.Active(Stats(Unit("Settler")), city, player));
		player.aiType = AiType.Advanced;
		Assert.True(FastExpand.On(player));
		Assert.True(FastExpand.Active(Stats(Unit("Settler")), city, player));
	}

	// With its one defender, the city wants a settler and no second soldier
	// (one a city, an escort per settler being built and two spare: here
	// four soldiers make enough); Civ3's garrison of two defenders and an
	// attacker is set aside. Buildings wait.
	[Fact]
	public void AGuardedCityBuildsSettlersNotASecondGarrison() {
		UnitPrototype settler = Unit("Settler"), spearman = Unit("Spearman"), archer = Unit("Archer");
		Building temple = gameData.Buildings.First(b => b.name == "Temple");
		player.aiType = AiType.Advanced;
		AiExperiments.Enable(player, FastExpand.Switch);
		// The city builds a Temple, so no soldier or settler is on the way.
		city.ChangeProduction(temple);
		Spearman();
		ChooseProducible.ProducibleStats stats = Stats(settler, spearman, archer);
		Assert.Equal(FastExpand.SettlerBonus, FastExpand.UnitAdjustment(stats, player, settler));
		// One soldier and three wanted: soldiers are still welcome.
		Assert.Equal((1, 1 + FastExpand.SpareSoldiers), FastExpand.Soldiers(player));
		Assert.Equal(0, FastExpand.UnitAdjustment(stats, player, spearman));
		Spearman();
		Spearman();
		stats = Stats(settler, spearman, archer);
		Assert.Equal(-FastExpand.SoldierPenalty, FastExpand.UnitAdjustment(stats, player, spearman));
		Assert.Equal(-FastExpand.SoldierPenalty, FastExpand.UnitAdjustment(stats, player, archer));
		Assert.Equal(-FastExpand.BuildingPenalty, FastExpand.BuildingAdjustment(temple));

		// The scores: the switch's adjustments in place of the stock
		// garrison bonus, against the same city with the switch off.
		float on = ChooseProducible.ScoreUnit(stats, city, player, spearman);
		float onBuilding = ChooseProducible.ScoreBuilding(Stats(temple), city, player, temple);
		AiExperiments.Disable(player, FastExpand.Switch);
		ChooseProducible.ProducibleStats offStats = Stats(settler, spearman, archer);
		float off = ChooseProducible.ScoreUnit(offStats, city, player, spearman);
		float offBuilding = ChooseProducible.ScoreBuilding(Stats(temple), city, player, temple);
		Assert.Equal(off - ChooseProducible.StockGarrisonBonus(player, spearman) - FastExpand.SoldierPenalty, on, 3);
		Assert.Equal(offBuilding - FastExpand.BuildingPenalty, onBuilding, 3);
	}

	// An unguarded city still builds its defender first.
	[Fact]
	public void AnUnguardedCityStillWantsItsDefender() {
		UnitPrototype settler = Unit("Settler"), spearman = Unit("Spearman");
		player.aiType = AiType.Advanced;
		AiExperiments.Enable(player, FastExpand.Switch);
		ChooseProducible.ProducibleStats stats = Stats(settler, spearman);
		Assert.Equal(0, FastExpand.UnitAdjustment(stats, player, settler));
		Assert.Equal(0, FastExpand.UnitAdjustment(stats, player, spearman));
		Assert.Equal(int.MinValue, ChooseProducible.ScoreUnit(stats, city, player, settler));
	}

	// A size 1 city may start its settler only when the shields would take
	// about as long as growing to size 3.
	[Fact]
	public void ASettlerIsStartedWhenItsShieldsTakeAsLongAsTheGrowth() {
		UnitPrototype settler = Unit("Settler");
		player.aiType = AiType.Advanced;
		AiExperiments.Enable(player, FastExpand.Switch);
		Spearman();
		Assert.Equal(1, city.residents.Count);
		int growth = city.FoodGrowthPerTurn();
		Assert.True(growth > 0);
		int toSize3 = FastExpand.TurnsToSize(city, 3, growth, city.TurnsUntilGrowth());
		Assert.Equal(city.TurnsUntilGrowth() + (int)Math.Ceiling(FastExpand.TownFoodBox / (double)growth), toSize3);
		ChooseProducible.ProducibleStats stats = Stats(settler);
		Assert.Equal(city.TurnsToProduce(settler) >= toSize3 - FastExpand.PrebuildSlackTurns, FastExpand.MayPrebuild(stats, city, settler));
		// Off, a size 1 city never starts one.
		player.aiType = AiType.Standard;
		Assert.Equal(int.MinValue, ChooseProducible.ScoreUnit(Stats(settler), city, player, settler));
	}

	// The settler's walk counts for more than SettlerLocationAI's 4% a
	// tile.
	[Fact]
	public void FarSitesLoseMoreForTheWalk() {
		Assert.Equal(1f, FastExpand.TravelFactor(FastExpand.FreeTravelTiles));
		Assert.Equal(1f - 5 * FastExpand.TravelDiscountPerTile, FastExpand.TravelFactor(FastExpand.FreeTravelTiles + 5), 4);
		Assert.True(FastExpand.TravelDiscountPerTile > .04f);
		Tile land = gameData.map.tiles.First(t => t.IsLand() && t.continent == city.location.continent && t.DistanceTo(city.location) == 6);
		Assert.Equal(FastExpand.TravelFactor(6), FastExpand.SiteTravelFactor(city.location, land));
		Tile overseas = gameData.map.tiles.FirstOrDefault(t => t.IsLand() && t.continent != city.location.continent);
		if (overseas != null) {
			Assert.Equal(FastExpand.TravelFactor(city.location.DistanceTo(overseas) + FastExpand.OverseasExtraTiles),
				FastExpand.SiteTravelFactor(city.location, overseas));
		}
	}

	// With land left on our landmass the switch is active, and a settler
	// is never held back for the transports.
	[Fact]
	public void LandLeftKeepsTheSwitchActive() {
		player.aiType = AiType.Advanced;
		AiExperiments.Enable(player, FastExpand.Switch);
		Assert.True(FastExpand.LandSites(player, city.location.continent) > 0);
		Assert.Equal(0, FastExpand.SettlersWaitingToSail(player));
		Assert.Equal(0, FastExpand.OverseasSettlerAdjustment(city, player));
	}
}

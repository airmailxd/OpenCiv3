using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.UnitAI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The lean-garrison trial switch (ZUR-71): Advanced AI only, off by
// default. The save has one city in the world, ours, so it starts
// interior.
public sealed class LeanGarrisonTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly Player rival;
	private readonly City city;

	public LeanGarrisonTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		rival = gameData.players.First(p => p != player && !p.isHuman && !p.IsIndependent);
		player.EnsureRelationshipExists(rival);
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		// Only the units each test makes, and the whole map known (the
		// Advanced AI plans in the fog).
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

	private static bool EmptyLand(Tile t) => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.IsAllowCities() && t.OwningPlayer() == null;

	// A land tile on our continent this many tiles from `from`, and at
	// least `awayFromCity` from our first city.
	private Tile LandAt(Tile from, int distance, int awayFromCity = 0) {
		return gameData.map.tiles.First(t => EmptyLand(t) && t.continent == city.location.continent
			&& t.DistanceTo(from) == distance && t.DistanceTo(city.location) >= awayFromCity);
	}

	private MapUnit Spearman(City at) {
		MapUnit u = gameData.SpawnUnit(player, Unit("Spearman"), at.location);
		u.currentAI = null;
		return u;
	}

	[Fact]
	public void InteriorCitiesWantOneDefenderBorderCitiesTwo() {
		player.aiType = AiType.Advanced;
		Assert.True(LeanGarrison.IsInterior(player, city));
		Assert.Equal(1, LeanGarrison.DefendersWanted(player, city, atWar: false));
		Assert.Equal(2, LeanGarrison.DefendersWanted(player, city, atWar: true));

		CityInteractions.BuildCity(LandAt(city.location, 4), rival, rival.GetNextCityName());
		Assert.False(LeanGarrison.IsInterior(player, city));
		Assert.Equal(2, LeanGarrison.DefendersWanted(player, city, atWar: false));
		Assert.Equal(3, LeanGarrison.DefendersWanted(player, city, atWar: true));
	}

	[Fact]
	public void AnInteriorCityWithItsDefenderBuildsAttackersInstead() {
		UnitPrototype spearman = Unit("Spearman");
		UnitPrototype archer = Unit("Archer");
		Assert.True(StockWarPlotting.IsDefensive(spearman));
		Assert.True(StockWarPlotting.IsOffensive(archer));
		Spearman(city);
		float Adjustment() => LeanGarrison.DefenderAdjustment(ChooseProducible.CalculateStats(city, player, [spearman]), city, player, spearman);

		// Advanced, switch off: Civ3's two defenders a city.
		player.aiType = AiType.Advanced;
		Assert.Equal(0, Adjustment());
		Assert.True(ChooseProducible.StockGarrisonBonus(player, spearman) > 0);
		Assert.True(ChooseProducible.StockGarrisonBonus(player, archer) > 0);

		// Standard, switch on: the same.
		player.aiType = AiType.Standard;
		AiExperiments.Enable(player, LeanGarrison.Switch);
		Assert.Equal(0, Adjustment());
		Assert.True(ChooseProducible.StockGarrisonBonus(player, spearman) > 0);

		// Advanced, switch on: the interior city has its one defender, so
		// no more, and the defender it saves is wanted as an attacker.
		player.aiType = AiType.Advanced;
		Assert.Equal(-LeanGarrison.ExtraDefenderPenalty, Adjustment());
		Assert.Equal(0, ChooseProducible.StockGarrisonBonus(player, spearman));
		gameData.SpawnUnit(player, archer, city.location);
		Assert.True(ChooseProducible.StockGarrisonBonus(player, archer) > 0);
	}

	// The standard AI: a spare defender goes to a farther city short of two
	// rather than a nearer one that has them.
	[Fact]
	public void ASpareDefenderGoesToACityShortOfTwo() {
		player.aiType = AiType.Standard;
		City near = CityInteractions.BuildCity(LandAt(city.location, 3), player, player.GetNextCityName());
		City far = CityInteractions.BuildCity(LandAt(city.location, 6, awayFromCity: 6), player, player.GetNextCityName());
		Spearman(near);
		Spearman(near);
		Spearman(far);
		Spearman(city);
		Spearman(city);
		MapUnit spare = Spearman(city);

		Assert.Equal(far.location, DefenderAI.MakeAiDataForDefendAtRiskCity(spare, player, int.MaxValue).destination);
	}

	// Defenders outside the cities don't count towards their two each, and
	// with the cities held the AI still wants 4 defenders for 3 attackers.
	[Fact]
	public void OnlyDefendersInCitiesCountAndTheRatioIsKept() {
		player.aiType = AiType.Standard;
		UnitPrototype spearman = Unit("Spearman");
		Spearman(city);
		gameData.SpawnUnit(player, spearman, LandAt(city.location, 3));
		Assert.True(ChooseProducible.StockGarrisonBonus(player, spearman) > 0);

		Spearman(city);
		Assert.Equal(0, ChooseProducible.StockGarrisonBonus(player, spearman));
		for (int i = 0; i < 3; ++i) {
			gameData.SpawnUnit(player, Unit("Archer"), city.location);
		}
		Assert.True(ChooseProducible.StockGarrisonBonus(player, spearman) > 0);
	}

	[Fact]
	public void SurplusDefendersInAnInteriorCityGoToAShortBorderCity() {
		// A border city of ours 3 tiles from a rival city, which is far
		// enough from our first city that it stays interior.
		Tile rivalSite = LandAt(city.location, 10);
		Tile borderSite = gameData.map.tiles.First(t => EmptyLand(t) && t.continent == city.location.continent
			&& t.DistanceTo(rivalSite) == 3 && t.DistanceTo(city.location) >= 6);
		City border = CityInteractions.BuildCity(borderSite, player, player.GetNextCityName());
		CityInteractions.BuildCity(rivalSite, rival, rival.GetNextCityName());
		MapUnit[] spears = [Spearman(city), Spearman(city), Spearman(city)];

		// Advanced, switch off: none has to leave, but a spare one, looking
		// for a city to defend, goes to the one short of two.
		player.aiType = AiType.Advanced;
		Assert.True(LeanGarrison.IsInterior(player, city));
		Assert.False(LeanGarrison.IsInterior(player, border));
		Assert.False(LeanGarrison.ShouldLeave(spears[0], player));
		Assert.Equal(border.location, DefenderAI.MakeAiDataForDefendAtRiskCity(spears[0], player, int.MaxValue).destination);

		// Standard, switch on: the same.
		player.aiType = AiType.Standard;
		AiExperiments.Enable(player, LeanGarrison.Switch);
		Assert.False(LeanGarrison.ShouldLeave(spears[0], player));
		Assert.Equal(border.location, DefenderAI.MakeAiDataForDefendAtRiskCity(spears[0], player, int.MaxValue).destination);

		// Advanced, switch on: the border city wants two, so two of the
		// interior city's three leave, one keeps it. Nothing is disbanded.
		player.aiType = AiType.Advanced;
		Assert.True(LeanGarrison.ShouldLeave(spears[0], player));
		Assert.True(LeanGarrison.ShouldLeave(spears[1], player));
		Assert.False(LeanGarrison.ShouldLeave(spears[2], player));
		Assert.Equal(border.location, DefenderAI.MakeAiDataForDefendAtRiskCity(spears[0], player, int.MaxValue).destination);
		Assert.Equal(3, player.units.Count(u => u.unitType == Unit("Spearman")));
	}
}

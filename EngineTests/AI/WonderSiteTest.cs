using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The AI builds great wonders only in cities safe from its wars (see
// WonderPlanner.SiteAdjustment).
public sealed class WonderSiteTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly Player enemy;
	private readonly City city;
	private readonly Building wonder;

	public WonderSiteTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		enemy = gameData.players.First(p => !p.isBarbarians && p != player);
		player.EnsureRelationshipExists(enemy);
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		wonder = gameData.Buildings.First(b => b.IsGreatWonder());
	}

	public void Dispose() {
		EngineStorage.aiFogOfWar = false;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private float WonderScore() {
		List<IProducible> options = city.ListProductionOptions(gameData).ToList();
		ChooseProducible.ProducibleStats stats = ChooseProducible.CalculateStats(city, player, options);
		return ChooseProducible.ScoreBuilding(stats, city, player, wonder);
	}

	// An enemy warrior two (or more) tiles from the city, on its continent.
	private MapUnit EnemyWarriorNearby(int distance = 2) {
		Tile tile = city.location.GetTilesWithinTileSquare(distance).First(t => t != Tile.NONE && t.IsLand() && !t.HasCity()
			&& t.unitsOnTile.Count == 0 && t.continent == city.location.continent && t.DistanceTo(city.location) == distance);
		return gameData.SpawnUnit(enemy, gameData.unitPrototypes.First(p => p.name == "Warrior"), tile);
	}

	[Fact]
	public void AnEnemyUnitNearbyThreatensTheCityOnlyInWar() {
		EnemyWarriorNearby();
		Assert.False(WonderPlanner.IsThreatened(city, player));

		player.DeclareWarOn(enemy, gameData.turn);
		Assert.True(WonderPlanner.IsThreatened(city, player));
	}

	[Fact]
	public void AThreatenedCityDoesntStartAGreatWonder() {
		float safe = WonderScore();
		EnemyWarriorNearby();
		player.DeclareWarOn(enemy, gameData.turn);
		Assert.True(WonderScore() <= -1000);
		Assert.True(safe > -1000);
	}

	// The standard AI builds two great wonders at a time. A city already
	// building one carries on.
	[Fact]
	public void TheStandardAiBuildsTwoWondersAtATime() {
		Assert.True(WonderPlanner.CivStyle(player));
		List<Building> others = gameData.Buildings.Where(b => b.IsGreatWonder() && b != wonder).Take(2).ToList();
		List<Tile> sites = gameData.map.tiles.Where(t => t.IsLand() && !t.HasCity() && t.IsAllowCities() && t.OwningPlayer() == null
			&& t.continent == city.location.continent && t.DistanceTo(city.location) >= 4).ToList();
		City first = CityInteractions.BuildCity(sites[0], player, player.GetNextCityName());
		City second = CityInteractions.BuildCity(sites.First(t => t.DistanceTo(sites[0]) >= 4 && !t.HasCity()), player, player.GetNextCityName());
		first.SetItemBeingProduced(others[0]);
		Assert.True(WonderScore() > -1000);

		second.SetItemBeingProduced(others[1]);
		Assert.True(WonderScore() <= -1000);

		city.SetItemBeingProduced(wonder);
		Assert.True(WonderScore() > -1000);
	}

	// Civ3's favourite wonders are all found by name in the rules.
	[Fact]
	public void TheFavouriteWondersAreInTheRules() {
		Assert.Equal(5, gameData.Buildings.Count(b => b.IsGreatWonder() && WonderPlanner.IsFavourite(b)));
	}

	// With the AI fog of war, an enemy the AI can't see doesn't count.
	[Fact]
	public void WithTheFogAnUnseenEnemyDoesntThreaten() {
		MapUnit theirs = EnemyWarriorNearby(3);
		player.DeclareWarOn(enemy, gameData.turn);
		Assert.True(WonderPlanner.IsThreatened(city, player));

		EngineStorage.aiFogOfWar = true;
		player.tileKnowledge.RecomputeActiveTiles();
		Assert.False(player.tileKnowledge.isActiveTile(theirs.location));
		Assert.False(WonderPlanner.IsThreatened(city, player));
	}
}

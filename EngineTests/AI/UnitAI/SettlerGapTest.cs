using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.UnitAI;

// The AI should found a city on any open land tile with no city within 2
// tiles, however poor the site, so that gaps in its land get filled.
public sealed class SettlerGapTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;

	public SettlerGapTests(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		foreach (Tile t in gameData.map.tiles) {
			player.tileKnowledge.knownTiles.Add(t);
		}

		// Make every site worthless, so that only the gap rule can make a
		// tile a site. (Set before anything is scored, as the yield scores
		// are cached.)
		Civilization.SettlerTileAdjustments adjustments = player.civilization.Adjustments;
		adjustments.FoodYieldBonus = -100;
		adjustments.ProductionYieldBonus = -100;
		adjustments.CommerceYieldBonus = -100;
		adjustments.HillsBonus = 0;
		adjustments.WaterBonus = 0;
		adjustments.LuxuryResourceBonus = 0;
		adjustments.StrategicResourceBonus = 0;
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private bool IsOpenLand(Tile t, Tile continentOf) {
		return t != Tile.NONE && t.IsLand() && t.IsAllowCities() && !t.HasCity() && t.continent == continentOf.continent
			&& (t.OwningPlayer() == null || t.OwningPlayer() == player);
	}

	private bool NoCityWithin(Tile t, int distance) {
		return gameData.cities.All(c => c.location.DistanceTo(t) > distance);
	}

	private City BuildFirstCity() {
		return CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
	}

	[Fact]
	public void PoorTileThreeTilesFromEveryCityIsASite() {
		City city = BuildFirstCity();
		Tile gap = gameData.map.tiles.First(t => IsOpenLand(t, city.location) && t.DistanceTo(city.location) == 3 && NoCityWithin(t, 2));

		Dictionary<Tile, float> sites = SettlerLocationAI.GetScoredSettlerCandidates(city.location, player);
		Assert.True(sites.ContainsKey(gap), $"{gap} isn't a site");
		Assert.True(sites[gap] > 0);
		Assert.NotEqual(Tile.NONE, SettlerLocationAI.FindSettlerLocation(city.location, player));
	}

	[Fact]
	public void TileTwoTilesFromACityIsNotASite() {
		City city = BuildFirstCity();
		Tile tooClose = gameData.map.tiles.First(t => IsOpenLand(t, city.location) && t.DistanceTo(city.location) == 2);

		Dictionary<Tile, float> sites = SettlerLocationAI.GetScoredSettlerCandidates(city.location, player);
		Assert.False(sites.ContainsKey(tooClose));
		Assert.All(sites.Keys, t => Assert.True(NoCityWithin(t, 2), $"{t} is within 2 tiles of a city"));
	}

	// With 5 or more cities, the AI used to stop expanding unless the open
	// spots outnumbered its cities twice over, and poor spots didn't count.
	[Fact]
	public void PoorGapsKeepTheAiExpanding() {
		City city = BuildFirstCity();
		while (player.cities.Count < 5) {
			Tile site = gameData.map.tiles.Where(t => IsOpenLand(t, city.location) && NoCityWithin(t, 4))
				.OrderBy(t => t.DistanceTo(city.location)).First();
			CityInteractions.BuildCity(site, player, player.GetNextCityName());
		}

		int spots = ChooseProducible.NumberOfReachableOpenCitySpots(city);
		Assert.True(spots > 0);
		ChooseProducible.ProducibleStats stats = new(city, player);
		Assert.True(stats.InExpansionPhase);
	}
}

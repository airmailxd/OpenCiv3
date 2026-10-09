using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.AIData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.UnitAI;

// Checks that the AI's caches give the same answers as computing from scratch.
public class PerfAiStrategyTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly Player rival;

	public PerfAiStrategyTests(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		player = civs[0];
		rival = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City FoundCity(Player owner) {
		Tile tile = owner.units.First(u => u.unitType.isSettler).location;
		return CityInteractions.BuildCity(tile, owner, owner.GetNextCityName());
	}

	private void KnowWholeMap(Player p) {
		foreach (Tile t in gameData.map.tiles) {
			p.tileKnowledge.knownTiles.Add(t);
		}
	}

	// The settler site scoring as it was before caching was added.
	private static Dictionary<Tile, float> ScoreFromScratch(Tile start, Player player, HashSet<Tile> excludedTiles = null) {
		List<MapUnit> playerSettlers = player.units.FindAll(u => u.unitType.name == "Settler");
		IEnumerable<Tile> candidates = player.tileKnowledge.AllKnownTiles().Where(t => !IsInvalidCityLocation(t) && t.continent == start.continent);
		candidates = candidates.Where(t => !SettlerAlreadyMovingTowardsTile(t, playerSettlers) && t.IsAllowCities() && (excludedTiles == null || !excludedTiles.Contains(t)));
		candidates = candidates.Where(t => !SettlerLocationAI.IsOwnedByRival(t, player));
		List<Tile> ownCities = player.cities.Select(c => c.location).ToList();
		List<Tile> rivalCities = EngineStorage.gameData.players.Where(p => p != player).SelectMany(p => p.cities).Select(c => c.location).Where(l => l.map == start.map).ToList();

		Dictionary<Tile, float> scores = new();
		var memo = new Dictionary<string, float>();
		foreach (Tile t in candidates) {
			float score = YieldScore(t, player, memo);
			var maxRank = player.rules.MaxRankOfWorkableTiles;
			foreach (Tile workable in t.GetTilesWithinRankDistance(maxRank)) {
				if (workable == Tile.NONE)
					continue;
				var rank = t.RankDistanceTo(workable);
				if (rank <= 0)
					continue;
				if (SettlerLocationAI.IsOwnedByRival(workable, player))
					continue;
				var adjustment = Math.Max(0, (maxRank - rank + 1f) / maxRank);
				score += YieldScore(workable, player, memo) * adjustment;
			}
			if (t.baseTerrainType.Key == "hills") {
				score += player.civilization.Adjustments.HillsBonus;
			}
			if (t.NeighborsWater()) {
				score += player.civilization.Adjustments.WaterBonus;
			}
			score += (float)t.baseTerrainType.defenseBonus.amount * 20.0f;
			score += player.civilization.Adjustments.RivalCityPenalty
				* rivalCities.Count(c => t.DistanceTo(c) <= player.civilization.Adjustments.RivalCityRadius);
			float preDistanceScore = score;
			int distance = SettlerLocationAI.DistanceFromEmpire(t, start, ownCities);
			float distancePenalty = SettlerLocationAI.DistancePenalty(distance, player.civilization.Adjustments);
			score += distancePenalty;
			if (preDistanceScore > 0 && score <= 0) {
				score = preDistanceScore / (preDistanceScore - distancePenalty);
			}
			if (score > 0)
				scores[t] = score;
		}
		return scores;
	}

	private static float YieldScore(Tile t, Player owner, Dictionary<string, float> memo) {
		var key = $"Tile_{t.XCoordinate}_{t.YCoordinate}";
		if (memo.TryGetValue(key, out var value))
			return value;
		float score = owner.civilization.Adjustments.FoodYieldBonus * t.FoodYield(owner).yield;
		score += owner.civilization.Adjustments.ProductionYieldBonus * t.ProductionYield(owner).yield;
		score += owner.civilization.Adjustments.CommerceYieldBonus * t.CommerceYield(owner).yield;
		if (owner.KnowsAboutResource(t.Resource)) {
			if (t.Resource.Category == ResourceCategory.STRATEGIC) {
				score += owner.civilization.Adjustments.StrategicResourceBonus;
			} else if (t.Resource.Category == ResourceCategory.LUXURY) {
				score += owner.civilization.Adjustments.LuxuryResourceBonus;
			}
		}
		memo[key] = score;
		return score;
	}

	private static bool IsInvalidCityLocation(Tile tile) {
		if (tile == Tile.NONE || tile.HasCity())
			return true;
		foreach (Tile neighbor in tile.neighbors.Values) {
			if (neighbor.HasCity()) {
				return true;
			}
			foreach (Tile neighborOfNeighbor in neighbor.neighbors.Values) {
				if (neighborOfNeighbor.HasCity()) {
					return true;
				}
			}
		}
		return false;
	}

	private static bool SettlerAlreadyMovingTowardsTile(Tile tile, List<MapUnit> playerSettlers) {
		foreach (MapUnit otherSettler in playerSettlers) {
			if (otherSettler.currentAI is SettlerAI otherSettlerAI) {
				Tile otherDestination = otherSettlerAI.data.destination;
				if (otherDestination == tile) {
					return true;
				}
				if (otherDestination.GetLandNeighbors().Exists(innerRingTile => innerRingTile == tile)) {
					return true;
				}
				foreach (Tile innerRingTile in otherDestination.GetLandNeighbors()) {
					if (innerRingTile.GetLandNeighbors().Exists(outerRingTile => outerRingTile == tile)) {
						return true;
					}
				}
			}
		}
		return false;
	}

	private void AssertScoresMatch(Tile start, HashSet<Tile> excluded = null) {
		List<KeyValuePair<Tile, float>> expected = ScoreFromScratch(start, player, excluded).ToList();
		List<KeyValuePair<Tile, float>> actual = SettlerLocationAI.GetScoredSettlerCandidates(start, player, excluded).ToList();
		Assert.NotEmpty(expected);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void SettlerScoringMatchesScoringFromScratch() {
		City city = FoundCity(player);
		KnowWholeMap(player);
		Tile start = city.location;

		AssertScoresMatch(start);
		// Again, now from the cache.
		AssertScoresMatch(start);

		// A different start tile only changes the distance penalties.
		Tile otherStart = start.GetTilesWithinRankDistance(4).First(t => t != Tile.NONE && t.IsLand() && t.continent == start.continent && t != start);
		AssertScoresMatch(otherStart);

		// Improving a tile changes its yield.
		TerrainImprovement mine = gameData.terrainImprovements.Find(ti => ti.key == "mine");
		Tile improved = start.GetTilesWithinRankDistance(4).First(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.overlays.CanAdd(t, mine));
		improved.overlays.Add(mine);
		AssertScoresMatch(start);

		// So does learning about techs.
		foreach (Tech tech in gameData.techs.Take(10)) {
			player.knownTechs.Add(tech.id);
		}
		AssertScoresMatch(start);

		// A new city rules out tiles near it.
		FoundCity(rival);
		AssertScoresMatch(start);

		// As does a settler heading somewhere.
		Tile destination = SettlerLocationAI.FindSettlerLocation(start, player);
		Assert.NotEqual(Tile.NONE, destination);
		MapUnit settler = player.units.First(u => u.unitType.name == "Settler");
		settler.currentAI = new SettlerAI(new SettlerAIData() { destination = destination });
		AssertScoresMatch(start);

		// And excluded tiles.
		AssertScoresMatch(start, new HashSet<Tile> { SettlerLocationAI.FindSettlerLocation(start, player) });
	}

	[Fact]
	public void ExplorationCheckMatchesUnitPlanner() {
		City city = FoundCity(player);

		// Also try a coastal city, so naval units have water to explore.
		Tile coastal = gameData.map.tiles
			.Where(t => t.IsLand() && t.NeighborsOcean() && t.IsAllowCities() && t.continent == city.location.continent && t.DistanceTo(city.location) > 3)
			.OrderBy(t => t.DistanceTo(city.location))
			.First();
		City coastalCity = CityInteractions.BuildCity(coastal, player, player.GetNextCityName());
		player.tileKnowledge.AddTilesToKnown(coastal);

		int explorers = 0;
		int seaExplorers = 0;
		int others = 0;
		foreach (City c in new[] { city, coastalCity }) {
			foreach (UnitPrototype proto in gameData.unitPrototypes) {
				MapUnit forCheck = proto.GetInstance(ID.None(proto.name), proto, player, location: c.location);
				MapUnit forPlanner = proto.GetInstance(ID.None(proto.name), proto, player, location: c.location);

				bool wouldExplore = PlayerAI.WouldExplore(forCheck, player);
				bool plannerExplores = PlayerAI.GetAIForUnit(forPlanner, player) is ExplorerAI;
				Assert.True(wouldExplore == plannerExplores, $"{proto.name}: check says {wouldExplore}, planner says {plannerExplores}");
				if (wouldExplore) {
					if (proto.categories.Contains("Sea")) { seaExplorers++; }
					++explorers;
				} else {
					++others;
				}
			}
		}
		Assert.True(explorers > 0);
		Assert.True(others > 0);
		Assert.True(seaExplorers > 0);
	}

	[Fact]
	public void SliderMovesOnlyChangeCityTaxesAndWealth() {
		City capital = FoundCity(player);
		City other = CityInteractions.BuildCity(
			gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && !t.HasCity() && t.continent == capital.location.continent && t.DistanceTo(capital.location) > 4),
			player, player.GetNextCityName());
		foreach (City c in new[] { capital, other }) {
			while (c.residents.Count < 4) {
				c.AddCitizen(new CityResident() { city = c, citizenType = c.residents[0].citizenType, tileWorked = c.residents[0].tileWorked });
			}
		}
		Building taxBuilding = gameData.Buildings.FirstOrDefault(b => b.increasesTax);
		if (taxBuilding != null) {
			capital.AddBuilding(taxBuilding);
		}
		player.gold = 250;

		for (int lux = 0; lux <= 2; ++lux) {
			player.luxuryRate = lux;
			player.scienceRate = 10 - lux;
			player.taxRate = 0;
			int fixedGoldPerTurn = player.CalculateGoldPerTurn() - PlayerAI.CityTaxesAndWealth(player);
			for (int science = 10 - lux; science >= 0; --science) {
				player.scienceRate = science;
				player.taxRate = 10 - lux - science;
				Assert.Equal(player.CalculateGoldPerTurn(), fixedGoldPerTurn + PlayerAI.CityTaxesAndWealth(player));
			}
		}
	}

	[Fact]
	public void WorkersDoNotAllHeadForTheSameTile() {
		City city = FoundCity(player);
		UnitPrototype workerType = gameData.unitPrototypes.First(p => p.name == "Worker");
		MapUnit first = gameData.SpawnUnit(player, workerType, city.location);
		MapUnit second = gameData.SpawnUnit(player, workerType, city.location);

		first.currentAI = PlayerAI.GetAIForUnit(first, player);
		second.currentAI = PlayerAI.GetAIForUnit(second, player);

		WorkerAIData firstPlan = DataOf(first);
		WorkerAIData secondPlan = DataOf(second);
		Assert.NotNull(firstPlan);
		Assert.NotNull(secondPlan);
		Assert.NotEqual(firstPlan.destination, secondPlan.destination);
	}

	private static WorkerAIData DataOf(MapUnit unit) {
		Assert.IsType<WorkerAI>(unit.currentAI);
		return (WorkerAIData)typeof(WorkerAI).GetField("data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(unit.currentAI);
	}
}

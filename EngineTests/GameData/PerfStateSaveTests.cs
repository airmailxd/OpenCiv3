using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Checks that the faster bookkeeping for active tiles, tile ownership, unit
// lookups and saved tile knowledge gives exactly the same results as the
// straightforward versions it replaced.
public class PerfStateSaveTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public PerfStateSaveTests(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.animationsEnabled = false;
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.pendingMessages.Dequeue();
		}
	}

	private C7GameData.GameData NewGame() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		return gameData;
	}

	private static void DrainMessages() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private static bool CanFoundCityAt(Tile t) {
		return IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || (!n.HasCity() && n.neighbors.Values.All(nn => nn == Tile.NONE || !nn.HasCity())));
	}

	// Founds a city for every player with a settler, then more cities at
	// random places for random players.
	private static void FoundCities(C7GameData.GameData gameData, Random random, int extraCities) {
		foreach (Player player in gameData.players.Where(p => !p.isBarbarians).ToList()) {
			MapUnit settler = player.units.FirstOrDefault(u => u.unitType.isSettler);
			if (settler != null && !settler.location.HasCity()) {
				CityInteractions.BuildCity(settler.location, player, player.GetNextCityName());
			}
		}
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		List<Tile> land = gameData.map.tiles.Where(t => t.IsLand()).ToList();
		int built = 0;
		for (int attempt = 0; attempt < 10000 && built < extraCities; ++attempt) {
			Tile t = land[random.Next(land.Count)];
			if (!CanFoundCityAt(t)) {
				continue;
			}
			Player owner = civs[random.Next(civs.Count)];
			CityInteractions.BuildCity(t, owner, owner.GetNextCityName());
			++built;
		}
		DrainMessages();
	}

	private static void AssertActiveTilesMatchFullRecompute(Player player, string context) {
		HashSet<Tile> expected = player.tileKnowledge.ComputeActiveTilesFromScratch();
		HashSet<Tile> actual = player.tileKnowledge.ActiveTiles();
		Assert.True(expected.SetEquals(actual),
			$"{context}: active tiles of {player} differ: {expected.Except(actual).Count()} missing, {actual.Except(expected).Count()} extra");
		foreach (Tile t in expected) {
			Assert.True(player.tileKnowledge.isActiveTile(t));
		}
	}

	private static void AssertAllActiveTilesMatch(C7GameData.GameData gameData, string context) {
		foreach (Player p in gameData.players) {
			p.tileKnowledge.RecomputeActiveTiles();
			AssertActiveTilesMatchFullRecompute(p, context);
		}
	}

	[Fact]
	public async Task ActiveTilesMatchFullRecomputeThroughMovesDeathsAndCaptures() {
		C7GameData.GameData gameData = NewGame();
		Random random = new(1234);
		System.Random originalRng = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new Random(99);
		try {
			FoundCities(gameData, random, 12);
			AssertAllActiveTilesMatch(gameData, "after founding cities");

			List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
			// Everyone at war, so moves lead to fights and captures.
			for (int i = 0; i < civs.Count; ++i) {
				for (int j = i + 1; j < civs.Count; ++j) {
					civs[i].DeclareWarOn(civs[j], gameData.turn);
				}
			}
			DrainMessages();

			Dictionary<Player, int> fullRecomputes = gameData.players.ToDictionary(p => p, p => p.tileKnowledge.fullRecomputeCount);

			List<Tile> land = gameData.map.tiles.Where(t => t.IsLand() && !t.IsImpassable()).ToList();
			List<Tile> water = gameData.map.tiles.Where(t => t.IsWater()).ToList();
			UnitPrototype warrior = gameData.unitPrototypes.Single(p => p.name == "Warrior");
			UnitPrototype worker = gameData.unitPrototypes.Single(p => p.name == "Worker");
			UnitPrototype settlerProto = gameData.unitPrototypes.Single(p => p.name == "Settler");
			UnitPrototype galley = gameData.unitPrototypes.Single(p => p.name == "Galley");
			TileDirection[] directions = Enum.GetValues<TileDirection>();

			int moves = 0;
			for (int step = 0; step < 1500; ++step) {
				int action = random.Next(100);
				string context = $"step {step}, action {action}";
				if (action < 55) {
					// Move a random unit.
					List<MapUnit> movable = gameData.mapUnits.Where(u => !u.IsLoaded()).ToList();
					if (movable.Count == 0) continue;
					MapUnit unit = movable[random.Next(movable.Count)];
					unit.movementPoints.reset(unit.MaxMovementPoints());
					Player owner = unit.owner;
					Tile from = unit.location;
					bool moved = await unit.Move(directions[random.Next(directions.Length)]);
					DrainMessages();
					if (moved && unit.hitPointsRemaining > 0 && unit.location != from) {
						++moves;
						// Moving brings the owner's active tiles up to date.
						AssertActiveTilesMatchFullRecompute(owner, context);
					}
				} else if (action < 70) {
					// Spawn a unit somewhere.
					Player owner = civs[random.Next(civs.Count)];
					int kind = random.Next(4);
					if (kind == 3) {
						gameData.SpawnUnit(owner, galley, water[random.Next(water.Count)]);
					} else {
						Tile tile = land[random.Next(land.Count)];
						if (tile.unitsOnTile.Count > 0 && tile.unitsOnTile[0].owner != owner) continue;
						gameData.SpawnUnit(owner, kind == 0 ? warrior : kind == 1 ? worker : settlerProto, tile);
					}
				} else if (action < 78) {
					// A unit dies.
					if (gameData.mapUnits.Count == 0) continue;
					MapUnit unit = gameData.mapUnits[random.Next(gameData.mapUnits.Count)];
					Player owner = unit.owner;
					gameData.RemoveUnit(unit);
					DrainMessages();
					AssertActiveTilesMatchFullRecompute(owner, context);
				} else if (action < 84) {
					// A worker is captured.
					List<MapUnit> workers = gameData.mapUnits.Where(u => u.unitType.isWorker && !u.owner.isBarbarians).ToList();
					if (workers.Count == 0) continue;
					MapUnit unit = workers[random.Next(workers.Count)];
					Player captor = civs[random.Next(civs.Count)];
					if (captor == unit.owner) continue;
					Player previous = unit.owner;
					gameData.CaptureUnit(unit, captor);
					DrainMessages();
					AssertActiveTilesMatchFullRecompute(previous, context);
					AssertActiveTilesMatchFullRecompute(captor, context);
				} else if (action < 88) {
					// A city produces a unit.
					if (gameData.cities.Count == 0) continue;
					City city = gameData.cities[random.Next(gameData.cities.Count)];
					city.AddUnit(warrior, gameData);
				} else if (action < 95) {
					// Borders grow.
					if (gameData.cities.Count == 0) continue;
					City city = gameData.cities[random.Next(gameData.cities.Count)];
					city.perPlayerCulture[city.owner] += random.Next(1, 120);
					gameData.UpdateTileOwners();
				} else if (action < 98) {
					// A city is captured, or destroyed if it is small.
					if (gameData.cities.Count == 0) continue;
					City city = gameData.cities[random.Next(gameData.cities.Count)];
					Player captor = civs[random.Next(civs.Count)];
					if (captor == city.owner) continue;
					CityInteractions.CaptureCity(city, captor);
					DrainMessages();
				} else {
					// A city is founded.
					Tile tile = land[random.Next(land.Count)];
					if (!CanFoundCityAt(tile)) continue;
					Player owner = civs[random.Next(civs.Count)];
					CityInteractions.BuildCity(tile, owner, owner.GetNextCityName());
					DrainMessages();
				}

				AssertAllActiveTilesMatch(gameData, context);
			}

			Assert.True(moves > 100, $"only {moves} moves happened");

			// None of this needed a recompute from scratch.
			foreach (Player p in gameData.players) {
				Assert.Equal(fullRecomputes[p], p.tileKnowledge.fullRecomputeCount);
			}
		} finally {
			C7GameData.GameData.rng = originalRng;
		}
	}

	[Fact]
	public void ActiveTilesNoticeTerrainChanges() {
		C7GameData.GameData gameData = NewGame();
		Player player = gameData.players.First(p => !p.isBarbarians);
		MapUnit unit = player.units.First();
		player.tileKnowledge.AddTilesToKnown(unit.location);
		AssertActiveTilesMatchFullRecompute(player, "start");

		// Founding a city clears the terrain around it.
		Tile forest = gameData.map.tiles.First(t => t.IsLand() && t.overlayTerrainType.allowedFoliageAction != TerrainType.Civ3FoliageAction.None
			&& t.unitsOnTile.Count == 0 && !t.HasCity() && t.IsAllowCities());
		MapUnit observer = gameData.SpawnUnit(player, gameData.unitPrototypes.Single(p => p.name == "Warrior"), forest.neighbors.Values.First(n => n != Tile.NONE && n.IsLand()));
		player.tileKnowledge.AddTilesToKnown(observer.location);
		AssertActiveTilesMatchFullRecompute(player, "observer");
		CityInteractions.BuildCity(forest, gameData.players.Last(p => !p.isBarbarians), "Clearing");
		DrainMessages();
		player.tileKnowledge.RecomputeActiveTiles();
		AssertActiveTilesMatchFullRecompute(player, "after the city cleared the forest");
	}

	// The tile ownership algorithm as it was before it was optimized.
	private static class ReferenceBorders {
		private static readonly MethodInfo resolve = typeof(C7GameData.GameData).GetMethod("ResolveTileOwnershipConflict", BindingFlags.NonPublic | BindingFlags.Instance);

		private static bool Resolve(C7GameData.GameData gd, City a, City b, Tile t, out City owner) {
			object[] args = { a, b, t, null };
			bool result = (bool)resolve.Invoke(gd, args);
			owner = (City)args[3];
			return result;
		}

		private static void AddTilesToKnown(TileKnowledge tk, Tile unitLocation) {
			tk.knownTiles.Add(unitLocation);
			tk.borderTiles.Remove(unitLocation);
			foreach (Tile t in tk.GetTilesVisibleToUnit(unitLocation)) {
				tk.knownTiles.Add(t);
				tk.borderTiles.Remove(t);
				foreach (Tile border in t.neighbors.Values) {
					if (border == Tile.NONE) {
						continue;
					}
					if (!tk.knownTiles.Contains(border)) {
						tk.borderTiles.Add(border);
					}
				}
			}
		}

		public static void UpdateTileOwners(C7GameData.GameData gd) {
			foreach (City city in gd.cities) {
				if (city.residents.Count == 0) {
					continue;
				}
				city.location.owningCity = city;
				foreach (Tile t in city.GetTilesWithinBorders()) {
					if (t.owningCity != null && Resolve(gd, t.owningCity, city, t, out City winnerCity)) {
						t.owningCity = winnerCity;
						AddTilesToKnown(t.owningCity.owner.tileKnowledge, t);
						continue;
					}
					t.owningCity = city;
					AddTilesToKnown(t.owningCity.owner.tileKnowledge, t);
				}
			}

			foreach (Player player in gd.players) {
				player.UpdateResourcesInBorders(gd.map.tiles.Where(t => t.owningCity?.owner == player));

				foreach (Tile t in player.tileKnowledge.knownTiles.Where(t => t.owningCity == null && t.GetEdgeNeighbors().Any(e => e.owningCity != null)).ToList()) {
					TryResolveOpposingNeighbors(gd, t, TileDirection.NORTHWEST, TileDirection.SOUTHEAST);
					if (t.owningCity != null) continue;
					TryResolveOpposingNeighbors(gd, t, TileDirection.NORTHEAST, TileDirection.SOUTHWEST);
				}
			}
		}

		private static void TryResolveOpposingNeighbors(C7GameData.GameData gd, Tile t, TileDirection dirA, TileDirection dirB) {
			if (!t.neighbors.TryGetValue(dirA, out Tile a) || !t.neighbors.TryGetValue(dirB, out Tile b)) return;
			if (a.owningCity == null || b.owningCity == null) return;
			if (a.owningCity.owner != b.owningCity.owner) return;
			if (!Resolve(gd, a.owningCity, b.owningCity, t, out City winnerCity)) return;
			if (t.baseTerrainType.Key == "ocean" && t.RankDistanceTo(winnerCity.location) > 2) {
				t.owningCity = null;
				return;
			}
			t.owningCity = winnerCity;
			AddTilesToKnown(winnerCity.owner.tileKnowledge, t);
		}
	}

	private static List<(int, int)> Coordinates(IEnumerable<Tile> tiles) {
		return tiles.Select(t => (t.XCoordinate, t.YCoordinate)).ToList();
	}

	private static void AssertSameState(C7GameData.GameData a, C7GameData.GameData b, string context) {
		Assert.Equal(a.map.tiles.Count, b.map.tiles.Count);
		for (int i = 0; i < a.map.tiles.Count; ++i) {
			Assert.True(a.map.tiles[i].owningCity?.id == b.map.tiles[i].owningCity?.id, $"{context}: owner of tile {a.map.tiles[i]} differs");
		}
		Assert.Equal(a.players.Count, b.players.Count);
		for (int i = 0; i < a.players.Count; ++i) {
			Player pa = a.players[i], pb = b.players[i];
			Assert.Equal(Coordinates(pa.tileKnowledge.knownTiles), Coordinates(pb.tileKnowledge.knownTiles));
			Assert.Equal(Coordinates(pa.tileKnowledge.borderTiles).ToHashSet(), Coordinates(pb.tileKnowledge.borderTiles).ToHashSet());
			Assert.Equal(pa.resourcesInBorders?.Keys.Select(r => r.Key).ToList(), pb.resourcesInBorders?.Keys.Select(r => r.Key).ToList());
			if (pa.resourcesInBorders != null) {
				foreach (Resource r in pa.resourcesInBorders.Keys) {
					Resource rb = pb.resourcesInBorders.Keys.Single(x => x.Key == r.Key);
					Assert.Equal(Coordinates(pa.resourcesInBorders[r]), Coordinates(pb.resourcesInBorders[rb]));
				}
			}
		}
	}

	private C7GameData.GameData Reload(C7GameData.GameData gameData) {
		byte[] json = SaveGame.FromGameData(gameData).ToCompactJSON();
		C7GameData.GameData loaded = SaveGame.FromJSON(json).ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(loaded);
		return loaded;
	}

	[Fact]
	public void UpdateTileOwnersMatchesTheOriginalAlgorithm() {
		Random random = new(77);
		C7GameData.GameData original = NewGame();
		FoundCities(original, random, 40);

		// Two identical copies, to run the new and the original algorithm on.
		C7GameData.GameData a = Reload(original);
		C7GameData.GameData b = Reload(original);
		EngineStorage.InitializeGameDataForTests(a);
		AssertSameState(a, b, "after loading");

		int gapResolutionsBefore = a.borderGapResolutionCount;
		for (int round = 0; round < 25; ++round) {
			// Grow the borders of some cities, by the same amount in both games.
			for (int k = 0; k < 6; ++k) {
				int index = random.Next(a.cities.Count);
				int extra = random.Next(1, 300);
				City ca = a.cities[index];
				City cb = b.cities.Single(c => c.id == ca.id);
				ca.perPlayerCulture[ca.owner] += extra;
				cb.perPlayerCulture[cb.owner] += extra;
			}

			EngineStorage.InitializeGameDataForTests(a);
			a.UpdateTileOwners();
			EngineStorage.InitializeGameDataForTests(b);
			ReferenceBorders.UpdateTileOwners(b);

			AssertSameState(a, b, $"round {round}");
		}

		// The rarely needed exact path for Laws VII and VIII was exercised.
		Assert.True(a.borderGapResolutionCount > gapResolutionsBefore, "Laws VII and VIII never gave away a tile");
	}

	[Fact]
	public void KnownTilesSurviveASaveInTheCompactAndTheOldFormat() {
		Random random = new(5);
		C7GameData.GameData a = NewGame();
		FoundCities(a, random, 10);
		foreach (Player p in a.players) {
			foreach (MapUnit u in p.units) {
				p.tileKnowledge.AddTilesToKnown(u.location);
			}
		}

		SaveGame save = SaveGame.FromGameData(a);
		Assert.All(save.Players, sp => Assert.Empty(sp.tileKnowledge));
		Assert.Contains(save.Players, sp => !string.IsNullOrEmpty(sp.knownTileIndices));

		// The compact format, as sent over the network.
		C7GameData.GameData b = SaveGame.FromJSON(save.ToCompactJSON()).ToGameData(fixture.behaviors);
		// The old format, as written by earlier versions.
		SaveGame oldStyle = SaveGame.FromJSON(save.ToCompactJSON());
		foreach (SavePlayer sp in oldStyle.Players) {
			if (string.IsNullOrEmpty(sp.knownTileIndices)) continue;
			sp.tileKnowledge = SavePlayer.DecodeTileIndices(sp.knownTileIndices)
				.Select(i => new TileLocation(oldStyle.Map.tiles[i].X, oldStyle.Map.tiles[i].Y)).ToList();
			sp.knownTileIndices = null;
		}
		string oldJson = System.Text.Encoding.UTF8.GetString(oldStyle.ToCompactJSON());
		Assert.Contains("\"tileKnowledge\":[{\"x\":", oldJson);
		Assert.DoesNotContain("knownTileIndices", oldJson);
		C7GameData.GameData c = SaveGame.LoadFromJSON(oldJson).ToGameData(fixture.behaviors);

		for (int i = 0; i < a.players.Count; ++i) {
			List<(int, int)> expected = Coordinates(a.players[i].tileKnowledge.knownTiles);
			Assert.Equal(expected, Coordinates(b.players[i].tileKnowledge.knownTiles));
			Assert.Equal(expected, Coordinates(c.players[i].tileKnowledge.knownTiles));
			Assert.Equal(Coordinates(b.players[i].tileKnowledge.borderTiles), Coordinates(c.players[i].tileKnowledge.borderTiles));
		}
	}

	[Fact]
	public void TileIndicesRoundTrip() {
		Random random = new(3);
		GameMap map = new() { numTilesWide = 200, numTilesTall = 100, tiles = new List<Tile>() };
		for (int i = 0; i < map.numTilesWide * map.numTilesTall / 2; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y });
		}
		List<Tile> tiles = Enumerable.Range(0, 5000).Select(_ => map.tiles[random.Next(map.tiles.Count)]).Distinct().ToList();
		string encoded = SavePlayer.EncodeTileIndices(tiles, map);
		List<int> decoded = SavePlayer.DecodeTileIndices(encoded);
		Assert.Equal(tiles, decoded.Select(i => map.tiles[i]).ToList());
		Assert.DoesNotContain('+', encoded);
		Assert.DoesNotContain('/', encoded);

		// Tiles that aren't on the map have to be saved by location.
		Assert.Null(SavePlayer.EncodeTileIndices(new[] { map.tiles[0], Tile.NONE }, map));
		Assert.Null(SavePlayer.EncodeTileIndices(new Tile[0], map));
	}

	[Fact]
	public void UnitAndCityLookupsStayCorrect() {
		C7GameData.GameData gameData = NewGame();
		Player player = gameData.players.First(p => !p.isBarbarians);
		UnitPrototype warrior = gameData.unitPrototypes.Single(p => p.name == "Warrior");

		void AssertLookupsMatch() {
			foreach (MapUnit u in gameData.mapUnits) {
				Assert.Same(gameData.mapUnits.Find(x => x.id == u.id), gameData.GetUnit(u.id));
			}
			foreach (City c in gameData.cities) {
				Assert.Same(gameData.cities.Find(x => x.id == c.id), gameData.GetCity(c.id));
			}
		}

		AssertLookupsMatch();
		MapUnit spawned = gameData.SpawnUnit(player, warrior, player.units[0].location);
		Assert.Same(spawned, gameData.GetUnit(spawned.id));

		City city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, "Lookup");
		Assert.Same(city, gameData.GetCity(city.id));

		// Units added to the list directly are found too.
		city.AddUnit(warrior, gameData);
		MapUnit produced = gameData.mapUnits[^1];
		Assert.Same(produced, gameData.GetUnit(produced.id));
		AssertLookupsMatch();

		gameData.RemoveUnit(spawned);
		Assert.Null(gameData.GetUnit(spawned.id));
		Assert.Null(gameData.GetUnit(ID.FromString("nothing-12345")));
		AssertLookupsMatch();

		CityInteractions.DestroyCity(city);
		DrainMessages();
		Assert.Null(gameData.GetCity(city.id));
		City another = CityInteractions.BuildCity(gameData.map.tiles.First(CanFoundCityAt), player, "Another");
		Assert.Same(another, gameData.GetCity(another.id));
		AssertLookupsMatch();

		// Replacing the list replaces the index.
		gameData.mapUnits = new List<MapUnit>();
		Assert.Null(gameData.GetUnit(produced.id));
	}

	[Fact]
	public void UIControllerPlayerFollowsTheController() {
		C7GameData.GameData gameData = NewGame();
		ID original = EngineStorage.uiControllerID;
		try {
			foreach (Player p in gameData.players) {
				EngineStorage.uiControllerID = p.id;
				Assert.Same(p, gameData.GetUIControllerPlayer());
				Assert.Same(p, gameData.GetUIControllerPlayer());
			}
			EngineStorage.uiControllerID = ID.FromString("nobody-999");
			Assert.Null(gameData.GetUIControllerPlayer());
		} finally {
			EngineStorage.uiControllerID = original;
		}
	}

	[Fact]
	public void WaitingUnitsAreSelectedLast() {
		C7GameData.GameData gameData = NewGame();
		Player human = gameData.players.First(p => p.isHuman);
		ID original = EngineStorage.uiControllerID;
		EngineStorage.uiControllerID = human.id;
		try {
			UnitInteractions.ClearWaitQueue();
			List<MapUnit> units = human.units.Where(u => u.movementPoints.canMove && !u.isFortified).ToList();
			Assert.True(units.Count >= 2);

			UnitInteractions.waitUnit(units[0].id);
			Assert.Same(units[1], UnitInteractions.getNextSelectedUnit());

			foreach (MapUnit u in units.Skip(1)) {
				UnitInteractions.waitUnit(u.id);
			}
			// Everyone is waiting, so they come back in the order they waited.
			Assert.Same(units[0], UnitInteractions.getNextSelectedUnit());

			// A missing unit is ignored.
			UnitInteractions.waitUnit(ID.FromString("nothing-12345"));
		} finally {
			UnitInteractions.ClearWaitQueue();
			EngineStorage.uiControllerID = original;
		}
	}

	[Fact]
	public void OwnedTileListsMatchFiltering() {
		Random random = new(9);
		C7GameData.GameData gameData = NewGame();
		FoundCities(gameData, random, 10);
		foreach (Player p in gameData.players) {
			List<Tile> owned = p.tileKnowledge.knownTiles.Where(t => t.OwningPlayer() == p).ToList();
			Assert.Equal(owned, p.tileKnowledge.OwnedTiles());
			Assert.Equal(owned.Where(t => t.IsCountedForDomination()).ToList(), p.tileKnowledge.DominationTiles());
			Assert.Equal(owned.Where(t => t.IsCountedForScore()).ToList(), p.tileKnowledge.ScoreTiles());
		}
	}
}

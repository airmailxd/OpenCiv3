using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Tests for the caches and data structures behind the core game types (IDs,
// tiles and their neighbors, terrain, units and unit prototypes), checking
// they behave exactly like the code they replaced.
public class PerfCoreTypesTests {
	[Fact]
	public void EqualIDsHaveEqualHashes() {
		ID a = ID.FromString("Man-O-War-12");
		ID b = ID.FromString("Man-O-War-12");
		ID c = ID.FromString("Man-O-War-13");
		ID none = ID.None("tile");

		Assert.Equal(a, b);
		Assert.True(a.Equals(b));
		Assert.Equal(a.GetHashCode(), b.GetHashCode());
		Assert.NotEqual(a, c);
		Assert.False(a.Equals((ID)null));
		Assert.Equal(none, ID.None("tile"));
		Assert.Equal(none.GetHashCode(), ID.None("tile").GetHashCode());
		Assert.Equal("Man-O-War-12", a.ToString());

		Dictionary<ID, int> dict = new() { [a] = 1, [none] = 2 };
		Assert.Equal(1, dict[b]);
		Assert.Equal(2, dict[ID.None("tile")]);
		Assert.False(dict.ContainsKey(c));
	}

	[Fact]
	public void TileNeighborsBehaveLikeADictionary() {
		Tile n = new(ID.None("tile")), e = new(ID.None("tile")), s = new(ID.None("tile"));
		TileNeighbors neighbors = new();

		Assert.Empty(neighbors);
		Assert.Throws<KeyNotFoundException>(() => neighbors[TileDirection.NORTH]);
		Assert.Throws<KeyNotFoundException>(() => neighbors[TileDirection.INVALID]);
		Assert.False(neighbors.TryGetValue(TileDirection.NORTH, out _));

		// Enumerates in insertion order, like a Dictionary.
		neighbors[TileDirection.SOUTH] = s;
		neighbors[TileDirection.NORTH] = n;
		neighbors[TileDirection.EAST] = e;
		neighbors[TileDirection.NORTH] = Tile.NONE; // replacing keeps its position
		Assert.Equal(3, neighbors.Count);
		Assert.Equal([TileDirection.SOUTH, TileDirection.NORTH, TileDirection.EAST], neighbors.Keys.ToArray());
		Assert.Equal([s, Tile.NONE, e], neighbors.Values.ToArray());
		Assert.Equal([TileDirection.SOUTH, TileDirection.NORTH, TileDirection.EAST], neighbors.Select(p => p.Key).ToArray());
		List<(TileDirection, Tile)> pairs = new();
		foreach ((TileDirection dir, Tile t) in neighbors) {
			pairs.Add((dir, t));
		}
		Assert.Equal([(TileDirection.SOUTH, s), (TileDirection.NORTH, Tile.NONE), (TileDirection.EAST, e)], pairs);

		Assert.True(neighbors.ContainsKey(TileDirection.EAST));
		Assert.False(neighbors.ContainsKey(TileDirection.WEST));
		Assert.True(neighbors.ContainsValue(e));
		Assert.False(neighbors.ContainsValue(n));
		Assert.True(neighbors.TryGetValue(TileDirection.EAST, out Tile east) && east == e);
		Assert.Same(s, neighbors[TileDirection.SOUTH]);

		// A Dictionary can still be assigned to Tile.neighbors.
		Tile tile = new(ID.None("tile"));
		tile.neighbors = new Dictionary<TileDirection, Tile> { [TileDirection.WEST] = e };
		Assert.Same(e, tile.neighbors[TileDirection.WEST]);
		Assert.Single(tile.neighbors);

		// Tile.NONE has no neighbors at all.
		Assert.Empty(Tile.NONE.neighbors);
	}

	private static GameMap MakeMap(int width, int height, bool wrapHorizontally, bool wrapVertically = false) {
		GameMap map = new() { numTilesWide = width, numTilesTall = height, wrapHorizontally = wrapHorizontally, wrapVertically = wrapVertically };
		for (int i = 0; i < width * height / 2; i++) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y, map = map });
		}
		map.computeNeighbors();
		return map;
	}

	// The implementation of Tile.GetTileAtNeighborIndex before its offsets
	// were precomputed.
	private static Tile OldGetTileAtNeighborIndex(Tile tile, int neighborIndex) {
		if (neighborIndex <= 0) {
			return tile;
		}
		int xDelta, yDelta;
		int ringNumber = 0;
		do {
			ringNumber++;
		} while (Math.Pow(2 * ringNumber + 1, 2) <= neighborIndex);
		int cellsInInnerRings = (ringNumber * 2 - 1) * (ringNumber * 2 - 1);
		int indexInRing1Based = neighborIndex - cellsInInnerRings;
		int cellsPerSquareEdge = ringNumber * 2;
		int segment1End = cellsPerSquareEdge;
		int segment2End = 2 * cellsPerSquareEdge;
		int segment3End = 3 * cellsPerSquareEdge;
		int segment4End = 4 * cellsPerSquareEdge;
		if (indexInRing1Based <= segment1End) {
			xDelta = indexInRing1Based;
			yDelta = indexInRing1Based - cellsPerSquareEdge;
		} else if (indexInRing1Based <= segment2End) {
			xDelta = segment2End - indexInRing1Based;
			yDelta = indexInRing1Based - cellsPerSquareEdge;
		} else if (indexInRing1Based <= segment3End) {
			xDelta = segment2End - indexInRing1Based;
			yDelta = segment3End - indexInRing1Based;
		} else {
			xDelta = indexInRing1Based - segment4End;
			yDelta = segment3End - indexInRing1Based;
		}
		return tile.map.tileAt(tile.XCoordinate + xDelta, tile.YCoordinate + yDelta);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public void NeighborIndexTilesMatchTheOriginalSpiral(bool wrapHorizontally, bool wrapVertically) {
		GameMap map = MakeMap(16, 12, wrapHorizontally, wrapVertically);
		Tile[] samples = [
			map.tileAt(0, 0), map.tileAt(1, 1), map.tileAt(14, 0), map.tileAt(15, 11),
			map.tileAt(8, 6), map.tileAt(7, 5), map.tileAt(0, 10), map.tileAt(2, 4),
		];
		foreach (Tile tile in samples) {
			Assert.NotEqual(Tile.NONE, tile);
			for (int rank = 0; rank <= 6; rank++) {
				int count = (rank * 2 + 1) * (rank * 2 + 1);
				List<Tile> square = new();
				List<Tile> withinRank = new();
				for (int i = 0; i < count; i++) {
					Tile expected = OldGetTileAtNeighborIndex(tile, i);
					Assert.Same(expected, tile.GetTileAtNeighborIndex(i));
					// The square and rank lists skip off-map tiles.
					if (expected == Tile.NONE) {
						continue;
					}
					square.Add(expected);
					if (tile.RankDistanceTo(expected) <= rank) {
						withinRank.Add(expected);
					}
				}
				Assert.Equal(square, tile.GetTilesWithinTileSquare(rank));
				Assert.Equal(withinRank, tile.GetTilesWithinRankDistance(rank));
			}
		}
		// Large indices, out of order, still match.
		Tile center = map.tileAt(8, 6);
		foreach (int i in new[] { 400, 3, 168, 169, 170, 1000, 0, -1 }) {
			Assert.Same(OldGetTileAtNeighborIndex(center, i), center.GetTileAtNeighborIndex(i));
		}
	}

	[Fact]
	public void ComputedNeighborsMatchTheMap() {
		GameMap map = MakeMap(10, 8, wrapHorizontally: false);
		foreach (Tile tile in map.tiles) {
			Assert.Equal(8, tile.neighbors.Count);
			Assert.Equal(TileDirectionExtensions.All, tile.neighbors.Keys.ToArray());
			foreach (TileDirection dir in TileDirectionExtensions.All) {
				Assert.Same(map.tileNeighbor(tile, dir), tile.neighbors[dir]);
			}
			Tile[] edges = tile.GetEdgeNeighbors();
			Assert.Equal(new[] { TileDirection.NORTHEAST, TileDirection.NORTHWEST, TileDirection.SOUTHEAST, TileDirection.SOUTHWEST }
				.Select(d => tile.neighbors[d]).Where(t => t != Tile.NONE), edges);
			Assert.Equal(edges.Any(t => t == Tile.NONE), tile.AnyEdgeNeighbor(t => t == Tile.NONE));
		}
	}

	[Fact]
	public void TerrainFlagsFollowTheKey() {
		TerrainType t = new();
		Assert.False(t.isWater());
		t.Key = "coast";
		Assert.True(t.isWater() && t.isCoast() && t.IsCoast && t.IsWater);
		Assert.False(t.isSea() || t.isHilly() || t.IsOcean);
		t.Key = "ocean";
		Assert.True(t.isWater() && t.IsOcean);
		Assert.False(t.isCoast());
		t.Key = "volcano";
		Assert.True(t.isHilly() && t.isVolcano() && t.IsVolcano);
		Assert.False(t.isWater());
		t.Key = "grassland";
		Assert.True(t.IsGrassland);
		Assert.False(t.isHilly() || t.isWater() || t.IsVolcano);

		TerrainType init = new() { Key = "hills" };
		Assert.True(init.isHilly() && init.IsHills);
	}

	[Fact]
	public void ContinentsCanBeRecomputed() {
		GameMap map = MakeMap(12, 10, wrapHorizontally: false);
		TerrainType land = new() { Key = "grassland" };
		TerrainType water = new() { Key = "coast" };
		foreach (Tile t in map.tiles) {
			t.baseTerrainType = (t.XCoordinate < 4 || t.XCoordinate > 8) ? land : water;
		}

		map.recomputeContinents();
		List<int> sizes = map.continents.Select(c => c.Count).ToList();
		foreach (HashSet<Tile> continent in map.continents) {
			foreach (Tile t in continent.Where(t => t != Tile.NONE)) {
				Assert.Equal(continent.Count, map.ContinentSize(t.continent));
			}
		}

		map.recomputeContinents();
		Assert.Equal(sizes, map.continents.Select(c => c.Count).ToList());
		Assert.Equal(map.tiles.Count, map.continents.Sum(c => c.Count(t => t != Tile.NONE)));
		foreach (Tile t in map.tiles) {
			Assert.Equal(map.continents.First(c => c.Contains(t)).Count, map.ContinentSize(t.continent));
		}
		Assert.Equal(0, map.ContinentSize(-1));
		Assert.Equal(0, map.ContinentSize(1000));
	}

	[Fact]
	public void UnitCategoryFlagsFollowTheCategories() {
		UnitPrototype proto = new();
		Assert.False(proto.IsLandUnit());
		proto.categories.Add("Land");
		Assert.True(proto.IsLandUnit());
		Assert.False(proto.IsSeaUnit() || proto.IsAirUnit());

		proto.categories = new HashSet<string> { "Sea" };
		Assert.True(proto.IsSeaUnit());
		Assert.False(proto.IsLandUnit());

		Assert.False(proto.HasLoadAction());
		proto.actions.Add(UnitAction.Load);
		Assert.True(proto.HasLoadAction());
		Assert.False(proto.HasUnloadAction());
		proto.actions = new HashSet<UnitAction> { UnitAction.Unload };
		Assert.True(proto.HasUnloadAction());
		Assert.False(proto.HasLoadAction());
	}

	[Fact]
	public void SlaveArtIsFoundAndFollowsTheArt() {
		UnitPrototype proto = new() {
			art = new Art { mainArt = new MainArt { defaultName = "Worker", variations = new() { ["Ancient"] = "WorkerA", ["WorkerSLAVE"] = "Slave" } } }
		};
		Assert.Equal("Slave", proto.GetSlaveArtName());
		proto.art = new Art { mainArt = new MainArt { defaultName = "Worker", variations = new() { ["Ancient"] = "WorkerA" } } };
		Assert.Null(proto.GetSlaveArtName());
	}

	[Fact]
	public void UpgradeOrderPutsUpgradesLast() {
		UnitPrototype a = new() { name = "A" }, b = new() { name = "B" }, c = new() { name = "C" }, x = new() { name = "X" };
		a.upgradesTo.Add(b);
		b.upgradesTo.Add(c);

		// The comparer this replaced put A last here.
		Assert.NotEqual(a, UnitPrototype.SortInUpgradeOrder([b, x, a]).Last());
		Assert.Equal([a, b, c], UnitPrototype.SortInUpgradeOrder([c, a, b]));
		Assert.Equal([a, c], UnitPrototype.SortInUpgradeOrder([c, a]));
		Assert.Equal([x, a, c], UnitPrototype.SortInUpgradeOrder([x, c, a]));

		// Every unit comes before the units it upgrades to, in any order.
		UnitPrototype[] all = [a, b, c, x];
		foreach (UnitPrototype[] order in Permutations(all)) {
			List<UnitPrototype> sorted = UnitPrototype.SortInUpgradeOrder(order.ToList());
			Assert.Equal(4, sorted.Count);
			Assert.True(sorted.IndexOf(a) < sorted.IndexOf(b));
			Assert.True(sorted.IndexOf(b) < sorted.IndexOf(c));
		}
	}

	private static IEnumerable<UnitPrototype[]> Permutations(UnitPrototype[] items) {
		if (items.Length <= 1) {
			yield return items;
			yield break;
		}
		for (int i = 0; i < items.Length; i++) {
			UnitPrototype[] rest = items.Where((_, j) => j != i).ToArray();
			foreach (UnitPrototype[] perm in Permutations(rest)) {
				yield return [items[i], .. perm];
			}
		}
	}

	[Fact]
	public void UpgradeCacheSeesChangedPrototypes() {
		C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty(), rules = new Rules() };
		EngineStorage.InitializeGameDataForTests(gameData);
		Civilization civ = new() { name = "Testers" };
		Player player = new() { civilization = civ, government = new Government() };
		gameData.players.Add(player);
		Tile tile = new(ID.None("tile"));
		City city = new(tile, player, "Testville", ID.None("city"));

		UnitPrototype warrior = new() { name = "Warrior" }, swordsman = new() { name = "Swordsman" }, pikeman = new() { name = "Pikeman" };
		foreach (UnitPrototype p in new[] { warrior, swordsman, pikeman }) {
			p.categories.Add("Land");
			gameData.unitPrototypes.Add(p);
		}
		warrior.producibleBy.Add(civ);
		HashSet<Resource> none = [];

		Assert.Null(warrior.GetProducibleUpgrade(city, none));

		warrior.upgradesTo.Add(swordsman);
		Assert.Null(warrior.GetProducibleUpgrade(city, none)); // not buildable by the civ yet
		swordsman.producibleBy.Add(civ);
		Assert.Equal(swordsman, warrior.GetProducibleUpgrade(city, none));

		swordsman.upgradesTo.Add(pikeman);
		pikeman.producibleBy.Add(civ);
		Assert.Equal(pikeman, warrior.GetProducibleUpgrade(city, none));
		Assert.Equal(pikeman, swordsman.GetProducibleUpgrade(city, none));
		Assert.True(warrior.UpgradesEventuallyTo(pikeman));

		// A prototype that isn't one of the game's still works.
		UnitPrototype outsider = new() { name = "Outsider" };
		outsider.categories.Add("Land");
		outsider.upgradesTo.Add(swordsman);
		Assert.Equal(pikeman, outsider.GetProducibleUpgrade(city, none));

		// Replacing the game's prototype list is noticed too.
		gameData.unitPrototypes = [warrior];
		warrior.upgradesTo.Clear();
		Assert.Null(warrior.GetProducibleUpgrade(city, none));
	}
}

// Defender choice, checked against the original quadratic comparison.
public class PerfCoreTypesDefenderTests : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public PerfCoreTypesDefenderTests(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		gameData.SpawnUnit(owner, gameData.unitPrototypes.Single(p => p.name == prototype), tile);
		return tile.unitsOnTile.Last();
	}

	private static MapUnit OldFindTopCombatUnit(MapUnit opponent, List<MapUnit> units) {
		if (units.Count < 1) return MapUnit.NONE;
		MapUnit leadingCandidate = units[0];
		foreach (MapUnit u in units)
			if (u.HasPriorityAsDefender(leadingCandidate, opponent))
				leadingCandidate = u;
		return leadingCandidate;
	}

	[Fact]
	public void TopDefenderMatchesTheOriginalChoice() {
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && t.OwningPlayer() == null);
		Tile from = tile.neighbors.Values.First(t => t != Tile.NONE && t.IsLand());
		MapUnit attacker = Spawn(them, "Warrior", from);

		// Ties included: two identical spearmen, the second one damaged and
		// then healed.
		MapUnit warrior = Spawn(us, "Warrior", tile);
		MapUnit spear1 = Spawn(us, "Spearman", tile);
		MapUnit spear2 = Spawn(us, "Spearman", tile);
		MapUnit worker = Spawn(us, "Worker", tile);
		Assert.Equal(OldFindTopCombatUnit(attacker, tile.unitsOnTile.Where(u => u.CanDefendAgainst(attacker) && !u.IsInArmy()).ToList()),
			tile.FindTopDefender(attacker));
		Assert.Equal(spear1, tile.FindTopDefender(attacker));

		spear1.hitPointsRemaining = 1;
		Assert.Equal(spear2, tile.FindTopDefender(attacker));
		spear2.hitPointsRemaining = 1;
		Assert.Equal(OldFindTopCombatUnit(attacker, tile.unitsOnTile.Where(u => u.CanDefendAgainst(attacker) && !u.IsInArmy()).ToList()),
			tile.FindTopDefender(attacker));

		foreach (List<MapUnit> order in new[] {
				new List<MapUnit> { worker, spear2, warrior, spear1 },
				new List<MapUnit> { spear1, spear2, worker, warrior },
				new List<MapUnit> { warrior },
				new List<MapUnit>(),
			}) {
			Assert.Equal(OldFindTopCombatUnit(attacker, order), tile.FindTopCombatUnit(attacker, order));
		}

		// Bombarding without lethal bombard skips units on their last hit
		// point.
		MapUnit catapult = Spawn(them, "Catapult", from);
		MapUnit target = tile.FindTopDefenderForBombard(catapult);
		List<MapUnit> healthy = tile.unitsOnTile.Where(u => u.IsCombatUnit() && !u.IsInArmy() && u.CompositeHitPoints() > 1).ToList();
		Assert.Equal(OldFindTopCombatUnit(catapult, healthy), target);
	}
}

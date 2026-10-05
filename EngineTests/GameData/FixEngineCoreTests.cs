using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Regression tests for fixes to tiles, the map, IDs and unit prototypes that
// don't need a full game.
public class FixEngineCoreTests {
	private static readonly TerrainType Land = new() { Key = "grassland" };
	private static readonly TerrainType Water = new() { Key = "coast" };

	private static GameMap MakeMap(int width, int height, bool wrapHorizontally, bool wrapVertically = false, TerrainType terrain = null) {
		GameMap map = new() { numTilesWide = width, numTilesTall = height, wrapHorizontally = wrapHorizontally, wrapVertically = wrapVertically };
		for (int i = 0; i < width * height / 2; i++) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y, map = map, baseTerrainType = terrain ?? Land });
		}
		map.computeNeighbors();
		return map;
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public void TileRangesNeverContainTheNoneTile(bool wrapHorizontally, bool wrapVertically) {
		GameMap map = MakeMap(16, 12, wrapHorizontally, wrapVertically);
		Tile topLeft = map.tileAt(0, 0);
		Tile topRight = map.tileAt(14, 0);
		Tile bottomRight = map.tileAt(15, 11);
		foreach (Tile tile in new[] { topLeft, topRight, bottomRight, map.tileAt(8, 6) }) {
			Assert.NotEqual(Tile.NONE, tile);
			for (int rank = 0; rank <= 4; rank++) {
				List<Tile> withinRank = tile.GetTilesWithinRankDistance(rank);
				List<Tile> square = tile.GetTilesWithinTileSquare(rank);
				Assert.DoesNotContain(Tile.NONE, withinRank);
				Assert.DoesNotContain(Tile.NONE, square);
				Assert.Equal(tile, withinRank[0]);
				Assert.Equal(tile, square[0]);
				// What callers do with the tiles must work.
				Assert.All(withinRank, t => Assert.True(tile.RankDistanceTo(t) <= rank));
			}
			Assert.DoesNotContain(Tile.NONE, tile.GetEdgeNeighbors());
			Assert.False(tile.AnyEdgeNeighbor(t => t == Tile.NONE));
		}

		// A corner tile on a map that doesn't wrap vertically has fewer tiles
		// around it than one in the middle.
		if (!wrapVertically) {
			Assert.True(topLeft.GetTilesWithinRankDistance(2).Count < map.tileAt(8, 6).GetTilesWithinRankDistance(2).Count);
		}
	}

	[Fact]
	public void SingleOffMapNeighborIndexIsStillTheNoneTile() {
		GameMap map = MakeMap(16, 12, wrapHorizontally: false);
		// GetTileAtNeighborIndex asks about one place, so NONE still answers
		// "off the map" there.
		Assert.Equal(Tile.NONE, map.tileAt(0, 0).GetTileAtNeighborIndex(1));
	}

	[Fact]
	public void ContinentsDoNotIncludeTheNoneTile() {
		int noneContinent = Tile.NONE.continent;
		GameMap map = MakeMap(10, 8, wrapHorizontally: false);
		map.recomputeContinents();

		// All land, so one continent of exactly the map's tiles.
		Assert.Equal(map.tiles.Count, map.ContinentSize(map.tiles[0].continent));
		Assert.Equal(map.tiles.Count, map.continents.Sum(c => c.Count));
		Assert.DoesNotContain(map.continents, c => c.Contains(Tile.NONE));
		Assert.Equal(noneContinent, Tile.NONE.continent);
	}

	[Fact]
	public void OffMapTilesDoNotMakeLandStrips() {
		// Land everywhere except two water tiles side by side on the top row.
		// Their only shared corner tiles are one land tile to the south and
		// the edge of the map to the north, so this is not a land strip.
		GameMap map = MakeMap(10, 8, wrapHorizontally: false);
		Tile west = map.tileAt(0, 0);
		Tile east = map.tileAt(2, 0);
		west.baseTerrainType = Water;
		east.baseTerrainType = Water;

		Assert.False(GameMap.IsLandStrip(east, west));
		Assert.False(GameMap.IsLandStrip(west, east));

		map.recomputeContinents();
		Assert.Equal(west.continent, east.continent);
		Assert.Equal(2, map.ContinentSize(west.continent));
		Assert.Equal(map.tiles.Count, map.continents.Sum(c => c.Count));

		// A real strip, with land on both corners, still splits the water.
		Tile west2 = map.tileAt(0, 4);
		Tile east2 = map.tileAt(2, 4);
		west2.baseTerrainType = Water;
		east2.baseTerrainType = Water;
		Assert.True(GameMap.IsLandStrip(east2, west2));
	}

	[Fact]
	public void IdsParseTheirOwnStrings() {
		Assert.Equal(ID.None("tile"), ID.FromString("tile-none"));
		Assert.Equal(ID.None("Man-O-War"), ID.FromString("Man-O-War-none"));
		Assert.Equal("tile-none", ID.FromString(ID.None("tile").ToString()).ToString());
		Assert.Equal("Man-O-War-12", ID.FromString("Man-O-War-12").ToString());
	}

	[Theory]
	[InlineData("warrior")]
	[InlineData("warrior-")]
	[InlineData("warrior-x")]
	[InlineData("warrior-99999999999")]
	[InlineData("")]
	[InlineData(null)]
	public void MalformedIdsThrowFormatException(string str) {
		Assert.Throws<FormatException>(() => ID.FromString(str));
	}

	[Fact]
	public void ToStringOverridesAreUsedThroughObject() {
		TradeOffer offer = new() { gold = 5, partOfPeaceTreaty = true };
		Assert.Equal(offer.ToString(), ((object)offer).ToString());
		Assert.NotEqual(typeof(TradeOffer).FullName, ((object)offer).ToString());
	}

	[Fact]
	public void PrototypeActionsAreCopiedFromTheSave() {
		SaveUnitPrototype save = new() { name = "Catapult" };
		save.actions.Add(UnitAction.Bombard);
		UnitPrototype proto = new(save, []);

		proto.actions.Add(UnitAction.Fortify);
		Assert.DoesNotContain(UnitAction.Fortify, save.actions);
		Assert.Contains(UnitAction.Bombard, proto.actions);
		Assert.NotSame(save.actions, proto.actions);
	}

	private static (C7GameData.GameData, Civilization, City) MakeUpgradeGame() {
		C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty(), rules = new Rules() };
		EngineStorage.InitializeGameDataForTests(gameData);
		Civilization civ = new() { name = "Testers" };
		Player player = new() { civilization = civ, government = new Government() };
		gameData.players.Add(player);
		Tile tile = new(ID.None("tile"));
		City city = new(tile, player, "Testville", ID.None("city"));
		return (gameData, civ, city);
	}

	private static UnitPrototype AddPrototype(C7GameData.GameData gameData, Civilization civ, string name) {
		UnitPrototype p = new() { name = name };
		p.categories.Add("Land");
		p.producibleBy.Add(civ);
		gameData.unitPrototypes.Add(p);
		return p;
	}

	[Fact]
	public void CyclicUpgradesDoNotLoopForever() {
		(C7GameData.GameData gameData, Civilization civ, City city) = MakeUpgradeGame();
		UnitPrototype a = AddPrototype(gameData, civ, "A");
		UnitPrototype b = AddPrototype(gameData, civ, "B");
		a.upgradesTo.Add(b);
		b.upgradesTo.Add(a);

		Task<UnitPrototype> task = Task.Run(() => a.GetProducibleUpgrade(city, []));
		Assert.True(task.Wait(TimeSpan.FromSeconds(10)), "GetProducibleUpgrade looped");
		Assert.Equal(b, task.Result);
		Assert.Equal(a, b.GetProducibleUpgrade(city, []));
	}

	[Fact]
	public void NullUpgradeListsAreTolerated() {
		(C7GameData.GameData gameData, Civilization civ, City city) = MakeUpgradeGame();
		UnitPrototype a = AddPrototype(gameData, civ, "A");
		UnitPrototype b = AddPrototype(gameData, civ, "B");
		a.upgradesTo = null;
		b.upgradesTo.Add(a);

		Assert.Null(a.GetProducibleUpgrade(city, []));
		Assert.Equal(a, b.GetProducibleUpgrade(city, []));
	}

	[Fact]
	public void UpgradeCacheSeesChangesAfterCachedCalls() {
		(C7GameData.GameData gameData, Civilization civ, City city) = MakeUpgradeGame();
		UnitPrototype warrior = AddPrototype(gameData, civ, "Warrior");
		UnitPrototype swordsman = AddPrototype(gameData, civ, "Swordsman");
		UnitPrototype medieval = AddPrototype(gameData, civ, "Medieval Infantry");
		warrior.upgradesTo.Add(swordsman);

		// Repeated calls in the same state take the fast path and agree.
		for (int i = 0; i < 3; i++) {
			Assert.Equal(swordsman, warrior.GetProducibleUpgrade(city, []));
			Assert.Null(swordsman.GetProducibleUpgrade(city, []));
		}

		swordsman.upgradesTo.Add(medieval);
		Assert.Equal(medieval, warrior.GetProducibleUpgrade(city, []));
		Assert.Equal(medieval, swordsman.GetProducibleUpgrade(city, []));

		// The best upgrade is the one furthest along, whichever order the
		// candidates come in.
		warrior.upgradesTo = [medieval, swordsman];
		Assert.Equal(medieval, warrior.GetProducibleUpgrade(city, []));

		// Taking a civ off a set the cache already saw is noticed.
		medieval.producibleBy.Remove(civ);
		Assert.Equal(swordsman, warrior.GetProducibleUpgrade(city, []));

		// So is a new prototype list.
		gameData.unitPrototypes = [warrior];
		warrior.upgradesTo = [];
		Assert.Null(warrior.GetProducibleUpgrade(city, []));
	}
}

// Fixes that need a real game: bombardment, forest clearing, terraforms and
// cities on the map edges.
public class FixEngineCoreGameTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public FixEngineCoreGameTests(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		C7GameData.GameData.rng = new Random(1234);

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		us = civs[0];
		them = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City FoundCity(Player player, Tile tile, int size = 1) {
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
			});
		}
		return city;
	}

	private Tile SettlerTile(Player player) {
		return player.units.First(u => u.unitType.isSettler).location;
	}

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		gameData.SpawnUnit(owner, gameData.unitPrototypes.Single(p => p.name == prototype), tile);
		return tile.unitsOnTile.Last();
	}

	[Fact]
	public void CitiesInTheTopCornersHaveBorders() {
		GameMap map = gameData.map;
		Tile topLeft = map.tiles.First(t => t.YCoordinate == 0);
		Tile topRight = map.tiles.Last(t => t.YCoordinate == 0);
		foreach (Tile tile in new[] { topLeft, topRight }) {
			tile.baseTerrainType = SettlerTile(us).baseTerrainType;
			City city = FoundCity(us, tile);
			List<Tile> borders = city.GetTilesWithinBorders();
			Assert.Contains(tile, borders);
			Assert.DoesNotContain(Tile.NONE, borders);
		}
	}

	// A city of theirs next to a bombarding unit of ours. The bombarder never
	// misses.
	private (City, MapUnit) SetUpBombard(int size) {
		Tile site = SettlerTile(them);
		City city = FoundCity(them, site, size);
		Tile from = site.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity());
		MapUnit catapult = Spawn(us, "Catapult", from);
		catapult.unitType.bombard = 1_000_000;
		us.DeclareWarOn(them, gameData.turn);
		return (city, catapult);
	}

	private async Task BombardOnce(MapUnit unit, Tile tile) {
		unit.hasAttackedThisTurn = false;
		unit.movementPoints.reset(unit.unitType.movement);
		await unit.Bombard(tile);
	}

	private Building Walls() {
		return gameData.Buildings.First(b => b.providesWalls && b.greatWonderProperties == null && !b.isSmallWonder);
	}

	// A great wonder that gives walls in every city, like the Great Wall
	// in some rulesets.
	private Building GreatWall() {
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder() && b.greatWonderProperties.buildingGainedInEveryCityOnContinent == null);
		wonder.greatWonderProperties.buildingGainedInEveryCity = Walls();
		return wonder;
	}

	[Fact]
	public async Task WallsGrantedByAWonderDoNotStopBombardment() {
		(City city, MapUnit catapult) = SetUpBombard(size: 4);
		Building greatWall = GreatWall();
		city.AddBuilding(greatWall);
		Assert.Contains(city.GetBuildings(), cb => cb.building.providesWalls);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building.providesWalls);

		// With only indestructible walls, the units in the city are hit.
		MapUnit defender = Spawn(them, "Spearman", city.location);
		int hitPoints = defender.hitPointsRemaining;
		await BombardOnce(catapult, city.location);
		Assert.True(defender.hitPointsRemaining < hitPoints);
		Assert.Contains(city.GetBuildings(), cb => cb.building.providesWalls);

		// With no units, the city itself is.
		defender.RemoveFromPlay();
		int buildings = city.constructed_buildings.Count;
		int population = city.residents.Count;
		await BombardOnce(catapult, city.location);
		Assert.True(city.constructed_buildings.Count < buildings || city.residents.Count < population);
		Assert.Contains(city.constructed_buildings, cb => cb.building == greatWall);
	}

	[Fact]
	public async Task BuiltWallsAreBombardedFirst() {
		(City city, MapUnit catapult) = SetUpBombard(size: 2);
		Building walls = Walls();
		city.AddBuilding(walls);
		city.AddBuilding(GreatWall());
		MapUnit defender = Spawn(them, "Spearman", city.location);
		int hitPoints = defender.hitPointsRemaining;

		await BombardOnce(catapult, city.location);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == walls);
		Assert.Equal(hitPoints, defender.hitPointsRemaining);
		// The Great Wall's walls remain.
		Assert.Contains(city.GetBuildings(), cb => cb.building.providesWalls);
	}

	[Fact]
	public async Task BombardmentDoesNotDestroyWonders() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		Building greatWonder = gameData.Buildings.First(b => b.IsGreatWonder() && !(b.greatWonderProperties.buildingGainedInEveryCity?.providesWalls ?? false));
		Building smallWonder = gameData.Buildings.First(b => b.isSmallWonder && !b.isCenterOfEmpire);
		city.AddBuilding(greatWonder);
		city.AddBuilding(smallWonder);
		Assert.Contains(city.constructed_buildings, cb => cb.building.isCenterOfEmpire);
		int buildings = city.constructed_buildings.Count;

		// Size 1, so population can't be hit either: nothing is destroyed.
		for (int i = 0; i < 20; i++) {
			await BombardOnce(catapult, city.location);
		}
		Assert.Equal(buildings, city.constructed_buildings.Count);
		Assert.Contains(city.constructed_buildings, cb => cb.building == greatWonder);
		Assert.Contains(city.constructed_buildings, cb => cb.building == smallWonder);
	}

	[Fact]
	public void ForestShieldsGoOnlyToTheOwningCity() {
		// Our city and theirs, with a forest between them that we own.
		City ours = FoundCity(us, SettlerTile(us));
		Tile forest = ours.location.neighbors[TileDirection.EAST];
		Tile theirSite = forest.neighbors[TileDirection.EAST];
		theirSite.baseTerrainType = ours.location.baseTerrainType;
		City theirs = FoundCity(them, theirSite);
		forest.owningCity = ours;

		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");
		foreach (City c in new[] { ours, theirs }) {
			c.SetItemBeingProduced(warrior);
			c.SetStoredShields(0);
		}

		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields();
		Assert.True(ours.shieldsStored > 0 || gameData.rules.ForestValueInShields == 0);
		Assert.Equal(0, theirs.shieldsStored);

		// A city building a wonder gets nothing, and nobody else does either.
		ours.SetStoredShields(0);
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder());
		ours.SetItemBeingProduced(wonder);
		ours.SetStoredShields(0);
		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields();
		Assert.Equal(0, ours.shieldsStored);
		Assert.Equal(0, theirs.shieldsStored);

		// Clearing someone else's forest doesn't feed their city.
		ours.SetItemBeingProduced(warrior);
		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields(them);
		Assert.Equal(0, ours.shieldsStored);
		Assert.Equal(0, theirs.shieldsStored);
	}

	[Fact]
	public void ToTerraformSkipsTerraformsWithoutAnImprovement() {
		Assert.Contains(gameData.Terraforms, tf => tf.Improvement == null);
		Assert.Null(TerrainImprovement.ToTerraform("no such improvement"));
		Terraform road = gameData.Terraforms.First(tf => tf.Improvement != null);
		Assert.Same(road, TerrainImprovement.ToTerraform(road.Improvement.key.ToUpperInvariant()));
		Assert.Equal(road.Name, ((object)road).ToString());
	}
}

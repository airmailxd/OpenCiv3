using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// Regression tests for AI and pathing fixes.
public sealed class FixAiPathingTests : MapBase {
	private static readonly TerrainType plains = new() { Key = "plains", movementCost = 1 };
	private static readonly TerrainType coast = new() { Key = "coast", movementCost = 1 };
	private static readonly TerrainImprovement testRoad = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);
	private static readonly TerrainImprovement testRailroad = new("railroad", TerrainImprovement.Layer.Roads, movementCost: 0);

	// A wide x tall map of plains, with water wherever isWater says so.
	internal static GameMap MakeMap(int wide, int tall, Func<int, int, bool> isWater = null) {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1));
		GameMap map = new() { numTilesWide = wide, numTilesTall = tall, tiles = new List<Tile>() };
		for (int i = 0; i < wide * tall / 2; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			bool water = isWater != null && isWater(x, y);
			Tile tile = new(ID.None("tile")) {
				XCoordinate = x,
				YCoordinate = y,
				baseTerrainType = water ? coast : plains,
				overlayTerrainType = water ? coast : plains,
			};
			map.tiles.Add(tile);
		}
		map.computeNeighbors();
		map.recomputeContinents();
		return map;
	}

	private static MapUnit MakeUnit(bool land, int movement, Tile location) {
		MapUnit unit = land ? MakeLandUnit(movement) : MakeWaterUnit(movement);
		unit.location = location;
		return unit;
	}

	// A city founded next to a pre-built railroad makes a 0-cost edge, even
	// though no two railroads are adjacent and nobody can build railroads.
	// The heuristic floor must account for that.
	[Fact]
	public void HeuristicFloorCoversRoadlessCityNextToRailroad() {
		GameMap map = MakeMap(20, 20);
		Tile rail = map.tileAt(10, 10);
		rail.overlays.Add(testRailroad);
		foreach (Tile t in map.tiles.Where(t => t.DistanceTo(rail) >= 3 && t.YCoordinate % 4 == 0)) {
			t.overlays.Add(testRoad);
		}

		Tile start = map.tileAt(2, 2);
		MapUnit unit = MakeUnit(true, 1, start);
		// Scan the map before the city exists.
		float before = MovementCostFloor.MinimumTileCost(unit, start);

		Tile citySite = rail.neighbors[TileDirection.NORTH];
		City city = new(citySite, unit.owner, "City", ID.None("city"));
		citySite.cityAtTile = city;
		unit.owner.cities.Add(city);

		float edge = TilePath.GetMovementCost(unit.owner, citySite, TileDirection.SOUTH, rail);
		Assert.Equal(0f, edge);
		Assert.True(before <= edge, $"floor {before} is above the cost {edge} of a city -> railroad step");
		Assert.True(MovementCostFloor.MinimumTileCost(unit, start) <= edge);
	}

	// Roads alone still give a useful (non-zero) heuristic, even with road-less
	// cities on the map.
	[Fact]
	public void HeuristicFloorIgnoresRoadlessCities() {
		GameMap map = MakeMap(20, 20);
		foreach (Tile t in map.tiles.Where(t => t.YCoordinate % 2 == 0)) {
			t.overlays.Add(testRoad);
		}
		Tile start = map.tileAt(3, 3);
		MapUnit unit = MakeUnit(true, 1, start);
		Tile citySite = map.tileAt(11, 11);
		City city = new(citySite, unit.owner, "City", ID.None("city"));
		citySite.cityAtTile = city;
		unit.owner.cities.Add(city);
		Assert.Equal(1.0f / 3, MovementCostFloor.MinimumTileCost(unit, start), 5);
	}

	[Fact]
	public void UnitWalkerSkipsTilesOffTheMap() {
		GameMap map = MakeMap(10, 10);
		Tile corner = map.tiles.First(t => t.neighbors.Values.Any(n => n == Tile.NONE));
		MapUnit unit = MakeUnit(true, 1, corner);
		unit.owner.isHuman = true;
		Assert.DoesNotContain(new UnitWalker(unit).getEdges(corner), e => e.current == Tile.NONE);
	}
}

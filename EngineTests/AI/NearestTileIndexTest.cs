using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.AI;
using C7GameData;
using Xunit;

namespace EngineTests.AI;

// NearestTileIndex must give exactly the smallest DistanceTo, which the AI
// scores with, on maps that wrap and maps that don't.
public sealed class NearestTileIndexTest {
	private static GameMap MakeMap(int width, int height, bool wrapX, bool wrapY) {
		GameMap map = new() { numTilesWide = width, numTilesTall = height, wrapHorizontally = wrapX, wrapVertically = wrapY, tiles = new List<Tile>() };
		for (int i = 0; i < width * height / 2; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y });
		}
		map.computeNeighbors();
		return map;
	}

	[Theory]
	[InlineData(40, 30, false, false, 1)]
	[InlineData(40, 30, true, false, 2)]
	[InlineData(42, 34, true, true, 3)]
	[InlineData(50, 22, false, true, 4)]
	[InlineData(18, 14, true, true, 5)]
	public void MatchesMeasuringToEachTile(int width, int height, bool wrapX, bool wrapY, int seed) {
		GameMap map = MakeMap(width, height, wrapX, wrapY);
		Random rng = new(seed);
		foreach (int count in new[] { 0, 1, 5, 12, 13, 40, 200 }) {
			List<Tile> sources = Enumerable.Range(0, count).Select(_ => map.tiles[rng.Next(map.tiles.Count)]).ToList();
			NearestTileIndex index = new(sources);
			foreach (Tile t in map.tiles) {
				int expected = int.MaxValue;
				foreach (Tile s in sources) {
					expected = Math.Min(expected, t.DistanceTo(s));
				}
				Assert.Equal(expected, index.DistanceToNearest(t));
			}
		}
	}
}

using System;
using System.Collections.Generic;
using C7GameData;

namespace C7Engine.AI {
	// Answers "how far is this tile from the nearest of these tiles?", with
	// the same answer as taking the smallest Tile.DistanceTo to each of them,
	// without measuring to each. The tiles are put into square cells of the
	// map, and a query looks at the cells in rings around the tile's own,
	// stopping once no further cell can hold anything nearer.
	internal sealed class NearestTileIndex {
		// Below this many tiles, measuring to each is as quick.
		private const int MinTilesForCells = 12;

		private readonly List<Tile> tiles = new();
		private readonly GameMap map;
		private readonly int cellSize;
		private readonly int columns, rows;
		private readonly List<Tile>[] cells;

		public NearestTileIndex(IEnumerable<Tile> source) {
			foreach (Tile t in source) {
				// DistanceTo a NONE tile is int.MaxValue, so it can never be
				// the nearest unless there's nothing else.
				if (t != null && t != Tile.NONE) {
					tiles.Add(t);
				}
			}
			if (tiles.Count < MinTilesForCells) {
				return;
			}
			GameMap m = tiles[0].map;
			if (m == null || m.numTilesWide <= 0 || m.numTilesTall <= 0) {
				return;
			}
			foreach (Tile t in tiles) {
				if (t.map != m || !InBounds(m, t)) {
					return;
				}
			}

			// Along an axis that wraps, the cells must tile it exactly, so
			// that every cell between two others is a whole cell wide.
			int size = 8;
			while (size > 1 && ((m.wrapHorizontally && m.numTilesWide % size != 0) || (m.wrapVertically && m.numTilesTall % size != 0))) {
				--size;
			}
			map = m;
			cellSize = size;
			columns = (m.numTilesWide + size - 1) / size;
			rows = (m.numTilesTall + size - 1) / size;
			cells = new List<Tile>[columns * rows];
			foreach (Tile t in tiles) {
				int i = (t.YCoordinate / size) * columns + t.XCoordinate / size;
				(cells[i] ??= new()).Add(t);
			}
		}

		private static bool InBounds(GameMap m, Tile t) {
			return t.XCoordinate >= 0 && t.XCoordinate < m.numTilesWide && t.YCoordinate >= 0 && t.YCoordinate < m.numTilesTall;
		}

		// The smallest DistanceTo from `t` to one of the tiles, or
		// int.MaxValue if there are none.
		public int DistanceToNearest(Tile t) {
			if (cells == null || t == null || t == Tile.NONE || t.map != map || !InBounds(map, t)) {
				int result = int.MaxValue;
				foreach (Tile other in tiles) {
					result = Math.Min(result, t.DistanceTo(other));
				}
				return result;
			}

			int column = t.XCoordinate / cellSize;
			int row = t.YCoordinate / cellSize;
			int maxRing = Math.Max(map.wrapHorizontally ? columns / 2 : columns - 1, map.wrapVertically ? rows / 2 : rows - 1);
			int best = int.MaxValue;
			for (int ring = 0; ring <= maxRing; ++ring) {
				// A cell `ring` cells away along an axis is at least
				// (ring - 1) whole cells and one tile away along it, so
				// DistanceTo, half the sum of the distances along the two
				// axes, can't be less than this for anything in it.
				if (ring > 0 && ((ring - 1) * cellSize + 1) / 2 >= best) {
					break;
				}
				for (int dy = -ring; dy <= ring; ++dy) {
					int y = row + dy;
					if (map.wrapVertically) {
						y = ((y % rows) + rows) % rows;
					} else if (y < 0 || y >= rows) {
						continue;
					}
					// Only the ring's edge: whole rows at the top and bottom,
					// the two ends elsewhere.
					int step = (dy == -ring || dy == ring) ? 1 : Math.Max(1, 2 * ring);
					for (int dx = -ring; dx <= ring; dx += step) {
						int x = column + dx;
						if (map.wrapHorizontally) {
							x = ((x % columns) + columns) % columns;
						} else if (x < 0 || x >= columns) {
							continue;
						}
						List<Tile> cell = cells[y * columns + x];
						if (cell == null) {
							continue;
						}
						foreach (Tile other in cell) {
							int distance = t.DistanceTo(other);
							if (distance < best) {
								best = distance;
							}
						}
					}
				}
			}
			return best;
		}
	}
}

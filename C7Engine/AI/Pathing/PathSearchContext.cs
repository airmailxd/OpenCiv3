using System;
using System.Collections.Generic;
using C7GameData;

namespace C7Engine.Pathing {
	// Scratch storage for one path search, reused between searches.
	//
	// Every tile touched by a search gets a dense node id: its index in
	// GameMap.tiles when the tile belongs to the map, or an id handed out on
	// demand otherwise (unit tests build maps whose tiles aren't in the list).
	// Per-node data lives in arrays indexed by that id, and a generation stamp
	// marks which entries belong to the current search, so starting a new
	// search doesn't require clearing anything.
	internal sealed class PathSearchContext {
		public const byte CLOSED = 1;
		public const byte PASSABILITY_KNOWN = 2;
		public const byte PASSABLE = 4;
		public const byte FORCEFUL_KNOWN = 8;
		public const byte FORCEFUL = 16;

		[ThreadStatic] private static PathSearchContext pooled;

		private GameMap map;
		private List<Tile> mapTiles;
		private int mapCount;
		private readonly Dictionary<Tile, int> extraIds = new(ReferenceEqualityComparer.Instance);

		private int generation;
		private int[] stamp = Array.Empty<int>();
		public Tile[] tiles = Array.Empty<Tile>();
		public double[] cost = Array.Empty<double>();
		public int[] parent = Array.Empty<int>();
		public byte[] flags = Array.Empty<byte>();

		public readonly PriorityQueue<Entry, double> open = new();

		public readonly struct Entry {
			public readonly int node;
			public readonly double cost;

			public Entry(int node, double cost) {
				this.node = node;
				this.cost = cost;
			}
		}

		// Takes the pooled context for this thread, or a new one if it is
		// already in use (e.g. a re-entrant search).
		public static PathSearchContext Rent(GameMap map) {
			PathSearchContext ctx = pooled ?? new PathSearchContext();
			pooled = null;
			ctx.Begin(map);
			return ctx;
		}

		public static void Return(PathSearchContext ctx) {
			ctx.open.Clear();
			ctx.extraIds.Clear();
			ctx.map = null;
			ctx.mapTiles = null;
			pooled = ctx;
		}

		private void Begin(GameMap m) {
			map = m;
			mapTiles = m?.tiles;
			mapCount = mapTiles?.Count ?? 0;
			extraIds.Clear();
			open.Clear();
			if (generation == int.MaxValue) {
				Array.Clear(stamp);
				generation = 0;
			}
			++generation;
			EnsureCapacity(mapCount + 16);
		}

		private void EnsureCapacity(int size) {
			if (stamp.Length >= size) {
				return;
			}
			int newSize = Math.Max(size, stamp.Length * 2);
			Array.Resize(ref stamp, newSize);
			Array.Resize(ref tiles, newSize);
			Array.Resize(ref cost, newSize);
			Array.Resize(ref parent, newSize);
			Array.Resize(ref flags, newSize);
		}

		// Returns the node id for a tile, initializing its data if this is the
		// first time the current search sees it.
		public int Id(Tile t) {
			int id;
			if (mapCount > 0 && ReferenceEquals(t.map, map)) {
				id = map.tileCoordsToIndex(t.XCoordinate, t.YCoordinate);
				if ((uint)id < (uint)mapCount && ReferenceEquals(mapTiles[id], t)) {
					return Touch(id, t);
				}
			}
			if (!extraIds.TryGetValue(t, out id)) {
				id = mapCount + extraIds.Count;
				extraIds.Add(t, id);
				EnsureCapacity(id + 1);
			}
			return Touch(id, t);
		}

		// Returns the node id for a tile only if the current search has seen
		// it, or -1 otherwise.
		public int PeekId(Tile t) {
			int id;
			if (mapCount > 0 && ReferenceEquals(t.map, map)) {
				id = map.tileCoordsToIndex(t.XCoordinate, t.YCoordinate);
				if ((uint)id < (uint)mapCount && ReferenceEquals(mapTiles[id], t)) {
					return stamp[id] == generation ? id : -1;
				}
			}
			if (extraIds.TryGetValue(t, out id) && stamp[id] == generation) {
				return id;
			}
			return -1;
		}

		private int Touch(int id, Tile t) {
			if (stamp[id] != generation) {
				stamp[id] = generation;
				tiles[id] = t;
				cost[id] = double.PositiveInfinity;
				parent[id] = -1;
				flags[id] = 0;
			}
			return id;
		}

		// Builds the path ending at `node`, excluding the start tile.
		public TilePath MakePath(int node, Tile destination) {
			List<Tile> reversed = new();
			for (int n = node; parent[n] != -1; n = parent[n]) {
				reversed.Add(tiles[n]);
			}
			Queue<Tile> path = new(reversed.Count);
			for (int i = reversed.Count - 1; i >= 0; --i) {
				path.Enqueue(reversed[i]);
			}
			return new TilePath(destination, path);
		}
	}
}

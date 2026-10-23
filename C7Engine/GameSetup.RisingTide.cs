using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine;

// Rising Tide, a custom scenario (owner's design, 2026-10-23; see
// RisingTide): on a pangaea that the sea eats away a ring at a time, every
// civ starts in the Ancient Times near the edge of the land, with a
// settler, three workers and two archers (and a scout, if expansionist).
// Settlers found cities of size 5, so a civ can move its people inland
// ahead of the tide.
public partial class GameSetup {
	// The citizens a settler founds a city with.
	public const int RisingTideCitySize = 5;

	// How many tiles from the sea each civ starts: near the edge, but out of
	// reach of the first waves (the capital falls to the RisingTideStartDepth-th
	// wave unless the land around it is higher).
	public const int RisingTideStartDepth = 3;

	private const string RisingTideWorker = "Worker";
	private const string RisingTideArcher = "Archer";
	private const int RisingTideWorkers = 3;
	private const int RisingTideArchers = 2;

	private static void SetUpRisingTideWorld(WorldCharacteristics world) {
		world.landform = WorldCharacteristics.Landform.Pangaea;
		world.singleLandmass = true;
	}

	private static void SetUpRisingTideRules(SaveGame save) {
		save.Rules.RisingTide = true;
		save.Rules.NewCitySize = RisingTideCitySize;
		save.RisingTide = new RisingTideState { nextWaveTurn = RisingTide.Interval(0) };
		// The last civ standing wins.
		save.VictoryConditions ??= new VictoryConditions();
		save.VictoryConditions.AllowConquestVictory = true;
	}

	// The starting units of a Rising Tide civ, on its start.
	private void AddRisingTideUnits(SaveGame save, SavePlayer player, TileLocation location) {
		AddUnit(save, player, save.Rules.StartUnitType1, location);
		for (int i = 0; i < RisingTideWorkers; i++) {
			AddUnit(save, player, RisingTideWorker, location);
		}
		for (int i = 0; i < RisingTideArchers; i++) {
			AddUnit(save, player, RisingTideArcher, location);
		}
	}

	// A start for each civ near the edge of the land: on land a city may
	// stand on, RisingTideStartDepth tiles from the sea (or as near to that
	// as the land allows), spread as far from each other as can be: each
	// next start the one farthest from those already chosen.
	private static List<SaveTile> RisingTideStarts(SaveGame save, Random rand, int count) {
		SaveMap map = save.Map;
		Dictionary<(int, int), SaveTile> at = map.tiles.ToDictionary(t => (t.X, t.Y));
		(int dx, int dy)[] around = [(1, -1), (1, 1), (-1, 1), (-1, -1), (2, 0), (-2, 0), (0, 2), (0, -2)];
		IEnumerable<SaveTile> Neighbors(SaveTile t) {
			foreach ((int dx, int dy) in around) {
				int x = t.X + dx;
				if (map.wrapHorizontally) {
					x = (x % map.tilesWide + map.tilesWide) % map.tilesWide;
				}
				if (at.TryGetValue((x, t.Y + dy), out SaveTile n)) {
					yield return n;
				}
			}
		}
		bool IsSalt(SaveTile t) => !t.isFreshWater && t.baseTerrain is "coast" or "sea" or "ocean";
		bool IsLand(SaveTile t) => t.baseTerrain is not ("coast" or "sea" or "ocean");

		// Each land tile's distance from the sea.
		Dictionary<SaveTile, int> depth = new();
		Queue<SaveTile> queue = new();
		foreach (SaveTile t in map.tiles.Where(IsSalt)) {
			depth[t] = 0;
			queue.Enqueue(t);
		}
		while (queue.Count > 0) {
			SaveTile t = queue.Dequeue();
			foreach (SaveTile n in Neighbors(t).Where(n => IsLand(n) && !depth.ContainsKey(n))) {
				depth[n] = depth[t] + 1;
				queue.Enqueue(n);
			}
		}

		List<SaveTile> sites = map.tiles
			.Where(t => IsLand(t) && depth.ContainsKey(t) && t.overlayTerrain is not ("mountains" or "volcano") && !t.isFreshWater)
			.ToList();
		if (sites.Count < count) {
			throw new InvalidOperationException($"The land has room for {sites.Count} starts, too few for {count} civilizations");
		}
		int deepest = sites.Max(t => depth[t]);
		int want = Math.Min(RisingTideStartDepth, deepest);
		List<SaveTile> edge = sites.Where(t => depth[t] == want).ToList();
		if (edge.Count < count) {
			edge = sites.OrderBy(t => Math.Abs(depth[t] - want)).Take(Math.Max(count, edge.Count)).ToList();
		}

		int Distance(SaveTile a, SaveTile b) {
			int dx = Math.Abs(a.X - b.X);
			if (map.wrapHorizontally) {
				dx = Math.Min(dx, map.tilesWide - dx);
			}
			return Math.Max(dx, Math.Abs(a.Y - b.Y));
		}
		List<SaveTile> starts = [edge[rand.Next(edge.Count)]];
		while (starts.Count < count) {
			starts.Add(edge.Where(t => !starts.Contains(t)).MaxBy(t => starts.Min(s => Distance(s, t))));
		}
		return starts;
	}
}

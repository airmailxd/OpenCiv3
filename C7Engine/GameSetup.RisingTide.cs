using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine;

// Rising Tide, a custom scenario (owner's design, 2026-10-23; see
// RisingTide): on a pangaea that the sea eats away a ring at a time, every
// civ starts in the Ancient Times with a city of size 5 on the shore, three
// workers and two archers (and a scout, if expansionist) (owner's change,
// 2026-10-24, from a settler three tiles inland). The first wave takes the
// start city. Settlers found cities of size 5, so a civ can move its people
// inland ahead of the tide, and a city's center feeds two citizens.
public partial class GameSetup {
	// The citizens a settler founds a city with.
	public const int RisingTideCitySize = 5;

	// How many tiles from the sea each civ starts: on the shore (owner's
	// ruling, 2026-10-24), so the capital falls to the first wave.
	public const int RisingTideStartDepth = 1;

	// The food a city's center makes: enough for two citizens (owner's
	// ruling, 2026-10-24; lowered from 6, 2026-10-25).
	public const int RisingTideCityCenterFood = 4;

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
		save.Rules.CityCenterFood = RisingTideCityCenterFood;
		save.RisingTide = new RisingTideState { nextWaveTurn = RisingTide.Interval(0) };
		// The last civ standing wins.
		save.VictoryConditions ??= new VictoryConditions();
		save.VictoryConditions.AllowConquestVictory = true;
	}

	// A Rising Tide civ's capital, on its start, and its starting units.
	private void AddRisingTideStart(SaveGame save, SavePlayer player, Civilization civ, TileLocation location) {
		SaveCity city = new() {
			id = ids.CreateID("city"),
			owner = player.id,
			location = location,
			capital = true,
			name = civ.cityNames.Count > 0 ? civ.cityNames[0] : civ.name,
			size = save.Rules.NewCitySize,
			producible = RisingTideUnit(save, civ, RisingTideArcher),
			producibleType = ProducibleType.UNIT,
		};
		city.perPlayerCulture.Add(player.id.ToString(), 0);
		if (save.Buildings.Find(b => b.flags.Contains(SaveBuilding.Flag.IsCenterOfEmpire)) is SaveBuilding palace) {
			city.buildings.Add(new SaveCityBuilding { building = palace.name, builtByPlayer = player.id });
		}
		save.Cities.Add(city);
		player.citiesFounded = 1;
		string worker = RisingTideUnit(save, civ, RisingTideWorker);
		for (int i = 0; i < RisingTideWorkers; i++) {
			AddUnit(save, player, worker, location);
		}
		string archer = RisingTideUnit(save, civ, RisingTideArcher);
		for (int i = 0; i < RisingTideArchers; i++) {
			AddUnit(save, player, archer, location);
		}
	}

	// The unit by the name, or, in rules without it (like MDJ's, see
	// GameSetup.Mdj.cs), the cheapest the civ can build that does the
	// same: a worker, or a land unit that can both attack and defend.
	private static string RisingTideUnit(SaveGame save, Civilization civ, string name) {
		if (save.UnitPrototypes.Any(p => p.name == name)) {
			return name;
		}
		bool worker = name == RisingTideWorker;
		return save.UnitPrototypes
			.Where(p => !p.unproducible && p.categories.Contains("Land") && (p.producibleBy.Count == 0 || p.producibleBy.Contains(civ.name))
				&& !p.actions.Contains(UnitAction.BuildCity)
				&& (worker ? p.terraformActions.Count > 0 : p.attack > 0 && p.defense > 0 && p.terraformActions.Count == 0))
			.OrderBy(p => p.shieldCost).ThenByDescending(p => p.attack + p.defense)
			.Select(p => p.name).FirstOrDefault()
			?? throw new InvalidOperationException($"The rules have no {name} for {civ.name}");
	}

	// A start for each civ at the edge of the land: on land a city may
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

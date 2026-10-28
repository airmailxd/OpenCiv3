using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using C7Engine.AI.UnitAI;
using C7Engine.Pathing;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// How the AI plays Rising Tide (see RisingTide; owner's design,
	// 2026-10-23). Each Tidal Wave turns the land next to salt water into
	// Coast, so a land tile d steps from the sea (over its eight neighbours,
	// across land) drowns at the d-th wave from now, and when that is can be
	// worked out from the waves' timetable. The AI founds its cities where
	// they will last, moves its people inland by settlers before the sea
	// takes their cities, stops building what a city won't keep, and walks
	// its units out of the way of the next wave. Outside Rising Tide nothing
	// here changes anything: every tile lasts for ever.
	public static class TideAI {
		private static readonly ILogger log = Log.ForContext(typeof(TideAI));

		// A tile the sea never reaches (water, or land walled off from the
		// sea by lakes), or any tile without the tide.
		public const int Never = int.MaxValue;

		public static bool IsOn() {
			return EngineStorage.gameData is GameData gameData && RisingTide.IsOn(gameData);
		}

		// Each land tile's distance from salt water, worked out again after
		// every wave (nothing else changes it).
		private sealed class Depths {
			public int waves = -1;
			public GameMap map;
			public readonly Dictionary<Tile, int> depth = new();
			public int deepest;
		}

		private static readonly ConditionalWeakTable<GameData, Depths> depthsByGame = new();

		private static Depths GetDepths(GameData gameData) {
			Depths d = depthsByGame.GetValue(gameData, _ => new Depths());
			if (d.waves == gameData.risingTide.waves && d.map == gameData.map) {
				return d;
			}
			d.waves = gameData.risingTide.waves;
			d.map = gameData.map;
			d.depth.Clear();
			d.deepest = 0;
			Queue<Tile> queue = new();
			foreach (Tile t in gameData.map.tiles) {
				if (t.IsWater() && !t.isFreshWater) {
					queue.Enqueue(t);
				}
			}
			while (queue.Count > 0) {
				Tile t = queue.Dequeue();
				int next = (t.IsLand() ? d.depth[t] : 0) + 1;
				foreach (Tile n in t.neighbors.Values) {
					if (n == null || n == Tile.NONE || !n.IsLand() || d.depth.ContainsKey(n)) {
						continue;
					}
					d.depth[n] = next;
					d.deepest = Math.Max(d.deepest, next);
					queue.Enqueue(n);
				}
			}
			return d;
		}

		// The wave, counting from the next (1), that drowns the tile, or
		// Never.
		public static int WavesUntilDrowned(Tile t) {
			if (t == null || t == Tile.NONE || !t.IsLand() || EngineStorage.gameData is not GameData gameData || !RisingTide.IsOn(gameData)) {
				return Never;
			}
			return GetDepths(gameData).depth.TryGetValue(t, out int d) ? d : Never;
		}

		// How many waves the deepest land lasts: the most WavesUntilDrowned
		// of any tile the sea will reach (0 with no land left, or no tide).
		public static int DeepestLand() {
			if (EngineStorage.gameData is not GameData gameData || !RisingTide.IsOn(gameData)) {
				return 0;
			}
			return GetDepths(gameData).deepest;
		}

		// The turns from now until the n-th wave from now (1 the next)
		// strikes, or Never. A unit on a tile this many turns from drowning
		// has this many turns, this one included, to get off it: the wave
		// strikes as the round ends, before anyone moves again.
		public static int TurnsUntilWave(int n) {
			if (n == Never || EngineStorage.gameData is not GameData gameData || !RisingTide.IsOn(gameData)) {
				return Never;
			}
			RisingTideState state = gameData.risingTide;
			int turns = state.nextWaveTurn < 0 ? RisingTide.Interval(0) - gameData.turn : RisingTide.TurnsUntilNextWave(gameData);
			for (int k = 1; k < n; ++k) {
				turns += RisingTide.Interval(state.waves + k);
			}
			return Math.Max(1, turns);
		}

		// The turns until the tile drowns (see TurnsUntilWave), or Never.
		public static int TurnsUntilDrowned(Tile t) {
			return TurnsUntilWave(WavesUntilDrowned(t));
		}

		////////////////////////////////////////////////////////////////////
		// City sites

		// HEURISTIC: a city is only founded where it will last this many
		// waves or more (or on the deepest land left, once none lasts that
		// long): the next couple of waves come too soon for a city to pay
		// back the settler.
		public const int MinSiteWaves = 3;

		// A settler on its way keeps its site while the site lasts this many
		// waves, one fewer than to choose it, so that a wave striking while
		// it walks doesn't send it elsewhere at once.
		public const int MinKeptSiteWaves = MinSiteWaves - 1;

		// Whether a city may be founded on the tile, as far as the tide is
		// concerned: it lasts at least `waves` waves, or as long as the
		// deepest land does.
		public static bool LastsLongEnough(Tile t, int waves) {
			int w = WavesUntilDrowned(t);
			return w == Never || w >= Math.Min(waves, DeepestLand());
		}

		// HEURISTIC: what the tide does to a site's score: the share of the
		// longest life any site could have, in waves. A city's worth is its
		// yield for as long as it stands, but under the tide the waves
		// matter more than the turns: the waves come ever faster, so
		// lasting a wave longer counts for more as the game goes on. The
		// civ works its way inland a ring or two at a time, its cities on
		// the edge making units while they stand, rather than making for
		// the middle at once (owner's suggestion, 2026-10-27). With the
		// share squared, in AI trials (32 paired games of 8 civs on a
		// 100x100 map), civs fell 5.5 turns sooner (+- 2.7).
		public static float SiteSurvivalFactor(Tile t) {
			int w = WavesUntilDrowned(t);
			int deepest = DeepestLand();
			if (w == Never || deepest <= 0) {
				return 1;
			}
			float share = Math.Min(w, deepest) / (float)deepest;
			return share;
		}

		// HEURISTIC: for a settler choosing where to go, the share of the
		// site's life left once it gets there, at about a tile a turn: a
		// long walk eats into the turns the city stands.
		public static float TravelSurvivalFactor(Tile site, int distance) {
			int life = TurnsUntilDrowned(site);
			if (life == Never) {
				return 1;
			}
			return Math.Max(0.05f, (life - distance) / (float)life);
		}

		// HEURISTIC: under the tide the AI founds its cities
		// as close as Civ3 allows, with only a tile between them, rather
		// than keeping two tiles between them as it otherwise does (see
		// SettlerLocationAI.IsInvalidCityLocation). A city stands only some
		// dozens of turns, too few to grow into a crowded fat cross, and
		// every city a settler founds is five citizens: packed close, more
		// of them fit on the deep land the sea takes last.
		public static bool IsNextToCity(Tile t) {
			if (t == null || t == Tile.NONE || t.HasCity()) {
				return true;
			}
			foreach (Tile n in t.neighbors.Values) {
				if (n != null && n != Tile.NONE && n.HasCity()) {
					return true;
				}
			}
			return false;
		}

		// HEURISTIC: the first city is founded no further than this from
		// where the settler starts: a few tiles deeper buys it waves, but
		// every turn without a city is a turn without production.
		public const int FirstCityMaxWalk = 3;

		////////////////////////////////////////////////////////////////////
		// Production

		// HEURISTIC: a city that will be gone within this many turns is a
		// relocation city: it wants settlers most of all, to found cities
		// further inland. (It has stopped building improvements and
		// wonders by then too, see MinImprovementUseTurns.)
		public const int DoomedCityTurns = 15;

		// HEURISTIC: an improvement must stand at least this many turns
		// once built to be worth building.
		public const int MinImprovementUseTurns = 15;

		// HEURISTIC: what a settler adds to its score under the tide, and in
		// a city the sea will soon take. Each settler founds a city of five
		// (RisingTideCitySize) for two citizens, so settlers grow the civ
		// faster than anything else could, and in a doomed city they are
		// the only way to save its people.
		public const float SettlerBonus = 30;
		public const float DoomedSettlerBonus = 100;

		// The turns the city has left, or Never.
		public static int CityTurnsLeft(City city) {
			return TurnsUntilDrowned(city.location);
		}

		// Whether the city can't finish the item in time to get any use
		// from it: an improvement or wonder must stand for a while after it
		// is done, and a unit must be done the turn before the wave at the
		// latest, to walk out.
		public static bool TooLateToBuild(City city, IProducible item) {
			int left = CityTurnsLeft(city);
			if (left == Never) {
				return false;
			}
			// (A city making no shields would never finish it.)
			long turns = city.TurnsToProduce(item);
			if (item is Building) {
				return turns + MinImprovementUseTurns > left;
			}
			return turns >= left;
		}

		// A city building what it can't finish in time to be of use chooses
		// again, and while the civ has no refuge, no city that outlasts the
		// next wave, every city builds settlers first. Before this, in AI
		// trials, civs still had only their start city on the shore days
		// before the first wave, building archers, and doomed cities built
		// wonders when nothing else was of use either; with it (32 paired
		// games of 8 civs on a 100x100 map) civs reached five cities 29 turns
		// sooner and fell 8.4 turns later (+- 2.8).

		// Whether the civ has a city that outlasts the next wave.
		public static bool HasRefuge(Player player) {
			return player.cities.Any(c => WavesUntilDrowned(c.location) > 1);
		}

		// Before production: a building the city can't finish in time to
		// use, or anything but a settler in a civ with no refuge, gives way
		// to what the city would choose now (for the latter, only a settler,
		// and only if no shields are lost).
		public static void ReconsiderProduction(Player player) {
			if (!IsOn() || player.isHuman || player.IsIndependent) {
				return;
			}
			bool refuge = HasRefuge(player);
			foreach (City city in player.cities.ToList()) {
				IProducible item = city.itemBeingProduced;
				if (city.hurriedThisTurn || item == null) {
					continue;
				}
				bool tooLate = item is Building && TooLateToBuild(city, item);
				bool wantsSettler = !refuge && !(item is UnitPrototype u && u.isSettler);
				if (!tooLate && !wantsSettler) {
					continue;
				}
				IProducible choice = ChooseProducible.Choose(city, player);
				if (choice == null || choice == item || !city.CanChangeProduction(choice)) {
					continue;
				}
				if (!tooLate && (choice is not UnitPrototype { isSettler: true } || city.ShieldsLostByChangingTo(choice) > 0)) {
					continue;
				}
				log.Information("{Player} switches {City} from {Item} to {Choice} ahead of the tide", player, city, item, choice);
				city.ChangeProduction(choice);
			}
		}

		// A city that won't finish the unit it's building before the sea
		// takes it hurries the unit, with gold or with citizens, who would
		// drown anyway. (Hurrying with citizens is otherwise left alone, see
		// DevelopmentAI.HurryProduction.)
		public static void HurryEvacuation(Player player) {
			if (!IsOn() || player.isHuman || player.IsIndependent) {
				return;
			}
			foreach (City city in player.cities.ToList()) {
				if (city.hurriedThisTurn || city.itemBeingProduced is not UnitPrototype unit || !unit.IsLandUnit()) {
					continue;
				}
				int left = CityTurnsLeft(city);
				// The unit appears next turn, and must walk out before the
				// wave; with no time for that, hurrying saves nothing.
				if (left == Never || left < 2 || city.TurnsUntilProductionFinished() < left) {
					continue;
				}
				if (unit.populationCost >= city.residents.Count) {
					continue;
				}
				City.HurryProductionDetails details = city.GetHurryProductionDetails();
				if (details.errorMessage != null) {
					continue;
				}
				if (details.popCost > 0 && city.residents.Count - details.popCost <= unit.populationCost) {
					continue;
				}
				log.Information("{Player} hurries {Item} in {City} before the tide takes it", player, unit, city);
				city.HurryProduction();
				AiExperiments.onHurry?.Invoke(city, details.goldCost);
			}
		}

		////////////////////////////////////////////////////////////////////
		// Units

		// HEURISTIC: a land unit leaves a tile this many turns before it
		// drowns, or more where no neighbour is any deeper, so it gets out
		// with a turn to spare.
		public const int FleeLeadTurns = 2;
		private const int FleeLeadTurnsFromPocket = 4;

		// HEURISTIC: a tile drowning within this many turns is no place to
		// send a unit to, or to improve.
		public const int DoomedTileTurns = 4;
		public const int MinImprovedTileTurns = 10;

		// An improvement is only of use while a city of ours can work the
		// tile, so a tile around a city the sea will soon take, like the
		// start city, isn't worth improving even if the tile outlasts it
		// (owner's suggestion, 2026-10-27). HEURISTIC: it must be of use for
		// this many turns. (In AI trials it made no clear difference to
		// when civs fell, +1.3 turns +- 2.0, and kept workers at home more.)
		public const int MinImprovementUseTurnsForWorkers = 15;

		// The turns an improvement on each tile our cities could work would
		// be of use: until the tile drowns, or the last of those cities
		// does, whichever is sooner.
		public static Dictionary<Tile, int> TileUseTurns(Player player) {
			Dictionary<Tile, int> result = new();
			foreach (City c in player.cities) {
				int life = CityTurnsLeft(c);
				foreach (Tile t in c.GetWorkableTiles()) {
					int use = Math.Min(life, TurnsUntilDrowned(t));
					if (!result.TryGetValue(t, out int u) || use > u) {
						result[t] = use;
					}
				}
			}
			return result;
		}

		// Half the workers build roads leading inland, rather than a
		// quarter of them joining the cities to a capital the sea will soon
		// take: from each city to the nearest of ours that lasts longer, and
		// to the sites our settlers are bound for, so units and settlers can
		// move inland ahead of the tide (owner's suggestion, 2026-10-27).
		// Roads go on any tile that lasts more than DoomedTileTurns. (In AI
		// trials it made no clear difference to when civs fell, +0.8 turns
		// +- 1.2.)

		public static List<(Tile from, Tile to)> InlandRoads(Player player) {
			List<(Tile, Tile)> roads = new();
			foreach (City c in player.cities) {
				int waves = WavesUntilDrowned(c.location);
				City deeper = player.cities
					.Where(o => o != c && o.location.continent == c.location.continent && WavesUntilDrowned(o.location) > waves)
					.MinBy(o => o.location.DistanceTo(c.location));
				if (deeper != null) {
					roads.Add((c.location, deeper.location));
				}
			}
			foreach (MapUnit unit in player.units) {
				if (unit.currentAI is not SettlerAI settler || settler.data.goal != C7GameData.AIData.SettlerAIData.SettlerGoal.BUILD_CITY
						|| settler.data.destination is not Tile site || site == Tile.NONE || site.HasCity()) {
					continue;
				}
				City from = player.cities.Where(c => c.location.continent == site.continent).MinBy(c => c.location.DistanceTo(site));
				if (from != null) {
					roads.Add((from.location, site));
				}
			}
			return roads;
		}

		// Whether the tile drowns within `turns` turns.
		public static bool DrownsWithin(Tile t, int turns) {
			int left = TurnsUntilDrowned(t);
			return left != Never && left <= turns;
		}

		// Whether stepping onto the tile would leave the unit where the next
		// wave strikes, from land that lasts longer.
		public static bool StepWouldDrown(MapUnit unit, Tile next) {
			if (!IsOn() || !unit.IsLandUnit() || next == null || !next.IsLand()) {
				return false;
			}
			int waves = WavesUntilDrowned(next);
			return waves < WavesUntilDrowned(unit.location) && TurnsUntilWave(waves) <= 1;
		}

		// Whether the unit must make for higher ground now.
		public static bool MustFlee(MapUnit unit) {
			if (!IsOn() || !unit.IsLandUnit() || unit.IsLoaded() || unit.hitPointsRemaining <= 0) {
				return false;
			}
			int waves = WavesUntilDrowned(unit.location);
			if (waves == Never) {
				return false;
			}
			int left = TurnsUntilWave(waves);
			if (left <= FleeLeadTurns) {
				return true;
			}
			if (left > FleeLeadTurnsFromPocket) {
				return false;
			}
			foreach (Tile n in unit.location.neighbors.Values) {
				if (n != null && n != Tile.NONE && n.IsLand() && WavesUntilDrowned(n) > waves) {
					return false;
				}
			}
			return true;
		}

		// HEURISTIC: how far a fleeing unit looks for higher ground.
		private const int FleeRadius = 5;

		// Walks the unit to the nearest tile that lasts longer than its own
		// and that it can reach before the wave (about a tile a turn per
		// move), the deeper the better among equally near ones, and
		// towards `toward` where it has somewhere to be. Returns whether it
		// moved.
		public static async Task<bool> Flee(MapUnit unit, Player player, Tile toward = null) {
			if (!unit.movementPoints.canMove) {
				return false;
			}
			Tile start = unit.location;
			int waves = WavesUntilDrowned(start);
			int reach = Math.Max(1, TurnsUntilWave(waves)) * Math.Max(1, unit.MaxMovementPoints());
			List<(Tile tile, float key)> candidates = new();
			foreach (Tile t in start.GetTilesWithinTileSquare(FleeRadius)) {
				if (t == Tile.NONE || t == start || !t.IsLand() || !player.tileKnowledge.isTileKnown(t)) {
					continue;
				}
				int w = WavesUntilDrowned(t);
				int distance = start.DistanceTo(t);
				if (w <= waves || distance > reach) {
					continue;
				}
				Player owner = t.OwningPlayer();
				float key = distance - 0.5f * Math.Min(w - waves, 2);
				if (toward != null && toward != Tile.NONE) {
					key += 0.25f * t.DistanceTo(toward);
				}
				// Better in one of our cities, and only into another civ's
				// land if there is no other way: one at peace with us may
				// put us out of it (see TerritoryDemands), onto whatever
				// tile outside it is nearest, drowning or not.
				if (t.cityAtTile?.owner == player) {
					key -= 0.5f;
				} else if (owner != null && owner != player) {
					key += 3;
				}
				candidates.Add((t, key));
			}
			if (candidates.Count == 0) {
				return false;
			}
			List<Tile> ordered = candidates.OrderBy(c => c.key).Select(c => c.tile).ToList();
			if (PathingAlgorithmChooser.GetAlgorithm(unit).FindFirstReachable(start, ordered, unit, out TilePath path) < 0 || path == null) {
				return false;
			}
			log.Information("{Unit} makes for {Tile} ahead of the tide", unit, path.destination);
			FleeAI flee = new(path);
			await ((C7GameData.UnitAI)flee).PlayTurn(player, unit);
			return unit.location != start;
		}

		// Walks a unit along its way to higher ground for one turn.
		private sealed class FleeAI(TilePath path) : C7GameData.UnitAI {
			private TilePath path = path;

			public C7GameData.UnitAI.MoveResult PlayTurnImpl(Player player, MapUnit unit) {
				if (path == null || unit.location == path.destination) {
					return C7GameData.UnitAI.Result.Done;
				}
				return this.TryToMoveAlongPath(unit, ref path);
			}

			public string SummarizePlan() {
				return $"FleeAI: to {path?.destination}";
			}

			public void UpdateOnDeath() { }
		}

		// Before a unit plays its turn: if the next wave it can't outrun
		// would drown it, it makes for higher ground first. A settler,
		// escort or soldier on a war campaign keeps its plan (a settler's
		// site lasts by its choosing, the campaign gathers it in again);
		// anything else plans afresh next turn, as its plan may have been
		// for the drowning land. Returns whether the unit's turn is done.
		public static async Task<bool> EvacuateIfNeeded(MapUnit unit, Player player) {
			if (!MustFlee(unit)) {
				return false;
			}
			Tile toward = unit.currentAI is SettlerAI settler && settler.data.goal == C7GameData.AIData.SettlerAIData.SettlerGoal.BUILD_CITY
				? settler.data.destination : null;
			unit.Wake();
			bool moved = await Flee(unit, player, toward);
			if (!moved) {
				return false;
			}
			if (!KeepsPlan(unit)) {
				unit.currentAI = null;
			}
			return true;
		}

		// After a unit played its turn: one left where the next wave strikes
		// moves off with what moves it has left.
		public static async Task EvacuateAfterMoving(MapUnit unit, Player player) {
			if (!IsOn() || !unit.IsLandUnit() || unit.IsLoaded() || unit.hitPointsRemaining <= 0 || !unit.movementPoints.canMove) {
				return;
			}
			if (!DrownsWithin(unit.location, 1)) {
				return;
			}
			if (await Flee(unit, player) && !KeepsPlan(unit)) {
				unit.currentAI = null;
			}
		}

		private static bool KeepsPlan(MapUnit unit) {
			return unit.currentAI is SettlerAI or EscortAI or WarCampaign.MemberAI;
		}
	}
}

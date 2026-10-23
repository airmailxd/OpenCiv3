using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7GameData {
	// The saved state of Rising Tide (see C7Engine.RisingTide).
	public class RisingTideState {
		// The turn the next Tidal Wave strikes.
		public int nextWaveTurn = -1;

		// How many waves have struck.
		public int waves = 0;
	}
}

namespace C7Engine {
	// Rising Tide, a custom scenario (owner's design, 2026-10-23): the
	// ocean creeps in on a pangaea. Every so often a Tidal Wave turns every
	// land tile next to the sea into Coast, so the shore moves a tile
	// inland. A city on such a tile is destroyed, and the land units on it
	// drown. The waves come ever faster: the turns between them run down the
	// Fibonacci numbers from 21 (21, 13, 8, 5), then each twice (5, 3, 3, 2,
	// 2, 1, 1), and then every turn, until no land is left; the last civ
	// standing wins. The waves count from the start of the game.
	public static class RisingTide {
		private static readonly ILogger log = Log.ForContext(typeof(RisingTide));

		// The turns from the start to the first wave, and between each wave
		// and the next; after the last, a wave every turn.
		public static readonly int[] Intervals = [21, 13, 8, 5, 5, 3, 3, 2, 2, 1];

		// The turns between the given wave (0 for the first, counted from
		// the start) and the one before.
		public static int Interval(int wave) {
			return wave < Intervals.Length ? Intervals[wave] : 1;
		}

		public static bool IsOn(GameData gameData) {
			return gameData.rules?.RisingTide == true;
		}

		// The turns until the next wave, or -1 without Rising Tide.
		public static int TurnsUntilNextWave(GameData gameData) {
			if (!IsOn(gameData) || gameData.risingTide.nextWaveTurn < 0) {
				return -1;
			}
			return System.Math.Max(0, gameData.risingTide.nextWaveTurn - gameData.turn);
		}

		// Called as each turn begins: strikes if a wave is due, and sets
		// when the next comes.
		public static void BeginTurn(GameData gameData) {
			if (!IsOn(gameData)) {
				return;
			}
			RisingTideState state = gameData.risingTide;
			if (state.nextWaveTurn < 0) {
				state.nextWaveTurn = Interval(0);
			}
			if (gameData.turn < state.nextWaveTurn) {
				return;
			}
			Strike(gameData);
			state.waves++;
			state.nextWaveTurn = gameData.turn + Interval(state.waves);
		}

		// The land tiles the next wave takes: those next to salt water.
		public static List<Tile> Shore(GameData gameData) {
			return gameData.map.tiles.Where(t => t.IsLand() && t.NeighborsOcean()).ToList();
		}

		// A Tidal Wave: the shore becomes Coast.
		public static void Strike(GameData gameData) {
			TerrainType coast = gameData.terrainTypes.Find(t => t.IsCoast);
			if (coast == null) {
				log.Warning("The rules have no Coast terrain for the tide");
				return;
			}
			List<Tile> shore = Shore(gameData);
			log.Information("A Tidal Wave on turn {Turn} floods {Tiles} tiles", gameData.turn, shore.Count);
			Dictionary<Player, List<string>> citiesLost = new();
			foreach (Tile tile in shore) {
				if (tile.cityAtTile is City city) {
					Player owner = city.owner;
					if (!citiesLost.TryGetValue(owner, out List<string> lost)) {
						citiesLost[owner] = lost = new();
					}
					lost.Add(city.name);
					log.Information("The tide destroys {City} of {Player}", city.name, owner);
					CityInteractions.DestroyCity(city);
				}
				foreach (MapUnit unit in tile.unitsOnTile.Where(u => !u.IsWaterUnit() && !u.IsAirUnit()).ToList()) {
					unit.RemoveFromPlay();
				}
				Flood(tile, coast);
			}
			if (shore.Count > 0) {
				gameData.map.recomputeContinents();
				gameData.UpdateTileOwners();
				gameData.InvalidateCachedTradeNetwork();
			}
			foreach (Player player in gameData.players.Where(p => p.IsToldNews)) {
				string message = "A Tidal Wave strikes! The sea swallows our shores.";
				if (citiesLost.TryGetValue(player, out List<string> lost)) {
					message += $" Lost beneath the waves: {string.Join(", ", lost)}.";
				}
				new MsgShowDomesticAdvisorPopup(player, message).send();
			}
		}

		// Makes the tile Coast, with nothing left of what was on it.
		private static void Flood(Tile tile, TerrainType coast) {
			tile.baseTerrainType = coast;
			tile.overlayTerrainType = coast;
			tile.Resource = Resource.NONE;
			tile.isBonusShield = false;
			tile.isSnowCapped = false;
			tile.isPineForest = false;
			tile.isFreshWater = false;
			tile.riverNorth = tile.riverNortheast = tile.riverEast = tile.riverSoutheast = false;
			tile.riverSouth = tile.riverSouthwest = tile.riverWest = tile.riverNorthwest = false;
			tile.overlays.Clear();
			TileChangeJournal.RecordTerrainChange(tile);
		}
	}
}

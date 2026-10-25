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

		// Lets AiTrialTest count the cities the tide destroys and the units
		// it drowns. Null outside a trial.
		internal static System.Action<City> onCityDrowned;
		internal static System.Action<MapUnit> onUnitDrowned;

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
			SinkShipsAtSea(gameData);
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

		// The sea is no refuge (owner's ruling, 2026-10-23): no ships can be
		// built (see UnitPrototype.IsAvailableTo), and a ship out on the
		// water, outside a city, sinks at once with whatever it carries.
		public static bool SinksAt(MapUnit unit, Tile tile) {
			return IsOn(EngineStorage.gameData) && unit.IsWaterUnit() && Tile.IsTileValid(tile)
				&& tile.baseTerrainType.IsWater && !tile.HasCity();
		}

		public static void Sink(MapUnit unit) {
			Player owner = unit.owner;
			log.Information("The tidal sea sinks {Unit}", unit);
			onUnitDrowned?.Invoke(unit);
			unit.RemoveFromPlay();
			if (owner?.IsToldNews == true) {
				new MsgShowMilitaryAdvisorPopup(owner, $"Our {unit.unitType.name} was lost to the raging sea!", happy: false).send();
			}
		}

		// Sinks every ship at sea, as each turn begins.
		private static void SinkShipsAtSea(GameData gameData) {
			foreach (MapUnit unit in gameData.mapUnits.Where(u => SinksAt(u, u.location)).ToList()) {
				if (gameData.mapUnits.Contains(unit)) {
					Sink(unit);
				}
			}
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
					onCityDrowned?.Invoke(city);
					CityInteractions.DestroyCity(city);
				}
				foreach (MapUnit unit in tile.unitsOnTile.Where(u => !u.IsWaterUnit() && !u.IsAirUnit()).ToList()) {
					onUnitDrowned?.Invoke(unit);
					unit.RemoveFromPlay();
				}
				Flood(tile, coast);
			}
			if (shore.Count > 0) {
				RedrawFlooded(gameData, shore);
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

		// The flooded tiles' art, and that of the tiles south of them, whose
		// corners they share, is chosen again for their new terrain (see
		// TerrainTextureFiles). With a random of its own, as the choice only
		// varies among corners of open sea, which these never are, and the
		// game's random sequence stays as it was.
		private static void RedrawFlooded(GameData gameData, List<Tile> flooded) {
			HashSet<Tile> tiles = new(flooded);
			foreach (Tile t in flooded) {
				foreach (TileDirection d in new[] { TileDirection.SOUTH, TileDirection.SOUTHEAST, TileDirection.SOUTHWEST }) {
					if (t.neighbors.TryGetValue(d, out Tile n) && n != Tile.NONE) {
						tiles.Add(n);
					}
				}
			}
			try {
				TerrainTextureFiles.ReassignTextureDetails(new System.Random(gameData.turn), gameData.terrainTypes, tiles);
			} catch (System.Exception e) {
				log.Warning(e, "Could not choose the art of the flooded tiles");
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

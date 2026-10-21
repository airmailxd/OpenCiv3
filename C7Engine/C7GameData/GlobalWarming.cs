using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine {
	// Civ3's global warming, as Civ3Conquests.exe 1.22 does it in the
	// interturn world update (FUN_004fd670, after the volcanoes; called from
	// FUN_004fe4d0 and FUN_004ff290 once a round).
	//
	// - World pollution is the sum of every city's pollution icons
	//   (FUN_004b8bc0, City.PollutionIcons) plus the square of the nuclear
	//   explosions and meltdowns so far (DAT_00a74e90, bumped by the nuke
	//   detonation FUN_005c29d0 and the meltdown FUN_004bb970, reset only for
	//   a new game; GameData.nuclearEvents). Pollution lying on the map
	//   doesn't count. This is the Conquests Civilopedia's "World pollution is
	//   the sum of all city pollution, nuclear explosions, and meltdowns"
	//   (GCON_Pollution). Buildings count only through the city icons (their
	//   BIQ pollution, and the two pollution flags of Mass Transit and the
	//   Recycling Center); no building or setting has an effect of its own.
	// - The sun icon (DAT_00a74e94): 0 with no world pollution, 1 with some,
	//   2 above a quarter of the map's tiles and 3 above half (SunLevel).
	// - Each round there are world pollution / 10 + 1 tries. Each strikes
	//   with a chance of world pollution in the map's tile count (a roll
	//   below world pollution in [0, tiles)), and then picks a tile with a
	//   second roll in [0, tiles), any tile on the map: the game draws both
	//   with its game random numbers (FUN_00626440 on DAT_00a74eac).
	// - A water tile is left alone. A land tile becomes its terrain's BIQ
	//   "Pollution Effect" (tile vtable +0x120, FUN_005f92b0), where "Base
	//   Terrain Type" (14) is the terrain under a forest or jungle. A tile
	//   whose terrain has none, or whose effect is water (Marsh -> Coast in
	//   conquests.biq: the effect must be below 11, Coast), is left alone,
	//   so coastlines never rise and the GLOBALWARMING_RISING_COASTLINES text
	//   is never shown. Nothing else is checked: cities, units, pollution and
	//   resources on the tile don't matter.
	// - The terrain is set as Civ3's FUN_005f9190 sets it: desert by a river
	//   becomes flood plain; desert, plains, grassland and tundra replace the
	//   base terrain too; other land terrains go over grassland, except a
	//   forest keeps a land base other than desert.
	// - Only when the tile then shows the effect's terrain (so not the
	//   desert turned flood plain) are its mines and irrigation removed
	//   (tile flags 0x4 and 0x8, vtable +0xcc) and the players told: the
	//   human who can see the tile now (the tile's four visibility masks at
	//   +0x5c, +0x60, +0x64, +0xd0, not the explored mask +0x58 the volcano
	//   messages use) gets Conquests/Text/script.txt's GLOBALWARMING "Global
	//   Warming has turned $TERRAIN0 into $TERRAIN1!" when the new terrain is
	//   desert, plains, grassland or tundra (index below 4), else
	//   GLOBALWARMING_FOREST_JUNGLE "Global Warming" has destroyed
	//   $TERRAIN0!" (the stray quote is Civ3's), $TERRAIN0 the old terrain
	//   and $TERRAIN1 the new. So with conquests.biq a forest on grassland
	//   reads "Global Warming has turned Forest into Grassland!".
	//
	// Forum lore that the code doesn't bear out: that warming only clears
	// forests and jungles (churchillpines and Android Assembly, C3X thread;
	// NJRBSA88, https://forums.civfanatics.com/threads/global-warming-doesnt-work.539761/)
	// and "One tile is affected by GW for every 9 tiles where pollution is
	// present" (Mad Bomber, https://codehappy.net/apolyton/threads/76575-1.htm).
	// Grassland and plains are hit like any other tile; the forest reports
	// may come from the chance being small until world pollution nears a
	// quarter of the map's tiles.
	//
	// Not modelled: the sun icon isn't shown (SunLevel is there for it).
	public static class GlobalWarming {
		private static readonly ILogger log = Log.ForContext(typeof(GlobalWarming));

		// The script.txt texts.
		public const string TurnedIntoText = "Global Warming has turned {0} into {1}!";
		public const string DestroyedText = "Global Warming\" has destroyed {0}!";

		public class Change {
			public Tile tile;
			public TerrainType oldTerrain;
			public TerrainType newTerrain;
		}

		// World pollution: every city's pollution icons plus the square of
		// the nuclear explosions and meltdowns so far.
		public static int WorldPollution(GameData gameData) {
			int result = 0;
			foreach (City city in gameData.cities) {
				result += city.PollutionIcons(gameData.rules);
			}
			return result + gameData.nuclearEvents * gameData.nuclearEvents;
		}

		// The sun icon's brightness, 0 (no sun) to 3.
		public static int SunLevel(GameData gameData) {
			return SunLevel(WorldPollution(gameData), gameData.map?.tiles?.Count ?? 0);
		}

		public static int SunLevel(int worldPollution, int tileCount) {
			if (worldPollution > tileCount / 2) {
				return 3;
			}
			if (worldPollution > tileCount / 4) {
				return 2;
			}
			return worldPollution > 0 ? 1 : 0;
		}

		// Rolls for global warming; called once per round, after every
		// player has played and after the volcanoes. Returns the tiles
		// changed.
		public static List<Change> ProcessEndOfRound(GameData gameData) {
			List<Change> changes = new();
			List<Tile> tiles = gameData.map?.tiles;
			if (tiles == null || tiles.Count == 0) {
				return changes;
			}
			int pollution = WorldPollution(gameData);
			int tries = pollution / 10 + 1;
			// Civ3 rolls even with no pollution; the roll can't strike then.
			for (int i = 0; i < tries; ++i) {
				if (GameData.rng.Next(tiles.Count) >= pollution) {
					continue;
				}
				Change change = Strike(gameData, tiles[GameData.rng.Next(tiles.Count)]);
				if (change != null) {
					changes.Add(change);
				}
			}
			return changes;
		}

		// What warming would turn the tile's terrain into, or null if it
		// leaves the tile alone.
		public static TerrainType TargetTerrain(GameData gameData, Tile tile) {
			if (tile == null || tile == Tile.NONE || tile.IsWater()) {
				return null;
			}
			string effect = tile.overlayTerrainType.PollutionEffect();
			TerrainType target;
			if (effect == TerrainType.PollutionEffectBaseTerrain) {
				target = tile.baseTerrainType;
			} else if (effect == null || effect == TerrainType.PollutionEffectNone) {
				return null;
			} else {
				target = gameData.terrainTypes.Find(t => t.Key == effect);
			}
			if (target == null || target == TerrainType.NONE || target.IsWater) {
				return null;
			}
			return target;
		}

		// Global warming strikes the tile. Returns the change, or null if
		// the tile stays as it is.
		public static Change Strike(GameData gameData, Tile tile) {
			TerrainType target = TargetTerrain(gameData, tile);
			if (target == null) {
				return null;
			}
			TerrainType oldTerrain = tile.overlayTerrainType;
			TerrainType oldBase = tile.baseTerrainType;
			SetTerrain(gameData, tile, target);
			if (tile.baseTerrainType != oldBase || tile.overlayTerrainType != oldTerrain) {
				TileChangeJournal.RecordTerrainChange(tile);
			}
			if (tile.overlayTerrainType != target) {
				return null;
			}

			foreach (TerrainImprovement improvement in tile.overlays.GetImprovements().ToList()) {
				if (improvement.key is Tile.TileOverlays.MINE or Tile.TileOverlays.IRRIGATION) {
					tile.overlays.Remove(improvement);
				}
			}
			log.Information("Global warming turns {Tile} from {Old} into {New}", tile, oldTerrain.DisplayName, target.DisplayName);

			string message = IsBasicTerrain(target)
				? string.Format(TurnedIntoText, oldTerrain.DisplayName, target.DisplayName)
				: string.Format(DestroyedText, oldTerrain.DisplayName);
			foreach (Player player in gameData.players) {
				if (player.IsToldNews && player.tileKnowledge != null && player.tileKnowledge.isActiveTile(tile)) {
					new MsgShowTemporaryPopup(message, tile, player).send();
				}
			}
			return new Change { tile = tile, oldTerrain = oldTerrain, newTerrain = target };
		}

		// Desert, plains, grassland or tundra: Civ3's terrains 0 to 3.
		private static bool IsBasicTerrain(TerrainType t) {
			return t.IsDesert || t.IsPlains || t.IsGrassland || t.IsTundra;
		}

		// Sets the tile's terrain as Civ3 FUN_005f9190 does.
		private static void SetTerrain(GameData gameData, Tile tile, TerrainType target) {
			TerrainType Find(System.Func<TerrainType, bool> which) => gameData.terrainTypes.Find(t => which(t));
			if (target.IsDesert && tile.BordersRiver() && Find(t => t.IsFloodPlain) is TerrainType floodPlain) {
				tile.overlayTerrainType = floodPlain;
				tile.baseTerrainType = target;
			} else if (IsBasicTerrain(target)) {
				tile.overlayTerrainType = target;
				tile.baseTerrainType = target;
			} else {
				tile.overlayTerrainType = target;
				TerrainType newBase = target.IsFloodPlain ? Find(t => t.IsDesert) : Find(t => t.IsGrassland);
				bool keepBase = target.IsForest && !tile.baseTerrainType.IsWater && !tile.baseTerrainType.IsDesert;
				if (!keepBase && newBase != null) {
					tile.baseTerrainType = newBase;
				}
			}
		}
	}
}

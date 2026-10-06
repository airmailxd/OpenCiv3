using System;
using System.Collections.Generic;
using C7GameData;
using Godot;
using static C7GameData.Tile.TileOverlays;

namespace C7.Map {
	public partial class TileOverlayLayer : LooseLayer {
		private readonly ImageTexture roadTexture;
		private readonly ImageTexture railroadTexture;
		private readonly ImageTexture grassIrrigationTexture;
		private readonly ImageTexture desertIrrigationTexture;
		private readonly ImageTexture plainsIrrigationTexture;
		private readonly ImageTexture tundraIrrigationTexture;
		private readonly ImageTexture ruinsTexture;
		private readonly ImageTexture pollutionTexture;
		private readonly ImageTexture cratersTexture;

		private readonly Vector2 tileSize;

		private int rng = 0;
		// The random texture variant chosen for each tile, by tile and by the per-layer offset given to GetRadomTextureIndex.
		private readonly Dictionary<(Tile tile, int offset), int> rngs = new();

		// Textures for the improvements without special drawing logic, by improvement key.
		private readonly Dictionary<string, ImageTexture> plainImprovementTextures = new();

		// A tile's improvements sorted by zIndex for drawing. A tile has at most one improvement per layer.
		private readonly List<TerrainImprovement> sortedImprovements = new();

		// The knowledge of the player whose view of the map is drawn, or null in observer mode. Tiles that player can't see now are drawn
		// with the improvements they last saw there, including when deciding how roads, irrigation and so on join up with their neighbors.
		private TileKnowledge knowledge;

		private static readonly TileDirection[] allDirections = [
			TileDirection.NORTH, TileDirection.NORTHEAST, TileDirection.EAST, TileDirection.SOUTHEAST,
			TileDirection.SOUTH, TileDirection.SOUTHWEST, TileDirection.WEST, TileDirection.NORTHWEST,
		];
		private static readonly TileDirection[] diagonalDirections = [
			TileDirection.NORTHWEST, TileDirection.NORTHEAST, TileDirection.SOUTHEAST, TileDirection.SOUTHWEST,
		];

		public TileOverlayLayer() {
			roadTexture = TextureLoader.Load("terrain_improvements.road");
			railroadTexture = TextureLoader.Load("terrain_improvements.railroad");
			tileSize = roadTexture.GetSize() / 16;
			// grid 16x16 tiles
			// assume that roads and railroads textures have the same size

			// Each irrigation.pcx has a 4x4 grid of irrigation tiles, with
			// each tile being 128x64 pixels.
			grassIrrigationTexture = TextureLoader.Load("terrain_improvements.irrigation.grass");
			desertIrrigationTexture = TextureLoader.Load("terrain_improvements.irrigation.desert");
			plainsIrrigationTexture = TextureLoader.Load("terrain_improvements.irrigation.plains");
			tundraIrrigationTexture = TextureLoader.Load("terrain_improvements.irrigation.tundra");

			ruinsTexture = TextureLoader.Load("terrain_improvements.ruins");
			pollutionTexture = TextureLoader.Load("terrain_improvements.pollution");
			cratersTexture = TextureLoader.Load("terrain_improvements.craters");

			rng = GameData.rng.Next(0, 5000) * 2 + 1;
		}

		public override void onGameDataReplaced(GameData gameData) {
			// Same tile IDs and the same rng give the same choices again.
			rngs.Clear();
		}

		public override void onBeginDraw(LooseView looseView, GameData gameData) {
			knowledge = looseView.tileKnowledge;
		}

		public override void onEndDraw(LooseView looseView, GameData gameData) {
			knowledge = null;
		}

		public override void drawObject(LooseView looseView, GameData gameData, Tile tile, Vector2 tileCenter) {
			// Draw in zIndex order, keeping the tile's order for equal zIndexes (a stable insertion sort).
			sortedImprovements.Clear();
			if (knowledge != null && knowledge.TryGetRememberedImprovements(tile, out TerrainImprovement[] remembered)) {
				foreach (TerrainImprovement ti in remembered) {
					InsertSorted(ti);
				}
			} else {
				foreach (TerrainImprovement ti in tile.overlays.terrainImprovementByLayer.Values) {
					InsertSorted(ti);
				}
			}
			if (sortedImprovements.Count == 0) {
				return;
			}

			Rect2 screenTarget = new Rect2(tileCenter - tileSize / 2, tileSize);

			foreach (TerrainImprovement ti in sortedImprovements) {
				switch (ti.key) {
					case IRRIGATION:
						DrawIrrigation(looseView, tile, screenTarget);
						break;
					case ROAD:
						DrawRoad(looseView, tile, screenTarget);
						break;
					case RAILROAD:
						DrawRailRoad(looseView, tile, screenTarget);
						break;
					case RUINS:
						DrawRuins(looseView, tile, tileCenter);
						break;
					case POLLUTION:
						DrawPollution(looseView, tile, screenTarget);
						break;
					case CRATERS:
						DrawCraters(looseView, tile, screenTarget);
						break;
					case FALLOUT:
						DrawFallout(looseView, tile, screenTarget);
						break;
					default:
						if (!plainImprovementTextures.TryGetValue(ti.key, out ImageTexture texture)) {
							texture = TextureLoader.Load($"terrain_improvements.{ti.key}");
							plainImprovementTextures[ti.key] = texture;
						}
						looseView.DrawTexture(texture, screenTarget.Position);
						break;
				}
			}
			sortedImprovements.Clear();
		}

		private void InsertSorted(TerrainImprovement ti) {
			int i = sortedImprovements.Count;
			while (i > 0 && sortedImprovements[i - 1].zIndex > ti.zIndex) {
				--i;
			}
			sortedImprovements.Insert(i, ti);
		}

		// Same as Tile.HasRoad, Tile.HasRailroad and Tile.HasIrrigation, with one dictionary lookup, but as the player last saw the tile.
		private TerrainImprovement ImprovementAt(Tile tile, TerrainImprovement.Layer layer) {
			if (knowledge != null && knowledge.TryGetRememberedImprovements(tile, out TerrainImprovement[] remembered)) {
				foreach (TerrainImprovement ti in remembered) {
					if (ti.layer == layer) {
						return ti;
					}
				}
				return null;
			}
			tile.overlays.terrainImprovementByLayer.TryGetValue(layer, out TerrainImprovement current);
			return current;
		}

		// Same as Tile.HasPollution and Tile.HasCraters, without allocating, but as the player last saw the tile.
		private bool HasImprovement(Tile tile, string key) {
			if (knowledge != null && knowledge.TryGetRememberedImprovements(tile, out TerrainImprovement[] remembered)) {
				foreach (TerrainImprovement ti in remembered) {
					if (ti.key == key) {
						return true;
					}
				}
				return false;
			}
			foreach (TerrainImprovement ti in tile.overlays.terrainImprovementByLayer.Values) {
				if (ti.key == key) {
					return true;
				}
			}
			return false;
		}

		private void DrawIrrigation(LooseView looseView, Tile tile, Rect2 screenTarget) {
			// Figure out which index into the irrigation texture to use for
			// this tile. Only the diagonal neighbors matter.
			int irrigationIndex = 0;
			foreach (TileDirection direction in diagonalDirections) {
				var neighbour = tile.neighbors[direction];
				if (ImprovementAt(neighbour, TerrainImprovement.Layer.ResourceDevelopment)?.key == IRRIGATION) {
					irrigationIndex |= GetIrrigationFlag(direction);
				}
			}

			// Deserts, plains, and tundra (??) have specific textures for
			// irrigation. Everything else uses the grassland texture.
			ImageTexture texture = tile.baseTerrainType.Key switch {
				"plains" => plainsIrrigationTexture,
				"desert" => desertIrrigationTexture,
				"tundra" => tundraIrrigationTexture,
				_ => grassIrrigationTexture
			};

			// Draw the subtexture of the irrigation texture for this tile.
			looseView.DrawTextureRectRegion(texture, screenTarget, GetIrrigationRect(irrigationIndex));
		}

		private void DrawRoad(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int roadIndex = 0;
			foreach (TileDirection direction in allDirections) {
				string key = ImprovementAt(tile.neighbors[direction], TerrainImprovement.Layer.Roads)?.key;
				if (key == ROAD || key == RAILROAD) {
					roadIndex |= GetRoadFlag(direction);
				}
			}
			looseView.DrawTextureRectRegion(roadTexture, screenTarget, GetRoadRect(roadIndex));
		}

		private void DrawRailRoad(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int roadIndex = 0;
			int railroadIndex = 0;
			foreach (TileDirection direction in allDirections) {
				string key = ImprovementAt(tile.neighbors[direction], TerrainImprovement.Layer.Roads)?.key;
				if (key == RAILROAD) {
					railroadIndex |= GetRoadFlag(direction);
				} else if (key == ROAD) {
					roadIndex |= GetRoadFlag(direction);
				}
			}
			if (roadIndex != 0) {
				looseView.DrawTextureRectRegion(roadTexture, screenTarget, GetRoadRect(roadIndex));
			}
			looseView.DrawTextureRectRegion(railroadTexture, screenTarget, GetRoadRect(railroadIndex));
		}

		private void DrawRuins(LooseView looseView, Tile tile, Vector2 tileCenter) {
			int ruinsIndex = GetRadomTextureIndex(tile, 3, 0x054F);

			var ruinsSingleTextureSize = new Vector2(167, 95);
			Rect2 screenRect = new(tileCenter - 0.5f * ruinsSingleTextureSize, ruinsSingleTextureSize);

			looseView.DrawTextureRectRegion(ruinsTexture, screenRect, new Rect2(167 * ruinsIndex, 0, ruinsSingleTextureSize));
		}

		private void DrawPollution(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int pollutionIndex = 0;
			foreach (TileDirection direction in diagonalDirections) {
				if (HasImprovement(tile.neighbors[direction], POLLUTION)) {
					pollutionIndex |= GetPollutionIndex(direction);
				}
			}

			// single tile pollution
			if (pollutionIndex == 0) {
				pollutionIndex = GetRadomTextureIndex(tile, 10, 0x11C8);

				looseView.DrawTextureRectRegion(pollutionTexture, screenTarget, GetPollutionRect(pollutionIndex));
			} else {
				looseView.DrawTextureRectRegion(pollutionTexture, screenTarget, GetPollutionRect(pollutionIndex - 1, 2));
			}

			// debug mask (with a FontFile loaded once, with FixedSize = 12)
			// looseView.DrawString(debugFont, tileCenter, $"{pollutionIndex}", modulate: Colors.Black);
		}

		// Civ3 has no separate fallout art, so fallout is drawn with the pollution
		// texture, tinted a sickly green to tell the two apart.
		private static readonly Color falloutTint = new Color(0.55f, 1.0f, 0.35f);

		private void DrawFallout(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int falloutIndex = 0;
			foreach (TileDirection direction in diagonalDirections) {
				if (HasImprovement(tile.neighbors[direction], FALLOUT)) {
					falloutIndex |= GetPollutionIndex(direction);
				}
			}

			if (falloutIndex == 0) {
				falloutIndex = GetRadomTextureIndex(tile, 10, 0x3F17);
				looseView.DrawTextureRectRegion(pollutionTexture, screenTarget, GetPollutionRect(falloutIndex), falloutTint);
			} else {
				looseView.DrawTextureRectRegion(pollutionTexture, screenTarget, GetPollutionRect(falloutIndex - 1, 2), falloutTint);
			}
		}

		private void DrawCraters(LooseView looseView, Tile tile, Rect2 screenTarget) {
			int cratersIndex = 0;
			foreach (TileDirection direction in diagonalDirections) {
				if (HasImprovement(tile.neighbors[direction], CRATERS)) {
					cratersIndex |= GetCraterIndex(direction);
				}
			}

			// single tile crater
			if (cratersIndex == 0) {
				cratersIndex = GetRadomTextureIndex(tile, 10, 0x24A5);

				looseView.DrawTextureRectRegion(cratersTexture, screenTarget, GetCratersRect(cratersIndex));
			} else {
				looseView.DrawTextureRectRegion(cratersTexture, screenTarget, GetCratersRect(cratersIndex - 1, 2));
			}
		}

		// Each kind of overlay passes its own offset, so the offset also identifies the kind.
		private int GetRadomTextureIndex(Tile tile, int variations, int offset) {
			if (rngs.TryGetValue((tile, offset), out int index)) {
				return index;
			}

			ID id = tile.Id;
			// The sums may overflow, so they're done without sign: a negative sum would give a negative index. Where nothing overflows, the
			// results are the same as with signed sums.
			var rand = new Random(unchecked(int.Parse(id.ToString().Replace("tile-", "")) + offset));
			uint customRng = unchecked((uint)rng + (uint)rand.Next() + (uint)rand.Next(0, variations));

			return rngs[(tile, offset)] = (int)(customRng % (uint)variations);
		}

		// Returns the rectangle within the road texture for a given index,
		// where the index has been constructed by OR'ing together the direction
		// flags for adjacent roads.
		private Rect2 GetRoadRect(int index) {
			int row = index >> 4;
			int column = index & 0xF;
			return new Rect2(column * tileSize.X, row * tileSize.Y, tileSize);
		}

		// Like above, but for irrigation.
		private Rect2 GetIrrigationRect(int index) {
			// The index is set up so that the layout looks like
			//
			//  0  1  2  3
			//  4  5  6  7
			//  ...
			int row = index / 4;
			int column = index % 4;
			return new Rect2(column * tileSize.X, row * tileSize.Y, tileSize);
		}

		// Like above, but for pollution
		private Rect2 GetPollutionRect(int index, int offset = 0) {
			// The index is set up so that the layout looks like
			//
			//  0  1  2  3  4
			//  5  6  7  8  9  
			//  ...
			int row = index / 5 + offset;
			int column = index % 5;
			return new Rect2(column * tileSize.X, row * tileSize.Y, tileSize);
		}

		// Like above, but for craters.
		private Rect2 GetCratersRect(int index, int offset = 0) {
			// The index is set up so that the layout looks like
			//
			//  0  1  2  3  4
			//  5  6  7  8  9  
			//  ...
			int row = index / 5 + offset;
			int column = index % 5;
			return new Rect2(column * tileSize.X, row * tileSize.Y, tileSize);
		}

		// The per-neighbor index values that can be OR'd together to get the
		// proper rectangle within the road/railroad texture.
		private static int GetRoadFlag(TileDirection direction) {
			return direction switch {
				TileDirection.NORTHEAST => 0x1,
				TileDirection.EAST => 0x2,
				TileDirection.SOUTHEAST => 0x4,
				TileDirection.SOUTH => 0x8,
				TileDirection.SOUTHWEST => 0x10,
				TileDirection.WEST => 0x20,
				TileDirection.NORTHWEST => 0x40,
				TileDirection.NORTH => 0x80,
				_ => throw new ArgumentOutOfRangeException("Invalid TileDirection")
			};
		}

		// Like getRoadFlag, but for irrigation, which only depends on the
		// diagonal neighbors.
		//
		// Index values taken from ClassicRenderer.java in
		// https://hg.sr.ht/~adj/civ3_cross_platform_editor.
		//
		// Some of these values are probably wrong, but I couldn't figure out the correct pattern.
		// Also, there might not be a perfect pattern, as I can also see inconsistencies
		// in the original editor/game as well.
		private static int GetIrrigationFlag(TileDirection direction) {
			return direction switch {
				TileDirection.NORTHWEST => 0x1,
				TileDirection.NORTHEAST => 0x2,
				TileDirection.SOUTHWEST => 0x4,
				TileDirection.SOUTHEAST => 0x8,
				TileDirection.EAST => 0,
				TileDirection.SOUTH => 0,
				TileDirection.WEST => 0,
				TileDirection.NORTH => 0,
				_ => throw new ArgumentOutOfRangeException("Invalid TileDirection")
			};
		}
		private static int GetPollutionIndex(TileDirection direction) {
			return direction switch {
				TileDirection.NORTHWEST => 0x1,
				TileDirection.NORTHEAST => 0x2,
				TileDirection.SOUTHEAST => 0x4,
				TileDirection.SOUTHWEST => 0x8,
				TileDirection.EAST => 0,
				TileDirection.SOUTH => 0,
				TileDirection.WEST => 0,
				TileDirection.NORTH => 0,
				_ => throw new ArgumentOutOfRangeException("Invalid TileDirection")
			};
		}
		private static int GetCraterIndex(TileDirection direction) {
			return direction switch {
				TileDirection.NORTHWEST => 0x1,
				TileDirection.NORTHEAST => 0x2,
				TileDirection.SOUTHEAST => 0x4,
				TileDirection.SOUTHWEST => 0x8,
				TileDirection.EAST => 0,
				TileDirection.SOUTH => 0,
				TileDirection.WEST => 0,
				TileDirection.NORTH => 0,
				_ => throw new ArgumentOutOfRangeException("Invalid TileDirection")
			};
		}
	}
}

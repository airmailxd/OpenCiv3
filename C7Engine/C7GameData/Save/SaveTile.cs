using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace C7GameData.Save {

	public class SaveTile {
		public SaveTile() { }

		public SaveTile(Tile tile) {
			id = tile.Id;
			extraInfo = tile.ExtraInfo;
			X = tile.XCoordinate;
			Y = tile.YCoordinate;
			continent = tile.continent;
			isFreshWater = tile.isFreshWater;
			baseTerrain = tile.baseTerrainType.Key;
			overlayTerrain = tile.overlayTerrainType.Key;
			if (tile.Resource != Resource.NONE) {
				resource = tile.ResourceKey;
			}
			if (tile.isBonusShield) {
				features.Add("bonusShield");
			}
			if (tile.isSnowCapped) {
				features.Add("snowCapped");
			}
			if (tile.isPineForest) {
				features.Add("pineForest");
			}
			// In the order the fields are declared on Tile.
			if (tile.riverNorth) features.Add("riverNorth");
			if (tile.riverNortheast) features.Add("riverNortheast");
			if (tile.riverEast) features.Add("riverEast");
			if (tile.riverSoutheast) features.Add("riverSoutheast");
			if (tile.riverSouth) features.Add("riverSouth");
			if (tile.riverSouthwest) features.Add("riverSouthwest");
			if (tile.riverWest) features.Add("riverWest");
			if (tile.riverNorthwest) features.Add("riverNorthwest");
			if (tile.hasBarbarianCamp) {
				features.Add("barbarianCamp");
			}
			if (tile.hasHadForestCleared) {
				features.Add("hasHadForestCleared");
			}

			overlays.AddRange(tile.overlays.GetImprovements().Select(i => i.key));
		}

		// Lookup tables for converting many tiles, built once rather than
		// searching the lists for every tile. Like List.Find, the first match
		// wins.
		internal class Lookups {
			internal readonly Dictionary<string, TerrainType> terrainTypesByKey = new();
			internal readonly Dictionary<string, Resource> resourcesByKey = new();
			internal readonly Dictionary<string, TerrainImprovement> improvementsByKey = new();

			internal Lookups(List<TerrainType> terrainTypes, List<Resource> resources, List<TerrainImprovement> improvements) {
				foreach (TerrainType tt in terrainTypes) {
					if (tt.Key != null) terrainTypesByKey.TryAdd(tt.Key, tt);
				}
				foreach (Resource r in resources) {
					if (r.Key != null) resourcesByKey.TryAdd(r.Key, r);
				}
				foreach (TerrainImprovement ti in improvements) {
					if (ti.key != null) improvementsByKey.TryAdd(ti.key, ti);
				}
			}

			internal static T Find<T>(Dictionary<string, T> dict, string key) where T : class {
				return key != null && dict.TryGetValue(key, out T value) ? value : null;
			}
		}

		// TODO: if this is slow, features can be read from JSON and then hashed so the Contains check is faster
		public Tile ToTile(List<TerrainType> terrainTypes, List<Resource> resources, List<TerrainImprovement> improvements) {
			return ToTile(new Lookups(terrainTypes, resources, improvements));
		}

		internal Tile ToTile(Lookups lookups) {
			Tile tile = new Tile(id){
				ExtraInfo = extraInfo,
				XCoordinate = X,
				YCoordinate = Y,
				continent = continent,
				isFreshWater = isFreshWater,
				baseTerrainType = Lookups.Find(lookups.terrainTypesByKey, baseTerrain),
				overlayTerrainType = Lookups.Find(lookups.terrainTypesByKey, overlayTerrain),
				hasBarbarianCamp = features.Contains("barbarianCamp"),
				hasHadForestCleared = features.Contains("hasHadForestCleared"),
				// TODO: load working tile
				ResourceKey = resource is null ? Resource.NONE.Key : resource,
				riverNorth = features.Contains("riverNorth"),
				riverNortheast = features.Contains("riverNortheast"),
				riverEast = features.Contains("riverEast"),
				riverSoutheast = features.Contains("riverSoutheast"),
				riverSouth = features.Contains("riverSouth"),
				riverSouthwest = features.Contains("riverSouthwest"),
				riverWest = features.Contains("riverWest"),
				riverNorthwest = features.Contains("riverNorthwest"),
				isBonusShield = features.Contains("bonusShield"),
				isSnowCapped = features.Contains("snowCapped"),
				isPineForest = features.Contains("pineForest"),
			};

			tile.Resource = tile.ResourceKey == Resource.NONE.Key ? Resource.NONE : Lookups.Find(lookups.resourcesByKey, tile.ResourceKey);
			overlays.ForEach(key => tile.overlays.Add(Lookups.Find(lookups.improvementsByKey, key)));

			return tile;
		}
		public Civ3ExtraInfo extraInfo;

		public ID id;
		public int X;
		public int Y;
		public int continent;
		public bool isFreshWater;
		[JsonRequired]
		public string baseTerrain;
		public string overlayTerrain;
		public string resource;
		public List<string> features = new List<string>();
		public List<string> overlays = new List<string>();
	}

}

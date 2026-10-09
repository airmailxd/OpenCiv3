using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace C7GameData.Save {

	public class SaveTile {
		public SaveTile() { }

		// A copy sharing everything with this one, for a LAN host to change
		// a little for one guest (see C7Engine.Network.SnapshotFilter).
		internal SaveTile ShallowCopy() {
			return (SaveTile)MemberwiseClone();
		}

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
			if (tile.hasGoodyHut) {
				features.Add("goodyHut");
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
			// A tile can't do without its terrain, so an unknown one is an
			// error in the save.
			TerrainType baseTerrainType = Lookups.Find(lookups.terrainTypesByKey, baseTerrain)
				?? throw new KeyNotFoundException($"The tile at ({X}, {Y}) has unknown base terrain {baseTerrain}");
			TerrainType overlayTerrainType = overlayTerrain == null ? baseTerrainType
				: Lookups.Find(lookups.terrainTypesByKey, overlayTerrain)
					?? throw new KeyNotFoundException($"The tile at ({X}, {Y}) has unknown overlay terrain {overlayTerrain}");
			Tile tile = new Tile(id){
				ExtraInfo = extraInfo,
				XCoordinate = X,
				YCoordinate = Y,
				continent = continent,
				isFreshWater = isFreshWater,
				baseTerrainType = baseTerrainType,
				overlayTerrainType = overlayTerrainType,
				hasBarbarianCamp = features.Contains("barbarianCamp"),
				hasGoodyHut = features.Contains("goodyHut"),
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

			if (tile.ResourceKey == Resource.NONE.Key) {
				tile.Resource = Resource.NONE;
			} else {
				tile.Resource = Lookups.Find(lookups.resourcesByKey, tile.ResourceKey);
				if (tile.Resource == null) {
					Serilog.Log.Warning("The tile at ({X}, {Y}) has unknown resource {Resource}, which is left out", X, Y, tile.ResourceKey);
					tile.Resource = Resource.NONE;
					tile.ResourceKey = Resource.NONE.Key;
				}
			}
			foreach (string key in overlays) {
				TerrainImprovement improvement = Lookups.Find(lookups.improvementsByKey, key);
				if (improvement == null) {
					Serilog.Log.Warning("The tile at ({X}, {Y}) has unknown terrain improvement {Improvement}, which is left out", X, Y, key);
					continue;
				}
				tile.overlays.Add(improvement);
			}

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

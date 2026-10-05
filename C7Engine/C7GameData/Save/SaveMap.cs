using System.Collections.Generic;
using System.Linq;

namespace C7GameData.Save {

	public class SaveMap {
		public int tilesWide, tilesTall;
		public bool wrapHorizontally, wrapVertically;
		public int techRate;
		public int optimalNumberOfCities;
		public List<SaveTile> tiles = new List<SaveTile>();
		public List<SaveTile> startingLocations = new();
		public SaveMap() { }

		public SaveMap(GameMap map) {
			tilesWide = map.numTilesWide;
			tilesTall = map.numTilesTall;
			wrapHorizontally = map.wrapHorizontally;
			wrapVertically = map.wrapVertically;
			techRate = map.techRate;
			optimalNumberOfCities = map.optimalNumberOfCities;
			tiles = map.tiles.ConvertAll(tile => new SaveTile(tile));
			startingLocations = map.startingLocations.ConvertAll(x => new SaveTile(x));
		}
		public GameMap ToGameMap(GameData gd) {
			GameMap gameMap = new GameMap{
				numTilesWide = tilesWide,
				numTilesTall = tilesTall,
				wrapHorizontally = wrapHorizontally,
				wrapVertically = wrapVertically,
				techRate = techRate,
				optimalNumberOfCities = optimalNumberOfCities,
			};
			SaveTile.Lookups lookups = new(gd.terrainTypes, gd.Resources, gd.terrainImprovements);
			gameMap.tiles = tiles.ConvertAll(tile => tile.ToTile(lookups));
			foreach (SaveTile st in startingLocations) {
				gameMap.startingLocations.Add(FindTile(gameMap, st.X, st.Y));
			}
			gameMap.computeNeighbors();
			gameMap.barbarianCamps = gameMap.tiles.Where(tile => tile.hasBarbarianCamp).ToList();
			return gameMap;
		}

		// Finds the tile with exactly the given coordinates, or null.
		private static Tile FindTile(GameMap map, int x, int y) {
			if (x >= 0 && y >= 0 && x < map.numTilesWide && y < map.numTilesTall) {
				int index = map.tileCoordsToIndex(x, y);
				if (index >= 0 && index < map.tiles.Count) {
					Tile t = map.tiles[index];
					if (t.XCoordinate == x && t.YCoordinate == y) {
						return t;
					}
				}
			}
			// The tiles aren't laid out as expected, so search for it.
			return map.tiles.Find(t => t.XCoordinate == x && t.YCoordinate == y);
		}
	}

}

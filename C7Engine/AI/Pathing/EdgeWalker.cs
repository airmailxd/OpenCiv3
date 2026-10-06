using System.Collections.Generic;
using C7GameData;

namespace C7Engine.Pathing {
	public abstract class EdgeWalker<TNode> {
		public abstract IEnumerable<Edge<TNode>> getEdges(TNode node);
	}

	public class UnitWalker : EdgeWalker<Tile> {
		private MapUnit unit;

		public UnitWalker(MapUnit unit) {
			this.unit = unit;
		}

		public override IEnumerable<Edge<Tile>> getEdges(Tile node) {
			List<Edge<Tile>> result = new List<Edge<Tile>>(8);
			Player owner = unit.owner;
			bool isHuman = owner.isHuman;
			bool isLandUnit = unit.IsLandUnit();
			bool isWaterUnit = unit.IsWaterUnit();
			float unitMovementPoints = unit.MaxMovementPoints();
			foreach (KeyValuePair<TileDirection, Tile> pair in node.neighbors) {
				TileDirection direction = pair.Key;
				Tile neighbor = pair.Value;

				// Tiles off the edge of the map can't be walked onto.
				if (neighbor == Tile.NONE) {
					continue;
				}

				bool isPassable = false;

				if (isHuman && !owner.HasExploredTile(neighbor)) {
					isPassable = true;
				} else {
					if (isLandUnit)
						isPassable = neighbor.IsLand();
					else if (isWaterUnit)
						isPassable = (neighbor.IsWater() && unit.CanEnterWaterTerrain(neighbor))
							|| (neighbor.cityAtTile != null && neighbor.cityAtTile.owner == owner);
				}

				if (!isPassable) {
					continue;
				}

				result.Add(new Edge<Tile>(node, neighbor, EdgeCost(owner, node, direction, neighbor, unitMovementPoints)));
			}
			return result;
		}

		// The cost, as a fraction of a turn, of moving from `node` to its
		// neighbor `neighbor` in direction `direction`.
		//
		// If this tile would consume all of the movement points of this
		// unit, it has a cost of 1 turn. Otherwise we use the fraction
		// of a turn it would use as the cost.
		//
		// Examples:
		//  - Warrior (1mp) moving onto Grassland (cost 1) => 1 turn
		//  - Warrior (1mp) moving onto Hills (cost 2) => 1 turn
		//  - Warrior (1mp) moving onto Jungle (cost 3) => 1 turn
		//
		//  - Cavalry (3mp) moving onto Grassland (cost 1) => 1/3
		//  - Cavalry (3mp) moving onto Hills (cost 2) => 2/3
		//  - Cavalry (3mp) moving onto Jungle (cost 3) => 3/3
		//
		internal static float EdgeCost(Player owner, Tile node, TileDirection direction, Tile neighbor, float unitMovementPoints) {
			float tileMovementCost = TilePath.GetMovementCost(owner, node, direction, neighbor);
			return tileMovementCost >= unitMovementPoints ? 1 : tileMovementCost / unitMovementPoints;
		}
	}
}

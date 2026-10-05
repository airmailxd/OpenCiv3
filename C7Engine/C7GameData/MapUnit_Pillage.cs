using System.Linq;
using C7Engine;
using static C7GameData.PlayerRelationship;

namespace C7GameData;

public partial class MapUnit {
	// The improvement pillaging this tile would remove: mines, irrigation and
	// fortresses go before roads.
	private TerrainImprovement PillageTarget() {
		// The first man-made improvement that isn't a road, else the first
		// road.
		TerrainImprovement road = null;
		foreach (TerrainImprovement i in location.overlays.GetImprovements()) {
			if (!Tile.TileOverlays.IsManMade(i))
				continue;
			if (i.layer != TerrainImprovement.Layer.Roads)
				return i;
			road ??= i;
		}
		return road;
	}

	// Units can pillage outside cities on tiles that are unowned, their own,
	// or owned by a civ they're at war with.
	public bool CanPillage() {
		if (!unitType.actions.Contains(UnitAction.Pillage) || !movementPoints.canMove || location.HasCity()) {
			return false;
		}
		Player tileOwner = location.OwningPlayer();
		if (tileOwner != null && tileOwner != owner && !AtWar(owner, tileOwner)) {
			return false;
		}
		return PillageTarget() != null;
	}

	// Destroys one improvement on the unit's tile, leaving behind what it was
	// built on (a railroad leaves a road), and ends the unit's turn.
	public bool Pillage() {
		if (!CanPillage()) {
			return false;
		}
		TerrainImprovement target = PillageTarget();
		log.Information($"{this} pillages the {target.key} at {location}");
		location.overlays.Remove(target);
		location.overlays.Add(target.upgradesFrom);
		movementPoints.onConsumeAll();
		Wake();
		return true;
	}
}

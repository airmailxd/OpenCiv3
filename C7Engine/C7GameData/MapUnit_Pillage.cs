using System.Collections.Generic;
using System.Linq;
using C7Engine;
using static C7GameData.PlayerRelationship;

namespace C7GameData;

public partial class MapUnit {
	// The improvements one pillage would remove. A railroad goes on its own
	// first, leaving its road behind; otherwise every man-made improvement
	// goes at once.
	private List<TerrainImprovement> PillageTargets() {
		List<TerrainImprovement> targets = new();
		foreach (TerrainImprovement i in location.overlays.GetImprovements()) {
			if (!Tile.TileOverlays.IsManMade(i))
				continue;
			if (i.upgradesFrom != null)
				return new() { i };
			targets.Add(i);
		}
		return targets;
	}

	// Units can pillage outside cities on tiles that are unowned, their own,
	// or owned by a civ they're at war with. Only armies can pillage more than
	// once per turn.
	public bool CanPillage() {
		if (!unitType.actions.Contains(UnitAction.Pillage) || !movementPoints.canMove || location.HasCity()) {
			return false;
		}
		if (hasPillagedThisTurn && !IsArmy()) {
			return false;
		}
		Player tileOwner = location.OwningPlayer();
		if (tileOwner != null && tileOwner != owner && !AtWar(owner, tileOwner)) {
			return false;
		}
		return PillageTargets().Count > 0;
	}

	// Destroys the improvements on the unit's tile (see PillageTargets) for
	// one movement point.
	public bool Pillage() {
		if (!CanPillage()) {
			return false;
		}
		foreach (TerrainImprovement target in PillageTargets()) {
			log.Information($"{this} pillages the {target.key} at {location}");
			location.overlays.Remove(target);
			if (target.upgradesFrom != null)
				location.overlays.Add(target.upgradesFrom);
		}
		movementPoints.onUnitMove(1);
		hasPillagedThisTurn = true;
		Wake();
		return true;
	}
}

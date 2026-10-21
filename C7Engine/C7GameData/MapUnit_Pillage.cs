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
	// one movement point. Armies pillage for nothing: in Conquests "Armies
	// now have blitz ... and can pillage without expending a movement point"
	// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/).
	//
	// On a barricade, pillaging costs all the unit's remaining moves, an
	// army's too: the pillage code "also checks whether the tile has a
	// barricade, if it does the unit loses all of its remaining moves"
	// (Flintlock, reading the 1.22 code,
	// https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16401798).
	public bool Pillage() {
		if (!CanPillage()) {
			return false;
		}
		bool onBarricade = location.overlays.HasImprovementWithKey(Tile.TileOverlays.BARRICADE, TerrainImprovement.Layer.Holdings);
		foreach (TerrainImprovement target in PillageTargets()) {
			log.Information("{Unit} pillages the {Improvement} at {Tile}", this, target.key, location);
			location.overlays.Remove(target);
			if (target.upgradesFrom != null)
				location.overlays.Add(target.upgradesFrom);
		}
		location.OwningPlayer()?.AddWarWeariness(owner, Player.WarWearinessForPillagedOrBombedImprovement);
		if (onBarricade)
			movementPoints.onConsumeAll();
		else if (!IsArmy())
			movementPoints.onUnitMove(1);
		hasPillagedThisTurn = true;
		Wake();
		return true;
	}
}

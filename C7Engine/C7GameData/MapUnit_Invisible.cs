using System;
using System.Collections.Generic;
using C7Engine;

namespace C7GameData;

// Invisible units, like Submarines: "[Submarines] can operate invisibly to
// enemy units because they travel beneath the waves. They remain invisible
// until they attack a surface ship, after which they again disappear.
// However, other submarines and AEGIS Cruisers and Destroyers can see them
// plainly" (Conquests Civilopedia, PRTO_Submarine). In conquests.biq those
// four have "Detect Invisible".
//
// UNVERIFIED: no source found gives how far a detecting unit sees invisible
// units, so it sees them on the tiles it sees (TileKnowledge's sight of a
// unit, two tiles over water). Nor does any say what happens on moving into
// a tile with an unseen unit; Civ3's "submarine bug"
// (https://forums.civfanatics.com/threads/submarine-bug-investigation.526635/)
// has units on a goto attack a submarine in their way, so a unit moving into
// an unseen enemy fights it, and is stopped by an unseen unit of a civ it
// isn't at war with (see Move).
public partial class MapUnit {
	// Whether the viewer can see this unit, when it can see its tile: its
	// owner always does, and others see invisible units only with a unit
	// that detects them nearby. A unit carried by an invisible one (a
	// missile aboard a Nuclear Submarine) is as hidden as its carrier.
	public bool IsSeenBy(Player viewer) {
		if (viewer == null || viewer == owner) {
			return true;
		}
		MapUnit hider = this;
		if (!unitType.isInvisible) {
			if (loadedOnUnitId == null) {
				return true;
			}
			hider = Carrier();
			if (hider == null || hider == NONE || !hider.unitType.isInvisible) {
				return true;
			}
			if (viewer == hider.owner) {
				return true;
			}
		}
		return DetectsInvisibleUnitsOn(viewer, hider.location);
	}

	// Whether one of the viewer's units that detect invisible units sees
	// the tile.
	public static bool DetectsInvisibleUnitsOn(Player viewer, Tile tile) {
		if (viewer == null || !Tile.IsTileValid(tile)) {
			return false;
		}
		// No unit sees further than two tiles, or a radar unit's range.
		int range = Math.Max(2, EngineStorage.gameData?.rules?.RadarTileVisibility ?? 2);
		foreach (Tile t in tile.GetTilesWithinTileSquare(range)) {
			if (!HasDetectorOf(viewer, t)) {
				continue;
			}
			if (t == tile || viewer.tileKnowledge.GetTilesVisibleToUnit(t).Contains(tile)) {
				return true;
			}
		}
		return false;
	}

	private static bool HasDetectorOf(Player viewer, Tile tile) {
		List<MapUnit> units = tile.unitsOnTile;
		for (int i = 0; i < units.Count; ++i) {
			if (units[i].owner == viewer && units[i].unitType.detectsInvisible) {
				return true;
			}
		}
		return false;
	}
}

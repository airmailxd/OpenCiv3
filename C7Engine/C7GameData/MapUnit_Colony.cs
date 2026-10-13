using C7Engine;
using static C7GameData.Tile.TileOverlays;

namespace C7GameData;

public partial class MapUnit {
	// The game's colony improvement, or null if the rules have none.
	private static TerrainImprovement ColonyImprovement() {
		foreach (TerrainImprovement ti in EngineStorage.gameData.terrainImprovements) {
			if (ti.key == COLONY) {
				return ti;
			}
		}
		return null;
	}

	// Whether the unit can found a colony where it stands (see
	// Tile.CanTakeColony).
	public bool CanBuildColony() {
		return unitType.actions.Contains(UnitAction.BuildColony)
			&& movementPoints.canMove
			&& !IsLockedInArmy()
			&& !IsLoaded()
			&& location.CanTakeColony(owner)
			&& ColonyImprovement() != null;
	}

	// Founds a colony where the unit stands, using the unit up: "The worker
	// is consumed by building a colony" (Conquests Civilopedia,
	// TFRM_Build_Colony).
	// UNVERIFIED: the Civilopedia doesn't say how long it takes; like
	// joining a city, it is done at once.
	public bool BuildColony() {
		if (!CanBuildColony()) {
			log.Warning("{Unit} can't build a colony at {Tile}", this, location);
			return false;
		}
		log.Information("{Unit} founds a colony on the {Resource} at {Tile}", this, location.Resource.Name, location);
		Tile tile = location;
		Player builder = owner;
		RemoveFromPlay();
		tile.PlaceHolding(ColonyImprovement(), builder);
		return true;
	}
}

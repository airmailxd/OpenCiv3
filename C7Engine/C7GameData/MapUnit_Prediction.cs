using System.Linq;
using C7Engine;

namespace C7GameData;

// The steps a LAN guest takes for its own units before the host has, so that
// they show at once rather than a round trip later (see
// C7Engine.Network.MovePrediction). Only plain steps are taken: those whose
// outcome the guest can be sure of from what it sees, with no fight, nothing
// found, nobody met and nothing left to chance. The host's snapshot replaces
// the guest's game soon after, whether it agrees or not.
public partial class MapUnit {
	// Whether stepping onto the neighboring tile is a plain step.
	internal bool CanPredictStepTo(Tile tile) {
		if (tile == null || tile == Tile.NONE || location == null || location == Tile.NONE
			|| !location.neighbors.ContainsValue(tile) || !movementPoints.canMove) {
			return false;
		}
		// Boarding, leaving or carrying other units is left to the host, as
		// are air units, whose moves are missions.
		if (IsAirUnit() || IsLoaded() || Passengers().Count > 0) {
			return false;
		}
		// A tile out of sight may hide units, and anything not ours is
		// someone to fight, capture, notice or ask. What's in a goody hut is
		// left to chance.
		if (!owner.tileKnowledge.isActiveTile(tile) || tile.hasBarbarianCamp || tile.hasGoodyHut
			|| (tile.HasCity() && tile.cityAtTile.owner != owner)
			|| tile.unitsOnTile.Any(u => u.owner != owner)) {
			return false;
		}
		if (IsLandUnit() ? !tile.IsLand() : !tile.IsWater() && !tile.HasCity()) {
			return false;
		}
		if (!CanEnterPeacefully(tile, out Intent intent) || intent != Intent.MoveFreely) {
			return false;
		}
		// Enemies next to both tiles may take a free shot.
		if (FindZoneOfControlAttackers(location, tile).Count > 0) {
			return false;
		}
		// Meeting a civilization for the first time is the host's news.
		foreach (Tile t in tile.neighbors.Values) {
			if (t == Tile.NONE) {
				continue;
			}
			if (t.unitsOnTile.Count > 0 && !HasMet(t.unitsOnTile[0].owner)) {
				return false;
			}
			if (t.owningCity != null && !HasMet(t.owningCity.owner)) {
				return false;
			}
		}
		return true;
	}

	private bool HasMet(Player other) {
		return other == owner || other.isBarbarians || other.defeated || owner.isBarbarians
			|| owner.playerRelationships.ContainsKey(other.id);
	}

	// Takes a step CanPredictStepTo allows, as Move would. What the unit sees
	// from its new tile isn't revealed: the guest may not have been sent the
	// tiles it hasn't seen, and the host's snapshot shows them.
	internal void PredictStepTo(Tile tile) {
		TileDirection dir = location.DirectionTo(tile);
		float movementCost = TilePath.GetMovementCost(owner, location, dir, tile);
		facingDirection = dir;
		Wake();
		location.unitsOnTile.Remove(this);
		TileChangeJournal.Record(location);
		TileChangeJournal.Record(tile);
		tile.unitsOnTile.Add(this);
		location = tile;
		movementPoints.onUnitMove(movementCost);
	}
}

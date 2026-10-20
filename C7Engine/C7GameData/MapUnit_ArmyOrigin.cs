namespace C7GameData;

// Where an army may take in members. The Civ3 1.29f readme says "Once that
// army leaves the city of it's construction, you cannot add or remove units
// from the army." So an army remembers the city that made it: the city that
// built it, or the one a leader formed it in.
//
// We read "leaves the city of its construction" as a one-way event: units may
// join only while the army is in its origin city and hasn't left it since it
// was made. An army that goes out and comes back is still one that has left,
// so its members stay fixed; and it never takes members in any other city,
// even one of its owner's.
public partial class MapUnit {
	// The city that made this army, or null if it isn't an army, wasn't made
	// in a city, or comes from a save made before armies remembered it.
	public ID armyOriginCityId { get; internal set; }

	// Whether the army has left its origin city since it was made.
	public bool hasLeftArmyOrigin { get; internal set; }

	// Remembers the city the army is made in. Called when an army appears on
	// a city's tile (see GameData.SpawnUnit).
	internal void RecordArmyOrigin() {
		if (!IsArmy() || !InOwnCity()) {
			return;
		}
		armyOriginCityId = location.cityAtTile.id;
		hasLeftArmyOrigin = false;
	}

	// Called whenever the unit's tile changes. Once an army with an origin
	// city is anywhere else, it has left it for good.
	private void NoteLeavingArmyOrigin() {
		if (armyOriginCityId == null || hasLeftArmyOrigin) {
			return;
		}
		if (location?.cityAtTile?.id != armyOriginCityId) {
			hasLeftArmyOrigin = true;
		}
	}

	// Whether this army may take in members where it is now: in its origin
	// city, not having left it. An army that doesn't know its origin city (from
	// an older save, or made outside a city) takes them in any of its owner's
	// cities, the rule before armies remembered where they were made.
	public bool CanTakeMembersHere() {
		if (!IsArmy() || !InOwnCity()) {
			return false;
		}
		if (armyOriginCityId == null) {
			return true;
		}
		return !hasLeftArmyOrigin && location.cityAtTile.id == armyOriginCityId;
	}
}

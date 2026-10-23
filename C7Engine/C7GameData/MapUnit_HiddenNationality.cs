using C7Engine;

namespace C7GameData;

// Hidden nationality, like the Privateer's: "Privateers are small frigates
// that carry no nationality markings, allowing them to attack and be attacked
// without revealing the nationality of the ship. Thus, you can attack another
// civilization's shipping without precipitating war" (Conquests Civilopedia,
// PRTO_Privateer). Hidden nationality units "can attack without starting a
// war, but if you attack them it starts a war", and "Human HN units can not
// attack cities when at peace"
// (https://forums.civfanatics.com/threads/hidden-nationality-observations.434753/),
// so they attack units at peace only outside cities.
//
// NOT CIV3 (owner's ruling, 2026-10-23): outside a city, a hidden
// nationality unit is at war with everyone else, as the Civilopedia's
// "attack and be attacked without revealing the nationality" reads, not as
// the observation above has it. Anyone may attack it without declaring war,
// and no one is shown whose it is. Teammates still never fight.
// Attacks by or on one cause no war weariness, since no one knows whose
// unit it was.
//
// UNVERIFIED: whether the victim's AI thinks worse of the attacker. The
// Civilopedia says the nationality isn't revealed, and one player's "they do
// get mad about it ... could have been a nasty coincidence"
// (https://forums.civfanatics.com/threads/what-did-you-attack-my-privateer-for-a-privateer-rop-exploit.36358/)
// is no rule, so attitudes don't change. Nor is it known whether units at
// peace may be captured, so they aren't: the attacker only fights, and moves
// in only if the tile is left empty.
public partial class MapUnit {
	// Whether attacking the unit is a hidden nationality attack: this unit
	// has hidden nationality, and the unit, outside a city, belongs to a
	// civ it isn't at war with.
	public bool MakesHiddenAttackOn(MapUnit target) {
		return unitType.hasHiddenNationality && target != null && target != NONE && target.owner != owner
			&& owner.IsAtPeaceWith(target.owner) && Tile.IsTileValid(target.location) && !target.location.HasCity();
	}

	// Whether the player's units may attack this one without war: it has
	// hidden nationality, is outside a city, and isn't the player's or a
	// teammate's.
	public bool IsFairGameFor(Player player) {
		return unitType.hasHiddenNationality && player != null && owner != null && player != owner
			&& Tile.IsTileValid(location) && !location.HasCity()
			&& EngineStorage.gameData?.AreTeammates(player, owner) != true;
	}

	// Whether the player may know whose unit this is.
	public bool ShowsOwnerTo(Player player) {
		return !unitType.hasHiddenNationality || player == null || player == owner
			|| EngineStorage.gameData?.AreTeammates(player, owner) == true;
	}

	// Whether this unit may attack the unit: one of a civ at war with its
	// owner, any other civ's when this unit has hidden nationality, or one
	// with hidden nationality.
	public bool MayAttack(MapUnit target) {
		return target != null && target != NONE && target.owner != owner
			&& (!owner.IsAtPeaceWith(target.owner) || MakesHiddenAttackOn(target) || target.IsFairGameFor(owner));
	}

	// Whether attacking the unit goes without war though its owner is at
	// peace with ours: either side has hidden nationality.
	public bool AttacksWithoutWar(MapUnit target) {
		return target != null && target != NONE && owner.IsAtPeaceWith(target.owner)
			&& (MakesHiddenAttackOn(target) || target.IsFairGameFor(owner));
	}
}

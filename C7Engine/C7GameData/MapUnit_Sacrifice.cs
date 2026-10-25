using System.Collections.Generic;
using C7Engine;
using C7GameData.Save;

namespace C7GameData;

// Sacrifice (Conquests): a captive is sacrificed in one of its owner's cities
// for culture. "Ritual Sacrifice allows you to take an "enslaved" enemy unit
// and "sacrifice" it in one of your friendly cities ... The worker is
// immediately sacrificed to the gods and you receive a set number of Culture
// Points ... These Culture Points are added to your [total] Culture Points and
// for the total in that city" (the Mesoamerica scenario's Civilopedia,
// GCON_Ritual_Sacrifice). The rules are the BIQ's, so any scenario may use
// them: a unit type with the "Sacrifice" special action, a tech that "Permits
// Sacrifice", and buildings that "Double Sacrifice".
//
// From Civ3 Conquests 1.22's code:
// - Whether a unit may be sacrificed (FUN_005d0670, the check for unit command
//   0x10100000, which FUN_004e3430 runs when the button is pressed): it has
//   moves left, its type has the Sacrifice action, it isn't the barbarians',
//   it isn't a Flag Unit, it has hit points left, its owner knows a tech that
//   permits sacrifice, its nationality isn't its owner's ("only foreign units
//   (whether captured or enslaved) may be sacrificed", tjs282,
//   https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16186366),
//   and it is in a city. The Civilopedia says the city is one of the owner's.
// - The culture (FUN_005d1990): twice the unit's shield cost to its owner
//   (FUN_005766b0's cost times the constant 20 at 0x74be3c, over 10), "times
//   two for each doubling improvement in the city" (Flintlock,
//   https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/page-144#post-16841608):
//   each building type with the flag that the city has and whose owner hasn't
//   made obsolete. None at all under a xenophobic government (the GOVT flag
//   read into the government at +0x24) when less than 51% of the city's
//   citizens are of its owner's nationality.
// - Sacrificing (FUN_005d1a90): the unit is removed, the culture is added to
//   the city's culture for its owner and to the owner's total, and the owner
//   is told SACRIFICE_REPORT at the city.
public partial class MapUnit {
	// The culture of a sacrifice per shield the unit costs (Civ3's 20, over
	// 10, at 0x74be3c).
	internal const int SacrificeCulturePerShield = 2;

	// The least share of a city's citizens that must be of its owner's
	// nationality for a sacrifice there to give culture under a xenophobic
	// government (Civ3 FUN_005d1990 compares FUN_004c2990's share to 0.51).
	internal const int XenophobicSacrificeNativePercent = 51;

	// Whether the unit can be sacrificed where it stands (see above).
	public bool CanSacrifice() {
		return unitType.actions.Contains(UnitAction.Sacrifice) && movementPoints.canMove && MaySacrifice();
	}

	// The conditions besides the unit type's Sacrifice action and moves left.
	// Civ3's AI (FUN_005d1f20) checks only these, so its AI sacrificed units
	// whose type lacks the action, a bug C3X fixes ("Fix AI players being able
	// to sacrifice units that don't have the sacrifice special ability set on
	// their type", Flintlock,
	// https://forums.civfanatics.com/threads/c3x-exe-mod-including-bug-fixes-stack-bombard-and-much-more.666881/post-16869943).
	// Our AI uses CanSacrifice.
	private bool MaySacrifice() {
		if (owner == null || owner.IsIndependent || unitType.isFlagUnit || hitPointsRemaining <= 0) {
			return false;
		}
		if (!owner.HasTechEnabling(SaveTech.Flag.PermitsSacrifice) || !IsCaptive()) {
			return false;
		}
		return Tile.IsTileValid(location) && location.HasCity() && location.cityAtTile.owner == owner;
	}

	// The culture sacrificing the unit gives the city it is in (see above).
	public int SacrificeValue() {
		int value = SacrificeCulturePerShield * System.Math.Max(1, owner.ShieldCost(unitType));
		City city = Tile.IsTileValid(location) ? location.cityAtTile : null;
		if (city == null) {
			return value;
		}

		if (city.owner?.government?.xenophobic == true && !HasNativeMajorityForSacrifice(city)) {
			return 0;
		}

		HashSet<Building> doubling = [];
		foreach (CityBuilding cb in city.AllEffectiveBuildings()) {
			Building b = cb.building;
			bool obsolete = b.renderedObsoleteBy != null && city.owner.knownTechs.Contains(b.renderedObsoleteBy.id);
			if (b.doublesSacrifice && !obsolete && doubling.Add(b)) {
				value *= 2;
			}
		}
		return value;
	}

	// Whether at least 51% of the city's citizens are of its owner's
	// nationality; a city of none counts as all native, as Civ3's FUN_004c2990
	// does.
	private static bool HasNativeMajorityForSacrifice(City city) {
		int total = city.residents.Count;
		if (total == 0) {
			return true;
		}
		int native = 0;
		foreach (CityResident r in city.residents) {
			if (r.nationality == null || r.nationality == city.owner.civilization) {
				++native;
			}
		}
		return native * 100 >= XenophobicSacrificeNativePercent * total;
	}

	// Sacrifices the unit, adding its culture to its city. Returns whether it
	// was sacrificed.
	public bool Sacrifice() {
		if (!CanSacrifice()) {
			return false;
		}
		GameData gameData = EngineStorage.gameData;
		City city = location.cityAtTile;
		Player player = owner;
		int value = SacrificeValue();
		string unitName = name;

		log.Information("{Player} sacrifices {Unit} in {City} for {Culture} culture", player, this, city, value);
		gameData.RemoveUnit(this);

		int startLevel = city.GetBorderExpansionLevel();
		city.perPlayerCulture[city.owner] = city.GetCultureFor(city.owner) + value;
		// The borders grow at once if the culture reaches the next level, as
		// they do at the end of a turn (see Player).
		if (city.GetBorderExpansionLevel() != startLevel) {
			gameData.UpdateTileOwners();
			BarbarianInteractions.DisperseCampsWithinBorders(gameData);
			GoodyHuts.OpenWithinBorders(gameData, city);
			gameData.InvalidateCachedTradeNetwork();
		}

		if (player.IsToldNews) {
			// Civ3's news (script.txt, #SACRIFICE_REPORT).
			new MsgShowTemporaryPopup($"The Sacrifice of {unitName} has pleased the gods!  Culture rises {value} in {city.name}", city.location, player).send();
		}
		return true;
	}
}

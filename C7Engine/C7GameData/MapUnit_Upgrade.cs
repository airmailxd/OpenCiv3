using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;

namespace C7GameData;

public partial class MapUnit {
	// The unit type this unit can upgrade to right now, or null. Units upgrade
	// in their owner's cities: land units need barracks, ships a harbor and
	// aircraft an airport ("A city with an [Airport] can be used to upgrade
	// air units", Conquests Civilopedia, BLDG_Airport). The new type must be
	// one the city could build.
	public UnitPrototype GetAvailableUpgrade() {
		if (!unitType.actions.Contains(UnitAction.Upgrade)) {
			return null;
		}

		City city = location?.cityAtTile;
		if (!City.IsValidCity(city) || city.owner != owner) {
			return null;
		}

		if (!HasVeteranBuildingFor(city)) {
			return null;
		}

		HashSet<Resource> resources = city.GetAvailableResources(EngineStorage.gameData).Keys.ToHashSet();
		return unitType.GetProducibleUpgrade(city, resources);
	}

	// Whether the city has (built, or granted by a wonder) the building that
	// trains this kind of unit: barracks for land units, a harbor for ships,
	// an airport for aircraft.
	private bool HasVeteranBuildingFor(City city) {
		bool land = IsLandUnit();
		bool water = IsWaterUnit();
		bool air = IsAirUnit();
		foreach (CityBuilding cb in city.EffectiveBuildings()) {
			if ((land && cb.building.providesVeteranGroundUnits)
				|| (water && cb.building.providesVeteranSeaUnits)
				|| (air && cb.building.providesVeteranAirUnits)) {
				return true;
			}
		}
		return false;
	}

	// The gold needed to upgrade to the given type: a fixed amount per shield
	// of difference in production cost, halved (rounding down) while the owner
	// has an active wonder with cheaper upgrades (Leonardo's Workshop).
	// The amount is the BIQ's (RULE's upgrade cost), 3 in Conquests: "To
	// upgrade a unit, the costs have gone from 2 * shield_difference to 3 *
	// shield_difference" and "Leonardo's Workshop still halves the upgrade
	// cost (to 1.5 * shield_difference)"
	// (https://forums.civfanatics.com/threads/civ3-conquests-additions-changes-list.104294/);
	// "Obsolete military units can be upgraded at half price" (Conquests
	// Civilopedia, BLDG_Inventor's_Workshop, Leonardo's Workshop).
	// UNVERIFIED: that half of an odd cost rounds down.
	public int UpgradeCost(UnitPrototype upgrade) {
		int shieldDifference = owner.ShieldCost(upgrade) - owner.ShieldCost(unitType);
		int cost = Math.Max(0, shieldDifference) * EngineStorage.gameData.rules.UpgradeCostPerShield;
		return HasCheaperUpgrades(owner) ? cost / 2 : cost;
	}

	private static bool HasCheaperUpgrades(Player player) {
		foreach ((City _, CityBuilding cb) in player.GetBuildingSnapshot().activeWonders) {
			if (cb.building.cheaperUpgrades) {
				return true;
			}
		}
		return false;
	}

	public bool CanUpgrade() {
		UnitPrototype upgrade = GetAvailableUpgrade();
		return upgrade != null && owner.gold >= UpgradeCost(upgrade);
	}

	// Upgrades the unit, keeping its experience. Returns whether it upgraded.
	public bool Upgrade() {
		return UpgradeTo(GetAvailableUpgrade());
	}

	// Like Upgrade, for callers that already looked up GetAvailableUpgrade
	// (or an equivalent) and pass its result.
	internal bool UpgradeTo(UnitPrototype upgrade) {
		if (upgrade == null) {
			return false;
		}
		int cost = UpgradeCost(upgrade);
		if (owner.gold < cost) {
			return false;
		}

		log.Information("Upgrading {Unit} to {Upgrade} for {Cost} gold", this, upgrade.name, cost);
		owner.gold -= cost;
		ChangeTypeForUpgrade(upgrade);
		return true;
	}

	// Turns this unit into the given type, keeping its experience, without
	// charging anything.
	private void ChangeTypeForUpgrade(UnitPrototype upgrade) {
		if (name == unitType.name) {
			name = upgrade.name;
		}
		unitType = upgrade;
		TileChangeJournal.Record(location);
		hitPointsRemaining = Math.Min(hitPointsRemaining, maxHitPoints);
	}
}

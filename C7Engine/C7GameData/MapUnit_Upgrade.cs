using System.Collections.Generic;
using System.Linq;
using C7Engine;

namespace C7GameData;

public partial class MapUnit {
	// The unit type this unit can upgrade to right now, or null. Units upgrade
	// in their owner's cities: land units need barracks and ships need a
	// harbor. The new type must be one the city could build.
	public UnitPrototype GetAvailableUpgrade() {
		if (!unitType.actions.Contains(UnitAction.Upgrade)) {
			return null;
		}

		City city = location?.cityAtTile;
		if (!City.IsValidCity(city) || city.owner != owner) {
			return null;
		}

		bool hasUpgradeBuilding = city.GetBuildings().Any(cb =>
			(IsLandUnit() && cb.building.providesVeteranGroundUnits)
			|| (IsWaterUnit() && cb.building.providesVeteranSeaUnits));
		if (!hasUpgradeBuilding) {
			return null;
		}

		HashSet<Resource> resources = EngineStorage.gameData.GetTradeNetwork()
			.GetResourcesAvailableToCity(owner, city).Keys.ToHashSet();
		return unitType.GetProducibleUpgrade(city, resources);
	}

	// The gold needed to upgrade to the given type: a fixed amount per shield
	// of difference in production cost.
	public int UpgradeCost(UnitPrototype upgrade) {
		int shieldDifference = owner.ShieldCost(upgrade) - owner.ShieldCost(unitType);
		return System.Math.Max(0, shieldDifference) * EngineStorage.gameData.rules.UpgradeCostPerShield;
	}

	public bool CanUpgrade() {
		UnitPrototype upgrade = GetAvailableUpgrade();
		return upgrade != null && owner.gold >= UpgradeCost(upgrade);
	}

	// Upgrades the unit, keeping its experience. Returns whether it upgraded.
	public bool Upgrade() {
		UnitPrototype upgrade = GetAvailableUpgrade();
		if (upgrade == null) {
			return false;
		}
		int cost = UpgradeCost(upgrade);
		if (owner.gold < cost) {
			return false;
		}

		log.Information($"Upgrading {this} to {upgrade.name} for {cost} gold");
		owner.gold -= cost;
		if (name == unitType.name) {
			name = upgrade.name;
		}
		unitType = upgrade;
		hitPointsRemaining = System.Math.Min(hitPointsRemaining, maxHitPoints);
		return true;
	}
}

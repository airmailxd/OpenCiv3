using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using Serilog;

namespace C7GameData;

// The per-turn effects of wonders that hand out units: Leonardo's Workshop,
// which upgrades a unit for free each turn, and buildings like the Statue of
// Zeus and Knights Templar, which grant a unit every few turns.
public static class WonderUnits {
	private static readonly ILogger log = Log.ForContext(typeof(WonderUnits));

	// Runs both effects for the player. Called once per player as the turn
	// advances.
	public static void DoPerTurnUpdates(Player player, GameData gameData) {
		if (player.defeated) {
			return;
		}
		ProduceFreeUnits(player, gameData);
		UpgradeOneUnitForFree(player, gameData);
	}

	// Buildings with a unitProduced (the Statue of Zeus, Knights Templar)
	// give their city's owner a unit of that type every unitFrequency turns,
	// as long as the building isn't obsolete for the owner.
	//
	// Assumptions: the count is kept per building (so a captured wonder keeps
	// counting for its new owner), a newly built wonder starts counting on the
	// turn after it is completed (so its first unit comes five turns later),
	// and the unit is made like a normally produced one, so it is a veteran
	// when the city has barracks. Civ3 data has nothing else for these
	// wonders: the Statue of Zeus's unhappiness effect from Civ2 is not part
	// of Conquests' rules.
	public static void ProduceFreeUnits(Player player, GameData gameData) {
		foreach (City city in player.cities.ToArray()) {
			foreach (CityBuilding cb in city.constructed_buildings.ToArray()) {
				Building building = cb.building;
				if (string.IsNullOrEmpty(building?.unitProducedName) || building.unitFrequency <= 0) {
					continue;
				}
				// Checked directly rather than with isGreatWonderObsolete, which
				// only covers great wonders, as any building may produce units.
				if (building.renderedObsoleteBy != null && player.knownTechs?.Contains(building.renderedObsoleteBy.id) == true) {
					continue;
				}

				cb.turnsTowardFreeUnit++;
				if (cb.turnsTowardFreeUnit < building.unitFrequency) {
					continue;
				}

				UnitPrototype proto = gameData.unitPrototypes.Find(p => p.name == building.unitProducedName);
				if (proto == null) {
					log.Warning("{Building} produces unknown unit type {Unit}", building.name, building.unitProducedName);
					continue;
				}

				cb.turnsTowardFreeUnit = 0;
				log.Information("{Building} in {City} produced a free {Unit}", building.name, city, proto.name);
				city.AddUnit(proto, gameData);
				if (player.isHuman) {
					new MsgShowTemporaryPopup($"{building.name} in {city.name} has produced a free {proto.name}.", city.location, player).send();
				}
			}
		}
	}

	// The player's active wonder that upgrades a unit per turn, or null.
	public static Building FreeUpgradeWonder(Player player) {
		foreach ((City _, CityBuilding cb) in player.GetBuildingSnapshot().activeWonders) {
			if (cb.building.cheaperUpgrades) {
				return cb.building;
			}
		}
		return null;
	}

	// Leonardo's Workshop: each turn, one of the owner's units that has an
	// upgrade the owner can build is upgraded at no cost, keeping its
	// experience. Unlike a paid upgrade, the unit doesn't need to be in a
	// city with barracks or a harbor.
	//
	// Assumptions: the new type must be buildable by the owner, including any
	// strategic resources it needs. Those are taken from the unit's own city
	// when it is in one, otherwise from the capital (any coastal city for a
	// ship). The first eligible unit in the owner's unit list is upgraded.
	// Returns the upgraded unit, or null.
	public static MapUnit UpgradeOneUnitForFree(Player player, GameData gameData) {
		if (player.cities.Count == 0) {
			return null;
		}
		Building wonder = FreeUpgradeWonder(player);
		if (wonder == null) {
			return null;
		}

		Dictionary<City, HashSet<Resource>> resourcesByCity = new();
		HashSet<Resource> ResourcesOf(City c) {
			if (!resourcesByCity.TryGetValue(c, out HashSet<Resource> resources)) {
				resources = c.GetAvailableResources(gameData).Keys.ToHashSet();
				resourcesByCity[c] = resources;
			}
			return resources;
		}

		City capital = player.cities.Find(c => c.IsCapital()) ?? player.cities[0];
		foreach (MapUnit unit in player.units.ToArray()) {
			if (!unit.unitType.actions.Contains(UnitAction.Upgrade) || unit.unitType.upgradesTo.Count == 0) {
				continue;
			}

			UnitPrototype upgrade = null;
			foreach (City c in CitiesToUpgradeFrom(unit, player, capital)) {
				upgrade = unit.unitType.GetProducibleUpgrade(c, ResourcesOf(c));
				if (upgrade != null) {
					break;
				}
			}
			if (upgrade == null) {
				continue;
			}

			string oldType = unit.unitType.name;
			log.Information("{Wonder} upgrades {Unit} to {Upgrade}", wonder.name, unit, upgrade.name);
			unit.ChangeTypeForUpgrade(upgrade);
			if (player.isHuman) {
				new MsgShowTemporaryPopup($"{wonder.name} has upgraded a {oldType} to a {upgrade.name}.", unit.location, player).send();
			}
			return unit;
		}
		return null;
	}

	private static IEnumerable<City> CitiesToUpgradeFrom(MapUnit unit, Player player, City capital) {
		City here = unit.location?.cityAtTile;
		if (City.IsValidCity(here) && here.owner == player) {
			yield return here;
		}
		if (here != capital) {
			yield return capital;
		}
		// Ships can only be built on the coast, so a landlocked capital can't
		// say what a ship upgrades to.
		if (unit.IsWaterUnit() && !capital.location.NeighborsWater()) {
			City coastal = player.cities.Find(c => c.location.NeighborsWater());
			if (coastal != null && coastal != here) {
				yield return coastal;
			}
		}
	}
}

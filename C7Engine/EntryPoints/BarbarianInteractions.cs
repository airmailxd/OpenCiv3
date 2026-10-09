using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

public class BarbarianInteractions {
	// Barbarians with nothing to do stay in their camp, so stop spawning at a
	// camp once it holds this many units, rather than piling them up forever.
	// TODO: Make configurable
	internal const int MaxUnitsPerCamp = 3;

	// The gold a civ earns for dispersing a barbarian camp.
	// TODO: make this configurable
	public const int CampDispersalGold = 25;

	// Removes the barbarian camp on the tile, if any, and pays the player who
	// dispersed it. Returns whether there was a camp to disperse.
	public static bool DisperseCamp(GameData gameData, Tile tile, Player player) {
		if (!tile.hasBarbarianCamp || player.isBarbarians) {
			return false;
		}
		gameData.map.barbarianCamps.Remove(tile);
		tile.hasBarbarianCamp = false;
		player.gold += CampDispersalGold;
		return true;
	}

	// Barbarian camps can't stand within a civ's borders, so any that have
	// come to be inside them, by borders growing with culture or a city
	// changing hands, are dispersed. As when a new city's borders take in a
	// camp, the owner of the borders is paid for each one. Call this after
	// updating tile owners. (Founding a city does its own dispersal, to name
	// the city in its message.)
	public static void DisperseCampsWithinBorders(GameData gameData) {
		Dictionary<Player, int> dispersedBy = new();
		foreach (Tile camp in gameData.map.barbarianCamps.ToList()) {
			Player owner = camp.owningCity?.owner;
			if (owner == null || !DisperseCamp(gameData, camp, owner)) {
				continue;
			}
			dispersedBy.TryGetValue(owner, out int count);
			dispersedBy[owner] = count + 1;
		}
		foreach ((Player owner, int count) in dispersedBy) {
			if (owner.IsToldNews) {
				string camps = count == 1 ? "a barbarian encampment" : $"{count} barbarian encampments";
				new MsgShowMilitaryAdvisorPopup(owner, $"Our borders have taken in {camps}, dispersing them and earning {count * CampDispersalGold} gold!", happy: true).send();
			}
		}
	}

	public static int SpawnBarbarians(GameData gameData) {
		Player barbPlayer = gameData.players.Find(player => player.isBarbarians);
		var activity = gameData.barbarianInfo.barbarianActivity;

		if (activity == BarbarianActivity.None)
			return 0;

		// Each camp has a chance to spawn a unit each turn, set by the
		// barbarian activity level, and held back while civs are still
		// founding their second city.
		var spawnRate = DetermineSpawnRate(activity) * SettledCivShare(gameData);
		if (spawnRate <= 0) {
			return 0;
		}
		int barbariansSpawned = 0;
		bool advancedAllowed = AdvancedBarbariansAvailable(gameData);
		foreach (Tile camp in gameData.map.barbarianCamps.ToList()) {
			if (camp.unitsOnTile.Count >= MaxUnitsPerCamp) {
				continue;
			}
			if (GameData.rng.NextDouble() >= spawnRate) {
				continue;
			}

			UnitPrototype unitType = SelectBarbarianUnitType(gameData.barbarianInfo, camp, advancedAllowed);
			Tile tile = SelectSpawnTile(barbPlayer, camp, unitType);
			if (tile != null) {
				gameData.SpawnUnit(barbPlayer, unitType, tile);
				++barbariansSpawned;
			}
		}

		return barbariansSpawned;
	}

	// The share of civs still in the game that have at least two cities.
	// Barbarians spawn at their full rate once every civ has, and not at all
	// until one has, so they don't overrun civs that are just starting out.
	public static float SettledCivShare(GameData gameData) {
		int civs = 0, settled = 0;
		foreach (Player player in gameData.players) {
			if (player.isBarbarians || player.defeated || !player.isIncludedInGame) {
				continue;
			}
			++civs;
			if (player.cities.Count >= 2) {
				++settled;
			}
		}
		return civs == 0 ? 1 : (float)settled / civs;
	}

	/// <summary>
	/// Apply barbarian activity level to barbarian unit spawn rate. Currently NOT based on Civ3 values.
	/// TODO: Make configurable
	/// TODO: Determine what these values are in Civ3 (could be a constant across all) 
	/// </summary>
	private static float DetermineSpawnRate(BarbarianActivity activity) {
		switch (activity) {
			case BarbarianActivity.None:
				return 0;
			case BarbarianActivity.Sedentary:
				return 0.03f;
			case BarbarianActivity.Roaming:
				return 0.05f;
			case BarbarianActivity.Restless:
				return 0.08f;
			case BarbarianActivity.Raging:
				return 0.12f;
			default:
				throw new ArgumentOutOfRangeException(nameof(activity), activity, null);
		}

	}

	// The tech that lets barbarians field their advanced unit (normally
	// horsemen), and how many civs must know it first: "once two civs know
	// horseback riding, Barb camps can spawn horsemen" (Padma,
	// https://forums.civfanatics.com/threads/barbarian-horsemen.50956/).
	public const string AdvancedBarbarianTechName = "Horseback Riding";
	public const int CivsKnowingTechForAdvancedBarbarians = 2;

	// Whether barbarians may field their advanced unit yet: once two civs in
	// the game know Horseback Riding. A ruleset without that tech has
	// nothing to gate them on, so they may.
	public static bool AdvancedBarbariansAvailable(GameData gameData) {
		Tech horsebackRiding = gameData.techs?.Find(t => string.Equals(t.Name, AdvancedBarbarianTechName, StringComparison.OrdinalIgnoreCase));
		if (horsebackRiding == null) {
			return true;
		}
		int civsKnowing = 0;
		foreach (Player player in gameData.players) {
			if (!player.isBarbarians && player.knownTechs.Contains(horsebackRiding.id)) {
				++civsKnowing;
			}
		}
		return civsKnowing >= CivsKnowingTechForAdvancedBarbarians;
	}

	public static UnitPrototype SelectBarbarianUnitType(BarbarianInfo barbInfo, Tile tile, bool advancedAllowed = true) {
		// Coastal camps have a 20% chance of spawning a sea unit
		if (tile.NeighborsWater() && GameData.rng.Next(100) < 20) {
			return barbInfo.barbarianSeaUnitProto;
		}

		// Land units are generated in a 1:3 ratio, one advanced unit for every three basic barbarians.
		// UNVERIFIED (no Civ3 source found): Civ3 uses only the RULE's basic and advanced barbarian
		// and barbarian sea unit (https://forums.civfanatics.com/threads/barbarians-best-way-to-upgrade.47200/),
		// but no source gives a ratio between them.
		bool advanced = GameData.rng.Next(100) < 25;
		return advanced && advancedAllowed && barbInfo.advancedBarbarian != null ? barbInfo.advancedBarbarian : barbInfo.basicBarbarian;
	}

	public static Tile SelectSpawnTile(Player player, Tile camp, UnitPrototype unitType) {
		// Spawn land units at the camp
		if (unitType.IsLandUnit())
			return camp;

		// Spawn sea units on a coast tile, but not in a lake or on a tile occupied by another player
		bool CanSpawnSeaUnits(Tile t) =>
			t.IsCoast() && !t.isFreshWater && t.unitsOnTile.TrueForAll(u => u.owner == player);

		if (unitType.IsSeaUnit()) {
			// Check first two ranks around camp for a suitable tile, or bail with a null 
			return camp.FindInRing(1, CanSpawnSeaUnits)
				?? camp.FindInRing(2, CanSpawnSeaUnits);
		}

		// Default to camp 
		return camp;
	}
}

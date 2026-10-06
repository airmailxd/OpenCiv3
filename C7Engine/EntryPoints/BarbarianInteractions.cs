using System;
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

	public static int SpawnBarbarians(GameData gameData) {
		Player barbPlayer = gameData.players.Find(player => player.isBarbarians);
		var activity = gameData.barbarianInfo.barbarianActivity;

		if (activity == BarbarianActivity.None)
			return 0;

		// Each camp has a chance to spawn a unit each turn, set by the
		// barbarian activity level.
		var spawnRate = DetermineSpawnRate(activity);
		int barbariansSpawned = 0;
		foreach (Tile camp in gameData.map.barbarianCamps.ToList()) {
			if (camp.unitsOnTile.Count >= MaxUnitsPerCamp) {
				continue;
			}
			if (GameData.rng.NextDouble() >= spawnRate) {
				continue;
			}

			UnitPrototype unitType = SelectBarbarianUnitType(gameData.barbarianInfo, camp);
			Tile tile = SelectSpawnTile(barbPlayer, camp, unitType);
			if (tile != null) {
				gameData.SpawnUnit(barbPlayer, unitType, tile);
				++barbariansSpawned;
			}
		}

		return barbariansSpawned;
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

	public static UnitPrototype SelectBarbarianUnitType(BarbarianInfo barbInfo, Tile tile) {
		// Coastal camps have a 20% chance of spawning a sea unit
		if (tile.NeighborsWater() && GameData.rng.Next(100) < 20) {
			return barbInfo.barbarianSeaUnitProto;
		}

		// Land units are generated in a 3:1 ratio, three advanced units for every basic barbarian
		return GameData.rng.Next(100) < 25 ? barbInfo.advancedBarbarian : barbInfo.basicBarbarian;
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

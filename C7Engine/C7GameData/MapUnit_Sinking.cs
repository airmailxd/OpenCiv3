using System.Collections.Generic;
using System.Linq;
using C7Engine;

namespace C7GameData;

// Ships that sail into water their civ hasn't yet learned to sail, like the
// Sea before Astronomy or the Ocean before Magnetism, may sink if they end
// their turn there.
public partial class MapUnit {
	// The chance a ship in unsafe water sinks at the end of its owner's turn,
	// the same for Sea and Ocean tiles. Seafaring civs' ships are hardier.
	// TODO: make this configurable
	public const double UnsafeWaterSinkChance = 0.5;
	public const double SeafaringUnsafeWaterSinkChance = 0.25;

	public static double SinkChanceFor(Player player) {
		return player.civilization?.traits.Contains(Civilization.Trait.Seafaring) == true
			? SeafaringUnsafeWaterSinkChance
			: UnsafeWaterSinkChance;
	}

	public bool IsInUnsafeWater() {
		return Tile.IsTileValid(location) && !IsLoaded() && IsUnsafeWater(location);
	}

	// Whether a path should steer clear of the tile. Paths keep ships out of
	// unsafe water, so that neither the AI nor a goto risks a ship without
	// being told to. A human may still choose unsafe water as a goto's
	// destination, as they may by moving there one step at a time, and
	// tiles they haven't explored are assumed safe, as they're assumed
	// passable.
	//
	// A ship already in unsafe water, say because the Great Lighthouse was
	// lost or it was withdrawn there, would otherwise have no path out and
	// sit there risking the roll every turn, so its paths may cross unsafe
	// water to get back to safety.
	internal bool PathAvoids(Tile tile, Tile destination) {
		if (!IsUnsafeWater(tile) || IsInUnsafeWater()) {
			return false;
		}
		if (owner.PathsByExploredMap && (tile == destination || !owner.HasExploredTile(tile))) {
			return false;
		}
		return true;
	}

	// Rolls for each of the player's ships ending the turn in unsafe water,
	// sinking those that lose along with anything they carry. Humans are told
	// what they lost. Returns the ships sunk.
	internal static List<MapUnit> SinkShipsInUnsafeWater(GameData gameData, Player player) {
		List<MapUnit> sunk = new();
		List<string> losses = new();
		double chance = SinkChanceFor(player);
		foreach (MapUnit ship in player.units.Where(u => u.IsInUnsafeWater()).ToList()) {
			if (GameData.rng.NextDouble() >= chance) {
				continue;
			}
			int lostAboard = ship.Passengers().Count;
			losses.Add(lostAboard switch {
				0 => $"our {ship.unitType.name}",
				1 => $"our {ship.unitType.name} and the unit aboard",
				_ => $"our {ship.unitType.name} and the {lostAboard} units aboard",
			});
			gameData.RemoveUnit(ship);
			sunk.Add(ship);
		}

		if (player.isHuman && losses.Count > 0 && !player.defeated) {
			string lost = losses.Count == 1 ? losses[0] : string.Join(", ", losses.SkipLast(1)) + " and " + losses[^1];
			new MsgShowMilitaryAdvisorPopup(player, $"Rough waters have sunk {lost}! Ships out of safe waters risk sinking at the end of each turn.", happy: false).send();
		}
		return sunk;
	}
}

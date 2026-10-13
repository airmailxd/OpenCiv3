using System.Collections.Generic;
using System.Threading.Tasks;

namespace C7GameData {
	// Coastal fortresses: "The fortress automatically bombards passing enemy
	// ships" (Conquests Civilopedia, BLDG_Coastal_Fortress). "It can only
	// shoot at units passing from one tile in its range to another (à la ZOC
	// attacks)" (The Last Conformist,
	// https://forums.civfanatics.com/threads/flak-mobile-sam-sam-battery-coastal-fortress.72196/):
	// "The CF Zone of Control can/will only activate if a ship moves from one
	// of the 8 (Coast) tiles directly adjacent to the CF-town, to another of
	// those 8 tiles", one shot each such move, and "the shot will not always
	// hit"; a hit takes one hit point
	// (https://forums.civfanatics.com/threads/coastal-forts-and-bombardment.690690/).
	// UNVERIFIED (no Civ3 source found): the shot hits with the BIQ's naval
	// power (8 for the Coastal Fortress) against the ship's bombard defence,
	// A/(A+D), and, like a zone of control attack, never sinks the ship.
	public partial class MapUnit {
		// The enemy cities with a coastal fortress that this ship passes as
		// it moves from one tile to the next: those next to both.
		public List<City> FindCoastalFortressesAlong(Tile from, Tile to) {
			List<City> result = new();
			if (!IsWaterUnit()) {
				return result;
			}
			foreach (Tile t in from.neighbors.Values) {
				if (t == to || !t.HasCity() || !to.neighbors.ContainsValue(t)) {
					continue;
				}
				City city = t.cityAtTile;
				if (city.owner != owner && !owner.IsAtPeaceWith(city.owner) && CoastalFortressPower(city) > 0) {
					result.Add(city);
				}
			}
			return result;
		}

		// The strongest naval power among the city's buildings, or 0.
		public static int CoastalFortressPower(City city) {
			int best = 0;
			foreach (CityBuilding cb in city.EffectiveBuildings()) {
				if (cb.building.navalPower > best) {
					best = cb.building.navalPower;
				}
			}
			return best;
		}

		// The city's coastal fortress fires once at this ship. Returns
		// whether it hit.
		public async Task<bool> TakeCoastalFortressShot(City city) {
			if (hitPointsRemaining <= 1) {
				return false;
			}
			double attack = CoastalFortressPower(city);
			double defence = StrengthVersus(null, CombatRole.BombardDefense, null);
			if (attack <= 0 || GameData.rng.NextDouble() >= attack / (attack + defence)) {
				await location.AnimateAsync(AnimatedEffect.WaterMiss);
				return false;
			}
			hitPointsRemaining -= 1;
			await location.AnimateAsync(AnimatedEffect.Hit);
			log.Information("The coastal fortress of {City} hit {Unit} sailing past", city.name, this);
			return true;
		}
	}
}

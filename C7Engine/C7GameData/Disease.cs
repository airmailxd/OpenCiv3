using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine {
	// Civ3's disease from unhealthy terrain.
	//
	// - "Disease kills citizens and units that spend too much time in
	//   unhealthy areas like floodplains, jungles and marsh... Your workers
	//   can clear jungles or marsh and eliminate that risk. Floodplains
	//   continue to be risky until you discover sanitation" (Conquests
	//   Civilopedia, GCON_Disease); Sanitation: "Disease from Floodplains is
	//   eliminated" (TECH_Sanitation).
	// - Which terrains, and which a tech cures, are the BIQ's terrain flags,
	//   and the tech is the one with the "disables diseases from flood
	//   plains" flag (Sanitation in conquests.biq): "'Disables Diseases from
	//   Flood Plains' is a flag you select, and Sanitation is the tech that
	//   has it"
	//   (https://forums.civfanatics.com/threads/what-is-the-probability-of-dying-of-disease.40716/).
	//   Departure: C3X documents that the Conquests executable checks for
	//   tech number 8 (Writing) instead of the flag, so in the shipped game
	//   flood plain disease stops with Writing
	//   (https://github.com/maxpetul/C3X/blob/master/default.c3x_config.ini,
	//   patch_disease_stopping_tech_flag_bug). The Civilopedia's rule is
	//   followed here, not the bug.
	// - Only citizens die of flood plain disease ("only citizens, not units,
	//   die from flood plain disease", the thread above), and each strike
	//   costs the city one citizen: "Cities near Jungle and Flood Plain
	//   terrain squares suffer a chance of being beset by disease, losing 1
	//   population point each time this occurs"
	//   (https://civilization.fandom.com/wiki/Flood_Plain_(Civ3), via search
	//   results; the page itself is behind a captcha).
	// - Neither an Aqueduct nor a Hospital prevents it: the Conquests
	//   Civilopedia's game text for them says only that they let a city grow
	//   past size 6 and 12 (the Aqueduct's history text mentions
	//   "water-borne diseases" but gives no game effect).
	// - The message is Civ3's (Conquests/Text/Script.txt): SUMMARY_DISEASE,
	//   "Disease strikes $CITY0."
	//
	// UNVERIFIED (simplest consistent behaviour):
	// - The chance. Each turn, a city is struck with a chance in a thousand
	//   of the sum of the disease strengths (50 for every terrain in
	//   conquests.biq) of the unhealthy tiles it works, its own tile
	//   included: 5% a turn per tile. Players report "3 to 4 deaths per 100
	//   citizens working flood plains per turn" (the thread above), and that
	//   cities built on flood plains get disease even when no citizen works
	//   one (https://forums.civfanatics.com/threads/disease-from-flood-plain.102771/,
	//   https://sullla.com/Civ3/strat7.html), which the city's own tile
	//   accounts for. At most one citizen is lost a turn.
	// - Which citizen: the last one working an unhealthy tile, or the last
	//   one if none does. A size 1 city doesn't shrink, as with starvation.
	// - Units fortified in jungle dying of disease (Script.txt's
	//   UNITJUNGLEDISEASE, "Our $UNIT0 has died of disease from the
	//   jungle!") isn't modelled, nor is the scenario plague (GCON_Plague).
	public static class Disease {
		private static readonly ILogger log = Log.ForContext(typeof(Disease));

		// The most a city's chance can be, in thousandths.
		private const int Certain = 1000;

		// Whether the terrain can give the player's cities disease.
		public static bool IsUnhealthy(TerrainType terrain, Player owner) {
			if (terrain == null || !terrain.causesDisease || terrain.diseaseStrength <= 0) {
				return false;
			}
			return !(terrain.diseaseCanBeCured && owner != null && owner.HasTechEnabling(SaveTech.Flag.DisablesDiseases));
		}

		private static bool IsUnhealthy(Tile tile, Player owner) {
			return tile != null && tile != Tile.NONE && IsUnhealthy(tile.overlayTerrainType, owner);
		}

		// The city's chance of disease this turn, in thousandths.
		public static int Chance(City city) {
			int chance = 0;
			if (IsUnhealthy(city.location, city.owner)) {
				chance += city.location.overlayTerrainType.diseaseStrength;
			}
			foreach (CityResident r in city.residents) {
				if (r.tileWorked != city.location && IsUnhealthy(r.tileWorked, city.owner)) {
					chance += r.tileWorked.overlayTerrainType.diseaseStrength;
				}
			}
			return System.Math.Min(chance, Certain);
		}

		// Called once a turn for each city: disease may strike it, costing
		// it a citizen. Returns whether it did.
		public static bool MaybeStrike(City city) {
			if (city.residents.Count <= 1) {
				return false;
			}
			int chance = Chance(city);
			// No draw without a chance, so healthy cities leave the random
			// numbers alone.
			if (chance <= 0 || GameData.rng.Next(Certain) >= chance) {
				return false;
			}
			Strike(city);
			return true;
		}

		// Disease strikes the city: it loses a citizen, and its owner is
		// told.
		public static void Strike(City city) {
			if (city.residents.Count <= 1) {
				return;
			}
			CityResident victim = city.residents[city.residents.Count - 1];
			for (int i = city.residents.Count - 1; i >= 0; --i) {
				CityResident r = city.residents[i];
				if (r.tileWorked != city.location && IsUnhealthy(r.tileWorked, city.owner)) {
					victim = r;
					break;
				}
			}
			city.RemoveCitizen(victim);
			log.Information("Disease strikes {City}, now size {Size}", city.name, city.residents.Count);
			if (city.owner != null && city.owner.IsToldNews) {
				new MsgShowDomesticAdvisorPopup(city.owner, $"Disease strikes {city.name}.").send();
			}
		}
	}
}

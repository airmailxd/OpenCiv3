using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine {
	// Civ3's disease from unhealthy terrain.
	//
	// - "Disease kills citizens and units that spend too much time in
	//   unhealthy areas like floodplains, jungles and marsh. Military units
	//   that fortify in these terrains are susceptible to disease." (Conquests
	//   Civilopedia, GCON_Disease). Which terrains cause it, and which a tech
	//   cures, are the BIQ's terrain flags (flood plain, jungle and marsh
	//   cause it; only flood plain is curable).
	// - Which tech cures it: the tech with the BIQ flag "Disables Diseases
	//   From Flood Plains", Sanitation in the standard rules, as intended:
	//   "Disease from Floodplains is eliminated, and the effect of Plague is
	//   reduced." (Conquests Civilopedia, TECH_Sanitation); "Floodplains
	//   continue to be risky until you discover sanitation." (GCON_Disease).
	//   The shipped executable has a bug here: "In the base game, the tech
	//   flag "Disables Diseases From Flood Plains" (applied to Sanitation)
	//   does nothing and instead flood plains disease stops once players
	//   discover Writing. This happens because that flag is encoded as
	//   number 8 and, instead of checking if players have a tech with that
	//   flag, the code mistakenly checks if players have tech #8, which is
	//   Writing under the standard rules."
	//   (https://github.com/maxpetul/C3X/blob/master/default.c3x_config.ini,
	//   "DISEASE CURING TECH FLAG BUG"; compendium 07 section 7). The
	//   project owner chose the intended behaviour over the bug (owner's
	//   decision, 2026-10-20).
	// - A city is at risk from the mere presence of unhealthy terrain in its
	//   radius, worked or not: "abstaining from working floodplain tiles
	//   does NOT stop the disease" (morchuflex,
	//   https://forums.civfanatics.com/threads/disease-from-flood-plain.102771/);
	//   "just the presence of disease causing terrain within the city radius
	//   will put that city/town at risk" (HellboundHeathcliff,
	//   https://forums.civfanatics.com/threads/cause-of-plague.120157).
	// - NOT CIV3 (owner's ruling, 2026-10-23, as disease struck far too
	//   often): a strike costs one citizen, and half the time
	//   (SecondStrikePercent) a second the next turn; the city is then
	//   spared for ImmuneTurns turns. In Civ3 a strike costs two citizens over two
	//   turns: a city with at least two citizens "tötet diese stets zwei
	//   Bevölkerungspunkte, soweit möglich" ("always loses two population
	//   points, if possible"), one in each of two consecutive turns (CivWiki,
	//   Krankheit (Civ3), https://www.civ-wiki.de/), and "you always get
	//   exactly two turns of disease in your city, losing 2 citizens"
	//   (morchuflex, above).
	// - Only fortified units die of it: 50 warriors fortified in jungle lost
	//   20 over 510 turns, and "I also ran the test with unfortified (but not
	//   moving) units and none of the 50 warriors had died after 200 turns."
	//   (Brain, https://forums.civfanatics.com/threads/death-by-jungle-fever.113653/,
	//   Conquests). Workers are immune: "Corrected Disease Civilopedia
	//   chapter to reflect workers are immune to disease." (README.txt).
	// - Plague is off in the standard rules (conquests.biq PermitPlagues=0)
	//   and isn't modelled.
	// - The messages are Civ3's (Conquests/Text/Script.txt):
	//   SUMMARY_DISEASE, "Disease strikes $CITY0.", and UNITJUNGLEDISEASE,
	//   "Our $UNIT0 has died of disease from the jungle!" (used for every
	//   unhealthy terrain, as Script.txt has no other).
	//
	// UNVERIFIED (the formulas aren't known):
	// - The city's chance: the strongest disease strength in its radius
	//   (50 for every terrain in conquests.biq) in ten-thousandths a turn,
	//   so 0.5% a turn, half CivWiki's estimate ("auf ca. 1% geschätzt"),
	//   by the owner's ruling above.
	// - A unit's chance: the strength in fifty-thousandths a turn, 1 in 1000
	//   at strength 50, which is Brain's rate (20 deaths in roughly 20,000
	//   unit-turns).
	// - Units fortified in a city aren't struck.
	// - Which citizen dies: the last one working an unhealthy tile, or the
	//   last one if none does. A size 1 city doesn't shrink.
	public static class Disease {
		private static readonly ILogger log = Log.ForContext(typeof(Disease));

		// The most a city's chance can be, in thousandths.
		private const int Certain = 1000;

		// A city's chance a turn is the disease strength divided by this, in
		// thousandths.
		private const int CityStrengthDivisor = 10;

		// How many turns after a strike the city is spared.
		public const int ImmuneTurns = 20;

		// The chance, in percent, that a strike takes a second citizen the
		// next turn.
		public const int SecondStrikePercent = 50;

		// A fortified unit's chance a turn is the disease strength in this
		// many.
		private const int UnitChanceScale = 50000;

		// The tech that cures flood plains: the first with the "Disables
		// Diseases From Flood Plains" flag (see above), or null if the rules
		// have none.
		public static Tech CuringTech(GameData gameData) {
			return gameData?.techs?.Find(t => t.HasFlag(SaveTech.Flag.DisablesDiseases));
		}

		// Whether the terrain can give the player's cities and units disease.
		public static bool IsUnhealthy(TerrainType terrain, Player owner) {
			if (terrain == null || !terrain.causesDisease || terrain.diseaseStrength <= 0) {
				return false;
			}
			if (!terrain.diseaseCanBeCured || owner == null) {
				return true;
			}
			return !owner.HasTechEnabling(SaveTech.Flag.DisablesDiseases);
		}

		private static bool IsUnhealthy(Tile tile, Player owner) {
			return tile != null && tile != Tile.NONE && IsUnhealthy(tile.overlayTerrainType, owner);
		}

		// The city's chance of disease this turn, in thousandths.
		public static int Chance(City city) {
			int strongest = 0;
			int radius = city.owner?.rules?.MaxRankOfWorkableTiles ?? 2;
			foreach (Tile t in city.location.GetTilesWithinRankDistance(radius)) {
				if (IsUnhealthy(t, city.owner)) {
					strongest = System.Math.Max(strongest, t.overlayTerrainType.diseaseStrength);
				}
			}
			return System.Math.Min(strongest / CityStrengthDivisor, Certain);
		}

		// Called once a turn for each city: disease may strike it, costing
		// it a citizen, and perhaps another the next turn. Returns whether it
		// cost a citizen this turn.
		public static bool MaybeStrike(City city, int turn) {
			if (city.diseaseStrikesNextTurn) {
				city.diseaseStrikesNextTurn = false;
				if (city.residents.Count <= 1) {
					return false;
				}
				Strike(city);
				city.lastDiseaseTurn = turn;
				return true;
			}
			if (city.residents.Count <= 1
				|| (city.lastDiseaseTurn >= 0 && turn - city.lastDiseaseTurn <= ImmuneTurns)) {
				return false;
			}
			int chance = Chance(city);
			// No draw without a chance, so healthy cities leave the random
			// numbers alone.
			if (chance <= 0 || GameData.rng.Next(Certain) >= chance) {
				return false;
			}
			Strike(city);
			city.lastDiseaseTurn = turn;
			city.diseaseStrikesNextTurn = GameData.rng.Next(100) < SecondStrikePercent;
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

		// Whether disease can strike the unit where it stands.
		public static bool CanStrikeUnit(MapUnit unit) {
			return unit.isFortified
				&& unit.IsLandUnit()
				&& !unit.unitType.isWorker
				&& unit.location != null && unit.location != Tile.NONE
				&& !unit.location.HasCity()
				&& IsUnhealthy(unit.location, unit.owner);
		}

		// Called once a turn for each player: each of its units fortified on
		// unhealthy terrain may die of disease. Returns the units that died.
		public static List<MapUnit> StrikeFortifiedUnits(Player player) {
			List<MapUnit> dead = new();
			foreach (MapUnit unit in player.units.Where(CanStrikeUnit).ToList()) {
				if (GameData.rng.Next(UnitChanceScale) >= unit.location.overlayTerrainType.diseaseStrength) {
					continue;
				}
				log.Information("{Unit} of {Player} died of disease at {Tile}", unit, player, unit.location);
				dead.Add(unit);
				unit.RemoveFromPlay();
				if (player.IsToldNews) {
					new MsgShowMilitaryAdvisorPopup(player, $"Our {unit.unitType.name} has died of disease from the jungle!", happy: false).send();
				}
			}
			return dead;
		}
	}
}

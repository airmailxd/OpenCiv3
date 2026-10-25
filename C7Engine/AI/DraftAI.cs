using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// When the AI drafts citizens (see City.Draft for the rules). HEURISTIC:
	// a city that may draft (a big one, size 7 or more by the rules) does so
	// while hostile land units that can attack, within ThreatDistance tiles
	// and seen by the AI, outnumber the units defending it, as far as the
	// government's draft limit allows. Drafting costs a citizen and makes
	// the city unhappy for a while, so it is kept for when the city is in
	// danger of falling.
	public static class DraftAI {
		private static readonly ILogger log = Log.ForContext(typeof(DraftAI));

		// HEURISTIC: how near an enemy unit must be for its city to be in
		// danger (as DevelopmentAI's hurrying for defence).
		public const int ThreatDistance = 2;

		// Drafts in each of the player's cities in danger, returning the
		// units drafted.
		public static List<MapUnit> PlayTurn(Player player, GameData gameData) {
			List<MapUnit> drafted = new();
			if (player.isHuman || player.IsIndependent || player.cities.Count == 0
				|| !player.HasTechEnabling(C7GameData.Save.SaveTech.Flag.EnablesConscription)) {
				return drafted;
			}
			bool fogRefreshed = false;
			foreach (City city in player.cities.ToArray()) {
				if (city.residents.Count < City.MinimumPopulationToDraft(player.rules)) {
					continue;
				}
				if (!fogRefreshed) {
					AIFogOfWar.Refresh(player);
					fogRefreshed = true;
				}
				int attackers = Attackers(player, city);
				if (attackers == 0) {
					continue;
				}
				int defenders = Defenders(player, city);
				while (defenders < attackers && city.WhyCannotDraft(gameData) == null) {
					MapUnit unit = city.Draft(gameData);
					if (unit == null) {
						break;
					}
					log.Information("{Player} drafts {Unit} in {City} against {Attackers} attackers", player, unit, city, attackers);
					drafted.Add(unit);
					++defenders;
				}
			}
			return drafted;
		}

		// The hostile land units that can attack, seen near the city.
		private static int Attackers(Player player, City city) {
			int count = 0;
			foreach (Tile t in city.location.GetTilesWithinRankDistance(ThreatDistance)) {
				if (t.unitsOnTile.Count == 0 || !AIFogOfWar.SeesUnitsOn(player, t)) {
					continue;
				}
				foreach (MapUnit u in t.unitsOnTile) {
					if (u.owner != player && u.IsLandUnit() && u.unitType.attack > 0 && !u.owner.IsAtPeaceWith(player)) {
						++count;
					}
				}
			}
			return count;
		}

		private static int Defenders(Player player, City city) {
			int count = 0;
			foreach (MapUnit u in city.location.unitsOnTile) {
				if (u.owner == player && u.CanDefendOnLand()) {
					++count;
				}
			}
			return count;
		}
	}
}

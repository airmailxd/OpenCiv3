using System.Collections.Generic;
using System.Linq;
using C7Engine.Pathing;
using C7GameData;
using Serilog;

namespace C7Engine.AI.UnitAI {
	// A hurt attacker going back to one of our cities to heal before it
	// fights again, rather than attacking at a fraction of its strength or
	// wandering about. It heals where it is if that's safe and no city is
	// near; units don't heal in enemy territory (without Battlefield
	// Medicine), so from there it goes further back. Units marching with a
	// war campaign heal with the campaign (see WarCampaign), and armies
	// look after themselves (see ArmyAI). In AI trials, civs whose hurt
	// attackers healed had about 3 more cities after 200 turns on Pangaea,
	// and did as well on Continents.
	public class RestAI : C7GameData.UnitAI {
		private static ILogger log = Log.ForContext<RestAI>();

		// How far (in tiles) a unit will go back to a city to heal; further
		// away it heals where it is.
		private const int MaxDistanceToCity = 6;

		// How far a unit that can't heal where it is goes back to a city.
		private const int FarthestCity = 12;

		private Tile destination;
		private TilePath path;

		// Whether the unit should stop what it's doing and heal.
		public static bool ShouldRest(MapUnit unit, Player player) {
			return !player.isHuman && !player.isBarbarians && unit.IsLandUnit() && !unit.IsArmy() && unit.CanAttack()
				&& unit.unitType.attack >= unit.unitType.defense && unit.currentAI is not WarCampaign.MemberAI && Hurt(unit);
		}

		// HEURISTIC: below this share of its hit points an attacker stops to
		// heal. A regular unit (3 hit points) that has lost one heals; a
		// veteran or elite can take one more hit first. Its odds in its next
		// fight fall about as fast as its hit points.
		private const double HurtHealth = 0.7;

		private static bool Hurt(MapUnit unit) {
			return unit.hitPointsRemaining < HurtHealth * unit.maxHitPoints;
		}

		// A plan to heal in the nearest of our cities nearby, or where the
		// unit is if it heals there, or null if it can't heal anywhere near.
		public static RestAI Make(MapUnit unit, Player player) {
			RestAI ai = new() { destination = unit.location };
			if (unit.location.cityAtTile?.owner != player) {
				List<Tile> cities = player.cities.Select(c => c.location)
					.Where(t => t.DistanceTo(unit.location) <= MaxDistanceToCity)
					.OrderBy(t => t.DistanceTo(unit.location)).ToList();
				if (cities.Count > 0 && PathingAlgorithmChooser.GetAlgorithm(unit).FindFirstReachable(unit.location, cities, unit, out TilePath p) is int i && i >= 0) {
					ai.destination = cities[i];
					ai.path = p;
				} else if (unit.HealRateAt(unit.location) <= 0 || NextToEnemy(unit, player)) {
					// Units don't heal in enemy territory, nor safely next
					// to an enemy: go further back.
					List<Tile> further = player.cities.Select(c => c.location)
						.Where(t => t.DistanceTo(unit.location) <= FarthestCity)
						.OrderBy(t => t.DistanceTo(unit.location)).ToList();
					TilePath p2 = null;
					int j = further.Count == 0 ? -1 : PathingAlgorithmChooser.GetAlgorithm(unit).FindFirstReachable(unit.location, further, unit, out p2);
					if (j < 0) {
						return unit.HealRateAt(unit.location) > 0 ? ai : null;
					}
					ai.destination = further[j];
					ai.path = p2;
				}
			}
			log.Information("{Unit} going to heal at {Destination}", unit, ai.destination);
			return ai;
		}

		private static bool NextToEnemy(MapUnit unit, Player player) {
			foreach (Tile t in unit.location.neighbors.Values) {
				foreach (MapUnit u in t.unitsOnTile) {
					if (u.owner != player && !u.owner.IsAtPeaceWith(player) && u.CanAttack()) {
						return true;
					}
				}
			}
			return false;
		}

		public C7GameData.UnitAI.MoveResult PlayTurnImpl(Player player, MapUnit unit) {
			if (unit.hitPointsRemaining >= unit.maxHitPoints) {
				return C7GameData.UnitAI.Result.Done;
			}
			if (unit.location != destination && path != null) {
				C7GameData.UnitAI.MoveResult result = this.TryToMoveAlongPath(unit, ref path);
				if (result.Result != C7GameData.UnitAI.Result.Error) {
					return result;
				}
				// Heal where we are instead.
				destination = unit.location;
				path = null;
			}
			// A unit that doesn't move this turn heals at its start.
			unit.SkipTurn();
			return C7GameData.UnitAI.Result.InProgress;
		}

		public string SummarizePlan() {
			return $"RestAI: healing at {destination}";
		}

		public void UpdateOnDeath() { }
	}
}

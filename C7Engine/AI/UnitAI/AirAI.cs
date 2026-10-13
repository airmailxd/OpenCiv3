using System.Collections.Generic;
using System.Threading.Tasks;
using C7Engine.AI;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine {
	// A deliberately simple, conservative AI for aircraft (see MapUnit_Air.cs).
	// Each turn, an aircraft:
	// - away from a base, re-bases to the nearest of our cities it can reach;
	// - if it flies air superiority and enemy units are near one of our cities
	//   within its reach, flies air superiority there (from where it is);
	// - else, if it bombs, bombs the enemy unit, or failing that the enemy
	//   improvement, nearest our cities within its reach;
	// - else stays where it is.
	// Only targets of civs we're already at war with are bombed, so the AI
	// never starts a war this way.
	public static class AirAI {
		// How near one of our cities an enemy has to be to be worth flying
		// at: within a city's work radius.
		public const int ThreatDistance = 3;

		public static async Task PlayTurn(Player player, MapUnit unit) {
			if (unit.hitPointsRemaining <= 0 || !unit.movementPoints.canMove || unit.IsLockedInArmy()) {
				return;
			}
			if (!unit.IsAtAirBase()) {
				Tile home = NearestBase(player, unit);
				if (home != null) {
					unit.Rebase(home);
				}
				return;
			}
			if (!HasEnemies(player)) {
				return;
			}

			AIFogOfWar.Refresh(player);
			if (unit.CanFlyAirSuperiority() && EnemyNearOurCities(player, unit.location, unit.InterceptionRadius())) {
				unit.FlyAirSuperiority();
				return;
			}
			Tile target = ChooseBombingTarget(player, unit);
			if (target != null) {
				await unit.AirBomb(target);
			}
		}

		private static bool HasEnemies(Player player) {
			foreach (Player other in EngineStorage.gameData.players) {
				if (other != player && !other.defeated && AtWar(player, other)) {
					return true;
				}
			}
			return false;
		}

		// The nearest of our cities the aircraft can re-base to, or null.
		private static Tile NearestBase(Player player, MapUnit unit) {
			Tile best = null;
			int bestDistance = int.MaxValue;
			foreach (City city in player.cities) {
				int distance = unit.location.DistanceTo(city.location);
				if (distance < bestDistance && unit.CanRebaseTo(city.location)) {
					best = city.location;
					bestDistance = distance;
				}
			}
			return best;
		}

		// The distance from the tile to the nearest of our cities.
		private static int DistanceToOurCities(Player player, Tile tile) {
			int best = int.MaxValue;
			foreach (City city in player.cities) {
				int d = tile.DistanceTo(city.location);
				if (d < best) {
					best = d;
				}
			}
			return best;
		}

		// Whether an enemy unit we can see is within `reach` of the tile and
		// near one of our cities.
		private static bool EnemyNearOurCities(Player player, Tile from, int reach) {
			foreach (Player enemy in EngineStorage.gameData.players) {
				if (enemy == player || !AtWar(player, enemy)) {
					continue;
				}
				foreach (MapUnit u in enemy.units) {
					Tile t = u.location;
					if (!Tile.IsTileValid(t) || from.DistanceTo(t) > reach || !AIFogOfWar.SeesUnitsOn(player, t)) {
						continue;
					}
					if (DistanceToOurCities(player, t) <= ThreatDistance) {
						return true;
					}
				}
			}
			return false;
		}

		// The enemy unit's tile, or failing any the enemy improvement's, the
		// nearest our cities (and then the nearest the aircraft) that it can
		// bomb, or null if none is near one of our cities.
		internal static Tile ChooseBombingTarget(Player player, MapUnit unit) {
			if (!unit.CanFlyMission(UnitAction.Bombing) || !unit.CanAttackAgainThisTurn()) {
				return null;
			}
			int range = unit.OperationalRange();
			Tile best = null;
			(int, int) bestScore = (int.MaxValue, int.MaxValue);
			void Consider(Tile t) {
				int nearCity = DistanceToOurCities(player, t);
				if (nearCity > ThreatDistance) {
					return;
				}
				(int, int) score = (nearCity, unit.location.DistanceTo(t));
				if (score.CompareTo(bestScore) < 0 && unit.CanAirBombTile(t)) {
					best = t;
					bestScore = score;
				}
			}

			foreach (Player enemy in EngineStorage.gameData.players) {
				if (enemy == player || !AtWar(player, enemy)) {
					continue;
				}
				foreach (MapUnit u in enemy.units) {
					Tile t = u.location;
					if (Tile.IsTileValid(t) && unit.location.DistanceTo(t) <= range && AIFogOfWar.SeesUnitsOn(player, t)
						&& t.FindTopDefenderForBombard(unit) is MapUnit target && target != MapUnit.NONE && AtWar(player, target.owner)) {
						Consider(t);
					}
				}
			}
			if (best != null) {
				return best;
			}

			// No unit to hit: an improvement in enemy territory near our
			// cities, with no city or units of anyone else on it.
			foreach (Tile t in unit.location.GetTilesWithinTileSquare(range)) {
				Player owner = t.OwningPlayer();
				if (owner == null || owner == player || !AtWar(player, owner) || t.HasCity()
					|| !t.overlays.HasBeenImproved() || t.unitsOnTile.Count > 0 || unit.location.DistanceTo(t) > range) {
					continue;
				}
				Consider(t);
			}
			return best;
		}
	}
}

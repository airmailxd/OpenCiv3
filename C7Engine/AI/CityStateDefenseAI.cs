using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine.AI.UnitAI;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// Kings & Nomads' city-states strike back (owner's ruling, 2026-10-25):
	// each turn, before its units' usual moves (which keep them at home, see
	// KingsAndNomadsAI.HoldsCityState), a city-state's bombard units bombard
	// the enemies next to it, and its attackers attack them where the odds
	// are good. See CityStates for what a city-state builds.
	public static class CityStateDefenseAI {
		private static readonly ILogger log = Log.ForContext(typeof(CityStateDefenseAI));

		// HEURISTIC: the least odds of winning an attacker attacks with.
		internal const double MinOdds = 0.5;

		public static async Task PlayTurn(Player player, GameData gameData) {
			if (!player.isCityStates || !CityStateTrade.IsOn(gameData)) {
				return;
			}
			foreach (City city in player.cities.ToList()) {
				await StrikeBack(player, city);
			}
		}

		// A tile in the open with a civ's units on it. City-states don't
		// strike a civ's city, and are at peace with the barbarians.
		private static bool IsEnemyTile(Player player, Tile tile) {
			return tile != Tile.NONE && !tile.HasCity()
				&& tile.unitsOnTile.Any(u => u.owner != null && !u.owner.IsIndependent && AIThreat.IsHostile(player, u.owner));
		}

		private static List<MapUnit> Ours(Player player, Tile home, CityStates.Role role) {
			return home.unitsOnTile.Where(u => u.owner == player && CityStates.RoleOf(u.unitType) == role && u.movementPoints.canMove).ToList();
		}

		internal static async Task StrikeBack(Player player, City city) {
			Tile home = city.location;
			List<Tile> enemies = home.neighbors.Values.Where(t => IsEnemyTile(player, t)).ToList();
			if (enemies.Count == 0) {
				return;
			}

			// Bombard units soften up the biggest stack first.
			foreach (MapUnit bombard in Ours(player, home, CityStates.Role.Bombard)) {
				Tile target = enemies.Where(t => IsEnemyTile(player, t) && bombard.CanBombardTile(t))
					.OrderByDescending(t => t.unitsOnTile.Count).FirstOrDefault();
				if (target != null) {
					log.Debug("{Unit} of {City} bombards {Tile}", bombard, city.name, target);
					await bombard.Bombard(target);
				}
			}

			// Then the attackers attack, each where its odds are best, if good
			// enough.
			foreach (MapUnit attacker in Ours(player, home, CityStates.Role.Attacker)) {
				if (!PlayerAI.IsStillOurs(attacker, player) || !attacker.CanAttackAgainThisTurn()) {
					continue;
				}
				Tile best = null;
				double bestOdds = MinOdds;
				foreach (Tile t in enemies.Where(t => IsEnemyTile(player, t))) {
					MapUnit defender = t.FindTopDefender(attacker);
					double odds = MapUnit.IsMapUnitValid(defender) ? CombatAI.EstimateChanceToWin(attacker, defender) : 1;
					if (odds >= bestOdds) {
						bestOdds = odds;
						best = t;
					}
				}
				if (best == null) {
					continue;
				}
				log.Debug("{Unit} of {City} attacks {Tile} at odds {Odds:0.00}", attacker, city.name, best, bestOdds);
				await attacker.Move(home.DirectionTo(best));
				// A winner that went on into the tile comes home if it can.
				if (PlayerAI.IsStillOurs(attacker, player) && attacker.location != home && attacker.movementPoints.canMove
						&& attacker.location.neighbors.ContainsValue(home)) {
					await attacker.Move(attacker.location.DirectionTo(home));
				}
			}
		}
	}
}

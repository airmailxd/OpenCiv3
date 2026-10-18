using System.Collections.Generic;
using System.Linq;
using C7Engine.AI;
using C7Engine.AI.UnitAI;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine {
	// A deliberately simple AI for airlifts (see MapUnit_Airlift.cs).
	// HEURISTIC, not from Civ3: at the start of its unit moves, the AI
	// looks for cities of its with an airport that enemy units it can see
	// are near, and fewer defenders than those enemies (or fewer than
	// MinDefenders). Each gets a defender airlifted in from another airport
	// city no enemy is near that keeps at least MinDefenders, the strongest
	// such defender first. The defender then defends its new city. Each
	// airport sends one unit a turn, so a threatened city may get several
	// defenders from several airports.
	public static class AirliftAI {
		// How near one of our cities an enemy has to be to threaten it:
		// within its work radius, as for aircraft (see AirAI).
		public const int ThreatDistance = AirAI.ThreatDistance;

		// How many defenders a city keeps, whether or not it's threatened.
		public const int MinDefenders = 2;

		public static void PlayTurn(Player player) {
			List<City> airports = player.cities.Where(c => c.Airport() != null).ToList();
			if (airports.Count < 2 || !HasEnemies(player)) {
				return;
			}
			AIFogOfWar.Refresh(player);

			Dictionary<City, int> threat = new();
			foreach (City city in airports) {
				threat[city] = EnemiesNear(player, city.location);
			}

			// The most threatened cities first, then in the order of the
			// player's cities.
			foreach (City target in airports.Where(c => threat[c] > 0).OrderByDescending(c => threat[c] - Defenders(c).Count)) {
				while (Defenders(target).Count < System.Math.Max(MinDefenders, threat[target])) {
					MapUnit defender = ChooseDefender(airports, threat, target);
					if (defender == null) {
						break;
					}
					defender.Airlift(target.location);
					defender.currentAI = new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(defender, player));
				}
			}
		}

		// The strongest defender that can be airlifted to the target from an
		// unthreatened airport city keeping MinDefenders, or null.
		private static MapUnit ChooseDefender(List<City> airports, Dictionary<City, int> threat, City target) {
			MapUnit best = null;
			foreach (City source in airports) {
				if (source == target || threat[source] > 0) {
					continue;
				}
				List<MapUnit> defenders = Defenders(source);
				if (defenders.Count <= MinDefenders) {
					continue;
				}
				foreach (MapUnit u in defenders) {
					if (u.CanAirliftTo(target.location) && (u.currentAI is null or DefenderAI)
						&& (best == null || u.unitType.defense > best.unitType.defense)) {
						best = u;
					}
				}
			}
			return best;
		}

		// Our land units in the city that can defend it.
		private static List<MapUnit> Defenders(City city) {
			return city.location.unitsOnTile
				.Where(u => u.owner == city.owner && u.IsLandUnit() && u.unitType.defense > 0 && !u.IsLoaded() && !u.unitType.isLeader && u.currentAI is not WarCampaign.MemberAI)
				.ToList();
		}

		private static bool HasEnemies(Player player) {
			foreach (Player other in EngineStorage.gameData.players) {
				if (other != player && !other.defeated && AtWar(player, other)) {
					return true;
				}
			}
			return false;
		}

		// How many enemy land units that can attack we see near the tile.
		private static int EnemiesNear(Player player, Tile tile) {
			int count = 0;
			foreach (Player enemy in EngineStorage.gameData.players) {
				if (enemy == player || !AtWar(player, enemy)) {
					continue;
				}
				foreach (MapUnit u in enemy.units) {
					Tile t = u.location;
					if (Tile.IsTileValid(t) && u.IsLandUnit() && u.unitType.attack > 0
						&& tile.DistanceTo(t) <= ThreatDistance && AIFogOfWar.SeesUnitsOn(player, t)) {
						count++;
					}
				}
			}
			return count;
		}
	}
}

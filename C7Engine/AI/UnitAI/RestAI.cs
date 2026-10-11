using System.Collections.Generic;
using System.Linq;
using C7Engine.Pathing;
using C7GameData;
using Serilog;

namespace C7Engine.AI.UnitAI {
	// A hurt veteran or elite attacker going back to one of our cities to
	// heal before it fights again (on trial, see the "elite-care" switch).
	// Only elite units can produce great leaders, and a unit needs many
	// victories to become one, so it's worth more alive and healthy than
	// spent on one more attack at half strength.
	public class RestAI : C7GameData.UnitAI {
		private static ILogger log = Log.ForContext<RestAI>();

		// How far (in tiles) a unit will go back to a city to heal; further
		// away it heals where it is.
		private const int MaxDistanceToCity = 6;

		private Tile destination;
		private TilePath path;

		public static bool IsEnabled(Player player) {
			return AiExperiments.Has(player, "elite-care");
		}

		// Whether the unit should stop what it's doing and heal.
		public static bool ShouldRest(MapUnit unit, Player player) {
			return IsEnabled(player) && unit.IsLandUnit() && !unit.IsArmy() && unit.CanAttack()
				&& unit.unitType.attack >= unit.unitType.defense
				&& unit.hitPointsRemaining < unit.maxHitPoints && IsVeteranOrBetter(unit);
		}

		private static bool IsVeteranOrBetter(MapUnit unit) {
			GameData gameData = EngineStorage.gameData;
			ExperienceLevel next = gameData.GetExperienceLevelAfter(unit.experienceLevel);
			return next == null || gameData.GetExperienceLevelAfter(next) == null;
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
				} else if (unit.HealRateAt(unit.location) <= 0) {
					return null;
				}
			}
			log.Information("{Unit} going to heal at {Destination}", unit, ai.destination);
			return ai;
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

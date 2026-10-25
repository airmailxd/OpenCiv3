using System.Linq;
using System.Threading.Tasks;
using C7Engine.AI.UnitAI;
using C7Engine.Pathing;
using C7Engine.AI;
using C7GameData;

namespace C7Engine {
	// The turn of Kings & Nomads' City-States player (see
	// Civilization.isCityStates): its cities strike the enemies next to them
	// (CityStateDefenseAI), then its units hold their cities, and any out in
	// the open make for the nearest of them, or hold their ground if they
	// can't reach one.
	public static class CityStatesAI {
		public static async Task PlayTurn(Player player, GameData gameData) {
			if (!player.isCityStates) {
				return;
			}
			await CityStateDefenseAI.PlayTurn(player, gameData);

			foreach (MapUnit unit in player.units.ToList()) {
				await EngineStorage.YieldIfFrameBudgetSpent();
				if (!PlayerAI.IsStillOurs(unit, player) || !unit.movementPoints.canMove) {
					continue;
				}
				if (!KingsAndNomadsAI.HoldsCityState(player, unit) && unit.IsLandUnit()) {
					await GoHome(player, unit);
					if (!PlayerAI.IsStillOurs(unit, player)) {
						continue;
					}
				}
				await ((C7GameData.UnitAI)new DefenderAI(DefenderAI.MakeAiDataForDefendInPlace(unit, player))).PlayTurn(player, unit);
			}
		}

		// Sends a unit out in the open towards the nearest of our cities it
		// can reach by land.
		private static async Task GoHome(Player player, MapUnit unit) {
			foreach (City city in player.cities.Where(c => c.location.continent == unit.location.continent)
					.OrderBy(c => c.location.DistanceTo(unit.location))) {
				TilePath path = PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, city.location, unit);
				if (path != null && path.PathLength() > 0) {
					unit.path = path;
					await unit.MoveAlongPath();
					return;
				}
			}
		}
	}
}

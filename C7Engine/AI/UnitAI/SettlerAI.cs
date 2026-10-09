using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.AI;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.AIData;
using Serilog;

namespace C7Engine {
	public class SettlerAI : UnitAI {
		private static ILogger log = Log.ForContext<SettlerAI>();
		public SettlerAIData data;

		public static SettlerAIData MakeAiData(MapUnit unit, Player player) {
			SettlerAIData settlerAiData = new SettlerAIData();
			settlerAiData.goal = SettlerAIData.SettlerGoal.BUILD_CITY;
			//If it's the starting settler, have it settle in place.  Otherwise, use an AI to find a location.
			if (player.cities.Count == 0 && CanFoundCityHere(unit, player)) {
				settlerAiData.destination = unit.location;
				log.Information("No cities yet!  Set AI for unit to settler AI with destination of " + settlerAiData.destination);
			} else {
				settlerAiData.destination = SettlerLocationAI.FindSettlerLocation(unit.location, player);
				if (settlerAiData.destination == Tile.NONE) {
					//This is possible if all tiles within 4 tiles of a city are either not land, or already claimed
					//by another colonist.  Longer-term, the AI shouldn't be building settlers if that is the case,
					//but right now we'll just spike the football to stop the clock and avoid building immediately next to another city.
					settlerAiData.goal = SettlerAIData.SettlerGoal.JOIN_CITY;
					log.Information($"Set AI for unit {unit.id} of {unit.owner.civilization.name} to JOIN_CITY due to lack of locations to settle");
				} else {
					PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
					settlerAiData.pathToDestination = algorithm.PathFrom(unit.location, settlerAiData.destination, unit);
					log.Information($"Set AI for unit {unit.id} of {unit.owner.civilization.name} to BUILD_CITY with destination of " + settlerAiData.destination);
				}

				// TODO: return the ranked list, so we can check paths here and avoid duplicate calculations.
			}
			return settlerAiData;
		}

		public SettlerAI(SettlerAIData d) {
			data = d;
		}

		public void UpdateOnDeath() {
			// When we're destroyed, clear out our reference to the unit we're
			// being escorted by, and clear our their reference to us.
			if (data.escort != null && data.escort.currentAI is EscortAI escortAi) {
				escortAi.data.unitToEscort = null;
			}
			data.escort = null;
		}

		C7GameData.UnitAI.MoveResult UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			switch (data.goal) {
				case SettlerAIData.SettlerGoal.BUILD_CITY:
					if (IsInvalidCityLocation(data.destination)) {
						log.Information("Seeking new destination for settler " + unit.id + " headed to " + data.destination);
						return C7GameData.UnitAI.Result.Error;
					}

					if (unit.location == data.destination) {
						// The same rules as for a human's settler, and no
						// founding cities inside another civ's borders.
						if (!CanFoundCityHere(unit, player)) {
							log.Information("Settler " + unit.id + " can't build a city at " + data.destination + ", seeking a new destination");
							return C7GameData.UnitAI.Result.Error;
						}
						log.Information("Building city with " + unit);
						//TODO: This should use a message, and the message handler should cause the disbanding to happen.
						CityInteractions.BuildCity(unit.location, player, unit.owner.GetNextCityName());
						unit.RemoveFromPlay();
					} else if (data.escort == null) {
						log.Information($"Settler {unit.id} is waiting for an escort");
						unit.movementPoints.onConsumeAll();
						return C7GameData.UnitAI.Result.InProgress;
					} else {
						C7GameData.UnitAI.MoveResult moveResult = this.TryToMoveAlongPath(unit, ref data.pathToDestination);
						if (moveResult.Result == C7GameData.UnitAI.Result.Error) {
							return FindNewDestination(unit, player);
						}
						return moveResult;
					}
					break;
				case SettlerAIData.SettlerGoal.JOIN_CITY:
					return JoinCity(unit, player);
			}

			return C7GameData.UnitAI.Result.Done;
		}

		// Whether the settler may found a city where it stands.
		private static bool CanFoundCityHere(MapUnit unit, Player player) {
			Player territoryOwner = unit.location.OwningPlayer();
			return unit.canBuildCity() && (territoryOwner == null || territoryOwner == player);
		}

		// With nowhere left to found a city, the settler adds its people to
		// the nearest of our cities with room for them. (Which city to join,
		// and when, is an AI heuristic, not a Civ3 rule.) If none has room, it
		// waits a turn, and looks again for a city site or a city to join.
		private C7GameData.UnitAI.MoveResult JoinCity(MapUnit unit, Player player) {
			City here = unit.location.cityAtTile;
			if (here != null && here.owner == player && HasRoomToJoin(here, PopulationOf(unit), player)) {
				AddPopulation(here, unit);
				return C7GameData.UnitAI.Result.Done;
			}

			City destination = data.destination?.cityAtTile;
			if (destination == null || destination.owner != player || !HasRoomToJoin(destination, PopulationOf(unit), player)
				|| data.pathToDestination == null) {
				List<Tile> candidates = player.cities.Where(c => HasRoomToJoin(c, PopulationOf(unit), player))
					.OrderBy(c => c.location.DistanceTo(unit.location)).Select(c => c.location).ToList();
				PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
				TilePath path = null;
				int index = candidates.Count > 0 ? algorithm.FindFirstReachable(unit.location, candidates, unit, out path) : -1;
				if (index < 0) {
					log.Information($"Settler {unit.id} has no city to join, waiting");
					data.destination = null;
					data.pathToDestination = null;
					unit.movementPoints.onConsumeAll();
					return C7GameData.UnitAI.Result.Done;
				}
				data.destination = candidates[index];
				data.pathToDestination = path;
				log.Information($"Settler {unit.id} heading to join {data.destination.cityAtTile}");
			}

			C7GameData.UnitAI.MoveResult moveResult = this.TryToMoveAlongPath(unit, ref data.pathToDestination);
			if (moveResult.Result == C7GameData.UnitAI.Result.Error) {
				// Look for another way, or another city, next turn.
				data.pathToDestination = null;
				unit.movementPoints.onConsumeAll();
				return C7GameData.UnitAI.Result.InProgress;
			}
			return moveResult;
		}

		// Whether the AI will add the settler's people to the city.
		//
		// The Civ3 rule: a unit can't join a city at size 6 without fresh
		// water or an aqueduct, at size 12 without a hospital, or (since PTW
		// 1.01f) a starving city
		// (https://forums.civfanatics.com/threads/cant-get-settler-to-join-city.68722/).
		// The AI heuristic, not a Civ3 rule, is to stay within the first of
		// those caps even where a city could grow further, to keep this
		// simple; that never joins a city Civ3 would refuse.
		public static bool HasRoomToJoin(City city, int population, Player player) {
			return city.residents.Count + population <= player.rules.MaximumLevel1CitySize
				&& city.FoodGrowthPerTurn() >= 0;
		}

		private static int PopulationOf(MapUnit unit) {
			return Math.Max(1, unit.unitType.populationCost);
		}

		private static void AddPopulation(City city, MapUnit unit) {
			GameData gameData = EngineStorage.gameData;
			CitizenType defaultCitizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
			for (int i = 0; i < PopulationOf(unit); ++i) {
				CityResident resident = new() {
					citizenType = defaultCitizen,
					nationality = unit.owner.civilization,
					city = city
				};
				city.AddCitizen(resident);
				CityTileAssignmentAI.AssignNewCitizenToTile(gameData, resident);
			}
			city.RecalculateCitizenMoods(gameData);
			log.Information($"Settler {unit.id} joined {city}");
			unit.RemoveFromPlay();
		}

		private static bool IsInvalidCityLocation(Tile tile) {
			if (tile.cityAtTile != null) {
				return true;
			}
			foreach (Tile neighbor in tile.neighbors.Values) {
				if (neighbor.cityAtTile != null) {
					return true;
				}
			}
			return false;
		}

		public string SummarizePlan() {
			return "SettlerAI: " + data.ToString();
		}

		// The destination became unreachable (issue #213); pick a new one, or
		// fall back to JOIN_CITY if nothing is left.
		// TODO: prefer path-checking at selection time over exclude-and-repick.
		public C7GameData.UnitAI.MoveResult FindNewDestination(MapUnit unit, Player player) {
			data.unreachableDestinations.Add(data.destination);
			log.Information($"Settler {unit.id} cannot reach {data.destination}, retargeting");

			Tile newDestination = SettlerLocationAI.FindSettlerLocation(unit.location, player, data.unreachableDestinations);
			if (newDestination == Tile.NONE) {
				data.goal = SettlerAIData.SettlerGoal.JOIN_CITY;
				log.Information($"Settler {unit.id} has no reachable destination left, joining a city instead");
			} else {
				data.destination = newDestination;
				PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
				data.pathToDestination = algorithm.PathFrom(unit.location, newDestination, unit);
				log.Information($"Settler {unit.id} retargeting from an unreachable tile to {newDestination}");
			}

			// Consume movement so PlayTurn does not retry the failed move this turn.
			unit.movementPoints.onConsumeAll();
			return C7GameData.UnitAI.Result.InProgress;
		}
	}
}

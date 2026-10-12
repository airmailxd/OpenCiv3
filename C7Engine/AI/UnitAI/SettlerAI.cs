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
					ChooseRoute(unit, player, settlerAiData);
					log.Information($"Set AI for unit {unit.id} of {unit.owner.civilization.name} to BUILD_CITY with destination of " + settlerAiData.destination);
				}

				// TODO: return the ranked list, so we can check paths here and avoid duplicate calculations.
			}
			return settlerAiData;
		}

		// Walks to the destination, or goes by sea when that is quicker by
		// SeaRoutes.TurnsSavedToSail turns or more, or the only way there.
		// (An AI heuristic, not a Civ3 rule.)
		internal static void ChooseRoute(MapUnit unit, Player player, SettlerAIData data) {
			data.port = null;
			data.ferry = null;
			data.landing = null;
			data.waitingSinceTurn = -1;

			PathingAlgorithm algorithm = PathingAlgorithmChooser.GetAlgorithm(unit);
			TilePath walk = algorithm.PathFrom(unit.location, data.destination, unit);
			data.pathToDestination = walk;
			int walkTurns = walk != null && walk.PathLength() > 0
				? walk.PathCost(player, unit.location, unit.MaxMovementPoints(), unit.movementPoints.remaining)
				: int.MaxValue;

			// Sailing can't save enough turns on a short walk.
			if (walkTurns <= SeaRoutes.TurnsSavedToSail) {
				return;
			}
			int maxTurns = walkTurns == int.MaxValue ? int.MaxValue : walkTurns - SeaRoutes.TurnsSavedToSail + 1;
			SeaRoutes.Voyage voyage = SeaRoutes.PlanVoyage(unit, data.destination, maxTurns);
			if (voyage == null) {
				return;
			}
			data.port = voyage.port;
			data.landing = voyage.landing;
			data.pathToDestination = voyage.port.location == unit.location ? null : algorithm.PathFrom(unit.location, voyage.port.location, unit);
			log.Information("Settler {Unit} will sail from {Port} to land at {Landing}, about {Turns} turns rather than {WalkTurns}",
				unit.id, voyage.port, voyage.landing, voyage.turns, walkTurns == int.MaxValue ? "never" : walkTurns);
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
			if (data.ferry?.currentAI is FerryAI ferryAi && ferryAi.data.settler != null) {
				ferryAi.data.settler = null;
			}
			data.ferry = null;
		}

		C7GameData.UnitAI.MoveResult UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			// Aboard a ferry, which does the moving. Whether the site is still
			// open is looked at again ashore.
			if (unit.IsLoaded() && data.port != null) {
				unit.movementPoints.onConsumeAll();
				return C7GameData.UnitAI.Result.InProgress;
			}
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
						return C7GameData.UnitAI.Result.Done;
					}
					// Take a spare unit nearby as an escort; it catches up.
					// With none to spare, a settler going by land sets off
					// alone rather than waiting (owner's decision,
					// 2026-10-12: "if no escort is available, send the
					// settler alone"), while one going by sea still waits
					// in port for a defender to land with it.
					if (data.escort == null && !TryToTakeEscort(unit, player) && data.port != null) {
						log.Information($"Settler {unit.id} is waiting for an escort to sail with");
						unit.movementPoints.onConsumeAll();
						return C7GameData.UnitAI.Result.InProgress;
					}
					if (data.port != null) {
						return GoToPort(unit, player);
					} else {
						C7GameData.UnitAI.MoveResult moveResult = this.TryToMoveAlongPath(unit, ref data.pathToDestination);
						if (moveResult.Result == C7GameData.UnitAI.Result.Error) {
							return FindNewDestination(unit, player);
						}
						return moveResult;
					}
				case SettlerAIData.SettlerGoal.JOIN_CITY:
					return JoinCity(unit, player);
			}

			return C7GameData.UnitAI.Result.Done;
		}

		// How far, in tiles, a settler looks for a spare unit to escort it.
		private const int ESCORT_SEARCH_DISTANCE = 3;

		// Makes the nearest of our spare soldiers within
		// ESCORT_SEARCH_DISTANCE tiles, on the settler's landmass, the
		// settler's escort. A soldier is spare if it's exploring or
		// defending, but not if it's the only defender of a city.
		internal bool TryToTakeEscort(MapUnit unit, Player player) {
			MapUnit best = null;
			int bestDistance = int.MaxValue;
			foreach (MapUnit u in player.units) {
				if (u == unit || u.hitPointsRemaining <= 0 || u.IsLoaded() || u.IsLeader() || u.IsArmy()
						|| !u.CanDefendOnLand() || !DevelopmentAI.IsMilitaryLandUnit(u.unitType)
						|| !(u.currentAI == null || u.currentAI is C7Engine.AI.UnitAI.DefenderAI || u.currentAI is ExplorerAI)
						|| u.location.continent != unit.location.continent) {
					continue;
				}
				int distance = u.location.DistanceTo(unit.location);
				if (distance > ESCORT_SEARCH_DISTANCE || distance >= bestDistance) {
					continue;
				}
				if (u.location.cityAtTile != null && !u.location.unitsOnTile.Any(o => o != u && o.owner == player && o.CanDefendOnLand())) {
					continue;
				}
				best = u;
				bestDistance = distance;
			}
			if (best == null) {
				return false;
			}
			best.Wake();
			best.currentAI = new EscortAI(new EscortAIData { unitToEscort = unit });
			data.escort = best;
			log.Information($"Settler {unit.id} takes {best.id} {best} as its escort");
			return true;
		}

		// Going by sea: walk to the port, and wait there for a transport,
		// which takes the settler and its escort aboard and puts them ashore
		// (see FerryAI).
		private C7GameData.UnitAI.MoveResult GoToPort(MapUnit unit, Player player) {
			if (data.port.owner != player) {
				return GiveUpSailing(unit, player);
			}

			Tile port = data.port.location;
			if (unit.location != port) {
				data.waitingSinceTurn = -1;
				C7GameData.UnitAI.MoveResult moveResult = this.TryToMoveAlongPath(unit, ref data.pathToDestination);
				if (moveResult.Result == C7GameData.UnitAI.Result.Error) {
					return GiveUpSailing(unit, player);
				}
				return moveResult;
			}

			int turn = EngineStorage.gameData.turn;
			if (data.waitingSinceTurn < 0) {
				data.waitingSinceTurn = turn;
			} else if (turn - data.waitingSinceTurn > SeaRoutes.MaxTurnsWaitingForTransport) {
				log.Information($"Settler {unit.id} waited too long for a transport at {data.port}");
				return GiveUpSailing(unit, player);
			}

			// Take one of our idle transports if there is one; otherwise
			// the port builds one (see ChooseProducible).
			if (!FerryAI.HasLiveFerry(data, player)) {
				data.ferry = null;
				MapUnit ship = SeaRoutes.TransportsReaching(player, port).FirstOrDefault();
				if (ship != null) {
					data.ferry = ship;
					ship.currentAI = new FerryAI(new FerryAIData { settler = unit });
					log.Information($"Settler {unit.id} takes {ship} to sail from {data.port}");
				}
			}
			unit.movementPoints.onConsumeAll();
			return C7GameData.UnitAI.Result.InProgress;
		}

		// The voyage is off: walk if we can, or find somewhere else.
		private C7GameData.UnitAI.MoveResult GiveUpSailing(MapUnit unit, Player player) {
			if (data.ferry?.currentAI is FerryAI ferryAi) {
				ferryAi.data.settler = null;
			}
			data.port = null;
			data.ferry = null;
			data.landing = null;
			data.waitingSinceTurn = -1;
			data.pathToDestination = PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, data.destination, unit);
			if (data.pathToDestination == null || data.pathToDestination.PathLength() <= 0) {
				return FindNewDestination(unit, player);
			}
			return C7GameData.UnitAI.Result.InProgress;
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
				ChooseRoute(unit, player, data);
				log.Information($"Settler {unit.id} retargeting from an unreachable tile to {newDestination}");
			}

			// Consume movement so PlayTurn does not retry the failed move this turn.
			unit.movementPoints.onConsumeAll();
			return C7GameData.UnitAI.Result.InProgress;
		}
	}
}

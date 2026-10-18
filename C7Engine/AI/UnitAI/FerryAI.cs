using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine.AI;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.AIData;
using Serilog;

namespace C7Engine {
	// Sails a settler and its escort to where they settle (see SeaRoutes):
	// to the settler's port, where both board once they are there, then to
	// the water by the landing tile, where both step ashore. (An AI
	// heuristic, not a Civ3 rule.)
	public class FerryAI : UnitAI {
		private static ILogger log = Log.ForContext<FerryAI>();
		public FerryAIData data;

		public FerryAI(FerryAIData d) {
			data = d;
		}

		// Takes on the voyage of a settler waiting at its port with no
		// transport, the one whose port is nearest, if the ship is a
		// transport. Returns null otherwise.
		public static FerryAIData MaybeMakeAiData(MapUnit ship, Player player) {
			MapUnit settler = SettlerWaitingForTransport(ship, player);
			if (settler == null) {
				return null;
			}
			((SettlerAI)settler.currentAI).data.ferry = ship;
			log.Information("{Ship} will ferry {Settler}", ship, settler);
			return new FerryAIData { settler = settler };
		}

		// The settler waiting for a transport that the ship would take on:
		// the one whose port is nearest, of those it can sail to.
		internal static MapUnit SettlerWaitingForTransport(MapUnit ship, Player player) {
			if (!SeaRoutes.IsTransport(ship)) {
				return null;
			}
			List<MapUnit> waiting = new();
			foreach (MapUnit u in player.units) {
				if (u.currentAI is SettlerAI settlerAi && settlerAi.data.port != null && !HasLiveFerry(settlerAi.data, player)) {
					waiting.Add(u);
				}
			}
			foreach (MapUnit u in waiting.OrderBy(u => ship.location.DistanceTo(((SettlerAI)u.currentAI).data.port.location))) {
				if (SeaRoutes.CanSailTo(ship, ((SettlerAI)u.currentAI).data.port.location)) {
					return u;
				}
			}
			return null;
		}

		// Whether the settler's transport is still ours and still on its way.
		internal static bool HasLiveFerry(SettlerAIData settler, Player player) {
			MapUnit ferry = settler.ferry;
			return ferry != null && PlayerAI.IsStillOurs(ferry, player) && ferry.currentAI is FerryAI ferryAi && ferryAi.data.settler != null;
		}

		public string SummarizePlan() {
			return "FerryAI: " + data;
		}

		public void UpdateOnDeath() {
			if (data.settler?.currentAI is SettlerAI settlerAi && settlerAi.data.ferry != null) {
				settlerAi.data.ferry = null;
			}
			data.settler = null;
		}

		UnitAI.MoveResult UnitAI.PlayTurnImpl(Player player, MapUnit ship) {
			MapUnit settler = data.settler;
			SettlerAIData voyage = (settler != null && PlayerAI.IsStillOurs(settler, player) && settler.currentAI is SettlerAI settlerAi)
				? settlerAi.data : null;
			if (voyage == null || voyage.port == null || voyage.landing == null || voyage.ferry != ship) {
				// The voyage is off. Anyone aboard gets off in a city of ours.
				data.settler = null;
				return ReleasePassengers(ship, player);
			}

			if (!settler.IsLoaded()) {
				return SailToPortAndBoard(player, ship, settler, voyage);
			}
			return SailToLanding(player, ship, settler, voyage);
		}

		private UnitAI.MoveResult SailToPortAndBoard(Player player, MapUnit ship, MapUnit settler, SettlerAIData voyage) {
			Tile port = voyage.port.location;
			if (voyage.port.owner != player) {
				return GiveUp(ship, voyage);
			}
			if (ship.location != port) {
				if (data.path == null || data.path.destination != port) {
					data.path = PathingAlgorithmChooser.GetAlgorithm(ship).PathFrom(ship.location, port, ship);
				}
				UnitAI.MoveResult moved = this.TryToMoveAlongPath(ship, ref data.path);
				return moved.Result == UnitAI.Result.Error ? GiveUp(ship, voyage) : moved;
			}

			// Wait in port for the settler and its escort, and take them both
			// aboard together.
			MapUnit escort = voyage.escort;
			if (settler.location != port || escort == null || escort.location != port || escort.IsLoaded()
				|| !settler.IsLoadable() || !escort.IsLoadable() || ship.FreeCapacity() < 2) {
				ship.movementPoints.onConsumeAll();
				return UnitAI.Result.InProgress;
			}

			data.path = PathToLanding(ship, voyage);
			if (data.path == null) {
				log.Information("{Ship} can't find a way to land {Settler} near {Destination}", ship, settler, voyage.destination);
				return GiveUp(ship, voyage);
			}
			settler.BoardTransport(ship);
			escort.BoardTransport(ship);
			log.Information("{Ship} sails with {Settler} and {Escort} for {Landing}", ship, settler, escort, voyage.landing);
			return UnitAI.Result.InProgress;
		}

		private UnitAI.MoveResult SailToLanding(Player player, MapUnit ship, MapUnit settler, SettlerAIData voyage) {
			if (ship.location.IsWater() && IsNeighbor(ship.location, voyage.landing)) {
				if (!SeaRoutes.CanLandOn(voyage.landing, player)) {
					// Someone is in the way. Land next to them if we can.
					Tile other = ship.location.neighbors.Values.FirstOrDefault(n => n != Tile.NONE && n.IsLand()
						&& n.continent == voyage.destination.continent && SeaRoutes.CanLandOn(n, player));
					if (other == null) {
						ship.movementPoints.onConsumeAll();
						return UnitAI.Result.InProgress;
					}
					voyage.landing = other;
				}
				return UnitAI.MoveResult.MoveRequested(GoAshore(player, ship, settler, voyage));
			}

			if (data.path == null || data.path.PathLength() <= 0) {
				data.path = PathToLanding(ship, voyage);
			}
			UnitAI.MoveResult moved = this.TryToMoveAlongPath(ship, ref data.path);
			if (moved.Result == UnitAI.Result.Error) {
				// The way is blocked: look for another landing next turn.
				data.path = PathToLanding(ship, voyage);
				ship.movementPoints.onConsumeAll();
				return UnitAI.Result.InProgress;
			}
			return moved;
		}

		// Puts the settler and everyone else aboard ashore, and hands the
		// settler back its walk to its site.
		private async Task<bool> GoAshore(Player player, MapUnit ship, MapUnit settler, SettlerAIData voyage) {
			Tile landing = voyage.landing;
			TileDirection direction = ship.location.DirectionTo(landing);
			List<MapUnit> aboard = ship.Passengers();
			// The settler first, so the escort stands with it.
			aboard.Remove(settler);
			aboard.Insert(0, settler);
			foreach (MapUnit passenger in aboard) {
				await passenger.Move(direction);
			}
			if (settler.location != landing) {
				// Still aboard (it couldn't move this turn): try again next turn.
				ship.movementPoints.onConsumeAll();
				return true;
			}
			log.Information("{Ship} landed {Settler} at {Landing}", ship, settler, landing);

			voyage.port = null;
			voyage.ferry = null;
			voyage.landing = null;
			voyage.pathToDestination = PathingAlgorithmChooser.GetAlgorithm(settler).PathFrom(settler.location, voyage.destination, settler);
			data.settler = null;
			ship.movementPoints.onConsumeAll();
			return true;
		}

		// The way to the water next to the landing tile, or if there's none,
		// next to another tile of the landmass near the destination.
		private static TilePath PathToLanding(MapUnit ship, SettlerAIData voyage) {
			Tile destination = voyage.destination;
			List<(Tile water, Tile land)> stops = new();
			foreach (Tile water in voyage.landing.neighbors.Values) {
				if (water != Tile.NONE && water.IsWater()) {
					stops.Add((water, voyage.landing));
				}
			}
			foreach (Tile land in destination.GetTilesWithinRankDistance(3).OrderBy(t => t.DistanceTo(destination))) {
				if (land == voyage.landing || !land.IsLand() || land.continent != destination.continent) {
					continue;
				}
				foreach (Tile water in land.neighbors.Values) {
					if (water != Tile.NONE && water.IsWater()) {
						stops.Add((water, land));
					}
				}
			}
			if (stops.Count == 0) {
				return null;
			}
			List<Tile> waters = stops.Select(s => s.water).ToList();
			int index = PathingAlgorithmChooser.GetAlgorithm(ship).FindFirstReachable(ship.location, waters, ship, out TilePath path);
			if (index < 0) {
				return null;
			}
			voyage.landing = stops[index].land;
			return path;
		}

		// Calls off the voyage: the settler finds another plan.
		private UnitAI.MoveResult GiveUp(MapUnit ship, SettlerAIData voyage) {
			log.Information("{Ship} gives up ferrying to {Destination}", ship, voyage.destination);
			voyage.ferry = null;
			data.settler = null;
			return ReleasePassengers(ship, ship.owner);
		}

		// With nobody left to ferry, the ship is done, but anyone aboard has
		// to get off first: in a city of ours, where they can walk away.
		private UnitAI.MoveResult ReleasePassengers(MapUnit ship, Player player) {
			List<MapUnit> aboard = ship.Passengers();
			if (aboard.Count == 0) {
				return UnitAI.Result.Done;
			}
			if (ship.location.cityAtTile?.owner == player) {
				foreach (MapUnit passenger in aboard) {
					passenger.UnboardTransport(ship);
					passenger.currentAI = null;
				}
				return UnitAI.Result.Done;
			}
			Tile home = NearestReachableCity(ship, player);
			if (home == null) {
				ship.movementPoints.onConsumeAll();
				return UnitAI.Result.InProgress;
			}
			if (data.path == null || data.path.destination != home) {
				data.path = PathingAlgorithmChooser.GetAlgorithm(ship).PathFrom(ship.location, home, ship);
			}
			UnitAI.MoveResult moved = this.TryToMoveAlongPath(ship, ref data.path);
			if (moved.Result == UnitAI.Result.Error) {
				data.path = null;
				ship.movementPoints.onConsumeAll();
				return UnitAI.Result.InProgress;
			}
			return moved;
		}

		private static Tile NearestReachableCity(MapUnit ship, Player player) {
			List<Tile> cities = player.cities.Select(c => c.location).Where(t => t.NeighborsWater())
				.OrderBy(t => t.DistanceTo(ship.location)).ToList();
			int index = PathingAlgorithmChooser.GetAlgorithm(ship).FindFirstReachableIndex(ship.location, cities, ship);
			return index < 0 ? null : cities[index];
		}

		private static bool IsNeighbor(Tile tile, Tile other) {
			foreach (Tile n in tile.neighbors.Values) {
				if (n == other) {
					return true;
				}
			}
			return false;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.Pathing;
using C7GameData;

namespace C7Engine.AI {
	// Planning for settlers that go by ship: which landmasses our transports
	// can reach, and whether a settler gets to its site sooner by sea than on
	// foot. (All of this is AI heuristics, not Civ3 rules.)
	//
	// A voyage is: walk to one of our coastal cities (the port), board a
	// transport there with the settler's escort, sail to the water next to a
	// landing tile, step ashore, and walk the rest of the way.
	public static class SeaRoutes {
		// A voyage must save at least this many turns over walking, for the
		// trouble and the risk of the sea.
		public const int TurnsSavedToSail = 2;

		// How many turns a settler waits at the port for a transport before
		// trying something else.
		public const int MaxTurnsWaitingForTransport = 20;

		// How long a transport the port would have to build is assumed to
		// take, if the port can't build one.
		private const int UnbuildableTransportTurns = 30;

		// How many of the coastal cities nearest the settler are considered as
		// ports, since each takes a path search to walk to.
		private const int MaxPortsConsidered = 4;

		// The plan for a settler's voyage.
		public sealed class Voyage {
			public City port;
			// The land tile the settler steps onto from the transport.
			public Tile landing;
			// The estimated turns from now until the settler reaches its site.
			public int turns;
		}

		// Whether the unit can carry the settler and its escort.
		public static bool IsTransport(MapUnit unit) {
			return unit.IsWaterUnit() && !unit.IsArmy() && unit.CanCarryUnits() && unit.unitType.capacity >= 2;
		}

		public static bool IsTransport(UnitPrototype type) {
			return type.IsSeaUnit() && !type.isArmy && type.HasUnloadAction() && type.capacity >= 2;
		}

		// The fastest transport the player knows how to build, or null.
		public static UnitPrototype BuildableTransport(Player player) {
			UnitPrototype best = null;
			foreach (UnitPrototype type in EngineStorage.gameData.unitPrototypes) {
				if (!IsTransport(type) || type.unproducible || !type.IsProducibleBy(player.civilization)
					|| !player.HasRequiredTechnology(type)) {
					continue;
				}
				if (best == null || type.movement > best.movement) {
					best = type;
				}
			}
			return best;
		}

		// Our transports that could take on a voyage now: those with no
		// passengers to deliver.
		public static IEnumerable<MapUnit> AvailableTransports(Player player) {
			foreach (MapUnit unit in player.units) {
				if (IsTransport(unit) && unit.currentAI is not FerryAI && unit.PassengerCount() == 0) {
					yield return unit;
				}
			}
		}

		// Whether the ship can sail to the tile (a port of ours), keeping to
		// water it can't sink in.
		public static bool CanSailTo(MapUnit ship, Tile tile) {
			if (ship.location == tile) {
				return true;
			}
			TilePath path = PathingAlgorithmChooser.GetAlgorithm(ship).PathFrom(ship.location, tile, ship);
			return path != null && path.PathLength() > 0;
		}

		// Our idle transports that can sail to the port, nearest first.
		public static IEnumerable<MapUnit> TransportsReaching(Player player, Tile port) {
			foreach (MapUnit ship in AvailableTransports(player).OrderBy(s => s.location.DistanceTo(port)).ToList()) {
				if (CanSailTo(ship, port)) {
					yield return ship;
				}
			}
		}

		// A ship of the type we'd sail with, at the tile, to ask which waters
		// it may sail. It isn't put into play.
		private static MapUnit ExampleTransport(Player player, Tile at) {
			MapUnit transport = player.units.FirstOrDefault(IsTransport);
			if (transport != null) {
				return transport;
			}
			UnitPrototype type = BuildableTransport(player);
			return type?.GetInstance(ID.None(type.name), type, player, location: at);
		}

		// Whether a ship may sail onto the tile: water it won't risk sinking
		// in, or one of our cities.
		private static bool Sailable(MapUnit ship, Tile t) {
			if (t.HasCity()) {
				return t.cityAtTile.owner == ship.owner;
			}
			return t.IsWater() && !ship.IsUnsafeWater(t);
		}

		// Our cities a ship can sail from.
		private static IEnumerable<City> Ports(Player player) {
			foreach (City city in player.cities) {
				if (city.location.NeighborsWater()) {
					yield return city;
				}
			}
		}

		private sealed class ReachCache {
			public int turn = -1, cities = -1, techs = -1;
			public HashSet<int> landmasses = new();
		}

		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Player, ReachCache> reachCaches = new();

		// The landmasses (Tile.continent) that our transports can land on,
		// sailing from any of our ports. Empty if we have no transport and
		// can't build one. Worked out at most once a turn, unless we gain a
		// city or a tech.
		public static HashSet<int> ReachableLandmasses(Player player) {
			ReachCache cache = reachCaches.GetOrCreateValue(player);
			int turn = EngineStorage.gameData?.turn ?? 0;
			if (cache.turn == turn && cache.cities == player.cities.Count && cache.techs == player.knownTechs.Count) {
				return cache.landmasses;
			}
			cache.turn = turn;
			cache.cities = player.cities.Count;
			cache.techs = player.knownTechs.Count;
			cache.landmasses = new HashSet<int>();

			City firstPort = Ports(player).FirstOrDefault();
			MapUnit ship = firstPort == null ? null : ExampleTransport(player, firstPort.location);
			if (ship == null) {
				return cache.landmasses;
			}

			HashSet<Tile> seen = new();
			Queue<Tile> open = new();
			foreach (City port in Ports(player)) {
				if (seen.Add(port.location)) {
					open.Enqueue(port.location);
				}
			}
			while (open.TryDequeue(out Tile t)) {
				foreach (Tile n in t.neighbors.Values) {
					if (n == Tile.NONE) {
						continue;
					}
					if (n.IsLand() && !n.HasCity()) {
						cache.landmasses.Add(n.continent);
					}
					if (Sailable(ship, n) && seen.Add(n)) {
						open.Enqueue(n);
					}
				}
			}
			return cache.landmasses;
		}

		// The fastest voyage for the settler to the destination, or null if
		// none gets there in fewer than `maxTurns` turns.
		public static Voyage PlanVoyage(MapUnit settler, Tile destination, int maxTurns) {
			Player player = settler.owner;
			if (!settler.location.IsLand() || !destination.IsLand()) {
				return null;
			}

			MapUnit ship = ExampleTransport(player, settler.location);
			if (ship == null) {
				return null;
			}
			float shipMoves = Math.Max(1, ship.unitType.movement);
			float settlerMoves = Math.Max(1, settler.MaxMovementPoints());

			// The ports on the settler's landmass nearest to it, and when it
			// could sail from each: once it has walked there, and once a
			// transport is there for it.
			PathingAlgorithm walking = PathingAlgorithmChooser.GetAlgorithm(settler);
			List<City> ports = Ports(player)
				.Where(c => c.location.continent == settler.location.continent)
				.OrderBy(c => c.location.DistanceTo(settler.location))
				.Take(MaxPortsConsidered)
				.ToList();
			Dictionary<Tile, City> portAt = new();
			PriorityQueue<Tile, double> open = new();
			Dictionary<Tile, double> cost = new();
			foreach (City port in ports) {
				int walk;
				if (port.location == settler.location) {
					walk = 0;
				} else {
					TilePath path = walking.PathFrom(settler.location, port.location, settler);
					if (path == null || path.PathLength() <= 0) {
						continue;
					}
					walk = path.PathCost(player, settler.location, settlerMoves, settler.movementPoints.remaining);
				}
				double start = Math.Max(walk, TurnsUntilTransportAt(player, port));
				if (start >= maxTurns) {
					continue;
				}
				portAt[port.location] = port;
				cost[port.location] = start;
				open.Enqueue(port.location, start);
			}

			// Sail outwards from the ports, nearest first, and look at the land
			// next to each tile reached.
			Voyage best = null;
			double bestTurns = maxTurns;
			Dictionary<Tile, City> from = new(portAt);
			while (open.TryDequeue(out Tile t, out double turns)) {
				if (turns > cost[t]) {
					continue;
				}
				// Stepping ashore takes a turn of its own.
				if (turns + 1 >= bestTurns) {
					break;
				}
				foreach (Tile n in t.neighbors.Values) {
					if (n == Tile.NONE) {
						continue;
					}
					if (n.IsLand() && !n.HasCity() && t.IsWater() && n.continent == destination.continent && CanLandOn(n, player)) {
						double total = Math.Ceiling(turns) + 1 + Math.Ceiling(n.DistanceTo(destination) / settlerMoves);
						if (total < bestTurns) {
							bestTurns = total;
							best = new Voyage { port = from[t], landing = n, turns = (int)total };
						}
					}
					if (!Sailable(ship, n)) {
						continue;
					}
					double next = turns + 1 / shipMoves;
					if (!cost.TryGetValue(n, out double known) || next < known) {
						cost[n] = next;
						from[n] = from[t];
						open.Enqueue(n, next);
					}
				}
			}
			return best;
		}

		// Whether our units can step ashore onto the tile without a fight or
		// a diplomatic incident.
		internal static bool CanLandOn(Tile t, Player player) {
			if (t.HasCity() && t.cityAtTile.owner != player) {
				return false;
			}
			Player owner = t.OwningPlayer();
			if (owner != null && owner != player) {
				return false;
			}
			foreach (MapUnit u in t.unitsOnTile) {
				if (u.owner != player) {
					return false;
				}
			}
			return true;
		}

		// About how many turns until a transport could be at the port: one
		// of ours sailing there, or one the port builds.
		private static double TurnsUntilTransportAt(Player player, City port) {
			double best = double.MaxValue;
			MapUnit nearest = TransportsReaching(player, port.location).FirstOrDefault();
			if (nearest != null) {
				best = Math.Ceiling(nearest.location.DistanceTo(port.location) / (double)Math.Max(1, nearest.unitType.movement));
			}
			UnitPrototype type = BuildableTransport(player);
			if (type != null) {
				best = Math.Min(best, port.ListProductionOptions(EngineStorage.gameData).Contains(type) ? port.TurnsToProduce(type) : UnbuildableTransportTurns);
			}
			return best;
		}
	}
}

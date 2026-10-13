using System.Collections.Generic;
using System.Linq;
using System;
using C7GameData;

namespace C7Engine.Pathing {
	public class TradeNetworkSegment {
		public Dictionary<Resource, int> resourceCounts = new();

		// The resources in this segment that the owning player knows about,
		// cached until the player learns a new tech.
		internal Dictionary<Resource, int> availableResources;
		internal int availableResourcesTechCount = -1;

		// The network's tileToSegment says which tiles are in the segment.
		public void AddTile(Tile t, Player p) {
			if (t.Resource != Resource.NONE && t.OwningPlayer() == p) {
				if (resourceCounts.TryGetValue(t.Resource, out int currentCount)) {
					resourceCounts[t.Resource] = currentCount + 1;
				} else {
					resourceCounts[t.Resource] = 1;
				}
			}
		}
	}

	// A class for calculating the trade networks.
	//
	// The main idea is that calculating the entire empire's trade network once
	// is faster than doing it repeatedly for each city. By doing it once we can
	// do a simple flood fill of the road network, rather than needing to do
	// actual pathfinding between different tiles.
	//
	// Each player's network is only computed the first time it is queried.
	// Changes to cities and borders invalidate the whole TradeNetwork; a
	// road built or removed only drops the networks it can change (see
	// OnRoadChanged).
	//
	// TODO: Handle harbors and airports
	// TODO: Account for passing through the borders of civs we're at war with
	// TODO: Invalidate the trade network when war status changes.
	public class TradeNetwork {
		private sealed class PlayerNetwork {
			public readonly Dictionary<City, TradeNetworkSegment> cityToSegment = new();
			// Segments never share tiles, so each tile maps to at most one.
			public readonly Dictionary<Tile, TradeNetworkSegment> tileToSegment = new();
			public City cachedCapital;
		}

		private readonly Dictionary<Player, PlayerNetwork> networks = new();

		public TradeNetwork(GameData gameData) {
		}

		private PlayerNetwork GetNetwork(Player player) {
			lock (networks) {
				if (!networks.TryGetValue(player, out PlayerNetwork network)) {
					network = ComputeTradeNetwork(player);
					networks[player] = network;
				}
				return network;
			}
		}

		// A road appearing or going away at `tile` can only change a player's
		// network if the flood fill reaches the tile or one of its
		// neighbours: the fill only steps onto roaded tiles next to tiles it
		// has reached. Other players' networks would come out the same if
		// recomputed, so they are kept.
		public void OnRoadChanged(Tile tile) {
			lock (networks) {
				List<Player> affected = null;
				foreach ((Player player, PlayerNetwork network) in networks) {
					if (Touches(network, tile)) {
						(affected ??= new()).Add(player);
					}
				}
				if (affected != null) {
					foreach (Player player in affected) {
						networks.Remove(player);
					}
				}
			}
		}

		private static bool Touches(PlayerNetwork network, Tile tile) {
			if (network.tileToSegment.ContainsKey(tile)) {
				return true;
			}
			foreach (Tile n in tile.neighbors.Values) {
				if (network.tileToSegment.ContainsKey(n)) {
					return true;
				}
			}
			return false;
		}

		private static PlayerNetwork ComputeTradeNetwork(Player player) {
			PlayerNetwork network = new();
			Dictionary<City, TradeNetworkSegment> segments = network.cityToSegment;
			Dictionary<Tile, TradeNetworkSegment> seen = network.tileToSegment;

			foreach (City c in player.cities) {
				if (segments.ContainsKey(c)) {
					continue;
				}

				// If we don't know about this city yet, start a new network
				// segment and do a flood fill for all roads coming from the
				// city.
				TradeNetworkSegment segment = new();
				segments[c] = segment;

				Queue<Tile> toCheck = new();
				toCheck.Enqueue(c.location);
				seen.TryAdd(c.location, segment);

				while (toCheck.Count > 0) {
					Tile x = toCheck.Dequeue();
					segment.AddTile(x, player);

					if (x.cityAtTile != null) {
						segments[x.cityAtTile] = segment;
					}

					foreach (Tile n in x.neighbors.Values) {
						if (n.IsRoaded() && seen.TryAdd(n, segment)) {
							toCheck.Enqueue(n);
						}
					}
				}
			}
			return network;
		}

		// Returns the resources and their counts that are available to the given
		// city.
		//
		// The returned dictionary is shared by every city on the same network
		// segment, so it must not be modified.
		public Dictionary<Resource, int> GetResourcesAvailableToCity(Player player, City city) {
			TradeNetworkSegment segment = GetNetwork(player).cityToSegment[city];

			int techCount = player.knownTechs.Count;
			Dictionary<Resource, int> cached = segment.availableResources;
			if (cached != null && segment.availableResourcesTechCount == techCount) {
				return cached;
			}

			Dictionary<Resource, int> result = new();
			foreach ((Resource r, int count) in segment.resourceCounts) {
				if (player.KnowsAboutResource(r)) {
					result[r] = count;
				}
			}
			segment.availableResources = result;
			segment.availableResourcesTechCount = techCount;
			return result;
		}

		// Whether the tile is joined by the player's trade network to one of
		// their cities (or is a city's tile).
		public bool IsJoinedToCity(Tile t, Player p) {
			return GetNetwork(p).tileToSegment.ContainsKey(t);
		}

		public bool HasTradeAccess(Tile t, Player p, Resource r) {
			if (!p.KnowsAboutResource(r)) {
				return false;
			}

			return GetNetwork(p).tileToSegment.TryGetValue(t, out TradeNetworkSegment segment)
				&& segment.resourceCounts.TryGetValue(r, out int count) && count > 0;
		}

		// Whether the player can use the resource at the tile, e.g. to build a
		// railroad: what the tile's network segment has, adjusted, when that is
		// the capital's segment, by the resources imported and exported
		// through deals (see City.GetAvailableResources).
		public bool HasResourceAccess(GameData gameData, Tile t, Player p, Resource r) {
			PlayerNetwork network = GetNetwork(p);
			City capital = GetCapital(p, network);
			if (capital != null
				&& network.tileToSegment.TryGetValue(t, out TradeNetworkSegment segment)
				&& network.cityToSegment.TryGetValue(capital, out TradeNetworkSegment capitalSegment)
				&& segment == capitalSegment) {
				return capital.GetAvailableResources(gameData).ContainsKey(r);
			}
			return HasTradeAccess(t, p, r);
		}

		// How many of the resource the player has on its capital's network
		// segment, not counting imports or exports. A player with no cities
		// has none.
		public int CapitalResourceCount(Player p, Resource r) {
			PlayerNetwork network = GetNetwork(p);
			City capital = GetCapital(p, network);
			if (capital == null || !network.cityToSegment.TryGetValue(capital, out TradeNetworkSegment segment)) {
				return 0;
			}
			return segment.resourceCounts.GetValueOrDefault(r);
		}

		// Whether a road runs between the two players' capitals. Only roads
		// are modelled, so capitals on different landmasses, which Civ3 can
		// connect by sea, count as connected.
		// TODO: Use harbors and airports once the trade network has them.
		public bool CapitalsConnected(Player a, Player b) {
			PlayerNetwork network = GetNetwork(a);
			City capitalA = GetCapital(a, network);
			City capitalB = GetCapital(b, GetNetwork(b));
			if (capitalA == null || capitalB == null) {
				return false;
			}
			if (capitalA.location.continent != capitalB.location.continent) {
				return true;
			}
			return network.cityToSegment.TryGetValue(capitalA, out TradeNetworkSegment segment)
				&& network.tileToSegment.TryGetValue(capitalB.location, out TradeNetworkSegment other)
				&& segment == other;
		}

		// The player's cities grouped by the network segment they're on, in the
		// order of the player's city list. The group with the capital comes
		// first.
		public List<List<City>> CityGroups(Player p) {
			PlayerNetwork network = GetNetwork(p);
			Dictionary<TradeNetworkSegment, List<City>> groups = new();
			List<List<City>> result = new();
			foreach (City c in p.cities) {
				if (!network.cityToSegment.TryGetValue(c, out TradeNetworkSegment segment)) {
					result.Add(new List<City> { c });
					continue;
				}
				if (!groups.TryGetValue(segment, out List<City> group)) {
					group = new();
					groups[segment] = group;
					result.Add(group);
				}
				group.Add(c);
			}
			City capital = GetCapital(p, network);
			int capitalGroup = result.FindIndex(g => g.Contains(capital));
			if (capitalGroup > 0) {
				List<City> group = result[capitalGroup];
				result.RemoveAt(capitalGroup);
				result.Insert(0, group);
			}
			return result;
		}

		public bool ConnectedToCapital(Player p, City c) {
			PlayerNetwork network = GetNetwork(p);
			City capital = GetCapital(p, network);
			if (capital == null) {
				return false;
			}
			return network.cityToSegment.TryGetValue(c, out TradeNetworkSegment segment)
				&& network.cityToSegment.TryGetValue(capital, out TradeNetworkSegment capitalSegment)
				&& segment == capitalSegment;
		}

		// The player's capital, or their first city if they have none. The
		// capital can move without the network being invalidated (when a new
		// palace is built), so the cached capital is re-checked on each use.
		private static City GetCapital(Player p, PlayerNetwork network) {
			City cached = network.cachedCapital;
			if (cached != null && cached.owner == p && cached.IsCapital()) {
				return cached;
			}
			City capital = p.cities.Find(x => x.IsCapital());
			network.cachedCapital = capital;
			return capital ?? p.cities.FirstOrDefault();
		}
	}
}

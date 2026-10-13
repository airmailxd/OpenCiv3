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
		// A resource counts inside the player's borders, or outside every
		// civ's borders under the player's colony (see Tile_Holdings).
		public void AddTile(Tile t, Player p) {
			if (t.Resource != Resource.NONE && (t.OwningPlayer() == p || (t.holdingOwner == p && t.OwningPlayer() == null && t.HasColony()))) {
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
	// OnRoadChanged); a building built or lost recomputes its owner's
	// network (see PlayerNetwork.IsValidFor).
	//
	// Harbors and airports join road segments together. The Civilopedia
	// (Conquests, GCON_Trade, "Trade Route Connections"): "Two cities are
	// connected if any one of the following are true: there is a road or
	// railroad running between them, there are harbors in both cities and a
	// visible water route between them, or both cities contain an airport."
	// "In order to use a water trade route, your civilization must be able
	// to safely traverse every square of the route." Which water: the
	// Civilopedia's Astronomy ("Harbors can be connected by sea squares"),
	// Navigation and Magnetism ("... by ocean squares"), which are the BIQ's
	// EnablesTradeOverSea and EnablesTradeOverOcean tech flags. Coast needs
	// no flag: the Harbor itself needs Map Making, which the Civilopedia
	// names for coastal routes. The Great Lighthouse also opens sea squares
	// ("You can trade over sea tiles before Astronomy by owning the Great
	// Lighthouse",
	// https://forums.civfanatics.com/threads/my-town-on-another-island-has-a-harbor-why-isnt-it-connecting-to-the-trade-network.113835/),
	// in line with the Civilopedia's "safely traverse". Which buildings link
	// comes from the BIQ's AllowsWaterTrade and AllowsAirTrade flags.
	//
	// The water links depend on the player's techs and on which tiles they
	// have seen, which change without telling the network, so a network
	// with harbors or airports is also recomputed when the player learns a
	// tech, and once a turn.
	//
	// War cuts routes. The Civilopedia (GCON_Trade): "two cities are not
	// connected if the only road/rail that connects them passes through
	// enemy territory, or if a harbor is blockaded by enemy naval forces."
	// - Roads don't run through tiles owned by a civ the player is at war
	//   with. Nor do water routes: "a city surrounded by water owned by a
	//   Civilization with which you do not have open borders, will be
	//   unable to create a trade route between it and the capital by way of
	//   the harbour"
	//   (https://forums.civfanatics.com/resources/how-do-trade-routes-roads-railroads-harbours-work-vanilla.25620/).
	//   That guide also has neutral territory cutting routes; the
	//   Civilopedia says enemy territory, which is what is followed here.
	// - A blockade: "An enemy naval unit can break a trade route between a
	//   city and the capital if its zone of control (one hex in a circle
	//   around the unit) blocks the continuous (invisible) trade route line
	//   between two harbours" (the same guide). Water routes don't cross the
	//   tile of an enemy ship or the tiles around it, so a ship next to a
	//   harbor city on a straight coast shuts its port. Barbarian ships
	//   count: "check for barb ships. They can and will interrupt your
	//   trade" (https://forums.civfanatics.com/threads/trade-route-keeps-being-broken.415882/).
	//   UNVERIFIED (simplest consistent behaviour): which ships blockade.
	//   Here any enemy ship that can attack (attack above 0) and isn't
	//   carried, whether or not it has the BIQ's zone of control flag, which
	//   no conquests.biq ship has.
	// A network is recomputed when the civs the player is at war with
	// change. Ships move without telling the network, so blockades are seen
	// at the once-a-turn recomputation of networks with harbors.
	public class TradeNetwork {
		private sealed class PlayerNetwork {
			public readonly Dictionary<City, TradeNetworkSegment> cityToSegment = new();
			// Segments never share tiles, so each tile maps to at most one.
			public readonly Dictionary<Tile, TradeNetworkSegment> tileToSegment = new();
			public City cachedCapital;

			// The water tiles the player's harbors can trade over, labelled by
			// which body of reachable water they're in, and the labels each
			// segment's harbors touch. Empty without harbors.
			public readonly Dictionary<Tile, int> waterLabel = new();
			public readonly Dictionary<TradeNetworkSegment, HashSet<int>> segmentWaterLabels = new();
			// The segments with an airport city.
			public readonly HashSet<TradeNetworkSegment> airportSegments = new();

			// What the network was computed from (see IsValidFor).
			public long buildingsVersion;
			public bool hasLinks;
			public int techCount;
			public int turn;
			// The civs the player was at war with, in the game's player order.
			public List<Player> enemies;

			public bool IsValidFor(Player player, int turn, List<Player> players) {
				if (buildingsVersion != player.BuildingsVersion) {
					return false;
				}
				if (!SameEnemies(player, players, enemies)) {
					return false;
				}
				return !hasLinks || (techCount == (player.knownTechs?.Count ?? 0) && this.turn == turn);
			}
		}

		// The civs the player is at war with, in the game's player order.
		private static List<Player> Enemies(Player player, List<Player> players) {
			List<Player> result = new();
			if (players == null) {
				return result;
			}
			foreach (Player other in players) {
				if (IsEnemy(player, other)) {
					result.Add(other);
				}
			}
			return result;
		}

		private static bool IsEnemy(Player player, Player other) {
			return other != null && other != player && !other.defeated && PlayerRelationship.AtWar(player, other);
		}

		// Whether the player is at war with just the given civs, checked
		// without making a list, as this runs on every query.
		private static bool SameEnemies(Player player, List<Player> players, List<Player> enemies) {
			int i = 0;
			if (players != null) {
				foreach (Player other in players) {
					if (!IsEnemy(player, other)) {
						continue;
					}
					if (i >= enemies.Count || enemies[i] != other) {
						return false;
					}
					++i;
				}
			}
			return i == enemies.Count;
		}

		private readonly Dictionary<Player, PlayerNetwork> networks = new();
		private readonly GameData gameData;

		public TradeNetwork(GameData gameData) {
			this.gameData = gameData;
		}

		private PlayerNetwork GetNetwork(Player player) {
			int turn = gameData?.turn ?? 0;
			List<Player> players = gameData?.players;
			lock (networks) {
				if (!networks.TryGetValue(player, out PlayerNetwork network) || !network.IsValidFor(player, turn, players)) {
					network = ComputeTradeNetwork(player, turn, Enemies(player, players));
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

		private static PlayerNetwork ComputeTradeNetwork(Player player, int turn, List<Player> enemies) {
			PlayerNetwork network = new() {
				buildingsVersion = player.BuildingsVersion,
				techCount = player.knownTechs?.Count ?? 0,
				turn = turn,
				enemies = enemies,
			};
			HashSet<Player> enemySet = new(enemies);
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
						if (n.IsRoaded() && !InEnemyTerritory(n, enemySet) && seen.TryAdd(n, segment)) {
							toCheck.Enqueue(n);
						}
					}
				}
			}
			JoinByHarborsAndAirports(player, network, enemySet);
			return network;
		}

		private static bool InEnemyTerritory(Tile t, HashSet<Player> enemies) {
			if (enemies.Count == 0) {
				return false;
			}
			Player owner = t.OwningPlayer();
			return owner != null && enemies.Contains(owner);
		}

		// The water tiles enemy ships blockade: each ship's tile and the
		// tiles around it (see the class comment).
		private static HashSet<Tile> BlockadedTiles(HashSet<Player> enemies) {
			HashSet<Tile> result = new();
			foreach (Player enemy in enemies) {
				foreach (MapUnit u in enemy.units) {
					if (!Blockades(u)) {
						continue;
					}
					result.Add(u.location);
					foreach (Tile n in u.location.neighbors.Values) {
						result.Add(n);
					}
				}
			}
			return result;
		}

		// Whether the ship can blockade a harbor (see the class comment).
		public static bool Blockades(MapUnit u) {
			return u != null && u.location != null && u.location != Tile.NONE && u.hitPointsRemaining > 0
				&& u.IsWaterUnit() && u.unitType.attack > 0 && !u.IsLoaded();
		}

		// Whether the city has a building that links it by water (a Harbor)
		// or by air (an Airport), built or granted by a wonder.
		private static bool HasWaterTrade(City c) => c.EffectiveBuildings().Exists(cb => cb.building.allowsWaterTrade);
		private static bool HasAirTrade(City c) => c.EffectiveBuildings().Exists(cb => cb.building.allowsAirTrade);

		// Whether the player's harbors can trade over the tile: coast always,
		// sea and ocean once allowed (see the class comment).
		private static bool CanTradeOver(Tile t, bool overSea, bool overOcean) {
			TerrainType terrain = t.baseTerrainType;
			if (terrain == null || !terrain.IsWater) {
				return false;
			}
			if (terrain.IsOcean) {
				return overOcean;
			}
			if (terrain.IsSea) {
				return overSea;
			}
			return true;
		}

		// Merges the road segments that harbors and airports link, and
		// records which water the harbors reach (for CapitalsConnected).
		private static void JoinByHarborsAndAirports(Player player, PlayerNetwork network, HashSet<Player> enemies) {
			List<City> harbors = null;
			List<City> airports = null;
			foreach (City c in player.cities) {
				if (!network.cityToSegment.ContainsKey(c)) {
					continue;
				}
				if (HasWaterTrade(c)) {
					(harbors ??= new()).Add(c);
				}
				if (HasAirTrade(c)) {
					(airports ??= new()).Add(c);
				}
			}
			if (harbors == null && airports == null) {
				return;
			}
			network.hasLinks = true;

			// Union-find over the road segments.
			Dictionary<TradeNetworkSegment, TradeNetworkSegment> parent = new();
			TradeNetworkSegment Find(TradeNetworkSegment s) {
				while (parent.TryGetValue(s, out TradeNetworkSegment p)) {
					s = p;
				}
				return s;
			}
			bool merged = false;
			void Union(TradeNetworkSegment a, TradeNetworkSegment b) {
				a = Find(a);
				b = Find(b);
				if (a != b) {
					parent[b] = a;
					merged = true;
				}
			}

			if (airports != null) {
				TradeNetworkSegment first = network.cityToSegment[airports[0]];
				foreach (City c in airports) {
					Union(first, network.cityToSegment[c]);
				}
			}

			Dictionary<City, HashSet<int>> harborLabels = new();
			if (harbors != null) {
				bool overSea = player.HasTechEnabling(C7GameData.Save.SaveTech.Flag.EnablesTradeOverSea) || player.HasSafeSeaTravel();
				bool overOcean = player.HasTechEnabling(C7GameData.Save.SaveTech.Flag.EnablesTradeOverOcean);
				TileKnowledge knowledge = player.tileKnowledge;
				HashSet<Tile> blockaded = BlockadedTiles(enemies);
				bool CanUse(Tile t) => knowledge.isTileKnown(t) && CanTradeOver(t, overSea, overOcean)
					&& !blockaded.Contains(t) && !InEnemyTerritory(t, enemies);
				Dictionary<Tile, int> label = network.waterLabel;
				Dictionary<int, TradeNetworkSegment> labelSegment = new();
				int nextLabel = 0;
				Queue<Tile> toCheck = new();
				foreach (City c in harbors) {
					HashSet<int> touched = new();
					foreach (Tile start in c.location.neighbors.Values) {
						if (label.TryGetValue(start, out int existing)) {
							touched.Add(existing);
							continue;
						}
						// "A visible water route": only water the player has
						// seen.
						if (knowledge == null || !CanUse(start)) {
							continue;
						}
						int id = nextLabel++;
						label[start] = id;
						touched.Add(id);
						toCheck.Enqueue(start);
						while (toCheck.Count > 0) {
							Tile x = toCheck.Dequeue();
							foreach (Tile n in x.neighbors.Values) {
								if (!label.ContainsKey(n) && CanUse(n)) {
									label[n] = id;
									toCheck.Enqueue(n);
								}
							}
						}
					}
					harborLabels[c] = touched;
					TradeNetworkSegment segment = network.cityToSegment[c];
					foreach (int id in touched) {
						if (labelSegment.TryGetValue(id, out TradeNetworkSegment other)) {
							Union(other, segment);
						} else {
							labelSegment[id] = segment;
						}
					}
				}
			}

			if (merged) {
				// Replace each group of segments by its root, which gets all
				// their resources.
				Dictionary<TradeNetworkSegment, TradeNetworkSegment> replacement = new();
				foreach (City c in player.cities) {
					if (!network.cityToSegment.TryGetValue(c, out TradeNetworkSegment s) || replacement.ContainsKey(s)) {
						continue;
					}
					TradeNetworkSegment root = Find(s);
					replacement[s] = root;
					if (root != s) {
						foreach ((Resource r, int count) in s.resourceCounts) {
							root.resourceCounts[r] = root.resourceCounts.GetValueOrDefault(r) + count;
						}
					}
				}
				foreach (City c in network.cityToSegment.Keys.ToList()) {
					network.cityToSegment[c] = replacement[network.cityToSegment[c]];
				}
				foreach (Tile t in network.tileToSegment.Keys.ToList()) {
					TradeNetworkSegment s = network.tileToSegment[t];
					if (replacement.TryGetValue(s, out TradeNetworkSegment root) && root != s) {
						network.tileToSegment[t] = root;
					}
				}
			}

			foreach ((City c, HashSet<int> labels) in harborLabels) {
				TradeNetworkSegment segment = network.cityToSegment[c];
				if (!network.segmentWaterLabels.TryGetValue(segment, out HashSet<int> all)) {
					all = new();
					network.segmentWaterLabels[segment] = all;
				}
				all.UnionWith(labels);
			}
			if (airports != null) {
				foreach (City c in airports) {
					network.airportSegments.Add(network.cityToSegment[c]);
				}
			}
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

		// Whether the two players' capitals are connected, which foreign
		// trade needs (Civilopedia, GCON_Trade): by a road from one capital's
		// network to the other's capital, by airports on both capitals'
		// networks, or by harbors on both capitals' networks with a water
		// route between them. UNVERIFIED: whose techs and map a water route
		// between two civs needs; here both civs must be able to trade over
		// it, as each one's "civilization must be able to safely traverse
		// every square of the route".
		public bool CapitalsConnected(Player a, Player b) {
			PlayerNetwork network = GetNetwork(a);
			PlayerNetwork otherNetwork = GetNetwork(b);
			City capitalA = GetCapital(a, network);
			City capitalB = GetCapital(b, otherNetwork);
			if (capitalA == null || capitalB == null
				|| !network.cityToSegment.TryGetValue(capitalA, out TradeNetworkSegment segment)
				|| !otherNetwork.cityToSegment.TryGetValue(capitalB, out TradeNetworkSegment otherSegment)) {
				return false;
			}
			if (network.tileToSegment.TryGetValue(capitalB.location, out TradeNetworkSegment reached) && reached == segment) {
				return true;
			}
			if (otherNetwork.tileToSegment.TryGetValue(capitalA.location, out reached) && reached == otherSegment) {
				return true;
			}
			if (network.airportSegments.Contains(segment) && otherNetwork.airportSegments.Contains(otherSegment)) {
				return true;
			}
			return network.segmentWaterLabels.TryGetValue(segment, out HashSet<int> labels)
				&& otherNetwork.segmentWaterLabels.TryGetValue(otherSegment, out HashSet<int> otherLabels)
				&& HarborReaches(network, labels, b, otherNetwork, otherSegment)
				&& HarborReaches(otherNetwork, otherLabels, a, network, segment);
		}

		// Whether one of the other player's harbor cities on otherSegment is
		// next to water that the network's harbors with the given labels
		// reach.
		private static bool HarborReaches(PlayerNetwork network, HashSet<int> labels, Player other, PlayerNetwork otherNetwork, TradeNetworkSegment otherSegment) {
			foreach (City c in other.cities) {
				if (!otherNetwork.cityToSegment.TryGetValue(c, out TradeNetworkSegment s) || s != otherSegment || !HasWaterTrade(c)) {
					continue;
				}
				foreach (Tile n in c.location.neighbors.Values) {
					if (network.waterLabel.TryGetValue(n, out int label) && labels.Contains(label)) {
						return true;
					}
				}
			}
			return false;
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

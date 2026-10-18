using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.Pathing;

// Checks the lazily computed trade network against the original eager
// implementation.
public sealed class PerfTradeNetworkTests : MapBase {
	private static readonly TerrainImprovement testRoad = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	// The original algorithm: one flood fill over roads per unseen city.
	private sealed class ReferenceNetwork {
		private readonly Dictionary<City, TradeNetworkSegment> segments = new();
		private readonly Dictionary<Tile, TradeNetworkSegment> tileSegments = new();

		public ReferenceNetwork(Player player) {
			HashSet<Tile> seen = new();
			foreach (City c in player.cities) {
				if (segments.ContainsKey(c)) {
					continue;
				}
				TradeNetworkSegment segment = new();
				segments[c] = segment;
				Queue<Tile> toCheck = new();
				toCheck.Enqueue(c.location);
				seen.Add(c.location);
				while (toCheck.Count > 0) {
					Tile x = toCheck.Dequeue();
					segment.AddTile(x, player);
					tileSegments[x] = segment;
					if (x.cityAtTile != null) {
						segments[x.cityAtTile] = segment;
					}
					foreach (Tile n in x.neighbors.Values) {
						if ((n.IsRoaded() || n.HasCity()) && seen.Add(n)) {
							toCheck.Enqueue(n);
						}
					}
				}
			}
		}

		public Dictionary<Resource, int> Resources(Player player, City city) {
			return segments[city].resourceCounts.Where(p => player.KnowsAboutResource(p.Key)).ToDictionary(p => p.Key, p => p.Value);
		}

		public bool HasTradeAccess(Tile t, Player p, Resource r) {
			if (!p.KnowsAboutResource(r)) {
				return false;
			}
			return tileSegments.TryGetValue(t, out TradeNetworkSegment s) && s.resourceCounts.TryGetValue(r, out int count) && count > 0;
		}

		public bool ConnectedToCapital(Player p, City c) {
			City capital = p.cities.Find(x => x.IsCapital()) ?? p.cities.FirstOrDefault();
			return capital != null && segments.TryGetValue(c, out var a) && segments.TryGetValue(capital, out var b) && a == b;
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void LazyTradeNetworkMatchesReference(int seed) {
		C7GameData.GameData gameData = new(seed);
		EngineStorage.InitializeGameDataForTests(gameData);
		Random rng = new(seed);

		GameMap map = new() { numTilesWide = 30, numTilesTall = 30, tiles = new List<Tile>() };
		for (int i = 0; i < 30 * 30 / 2; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) {
				XCoordinate = x,
				YCoordinate = y,
				baseTerrainType = new() { Key = "plains" },
				overlayTerrainType = new() { Key = "plains", movementCost = 1 },
			});
		}
		map.computeNeighbors();

		ID knownTech = ID.FromString("tech-1");
		ID unknownTech = ID.FromString("tech-2");
		Resource[] resources = {
			new() { Key = "horses", Name = "horses" },
			new() { Key = "iron", Name = "iron", Prerequisite = knownTech },
			new() { Key = "coal", Name = "coal", Prerequisite = unknownTech },
		};

		// Civs that have not met, so not at war (see TradeNetwork).
		List<Player> players = new() { new Player() { civilization = new Civilization("A") }, new Player() { civilization = new Civilization("B") } };
		players[0].knownTechs.Add(knownTech);
		gameData.players.AddRange(players);

		foreach (Tile t in map.tiles) {
			if (rng.NextDouble() < 0.45) {
				t.overlays.Add(testRoad);
			}
			if (rng.NextDouble() < 0.2) {
				t.Resource = resources[rng.Next(resources.Length)];
			}
		}

		// Found spaced out cities for both players, and assign tile ownership
		// to the nearest one.
		List<City> allCities = new();
		foreach (Tile t in map.tiles.OrderBy(_ => rng.Next())) {
			if (allCities.Count >= 14) {
				break;
			}
			if (allCities.Any(c => c.location.DistanceTo(t) < 3)) {
				continue;
			}
			Player owner = players[allCities.Count % 2];
			City city = new(t, owner, "City " + allCities.Count, ID.None("city"));
			t.cityAtTile = city;
			owner.cities.Add(city);
			allCities.Add(city);
		}
		players[0].cities[1].capital = true;
		foreach (Tile t in map.tiles) {
			t.owningCity = allCities.OrderBy(c => c.location.DistanceTo(t)).First();
		}

		TradeNetwork network = new(gameData);
		foreach (Player p in players) {
			AssertMatchesReference(network, p, map, resources);
		}

		// Learning a tech makes its resources available.
		players[0].knownTechs.Add(unknownTech);
		ReferenceNetwork updated = new(players[0]);
		foreach (City c in players[0].cities) {
			Assert.Equal(updated.Resources(players[0], c).OrderBy(x => x.Key.Key), network.GetResourcesAvailableToCity(players[0], c).OrderBy(x => x.Key.Key));
		}

		// Moving the palace moves the capital.
		players[0].cities[1].capital = false;
		players[0].cities[2].capital = true;
		foreach (City c in players[0].cities) {
			Assert.Equal(updated.ConnectedToCapital(players[0], c), network.ConnectedToCapital(players[0], c));
		}

		// Building and removing roads drops only the networks they can
		// change; every network still matches one computed from scratch.
		TradeNetwork gameNetwork = gameData.GetTradeNetwork();
		for (int i = 0; i < 300; ++i) {
			Tile t = map.tiles[rng.Next(map.tiles.Count)];
			if (t.cityAtTile != null) {
				continue;
			}
			TerrainImprovement road = t.overlays.ImprovementAtLayer(TerrainImprovement.Layer.Roads);
			if (road != null) {
				t.overlays.Remove(road);
			} else {
				t.overlays.Add(testRoad);
			}
			// Only query some of the time, so that some changes happen to
			// networks that are cached and some to ones that aren't.
			if (i % 3 == 0) {
				foreach (Player p in players) {
					AssertMatchesReference(gameNetwork, p, map, resources);
				}
			}
		}
	}

	private static void AssertMatchesReference(TradeNetwork network, Player p, GameMap map, Resource[] resources) {
		ReferenceNetwork reference = new(p);
		foreach (City c in p.cities) {
			Assert.Equal(reference.Resources(p, c).OrderBy(x => x.Key.Key), network.GetResourcesAvailableToCity(p, c).OrderBy(x => x.Key.Key));
			Assert.Equal(reference.ConnectedToCapital(p, c), network.ConnectedToCapital(p, c));
		}
		foreach (Tile t in map.tiles) {
			foreach (Resource r in resources) {
				Assert.Equal(reference.HasTradeAccess(t, p, r), network.HasTradeAccess(t, p, r));
			}
		}
	}
}

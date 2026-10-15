namespace C7Engine {
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Linq;
	using Serilog;
	using System.Diagnostics;
	using C7GameData;
	using C7GameData.Save;

	public class WorldCharacteristics {
		public enum Climate {
			Arid,
			Normal,
			Wet,
		}
		public Climate climate;

		public enum Landform {
			Archipelago,
			Continents,
			Pangaea,
		}
		public Landform landform;

		public enum OceanCoverage {
			Percent_60 = 60,
			Percent_70 = 70,
			Percent_80 = 80,
		}
		public OceanCoverage oceanCoverage;

		public enum Temperature {
			Cool,
			Temperate,
			Warm,
		}
		public Temperature temperature;

		public enum Age {
			Billion_3,
			Billion_4,
			Billion_5,
		}
		public Age age;

		public BarbarianActivity barbarianActivity;

		public int mapSeed = -1;
		public WorldSize worldSize;
		// How many of the starts must be next to the sea: one for each
		// seafaring civ picked for the game (see GameSetup), which must start
		// on the coast. Other civs may start anywhere.
		public int coastalStartsNeeded;
		public List<TerrainType> terrainTypes;
		public List<Resource> resources = new();
		public Government defaultGovernment = new();

		public int maxRankOfWorkableTiles;
		public int maxRankOfBarbarianCampTiles;

		// The food each citizen eats a turn (the BIQ's RULE).
		public int foodPerCitizen = 2;

		public WorldCharacteristics() { }

		public WorldCharacteristics(SaveGame save) {
			terrainTypes = save.TerrainTypes;
			resources = save.Resources;
			defaultGovernment = save.Governments.Find(g => g.defaultType);

			maxRankOfWorkableTiles = save.Rules.MaxRankOfWorkableTiles;
			maxRankOfBarbarianCampTiles = save.Rules.MaxRankOfBarbarianCampTiles;
			foodPerCitizen = save.Rules.FoodConsumptionPerCitizen;

			barbarianActivity = save.BarbarianInfo.barbarianActivity;
		}
	}

	public class MapGenerator {
		private static ILogger log = Log.ForContext<MapGenerator>();

		private const int MIN_TILES_PER_PLAYER_ISLAND = 81;

		// The entry point to the overall map generation process.
		public static GameMap GenerateMap(WorldCharacteristics wc) {
			return GenerateMap(wc, out _);
		}

		// Generates a map, and says why it can't be played, or null if it
		// can. A map can't be played when it has no room for a starting
		// location for every civ, the world size's distance between civs
		// apart. Such a map is still generated in full, so it can be looked
		// at.
		public static GameMap GenerateMap(WorldCharacteristics wc, out string problem) {
			if (wc.mapSeed == -1) {
				log.Information("Random seed is not specified, generating...");
				wc.mapSeed = new Random().Next(int.MaxValue);
			}
			log.Information("Seed: " + wc.mapSeed);

			// Step 0: Sanitize world characteristics
			SanitizeWorldCharacteristics(wc);

			// Step 1: generate the general shape of the terrain.
			GameMap gameMap = GenerateTerrainShape(wc);

			// Step 2: ensure we have a proper continental shelf around each
			// landmass, with land->coast->sea->ocean transitions.
			FixContinentalShelf(wc, gameMap);

			// Step 3: add hills and mountains.
			AddHillsAndMountains(wc, gameMap);

			// Step 4: use random walks to split each landmass up into potential
			// biome zones, which we will then assign in step 4 based on the
			// world characteristics.
			SplitContinentsIntoPotentialBiomes(wc, gameMap);

			// Step 5: Using the temperature information, assign each potential
			// biome region a specific biome: grassland, plains, desert, tundra
			AssignBiomes(wc, gameMap);

			// Step 6: add vegetation where appropriate.
			AddVegetation(wc, gameMap);

			// Step 7: Add rivers that start from high points and flow to low
			// points.
			AddRivers(wc, gameMap);

			// Step 7b: Deserts along rivers are flood plains.
			AddFloodPlains(wc, gameMap);

			// Step 8: Add resources (luxury/strategic/bonus).
			AddResources(wc, gameMap);

			// Step 9: Add bonus grassland. We do this after assigning resources
			// to make sure resources get placed - we don't want to steal spots
			// for resources.
			AddBonusGrasslands(wc, gameMap);

			// Step 10: Place player starting locations
			// Placing this before barb camps so barbs don't spawn <5 tiles away from players
			// Tried placing barbs then players, but caused all players to spawn together
			DetermineStartingLocations(wc, gameMap);
			problem = null;
			if (gameMap.startingLocations.Count < wc.worldSize.numberOfCivs) {
				problem = $"Only {gameMap.startingLocations.Count} of {wc.worldSize.numberOfCivs} starting locations fit at least {wc.worldSize.distanceBetweenCivs} apart in map coordinates";
			} else if (gameMap.startingLocations.Count(s => s.NeighborsOcean()) < wc.coastalStartsNeeded) {
				problem = $"Fewer than {wc.coastalStartsNeeded} coastal starting locations for the seafaring civs";
			}

			// Step 11: Add barb camps. We do this towards the end to make sure
			// that we don't have barb camps on top of resources.
			// Previously step 10
			AddBarbarianCamps(wc, gameMap);

			// Step 12: Add goody huts, away from the players and the barb
			// camps.
			AddGoodyHuts(wc, gameMap);

			// Last step: Assign the terrain file and image ids to each tile so
			// we know which texture to use when displaying them.
			TerrainTextureFiles.AssignTextureDetails(new Random(wc.mapSeed + 0xebac), wc.terrainTypes, gameMap);

			return gameMap;
		}

		private static void SanitizeWorldCharacteristics(WorldCharacteristics wc) {
			// TODO: Supporting maps of odd dimensions should be doable, but beyond current tiling implementation 
			// As a mitigation, we simply shrink the map dimensions a bit 

			// The world size is usually one of the game's list of sizes, so
			// change a copy of it.
			if (wc.worldSize.width % 2 == 1 || wc.worldSize.height % 2 == 1) {
				wc.worldSize = wc.worldSize.Clone();
			}

			if (wc.worldSize.width % 2 == 1) {
				log.Warning("Uneven map width. Shrinking by one.");
				wc.worldSize.width -= 1;
			}

			if (wc.worldSize.height % 2 == 1) {
				log.Warning("Uneven map height. Shrinking by one.");
				wc.worldSize.height -= 1;
			}
		}

		private static GameMap GenerateTerrainShape(WorldCharacteristics wc) {
			WorldCharacteristics.Landform landform = wc.landform;

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();

			GameMap lastMap = null;

			int maxAttempts = 30;
			for (int attempt = 0; attempt < maxAttempts; ++attempt) {
				GameMap m = GenerateTerrainShapeAttempt(wc, attempt);

				if (MapIsAcceptable(wc, m)) {
					stopwatch.Stop();
					log.Information($"Map gen took {attempt} attempts and {stopwatch.ElapsedMilliseconds} milliseconds");
					return m;
				}

				lastMap = m;
			}

			// None of the maps has the shape asked for (the shape checks are
			// C7's own, and some pass for only a few of the maps). Use
			// the last one: whether it can be played depends on whether
			// every civ gets a starting location (see GenerateMap).
			log.Warning($"No map had the shape of a {landform} map after {maxAttempts} attempts; using the last one");
			return lastMap;
		}

		// One try at the land and water of the map, which GenerateTerrainShape
		// then checks has the shape asked for.
		private static GameMap GenerateTerrainShapeAttempt(WorldCharacteristics wc, int attempt) {
			int seed = wc.mapSeed + 0x1234 * attempt;
			HeightMap hm = new(seed: seed, width:wc.worldSize.width, height:wc.worldSize.height, scale:GetNoiseScale(wc.landform));
			if (wc.landform == WorldCharacteristics.Landform.Archipelago) {
				hm.RaiseDomes(ArchipelagoIslands(wc, new Random(seed + 0x15a7d)), ARCHIPELAGO_DOME_WEIGHT, ARCHIPELAGO_CHANNEL);
			}
			return ToLandAndWaterGameMap(wc, hm, wc.oceanCoverage);
		}

		// The share of an archipelago's land that lies on its planned
		// islands; the rest is islets the noise makes.
		private const double ARCHIPELAGO_ISLAND_LAND_SHARE = .85;

		// The sizes of an archipelago's planned islands, in civs' shares of
		// the land (the land over the number of civs). The islands mix big
		// ones several civs share, middling ones a civ has to itself and
		// small ones left to settle (owner's report, 2026-10-12). An island
		// may be any size, as long as the land and water add up (owner's
		// decision, 2026-10-12: "A long winding island with 5 civs is
		// fine... There is no arbitrary limit"), and the owner wants
		// "clearly large islands, several holding 2-3 civs, alongside
		// medium and small ones, including some empty ones worth settling"
		// (owner's report on seed 1426963693, 2026-10-12, where every
		// island but one was 100 to 235 tiles). So the islands come in four
		// kinds, each of a random size within its range:
		// - big islands, one per five civs, each for two or three civs
		//   (now and then four), with more land per civ than a home island;
		// - home islands, one for each civ left;
		// - islands worth settling, about two per five civs, left empty;
		// - small islands, from the rest of the land.
		private const double ARCHIPELAGO_BIG_ISLANDS_PER_CIV = .2;
		private const double ARCHIPELAGO_MOST_BIG_ISLAND_CIVS = .6;
		private const double ARCHIPELAGO_BIG_ISLAND_MIN = .75;
		private const double ARCHIPELAGO_BIG_ISLAND_MAX = .95;
		private const double ARCHIPELAGO_HOME_ISLAND_MIN = .35;
		private const double ARCHIPELAGO_HOME_ISLAND_MAX = .5;
		private const double ARCHIPELAGO_SETTLING_ISLANDS_PER_CIV = .4;
		private const double ARCHIPELAGO_SETTLING_ISLAND_MIN = .25;
		private const double ARCHIPELAGO_SETTLING_ISLAND_MAX = .45;
		private const double ARCHIPELAGO_SMALL_ISLAND_MIN = .05;
		private const double ARCHIPELAGO_SMALL_ISLAND_MAX = .15;

		// How much the islands' domes, rather than the noise, decide the
		// height of an archipelago (see HeightMap.RaiseDomes). The noise
		// shapes the coasts and makes the islets.
		private const double ARCHIPELAGO_DOME_WEIGHT = .45;

		// How much wider, in map coordinates, an island's dome is made than
		// the island is planned to be, as the dome's edge ends up under
		// water. The same for every island, as the strip lost is about as
		// wide around a small island as around a big one.
		private const double ARCHIPELAGO_DOME_MARGIN = 3.5;

		// The width of the open water between neighboring islands, in map
		// coordinates (see HeightMap.RaiseDomes).
		private const double ARCHIPELAGO_CHANNEL = 3;

		// The number of tiles an archipelago's island needs to hold a civ
		// (see MapIsAcceptable): half of each civ's share of the land. A
		// fixed number wouldn't do, as the land per civ varies by two and a
		// half times with the world size and water: a Tiny world with 80%
		// water has about 70 land tiles per civ, a Huge one with 60% water
		// about 280.
		private static int ArchipelagoTilesPerPlayer(int landTiles, int numberOfCivs) {
			return Math.Max(1, landTiles / (2 * Math.Max(1, numberOfCivs)));
		}

		// The land tiles a map is meant to have: what the ocean coverage
		// and the extra water (see ToLandAndWaterGameMap) leave.
		private static int ExpectedLandTiles(WorldCharacteristics wc) {
			int tiles = wc.worldSize.width * wc.worldSize.height / 2;
			return tiles * (100 - (int)wc.oceanCoverage - EXTRA_WATER_PERCENT) / 100;
		}

		// The sizes, in tiles, of an archipelago's planned islands, biggest
		// kind first (see ARCHIPELAGO_BIG_ISLANDS_PER_CIV).
		private static List<double> ArchipelagoIslandSizes(int civs, double landTiles, Random random) {
			double share = landTiles / civs;
			List<double> sizes = new();
			double Draw(double min, double max) => share * (min + (max - min) * random.NextDouble());

			// A map of fewer than four civs has no island for two. Each big
			// island is for two or three civs, and one in ten for four, so
			// long as the big islands take at most
			// ARCHIPELAGO_MOST_BIG_ISLAND_CIVS of the civs, leaving the rest
			// home islands.
			int big = civs < 4 ? 0 : Math.Max(1, (int)Math.Round(civs * ARCHIPELAGO_BIG_ISLANDS_PER_CIV));
			int bigCivsLeft = (int)(civs * ARCHIPELAGO_MOST_BIG_ISLAND_CIVS);
			int civsLeft = civs;
			for (int i = 0; i < big && bigCivsLeft >= 2; ++i) {
				double roll = random.NextDouble();
				int islandCivs = Math.Min(bigCivsLeft, roll < .1 ? 4 : roll < .55 ? 3 : 2);
				sizes.Add(islandCivs * Draw(ARCHIPELAGO_BIG_ISLAND_MIN, ARCHIPELAGO_BIG_ISLAND_MAX));
				civsLeft -= islandCivs;
				bigCivsLeft -= islandCivs;
			}
			for (int i = 0; i < civsLeft; ++i) {
				sizes.Add(Draw(ARCHIPELAGO_HOME_ISLAND_MIN, ARCHIPELAGO_HOME_ISLAND_MAX));
			}
			int settling = (int)Math.Round(civs * ARCHIPELAGO_SETTLING_ISLANDS_PER_CIV);
			for (int i = 0; i < settling; ++i) {
				sizes.Add(Draw(ARCHIPELAGO_SETTLING_ISLAND_MIN, ARCHIPELAGO_SETTLING_ISLAND_MAX));
			}
			// If the islands so far need more land than there is, they are
			// all made smaller to fit: planning more makes the sea rise
			// over the smaller islands' domes, so they come out as nothing.
			double planned = landTiles * ARCHIPELAGO_ISLAND_LAND_SHARE;
			if (sizes.Sum() > planned) {
				double scale = planned / sizes.Sum();
				for (int i = 0; i < sizes.Count; ++i) {
					sizes[i] *= scale;
				}
			}
			double left = planned - sizes.Sum();
			while (left >= share * ARCHIPELAGO_SMALL_ISLAND_MIN) {
				double size = Math.Min(left, Draw(ARCHIPELAGO_SMALL_ISLAND_MIN, ARCHIPELAGO_SMALL_ISLAND_MAX));
				sizes.Add(size);
				left -= size;
			}
			return sizes;
		}

		// Plans the islands of an archipelago. Civ3's archipelago is many
		// small and medium islands with no continent among them, where at
		// most a few civs share an island (see MapIsAcceptable). Noise alone
		// doesn't make that: it makes dozens of specks and a few big
		// landmasses that run into each other, so the shape check almost
		// never passed. So the islands are planned, as domes the noise is
		// blended with (HeightMap.RaiseDomes), of the sizes
		// ArchipelagoIslandSizes picks, in all most of the land. They are
		// spread over a jittered hexagonal grid, away from the poles, with
		// some places on the grid left empty and the big islands kept
		// apart, and come in different lengths.
		private static List<HeightMap.Dome> ArchipelagoIslands(WorldCharacteristics wc, Random random) {
			// A world size without civs (as in some tests) gets as many
			// islands as a Standard world's 8 civs on its 5000 tiles would.
			int civs = wc.worldSize.numberOfCivs > 0 ? wc.worldSize.numberOfCivs : Math.Max(1, wc.worldSize.width * wc.worldSize.height / 2 / 625);
			double share = ExpectedLandTiles(wc) / (double)civs;
			List<double> sizes = ArchipelagoIslandSizes(civs, ExpectedLandTiles(wc), random);
			int count = sizes.Count;

			double width = wc.worldSize.width;
			double bandTop = wc.worldSize.height * .08;
			double bandHeight = wc.worldSize.height * .84;
			int places = (int)Math.Ceiling(count * 1.15);
			double spacing = Math.Sqrt(width * bandHeight / (places * .866));
			int columns, rows;
			while (true) {
				columns = Math.Max(1, (int)(width / spacing));
				rows = Math.Max(1, (int)(bandHeight / (spacing * .866)));
				if (columns * rows >= places) {
					break;
				}
				spacing *= .95;
			}
			double columnWidth = width / columns;
			double rowHeight = bandHeight / rows;
			double neighborDistance = Math.Min(columnWidth, Math.Sqrt(columnWidth * columnWidth / 4 + rowHeight * rowHeight));
			double jitter = neighborDistance * .3;

			List<(double x, double y)> grid = new();
			for (int row = 0; row < rows; ++row) {
				for (int column = 0; column < columns; ++column) {
					double x = (column + .5 + (row % 2 == 1 ? .5 : 0)) * columnWidth;
					double y = bandTop + (row + .5) * rowHeight;
					grid.Add((x, y));
				}
			}

			List<HeightMap.Dome> domes = new();
			List<(double x, double y)> bigPlaces = new();
			foreach (double size in sizes) {
				if (grid.Count == 0) {
					break;
				}
				// A big island isn't put next to another, where the two
				// would squeeze each other (see HeightMap.RaiseDomes), if
				// there is a place for it that isn't.
				bool isBig = size >= share;
				int pick = random.Next(grid.Count);
				if (isBig) {
					List<int> apart = Enumerable.Range(0, grid.Count)
						.Where(i => bigPlaces.All(p => WrappedDistance(grid[i], p, width) > neighborDistance * 1.5)).ToList();
					if (apart.Count > 0) {
						pick = apart[random.Next(apart.Count)];
					}
				}
				(double x, double y) = grid[pick];
				grid.RemoveAt(pick);
				if (isBig) {
					bigPlaces.Add((x, y));
				}
				x += (random.NextDouble() * 2 - 1) * jitter;
				y += (random.NextDouble() * 2 - 1) * jitter;

				// A tile takes up two units of map coordinates (only every
				// other coordinate is a tile), so a disc of radius r holds
				// pi r^2 / 2 tiles. The domes' edges end up under water, so
				// they are made wider than the islands. A big island is
				// long, to have room for its civs the world size's distance
				// between civs apart, and the more civs, the longer.
				double radius = Math.Sqrt(2 * size / Math.PI) + ARCHIPELAGO_DOME_MARGIN;
				double stretch = isBig ? 1.5 + size / share * .5 + random.NextDouble() : 1 + 1.5 * random.NextDouble();
				domes.Add(new HeightMap.Dome((x + width) % width, y, radius, stretch, random.NextDouble() * Math.PI));
			}
			return domes;
		}

		// The distance between two points in map coordinates, around the
		// map's east-west wrap.
		private static double WrappedDistance((double x, double y) a, (double x, double y) b, double width) {
			double dx = Math.Abs(a.x - b.x);
			dx = Math.Min(dx, width - dx);
			return Math.Sqrt(dx * dx + (a.y - b.y) * (a.y - b.y));
		}

		private static GameMap ToLandAndWaterGameMap(WorldCharacteristics wc, HeightMap hm, WorldCharacteristics.OceanCoverage oceanCoverage) {
			TerrainType grassland = wc.terrainTypes.Find(x => x.Key == "grassland");
			TerrainType coast = wc.terrainTypes.Find(x => x.Key == "coast");
			TerrainType sea = wc.terrainTypes.Find(x => x.Key == "sea");
			TerrainType ocean = wc.terrainTypes.Find(x => x.Key == "ocean");

			// Set up the game map with basic land and water tiles.
			GameMap m = new GameMap();
			m.numTilesTall = hm.mapHeight;
			m.numTilesWide = hm.mapWidth;
			m.wrapHorizontally = hm.wrapX;
			m.wrapVertically = hm.wrapY;
			m.optimalNumberOfCities = wc.worldSize.optimalNumberOfCities;
			m.techRate = wc.worldSize.techRate;

			ID.Factory factory = new();
			// Civ3's maps come out with a few points more water than the setting
			// says (a 70% map is about 74% water), and with relatively more
			// deep ocean, so match that.
			int waterPercent = (int)oceanCoverage + EXTRA_WATER_PERCENT;
			int seaLevel = hm.FindSeaLevel(waterPercent);
			int mediumWaterLevel = hm.FindSeaLevel((int)(waterPercent * .92));
			int deepWaterLevel = hm.FindSeaLevel((int)(waterPercent * .78));

			for (int Y = 0; Y < m.numTilesTall; Y++) {
				for (int X = Y % 2; X < m.numTilesWide; X += 2) {
					Tile newTile = new Tile(factory.CreateID("tile"));
					newTile.XCoordinate = X;
					newTile.YCoordinate = Y;

					int height = hm.GetHeight(X, Y);
					if (height > seaLevel) {
						newTile.baseTerrainType = grassland;
					} else if (height > mediumWaterLevel) {
						newTile.baseTerrainType = coast;
						newTile.overlayTerrainType = coast;
					} else if (height > deepWaterLevel) {
						newTile.baseTerrainType = sea;
						newTile.overlayTerrainType = sea;
					} else {
						newTile.baseTerrainType = ocean;
						newTile.overlayTerrainType = ocean;
					}
					m.tiles.Add(newTile);
				}
			}

			// Calculate neighbors and continents.
			m.computeNeighbors();
			m.recomputeContinents();

			return m;
		}

		private const int EXTRA_WATER_PERCENT = 4;

		private static double GetNoiseScale(WorldCharacteristics.Landform landform) {
			return landform switch {
				WorldCharacteristics.Landform.Archipelago => 0.2,
				WorldCharacteristics.Landform.Continents => 0.05,
				WorldCharacteristics.Landform.Pangaea => 0.02,
				_ => throw new ArgumentOutOfRangeException(nameof(landform), landform, null),
			};
		}

		private static bool MapIsAcceptable(WorldCharacteristics wc, GameMap m) {
			WorldCharacteristics.Landform landform = wc.landform;
			// The land the map is made with, after the extra water. Leaving
			// that out asked for more land than there is, which an 80% water
			// pangaea's largest landmass almost never had.
			int expectedLandTiles = ExpectedLandTiles(wc);

			// Count the tiles that are too close to the poles.
			int tilesInTopOrBottom10Percent = 0;
			foreach (Tile t in m.tiles) {
				if (!t.IsLand()) {
					continue;
				}
				if (t.YCoordinate <= .1 * m.numTilesTall || t.YCoordinate >= .9 * m.numTilesTall) {
					++tilesInTopOrBottom10Percent;
				}
			}

			if (landform == WorldCharacteristics.Landform.Continents) {
				// Ensure that we have at least 3 land continents and that the
				// largest is at most 1.3 times the size of the second largest.
				// This should promote cases where we have 2 large land masses.
				List<HashSet<Tile>> landContinents = m.continents.Where(x => x.First().IsLand()).ToList();
				if (landContinents.Count < 3) {
					return false;
				}

				if (landContinents[0].Count > 1.3 * landContinents[1].Count) {
					return false;
				}

				if (tilesInTopOrBottom10Percent > .1 * expectedLandTiles) {
					return false;
				}

				return true;
			}

			if (landform == WorldCharacteristics.Landform.Pangaea) {
				// Ensure that the largest landmass has at least 80% of the land.
				List<HashSet<Tile>> landContinents = m.continents.Where(x => x.First().IsLand()).ToList();
				if (landContinents[0].Count < .8 * expectedLandTiles) {
					return false;
				}

				// Don't allow maps where the land clumps at the north and south
				// poles.
				if (tilesInTopOrBottom10Percent > .2 * expectedLandTiles) {
					return false;
				}

				return true;
			}

			if (landform == WorldCharacteristics.Landform.Archipelago) {
				// An archipelago is many islands. An island may be any size
				// and hold any number of civs (owner's decision,
				// 2026-10-12: "There is no arbitrary limit"). An island
				// holds as many civs as it has tilesPerPlayer tiles.
				List<HashSet<Tile>> landContinents = m.continents.Where(x => x.First().IsLand()).ToList();
				int civs = wc.worldSize.numberOfCivs;
				int tilesPerPlayer = ArchipelagoTilesPerPlayer(landContinents.Sum(x => x.Count), civs);

				// Ensure that we have more islands than players.
				if (landContinents.Count < civs * 1.2) {
					return false;
				}

				// Make sure that there are enough islands that can hold a
				// player for every player.
				int playersLeft = civs;
				foreach (var continent in landContinents) {
					playersLeft -= continent.Count / tilesPerPlayer;
				}
				if (playersLeft > 0) {
					return false;
				}

				return true;
			}

			return true;
		}

		// The share of hills and mountains that lie in highlands; the rest
		// are scattered.
		private const double HIGHLAND_SHARE = .7;

		// The share of mountains scattered on their own, away from the
		// highlands.
		private const double LONE_MOUNTAIN_SHARE = .2;

		// Adds hills and mountains the way Civ3 maps have them. Most lie in
		// highlands: irregular belts two or three tiles wide where hills and
		// mountains mix, with the mountains clumped along the middle. The
		// rest are scattered over the land on their own or in twos and
		// threes, so that most land is within a couple of tiles of a hill.
		private static void AddHillsAndMountains(WorldCharacteristics wc, GameMap m) {
			Random random = new Random(wc.mapSeed + 0xface);

			TerrainType hills = wc.terrainTypes.Find(x => x.Key == "hills");
			TerrainType mountains = wc.terrainTypes.Find(x => x.Key == "mountains");
			TerrainType volcano = wc.terrainTypes.Find(x => x.Key == "volcano");

			// The share of land that becomes hills or mountains, and the share
			// of those that are mountains, in percent. Older worlds are more
			// worn down. These match Civ3's own maps; the cap on how many can
			// share a 9x9 square (see CapHillsAndMountainsPerArea) then thins
			// out the densest highlands.
			double hillyPercent, mountainPercent;

			// The percentage of mountains that will become volcanos.
			int volcanoPercent;

			switch (wc.age) {
				case WorldCharacteristics.Age.Billion_3:
					hillyPercent = 33;
					mountainPercent = 45;
					volcanoPercent = 6;
					break;
				case WorldCharacteristics.Age.Billion_4:
					hillyPercent = 21;
					mountainPercent = 41;
					volcanoPercent = 3;
					break;
				case WorldCharacteristics.Age.Billion_5:
					hillyPercent = 14;
					mountainPercent = 41;
					volcanoPercent = 2;
					break;
				default:
					throw new Exception($"Unknown age: {wc.age}");
			}

			List<Tile> land = m.tiles.Where(t => t.IsLand()).ToList();
			if (land.Count == 0) {
				return;
			}

			// Some worlds are more rugged than others.
			hillyPercent *= .8 + .4 * random.NextDouble();
			mountainPercent *= .85 + .3 * random.NextDouble();
			int hillyCount = (int)(land.Count * hillyPercent / 100);
			int mountainCount = (int)(hillyCount * mountainPercent / 100);
			int loneMountainCount = (int)(mountainCount * LONE_MOUNTAIN_SHARE);

			// Lay out highlands from random spots until they've used their
			// share, then make the tiles most suited to it mountains.
			int highlandCount = (int)(hillyCount * HIGHLAND_SHARE);
			Dictionary<Tile, double> highland = new();
			for (int attempts = 0; highland.Count < highlandCount && attempts < land.Count; ++attempts) {
				Tile start = land[random.Next(land.Count)];
				if (!highland.ContainsKey(start)) {
					AddHighland(start, highland, highlandCount, random);
				}
			}
			int highlandMountains = Math.Min(mountainCount - loneMountainCount, highland.Count);
			int placed = 0;
			foreach (Tile t in highland.Keys.OrderByDescending(t => highland[t])) {
				if (placed++ < highlandMountains) {
					t.overlayTerrainType = random.Next(100) < volcanoPercent ? volcano : mountains;
				} else {
					t.overlayTerrainType = hills;
				}
			}

			// Scatter the rest: mountains alone, hills alone or in small
			// clumps.
			int mountainsLeft = mountainCount - highlandMountains;
			int hillsLeft = hillyCount - highland.Count - mountainsLeft;
			List<Tile> flat = land.Where(t => !t.overlayTerrainType.isHilly()).ToList();
			random.Shuffle(CollectionsMarshal.AsSpan(flat));
			foreach (Tile t in flat) {
				if (mountainsLeft <= 0 && hillsLeft <= 0) {
					break;
				}
				if (t.overlayTerrainType.isHilly()) {
					continue;
				}
				// A lone mountain often has a hill beside it.
				bool mountain = random.Next(mountainsLeft + Math.Max(0, hillsLeft)) < mountainsLeft;
				if (mountain) {
					t.overlayTerrainType = random.Next(100) < volcanoPercent ? volcano : mountains;
					--mountainsLeft;
				} else {
					t.overlayTerrainType = hills;
					--hillsLeft;
				}
				int clumpChance = mountain ? LONE_MOUNTAIN_HILL_CHANCE : SCATTERED_CLUMP_CHANCE;
				Tile clump = t;
				while (hillsLeft > 0 && random.Next(100) < clumpChance) {
					List<Tile> next = clump.GetLandNeighbors().Where(n => !n.overlayTerrainType.isHilly()).ToList();
					if (next.Count == 0) {
						break;
					}
					clump = next[random.Next(next.Count)];
					clump.overlayTerrainType = hills;
					--hillsLeft;
				}
			}

			CapHillsAndMountainsPerArea(wc, land, hills, mountains, random);
		}

		// The most hills and mountains (and, of those, mountains) allowed in
		// any 9x9 square of tiles, so that highlands never pile up into a
		// wall of peaks.
		private const int MAX_HILLY_PER_AREA = 22;
		private const int MAX_MOUNTAINS_PER_AREA = 9;
		private const int AREA_RADIUS = 4;

		// How many other mountains a mountain may touch, and how many
		// mountains a 5x5 square may hold, vary over the map so that some
		// ranges are thin spines and others broader massifs.
		private const int MIN_MOUNTAIN_NEIGHBORS = 2;
		private const int MAX_MOUNTAIN_NEIGHBORS = 4;
		private const int MIN_MOUNTAINS_PER_SMALL_AREA = 4;
		private const int MAX_MOUNTAINS_PER_SMALL_AREA = 7;
		private const int SMALL_AREA_RADIUS = 2;

		// Keeps hills and mountains from piling up. Goes over them in a random
		// order, keeping each only if every 9x9 square it lies in stays
		// within the limits, and, for a mountain, if it keeps the range thin
		// enough there. A mountain that doesn't fit becomes a hill if that
		// fits. Whatever had to go is then put back elsewhere, preferably at
		// the end of a highland, lengthening it rather than widening it, so
		// the map keeps its share of hills and mountains.
		private static void CapHillsAndMountainsPerArea(WorldCharacteristics wc, List<Tile> land, TerrainType hills, TerrainType mountains, Random random) {
			HeightMap thicknessNoise = new(seed: wc.mapSeed + 0x7417, width:wc.worldSize.width, height:wc.worldSize.height, scale:.15, forceLowPointsAtPoles:false);
			double[] thickness = BiomeClimate.NoisePercentiles(thicknessNoise);
			double Thickness(Tile t) => thickness[thicknessNoise.GetHeight(t.XCoordinate, t.YCoordinate)];

			// Each tile's limit, picked once: mostly from the noise, but now
			// and then a tile is allowed one more or one fewer.
			Dictionary<Tile, int> neighborLimits = new();
			int NeighborLimit(Tile t) {
				if (!neighborLimits.TryGetValue(t, out int limit)) {
					limit = MIN_MOUNTAIN_NEIGHBORS + (int)(Thickness(t) * (MAX_MOUNTAIN_NEIGHBORS - MIN_MOUNTAIN_NEIGHBORS + 1));
					if (random.Next(100) < 25) {
						limit += random.Next(2) == 0 ? -1 : 1;
					}
					limit = Math.Clamp(limit, MIN_MOUNTAIN_NEIGHBORS, MAX_MOUNTAIN_NEIGHBORS + 1);
					neighborLimits[t] = limit;
				}
				return limit;
			}
			int SmallAreaLimit(Tile center) {
				return MIN_MOUNTAINS_PER_SMALL_AREA + (int)(Thickness(center) * (MAX_MOUNTAINS_PER_SMALL_AREA - MIN_MOUNTAINS_PER_SMALL_AREA + 1));
			}

			// The counts in the square centered on each tile. A tile lies in
			// exactly the squares centered on the tiles in its own square.
			Dictionary<Tile, int> hillyCount = new();
			Dictionary<Tile, int> mountainCount = new();
			Dictionary<Tile, int> smallAreaMountainCount = new();
			HashSet<Tile> keptMountains = new();
			int MountainNeighbors(Tile t) => t.neighbors.Values.Count(keptMountains.Contains);

			bool Fits(Tile t, bool mountain) {
				List<Tile> squares = t.GetTilesWithinTileSquare(AREA_RADIUS);
				if (!squares.All(c => hillyCount.GetValueOrDefault(c) < MAX_HILLY_PER_AREA)) {
					return false;
				}
				if (!mountain) {
					return true;
				}
				if (!squares.All(c => mountainCount.GetValueOrDefault(c) < MAX_MOUNTAINS_PER_AREA)) {
					return false;
				}
				if (!t.GetTilesWithinTileSquare(SMALL_AREA_RADIUS).All(c => smallAreaMountainCount.GetValueOrDefault(c) < SmallAreaLimit(c))) {
					return false;
				}
				// Neither this mountain nor the ones beside it may end up
				// touching more mountains than they're allowed.
				List<Tile> beside = t.neighbors.Values.Where(keptMountains.Contains).ToList();
				return beside.Count <= NeighborLimit(t) && beside.All(n => MountainNeighbors(n) + 1 <= NeighborLimit(n));
			}

			void Keep(Tile t, bool mountain) {
				foreach (Tile c in t.GetTilesWithinTileSquare(AREA_RADIUS)) {
					hillyCount[c] = hillyCount.GetValueOrDefault(c) + 1;
					if (mountain) {
						mountainCount[c] = mountainCount.GetValueOrDefault(c) + 1;
					}
				}
				if (mountain) {
					foreach (Tile c in t.GetTilesWithinTileSquare(SMALL_AREA_RADIUS)) {
						smallAreaMountainCount[c] = smallAreaMountainCount.GetValueOrDefault(c) + 1;
					}
					keptMountains.Add(t);
				}
			}

			List<Tile> hilly = WalkOrder(land.Where(t => t.overlayTerrainType.isHilly()).ToList(), random);
			int mountainsToReplace = 0, hillyToReplace = 0;
			foreach (Tile t in hilly) {
				bool mountain = t.overlayTerrainType.IsMountains || t.overlayTerrainType.IsVolcano;
				if (mountain && Fits(t, true)) {
					Keep(t, true);
					continue;
				}
				if (mountain) {
					++mountainsToReplace;
				}
				if (Fits(t, false)) {
					t.overlayTerrainType = hills;
					Keep(t, false);
				} else {
					t.overlayTerrainType = TerrainType.NONE;
					++hillyToReplace;
				}
			}

			// Put back what had to go: first on flat land touching exactly one
			// hill or mountain, extending a highland at its end (now and then
			// at its side), then anywhere there's room. A mountain goes back
			// beside a mountain where it can, extending the range.
			List<Tile> flat = land.Where(t => !t.overlayTerrainType.isHilly()).ToList();
			foreach (bool extending in new[] { true, true }) {
				random.Shuffle(CollectionsMarshal.AsSpan(flat));
				foreach (Tile t in flat) {
					if (hillyToReplace <= 0 && mountainsToReplace <= 0) {
						return;
					}
					if (t.overlayTerrainType.isHilly()) {
						continue;
					}
					List<Tile> hillyBeside = t.neighbors.Values.Where(n => n != Tile.NONE && n.overlayTerrainType.isHilly()).ToList();
					if (extending && !(hillyBeside.Count == 1 || hillyBeside.Count == 2 && random.Next(100) < 25)) {
						continue;
					}
					bool nextToMountain = hillyBeside.Any(keptMountains.Contains);
					if (mountainsToReplace > 0 && (nextToMountain || !extending) && Fits(t, true)) {
						t.overlayTerrainType = mountains;
						Keep(t, true);
						--mountainsToReplace;
						--hillyToReplace;
					} else if (hillyToReplace > 0 && Fits(t, false)) {
						t.overlayTerrainType = hills;
						Keep(t, false);
						--hillyToReplace;
					}
				}
			}
		}

		// Orders the tiles so each highland is gone through as one winding
		// walk from a random tile, each step to a random unvisited neighbor,
		// backing up when stuck. Kept in that order, a range stays connected
		// along its length instead of breaking into scattered peaks.
		private static List<Tile> WalkOrder(List<Tile> tiles, Random random) {
			HashSet<Tile> remaining = tiles.ToHashSet();
			random.Shuffle(CollectionsMarshal.AsSpan(tiles));
			List<Tile> result = new();
			foreach (Tile start in tiles) {
				if (!remaining.Remove(start)) {
					continue;
				}
				Stack<Tile> path = new();
				path.Push(start);
				result.Add(start);
				while (path.Count > 0) {
					List<Tile> next = path.Peek().neighbors.Values.Where(remaining.Contains).ToList();
					if (next.Count == 0) {
						path.Pop();
						continue;
					}
					Tile step = next[random.Next(next.Count)];
					remaining.Remove(step);
					result.Add(step);
					path.Push(step);
				}
			}
			return result;
		}

		// How often a scattered hill grows into a clump by another tile, and
		// how often a lone mountain gets a hill beside it.
		private const int SCATTERED_CLUMP_CHANCE = 50;
		private const int LONE_MOUNTAIN_HILL_CHANCE = 40;

		// Lays out a highland starting at the given tile: a meandering walk
		// with some of the tiles beside it, until the highlands reach
		// maxCount tiles. Each tile gets a score for how suited it is to be a
		// mountain: the walk itself is better suited than its sides, and in
		// some highlands, which are just hill country, nothing is.
		private static void AddHighland(Tile start, Dictionary<Tile, double> highland, int maxCount, Random random) {
			double scale = random.Next(100) < HILL_COUNTRY_CHANCE ? .2 : 1;
			void Add(Tile t, double score) {
				if (highland.Count < maxCount && t != Tile.NONE && t.IsLand() && !highland.ContainsKey(t)) {
					highland[t] = scale * (score + 2 * random.NextDouble());
				}
			}

			// Now and then a highland runs on much further.
			int length = HIGHLAND_MIN_LENGTH + random.Next(HIGHLAND_MAX_LENGTH - HIGHLAND_MIN_LENGTH + 1);
			if (random.Next(100) < LONG_HIGHLAND_CHANCE) {
				length *= 3;
			}
			int dir = random.Next(8);
			Tile t = start;
			for (int i = 0; i < length; ++i) {
				Add(t, .5);
				foreach (Tile n in t.neighbors.Values) {
					if (random.Next(100) < HIGHLAND_WIDTH_CHANCE) {
						Add(n, 0);
					}
				}

				// Wander, mostly keeping the same heading. At the water, turn
				// along the shore if possible.
				int turn = random.Next(100);
				if (turn < 20) {
					dir = (dir + 7) % 8;
				} else if (turn < 40) {
					dir = (dir + 1) % 8;
				}
				Tile next = Tile.NONE;
				foreach (int d in new[] { 0, 1, 7, 2, 6 }) {
					Tile n = t.neighbors[(TileDirection)((dir + d) % 8)];
					if (n != Tile.NONE && n.IsLand()) {
						next = n;
						dir = (dir + d) % 8;
						break;
					}
				}
				if (next == Tile.NONE) {
					break;
				}
				t = next;
			}
		}

		private const int HIGHLAND_MIN_LENGTH = 2;
		private const int HIGHLAND_MAX_LENGTH = 8;
		private const int LONG_HIGHLAND_CHANCE = 15;

		// The chance of each tile beside the walk joining the highland.
		private const int HIGHLAND_WIDTH_CHANCE = 45;

		// The chance of a highland being hill country.
		private const int HILL_COUNTRY_CHANCE = 25;

		// Assigns a biome id to each land tile, creating potential biome zones
		// that will later be assigned to actual biomes.
		private static void SplitContinentsIntoPotentialBiomes(WorldCharacteristics wc, GameMap m) {
			// Randomize the order we iterate through the tiles.
			Random rand = new(wc.mapSeed + 0xba11);
			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			int nextBiomeId = 0;
			foreach (int tileIndex in tileIndicies) {
				Tile t = m.tiles[tileIndex];

				// Skip tile that have been handled or tiles that already have
				// their final assignment.
				if (t.biomeRegion != -1 || !t.IsLand() || t.overlayTerrainType.isHilly()) {
					continue;
				}

				// Do a random walk to find tiles that haven't been assigned and
				// then we "bump out" in each direction from those tiles by one,
				// with some probability.
				HashSet<Tile> updated = RandomWalkAndAssignBiomeId(rand, t, walkLength:16, biomeId:nextBiomeId);
				SemiRandomFloodFillExpansion(rand, updated, nextBiomeId);
				++nextBiomeId;
			}

			// Fix up the biome regions to avoid 1 tile biomes.
			foreach (int tileIndex in tileIndicies) {
				Tile t = m.tiles[tileIndex];
				if (!t.IsLand() || t.overlayTerrainType.isHilly()) {
					continue;
				}

				Tile nw = t.neighbors[TileDirection.NORTHWEST];
				Tile sw = t.neighbors[TileDirection.SOUTHWEST];
				Tile ne = t.neighbors[TileDirection.NORTHEAST];
				Tile se = t.neighbors[TileDirection.SOUTHEAST];

				if ((t.biomeRegion == nw.biomeRegion && nw.biomeRegion != -1)
					|| (t.biomeRegion == ne.biomeRegion && ne.biomeRegion != -1)
					|| (t.biomeRegion == sw.biomeRegion && sw.biomeRegion != -1)
					|| (t.biomeRegion == se.biomeRegion && se.biomeRegion != -1)) {
					continue;
				}

				List<Tile> validNeighbors = new();
				if (nw != Tile.NONE && nw.biomeRegion != -1) validNeighbors.Add(nw);
				if (sw != Tile.NONE && sw.biomeRegion != -1) validNeighbors.Add(sw);
				if (ne != Tile.NONE && ne.biomeRegion != -1) validNeighbors.Add(ne);
				if (se != Tile.NONE && se.biomeRegion != -1) validNeighbors.Add(se);

				// This could be an island.
				if (validNeighbors.Count == 0) {
					continue;
				}

				t.biomeRegion = validNeighbors[rand.Next(validNeighbors.Count)].biomeRegion;
			}
		}

		private static HashSet<Tile> RandomWalkAndAssignBiomeId(Random rand, Tile t, int walkLength, int biomeId) {
			HashSet<Tile> result = new();

			for (int i = 0; i < walkLength; ++i) {
				if (t.biomeRegion != -1 || !t.IsLand() || t.overlayTerrainType.isHilly()) {
					continue;
				}
				t.biomeRegion = biomeId;
				result.Add(t);

				TileDirection[] options = {
					TileDirection.NORTH,
					TileDirection.SOUTH,
					TileDirection.EAST,
					TileDirection.WEST,
				};
				rand.Shuffle<TileDirection>(options);

				// Try to move in a random direction to a tile that hasn't been
				// processed.
				bool moved = false;
				foreach (TileDirection dir in options) {
					Tile neighbor = t.neighbors[dir];
					if (neighbor.biomeRegion != -1 || !neighbor.IsLand() || neighbor.overlayTerrainType.isHilly() || neighbor == Tile.NONE) {
						continue;
					}
					t = neighbor;
					moved = true;
					break;
				}

				if (!moved) {
					return result;
				}
			}
			return result;
		}

		private static void SemiRandomFloodFillExpansion(Random rand, HashSet<Tile> tiles, int biomeId) {
			List<Tile> toUpdate = new();

			foreach (Tile t in tiles) {
				foreach (Tile neighbor in t.neighbors.Values) {
					if (neighbor == Tile.NONE || neighbor.biomeRegion != -1 || !neighbor.IsLand() || neighbor.overlayTerrainType.isHilly()) {
						continue;
					}

					if (rand.Next(100) < 50) {
						toUpdate.Add(neighbor);
					}
				}
			}

			foreach (Tile t in toUpdate) {
				t.biomeRegion = biomeId;
			}
		}

		private static void AssignBiomes(WorldCharacteristics wc, GameMap m) {
			TerrainType tundra = wc.terrainTypes.Find(x => x.Key == "tundra");
			TerrainType grassland = wc.terrainTypes.Find(x => x.Key == "grassland");
			TerrainType desert = wc.terrainTypes.Find(x => x.Key == "desert");
			TerrainType plains = wc.terrainTypes.Find(x => x.Key == "plains");

			BiomeClimate climate = new(wc, m);

			// Group the tiles by biome region once, so filling a region doesn't
			// need to scan the whole map. Biome regions don't change here.
			Dictionary<int, List<Tile>> tilesByBiomeRegion = GroupTilesByBiomeRegion(m);

			foreach ((int biomeRegion, List<Tile> tiles) in tilesByBiomeRegion) {
				// Water and hills aren't part of any biome region.
				if (biomeRegion == -1) {
					continue;
				}

				// Each region takes the average climate of its tiles. Since the
				// climate changes smoothly across the map, neighbouring regions
				// end up with similar climates and so similar terrain.
				double latitude = 0, moisture = 0;
				foreach (Tile t in tiles) {
					latitude += climate.EffectiveLatitude(t);
					moisture += climate.Moisture(t);
				}
				latitude /= tiles.Count;
				moisture /= tiles.Count;

				TerrainType biome;
				if (latitude >= BiomeClimate.TUNDRA_LATITUDE) {
					biome = tundra;
				} else if (moisture < .31 && latitude < 38 || moisture < .05) {
					// Deserts mostly sit in the dry subtropics, with the odd
					// cold desert further out where it's very dry.
					biome = desert;
				} else if (moisture < .425) {
					biome = plains;
				} else {
					biome = grassland;
				}
				FillBiomeRegion(tilesByBiomeRegion, tiles[0], biome, biome);
			}

			// Deserts fade into grassland through plains, so turn grassland
			// touching a desert into plains.
			List<Tile> desertEdges = new();
			foreach (Tile t in m.tiles) {
				if (t.baseTerrainType.Key != "grassland" || t.overlayTerrainType.isHilly()) {
					continue;
				}
				if (t.neighbors.Values.Any(n => n != Tile.NONE && n.baseTerrainType.Key == "desert")) {
					desertEdges.Add(t);
				}
			}
			foreach (Tile t in desertEdges) {
				t.baseTerrainType = plains;
				t.overlayTerrainType = plains;
			}

			// Do one more pass to ensure that tundra tiles never border land
			// tiles that aren't grassland - the terrain textures don't support
			// anything else.
			foreach (Tile t in m.tiles) {
				if (t.baseTerrainType.Key != "tundra") {
					continue;
				}

				foreach (Tile neighbor in t.neighbors.Values) {
					// We can border water, other tundra, and hills/mountains
					// just fine.
					if (neighbor.baseTerrainType.Key == "tundra" || !neighbor.IsLand() || neighbor == Tile.NONE || neighbor.overlayTerrainType.isHilly()) {
						continue;
					}

					// But anything else has to be grassland.
					neighbor.baseTerrainType = grassland;
					neighbor.overlayTerrainType = grassland;
				}
			}
		}

		// The climate of a map, used to pick biomes and vegetation. Temperature
		// comes mostly from latitude, with a blobby noise map nudging the bands
		// around so their edges aren't straight lines. Moisture has its own
		// noise map, shaped by latitude like on Earth: wet at the equator, dry
		// in the subtropics (the Sahara, the Outback), and middling beyond.
		private class BiomeClimate {
			// Land at or above this effective latitude is tundra.
			public const double TUNDRA_LATITUDE = 70;

			private readonly GameMap map;
			private readonly HeightMap temperatureNoise;
			private readonly HeightMap moistureNoise;
			private readonly double[] temperaturePercentiles;
			private readonly double[] moisturePercentiles;

			// Cool worlds push the bands toward the equator and warm worlds
			// push them toward the poles.
			private readonly double latitudeShift;

			// Arid worlds are drier everywhere, wet worlds wetter.
			private readonly double moistureShift;

			public BiomeClimate(WorldCharacteristics wc, GameMap m) {
				map = m;

				// Without forceLowPointsAtPoles, since that would drag the
				// noise at the poles toward the middle of the range.
				temperatureNoise = new(seed: wc.mapSeed + 0xdad, width: wc.worldSize.width, height: wc.worldSize.height, scale: .4, forceLowPointsAtPoles: false);
				moistureNoise = new(seed: wc.mapSeed + 0x3e7, width: wc.worldSize.width, height: wc.worldSize.height, scale: .3, forceLowPointsAtPoles: false);
				temperaturePercentiles = NoisePercentiles(temperatureNoise);
				moisturePercentiles = NoisePercentiles(moistureNoise);

				latitudeShift = wc.temperature switch {
					WorldCharacteristics.Temperature.Cool => 8,
					WorldCharacteristics.Temperature.Temperate => 0,
					WorldCharacteristics.Temperature.Warm => -8,
					_ => throw new Exception($"Unknown temperature: {wc.temperature}"),
				};
				moistureShift = wc.climate switch {
					WorldCharacteristics.Climate.Arid => -.08,
					WorldCharacteristics.Climate.Normal => 0,
					WorldCharacteristics.Climate.Wet => .08,
					_ => throw new Exception($"Unknown climate: {wc.climate}"),
				};
			}

			// The tile's distance from the equator, in degrees.
			public double Latitude(Tile t) {
				double normalizedY = (double)t.YCoordinate / (map.numTilesTall - 1.0);
				return Math.Abs(90.0 - (normalizedY * 180.0));
			}

			// The latitude whose temperature this tile has: its real latitude,
			// moved by the temperature setting and up to 12 degrees of noise.
			public double EffectiveLatitude(Tile t) {
				double noise = temperaturePercentiles[temperatureNoise.GetHeight(t.XCoordinate, t.YCoordinate)];
				return Latitude(t) + latitudeShift + (noise - .5) * 24;
			}

			// Roughly 0 (dry) to 1 (wet), though latitude and climate can push
			// it a bit past either end.
			public double Moisture(Tile t) {
				double noise = moisturePercentiles[moistureNoise.GetHeight(t.XCoordinate, t.YCoordinate)];
				double latitude = EffectiveLatitude(t);
				double belts = .25 * Bump(latitude, 0, 12)
					- .35 * Bump(latitude, 25, 9)
					+ .1 * Bump(latitude, 50, 15);
				return noise + belts + moistureShift;
			}

			private static double Bump(double x, double centre, double width) {
				double d = (x - centre) / width;
				return Math.Exp(-d * d);
			}

			// Maps each noise value to the share of the map below it, so a
			// noise map can be read as values spread evenly between 0 and 1.
			public static double[] NoisePercentiles(HeightMap hm) {
				int[] counts = new int[256];
				for (int x = 0; x < hm.mapWidth; x++) {
					for (int y = 0; y < hm.mapHeight; y++) {
						++counts[hm.GetHeight(x, y)];
					}
				}

				double total = hm.mapWidth * hm.mapHeight;
				double[] result = new double[256];
				int below = 0;
				for (int i = 0; i < 256; i++) {
					result[i] = (below + counts[i] / 2.0) / total;
					below += counts[i];
				}
				return result;
			}
		}

		private static Dictionary<int, List<Tile>> GroupTilesByBiomeRegion(GameMap m) {
			Dictionary<int, List<Tile>> result = new();
			foreach (Tile t in m.tiles) {
				if (!result.TryGetValue(t.biomeRegion, out List<Tile> region)) {
					region = new List<Tile>();
					result[t.biomeRegion] = region;
				}
				region.Add(t);
			}
			return result;
		}

		private static void FillBiomeRegion(Dictionary<int, List<Tile>> tilesByBiomeRegion, Tile seed, TerrainType baseType, TerrainType overlayType) {
			foreach (Tile t in tilesByBiomeRegion[seed.biomeRegion]) {
				t.baseTerrainType = baseType;
				t.overlayTerrainType = overlayType;
			}
		}

		// Forest grows more readily on tundra than elsewhere: Civ3's maps have
		// a lot of taiga.
		private const double TUNDRA_FOREST_BONUS = .08;

		private static void AddVegetation(WorldCharacteristics wc, GameMap m) {
			TerrainType forest = wc.terrainTypes.Find(x => x.Key == "forest");
			TerrainType jungle = wc.terrainTypes.Find(x => x.Key == "jungle");
			TerrainType marsh = wc.terrainTypes.Find(x => x.Key == "marsh");

			BiomeClimate climate = new(wc, m);

			// Vegetation grows in patches: a blobby noise map sets how thick
			// it is in each area, and a finer one breaks that up into smaller
			// woods. A finer one again places the small marshes.
			HeightMap growthNoise = new(seed: wc.mapSeed + 0xabcde, width:wc.worldSize.width, height:wc.worldSize.height, scale:.5, forceLowPointsAtPoles:false);
			HeightMap patchNoise = new(seed: wc.mapSeed + 0x9a7c, width:wc.worldSize.width, height:wc.worldSize.height, scale:1.0, forceLowPointsAtPoles:false);
			double[] growthPercentiles = BiomeClimate.NoisePercentiles(growthNoise);
			double[] patchPercentiles = BiomeClimate.NoisePercentiles(patchNoise);
			HeightMap marshNoise = new(seed: wc.mapSeed + 0x3a75, width:wc.worldSize.width, height:wc.worldSize.height, scale:1.2, forceLowPointsAtPoles:false);
			int marshLevel = marshNoise.FindSeaLevel(94);

			// Jungle only grows near the equator. Wetter worlds grow more of
			// everything, and spread jungle further out.
			double jungleLatitude, jungleThreshold, forestThreshold;
			switch (wc.climate) {
				case WorldCharacteristics.Climate.Wet:
					jungleLatitude = 20;
					jungleThreshold = .64;
					forestThreshold = .69;
					break;
				case WorldCharacteristics.Climate.Normal:
					jungleLatitude = 16;
					jungleThreshold = .68;
					forestThreshold = .73;
					break;
				case WorldCharacteristics.Climate.Arid:
					jungleLatitude = 12;
					jungleThreshold = .74;
					forestThreshold = .78;
					break;
				default:
					throw new Exception($"Unknown climate: {wc.climate}");
			}

			Random rand = new(wc.mapSeed + 0xe5ab);
			foreach (Tile t in m.tiles) {
				// Skip water tiles and tiles that are hilly.
				if (!t.IsLand() || t.overlayTerrainType.isHilly()) {
					continue;
				}

				// Patches of growth, with more of it where it's wet, and a bit
				// of randomness per tile so the patches have ragged edges.
				double moisture = climate.Moisture(t);
				double growth = .35 * growthPercentiles[growthNoise.GetHeight(t.XCoordinate, t.YCoordinate)]
					+ .65 * patchPercentiles[patchNoise.GetHeight(t.XCoordinate, t.YCoordinate)]
					+ (moisture - .5) * .25
					+ (rand.NextDouble() - .5) * .1;
				double latitude = climate.EffectiveLatitude(t);
				string terrain = t.overlayTerrainType.Key;

				// Jungle thins out toward the edge of its band instead of
				// stopping at a line.
				double jungleFade = Math.Max(0, latitude) / jungleLatitude;
				if (terrain == "grassland" && latitude < jungleLatitude && growth > jungleThreshold + .12 * jungleFade * jungleFade) {
					// We only put jungle on grassland.
					t.overlayTerrainType = jungle;
				} else if ((terrain == "grassland" || terrain == "plains" || terrain == "tundra")
						&& growth > forestThreshold - (terrain == "tundra" ? TUNDRA_FOREST_BONUS : 0)) {
					// Forests grow on anything but desert, including the
					// taiga on the tundra.
					t.overlayTerrainType = forest;
				} else if (terrain == "grassland" && moisture > .4 && marshNoise.GetHeight(t.XCoordinate, t.YCoordinate) >= marshLevel) {
					// We only put marsh on grassland.
					t.overlayTerrainType = marsh;
				}
			}
		}

		// The drainage threshold is picked so that about this share of the
		// land borders a river.
		private const int RIVER_LAND_PERCENT = 22;

		// Rivers flowing into the sea or a lake shorter than this many tiles
		// are thrown away, and so are tributaries shorter than this.
		private const int MIN_RIVER_LENGTH = 3;
		private const int MIN_TRIBUTARY_LENGTH = 2;

		// Rivers only form where at least this many tiles drain, so the
		// smallest islands get none.
		private const int MIN_RIVER_DRAINAGE = 3;

		// As in Civ3, rivers run from the highlands to the sea (or a lake),
		// and merge with each other as tributaries instead of forking. Every
		// land tile drains to a neighbor along the cheapest way to water,
		// where hills and mountains are costly to cross and noise makes the
		// way wander, so the land splits into basins. Rivers run where
		// enough of the land drains through, which makes them converge into
		// trees instead of running side by side to the coast.
		private static void AddRivers(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0xabc12);
			HeightMap hm = new(seed: wc.mapSeed + 0x21cba, width:wc.worldSize.width, height:wc.worldSize.height, scale:.1, forceLowPointsAtPoles: false);

			Dictionary<Tile, Tile> downstream = new();
			List<Tile> drainOrder = FindDrainage(m, hm, rand, downstream);

			// How many tiles drain through each tile, counting itself, and
			// how many steps it is from water.
			Dictionary<Tile, int> drainage = new();
			for (int i = drainOrder.Count - 1; i >= 0; --i) {
				Tile t = drainOrder[i];
				drainage[t] = drainage.GetValueOrDefault(t) + 1;
				drainage[downstream[t]] = drainage.GetValueOrDefault(downstream[t]) + drainage[t];
			}
			Dictionary<Tile, int> stepsToWater = new();
			foreach (Tile t in drainOrder) {
				stepsToWater[t] = stepsToWater.GetValueOrDefault(downstream[t]) + 1;
			}

			// Vary how much river each map gets by up to 2% of the land either
			// way, so maps don't all feel the same.
			int riverPerMille = RIVER_LAND_PERCENT * 10 + rand.Next(-20, 21);
			int target = m.tiles.Count(t => t.IsLand()) * riverPerMille / 1000;
			int drawSeed = rand.Next();

			// Lower thresholds give more and longer rivers. Find the highest
			// one that reaches the target, then keep whichever of it and the
			// one above it comes closer.
			List<int> thresholds = drainOrder.Select(t => drainage[t]).Where(d => d >= MIN_RIVER_DRAINAGE).Distinct().OrderByDescending(d => d).ToList();
			if (thresholds.Count == 0) {
				return;
			}
			Dictionary<int, int> riverLand = new();
			int Draw(int i) {
				if (!riverLand.TryGetValue(i, out int count)) {
					count = riverLand[i] = DrawRivers(m, downstream, drainage, stepsToWater, thresholds[i], new Random(drawSeed));
				}
				return count;
			}
			int lo = 0;
			int hi = thresholds.Count - 1;
			while (lo < hi) {
				int mid = (lo + hi) / 2;
				if (Draw(mid) >= target) {
					hi = mid;
				} else {
					lo = mid + 1;
				}
			}
			int best = lo;
			if (lo > 0 && target - Draw(lo - 1) < Draw(lo) - target) {
				best = lo - 1;
			}
			DrawRivers(m, downstream, drainage, stepsToWater, thresholds[best], new Random(drawSeed));
		}

		// Finds where each land tile drains to, with a priority flood from
		// all water across tile edges. Each land tile drains to the tile it
		// was reached from, so all of them drain to water without pits.
		// Returns the land tiles that reach water, downstream ones first.
		private static List<Tile> FindDrainage(GameMap m, HeightMap hm, Random rand, Dictionary<Tile, Tile> downstream) {
			// The cost of draining across each tile. Hills and mountains are
			// where rivers start and basins divide.
			Dictionary<Tile, int> stepCost = new();
			foreach (Tile t in m.tiles) {
				if (t.IsLand()) {
					int terrain = t.overlayTerrainType.IsMountains ? 150 : t.overlayTerrainType.isHilly() ? 80 : 0;
					stepCost[t] = 100 + terrain + hm.GetHeight(t.XCoordinate, t.YCoordinate) + rand.Next(100);
				}
			}

			Dictionary<Tile, int> cost = new();
			PriorityQueue<Tile, int> queue = new();
			foreach (Tile t in m.tiles) {
				if (!t.IsLand()) {
					cost[t] = 0;
					queue.Enqueue(t, 0);
				}
			}

			List<Tile> result = new();
			HashSet<Tile> done = new();
			while (queue.TryDequeue(out Tile t, out int c)) {
				if (!done.Add(t)) {
					continue;
				}
				if (t.IsLand()) {
					result.Add(t);
				}
				foreach (Tile neighbor in t.GetEdgeNeighbors()) {
					if (!neighbor.IsLand() || done.Contains(neighbor)) {
						continue;
					}
					int newCost = c + stepCost[neighbor];
					if (!cost.TryGetValue(neighbor, out int oldCost) || newCost < oldCost) {
						cost[neighbor] = newCost;
						downstream[neighbor] = t;
						queue.Enqueue(neighbor, newCost);
					}
				}
			}
			return result;
		}

		// Clears any rivers, then draws one along every tile that at least
		// the threshold number of tiles drain through. The longest rivers are
		// drawn first, then their tributaries down to where they join them.
		// Returns the number of land tiles bordering a river.
		private static int DrawRivers(GameMap m, Dictionary<Tile, Tile> downstream, Dictionary<Tile, int> drainage, Dictionary<Tile, int> stepsToWater,
				int threshold, Random rand) {
			foreach (Tile t in m.tiles) {
				t.riverNortheast = t.riverNorthwest = t.riverSoutheast = t.riverSouthwest = false;
			}

			// Rivers start where no river drains into them.
			HashSet<Tile> fed = new();
			foreach ((Tile t, int d) in drainage) {
				if (d >= threshold && t.IsLand()) {
					fed.Add(downstream[t]);
				}
			}
			List<Tile> heads = new();
			foreach (Tile t in m.tiles) {
				if (t.IsLand() && drainage.GetValueOrDefault(t) >= threshold && !fed.Contains(t)) {
					heads.Add(t);
				}
			}
			rand.Shuffle<Tile>(CollectionsMarshal.AsSpan(heads));

			// Run each river up to its source, along the branch most of the
			// land drains from, so that rivers start in the highlands instead
			// of where they get big enough.
			Dictionary<Tile, Tile> mainBranch = new();
			foreach ((Tile t, int d) in drainage) {
				if (t.IsLand() && (!mainBranch.TryGetValue(downstream[t], out Tile other) || drainage[other] < d)) {
					mainBranch[downstream[t]] = t;
				}
			}
			for (int i = 0; i < heads.Count; ++i) {
				while (mainBranch.TryGetValue(heads[i], out Tile up)) {
					heads[i] = up;
				}
			}
			heads = heads.OrderByDescending(t => stepsToWater[t]).ToList();

			HashSet<Tile> riverTiles = new();
			foreach (Tile head in heads) {
				// Follow the drainage until it reaches water or comes next to a
				// river already drawn, which it joins. The last tile is the
				// water or river tile it flows into.
				List<Tile> river = new();
				for (Tile t = head; ; t = downstream[t]) {
					if (!t.IsLand()) {
						river.Add(t);
						break;
					}
					if (TouchesAny(t, riverTiles)) {
						if (river.Count > 0) {
							river.Add(t);
							river.Add(JoinedRiverTile(t, downstream[t], riverTiles));
						}
						break;
					}
					river.Add(t);
				}
				if (river.Count == 0 || river.Count - 1 < (river[^1].IsLand() ? MIN_TRIBUTARY_LENGTH : MIN_RIVER_LENGTH)) {
					continue;
				}

				// Usually start the drawn river from the far side of its source
				// so it runs the full length of the tile, but not always.
				List<(int x, int y)> corners = null;
				if (rand.Next(100) < 60) {
					corners = TraceRiverCorners(m, river, fromFarSide: true);
				}
				corners ??= TraceRiverCorners(m, river, fromFarSide: false);
				if (corners == null) {
					continue;
				}

				for (int i = 0; i + 1 < river.Count; ++i) {
					riverTiles.Add(river[i]);
				}
				for (int i = 0; i + 1 < corners.Count; ++i) {
					setRiverFlags(m, corners[i], corners[i + 1]);
				}
			}
			return m.tiles.Count(t => t.IsLand() && t.BordersRiver());
		}

		// The river tile a river at t joins: the one it drains into if that
		// has a river, else one sharing an edge with t, else one touching it.
		private static Tile JoinedRiverTile(Tile t, Tile next, HashSet<Tile> riverTiles) {
			if (riverTiles.Contains(next)) {
				return next;
			}
			foreach (Tile neighbor in t.GetEdgeNeighbors()) {
				if (riverTiles.Contains(neighbor)) {
					return neighbor;
				}
			}
			return t.neighbors.Values.First(riverTiles.Contains);
		}

		// Whether the tile is, or touches (including at a corner), any of the
		// given tiles.
		private static bool TouchesAny(Tile t, HashSet<Tile> tiles, Tile except1 = null, Tile except2 = null) {
			if (tiles.Contains(t) && t != except1 && t != except2) {
				return true;
			}
			foreach (Tile neighbor in t.neighbors.Values) {
				if (neighbor != except1 && neighbor != except2 && tiles.Contains(neighbor)) {
					return true;
				}
			}
			return false;
		}

		// As in Civ3, a flood plain is a desert tile bordering a river: it
		// keeps the desert as its base terrain, which is what the terrain
		// textures are chosen from, and gets flood plain as its overlay.
		private static void AddFloodPlains(WorldCharacteristics wc, GameMap m) {
			TerrainType floodPlain = wc.terrainTypes.Find(x => x.IsFloodPlain);
			if (floodPlain == null) {
				return;
			}

			foreach (Tile t in m.tiles) {
				if (t.overlayTerrainType.IsDesert && t.BordersRiver()) {
					t.overlayTerrainType = floodPlain;
				}
			}
		}

		// Rivers run along tile edges, from corner to corner. A corner is
		// given by map coordinates halfway between tiles: the tile at (x, y)
		// has its north corner at (x, y - 1), east at (x + 1, y), south at
		// (x, y + 1) and west at (x - 1, y). Neighboring corners differ by
		// one in both x and y.
		private static readonly (int dx, int dy)[] CornerSteps = { (1, -1), (-1, -1), (1, 1), (-1, 1) };

		// Finds the shortest chain of corners that follows the river's tiles
		// in order, from a corner of the source to a corner touching the
		// water or the river it flows into, without touching water or another
		// river on the way. Returns null if there is none.
		// With fromFarSide, the chain only starts from the corners of the
		// source facing away from the next river tile.
		private static List<(int x, int y)> TraceRiverCorners(GameMap m, List<Tile> river, bool fromFarSide) {
			Tile end = river[^1];
			int lastOwn = river.Count - 2;

			// The chain may also walk around the river it joins, until it
			// meets that river's edges.
			int lastWalkable = end.IsLand() ? river.Count - 1 : lastOwn;

			// Each state is a corner and the index of the river tile it is a
			// corner of.
			Dictionary<(int x, int y, int i), (int x, int y, int i)> cameFrom = new();
			Queue<(int x, int y, int i)> queue = new();
			foreach ((int x, int y) corner in CornersOf(m, river[0])) {
				if (fromFarSide && IsCornerOf(m, corner, river[1])) {
					continue;
				}
				if (!CornerTouchesWater(m, corner) && !CornerOnRiver(m, corner)) {
					cameFrom[(corner.x, corner.y, 0)] = (0, 0, -1);
					queue.Enqueue((corner.x, corner.y, 0));
				}
			}

			while (queue.Count > 0) {
				(int x, int y, int i) state = queue.Dequeue();
				foreach ((int dx, int dy) in CornerSteps) {
					// Only follow edges between two land tiles.
					Tile a = m.tileAt(state.x + dx, state.y);
					Tile b = m.tileAt(state.x, state.y + dy);
					if (a == Tile.NONE || b == Tile.NONE || !a.IsLand() || !b.IsLand()) {
						continue;
					}

					(int x, int y) next = (m.wrapTileX(state.x + dx), state.y + dy);
					for (int j = state.i; j <= Math.Min(state.i + 1, lastWalkable); ++j) {
						if (!IsCornerOf(m, next, river[j]) || cameFrom.ContainsKey((next.x, next.y, j))) {
							continue;
						}
						cameFrom[(next.x, next.y, j)] = state;

						bool reachedWater = j >= lastOwn && CornerTouchesWater(m, next);
						if (reachedWater || CornerOnRiver(m, next)) {
							List<(int x, int y)> result = new();
							for ((int x, int y, int i) s = (next.x, next.y, j); s.i >= 0; s = cameFrom[s]) {
								result.Add((s.x, s.y));
							}
							result.Reverse();
							return result;
						}

						if (!CornerTouchesWater(m, next)) {
							queue.Enqueue((next.x, next.y, j));
						}
					}
				}
			}
			return null;
		}

		private static (int x, int y)[] CornersOf(GameMap m, Tile t) {
			int x = t.XCoordinate;
			int y = t.YCoordinate;
			return new[] { (x, y - 1), (m.wrapTileX(x + 1), y), (x, y + 1), (m.wrapTileX(x - 1), y) };
		}

		// The up to four tiles meeting at a corner.
		private static Tile[] TilesAtCorner(GameMap m, (int x, int y) corner) {
			return new[] {
				m.tileAt(corner.x, corner.y - 1),
				m.tileAt(corner.x + 1, corner.y),
				m.tileAt(corner.x, corner.y + 1),
				m.tileAt(corner.x - 1, corner.y),
			};
		}

		private static bool IsCornerOf(GameMap m, (int x, int y) corner, Tile t) {
			return TilesAtCorner(m, corner).Contains(t);
		}

		private static bool CornerTouchesWater(GameMap m, (int x, int y) corner) {
			return TilesAtCorner(m, corner).Any(t => t != Tile.NONE && !t.IsLand());
		}

		// Whether any river edge already ends at the corner.
		private static bool CornerOnRiver(GameMap m, (int x, int y) corner) {
			foreach ((int dx, int dy) in CornerSteps) {
				Tile a = m.tileAt(corner.x + dx, corner.y);
				if (a != Tile.NONE && a.HasRiverCrossing(EdgeDirection(-dx, dy))) {
					return true;
				}
			}
			return false;
		}

		// The direction of the tile one step (dx, dy) away, for diagonal steps.
		private static TileDirection EdgeDirection(int dx, int dy) {
			if (dy < 0) {
				return dx > 0 ? TileDirection.NORTHEAST : TileDirection.NORTHWEST;
			}
			return dx > 0 ? TileDirection.SOUTHEAST : TileDirection.SOUTHWEST;
		}

		// Marks the edge between two neighboring corners as a river, on both
		// tiles that share it.
		internal static void setRiverFlags(GameMap m, (int x, int y) from, (int x, int y) to) {
			int dx = to.x - from.x;
			int dy = to.y - from.y;

			// Undo the world wrap.
			if (Math.Abs(dx) > 1) {
				dx = -Math.Sign(dx);
			}

			// The two tiles sharing the edge are off to the side of it, so
			// from one to the other is the step (-dx, dy).
			Tile a = m.tileAt(from.x + dx, from.y);
			Tile b = m.tileAt(from.x, from.y + dy);
			SetRiverFlag(a, EdgeDirection(-dx, dy));
			SetRiverFlag(b, EdgeDirection(dx, -dy));
		}

		internal static void SetRiverFlag(Tile t, TileDirection direction) {
			// Neighbors off the edge of the map are Tile.NONE, which is shared
			// by every map, so flags must never be set on it.
			if (t == Tile.NONE) {
				return;
			}
			switch (direction) {
				case TileDirection.NORTHEAST: t.riverNortheast = true; break;
				case TileDirection.NORTHWEST: t.riverNorthwest = true; break;
				case TileDirection.SOUTHEAST: t.riverSoutheast = true; break;
				case TileDirection.SOUTHWEST: t.riverSouthwest = true; break;
			}
		}

		private static void FixContinentalShelf(WorldCharacteristics wc, GameMap m) {
			TerrainType coast = wc.terrainTypes.Find(x => x.Key == "coast");
			TerrainType sea = wc.terrainTypes.Find(x => x.Key == "sea");

			// Ensure that land only borders coast.
			foreach (Tile t in m.tiles) {
				if (t.baseTerrainType.Key != "ocean" && t.baseTerrainType.Key != "sea") {
					continue;
				}

				foreach (Tile neighbor in t.neighbors.Values) {
					if (neighbor != Tile.NONE && neighbor.IsLand()) {
						t.baseTerrainType = coast;
						t.overlayTerrainType = coast;
						break;
					}
				}
			}

			// Ensure that coast only borders sea, not ocean.
			foreach (Tile t in m.tiles) {
				if (t.baseTerrainType.Key != "ocean") {
					continue;
				}

				foreach (Tile neighbor in t.neighbors.Values) {
					if (neighbor.baseTerrainType.Key == "coast") {
						t.baseTerrainType = sea;
						t.overlayTerrainType = sea;
						break;
					}
				}
			}
		}

		private static void AddResources(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0x7171);

			// Randomize the order resources are placed and the order we go
			// through tiles.
			List<Resource> resourcesToPlace = new();
			foreach (Resource r in wc.resources) {
				log.Information(r.Key);
				resourcesToPlace.Add(r);
			}
			rand.Shuffle<Resource>(CollectionsMarshal.AsSpan(resourcesToPlace));

			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			Dictionary<int, int> continentSizes = ComputeContinentSizes(m);

			// Luxury resources.
			//
			// Keep track of which continent a luxury resource is first placed
			// to ensure they don't get spread over multiple continents.
			Dictionary<Resource, int> resourceToContinentPlacement = new();
			foreach (Resource r in resourcesToPlace) {
				if (r.Category != ResourceCategory.LUXURY) {
					continue;
				}

				PlaceLuxuryResourceType(rand, wc, m, r, tileIndicies, resourceToContinentPlacement, continentSizes);
			}

			// Strategic resources.
			foreach (Resource r in resourcesToPlace) {
				if (r.Category != ResourceCategory.STRATEGIC) {
					continue;
				}

				PlaceStrategicResourceType(rand, wc, m, r, tileIndicies, continentSizes);
			}

			// Bonus resources.
			PlaceBonusResources(rand, wc, m, resourcesToPlace.Where(x => x.Category == ResourceCategory.BONUS).ToList(), tileIndicies);
		}

		// Determine the rate of appearance for a given resource. The rate of
		// appearance seems to have a default of 100 for civ3, but if it isn't
		// specified (like for luxuries) it is some random value less than 100
		// but always larger than 50.
		private const int APPEARANCE_PERCENT = 72;

		private static int GetAppearance(WorldCharacteristics wc, Random rand, Resource r, int minCount) {
			// The the appearance ratio, with a random number between 50 and
			// 100 if it isn't specified. Use multiple random calls to roughly
			// simulate a normal distribution using uniform random samples.
			int baseCount = r.AppearanceRatio;
			if (baseCount == 0) {
				baseCount = 50 + rand.Next(11) + rand.Next(11) + rand.Next(11) + rand.Next(11) + rand.Next(11);
			}

			// Scale the appearance based on the number of civs in the game.
			// Civ3's own maps hold about 72% of that. Round to the nearest
			// count: rounding down cost about half of each resource per map.
			int targetCount = (int)Math.Round(wc.worldSize.numberOfCivs * baseCount * APPEARANCE_PERCENT / 10000.0);
			targetCount = Math.Max(minCount, targetCount);
			return targetCount;
		}

		private static void PlaceLuxuryResourceType(Random rand, WorldCharacteristics wc, GameMap m, Resource r, List<int> tileIndicies, Dictionary<Resource, int> resourceToContinentPlacement, Dictionary<int, int> continentSizes) {
			int targetCount = GetAppearance(wc, rand, r, minCount:1);
			int placed = 0;
			resourceToContinentPlacement[r] = -1;

			for (int i = 0; placed < targetCount && i < tileIndicies.Count; ++i) {
				Tile t = m.tiles[tileIndicies[i]];

				// Skip tiles where we can't place this resource.
				if (!t.overlayTerrainType.allowedResources.Contains(r.Key)) {
					continue;
				}

				// Skip tiles that already have a resource or are next to one.
				if ((t.Resource != null && t.Resource != Resource.NONE) || IsNextToExistingResource(t)) {
					continue;
				}

				// If we have a continent for this luxury and this tile isn't
				// on that continent, skip it.
				if (resourceToContinentPlacement[r] != -1 && resourceToContinentPlacement[r] != t.continent) {
					continue;
				}

				// Skip tiles that don't need the luxury-specific criteria.
				if (!IsValidForLuxuryPlacement(wc, m, r, t, continentSizes)) {
					continue;
				}

				// Place the resource.
				++placed;
				t.Resource = r;
				t.ResourceKey = r.Key;
				resourceToContinentPlacement[r] = t.continent;

				// Give ourselves the chance to place additional instances of
				// this luxury in a clump.
				for (int clusterAttempt = 0; clusterAttempt < 4 && placed < targetCount && rand.Next(100) < 50; ++clusterAttempt) {
					// Only neighbors without a resource, so we neither replace
					// another resource nor count a tile twice.
					Tile neighbor = t.neighbors.Values
						.Where(x => x != Tile.NONE
									&& (x.Resource == null || x.Resource == Resource.NONE)
									&& x.overlayTerrainType.allowedResources.Contains(r.Key)
									&& x.continent == t.continent)
						.OrderBy(x => rand.Next()) // Shuffle the neighbors
						.FirstOrDefault(Tile.NONE);

					if (neighbor == Tile.NONE) {
						break;
					}
					++placed;
					neighbor.Resource = r;
					neighbor.ResourceKey = r.Key;
				}
			}

			if (placed < targetCount) {
				log.Information($"Only placed {placed} of {targetCount} {r.Key}");
			}
		}

		private static bool IsNextToExistingResource(Tile t) {
			foreach (Tile neighbor in t.neighbors.Values) {
				if (neighbor.Resource != null && neighbor.Resource != Resource.NONE) {
					return true;
				}
			}
			return false;
		}

		private static bool IsValidForLuxuryPlacement(WorldCharacteristics wc, GameMap m, Resource r, Tile t, Dictionary<int, int> continentSizes) {
			// Don't put luxuries on islands too small for players.
			if (continentSizes[t.continent] < MIN_TILES_PER_PLAYER_ISLAND) {
				return false;
			}

			// Keep different luxuries apart, so each region has its own (copies
			// of the same luxury clump together), but not so far apart that a
			// crowded continent runs out of room for them.
			int minLuxurySpacing = (m.numTilesTall + m.numTilesWide) / 60;
			minLuxurySpacing = Math.Max(2, minLuxurySpacing);
			minLuxurySpacing = Math.Min(minLuxurySpacing, 5);

			foreach (Tile x in t.GetTilesWithinRankDistance(minLuxurySpacing)) {
				if (x.Resource != null
					&& x.Resource != Resource.NONE
					&& x.Resource.Category == ResourceCategory.LUXURY
					&& x.Resource.Key != r.Key) {
					return false;
				}
			}

			// If this is a water-based resource, ensure it doesn't end up in the
			// middle of the ocean.
			if (!t.IsLand() && !HasSufficientLandNeighborsForResource(wc, t)) {
				return false;
			}

			return true;
		}

		private static bool HasSufficientLandNeighborsForResource(WorldCharacteristics wc, Tile t) {
			int landTiles = 0;

			foreach (Tile x in t.GetTilesWithinRankDistance(wc.maxRankOfWorkableTiles)) {
				if (x.IsLand()) {
					++landTiles;
				}
			}

			return landTiles >= 1;
		}

		private static void PlaceStrategicResourceType(Random rand, WorldCharacteristics wc, GameMap m, Resource r, List<int> tileIndicies, Dictionary<int, int> continentSizes) {
			int targetCount = GetAppearance(wc, rand, r, minCount:2);
			int placed = 0;

			for (int i = 0; placed < targetCount && i < tileIndicies.Count; ++i) {
				Tile t = m.tiles[tileIndicies[i]];

				// Skip tiles where we can't place this resource.
				if (!t.overlayTerrainType.allowedResources.Contains(r.Key)) {
					continue;
				}

				// Skip tiles that already have a resource or are next to one.
				// Every resource goes through the tiles in the same order, so
				// without the first check each would land on, and replace,
				// the ones placed before it.
				if ((t.Resource != null && t.Resource != Resource.NONE) || IsNextToExistingResource(t)) {
					continue;
				}

				// Skip tiles that don't need the strategic resource-specific criteria.
				if (!IsValidForStrategicResourcePlacement(wc, m, r, t, continentSizes)) {
					continue;
				}

				// Place the resource.
				++placed;
				t.Resource = r;
				t.ResourceKey = r.Key;
			}

			if (placed < targetCount) {
				log.Information($"Only placed {placed} of {targetCount} {r.Key}");
			}
		}

		private static bool IsValidForStrategicResourcePlacement(WorldCharacteristics wc, GameMap m, Resource r, Tile t, Dictionary<int, int> continentSizes) {
			// Don't put strategic resources on super tiny islands - though
			// putting them on small islands is ok.
			if (continentSizes[t.continent] < MIN_TILES_PER_PLAYER_ISLAND / 2) {
				return false;
			}

			int minSpacing = (m.numTilesTall + m.numTilesWide) / 60;
			minSpacing = Math.Max(2, minSpacing);
			minSpacing = Math.Min(minSpacing, 5);

			// Ensure strategic resources of the same kind don't clump up.
			foreach (Tile x in t.GetTilesWithinRankDistance(minSpacing)) {
				if (x.Resource != null
					&& x.Resource != Resource.NONE
					&& x.Resource.Category == ResourceCategory.STRATEGIC
					&& x.Resource.Key == r.Key) {
					return false;
				}
			}

			// If this is a water-based resource, ensure it doesn't end up in the
			// middle of the ocean.
			if (!t.IsLand() && !HasSufficientLandNeighborsForResource(wc, t)) {
				return false;
			}

			return true;
		}

		private static void PlaceBonusResources(Random rand, WorldCharacteristics wc, GameMap m,
												List<Resource> bonusResources, List<int> tileIndicies) {
			// About as dense as on Civ3's maps.
			int totalPossibleBonusResources = m.tiles.Count * 22 / 1000;
			int placed = 0;

			Dictionary<Resource, int> terrainScores = CalculateBonusResourceTerrainScores(wc, bonusResources);

			// Where each resource's search for a tile resumes. A tile rejected
			// for a resource stays rejected (resources are only ever added), so
			// later searches can skip past it.
			Dictionary<Resource, int> searchCursors = new();

			for (int pass = 0; pass < 200 && placed < totalPossibleBonusResources; ++pass) {
				rand.Shuffle<Resource>(CollectionsMarshal.AsSpan(bonusResources));

				foreach (Resource r in bonusResources) {
					int terrainScore = terrainScores[r];

					// Can't be placed anywhere.
					if (terrainScore == 0) {
						continue;
					}

					// Resources that can go in more places have a higher chance
					// of being placed.
					int placementProbability = 25;
					if (terrainScore < 2) {
						placementProbability = 16;
					} else if (terrainScore > 3) {
						placementProbability = 50;
					}

					if (rand.Next(100) >= placementProbability) {
						continue;
					}

					if (PlaceBonusResource(wc, m, r, tileIndicies, searchCursors)) {
						++placed;
					}
				}
			}
		}

		private static bool PlaceBonusResource(WorldCharacteristics wc, GameMap m, Resource r, List<int> tileIndicies, Dictionary<Resource, int> searchCursors) {
			// We want to place this resource. Find the first valid tile
			// we can stick it on.
			//
			// We don't want it next to other resources, and if it is a
			// water resource (fish/whale/etc) don't stick it in the
			// middle of the ocean.
			searchCursors.TryGetValue(r, out int start);
			for (int i = start; i < tileIndicies.Count; ++i) {
				Tile t = m.tiles[tileIndicies[i]];
				if (!t.overlayTerrainType.allowedResources.Contains(r.Key)) {
					continue;
				}

				bool hasResource = !(t.Resource == Resource.NONE || t.Resource == null);
				if (hasResource || IsNextToExistingResource(t)) {
					continue;
				}

				if (!t.IsLand() && !HasSufficientLandNeighborsForResource(wc, t)) {
					continue;
				}

				t.Resource = r;
				t.ResourceKey = r.Key;
				searchCursors[r] = i + 1;
				return true;
			}
			searchCursors[r] = tileIndicies.Count;
			return false;
		}

		private static Dictionary<Resource, int> CalculateBonusResourceTerrainScores(WorldCharacteristics wc, List<Resource> bonusResources) {
			Dictionary<Resource, int> result = new();

			foreach (Resource r in bonusResources) {
				int score = 0;
				foreach (TerrainType tt in wc.terrainTypes) {
					if (tt.allowedResources.Contains(r.Key)) {
						// Make water bonus resources get a higher score, since
						// land bonus resources are generally more valuable.
						if (tt.isWater()) {
							score += 4;
						} else {
							score += 1;
						}
					}
				}
				result[r] = score;
			}

			return result;
		}

		private static void AddBarbarianCamps(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0xba7b5);
			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			int landTiles = 0;
			foreach (Tile t in m.tiles) {
				if (t.IsLand()) {
					++landTiles;
				}
			}

			int totalPossibleBarbCamps = DeriveTotalPossibleBarbCamps(wc, landTiles);
			Dictionary<int, int> continentSizes = ComputeContinentSizes(m);

			int numCamps = 0;
			for (int i = 0; i < tileIndicies.Count && numCamps < totalPossibleBarbCamps; ++i) {
				Tile t = m.tiles[tileIndicies[i]];
				if (IsValidForBarbarianCamp(wc, m, t, continentSizes)) {
					m.barbarianCamps.Add(t);
					t.hasBarbarianCamp = true;
					++numCamps;
				}
			}
		}

		// How many barbarian camps a map starts with: one for every
		// LAND_TILES_PER_BARBARIAN_CAMP land tiles, with more at the higher
		// barbarian activity levels. These are tuned guesses, NOT taken from
		// Civ3, whose own numbers aren't known.
		private const int LAND_TILES_PER_BARBARIAN_CAMP = 100;
		private const double RESTLESS_BARBARIAN_CAMP_FACTOR = 1.25;
		private const double RAGING_BARBARIAN_CAMP_FACTOR = 1.5;

		/// <summary>
		/// Apply barbarian activity level to barbarian camp spawn rate. Currently NOT based on Civ3 values.
		/// TODO: Make configurable
		/// TODO: Determine what these values are in Civ3 
		/// </summary>
		private static int DeriveTotalPossibleBarbCamps(WorldCharacteristics wc, int landTiles) {
			var totalCampsBaseline = landTiles / LAND_TILES_PER_BARBARIAN_CAMP;
			switch (wc.barbarianActivity) {
				case BarbarianActivity.None:
					return 0;
				case BarbarianActivity.Sedentary:
					return totalCampsBaseline;
				case BarbarianActivity.Roaming:
					return totalCampsBaseline;
				case BarbarianActivity.Restless:
					return (int)Math.Round(totalCampsBaseline * RESTLESS_BARBARIAN_CAMP_FACTOR);
				case BarbarianActivity.Raging:
					return (int)Math.Round(totalCampsBaseline * RAGING_BARBARIAN_CAMP_FACTOR);
				default:
					log.Warning("Unknown Barbarian Activity at barb camps derivation.");
					return totalCampsBaseline;
			}
		}

		// About one goody hut for every this many land tiles. A tuned guess,
		// NOT taken from Civ3.
		private const int LandTilesPerGoodyHut = 60;

		private static void AddGoodyHuts(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0x900d7);
			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			int landTiles = m.tiles.Count(t => t.IsLand());
			int totalHuts = landTiles / LandTilesPerGoodyHut;
			Dictionary<int, int> continentSizes = ComputeContinentSizes(m);

			int numHuts = 0;
			for (int i = 0; i < tileIndicies.Count && numHuts < totalHuts; ++i) {
				Tile t = m.tiles[tileIndicies[i]];
				if (IsValidForGoodyHut(m, t, continentSizes)) {
					t.hasGoodyHut = true;
					++numHuts;
				}
			}
		}

		private static bool IsValidForGoodyHut(GameMap m, Tile t, Dictionary<int, int> continentSizes) {
			if (t == Tile.NONE || !t.IsLand() || t.IsImpassable() || t.hasBarbarianCamp || t.overlayTerrainType.Key == "volcano") {
				return false;
			}

			// Not on luxury or strategic resources, which would otherwise be
			// hidden under the hut.
			if (t.Resource != null && t.Resource != Resource.NONE &&
				(t.Resource.Category == ResourceCategory.STRATEGIC || t.Resource.Category == ResourceCategory.LUXURY)) {
				return false;
			}

			// Not on islands too small to settle.
			if (continentSizes[t.continent] < 5) {
				return false;
			}

			// Spread out, and not right next to a barbarian camp.
			foreach (Tile n in t.GetTilesWithinTileSquare(2)) {
				if (n.hasGoodyHut || n.hasBarbarianCamp) {
					return false;
				}
			}

			// Not where the players start, or on their doorstep.
			if (m.startingLocations.Any(x => t.DistanceTo(x) < 3)) {
				return false;
			}

			return true;
		}

		private static bool IsValidForBarbarianCamp(WorldCharacteristics wc, GameMap m, Tile t, Dictionary<int, int> continentSizes) {
			if (!IsCampSiteSuitable(t, continent => continentSizes[continent], wc.maxRankOfBarbarianCampTiles)) {
				return false;
			}

			// No barbarian camps less than 6 tiles away from a player
			if (m.startingLocations.Any(x => t.DistanceTo(x) < 6)) return false;

			return true;
		}

		// The terrain and spacing rules for a barbarian camp, shared by the
		// camps placed with the map and those that appear during the game
		// (see BarbarianInteractions.MaybeAddCamp).
		public static bool IsCampSiteSuitable(Tile t, Func<int, int> continentSize, int maxRankOfCampTiles) {
			// No barbarian camps on water, volcanos, or mountains.
			if (!t.IsLand() || t == Tile.NONE || t.overlayTerrainType.Key == "volcano" || t.overlayTerrainType.Key == "mountains") {
				return false;
			}

			// No barbarian camps on luxury or strategic resources (we're ok
			// with barb camps on things like cows or gold).
			if (t.Resource != null && t.Resource != Resource.NONE &&
				(t.Resource.Category == ResourceCategory.STRATEGIC || t.Resource.Category == ResourceCategory.LUXURY)) {
				return false;
			}

			// No barbarian camps on super tiny islands.
			if (continentSize(t.continent) < 15) {
				return false;
			}

			// No barbarian camps within the big fat cross of another camp.
			foreach (Tile n in t.GetTilesWithinRankDistance(maxRankOfCampTiles)) {
				if (n.hasBarbarianCamp) {
					return false;
				}
			}

			return true;
		}

		private static void DetermineStartingLocations(WorldCharacteristics wc, GameMap m) {
			Random rand = new(wc.mapSeed + 0x1337);
			List<HashSet<Tile>> landContinents = m.continents.Where(x => x.First().IsLand()).ToList();

			// Count how many luxuries each continent has.
			Dictionary<int, int> continentLuxuryCount = new();
			foreach (HashSet<Tile> continent in landContinents) {
				continentLuxuryCount[continent.First().continent] = 0;
				foreach (Tile t in continent) {
					if (t.Resource.Category == ResourceCategory.LUXURY) {
						continentLuxuryCount[t.continent]++;
					}
				}
			}

			// Find all viable city locations. One player with the default
			// government is enough to evaluate every tile's yields.
			Player scoringPlayer = new();
			scoringPlayer.government = wc.defaultGovernment;
			Dictionary<Tile, int> scoredTiles = new();
			foreach (Tile t in m.tiles) {
				int score = ScorePossibleCityLocation(wc, t, scoringPlayer);
				if (score > 0) {
					scoredTiles[t] = StartScore(t, score);
				}
			}
			List<Tile> orderedTiles = scoredTiles.OrderByDescending(t => t.Value).Select(x => x.Key).ToList();

			// Use those to find the appropriate number of starting locations.
			List<Tile> startingLocations = new();
			Dictionary<int, int> continentStartingLocationCount = new();
			Dictionary<int, int> continentSizes = ComputeContinentSizes(m);

			int spacing = StartSpacing(wc, continentSizes);
			// The land a civ is given on an island it shares (see
			// IslandCapacity): more than the least a civ needs
			// (ArchipelagoTilesPerPlayer), so that a big island holds the
			// two or three civs it was planned for (see
			// ArchipelagoIslandSizes) and the home islands aren't left
			// empty while a big one fills up.
			int tilesPerPlayer = (int)(landContinents.Sum(c => c.Count) * SHARED_ISLAND_ROOM_PER_CIV / Math.Max(1, wc.worldSize.numberOfCivs));
			bool archipelago = wc.landform == WorldCharacteristics.Landform.Archipelago;
			double qualityFloor = orderedTiles.Count > 0 ? scoredTiles[orderedTiles[0]] * START_QUALITY_FLOOR : 0;

			for (int attempt = 0; attempt < START_PLACEMENT_PASSES && startingLocations.Count < wc.worldSize.numberOfCivs; ++attempt) {
				HashSet<Tile> leavesRoom = null;
				HashSet<int> islandsWithRoom = null;
				while (startingLocations.Count < wc.worldSize.numberOfCivs) {
					// On an archipelago, or a map with about as many islands
					// that can hold a civ as there are civs, each civ gets an
					// island of its own, the bigger ones first, rather than
					// two sharing one while another is left empty. (Owner's
					// decision, 2026-10-12: "when there are at least as many
					// suitable islands as civs (or close to it), place one
					// civ per island, using the largest/best islands first".)
					// An island's room is counted by its size
					// (IslandCapacity), so that a big island holds two or
					// more civs spaced apart and the islands left empty are
					// the small ones meant for settling (owner's report,
					// 2026-10-12). Not on the last pass, so that a map with
					// room for every civ isn't turned down for it. A map of
					// a few big continents isn't affected.
					int islands = 0;
					int largestEmptyIsland = 0;
					int largestIslandWithRoom = 0;
					foreach (int continent in orderedTiles.Select(c => c.continent).Distinct()) {
						if (IsContinentLargeEnough(continentSizes, continent, attempt)) {
							++islands;
							int startsOnIsland = continentStartingLocationCount.GetValueOrDefault(continent);
							if (startsOnIsland == 0) {
								largestEmptyIsland = Math.Max(largestEmptyIsland, continentSizes[continent]);
							}
							if (startsOnIsland < IslandCapacity(continentSizes[continent], tilesPerPlayer)) {
								largestIslandWithRoom = Math.Max(largestIslandWithRoom, continentSizes[continent]);
							}
						}
					}
					bool islandEach = attempt < START_PLACEMENT_PASSES - 1
						&& (archipelago || (largestIslandWithRoom > 0 && islands >= wc.worldSize.numberOfCivs * ISLAND_EACH_SHARE));
					if (islandEach && leavesRoom == null) {
						leavesRoom = TilesLeavingRoomForAnotherStart(wc, orderedTiles, scoredTiles, qualityFloor, continentSizes, tilesPerPlayer, attempt);
						islandsWithRoom = leavesRoom.Select(x => x.continent).ToHashSet();
					}

					// Take the valid tile with the best score, discounted by
					// how much closer than the spacing it is to a start on
					// its continent. Taking the best scores alone packs every
					// civ into the best land at the minimum distance apart.
					// When each civ gets an island of its own, the tile is
					// taken on the biggest island that still has room and a
					// valid tile, so that the big islands fill up and the
					// islands left empty are the smallest.
					int coastalStarts = startingLocations.Count(s => s.NeighborsOcean());
					bool mustBeCoastal = wc.worldSize.numberOfCivs - startingLocations.Count <= wc.coastalStartsNeeded - coastalStarts;
					// On an archipelago any other start is as likely as not
					// to be on the coast, if there's a coastal site to take
					// (owner's decision, 2026-10-12: only seafaring civs are
					// guaranteed the coast, the others "may also start on
					// the coast, but that should be random"). Left to the
					// scores, almost none were: a coastal site has less land
					// around it.
					bool coastalOnly = mustBeCoastal || (archipelago && rand.NextDouble() < ARCHIPELAGO_COASTAL_START_CHANCE);

					Tile t = null;
					double bestValue = 0;
					int bestIsland = 0;
					for (bool searched = false; !searched;) {
						searched = true;
						foreach (Tile candidate in orderedTiles) {
							// The tiles are in score order, so none after this
							// one is good enough.
							if (attempt < START_QUALITY_FLOOR_PASSES && scoredTiles[candidate] < qualityFloor) {
								break;
							}

							if (!IsContinentLargeEnough(continentSizes, candidate.continent, attempt)) {
								continue;
							}

							int startsOnContinent = continentStartingLocationCount.GetValueOrDefault(candidate.continent);
							if (islandEach && startsOnContinent >= IslandCapacity(continentSizes[candidate.continent], tilesPerPlayer)) {
								continue;
							}

							// A civ joins another on an island only if its share
							// of the island is comparable to the biggest empty
							// island, even if that one has no start this pass may
							// take (say, for want of luxuries): it waits for a
							// later pass instead.
							if (islandEach && startsOnContinent > 0
								&& continentSizes[candidate.continent] / (double)(startsOnContinent + 1) < largestEmptyIsland * ISLAND_JOIN_SHARE) {
								continue;
							}

							// The first civ on an island with room for two
							// starts where it leaves room for the second, rather
							// than in the middle.
							if (islandEach && startsOnContinent == 0 && islandsWithRoom.Contains(candidate.continent) && !leavesRoom.Contains(candidate)) {
								continue;
							}

							// Only seafaring civs must start on the coast; the
							// others may start anywhere (owner's decision,
							// 2026-10-12). When the starts left are only enough
							// for the coastal ones still needed, they must be
							// coastal. GameSetup.AssignStartingLocations gives
							// the seafaring civs those starts.
							if (coastalOnly && !candidate.NeighborsOcean()) {
								continue;
							}

							// Not when each civ gets an island of its own: an
							// island's size comes first there, or a civ would
							// start on a small island with a luxury while a big
							// one without is left empty.
							if (!islandEach && !ContinentHasEnoughLuxuries(candidate, continentLuxuryCount, continentStartingLocationCount, attempt)) {
								continue;
							}

							if (TileIsTooCloseToOtherStarts(candidate, startingLocations, wc.worldSize.distanceBetweenCivs)) {
								continue;
							}

							double value = scoredTiles[candidate] * SpreadFactor(candidate, startingLocations, spacing);
							int island = islandEach ? continentSizes[candidate.continent] : 0;
							if (island > bestIsland || (island == bestIsland && value > bestValue)) {
								t = candidate;
								bestValue = value;
								bestIsland = island;
							}
						}
						// With no coastal site to take, a start that only
						// leaned to the coast goes anywhere.
						if (t == null && coastalOnly && !mustBeCoastal) {
							coastalOnly = false;
							searched = false;
						}
					}
					if (t == null) {
						break;
					}

					// This is a valid starting location.
					startingLocations.Add(t);
					if (continentStartingLocationCount.ContainsKey(t.continent)) {
						continentStartingLocationCount[t.continent]++;
					} else {
						continentStartingLocationCount[t.continent] = 1;
					}
					log.Information($"Placed start number {startingLocations.Count} on attempt {attempt}");
				}
			}

			// There is no last resort: a map without a start for every civ
			// can't be played, and GameSetup generates another.
			if (wc.worldSize.numberOfCivs > startingLocations.Count)
				log.Warning($"More civs than available starting locations: {startingLocations.Count} starts for {wc.worldSize.numberOfCivs} civs.");

			// Before using the starting locations, shuffle them, so that the
			// human player doesn't always get the best starting spot.
			rand.Shuffle<Tile>(CollectionsMarshal.AsSpan(startingLocations));
			m.startingLocations = startingLocations;
		}

		// The candidate starts on islands with room for more than one civ
		// (see IslandCapacity) that leave room for another start on the
		// island the world size's distance between civs away, among the
		// candidates the given pass of DetermineStartingLocations may take.
		private static HashSet<Tile> TilesLeavingRoomForAnotherStart(WorldCharacteristics wc, List<Tile> orderedTiles, Dictionary<Tile, int> scoredTiles,
				double qualityFloor, Dictionary<int, int> continentSizes, int tilesPerPlayer, int attempt) {
			HashSet<Tile> result = new();
			IEnumerable<IGrouping<int, Tile>> islands = orderedTiles
				.Where(t => attempt >= START_QUALITY_FLOOR_PASSES || scoredTiles[t] >= qualityFloor)
				.Where(t => IsContinentLargeEnough(continentSizes, t.continent, attempt) && IslandCapacity(continentSizes[t.continent], tilesPerPlayer) > 1)
				.GroupBy(t => t.continent);
			foreach (IGrouping<int, Tile> island in islands) {
				foreach (Tile t in island) {
					if (island.Any(other => MapCoordinateDistance(t, other) >= wc.worldSize.distanceBetweenCivs)) {
						result.Add(t);
					}
				}
			}
			return result;
		}

		// How many civs an island has room for when each civ gets an island
		// of its own where it can (see DetermineStartingLocations): one per
		// tilesPerPlayer tiles, the land a civ needs on an archipelago, but
		// at least one. There is no most (owner's decision, 2026-10-12).
		private static int IslandCapacity(int islandSize, int tilesPerPlayer) {
			return Math.Max(1, islandSize / tilesPerPlayer);
		}

		// A start next to fresh water (a river or a lake) scores this many
		// percent more. Not from Civ3, which says nothing of how it places
		// starts. It tips a choice between sites about as good, while a
		// clearly better site still wins. Starts don't lean to the coast:
		// only seafaring civs must start there (owner's decision,
		// 2026-10-12; see coastalStartsNeeded).
		private const int FRESH_WATER_START_BONUS_PERCENT = 5;

		// A candidate start's score: the city site's (ScorePossibleCityLocation)
		// with the fresh water bonus.
		private static int StartScore(Tile t, int score) {
			int bonus = t.BordersRiver() || t.NeighborsFreshWater() ? FRESH_WATER_START_BONUS_PERCENT : 0;
			return score * (100 + bonus) / 100;
		}

		// How far apart, in moves, starts on the same continent should be for
		// each civ to get an even share of the land: the side of a square of
		// land per civ. Civ3 spreads its starts out about this far: in its
		// turn-one saves and on the capitals of its standard pangaeas, the
		// average distance from a start to the nearest on its continent comes
		// within a move or two of it. UNVERIFIED as Civ3's method; no Civ3
		// source describes how it places starts.
		private static int StartSpacing(WorldCharacteristics wc, Dictionary<int, int> continentSizes) {
			int land = continentSizes.Values.Where(size => size > MIN_TILES_PER_PLAYER_ISLAND).Sum();
			if (land == 0) {
				land = continentSizes.Values.Sum();
			}
			return Math.Max(1, (int)Math.Round(Math.Sqrt(land / (double)Math.Max(1, wc.worldSize.numberOfCivs))));
		}

		// Scales a candidate start's score down linearly the closer it is than
		// the spacing to the nearest start on its continent. A start on
		// another continent is no crowding.
		private static double SpreadFactor(Tile t, List<Tile> startingLocations, int spacing) {
			int nearest = spacing;
			foreach (Tile start in startingLocations) {
				if (start.continent == t.continent) {
					nearest = Math.Min(nearest, t.DistanceTo(start));
				}
			}
			return nearest / (double)spacing;
		}

		// Allow smaller continents the more desperate we are to find a starting
		// location. Hopefully map generation will have handled this; if not
		// every civ fits, GameSetup generates another map.
		private static bool IsContinentLargeEnough(Dictionary<int, int> continentSizes, int continent, int attempt) {
			int continentSize = continentSizes[continent];

			if (attempt < 3) {
				return continentSize > MIN_TILES_PER_PLAYER_ISLAND;
			} else if (attempt < 6) {
				return continentSize > MIN_TILES_PER_PLAYER_ISLAND / 2;
			}
			return true;
		}

		// Try to match the behavior of at least one luxury per player per continent.
		private static bool ContinentHasEnoughLuxuries(Tile t, Dictionary<int, int> continentLuxuryCount, Dictionary<int, int> continentStartingLocationCount, int attempt) {
			// Only be strict on the first two passes.
			if (attempt >= 2) {
				return true;
			}

			int luxuriesOnContinent = 0;
			if (continentLuxuryCount.ContainsKey(t.continent)) {
				luxuriesOnContinent = continentLuxuryCount[t.continent];
			}

			int startsOnContinent = 0;
			if (continentStartingLocationCount.ContainsKey(t.continent)) {
				startsOnContinent = continentStartingLocationCount[t.continent];
			}

			return startsOnContinent < luxuriesOnContinent;
		}

		// How many passes over the candidate tiles DetermineStartingLocations
		// makes, relaxing the continent size and luxury requirements as it
		// goes (see IsContinentLargeEnough and ContinentHasEnoughLuxuries).
		// The last relaxation is on pass 6, so later passes would find
		// nothing new.
		private const int START_PLACEMENT_PASSES = 7;

		// On a map other than an archipelago, the share of the civs there
		// must be islands for, for each civ to get one of its own (owner's
		// "or close to it").
		private const double ISLAND_EACH_SHARE = 0.75;

		// A civ's room on an island it shares, as a share of the land per
		// civ (see DetermineStartingLocations).
		private const double SHARED_ISLAND_ROOM_PER_CIV = 0.6;

		// The chance that a start on an archipelago that needn't be coastal
		// is put on the coast (see DetermineStartingLocations).
		private const double ARCHIPELAGO_COASTAL_START_CHANCE = 0.5;

		// A civ joins a civ on an island only if its share of the island is
		// at least this share of the biggest empty island's land: "no two
		// civs sharing an island while a comparable empty island suitable
		// for a civ is left over" (owner, 2026-10-12).
		private const double ISLAND_JOIN_SHARE = 0.65;

		// On the first passes, a start must score at least this share of the
		// best candidate on the map, so that spreading the starts out doesn't
		// put a civ on land much worse than its rivals'. Not from Civ3: with
		// it, generated standard pangaeas space their starts as Civ3's do
		// (see StartSpacing) while the starts stay in about the best eighth
		// of the candidate tiles.
		private const double START_QUALITY_FLOOR = 0.8;
		private const int START_QUALITY_FLOOR_PASSES = 3;

		// Starts on the same continent are always at least the world size's
		// distance between civs (WSIZ) apart; that is never relaxed. The
		// distance is in map coordinates, the units Civ3's editor gives the
		// world size's width and height in ("in tiles", though a 100 wide
		// map has 50 tiles to a row), so 12 is 6 moves. UNVERIFIED (no Civ3
		// source gives the formula): the editor's help only says "the desired
		// minimum distance (in tiles)". Measured in moves, Civ3's own maps
		// couldn't keep it: a Civ3 huge pangaea fits its 16 civs on about
		// 3,300 land tiles, where 24 moves apart leaves room for fewer than 10, and
		// the turn-one autosave of a Civ3 large map has starts on the same
		// continent 14 moves apart with a distance of 18 (28 in map
		// coordinates). Its turn-one saves of huge maps (24) have starts on the
		// same continent 20 to 22 apart in map coordinates, so it seems to bend
		// even this a little when a map is crowded.
		// The distance between two tiles in Civ3's map coordinates, in which
		// a map's width and height are given: each step to a neighbouring
		// tile is 2, as rows are offset by 1 and a tile's neighbours to the
		// east and west are 2 apart. So it is twice the number of moves.
		private static int MapCoordinateDistance(Tile a, Tile b) {
			return Math.Abs(a.map.CalculateXDelta(a.XCoordinate, b.XCoordinate))
				+ Math.Abs(a.map.CalculateYDelta(a.YCoordinate, b.YCoordinate));
		}

		private static bool TileIsTooCloseToOtherStarts(Tile t, List<Tile> startingLocations, int minDistance) {
			foreach (Tile start in startingLocations) {
				if (start.continent == t.continent && MapCoordinateDistance(start, t) < minDistance) {
					return true;
				}
			}

			return false;
		}

		// Every civ must start where its capital can grow to at least this
		// size working the best food tiles around it.
		private const int MIN_START_GROWTH_SIZE = 3;

		// A city center always makes this much food (see Tile.BaseFoodYield).
		// Each citizen eats the rules' foodPerCitizen.
		private const int CITY_CENTER_FOOD = 2;

		// Whether a city founded on t could grow to the given size, with each
		// citizen working the best food tile left in reach. To grow past a
		// size the city needs a food surplus at that size.
		private static bool CanGrowToSize(WorldCharacteristics wc, Tile t, Player player, int size) {
			// A new city can only work tiles inside its borders, which start
			// out covering just the tiles next to it, so only count those.
			List<int> food = t.GetTilesWithinRankDistance(1)
				.Where(n => n != t && n != Tile.NONE)
				.Select(n => n.FoodYield(player).yield)
				.OrderByDescending(f => f)
				.ToList();

			int total = CITY_CENTER_FOOD;
			for (int citizens = 1; citizens < size; ++citizens) {
				if (citizens > food.Count) {
					return false;
				}
				total += food[citizens - 1];
				if (total <= citizens * wc.foodPerCitizen) {
					return false;
				}
			}
			return true;
		}

		// TODO: merge this with the ai logic
		// Maps each continent id to the number of tiles in that continent.
		private static Dictionary<int, int> ComputeContinentSizes(GameMap m) {
			Dictionary<int, int> result = new();
			foreach (HashSet<Tile> continent in m.continents) {
				foreach (Tile t in continent) {
					result[t.continent] = continent.Count;
				}
			}
			return result;
		}

		private static int ScorePossibleCityLocation(WorldCharacteristics wc, Tile t, Player player) {
			if (!t.IsAllowCities() || !CanGrowToSize(wc, t, player, MIN_START_GROWTH_SIZE)) {
				return int.MinValue;
			}

			const int CommercePoints = 4;
			const int ShieldPoints = 12;
			const int FoodPoints = 20;
			const int RiverPoints = 35;
			const int CoastPoints = 30;
			const int LandTilePoints = 1;
			// Barbarian camps aren't scored: starting locations are chosen
			// before the camps are placed, and the camps keep away from them
			// (see IsValidForBarbarianCamp).

			// Calculate the score for tiles in the immediate area.
			int score = 0;
			foreach (Tile n in t.GetTilesWithinRankDistance(1)) {
				score += CommercePoints * n.CommerceYield(player).yield;
				score += ShieldPoints * n.ProductionYield(player).yield;
				score += FoodPoints * n.FoodYield(player).yield;

				if (!n.IsLand()) {
					score += CoastPoints;
				}
			}

			// Then do it again for the full big fat cross, effectively weighting
			// the immediate neighbors at a 2x rate.
			foreach (Tile n in t.GetTilesWithinRankDistance(wc.maxRankOfWorkableTiles)) {
				score += CommercePoints * n.CommerceYield(player).yield;
				score += ShieldPoints * n.ProductionYield(player).yield;
				score += FoodPoints * n.FoodYield(player).yield;
			}

			// Give an extra bonus for fresh water, a river or a lake (the
			// owner asked for starts with fresh water, 2026-10-12).
			if (t.BordersRiver() || t.NeighborsFreshWater()) {
				score += RiverPoints;
			}

			// Try to get a rough sense of the number of surrounding land tiles
			// on the same continent, to avoid players getting stuck on a
			// peninsula.
			foreach (Tile n in t.GetTilesWithinRankDistance(4)) {
				if (n.continent == t.continent) {
					score += LandTilePoints;
				}
			}

			return score;
		}

		public static void AddBonusGrasslands(WorldCharacteristics wc, GameMap m) {
			// Randomize the order we iterate through the tiles.
			Random rand = new(wc.mapSeed + 0x7361);
			List<int> tileIndicies = Enumerable.Range(0, m.tiles.Count).ToList();
			rand.Shuffle<int>(CollectionsMarshal.AsSpan(tileIndicies));

			foreach (int index in tileIndicies) {
				Tile t = m.tiles[index];

				// We only want grassland tiles without hills. A forest/jungle/marsh
				// is ok, since that can be cleared.
				if (t.baseTerrainType.Key != "grassland") {
					continue;
				}

				// Each grassland tile has a third of a chance, about as often
				// as on Civ3's maps.
				if (rand.Next(100) >= 33) {
					continue;
				}

				// We can't have bonus grassland under a resource/luxury.
				bool hasResource = !(t.Resource == Resource.NONE || t.Resource == null);
				if (hasResource) {
					continue;
				}

				// We can't have bonus grassland under a hill/mountain.
				if (t.overlayTerrainType.isHilly()) {
					continue;
				}

				t.isBonusShield = true;
			}
		}
	}
}

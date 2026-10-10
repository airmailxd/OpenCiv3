using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// Regression tests for AI and pathing fixes.
public sealed class FixAiPathingTests : MapBase {
	private static readonly TerrainType plains = new() { Key = "plains", movementCost = 1 };
	private static readonly TerrainType coast = new() { Key = "coast", movementCost = 1 };
	private static readonly TerrainImprovement testRoad = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);
	private static readonly TerrainImprovement testRailroad = new("railroad", TerrainImprovement.Layer.Roads, movementCost: 0);

	// A wide x tall map of plains, with water wherever isWater says so.
	internal static GameMap MakeMap(int wide, int tall, Func<int, int, bool> isWater = null) {
		EngineStorage.InitializeGameDataForTests(new C7GameData.GameData(1));
		GameMap map = new() { numTilesWide = wide, numTilesTall = tall, tiles = new List<Tile>() };
		for (int i = 0; i < wide * tall / 2; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			bool water = isWater != null && isWater(x, y);
			Tile tile = new(ID.None("tile")) {
				XCoordinate = x,
				YCoordinate = y,
				baseTerrainType = water ? coast : plains,
				overlayTerrainType = water ? coast : plains,
			};
			map.tiles.Add(tile);
		}
		map.computeNeighbors();
		map.recomputeContinents();
		return map;
	}

	private static MapUnit MakeUnit(bool land, int movement, Tile location) {
		MapUnit unit = land ? MakeLandUnit(movement) : MakeWaterUnit(movement);
		unit.location = location;
		return unit;
	}

	// A city founded next to a pre-built railroad makes a 0-cost edge, even
	// though no two railroads are adjacent and nobody can build railroads.
	// The heuristic floor must account for that.
	[Fact]
	public void HeuristicFloorCoversRoadlessCityNextToRailroad() {
		GameMap map = MakeMap(20, 20);
		Tile rail = map.tileAt(10, 10);
		rail.overlays.Add(testRailroad);
		foreach (Tile t in map.tiles.Where(t => t.DistanceTo(rail) >= 3 && t.YCoordinate % 4 == 0)) {
			t.overlays.Add(testRoad);
		}

		Tile start = map.tileAt(2, 2);
		MapUnit unit = MakeUnit(true, 1, start);
		// Scan the map before the city exists.
		float before = MovementCostFloor.MinimumTileCost(unit, start);

		Tile citySite = rail.neighbors[TileDirection.NORTH];
		City city = new(citySite, unit.owner, "City", ID.None("city"));
		citySite.cityAtTile = city;
		unit.owner.cities.Add(city);

		float edge = TilePath.GetMovementCost(unit.owner, citySite, TileDirection.SOUTH, rail);
		Assert.Equal(0f, edge);
		Assert.True(before <= edge, $"floor {before} is above the cost {edge} of a city -> railroad step");
		Assert.True(MovementCostFloor.MinimumTileCost(unit, start) <= edge);
	}

	// Roads alone still give a useful (non-zero) heuristic, even with road-less
	// cities on the map.
	[Fact]
	public void HeuristicFloorIgnoresRoadlessCities() {
		GameMap map = MakeMap(20, 20);
		foreach (Tile t in map.tiles.Where(t => t.YCoordinate % 2 == 0)) {
			t.overlays.Add(testRoad);
		}
		Tile start = map.tileAt(3, 3);
		MapUnit unit = MakeUnit(true, 1, start);
		Tile citySite = map.tileAt(11, 11);
		City city = new(citySite, unit.owner, "City", ID.None("city"));
		citySite.cityAtTile = city;
		unit.owner.cities.Add(city);
		Assert.Equal(1.0f / 3, MovementCostFloor.MinimumTileCost(unit, start), 5);
	}

	[Fact]
	public void UnitWalkerSkipsTilesOffTheMap() {
		GameMap map = MakeMap(10, 10);
		Tile corner = map.tiles.First(t => t.neighbors.Values.Any(n => n == Tile.NONE));
		MapUnit unit = MakeUnit(true, 1, corner);
		unit.owner.isHuman = true;
		Assert.DoesNotContain(new UnitWalker(unit).getEdges(corner), e => e.current == Tile.NONE);
	}
}

// Regression tests for AI fixes that need a real game.
public sealed class FixAiGameTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly Player barbarians;

	public FixAiGameTests(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		barbarians = gameData.players.First(p => p.isBarbarians);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Proto(string name) {
		return gameData.unitPrototypes.First(p => p.name == name);
	}

	private static bool IsEmptyLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.OwningPlayer() == null;
	}

	private static C7GameData.AIData.CombatAIData DataOf(C7Engine.AI.UnitAI.CombatAI ai) {
		return ai.data;
	}

	// Turns the ring of tiles around `center` into coast, making it an island.
	private void MakeIsland(Tile center) {
		TerrainType coast = gameData.terrainTypes.First(t => t.Key == "coast");
		foreach (Tile n in center.neighbors.Values) {
			n.baseTerrainType = coast;
			n.overlayTerrainType = coast;
		}
		gameData.map.recomputeContinents();
	}

	// A land tile whose two-tile neighborhood is empty land.
	private Tile FindOpenLand(Func<Tile, bool> extra = null) {
		return gameData.map.tiles.First(t => IsEmptyLand(t) && (extra == null || extra(t))
			&& t.GetTilesWithinTileSquare(2).All(n => n != Tile.NONE && IsEmptyLand(n)));
	}

	[Fact]
	public void UnitsIgnoreBarbarianCampsTheyCantReach() {
		Tile camp = FindOpenLand(t => t.GetTilesWithinTileSquare(4).All(n => n != Tile.NONE && IsEmptyLand(n)));
		MakeIsland(camp);
		camp.hasBarbarianCamp = true;
		Tile start = camp.GetTilesWithinTileSquare(3).First(t => t.IsLand() && t.DistanceTo(camp) == 3);
		Assert.NotEqual(start.continent, camp.continent);
		foreach (Tile t in start.GetTilesWithinTileSquare(4)) {
			player.tileKnowledge.knownTiles.Add(t);
			// Only our camp is nearby.
			if (t != camp && t != Tile.NONE) {
				t.hasBarbarianCamp = false;
			}
		}

		MapUnit warrior = gameData.SpawnUnit(player, Proto("Warrior"), start);
		Assert.True(warrior.CanEnter(camp));
		C7GameData.UnitAI ai = PlayerAI.GetAIForUnit(warrior, player);
		Assert.False(ai is C7Engine.AI.UnitAI.CombatAI combat && DataOf(combat).destination == camp,
			"the warrior was sent to a camp it can't reach");

		// A reachable camp is still attacked.
		Tile reachable = start.GetTilesWithinTileSquare(2).First(t => t != start && IsEmptyLand(t) && t.continent == start.continent);
		reachable.hasBarbarianCamp = true;
		C7GameData.UnitAI ai2 = PlayerAI.GetAIForUnit(warrior, player);
		C7Engine.AI.UnitAI.CombatAI combat2 = Assert.IsType<C7Engine.AI.UnitAI.CombatAI>(ai2);
		C7GameData.AIData.CombatAIData data = DataOf(combat2);
		Assert.Equal(reachable, data.destination);
		Assert.NotEmpty(data.path.path);
	}

	[Fact]
	public void WaitingToAttackKeepsThePathStep() {
		Tile start = FindOpenLand();
		(TileDirection dir, Tile enemyTile) = start.neighbors.First(p => p.Value.IsLand());
		Tile beyond = enemyTile.neighbors[dir];
		MapUnit attacker = gameData.SpawnUnit(player, Proto("Warrior"), start);
		gameData.SpawnUnit(barbarians, Proto("Warrior"), enemyTile);
		attacker.hasAttackedThisTurn = true;

		TilePath path = new(beyond, new Queue<Tile>(new[] { enemyTile, beyond }));
		C7GameData.UnitAI ai = new ExplorerAI(null);
		C7GameData.UnitAI.MoveResult result = ai.TryToMoveAlongPath(attacker, ref path);

		Assert.Equal(C7GameData.UnitAI.Result.InProgress, result.Result);
		Assert.False(result.IsMoveRequested);
		Assert.False(attacker.movementPoints.canMove);
		Assert.Equal(2, path.PathLength());
		Assert.Equal(enemyTile, path.PeekNext());
	}

	[Fact]
	public void UnitsThatCantAttackGiveUpOnPathsThroughEnemies() {
		Tile start = FindOpenLand();
		Tile enemyTile = start.neighbors.Values.First(t => t.IsLand());
		UnitPrototype wall = new() { name = "Wall", attack = 0, defense = 2, movement = 1 };
		wall.categories.Add("Land");
		MapUnit defender = gameData.SpawnUnit(player, Proto("Warrior"), start);
		defender.unitType = wall;
		gameData.SpawnUnit(barbarians, Proto("Warrior"), enemyTile);
		Assert.False(defender.CanAttack());
		Assert.True(defender.CanEnterForcefully(enemyTile));

		TilePath path = new(enemyTile, new Queue<Tile>(new[] { enemyTile }));
		C7GameData.UnitAI ai = new ExplorerAI(null);
		Assert.Equal(C7GameData.UnitAI.Result.Error, ai.TryToMoveAlongPath(defender, ref path).Result);
	}

	// A plan that never moves the unit or uses its movement points must not
	// keep the unit's turn going forever.
	private sealed class StuckAI : C7GameData.UnitAI {
		public int calls;
		C7GameData.UnitAI.MoveResult C7GameData.UnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			if (++calls > 1000) {
				throw new Exception("PlayTurn doesn't stop");
			}
			return C7GameData.UnitAI.MoveResult.MoveRequested(System.Threading.Tasks.Task.FromResult(true));
		}
		public string SummarizePlan() => "stuck";
		public void UpdateOnDeath() { }
	}

	[Fact]
	public async System.Threading.Tasks.Task PlayTurnStopsWhenNothingHappens() {
		Tile start = FindOpenLand();
		MapUnit unit = gameData.SpawnUnit(player, Proto("Warrior"), start);
		StuckAI ai = new();
		C7GameData.UnitAI.Result result = await ((C7GameData.UnitAI)ai).PlayTurn(player, unit);
		Assert.Equal(C7GameData.UnitAI.Result.InProgress, result);
		Assert.True(ai.calls < 10);
	}

	[Fact]
	public async System.Threading.Tasks.Task EscortIsDoneOnceItsSettlerFoundsACity() {
		MapUnit settler = player.units.First(u => u.unitType.isSettler);
		Tile site = settler.location;
		MapUnit escort = gameData.SpawnUnit(player, Proto("Warrior"), site);
		SettlerAI settlerAi = new(new C7GameData.AIData.SettlerAIData() {
			goal = C7GameData.AIData.SettlerAIData.SettlerGoal.BUILD_CITY,
			destination = site,
			escort = escort,
		});
		settler.currentAI = settlerAi;
		EscortAI escortAi = new(new C7GameData.AIData.EscortAIData() { unitToEscort = settler });
		escort.currentAI = escortAi;

		C7GameData.UnitAI.Result result = await ((C7GameData.UnitAI)escortAi).PlayTurn(player, escort);
		Assert.NotNull(site.cityAtTile);
		Assert.Equal(C7GameData.UnitAI.Result.Done, result);
	}

	[Fact]
	public async System.Threading.Tasks.Task EscortKeepsItsSettlerWhenTheSettlersPlanFails() {
		MapUnit settler = player.units.First(u => u.unitType.isSettler);
		Tile site = settler.location;
		MapUnit escort = gameData.SpawnUnit(player, Proto("Warrior"), site);
		SettlerAI settlerAi = new(new C7GameData.AIData.SettlerAIData() {
			goal = C7GameData.AIData.SettlerAIData.SettlerGoal.BUILD_CITY,
			destination = site,
			escort = escort,
		});
		settler.currentAI = settlerAi;
		EscortAI escortAi = new(new C7GameData.AIData.EscortAIData() { unitToEscort = settler });
		escort.currentAI = escortAi;

		// A city founded next to the settler's destination makes its plan fail.
		Tile next = site.neighbors.Values.First(n => n != Tile.NONE && n.IsLand() && n.IsAllowCities() && !n.HasCity());
		CityInteractions.BuildCity(next, player, "Neighbor");

		C7GameData.UnitAI.Result result = await ((C7GameData.UnitAI)escortAi).PlayTurn(player, escort);
		Assert.Equal(C7GameData.UnitAI.Result.InProgress, result);
		Assert.Same(escortAi, escort.currentAI);
		SettlerAI newSettlerAi = Assert.IsType<SettlerAI>(settler.currentAI);
		Assert.NotSame(settlerAi, newSettlerAi);
		Assert.Same(escort, newSettlerAi.data.escort);
		Assert.Same(settler, escortAi.data.unitToEscort);
	}
}

public sealed class FixAiWorkerTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;

	public FixAiWorkerTests(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	// A coastal city whose only other land is a tile across the water: the
	// worker can't get there, so it must not be sent there.
	[Fact]
	public void WorkersSkipTilesTheyCantReach() {
		MapUnit settler = player.units.First(u => u.unitType.isSettler);
		Tile site = settler.location;
		TerrainType coast = gameData.terrainTypes.First(t => t.Key == "coast");
		TerrainType grassland = gameData.terrainTypes.First(t => t.Key == "grassland");
		Tile island = site.GetTilesWithinTileSquare(2).First(t => t != Tile.NONE && t.DistanceTo(site) == 2);
		foreach (Tile t in site.GetTilesWithinTileSquare(4)) {
			if (t == Tile.NONE || t == site) {
				continue;
			}
			TerrainType terrain = t == island ? grassland : coast;
			t.baseTerrainType = terrain;
			t.overlayTerrainType = terrain;
			t.Resource = Resource.NONE;
			foreach (MapUnit u in t.unitsOnTile.ToList()) {
				gameData.RemoveUnit(u);
			}
		}
		gameData.map.recomputeContinents();
		Assert.NotEqual(site.continent, island.continent);

		City city = CityInteractions.BuildCity(site, player, player.GetNextCityName());
		city.residents[0].tileWorked = island;
		MapUnit worker = gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Worker"), site);

		C7GameData.AIData.WorkerAIData plan = WorkerAI.MakeAiData(worker, player);
		Assert.True(plan == null || plan.destination != island, "the worker was sent across the water");
	}

	// A mine and irrigation replace each other, and whichever one a tile has
	// can't be built again. Workers used to see the other one as a pure gain
	// and swap the two forever; once the better one is built, it must stay.
	[Fact]
	public void WorkersDontSwapMinesAndIrrigationBackAndForth() {
		MapUnit settler = player.units.First(u => u.unitType.isSettler);
		Tile site = settler.location;
		City city = CityInteractions.BuildCity(site, player, player.GetNextCityName());

		// Desert takes both a mine and irrigation, whatever the government.
		// Knowing a tech that irrigates anywhere rules out water access.
		TerrainType desert = gameData.terrainTypes.First(t => t.Key == "desert");
		Tile tile = site.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity());
		tile.baseTerrainType = desert;
		tile.overlayTerrainType = desert;
		tile.Resource = Resource.NONE;
		tile.overlays.Clear();
		foreach (MapUnit u in tile.unitsOnTile.ToList()) {
			gameData.RemoveUnit(u);
		}
		player.knownTechs.Add(gameData.techs.First(t => t.EnablesIrrigationEverywhere).id);
		city.residents[0].tileWorked = tile;
		MapUnit worker = gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Worker"), tile);

		// Finish whatever job the worker picks for the tile, several times
		// over, noting what ends up on the tile.
		List<string> history = new();
		for (int i = 0; i < 6; ++i) {
			C7GameData.AIData.WorkerAIData plan = WorkerAI.MakeAiData(worker, player);
			if (plan == null || plan.destination != tile) {
				break;
			}
			plan.workerMove.OnComplete(player, tile);
			TerrainImprovement built = tile.overlays.ImprovementAtLayer(TerrainImprovement.Layer.ResourceDevelopment);
			if (built != null && (history.Count == 0 || history[^1] != built.key)) {
				history.Add(built.key);
			}
		}

		Assert.Equal(new List<string> { Tile.TileOverlays.IRRIGATION }, history);
	}
}

public sealed class FixAiProductionTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;

	public FixAiProductionTests(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	// Each counted spot rules out every other spot within two tiles of it,
	// as founding a city there would.
	[Fact]
	public void OpenCitySpotsAreAtLeastThreeTilesApart() {
		City city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		foreach (Tile t in gameData.map.tiles) {
			player.tileKnowledge.knownTiles.Add(t);
		}

		List<Tile> chosen = new();
		foreach (Tile t in SettlerLocationAI.GetScoredSettlerCandidates(city.location, player).OrderByDescending(p => p.Value).Select(p => p.Key)) {
			if (chosen.All(c => c.DistanceTo(t) > 2)) {
				chosen.Add(t);
			}
		}
		Assert.True(chosen.Count > 1);

		int counted = ChooseProducible.NumberOfReachableOpenCitySpots(city);
		Assert.Equal(chosen.Count, counted);
	}

	// With no option that has any attack (or defense), units used to all score
	// NaN and could never be chosen.
	[Fact]
	public void UnitsCanBeChosenWhenNoOptionCanAttack() {
		City city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		UnitPrototype wall = new() { name = "Wall", attack = 0, defense = 1, movement = 1, shieldCost = 10 };
		wall.categories.Add("Land");
		ChooseProducible.ProducibleStats stats = ChooseProducible.CalculateStats(city, player, new List<IProducible> { wall });
		float score = ChooseProducible.ScoreUnit(stats, city, player, wall);
		Assert.False(float.IsNaN(score));
	}

	// The city tile's yield depends on its owner's traits, so a cached yield
	// must not survive the city changing hands.
	[Fact]
	public void SettlerYieldCacheNoticesCityOwnerChange() {
		City city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		SettlerLocationAI.TileYieldEntry entry = new();
		entry.Capture(city.location);
		Assert.True(entry.Matches(city.location));

		city.owner = gameData.players.First(p => p != player && !p.isBarbarians);
		Assert.False(entry.Matches(city.location));
	}

	// After a unit is given a plan to defend a city, asking again (in the
	// same turn, from the same place) must count it as on its way there.
	[Fact]
	public void DefenderPlanningCountsTheJustAssignedDefender() {
		City city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		Tile elsewhere = city.location.GetTilesWithinTileSquare(3).First(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.continent == city.location.continent && t.DistanceTo(city.location) >= 2);
		MapUnit unit = gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Warrior"), elsewhere);

		int EnRoute() {
			return C7Engine.AI.UnitAI.DefenderAI.GetSnapshot(unit, player).enRoute.Sum();
		}

		Assert.Equal(0, EnRoute());
		C7GameData.AIData.DefenderAIData data = C7Engine.AI.UnitAI.DefenderAI.MakeAiDataForDefendAtRiskCity(unit, player, int.MaxValue);
		Assert.Equal(city.location, data.destination);
		unit.currentAI = new C7Engine.AI.UnitAI.DefenderAI(data);
		Assert.Equal(1, EnRoute());
	}

	private sealed class FixedPriority : C7Engine.AI.StrategicAI.StrategicPriority {
		public FixedPriority(float weight) {
			calculatedWeight = weight;
		}
		public override void CalculateWeightAndMetadata(Player player) { }
	}

	private static C7Engine.AI.StrategicAI.StrategicPriority ChooseWeighted(List<C7Engine.AI.StrategicAI.StrategicPriority> options) {
		return C7Engine.AI.StrategicPriorityArbitrator.ChooseWeightedPriority(options, Weighting.WEIGHTED_QUADRATIC);
	}

	[Fact]
	public void WeightedPriorityChoiceIgnoresNegativeWeightsAndPicksFromTheList() {
		FixedPriority negative = new(-100);
		FixedPriority positive = new(1);
		for (int i = 0; i < 50; ++i) {
			Assert.Same(positive, ChooseWeighted(new() { negative, positive }));
		}

		// With nothing positive, the pick still comes from the list.
		FixedPriority a = new(-5);
		FixedPriority b = new(0);
		Assert.Same(b, ChooseWeighted(new() { a, b }));
	}

	[Fact]
	public void WarIsNotPlannedAgainstPlayersOnOtherContinents() {
		Player rival = gameData.players.First(p => p != player && !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		City ours = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
		City theirs = CityInteractions.BuildCity(rival.units.First(u => u.unitType.isSettler).location, rival, rival.GetNextCityName());
		player.EnsureRelationshipExists(rival);
		foreach (ID other in player.playerRelationships.Keys.Where(id => id != rival.id).ToList()) {
			player.playerRelationships.Remove(other);
		}
		// A stronger rival scales its (very negative) score towards zero.
		for (int i = 0; i < 5; ++i) {
			gameData.SpawnUnit(rival, gameData.unitPrototypes.First(p => p.name == "Warrior"), theirs.location);
		}
		theirs.location.continent = ours.location.continent + 100000;

		Player picked = C7GameData.AIData.WarPriority.PickPlayerToFight(player);
		Assert.Null(picked);
	}
}

public sealed class FixAiMapGeneratorTests {
	private static GameMap Generate(WorldCharacteristics.Landform landform, int seed) {
		SaveGame save = C7Engine.Lua.GameMode.Load(PathUtils.GameModesDir, new C7Engine.Lua.GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = landform,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Wet,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() {
				width = 60,
				height = 60,
				numberOfCivs = 4,
				distanceBetweenCivs = 10,
				techRate = 240,
				optimalNumberOfCities = 12,
			},
			mapSeed = seed,
		};
		return MapGenerator.GenerateMap(wc);
	}

	// Rivers reaching the edge of the map used to set river flags on the
	// shared Tile.NONE, which leaked into later maps.
	[Fact]
	public void RiversNeverMarkTheTileOffTheMap() {
		Tile none = Tile.NONE;
		try {
			GameMap map = FixAiPathingTests.MakeMap(10, 10);
			TileDirection[] diagonals = { TileDirection.SOUTHWEST, TileDirection.SOUTHEAST, TileDirection.NORTHWEST, TileDirection.NORTHEAST };
			foreach (TileDirection dir in diagonals) {
				MapGenerator.SetRiverFlag(Tile.NONE, dir);
			}
			// Draw every diagonal river edge from every corner on the top and
			// bottom rows, where one of the two tiles sharing it is off the map.
			foreach (Tile t in map.tiles.Where(t => t.neighbors.Get(TileDirection.NORTHEAST) == Tile.NONE || t.neighbors.Get(TileDirection.SOUTHEAST) == Tile.NONE)) {
				// River corners sit between tiles: one step left, right, up or
				// down from a tile's center.
				foreach ((int cx, int cy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) {
					(int x, int y) from = (t.XCoordinate + cx, t.YCoordinate + cy);
					foreach ((int dx, int dy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) }) {
						MapGenerator.setRiverFlags(map, from, (from.x + dx, from.y + dy));
					}
				}
			}
			Assert.False(none.BordersRiver());
			Assert.Contains(map.tiles, t => t.BordersRiver());
		} finally {
			none.riverNorth = none.riverNortheast = none.riverEast = none.riverSoutheast = false;
			none.riverSouth = none.riverSouthwest = none.riverWest = none.riverNorthwest = false;
		}
	}
}

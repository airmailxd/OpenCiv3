using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Regression tests for fixes to game state, saving and loading.
public class FixStateSaveTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly ID originalUIController;
	private readonly ID originalActivePlayer;

	public FixStateSaveTests(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.animationsEnabled = false;
		originalUIController = EngineStorage.uiControllerID;
		originalActivePlayer = EngineStorage.activePlayerID;
	}

	public void Dispose() {
		DrainMessages();
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.pendingMessages.Dequeue();
		}
		UnitInteractions.ClearWaitQueue();
		EngineStorage.uiControllerID = originalUIController;
		EngineStorage.activePlayerID = originalActivePlayer;
		EngineStorage.ClearUnhandledEngineException();
	}

	private C7GameData.GameData NewGame() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		return gameData;
	}

	private static void DrainMessages() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
		while (EngineStorage.TryDequeueNextAnimationMessage(out _)) { }
	}

	private static bool CanFoundCityAt(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable() && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || (!n.HasCity() && n.neighbors.Values.All(nn => nn == Tile.NONE || !nn.HasCity())));
	}

	private static void AssertActiveTilesMatchFullRecompute(Player player, string context) {
		player.tileKnowledge.RecomputeActiveTiles();
		HashSet<Tile> expected = player.tileKnowledge.ComputeActiveTilesFromScratch();
		HashSet<Tile> actual = player.tileKnowledge.ActiveTiles();
		Assert.True(expected.SetEquals(actual),
			$"{context}: active tiles of {player} differ: {expected.Except(actual).Count()} missing, {actual.Except(expected).Count()} extra");
	}

	// Item: the tile change journal kept every replaced game alive.

	[MethodImpl(MethodImplOptions.NoInlining)]
	private List<WeakReference> ReplaceAndForget(SaveGame snapshot) {
		C7GameData.GameData gameData = CreateGame.ReplaceWithSnapshot(snapshot, fixture.behaviors);
		foreach (Player p in gameData.players) {
			p.tileKnowledge.RecomputeActiveTiles();
		}
		// Busy units remember the game they were asked about.
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.uiControllerID = human.id;
		UnitInteractions.getNextSelectedUnit();
		DrainMessages();
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.pendingMessages.Dequeue();
		}
		// The game, its map and a tile whose owner was recorded when the
		// snapshot was loaded.
		return [
			new WeakReference(gameData),
			new WeakReference(gameData.map),
			new WeakReference(gameData.cities[0].location),
		];
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private SaveGame MakeSnapshot() {
		C7GameData.GameData gameData = NewGame();
		// Cities, so that loading the snapshot records their tiles' owners.
		foreach (Player player in gameData.players.Where(p => !p.isBarbarians).ToList()) {
			MapUnit settler = player.units.FirstOrDefault(u => u.unitType.isSettler);
			if (settler != null && !settler.location.HasCity()) {
				CityInteractions.BuildCity(settler.location, player, player.GetNextCityName());
			}
		}
		DrainMessages();
		Assert.NotEmpty(gameData.cities);
		return SaveGame.FromGameData(gameData);
	}

	[Fact]
	public void ReplacedGamesCanBeCollected() {
		SaveGame snapshot = MakeSnapshot();
		List<List<WeakReference>> games = new();
		for (int i = 0; i < 4; ++i) {
			games.Add(ReplaceAndForget(snapshot));
		}
		// The last game is still the current one.
		List<WeakReference> replaced = games.Take(games.Count - 1).SelectMany(g => g).ToList();

		for (int i = 0; i < 3; ++i) {
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}
		foreach (WeakReference r in replaced) {
			Assert.False(r.IsAlive, $"a replaced game's {r.Target?.GetType().Name} is still reachable");
		}

		// The current game still works incrementally after the reset.
		C7GameData.GameData current = EngineStorage.gameData;
		foreach (Player p in current.players) {
			AssertActiveTilesMatchFullRecompute(p, "after replacing the game");
		}
	}

	[Fact]
	public void ResettingTheJournalMakesEveryoneRecompute() {
		C7GameData.GameData gameData = NewGame();
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.tileKnowledge.AddTilesToKnown(player.units[0].location);
		int before = player.tileKnowledge.fullRecomputeCount;

		TileChangeJournal.Reset();
		AssertActiveTilesMatchFullRecompute(player, "after a reset");
		Assert.Equal(before + 1, player.tileKnowledge.fullRecomputeCount);
	}

	// Item: units produced in cities and upgraded units are journaled, so the
	// active tiles don't need to walk every unit and city.

	// A flat inland tile, where a unit without radar sees no further than a
	// city's tiles reach, and only radar sees two tiles away.
	private static Tile FlatInlandCitySite(C7GameData.GameData gameData) {
		return gameData.map.tiles.First(t => CanFoundCityAt(t)
			&& t.GetTilesWithinTileSquare(2).Count(Tile.IsTileValid) == 25
			&& t.GetTilesWithinTileSquare(2).All(n => n.IsLand() && n.overlayTerrainType.height < 2));
	}

	// A new city's borders and their neighbors already reach two tiles out,
	// so with the standard radar range whether radar adds active tiles
	// depends on the generated map. Radar that sees three tiles always does.
	// (saveGame is a fresh clone per test, so this doesn't leak.)
	private static void WidenRadar(C7GameData.GameData gameData) {
		gameData.rules.RadarTileVisibility = 3;
	}

	[Fact]
	public void ActiveTilesNoticeUnitsProducedInCities() {
		C7GameData.GameData gameData = NewGame();
		WidenRadar(gameData);
		Player player = gameData.players.First(p => !p.isBarbarians);
		UnitPrototype radar = new() { name = "TestRadarUnit", shieldCost = 20, movement = 1 };
		radar.categories.Add("Land");
		radar.hasRadar = true;
		gameData.unitPrototypes.Add(radar);
		Tile tile = FlatInlandCitySite(gameData);
		City city = CityInteractions.BuildCity(tile, player, "Producer");
		DrainMessages();
		player.tileKnowledge.AddTilesToKnown(tile);
		AssertActiveTilesMatchFullRecompute(player, "after founding");
		int full = player.tileKnowledge.fullRecomputeCount;

		int beforeCount = player.tileKnowledge.ComputeActiveTilesFromScratch().Count;
		city.AddUnit(radar, gameData);
		DrainMessages();
		Assert.True(player.tileKnowledge.ComputeActiveTilesFromScratch().Count > beforeCount);
		AssertActiveTilesMatchFullRecompute(player, "after producing a unit");
		Assert.Equal(full, player.tileKnowledge.fullRecomputeCount);
	}

	[Fact]
	public void ActiveTilesNoticeUpgrades() {
		C7GameData.GameData gameData = NewGame();
		WidenRadar(gameData);
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.gold = 10000;

		UnitPrototype scout = new() { name = "TestScout", shieldCost = 10, movement = 1 };
		UnitPrototype radar = new() { name = "TestRadarScout", shieldCost = 20, movement = 1 };
		foreach (UnitPrototype p in new[] { scout, radar }) {
			p.categories.Add("Land");
			p.actions.Add(UnitAction.Upgrade);
			p.producibleBy.Add(player.civilization);
			gameData.unitPrototypes.Add(p);
		}
		radar.hasRadar = true;
		scout.upgradesTo.Add(radar);

		Tile tile = FlatInlandCitySite(gameData);
		City city = CityInteractions.BuildCity(tile, player, "Upgrader");
		SaveBuilding barracks = new() { name = "TestBarracks" };
		barracks.flags.Add(SaveBuilding.Flag.VeteranGroundUnits);
		city.AddBuilding(new Building(barracks, new C7GameData.GameData()));
		DrainMessages();

		MapUnit unit = gameData.SpawnUnit(player, scout, tile);
		player.tileKnowledge.AddTilesToKnown(tile);
		AssertActiveTilesMatchFullRecompute(player, "before upgrading");
		int full = player.tileKnowledge.fullRecomputeCount;

		Assert.Same(radar, unit.GetAvailableUpgrade());
		int beforeCount = player.tileKnowledge.ComputeActiveTilesFromScratch().Count;
		Assert.True(unit.Upgrade());
		// Radar lets the unit see further, which the active tiles must notice.
		Assert.True(player.tileKnowledge.ComputeActiveTilesFromScratch().Count > beforeCount);
		AssertActiveTilesMatchFullRecompute(player, "after upgrading to a radar unit");
		Assert.Equal(full, player.tileKnowledge.fullRecomputeCount);
	}

	// Item: terrain changes recorded each tile once.

	[Fact]
	public void TerrainChangesAreRecordedOncePerTile() {
		C7GameData.GameData gameData = NewGame();
		Tile tile = gameData.map.tiles.First(t => t.XCoordinate > 6 && t.YCoordinate > 6
			&& t.XCoordinate < gameData.map.numTilesWide - 6 && t.YCoordinate < gameData.map.numTilesTall - 6);
		long head = TileChangeJournal.head;
		TileChangeJournal.RecordTerrainChange(tile);
		List<Tile> recorded = new();
		for (long i = head; i < TileChangeJournal.head; ++i) {
			recorded.Add(TileChangeJournal.EntryAt(i));
		}
		Assert.Equal(recorded.Count, recorded.Distinct().Count());
		Assert.Equal(25, recorded.Count);
	}

	// Item: the wait queue.

	[Fact]
	public void WaitingSurvivesASnapshotAndHandsOutTheCurrentGamesUnit() {
		C7GameData.GameData gameData = NewGame();
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.uiControllerID = human.id;
		UnitInteractions.ClearWaitQueue();
		List<MapUnit> units = human.units.Where(u => u.movementPoints.canMove && !u.isFortified && !u.IsBusy()).ToList();
		Assert.True(units.Count >= 2);

		UnitInteractions.waitUnit(units[0].id);

		C7GameData.GameData replaced = CreateGame.ReplaceWithSnapshot(SaveGame.FromGameData(gameData), fixture.behaviors);
		MapUnit first = UnitInteractions.getNextSelectedUnit();
		Assert.NotEqual(units[0].id, first.id);
		Assert.Same(replaced.GetUnit(first.id), first);

		foreach (MapUnit u in replaced.GetUIControllerPlayer().units.Where(u => u.movementPoints.canMove && !u.isFortified && !u.IsBusy())) {
			if (u.id != units[0].id) {
				UnitInteractions.waitUnit(u.id);
			}
		}
		MapUnit waited = UnitInteractions.getNextSelectedUnit();
		Assert.Equal(units[0].id, waited.id);
		Assert.Same(replaced.GetUnit(units[0].id), waited);
	}

	[Fact]
	public void WaitingUnitsDontPassToTheNextHotseatPlayer() {
		C7GameData.GameData gameData = NewGame();
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		Player a = civs[0];
		Player b = civs[1];
		a.isHuman = true;
		b.isHuman = true;
		EngineStorage.uiControllerID = a.id;
		UnitInteractions.ClearWaitQueue();
		foreach (MapUnit u in a.units) {
			UnitInteractions.waitUnit(u.id);
		}

		EngineStorage.uiControllerID = b.id;
		foreach (MapUnit u in b.units) {
			u.movementPoints.onConsumeAll();
		}
		// B has nothing to move, and A's waiting units are not B's.
		Assert.Same(MapUnit.NONE, UnitInteractions.getNextSelectedUnit());

		// Nor do they come back when A's turn comes around again.
		EngineStorage.uiControllerID = a.id;
		MapUnit next = UnitInteractions.getNextSelectedUnit();
		Assert.Same(a, next.owner);
	}

	[Fact]
	public void WaitingUnitsThatChangedHandsOrDiedAreSkipped() {
		C7GameData.GameData gameData = NewGame();
		Player human = gameData.players.First(p => p.isHuman);
		Player other = gameData.players.First(p => !p.isBarbarians && p != human);
		EngineStorage.uiControllerID = human.id;
		UnitInteractions.ClearWaitQueue();
		List<MapUnit> units = human.units.ToList();
		foreach (MapUnit u in units) {
			UnitInteractions.waitUnit(u.id);
		}
		MapUnit dead = units[0];
		gameData.RemoveUnit(dead);
		MapUnit captured = units.FirstOrDefault(u => u.unitType.isWorker || u.unitType.isSettler);
		if (captured != null && captured != dead) {
			gameData.CaptureUnit(captured, other);
		}
		DrainMessages();

		HashSet<MapUnit> handedOut = new();
		for (MapUnit u = UnitInteractions.getNextSelectedUnit(); u != MapUnit.NONE; u = UnitInteractions.getNextSelectedUnit()) {
			Assert.Same(human, u.owner);
			Assert.True(handedOut.Add(u));
		}
		Assert.DoesNotContain(dead, handedOut);
	}

	[Fact]
	public void NewGameClearsTheWaitQueue() {
		C7GameData.GameData gameData = NewGame();
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.uiControllerID = human.id;
		foreach (MapUnit u in human.units) {
			UnitInteractions.waitUnit(u.id);
		}
		EngineStorage.ResetForNewGame();
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.uiControllerID = human.id;
		// The units are selected normally rather than as waiting units.
		MapUnit first = UnitInteractions.getNextSelectedUnit();
		Assert.Same(human.units.First(u => u.movementPoints.canMove && !u.isFortified && !u.IsLockedInArmy() && !u.IsBusy()), first);
	}

	// Items: unit paths and locations in saves.

	[Fact]
	public void UnitPathsSurviveASave() {
		C7GameData.GameData gameData = NewGame();
		MapUnit unit = gameData.mapUnits.First(u => u.unitType.IsLandUnit());
		Tile a = unit.location.neighbors.Values.First(t => t != Tile.NONE);
		Tile b = a.neighbors.Values.First(t => t != Tile.NONE && t != unit.location);
		unit.path = new TilePath(b, new Queue<Tile>([a, b]));

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).Clone().ToGameData(fixture.behaviors);
		MapUnit reloaded = loaded.GetUnit(unit.id);
		Assert.NotNull(reloaded.path);
		Assert.Equal(new[] { (a.XCoordinate, a.YCoordinate), (b.XCoordinate, b.YCoordinate) },
			reloaded.path.path.Select(t => (t.XCoordinate, t.YCoordinate)).ToArray());
		Assert.Same(loaded.map.tileAt(b.XCoordinate, b.YCoordinate), reloaded.path.destination);
		Assert.Same(loaded.map.tileAt(a.XCoordinate, a.YCoordinate), reloaded.path.path.Peek());

		// Units without a path have none after loading.
		MapUnit other = loaded.mapUnits.First(u => u.id != unit.id);
		Assert.True(other.path == null || other.path.PathLength() <= 0);
	}

	[Fact]
	public void PathsLeavingTheMapAreDropped() {
		C7GameData.GameData gameData = NewGame();
		SaveGame save = SaveGame.FromGameData(gameData);
		SaveUnit saveUnit = save.Units[0];
		saveUnit.path = [new TileLocation(saveUnit.currentLocation.X + 1, saveUnit.currentLocation.Y + 1), new TileLocation(-50, -50)];

		C7GameData.GameData loaded = save.ToGameData(fixture.behaviors);
		Assert.Null(loaded.GetUnit(saveUnit.id).path);
	}

	[Fact]
	public void PreviousLocationIsRestoredFromItself() {
		C7GameData.GameData gameData = NewGame();
		SaveGame save = SaveGame.FromGameData(gameData);
		SaveUnit saveUnit = save.Units[0];
		TileLocation current = saveUnit.currentLocation;

		saveUnit.previousLocation = new TileLocation();
		Assert.Same(Tile.NONE, ToMapUnit(saveUnit, save, gameData).previousLocation);

		saveUnit.previousLocation = new TileLocation(current.X + 2, current.Y);
		Assert.Same(gameData.map.tileAt(current.X + 2, current.Y), ToMapUnit(saveUnit, save, gameData).previousLocation);

		// A unit off the map still keeps the previous location it had.
		saveUnit.currentLocation = new TileLocation();
		int noneUnits = Tile.NONE.unitsOnTile.Count;
		MapUnit offMap = ToMapUnit(saveUnit, save, gameData);
		Assert.Same(gameData.map.tileAt(current.X + 2, current.Y), offMap.previousLocation);
		Assert.Equal(noneUnits, Tile.NONE.unitsOnTile.Count);
	}

	private static MapUnit ToMapUnit(SaveUnit saveUnit, SaveGame save, C7GameData.GameData gameData) {
		// Remove the unit from its tile again, so the game is left alone.
		MapUnit unit = saveUnit.ToMapUnit(gameData.unitPrototypes, save.ExperienceLevels, gameData.players, gameData.Terraforms, gameData.map);
		unit.location.unitsOnTile.Remove(unit);
		return unit;
	}

	[Fact]
	public void UnitsOffTheMapAreLeftOutRatherThanPutOnNone() {
		C7GameData.GameData gameData = NewGame();
		SaveGame save = SaveGame.FromGameData(gameData);
		SaveUnit saveUnit = save.Units.First(u => save.Units.All(o => o.loadedOnUnitId != u.id));
		saveUnit.currentLocation = new TileLocation(-7, -7);
		int noneUnits = Tile.NONE.unitsOnTile.Count;

		C7GameData.GameData loaded = save.ToGameData(fixture.behaviors);
		Assert.Equal(noneUnits, Tile.NONE.unitsOnTile.Count);
		Assert.Null(loaded.GetUnit(saveUnit.id));
		Assert.DoesNotContain(loaded.players.SelectMany(p => p.units), u => u.id == saveUnit.id);
		Assert.Equal(save.Units.Count - 1, loaded.mapUnits.Count);
	}

	// Item: known techs were shared between civs, saves and games.

	[Fact]
	public void LearningATechDoesntChangeTheSaveOrTheCiv() {
		SaveGame save = fixture.saveGame;
		C7GameData.GameData gameData = NewGame();
		Player player = gameData.players.First(p => !p.isBarbarians);
		SavePlayer savePlayer = save.Players.Single(p => p.id == player.id);
		Civilization saveCiv = save.Civilizations.Single(c => c.name == player.civilization.name);
		int savedTechs = savePlayer.knownTechs.Count;
		int startingTechs = saveCiv.startingTechs.Count;

		Tech unknown = gameData.techs.First(t => !player.knownTechs.Contains(t.id));
		player.knownTechs.Add(unknown.id);

		Assert.Equal(savedTechs, savePlayer.knownTechs.Count);
		Assert.Equal(startingTechs, saveCiv.startingTechs.Count);
		Assert.Equal(startingTechs, player.civilization.startingTechs.Count);
		Assert.DoesNotContain(unknown.id, NewGame().GetPlayer(player.id).knownTechs);

		// A save made from the game doesn't change with it either.
		SavePlayer saved = new(player);
		Tech another = gameData.techs.First(t => !player.knownTechs.Contains(t.id));
		player.knownTechs.Add(another.id);
		Assert.DoesNotContain(another.id, saved.knownTechs);
	}

	[Fact]
	public void SavedPrototypeActionsAreACopy() {
		C7GameData.GameData gameData = NewGame();
		UnitPrototype proto = gameData.unitPrototypes.First();
		SaveUnitPrototype saved = new(proto);
		Assert.NotSame(proto.actions, saved.actions);
		Assert.True(proto.actions.SetEquals(saved.actions));
	}

	// Item: the known tile decoder accepted values that don't fit in 32 bits.

	[Fact]
	public void TileIndicesRejectAnOverlongFifthByte() {
		static string Encode(params byte[] bytes) => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_');

		Assert.Throws<FormatException>(() => SavePlayer.DecodeTileIndices(Encode(0xff, 0xff, 0xff, 0xff, 0x1f)));
		Assert.Throws<FormatException>(() => SavePlayer.DecodeTileIndices(Encode(0x80, 0x80, 0x80, 0x80, 0x80, 0x00)));
		// The largest value that fits is fine.
		Assert.Single(SavePlayer.DecodeTileIndices(Encode(0xff, 0xff, 0xff, 0xff, 0x0f)));
	}

	// Item: city lookups rebuilt their index on every miss.

	[Fact]
	public void MissingCitiesDontRebuildTheIndex() {
		C7GameData.GameData gameData = NewGame();
		Player player = gameData.players.First(p => !p.isBarbarians);
		City city = CityInteractions.BuildCity(gameData.map.tiles.First(CanFoundCityAt), player, "Indexed");
		DrainMessages();
		Assert.Same(city, gameData.GetCity(city.id));

		FieldInfo indexField = typeof(C7GameData.GameData).GetField("cityIndexById", BindingFlags.NonPublic | BindingFlags.Instance);
		object index = indexField.GetValue(gameData);
		Assert.Null(gameData.GetCity(ID.FromString("no-city-1")));
		Assert.Null(gameData.GetCity(ID.FromString("no-city-2")));
		Assert.Same(index, indexField.GetValue(gameData));

		// A city swapped in without changing the count is still found.
		City swapped = new(city.location, player, "Swapped", ID.FromString("swapped-city-1"));
		int position = gameData.cities.IndexOf(city);
		gameData.cities[position] = swapped;
		Assert.Same(swapped, gameData.GetCity(swapped.id));
		Assert.Null(gameData.GetCity(city.id));
		gameData.cities[position] = city;
		Assert.Same(city, gameData.GetCity(city.id));
	}

	// Item: exceptions in message handlers nobody awaits were lost.

	[Fact]
	public void ExceptionsInMessageHandlersAreReported() {
		C7GameData.GameData gameData = NewGame();
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.activePlayerID = human.id;
		EngineStorage.ClearUnhandledEngineException();
		List<string> sources = new();
		void OnException(Exception e, string source) => sources.Add(source);
		EngineStorage.UnhandledEngineException += OnException;
		MapUnit unit = human.units[0];
		Tile location = unit.location;
		try {
			// A broken unit makes moving it throw.
			unit.location = null;
			new MsgMoveUnit(unit.id, TileDirection.NORTH) { playerID = human.id }.send();
			EngineStorage.ProcessNextMessageToEngine();
		} finally {
			unit.location = location;
			EngineStorage.UnhandledEngineException -= OnException;
		}
		Assert.NotNull(EngineStorage.LastUnhandledEngineException);
		Assert.Contains(nameof(MsgMoveUnit), sources);
	}

	[Fact]
	public void FaultedTasksNobodyAwaitsAreReported() {
		EngineStorage.ClearUnhandledEngineException();
		InvalidOperationException failure = new("boom");
		EngineStorage.ObserveTask(Task.FromException(failure), "test");
		Assert.Same(failure, EngineStorage.LastUnhandledEngineException);

		EngineStorage.ClearUnhandledEngineException();
		TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
		EngineStorage.ObserveTask(tcs.Task, "test");
		tcs.SetException(failure);
		SpinWait.SpinUntil(() => EngineStorage.LastUnhandledEngineException != null, 5000);
		Assert.Same(failure, EngineStorage.LastUnhandledEngineException);
	}

	// Item: paths from the network may leave the map.

	[Fact]
	public void PathsLeavingTheMapOrSkippingTilesAreRejected() {
		C7GameData.GameData gameData = NewGame();
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.activePlayerID = human.id;
		EngineStorage.ClearUnhandledEngineException();
		MapUnit unit = human.units.First(u => u.unitType.IsLandUnit());
		Tile start = unit.location;
		Tile far = gameData.map.tileAt(start.XCoordinate + 6, start.YCoordinate + 6);

		Assert.False(unit.IsFollowablePath(new TilePath(Tile.NONE, new Queue<Tile>([Tile.NONE]))));
		Assert.False(unit.IsFollowablePath(new TilePath(far, new Queue<Tile>([far]))));
		Tile neighbor = start.neighbors.Values.First(t => t != Tile.NONE);
		Assert.True(unit.IsFollowablePath(new TilePath(neighbor, new Queue<Tile>([neighbor]))));

		new MsgSetUnitPath(unit.id, new TilePath(Tile.NONE, new Queue<Tile>([neighbor, Tile.NONE]))) { playerID = human.id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		new MsgSetUnitPath(unit.id, new TilePath(far, new Queue<Tile>([far]))) { playerID = human.id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		DrainMessages();

		Assert.Same(start, unit.location);
		Assert.Null(EngineStorage.LastUnhandledEngineException);

		// A path that leaves the map partway is dropped when it gets there,
		// rather than throwing.
		unit.path = new TilePath(Tile.NONE, new Queue<Tile>([Tile.NONE]));
		Task move = unit.MoveAlongPath();
		Assert.True(move.IsCompletedSuccessfully);
		Assert.Null(unit.path);
	}

	// Item: the palace view's sections may come in any order.

	[Fact]
	public void PalaceViewRulesMayComeBeforeFilenames() {
		string path = System.IO.Path.GetTempFileName();
		try {
			System.IO.File.WriteAllLines(path, [
				"#PVRULES_EUROPEAN",
				"00",
				"10",
				"#PVICONS_EUROPEAN",
				"normal.pcx",
				"hover.pcx",
				"pressed.pcx",
				"#PVFNAME_EUROPEAN",
				"header",
				"base.pcx",
				"wall.pcx",
				"#PV_SPRITE_XLOCS",
				"1",
				"2",
				"#PV_SPRITE_YLOCS",
				"3",
				"4",
			]);
			var config = new C7Engine.PalaceMinigame.ConfigParser().Parse(path);
			var culture = config["european"];
			Assert.Equal(2, culture.Buildings.Count);
			Assert.Empty(culture.Buildings[0].Prerequisites);
			Assert.Equal([0], culture.Buildings[1].Prerequisites);
			Assert.EndsWith("hover.pcx", culture.ButtonTextures.Hover);
			Assert.Equal(2, culture.Buildings[1].X);
			Assert.Equal(4, culture.Buildings[1].Y);
		} finally {
			System.IO.File.Delete(path);
		}
	}

	// Item: other players' roads and improvements showed up in tiles the
	// player couldn't see.

	private static bool RemembersRoad(Player player, Tile tile) {
		Assert.True(player.tileKnowledge.TryGetRememberedImprovements(tile, out TerrainImprovement[] remembered));
		return remembered.Any(ti => ti.key == Tile.TileOverlays.ROAD);
	}

	[Fact]
	public void ImprovementsOutOfViewAreRememberedAsLastSeen() {
		C7GameData.GameData gameData = NewGame();
		Player player = gameData.players.First(p => !p.isBarbarians);
		TerrainImprovement road = gameData.terrainImprovements.First(ti => ti.key == Tile.TileOverlays.ROAD);
		player.tileKnowledge.RecomputeActiveTiles();
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& t.overlays.ImprovementAtLayer(TerrainImprovement.Layer.Roads) == null
			&& !player.tileKnowledge.isTileKnown(t) && !player.tileKnowledge.isActiveTile(t));

		// The player learns of the tile, then someone builds a road there
		// while they aren't looking.
		player.tileKnowledge.AddTileToKnown(tile);
		tile.overlays.Add(road);
		Assert.False(RemembersRoad(player, tile));

		// What the player remembers survives saving and loading.
		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(loaded);
		Player loadedPlayer = loaded.players.First(p => p.id == player.id);
		Tile loadedTile = loaded.map.tileAt(tile.XCoordinate, tile.YCoordinate);
		Assert.NotNull(loadedTile.overlays.ImprovementAtLayer(TerrainImprovement.Layer.Roads));
		Assert.False(RemembersRoad(loadedPlayer, loadedTile));

		// Seeing the tile again shows the road.
		loadedPlayer.tileKnowledge.AddTilesToKnown(loadedTile);
		Assert.True(RemembersRoad(loadedPlayer, loadedTile));
	}

	// Saving can happen from the UI while the engine changes the game, so
	// saving what the player remembers must not bring their active tiles up
	// to date (which changes them) but use them as they are.
	[Fact]
	public void SavingDoesntRecomputeActiveTiles() {
		C7GameData.GameData gameData = NewGame();
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.tileKnowledge.RecomputeActiveTiles();
		HashSet<Tile> activeBefore = player.tileKnowledge.ActiveTiles();

		// Something that would make the next update recompute everything.
		TileChangeJournal.InvalidateAll();
		int fullBefore = player.tileKnowledge.fullRecomputeCount;

		SaveGame.FromGameData(gameData);

		Assert.Equal(fullBefore, player.tileKnowledge.fullRecomputeCount);
		Assert.True(activeBefore.SetEquals(player.tileKnowledge.ActiveTiles()));
	}
}

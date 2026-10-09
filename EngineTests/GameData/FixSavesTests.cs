using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Regression tests for fixes to saving, loading, importing and setting up
// games.
public class FixSavesTests : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public FixSavesTests(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private C7GameData.GameData NewGame(SaveGame save) {
		C7GameData.GameData gameData = save.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		return gameData;
	}

	// Saves the game and loads it again, through JSON as a save file would.
	private C7GameData.GameData SaveAndLoad(C7GameData.GameData gameData) {
		byte[] json = SaveGame.FromGameData(gameData).ToCompactJSON();
		return NewGame(SaveGame.FromJSON(json));
	}

	// Item: the random number generator was re-seeded from the map seed on
	// every load, so a loaded game rolled different numbers.

	[Fact]
	public void GameRandomCarriesOnFromItsState() {
		GameRandom a = new(1234);
		for (int i = 0; i < 10; ++i) {
			a.Next();
		}
		GameRandom b = new(a.State);
		for (int i = 0; i < 100; ++i) {
			Assert.Equal(a.Next(1000), b.Next(1000));
			Assert.Equal(a.NextDouble(), b.NextDouble());
		}
	}

	[Fact]
	public void GameRandomStaysInRange() {
		GameRandom random = new(99);
		for (int i = 0; i < 1000; ++i) {
			int n = random.Next(-5, 5);
			Assert.InRange(n, -5, 4);
			Assert.InRange(random.Next(7), 0, 6);
			Assert.InRange(random.NextDouble(), 0.0, 0.9999999999);
			Assert.NotEqual(int.MaxValue, random.Next());
		}
		Assert.Equal(0, random.Next(0));
		Assert.Equal(3, random.Next(3, 3));
	}

	[Fact]
	public void LoadedGameRollsTheNumbersItWouldHaveRolled() {
		C7GameData.GameData gameData = NewGame(fixture.saveGame);
		for (int i = 0; i < 17; ++i) {
			C7GameData.GameData.rng.Next();
		}
		SaveGame save = SaveGame.FromGameData(gameData);
		List<int> expected = Enumerable.Range(0, 20).Select(_ => gameData.random.Next(1000)).ToList();

		C7GameData.GameData loaded = NewGame(SaveGame.FromJSON(save.ToCompactJSON()));
		List<int> actual = Enumerable.Range(0, 20).Select(_ => C7GameData.GameData.rng.Next(1000)).ToList();

		Assert.Same(loaded.random, C7GameData.GameData.rng);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void SaveWithoutRandomStateStartsFromSeedAndTurn() {
		SaveGame save = fixture.saveGame;
		save.RngState = null;
		save.Seed = 4321;
		save.TurnNumber = 12;
		C7GameData.GameData gameData = NewGame(save);

		GameRandom expected = new(GameRandom.InitialState(4321, 12));
		Assert.Equal(expected.Next(1000), gameData.random.Next(1000));
	}

	[Fact]
	public void SnapshotKeepsTheClientsRandomNumbers() {
		SaveGame snapshot = SaveGame.FromGameData(NewGame(fixture.saveGame));
		C7GameData.GameData client = NewGame(fixture.saveGame);
		Random clientRandom = client.random;

		C7GameData.GameData replaced = CreateGame.ReplaceWithSnapshot(snapshot, fixture.behaviors);

		Assert.Same(clientRandom, replaced.random);
	}

	// Item: saves had no format version.

	[Fact]
	public void SavesRecordTheirFormatVersion() {
		SaveGame save = SaveGame.FromGameData(NewGame(fixture.saveGame));
		Assert.Equal(SaveGame.CurrentFormatVersion, save.FormatVersion);

		save.FormatVersion = null;
		SaveGame old = SaveGame.Migrate(save);
		Assert.Equal(SaveGame.CurrentFormatVersion, old.FormatVersion);
	}

	[Fact]
	public void SavesFromANewerFormatAreRefused() {
		SaveGame save = SaveGame.FromGameData(NewGame(fixture.saveGame));
		save.FormatVersion = SaveGame.CurrentFormatVersion + 1;
		byte[] json = save.ToCompactJSON();

		Assert.Throws<NotSupportedException>(() => SaveGame.FromJSON(json));
	}

	// Item: the difficulty was loaded as a copy, so the score's difficulty
	// factor no longer found it among the difficulties.

	[Fact]
	public void LoadedDifficultyIsOneOfTheDifficulties() {
		SaveGame save = fixture.saveGame;
		Assert.True(save.Difficulties.Count > 2);
		save.GameDifficulty = save.Difficulties[2];
		C7GameData.GameData gameData = NewGame(save);

		C7GameData.GameData loaded = SaveAndLoad(gameData);

		Assert.Same(loaded.difficulties[2], loaded.gameDifficulty);
	}

	[Fact]
	public void DifficultyFromAnotherCopyOfTheRulesIsMatched() {
		SaveGame save = fixture.saveGame;
		SaveGame other = fixture.saveGame;
		save.GameDifficulty = other.Difficulties[1];

		Assert.Same(save.Difficulties[1], save.ResolveGameDifficulty());
	}

	// Item: the AI's priorities aren't saved, but their countdown was.

	[Fact]
	public void LoadedAIKeepsItsReevaluationCountdown() {
		// Priorities themselves aren't saved: PlayerAI works them out again
		// quietly on the AI's next turn. The countdown is kept, so saving a
		// loaded game again gives the same save.
		C7GameData.GameData gameData = NewGame(fixture.saveGame);
		foreach (Player p in gameData.players) {
			p.turnsUntilPriorityReevaluation = 7;
		}

		C7GameData.GameData loaded = SaveAndLoad(gameData);

		Assert.All(loaded.players, p => Assert.Equal(7, p.turnsUntilPriorityReevaluation));
		Assert.All(loaded.players, p => Assert.Empty(p.strategicPriorityData));
	}

	// Item: units' defensive bombards and players' skipFirstTurn weren't
	// saved.

	[Fact]
	public void DefensiveBombardsAndSkipFirstTurnAreSaved() {
		C7GameData.GameData gameData = NewGame(fixture.saveGame);
		MapUnit unit = gameData.mapUnits.First();
		unit.defensiveBombardsRemaining = 0;
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.skipFirstTurn = true;

		C7GameData.GameData loaded = SaveAndLoad(gameData);

		Assert.Equal(0, loaded.GetUnit(unit.id).defensiveBombardsRemaining);
		Assert.True(loaded.GetPlayer(player.id).skipFirstTurn);
	}

	// Item: unknown names in a save were loaded as nulls.

	[Fact]
	public void UnknownTileContentsAreLeftOut() {
		C7GameData.GameData gameData = NewGame(fixture.saveGame);
		SaveTile saveTile = new() {
			X = 0,
			Y = 0,
			baseTerrain = gameData.terrainTypes[0].Key,
			overlayTerrain = gameData.terrainTypes[0].Key,
			resource = "No Such Resource",
			overlays = ["noSuchImprovement", Tile.TileOverlays.ROAD],
		};

		Tile tile = saveTile.ToTile(gameData.terrainTypes, gameData.Resources, gameData.terrainImprovements);

		Assert.Same(Resource.NONE, tile.Resource);
		Assert.True(tile.overlays.GetImprovements().All(i => i != null));
		Assert.Contains(tile.overlays.GetImprovements(), i => i.key == Tile.TileOverlays.ROAD);
	}

	[Fact]
	public void UnknownTerrainFailsWithItsName() {
		C7GameData.GameData gameData = NewGame(fixture.saveGame);
		SaveTile saveTile = new() { X = 0, Y = 0, baseTerrain = "noSuchTerrain" };

		KeyNotFoundException e = Assert.Throws<KeyNotFoundException>(
			() => saveTile.ToTile(gameData.terrainTypes, gameData.Resources, gameData.terrainImprovements));
		Assert.Contains("noSuchTerrain", e.Message);
	}

	[Fact]
	public void UnknownUnitTypeFailsWithItsName() {
		SaveGame save = fixture.saveGame;
		save.Units[0].prototype = "No Such Unit";

		KeyNotFoundException e = Assert.Throws<KeyNotFoundException>(() => save.ToGameData(fixture.behaviors));
		Assert.Contains("No Such Unit", e.Message);
	}

	// Item: a game with more civs than starting locations crashed.

	[Fact]
	public void MoreCivsThanTheWorldSizeAllowsAllGetAStart() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldSize worldSize = new() { name = "Tiny", width = 60, height = 60, numberOfCivs = 2, distanceBetweenCivs = 12 };
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = worldSize,
			mapSeed = 77,
		};
		GameSetup setup = new() {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 7).ToList(),
			victoryConditions = new VictoryConditions(),
		};

		setup.Populate(save);

		// The shared world size is left as it was.
		Assert.Equal(2, worldSize.numberOfCivs);
		// No opponent is left out.
		List<SavePlayer> civs = save.Players.Where(p => !p.isBarbarian).ToList();
		Assert.Equal(8, civs.Count);
		Assert.All(civs, p => Assert.Contains(save.Units, u => u.owner == p.id));
		Assert.Contains(civs, p => p.human);
	}

	// Item: starts were allowed 3 tiles apart, opponents were left out, and
	// a map without room for everyone was used anyway. Now a map without a
	// start for every civ the world size's distance apart is generated
	// again, and each one turned down is saved to look at.

	private static GameSetup SetupFor(SaveGame save, WorldSize worldSize, int opponents, int seed, string invalidMaps) {
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			worldSize = worldSize,
			mapSeed = seed,
		};
		return new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, opponents).ToList(),
			victoryConditions = new VictoryConditions(),
			invalidMapsDirectory = invalidMaps,
		};
	}

	[Fact]
	public void StartsOnAContinentAreTheDistanceBetweenCivsApart() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldSize worldSize = new() { name = "Test", width = 80, height = 80, numberOfCivs = 6, distanceBetweenCivs = 14 };
		SetupFor(save, worldSize, 5, 21, null).Populate(save);

		List<SaveTile> starts = save.Map.startingLocations;
		Assert.Equal(6, save.Players.Count(p => !p.isBarbarian));
		C7GameData.GameData gameData = NewGame(save);
		foreach (SaveTile a in starts) {
			foreach (SaveTile b in starts.Where(b => b != a)) {
				Tile ta = gameData.map.tileAt(a.X, a.Y);
				Tile tb = gameData.map.tileAt(b.X, b.Y);
				if (ta.continent != tb.continent) {
					continue;
				}
				int distance = Math.Abs(gameData.map.CalculateXDelta(a.X, b.X)) + Math.Abs(gameData.map.CalculateYDelta(a.Y, b.Y));
				Assert.True(distance >= 14, $"starts at {a.X},{a.Y} and {b.X},{b.Y} are {distance} apart");
			}
		}
	}

	[Fact]
	public void ImpossibleSettingsFailClearlyAndSaveTheMapsTurnedDown() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		// Eight civs can't start 60 apart on a 30 by 30 map.
		WorldSize worldSize = new() { name = "Test", width = 30, height = 30, numberOfCivs = 8, distanceBetweenCivs = 60 };
		string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"c7-invalid-maps-{Guid.NewGuid():N}");
		try {
			InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => SetupFor(save, worldSize, 7, 3, folder).Populate(save));
			Assert.Contains("start far enough apart", e.Message);
			Assert.Empty(save.Players);

			string[] maps = System.IO.Directory.GetFiles(folder, "*.json");
			Assert.Equal(GameSetup.MaxMapAttempts, maps.Length);
			Assert.Equal(GameSetup.MaxMapAttempts, System.IO.Directory.GetFiles(folder, "*.txt").Length);
			string reason = System.IO.File.ReadAllText(System.IO.Path.ChangeExtension(maps[0], ".txt"));
			Assert.Contains("starting locations fit", reason);

			// Each can be loaded as a game, with every civ somewhere.
			SaveGame invalid = SaveGame.FromJSON(System.IO.File.ReadAllBytes(maps[0]));
			Assert.NotEmpty(invalid.Map.tiles);
			Assert.Equal(8, invalid.Players.Count(p => !invalid.Civilizations.Find(c => c.name == p.civilization).isBarbarian));
			NewGame(invalid);
		} finally {
			if (System.IO.Directory.Exists(folder)) {
				System.IO.Directory.Delete(folder, recursive: true);
			}
		}
	}

	[Fact]
	public void AMapWithoutRoomIsGeneratedAgainFromAnotherSeed() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldSize worldSize = new() { name = "Test", width = 60, height = 60, numberOfCivs = 6, distanceBetweenCivs = 22 };
		// Find a seed whose map has no room, and check the game is set up on
		// the next seed that has.
		int seed = Enumerable.Range(1, 40).First(s => {
			MapGenerator.GenerateMap(new WorldCharacteristics(save) {
				landform = WorldCharacteristics.Landform.Pangaea,
				oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
				worldSize = worldSize,
				mapSeed = s,
			}, out string problem);
			return problem != null;
		});
		GameSetup setup = SetupFor(save, worldSize, 5, seed, null);
		setup.Populate(save);

		Assert.NotEqual(seed, save.Seed);
		Assert.Equal(6, save.Players.Count(p => !p.isBarbarian));
		Assert.Equal(6, save.Map.startingLocations.Count);
	}

	[Fact]
	public void OddWorldSizeIsNotChangedByMapGeneration() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldSize worldSize = new() { width = 41, height = 41, numberOfCivs = 2, distanceBetweenCivs = 8 };
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			worldSize = worldSize,
			mapSeed = 5,
		};

		GameMap map = MapGenerator.GenerateMap(wc);

		Assert.Equal(41, worldSize.width);
		Assert.Equal(40, wc.worldSize.width);
		Assert.NotEmpty(map.tiles);
	}

	// Item: new games started with hardcoded gold and era.

	[Fact]
	public void NewGameStartingGoldAndEraComeFromTheRules() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		save.Rules.StartingTreasury = 37;
		save.Rules.FirstEraCivilopediaName = "ERAS_Test_Era";
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			worldSize = new WorldSize { width = 60, height = 60, numberOfCivs = 2, distanceBetweenCivs = 10 },
			mapSeed = 11,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = [new SelectedOpponent { isRandom = true }],
			victoryConditions = new VictoryConditions(),
		}.Populate(save);

		Assert.All(save.Players.Where(p => !p.isBarbarian), p => {
			Assert.Equal(37, p.gold);
			Assert.Equal("ERAS_Test_Era", p.eraCivilopediaName);
		});
	}

	// Item: a turn limit of 0 ended the game at once.

	[Fact]
	public void NoTurnLimitNeverEndsTheGame() {
		TimeLimitVictory victory = new(0);
		Assert.False(victory.HasVictory(new VictoryStatus { CurrentTurn = 1000 }));
		Assert.True(new TimeLimitVictory(10).HasVictory(new VictoryStatus { CurrentTurn = 10 }));
	}
}

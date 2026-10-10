using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.GameData;

// Plays a generated game with the AI playing every civ, and hashes the whole
// game state after each turn. Used to check that a change meant only to make
// the engine faster leaves what happens in the game exactly as it was: run it
// before and after the change and compare the hashes.
//
// It does nothing unless C7_DETERMINISM_OUT names a file to write the
// per-turn hashes to. The size of the game comes from C7_DETERMINISM_SIZE
// (map width and height, default 80), C7_DETERMINISM_CIVS (default 8),
// C7_DETERMINISM_TURNS (default 80) and C7_DETERMINISM_SEED (default 1234).
//   C7_DETERMINISM_OUT=before.txt dotnet test EngineTests --filter "FullyQualifiedName~AiGameDeterminismTest"
// and again with after.txt, then diff the two files. The file also gives the
// time each turn took.
public class AiGameDeterminismTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;

	public AiGameDeterminismTest(SaveGameFixture fixture, ITestOutputHelper output) {
		this.fixture = fixture;
		this.output = output;
		// Engine message queues are static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	private static int Setting(string name, int fallback) {
		string value = Environment.GetEnvironmentVariable(name);
		return int.TryParse(value, out int n) ? n : fallback;
	}

	private static SaveGame NewSave(int size, int civs, int seed) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Continents,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() {
				width = size,
				height = size,
				numberOfCivs = civs,
				distanceBetweenCivs = Math.Max(6, size / 8),
				techRate = 240,
				optimalNumberOfCities = 20,
			},
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, civs - 1).ToList(),
			victoryConditions = new VictoryConditions(),
		}.Populate(save);
		return save;
	}

	private static string Hash(C7GameData.GameData gameData) {
		return Convert.ToHexString(SHA256.HashData(SaveGame.FromGameData(gameData).ToCompactJSON()))[..16];
	}

	[Fact]
	public async Task PlayAiGame() {
		string outPath = Environment.GetEnvironmentVariable("C7_DETERMINISM_OUT");
		if (string.IsNullOrEmpty(outPath)) {
			return;
		}
		int size = Setting("C7_DETERMINISM_SIZE", 80);
		int civs = Setting("C7_DETERMINISM_CIVS", 8);
		int turns = Setting("C7_DETERMINISM_TURNS", 80);
		int seed = Setting("C7_DETERMINISM_SEED", 1234);

		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		// C7_DETERMINISM_FRAME_MS spreads the AIs' turns over frames, as the
		// game does, which mustn't change what happens.
		EngineStorage.frameBudget = TimeSpan.FromMilliseconds(Setting("C7_DETERMINISM_FRAME_MS", 0));

		await CreateGame.createGame(NewSave(size, civs, seed), (_) => fixture.behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;

		// As the Watch AI Game mode does: the AI plays every civ, and the
		// game hands back to the observed player at the end of each turn.
		gameData.observerMode = true;
		foreach (Player player in gameData.players) {
			player.isHuman = false;
		}

		List<string> lines = [$"size={size} civs={civs} turns={turns} seed={seed}"];
		Stopwatch total = Stopwatch.StartNew();
		try {
			TurnHandling.OnBeginTurn();
			TurnHandling.InitTurnData();
			// Not awaited: the AIs may wait for the next frame, which only
			// handling the engine's messages gives them, as the game does.
			Task firstTurn = TurnHandling.AdvanceTurn();
			EngineWaits.WaitForStartTurnMessage(_ => { });
			await firstTurn;
			for (int turn = 0; turn < turns && !gameData.gameOver; ++turn) {
				Stopwatch watch = Stopwatch.StartNew();
				new MsgEndTurn { turn = gameData.turn }.send();
				EngineWaits.WaitForStartTurnMessage(_ => { });
				long ms = watch.ElapsedMilliseconds;
				string line = $"turn {gameData.turn} units={gameData.mapUnits.Count} cities={gameData.cities.Count} hash={Hash(gameData)}";
				lines.Add(line);
				output.WriteLine($"{line} ms={ms}");
				File.AppendAllText(outPath + ".times", $"turn {gameData.turn} ms={ms}\n");
			}
		} finally {
			lines.Add($"total ms={total.ElapsedMilliseconds}");
			File.WriteAllLines(outPath, lines);
			gameData.observerMode = false;
			EngineStorage.frameBudget = TimeSpan.Zero;
		}
	}
}

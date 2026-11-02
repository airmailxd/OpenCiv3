using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using C7Engine;
using C7Engine.Network;
using C7Engine.PlayLog;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// A recorded game's own folder and log (PlayLogFolders, and PlayRecorder's
// UseLogFile): the name made fit for a folder, a folder of its own, the game
// remembering them in its saves, and a loaded game carrying on its log.
public class PlayLogFoldersTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;

	public PlayLogFoldersTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public void Dispose() {
		PlayRecorder.Close();
		PlayRecorder.enabled = false;
		PlayRecorder.waitForLogFile = false;
		PlayRecorder.directory = null;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	[Theory]
	[InlineData("Rome 2026-11-02", "Rome 2026-11-02")]
	[InlineData("  Rome: the <best> / game?  ", "Rome the best game")]
	[InlineData("a\\b|c*d\"e", "a b c d e")]
	[InlineData("trailing dots...", "trailing dots")]
	[InlineData("tab\there", "tab here")]
	[InlineData("CON", "_CON")]
	[InlineData("aux.txt", "_aux.txt")]
	[InlineData("", "Game")]
	[InlineData("???", "Game")]
	[InlineData("   ", "Game")]
	public void NamesAreMadeFitForAFolder(string name, string expected) {
		Assert.Equal(expected, PlayLogFolders.Sanitize(name));
	}

	[Fact]
	public void LongNamesAreCut() {
		string name = PlayLogFolders.Sanitize(new string('x', 200));
		Assert.Equal(PlayLogFolders.MaxNameLength, name.Length);
		Assert.Equal("Game", PlayLogFolders.Sanitize(null));
	}

	[Fact]
	public void TheDefaultNameIsTheCivilizationAndTheDate() {
		Player human = gameData.players.First(p => p.isHuman);
		Assert.Equal($"{human.civilization.name} 2026-11-02", PlayLogFolders.DefaultName(gameData, new DateTime(2026, 11, 2)));
	}

	[Fact]
	public void AFolderAlreadyThereGetsANumber() {
		using TempDirectory saves = new("play-log-folders");
		Assert.Equal(Path.Combine(saves.Path, "Rome"), PlayLogFolders.UniqueFolder(saves.Path, "Rome"));
		Directory.CreateDirectory(Path.Combine(saves.Path, "Rome"));
		Assert.Equal(Path.Combine(saves.Path, "Rome (2)"), PlayLogFolders.UniqueFolder(saves.Path, "Rome"));
		Directory.CreateDirectory(Path.Combine(saves.Path, "Rome (2)"));
		Assert.Equal(Path.Combine(saves.Path, "Rome (3)"), PlayLogFolders.UniqueFolder(saves.Path, "Rome"));
	}

	[Fact]
	public void StartingMakesTheFolderAndTheGameRemembersIt() {
		using TempDirectory saves = new("play-log-folders");
		Directory.CreateDirectory(Path.Combine(saves.Path, "My game"));
		PlayLogInfo info = PlayLogFolders.Start(gameData, saves.Path, "My: game");
		// "My: game" is "My game", which is there already.
		Assert.Equal("My game (2)", info.name);
		Assert.Equal(Path.Combine(saves.Path, "My game (2)"), info.folder);
		Assert.Equal("My game (2).jsonl", info.logFile);
		Assert.True(Directory.Exists(info.folder));
		Assert.Same(info, gameData.playLog);
		gameData.turn = 37;
		Assert.Equal("My game (2) Turn 37", PlayLogFolders.SaveName(gameData));
	}

	[Fact]
	public void TheSaveCarriesTheGamesNameAndFolder() {
		gameData.playLog = new PlayLogInfo { name = "Rome", folder = @"C:\Saves\Rome", logFile = "Rome.jsonl" };
		string json = Encoding.UTF8.GetString(SaveGame.FromGameData(gameData).ToCompactJSON());

		C7GameData.GameData loaded = SaveGame.LoadFromJSON(json).ToGameData(fixture.behaviors);

		Assert.Equal("Rome", loaded.playLog.name);
		Assert.Equal(@"C:\Saves\Rome", loaded.playLog.folder);
		Assert.Equal("Rome.jsonl", loaded.playLog.logFile);
		// The save has its own copy, not the game's.
		Assert.NotSame(gameData.playLog, loaded.playLog);
	}

	[Fact]
	public void LanSnapshotsLeaveItOut() {
		gameData.playLog = new PlayLogInfo { name = "Rome", folder = @"C:\Saves\Rome", logFile = "Rome.jsonl" };
		Assert.Null(LanProtocol.SnapshotForPeers(gameData).PlayLog);
		// The game itself still has it.
		Assert.NotNull(gameData.playLog);
	}

	[Fact]
	public void OldSavesLoadWithoutIt() {
		Assert.Null(gameData.playLog);
		string json = Encoding.UTF8.GetString(SaveGame.FromGameData(gameData).ToCompactJSON());
		// A game that isn't recorded saves as before.
		Assert.DoesNotContain("playLog", json);

		C7GameData.GameData loaded = SaveGame.LoadFromJSON(json).ToGameData(fixture.behaviors);
		Assert.Null(loaded.playLog);
	}

	[Fact]
	public void TheLogsLastTurnSaysWhetherASaveIsOlder() {
		using TempDirectory folder = new("play-log-folders");
		gameData.playLog = new PlayLogInfo { name = "Rome", folder = folder.Path, logFile = "Rome.jsonl" };
		string log = PlayLogFolders.LogPath(gameData.playLog);

		// No log yet.
		Assert.Null(PlayLogFolders.LastLoggedTurn(log));
		Assert.False(PlayLogFolders.LogIsAhead(gameData));

		File.WriteAllText(log, "{\"type\":\"header\",\"turn\":0}\n{\"turn\":4,\"civ\":\"Rome\"}\n{\"type\":\"action\",\"turn\":10}\n{\"type\":\"act");
		Assert.Equal(10, PlayLogFolders.LastLoggedTurn(log));

		gameData.turn = 5;
		Assert.True(PlayLogFolders.LogIsAhead(gameData));
		gameData.turn = 10;
		Assert.False(PlayLogFolders.LogIsAhead(gameData));
		gameData.turn = 11;
		Assert.False(PlayLogFolders.LogIsAhead(gameData));
	}

	[Fact]
	public void TheGameIsFoundWhereItsSaveIs() {
		using TempDirectory saves = new("play-log-folders");
		string moved = Path.Combine(saves.Path, "Moved");
		Directory.CreateDirectory(moved);
		File.WriteAllText(Path.Combine(moved, "Rome.jsonl"), "{\"turn\":3}\n");
		string gone = Path.Combine(saves.Path, "Gone");

		// The log is beside the save loaded: that's the game's folder now.
		gameData.playLog = new PlayLogInfo { name = "Rome", folder = gone, logFile = "Rome.jsonl" };
		Assert.Equal(moved, PlayLogFolders.Locate(gameData, Path.Combine(moved, "Rome Turn 3.json")));
		Assert.Equal(moved, gameData.playLog.folder);

		// Loaded from elsewhere: the folder the game remembers.
		Assert.Equal(moved, PlayLogFolders.Locate(gameData, Path.Combine(saves.Path, "copy.json")));

		// The folder remembered is gone and the save has no log beside it:
		// a new log, beside the save.
		gameData.playLog.folder = gone;
		Assert.Equal(saves.Path, PlayLogFolders.Locate(gameData, Path.Combine(saves.Path, "copy.json")));
	}

	private static List<JsonElement> Rows(string path) =>
		File.ReadAllLines(path).Select(l => JsonDocument.Parse(l).RootElement).ToList();

	private static string Type(JsonElement r) => r.TryGetProperty("type", out JsonElement t) ? t.GetString() : "state";

	[Fact]
	public void AGameCarriesOnItsLogWithASessionRow() {
		Assert.Null(Environment.GetEnvironmentVariable("C7_PLAY_LOG_DIR"));
		using TempDirectory saves = new("play-log-folders");
		Player human = gameData.players.First(p => p.isHuman);
		PlayRecorder.enabled = true;
		PlayRecorder.waitForLogFile = true;
		PlayRecorder.directory = Path.Combine(saves.Path, "play-logs");

		// A new game: its rows wait until it's named.
		gameData.turn = 1;
		PlayRecorder.OnGameCreated(gameData);
		Assert.True(PlayRecorder.WaitingForLogFile);
		PlayRecorder.HumanTurnStarted(human, gameData);
		Assert.Null(PlayRecorder.CurrentPath);
		PlayLogInfo info = PlayLogFolders.Start(gameData, saves.Path, "Rome");
		string log = PlayLogFolders.LogPath(info);
		PlayRecorder.UseLogFile(log);
		Assert.False(PlayRecorder.WaitingForLogFile);
		Assert.Equal(log, PlayRecorder.CurrentPath);
		gameData.turn = 2;
		PlayRecorder.HumanTurnStarted(human, gameData);
		PlayRecorder.Close();
		// Nothing went to the old play-logs folder.
		Assert.False(Directory.Exists(PlayRecorder.directory));

		List<JsonElement> first = Rows(log);
		Assert.Equal(["header", "state", "state"], first.Select(Type).ToList());
		Assert.Equal("Rome", first[0].GetProperty("name").GetString());
		Assert.Equal(1, first[1].GetProperty("turn").GetInt32());
		Assert.Equal(2, first[2].GetProperty("turn").GetInt32());

		// Loaded again (from its save, at turn 2): the same file carries on.
		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(loaded);
		PlayRecorder.OnGameCreated(loaded);
		PlayLogFolders.Locate(loaded, Path.Combine(info.folder, "Rome Turn 2.json"));
		Assert.False(PlayLogFolders.LogIsAhead(loaded));
		loaded.turn = 3;
		PlayRecorder.HumanTurnStarted(loaded.players.First(p => p.isHuman), loaded);
		PlayRecorder.UseLogFile(PlayLogFolders.LogPath(loaded.playLog), "Rome Turn 2.json");
		PlayRecorder.Close();

		Assert.Single(Directory.GetFiles(info.folder, "*.jsonl"));
		List<JsonElement> rows = Rows(log);
		Assert.Equal(["header", "state", "state", "session", "state"], rows.Select(Type).ToList());
		Assert.Equal("Rome Turn 2.json", rows[3].GetProperty("loaded").GetString());
		Assert.Equal(3, rows[4].GetProperty("turn").GetInt32());
	}

	[Fact]
	public void AGameNotToBeLoggedWritesNothing() {
		using TempDirectory saves = new("play-log-folders");
		PlayRecorder.enabled = true;
		PlayRecorder.waitForLogFile = true;
		PlayRecorder.directory = saves.Path;
		PlayRecorder.OnGameCreated(gameData);
		PlayRecorder.HumanTurnStarted(gameData.players.First(p => p.isHuman), gameData);
		// "Don't log this session".
		PlayRecorder.Close();
		Assert.Empty(Directory.GetFileSystemEntries(saves.Path));
	}

	[Fact]
	public void WithTheSettingOffNothingWaits() {
		PlayRecorder.enabled = false;
		PlayRecorder.waitForLogFile = true;
		PlayRecorder.OnGameCreated(gameData);
		Assert.False(PlayRecorder.WaitingForLogFile);
		Assert.False(PlayRecorder.Active);
	}
}

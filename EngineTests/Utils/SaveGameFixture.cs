using System;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;

namespace EngineTests.Utils;

// Generated test games, shared by every test class.
//
// Generating a map is slow, so each kind of game is generated once per test
// run. Building a game from a SaveGame shares much of the save's state (rules,
// history, civilizations, relationships, ...) with the game, and playing the
// game changes it, so every test gets its own copy: saveGame and
// standaloneSaveGame return a fresh clone each time they are read.
public class SaveGameFixture : IDisposable {
	internal BehaviorEngine behaviors;

	const int TestSeed = 123456;

	private static readonly Lazy<SaveGame> basicSave = new(() => LoadSave(new GameMode.Config("civ3")));
	private static readonly Lazy<SaveGame> standaloneSave = new(() => LoadSave(new GameMode.Config("civ3", ["standalone"])));
	private static readonly Lazy<SaveGame> twoHumanSave = new(() => LoadSave(new GameMode.Config("civ3"), humanPlayers: 2));
	private static readonly Lazy<SaveGame> threeHumanSave = new(() => LoadSave(new GameMode.Config("civ3"), humanPlayers: 3));
	private static readonly Lazy<BehaviorEngine> sharedBehaviors = new(() => LoadGameMode(new GameMode.Config("civ3")).behaviors);

	// A fresh copy of the generated single-human game. Each read returns a
	// new copy, so read it once if a test needs the same SaveGame twice.
	internal SaveGame saveGame => basicSave.Value.Clone();

	// A fresh copy of the generated single-human game in standalone mode.
	internal SaveGame standaloneSaveGame => standaloneSave.Value.Clone();

	public SaveGameFixture() {
		// Standalone and basic modes should use the same set of behaviors
		behaviors = LoadGameMode(new GameMode.Config("civ3")).behaviors;
	}

	// A fresh copy of a generated game with two human players.
	internal static SaveGame TwoHumanSave() {
		return twoHumanSave.Value.Clone();
	}

	// A fresh copy of a generated game with three human players.
	internal static SaveGame ThreeHumanSave() {
		return threeHumanSave.Value.Clone();
	}

	internal static GameMode LoadGameMode(GameMode.Config gameModeConfig) {
		return GameMode.Load(PathUtils.GameModesDir, gameModeConfig);
	}

	// Generates a new game. This is slow: prefer saveGame, standaloneSaveGame
	// or TwoHumanSave(), which generate each game once per test run.
	internal static SaveGame LoadSave(GameMode.Config gameModeConfig, int humanPlayers = 1) {
		SaveGame save = LoadGameMode(gameModeConfig).GetSave();

		WorldSize worldSize = new() {
			width = 100,
			height = 100,
			numberOfCivs = 8,
			distanceBetweenCivs = 12,
			techRate = 240,
			optimalNumberOfCities = 20,
		};

		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = worldSize,
			mapSeed = TestSeed,
		};

		Civilization[] humanCivs = save.Civilizations.Where(c => !c.isBarbarian).Take(humanPlayers).ToArray();
		GameSetup gameSetup = new() {
			playerCivilization = humanCivs[0],
			playerName = humanPlayers > 1 ? "Player 1" : null,
			hotseatPlayers = humanCivs.Skip(1).Select((civ, i) => new HotseatPlayer { civilization = civ, name = $"Player {i + 2}" }).ToList(),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent() { isRandom = true }, worldSize.numberOfCivs - humanPlayers).ToList(),
			victoryConditions = new VictoryConditions()
		};

		gameSetup.Populate(save);

		return save;
	}

	/// <summary>
	/// Given a save game, create test-ready game data. The game data shares
	/// state with the save, so pass a save the test owns (such as a fresh
	/// saveGame).
	/// </summary>
	public static C7GameData.GameData HydrateSaveGame(SaveGame game) {
		return game.ToGameData(sharedBehaviors.Value);
	}

	public void Dispose() {
		return;
	}
}

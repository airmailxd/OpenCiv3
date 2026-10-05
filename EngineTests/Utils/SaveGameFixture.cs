using System;
using System.Linq;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;

namespace EngineTests.Utils;

public class SaveGameFixture : IDisposable {
	internal SaveGame saveGame;
	internal SaveGame standaloneSaveGame;
	internal BehaviorEngine behaviors;

	const int TestSeed = 123456;

	public SaveGameFixture() {
		GameMode.Config basic = new("civ3");
		GameMode.Config standalone = new("civ3", ["standalone"]);

		saveGame = LoadSave(basic);
		standaloneSaveGame = LoadSave(standalone);

		// Standalone and basic modes should use the same set of behaviors
		behaviors = LoadGameMode(basic).behaviors;
	}

	private static GameMode LoadGameMode(GameMode.Config gameModeConfig) {
		return GameMode.Load(PathUtils.GameModesDir, gameModeConfig);
	}

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
	/// Given a save game, create test-ready game data.
	/// </summary>
	public static C7GameData.GameData HydrateSaveGame(SaveGame game) {
		var fixture = new SaveGameFixture();
		C7GameData.GameData gd = game.ToGameData(fixture.behaviors);
		return gd;
	}

	public void Dispose() {
		return;
	}
}

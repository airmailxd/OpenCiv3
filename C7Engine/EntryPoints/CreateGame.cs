using System;
using System.Linq;
using System.Threading.Tasks;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;

namespace C7Engine;

public class GameParams {
	public string DefaultBicPath;
	public Func<GameMode.Config, BehaviorEngine> GameModeLoader;

	public Func<string, string> GetPediaIconsPath = s => s;

	public GameParams(string DefaultBicPath) {
		this.DefaultBicPath = DefaultBicPath;
	}
}

public class CreateGame {
	/**
		* For now, I'm making the methods that the C7 client can call be static.
		* We may want a different solution in the end, but this lets us start prototyping
		* quickly.  By keeping all the client-callable APIs in the EntryPoints folder,
		* hopefully it won't be too much of a goose hunt to refactor it later if we decide to do so.
		**/
	public static async Task<Player> createGame(string loadFilePath, GameParams options) {
		SaveGame save = SaveManager.LoadSave(loadFilePath, options.DefaultBicPath, options.GetPediaIconsPath);

		return await createGame(save, options.GameModeLoader);
	}

	public static async Task<Player> createGame(SaveGame save, Func<GameMode.Config, BehaviorEngine> gameModeLoader) {
		BehaviorEngine behaviors = gameModeLoader(save.GameModeConfig);

		EngineStorage.ResetForNewGame();
		GameData gameData = save.ToGameData(behaviors);

		EngineStorage.gameData = gameData;
		EngineStorage.gameData.onGameCreation();

		Player humanPlayer = TurnHandling.FirstHumanToPlay(gameData)
			?? throw new Exception($"The provided save does not contain a human player");

		EngineStorage.uiControllerID = humanPlayer.id;
		EngineStorage.activePlayerID = humanPlayer.id;

		return humanPlayer;
	}

	// Replaces the game with a LAN host's snapshot of it, keeping who this
	// machine plays and whose turn it is.
	public static GameData ReplaceWithSnapshot(SaveGame save, BehaviorEngine behaviors) {
		GameData gameData = save.ToGameData(behaviors);
		EngineStorage.gameData = gameData;
		gameData.onGameCreation();
		return gameData;
	}
}

using Godot;
using System;
using C7GameData;
using C7Engine;
using C7Engine.Lua;
using Serilog;

// Quick Start: a new game set up as the player last set one up, on a new
// map (see NewGameChoices.QuickStartGame).
public partial class QuickStartSetup : Node {
	private static ILogger log = LogManager.ForContext<QuickStartSetup>();

	public static void Init(GlobalSingleton global) {
		log.Information("Setting up a QuickStart game");

		global.ResetLoadGameFields();

		var game = GameMode.Load(GamePaths.GameModesDir, GamePaths.GameMode);
		var save = game.GetSave();

		Random random = new();
		GameSetup gameSetup = NewGameChoices.Load().QuickStartGame(save, random.Next(), random, InvalidMaps.FolderIfEnabled);
		global.WorldCharacteristics = gameSetup.worldCharacteristics;
		global.SaveGame = save;

		WorldCharacteristics world = gameSetup.worldCharacteristics;
		log.Information($"Quick Start: {world.worldSize.name} {world.landform} {world.oceanCoverage} {world.climate} {world.temperature} {world.age}, "
			+ $"barbarians {world.barbarianActivity}, seed {world.mapSeed}, {gameSetup.playerCivilization.name} on {gameSetup.difficulty.Name}, "
			+ $"{gameSetup.opponents.Count} rivals, {gameSetup.hotseatPlayers.Count} more humans");

		gameSetup.Populate(save);
	}
}

using System;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class HotseatTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public HotseatTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine message queues are static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	// Processes engine messages until the engine hands the UI to a player,
	// ignoring informational messages along the way.
	private static void WaitForStartTurnMessage() {
		for (int i = 0; i < 100_000; ++i) {
			EngineStorage.ProcessNextMessageToEngine();
			while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
				if (msg is MsgStartTurn) {
					return;
				}
				Assert.IsNotType<MsgShowTradeOffer>(msg);
			}
		}
		throw new Exception("Engine never started a player turn");
	}

	// Map generation is slow, so share one two-human save across the tests.
	private static readonly Lazy<SaveGame> hotseatSave = new(() => SaveGameFixture.LoadSave(new GameMode.Config("civ3"), humanPlayers: 2));

	private async Task<C7GameData.GameData> CreateHotseatGame() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();

		await CreateGame.createGame(hotseatSave.Value, (_) => fixture.behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;

		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		return gameData;
	}

	[Fact]
	public async Task EachHumanControlsTheUiInTurn() {
		C7GameData.GameData gameData = await CreateHotseatGame();
		Player[] humans = gameData.players.Where(p => p.isHuman).ToArray();
		Assert.Equal(2, humans.Length);
		Assert.True(TurnHandling.IsHotseat(gameData));

		WaitForStartTurnMessage();
		Assert.Equal(humans[0].id, EngineStorage.uiControllerID);
		Assert.Equal(0, gameData.turn);

		// The first human ends their turn; the second human gets the UI
		// during the same game turn.
		new MsgEndTurn().send();
		WaitForStartTurnMessage();
		Assert.Equal(humans[1].id, EngineStorage.uiControllerID);
		Assert.Equal(0, gameData.turn);
		Assert.True(humans[0].hasPlayedThisTurn);
		Assert.False(humans[1].hasPlayedThisTurn);

		// The second human ends their turn; the AIs play, the turn advances
		// and the first human is back in control.
		new MsgEndTurn().send();
		WaitForStartTurnMessage();
		Assert.Equal(humans[0].id, EngineStorage.uiControllerID);
		Assert.Equal(1, gameData.turn);
	}

	[Fact]
	public async Task LoadingMidRotationResumesWithNextHuman() {
		C7GameData.GameData gameData = await CreateHotseatGame();
		Player[] humans = gameData.players.Where(p => p.isHuman).ToArray();

		WaitForStartTurnMessage();
		new MsgEndTurn().send();
		WaitForStartTurnMessage();
		Assert.Equal(humans[1].id, EngineStorage.uiControllerID);

		// Save while the second human is playing, then load the save.
		SaveGame save = SaveGame.FromGameData(gameData);
		Player resumed = await CreateGame.createGame(save, (_) => fixture.behaviors);

		Assert.Equal(humans[1].id, resumed.id);
		Assert.Equal(humans[1].id, EngineStorage.uiControllerID);
	}

	[Fact]
	public void GameSetupCreatesEachHumanPlayer() {
		SaveGame save = hotseatSave.Value;
		Assert.Equal(2, save.Players.Count(p => p.human));
		// Humans + AI opponents fill every starting location, plus the barbarians.
		Assert.Equal(save.Map.startingLocations.Count + 1, save.Players.Count);
		Assert.Equal(save.Players.Count, save.Players.Select(p => p.civilization).Distinct().Count());
	}

	[Fact]
	public async Task SingleHumanGameIsNotHotseat() {
		await CreateGame.createGame(fixture.saveGame, (_) => fixture.behaviors);
		Assert.False(TurnHandling.IsHotseat(EngineStorage.gameData));
	}
}

using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Watching the AIs play (observer mode), the game goes on when the civ being
// followed is destroyed: the view follows another civ, rather than the game
// ending for want of a human player.
public class WatchedCivDestroyedTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public WatchedCivDestroyedTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine message queues are static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	[Fact]
	public async Task TheViewFollowsAnotherCiv() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await CreateGame.createGame(SaveGameFixture.LoadSave(new C7Engine.Lua.GameMode.Config("civ3")), (_) => fixture.behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;
		gameData.observerMode = true;
		try {
			foreach (Player p in gameData.players) {
				p.isHuman = false;
			}
			Player followed = gameData.GetPlayer(EngineStorage.uiControllerID);
			Assert.NotNull(followed);
			followed.defeated = true;

			Task firstTurn = TurnHandling.AdvanceTurn();
			MsgStartTurn started = EngineWaits.WaitForStartTurnMessage(msg => Assert.IsNotType<MsgNoHumansRemain>(msg));
			await firstTurn;

			Assert.False(gameData.gameOver);
			Assert.NotEqual(followed.id, EngineStorage.uiControllerID);
			Assert.Equal(EngineStorage.uiControllerID, started.player.id);
			Assert.False(started.player.defeated);
			Assert.False(started.player.isBarbarians);
		} finally {
			gameData.observerMode = false;
		}
	}
}

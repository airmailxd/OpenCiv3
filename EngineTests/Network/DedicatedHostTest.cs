using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.Network;

// A dedicated host, as a game server's worker runs: no player of its own,
// each seat for whoever has its token, viewers who may act only when the
// game waits on them, and turns played at once when everyone is connected
// as they begin, and in turn otherwise.
public class DedicatedHostTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public DedicatedHostTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private const string FirstToken = "first-seat-token";
	private const string SecondToken = "second-seat-token";

	// Loads a new two-human game as a worker does, and hosts it.
	private async Task<(LanHost host, Player[] humans)> HostGame() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		SaveGame save = SaveGameFixture.TwoHumanSave();
		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.InitTurnData();
		Player[] humans = EngineStorage.gameData.players.Where(p => p.isHuman).ToArray();
		LanHost host = LanHost.Dedicated("Friday Game", save, [
			new LanHost.DedicatedSeat(humans[0].id, FirstToken, "alice"),
			new LanHost.DedicatedSeat(humans[1].id, SecondToken, "bob"),
		]);
		host.StartGame();
		EngineStorage.messagesToUI.Clear();
		return (host, humans);
	}

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	private static void PumpUntil(LanHost host, IEnumerable<LanClient> clients, Func<bool> condition) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (true) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			EngineStorage.messagesToUI.Clear();
			foreach (LanClient client in clients) {
				client.Poll();
			}
			if (condition()) {
				return;
			}
			if (pumping.Elapsed > PumpTimeout) {
				throw new TimeoutException("The game never got there");
			}
			Thread.Sleep(5);
		}
	}

	// Runs everything for a while, for checking that something doesn't
	// happen.
	private static void PumpFor(LanHost host, IEnumerable<LanClient> clients, TimeSpan time) {
		Stopwatch pumping = Stopwatch.StartNew();
		PumpUntil(host, clients, () => pumping.Elapsed >= time);
	}

	private static LanClient Join(LanHost host, string token, string name) {
		LanClient client = LanClient.Connect("127.0.0.1", host.Port, name, token);
		client.SnapshotReceived = _ => { };
		client.UiMessageReceived = _ => { };
		PumpUntil(host, [client], () => client.PlayerIDs.Count > 0 || client.RejectedReason != null);
		return client;
	}

	[Fact]
	public async Task OnlyASeatsTokenGetsIn() {
		(LanHost host, Player[] humans) = await HostGame();
		using (host) {
			Assert.True(host.IsDedicated);
			Assert.Null(host.HostPlayerID);

			using LanClient stranger = LanClient.Connect("127.0.0.1", host.Port, "mallory");
			PumpUntil(host, [stranger], () => stranger.RejectedReason != null);
			Assert.Equal(LanHost.DedicatedJoinReason, stranger.RejectedReason);

			using LanClient guesser = LanClient.Connect("127.0.0.1", host.Port, "mallory", "not-a-token");
			PumpUntil(host, [guesser], () => guesser.RejectedReason != null);
			Assert.Equal(LanHost.DedicatedJoinReason, guesser.RejectedReason);

			// Watching needs letting in first, which nobody without a token is.
			Assert.False(host.AllowSpectators);

			using LanClient bob = Join(host, SecondToken, "bob");
			Assert.Null(bob.RejectedReason);
			Assert.Equal([humans[1].id], bob.PlayerIDs);
			Assert.Equal([humans[1].id], host.ConnectedPlayerIDs);
			// The lobby shows the game's seats, and no host's.
			PumpUntil(host, [bob], () => bob.Lobby != null);
			Assert.Equal([humans[0].id, humans[1].id], bob.Lobby.seats.Select(s => s.playerID));
			Assert.Equal(["alice", "bob"], bob.Lobby.seats.Select(s => s.takenBy));
		}
	}

	[Fact]
	public async Task AViewerMayNotActUntilItsTheirTurnAndThenPlaysWithoutJoiningAgain() {
		(LanHost host, Player[] humans) = await HostGame();
		using (host) {
			C7GameData.GameData gameData = EngineStorage.gameData;
			// A game loaded with nobody connected is played in turn.
			Assert.False(gameData.simultaneousTurns);
			Assert.Equal([humans[0]], TurnHandling.PlayersToMove(gameData));

			using LanClient alice = Join(host, FirstToken, "alice");
			using LanClient bob = Join(host, SecondToken, "bob");
			LanClient[] both = [alice, bob];

			// Bob views: his orders are refused by the host, not just the UI.
			MapUnit bobsUnit = humans[1].units.First(u => !u.isFortified);
			bob.SendCommand(new MsgSetFortification(bobsUnit.id, true));
			bob.SendCommand(new MsgEndTurn { turn = 0 });
			PumpUntil(host, both, () => host.RefusedCommands == 2);
			Assert.False(bobsUnit.isFortified);
			Assert.False(humans[1].hasPlayedThisTurn);

			// Alice plays.
			MapUnit alicesUnit = humans[0].units.First(u => !u.isFortified);
			alice.SendCommand(new MsgSetFortification(alicesUnit.id, true));
			PumpUntil(host, both, () => alicesUnit.isFortified);
			alice.SendCommand(new MsgEndTurn { turn = 0 });
			PumpUntil(host, both, () => TurnHandling.IsPlayersTurn(gameData, humans[1].id));

			// Now Bob's orders go through, on the same connection.
			bob.SendCommand(new MsgSetFortification(bobsUnit.id, true));
			PumpUntil(host, both, () => bobsUnit.isFortified);
			Assert.Equal(2, host.RefusedCommands);

			// With both connected as the next turn begins, it's played at once.
			bob.SendCommand(new MsgEndTurn { turn = 0 });
			PumpUntil(host, both, () => gameData.turn == 1 && !TurnHandling.TurnInProgress);
			Assert.True(gameData.simultaneousTurns);
			Assert.True(host.SimultaneousTurns);
			Assert.Equal(humans, TurnHandling.PlayersToMove(gameData));
		}
	}

	[Fact]
	public async Task SomeoneDroppingFromASimultaneousTurnKeepsItOpenAndTheNextIsInTurn() {
		(LanHost host, Player[] humans) = await HostGame();
		using (host) {
			C7GameData.GameData gameData = EngineStorage.gameData;
			LanClient alice = Join(host, FirstToken, "alice");
			LanClient bob = Join(host, SecondToken, "bob");
			try {
				// Turn 0 is played in turn; turn 1 at once.
				alice.SendCommand(new MsgEndTurn { turn = 0 });
				PumpUntil(host, [alice, bob], () => TurnHandling.IsPlayersTurn(gameData, humans[1].id));
				bob.SendCommand(new MsgEndTurn { turn = 0 });
				PumpUntil(host, [alice, bob], () => gameData.turn == 1 && !TurnHandling.TurnInProgress);
				Assert.True(gameData.simultaneousTurns);

				// Bob drops; Alice finishes; the turn waits for Bob.
				bob.Dispose();
				PumpUntil(host, [alice], () => host.ConnectedPlayerIDs.Count == 1);
				alice.SendCommand(new MsgEndTurn { turn = 1 });
				PumpUntil(host, [alice], () => humans[0].hasPlayedThisTurn);
				PumpFor(host, [alice], TimeSpan.FromMilliseconds(300));
				Assert.Equal(1, gameData.turn);
				Assert.Equal([humans[1]], TurnHandling.PlayersToMove(gameData));

				// Alice leaves too, and Bob comes back to finish his turn.
				alice.Dispose();
				bob = Join(host, SecondToken, "bob");
				Assert.Equal([humans[1].id], host.ConnectedPlayerIDs);
				bob.SendCommand(new MsgEndTurn { turn = 1 });
				PumpUntil(host, [bob], () => gameData.turn == 2 && !TurnHandling.TurnInProgress);

				// Alice wasn't there as turn 2 began, so it's played in turn.
				Assert.False(gameData.simultaneousTurns);
				Assert.Equal([humans[0]], TurnHandling.PlayersToMove(gameData));
			} finally {
				alice.Dispose();
				bob.Dispose();
			}
		}
	}
}

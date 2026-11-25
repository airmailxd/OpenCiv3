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

// A server game waiting on a human who isn't connected: its owner (who set
// it up) may have the computer play them, or play them at their own machine
// as in a hotseat game, until they're back (LanHost.ChooseStandIn).
public class StandInTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public StandInTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private const string OwnerToken = "owner-seat-token";
	private const string OtherToken = "other-seat-token";

	// A new two-human game, hosted as a worker does, set up by the first
	// human, Alice.
	private async Task<(LanHost host, Player alice, Player bob)> HostGame(SaveGame save = null) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		save ??= SaveGameFixture.TwoHumanSave();
		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.InitTurnData();
		Player[] humans = EngineStorage.gameData.players.Where(p => p.isHuman || p.standIn != StandIn.None).ToArray();
		LanHost host = LanHost.Dedicated("Friday Game", save, [
			new LanHost.DedicatedSeat(humans[0].id, OwnerToken, "alice"),
			new LanHost.DedicatedSeat(humans[1].id, OtherToken, "bob"),
		], ownerPlayerID: humans[0].id);
		host.StartGame();
		EngineStorage.messagesToUI.Clear();
		return (host, humans[0], humans[1]);
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

	// Alice, the owner, connects alone and ends turn 0, which then waits on
	// Bob, who isn't connected.
	private static LanClient WaitOnBob(LanHost host, Player alice, Player bob) {
		LanClient owner = Join(host, OwnerToken, "alice");
		PumpUntil(host, [owner], () => owner.CurrentClock() != null);
		Assert.Equal(alice.id, owner.CurrentClock().ownerPlayerID);
		owner.SendCommand(new MsgEndTurn { playerID = alice.id, turn = 0 });
		PumpUntil(host, [owner], () => EngineStorage.activePlayerID == bob.id && !TurnHandling.TurnInProgress);
		Assert.Equal([bob], TurnHandling.PlayersToMove(EngineStorage.gameData));
		Assert.Equal([bob.id], host.AbsentPlayers);
		return owner;
	}

	[Fact]
	public async Task OnlyTheOwnerChoosesAndOnlyForSomeoneAbsent() {
		(LanHost host, Player alice, Player bob) = await HostGame();
		using (host) {
			Assert.Equal(alice.id, host.OwnerPlayerID);
			// Not with the owner away, nor for the owner.
			Assert.False(host.ChooseStandIn(bob.id, StandIn.Computer));

			using LanClient bobsGame = Join(host, OtherToken, "bob");
			Assert.False(host.ChooseStandIn(alice.id, StandIn.Computer));
			// Bob isn't the owner: his choice for Alice is ignored.
			bobsGame.ChooseStandIn(alice.id, StandIn.Computer);
			PumpFor(host, [bobsGame], TimeSpan.FromMilliseconds(300));
			Assert.True(alice.isHuman);
			Assert.Equal(StandIn.None, alice.standIn);

			// Nor for someone connected.
			using LanClient owner = Join(host, OwnerToken, "alice");
			Assert.False(host.ChooseStandIn(bob.id, StandIn.Computer));
			// A message to the engine from a guest isn't the host's.
			owner.SendCommand(new MsgStandIn { playerID = bob.id, standIn = StandIn.Computer });
			PumpFor(host, [owner, bobsGame], TimeSpan.FromMilliseconds(300));
			Assert.True(alice.isHuman && bob.isHuman);
			Assert.Equal(StandIn.None, alice.standIn);
			Assert.Equal(StandIn.None, bob.standIn);
		}
	}

	[Fact]
	public async Task TheComputerPlaysTheAbsentPlayerUntilTheyAreBack() {
		(LanHost host, Player alice, Player bob) = await HostGame();
		using (host) {
			C7GameData.GameData gameData = EngineStorage.gameData;
			LanClient owner = WaitOnBob(host, alice, bob);
			LanClient bobsGame = null;
			try {
				owner.ChooseStandIn(bob.id, StandIn.Computer);
				// The computer plays Bob's turn 0 at once, and turn 1 begins,
				// with Alice the only human to move.
				PumpUntil(host, [owner], () => gameData.turn == 1 && !TurnHandling.TurnInProgress);
				Assert.False(bob.isHuman);
				Assert.Equal(StandIn.Computer, bob.standIn);
				Assert.Equal([alice], TurnHandling.PlayersToMove(gameData));
				Assert.Empty(host.AbsentPlayers);
				PumpUntil(host, [owner], () => owner.CurrentClock()?.computerPlayers?.Contains(bob.id) == true);

				// Saved, he's still a human, whom the computer plays.
				SavePlayer saved = LanProtocol.SnapshotOf(gameData).Players.Find(p => p.id == bob.id);
				Assert.True(saved.human);
				Assert.Equal(StandIn.Computer, saved.standIn);

				// The game doesn't wait for him: Alice's turn ends the round.
				owner.SendCommand(new MsgEndTurn { playerID = alice.id, turn = 1 });
				PumpUntil(host, [owner], () => gameData.turn == 2 && !TurnHandling.TurnInProgress);

				// Bob is back: he's human again, and with everyone here the
				// rest of the turn is played at once.
				bobsGame = Join(host, OtherToken, "bob");
				PumpUntil(host, [owner, bobsGame], () => bob.isHuman && gameData.simultaneousTurns);
				Assert.Equal(StandIn.None, bob.standIn);
				Assert.Contains(bob, TurnHandling.PlayersToMove(gameData));
				PumpUntil(host, [owner, bobsGame], () => owner.CurrentClock()?.computerPlayers?.Count == 0);
				bobsGame.SendCommand(new MsgEndTurn { playerID = bob.id, turn = 2 });
				PumpUntil(host, [owner, bobsGame], () => bob.hasPlayedThisTurn);
				Assert.Equal(2, gameData.turn);
			} finally {
				owner.Dispose();
				bobsGame?.Dispose();
			}
		}
	}

	[Fact]
	public async Task TheOwnerPlaysTheAbsentPlayerAtTheirMachineUntilTheyAreBack() {
		(LanHost host, Player alice, Player bob) = await HostGame();
		using (host) {
			C7GameData.GameData gameData = EngineStorage.gameData;
			LanClient owner = WaitOnBob(host, alice, bob);
			LanClient bobsGame = null;
			try {
				owner.ChooseStandIn(bob.id, StandIn.Hotseat);
				// Alice's machine plays Bob too now, as a hotseat game does.
				PumpUntil(host, [owner], () => owner.PlayerIDs.Count == 2);
				Assert.Equal([alice.id, bob.id], owner.PlayerIDs);
				Assert.True(bob.isHuman);
				PumpUntil(host, [owner], () => bob.standIn == StandIn.Hotseat);
				PumpUntil(host, [owner], () => owner.CurrentClock()?.hotseatPlayers?.Contains(bob.id) == true);

				MapUnit bobsUnit = bob.units.First(u => !u.isFortified);
				owner.SendCommand(new MsgSetFortification(bobsUnit.id, true) { playerID = bob.id });
				PumpUntil(host, [owner], () => bobsUnit.isFortified);
				owner.SendCommand(new MsgEndTurn { playerID = bob.id, turn = 0 });
				PumpUntil(host, [owner], () => gameData.turn == 1 && !TurnHandling.TurnInProgress);
				// The owner plays both, at one machine, so turns are played at
				// once.
				Assert.True(gameData.simultaneousTurns);

				// Bob is back, and his seat is his alone again.
				bobsGame = Join(host, OtherToken, "bob");
				PumpUntil(host, [owner, bobsGame], () => owner.PlayerIDs.Count == 1 && bob.standIn == StandIn.None);
				Assert.Equal([alice.id], owner.PlayerIDs);
				Assert.Equal([bob.id], bobsGame.PlayerIDs);
				int refused = host.RefusedCommands;
				MapUnit anotherUnit = bob.units.First(u => !u.isFortified);
				owner.SendCommand(new MsgSetFortification(anotherUnit.id, true) { playerID = bob.id });
				PumpFor(host, [owner, bobsGame], TimeSpan.FromMilliseconds(300));
				Assert.False(anotherUnit.isFortified);
				bobsGame.SendCommand(new MsgSetFortification(anotherUnit.id, true));
				PumpUntil(host, [owner, bobsGame], () => anotherUnit.isFortified);
				Assert.Equal(refused, host.RefusedCommands);
			} finally {
				owner.Dispose();
				bobsGame?.Dispose();
			}
		}
	}

	// The choice is kept with the game, and a worker hosting it again keeps
	// to it.
	[Fact]
	public async Task TheChoiceIsKeptWithTheGame() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		SavePlayer second = save.Players.Where(p => p.human).ElementAt(1);
		second.standIn = StandIn.Computer;
		(LanHost host, Player alice, Player bob) = await HostGame(save);
		using (host) {
			Assert.False(bob.isHuman);
			Assert.Equal(StandIn.Computer, bob.standIn);
			// Only Alice, who isn't connected, is waited for.
			Assert.Equal([alice.id], host.AbsentPlayers);
			PumpUntil(host, [], () => host.CurrentClock() != null);
			Assert.Equal([bob.id], host.CurrentClock().computerPlayers);
			// Alice alone plays: the game doesn't wait for Bob.
			Assert.Equal([alice], TurnHandling.PlayersToMove(EngineStorage.gameData));
		}
	}
}

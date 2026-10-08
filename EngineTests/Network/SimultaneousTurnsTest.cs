using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.Network;

// LAN games whose humans play their turns at the same time.
public class SimultaneousTurnsTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public SimultaneousTurnsTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	// Creates the game and plays up to the humans' first turn. The messages
	// the UI was sent on the way are left for the test.
	private async Task<C7GameData.GameData> CreateGame(SaveGame save, bool simultaneous) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();

		save.SimultaneousTurns = simultaneous;
		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		return EngineStorage.gameData;
	}

	private static Player[] Humans(C7GameData.GameData gameData) {
		return gameData.players.Where(p => p.isHuman).ToArray();
	}

	private static List<Player> TurnsStarted() {
		List<Player> started = EngineStorage.messagesToUI.OfType<MsgStartTurn>().Select(m => m.player).ToList();
		EngineStorage.messagesToUI.Clear();
		return started;
	}

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	// Lets the engine handle its messages until the condition holds.
	private static void RunEngineUntil(Func<bool> condition) {
		Stopwatch running = Stopwatch.StartNew();
		while (!condition()) {
			if (running.Elapsed > PumpTimeout) {
				throw new TimeoutException("The engine never got there");
			}
			if (EngineStorage.HasPendingMessagesToEngine()) {
				EngineStorage.ProcessNextMessageToEngine();
			} else {
				Thread.Sleep(5);
			}
		}
	}

	private static void ProcessEngineMessages() {
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.ProcessNextMessageToEngine();
		}
	}

	private static MapUnit UnfortifiedUnit(Player player) {
		return player.units.First(u => !u.isFortified);
	}

	[Fact]
	public async Task EveryHumanMovesAtOnce() {
		C7GameData.GameData gameData = await CreateGame(SaveGameFixture.TwoHumanSave(), simultaneous: true);
		Player[] humans = Humans(gameData);

		Assert.Equal(humans, TurnsStarted());
		Assert.Equal(humans, TurnHandling.PlayersToMove(gameData));
		Assert.Equal(humans[0].id, EngineStorage.activePlayerID);

		// Both can order their units in the same turn.
		MapUnit first = UnfortifiedUnit(humans[0]);
		MapUnit second = UnfortifiedUnit(humans[1]);
		new MsgSetFortification(second.id, true) { playerID = humans[1].id }.send();
		new MsgSetFortification(first.id, true) { playerID = humans[0].id }.send();
		ProcessEngineMessages();
		Assert.True(first.isFortified);
		Assert.True(second.isFortified);

		// But not each other's.
		MapUnit another = humans[0].units.First(u => !u.isFortified);
		new MsgSetFortification(another.id, true) { playerID = humans[1].id }.send();
		ProcessEngineMessages();
		Assert.False(another.isFortified);
	}

	[Fact]
	public async Task TheRoundWaitsForTheLastHuman() {
		C7GameData.GameData gameData = await CreateGame(SaveGameFixture.TwoHumanSave(), simultaneous: true);
		Player[] humans = Humans(gameData);
		Player ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
		TurnsStarted();

		// The second human finishes first: nothing else happens.
		new MsgEndTurn { playerID = humans[1].id }.send();
		ProcessEngineMessages();
		Assert.True(humans[1].hasPlayedThisTurn);
		Assert.Equal(0, gameData.turn);
		Assert.False(ai.hasPlayedThisTurn);
		Assert.Empty(TurnsStarted());
		Assert.Equal([humans[0]], TurnHandling.PlayersToMove(gameData));
		Assert.Equal(humans[0].id, EngineStorage.activePlayerID);

		// Once they have finished, they can't act again this turn.
		MapUnit unit = UnfortifiedUnit(humans[1]);
		new MsgSetFortification(unit.id, true) { playerID = humans[1].id }.send();
		new MsgEndTurn { playerID = humans[1].id }.send();
		ProcessEngineMessages();
		Assert.False(unit.isFortified);
		Assert.Equal(0, gameData.turn);

		// The first human still can.
		MapUnit firstsUnit = UnfortifiedUnit(humans[0]);
		new MsgSetFortification(firstsUnit.id, true) { playerID = humans[0].id }.send();
		ProcessEngineMessages();
		Assert.True(firstsUnit.isFortified);

		// When the last human finishes, the AIs play and every human starts
		// the next turn together.
		new MsgEndTurn { playerID = humans[0].id }.send();
		RunEngineUntil(() => gameData.turn == 1 && TurnHandling.PlayersToMove(gameData).Count == 2);
		Assert.Equal(humans, TurnsStarted());
		Assert.False(ai.hasPlayedThisTurn);
		Assert.All(humans, h => Assert.False(h.hasPlayedThisTurn));

		new MsgSetFortification(unit.id, true) { playerID = humans[1].id }.send();
		ProcessEngineMessages();
		Assert.True(unit.isFortified);
	}

	[Fact]
	public async Task AnEndSentForAnEarlierTurnIsIgnored() {
		C7GameData.GameData gameData = await CreateGame(SaveGameFixture.TwoHumanSave(), simultaneous: true);
		Player[] humans = Humans(gameData);

		new MsgEndTurn { playerID = humans[0].id, turn = 0 }.send();
		new MsgEndTurn { playerID = humans[1].id, turn = 0 }.send();
		RunEngineUntil(() => gameData.turn == 1 && TurnHandling.PlayersToMove(gameData).Count == 2);

		// A second end for the turn that is over doesn't end the next one.
		new MsgEndTurn { playerID = humans[0].id, turn = 0 }.send();
		ProcessEngineMessages();
		Assert.False(humans[0].hasPlayedThisTurn);
	}

	[Fact]
	public async Task EndingATurnRefusesOnlyTheEndingPlayersDeals() {
		C7GameData.GameData gameData = await CreateGame(SaveGameFixture.ThreeHumanSave(), simultaneous: true);
		Player[] humans = Humans(gameData);
		humans[1].gold = 50;
		humans[2].gold = 0;

		// The second human offers gold to the third.
		new MsgProposeDeal(humans[2], new TradeOffer { gold = 30 }, new TradeOffer()) { playerID = humans[1].id }.send();
		ProcessEngineMessages();

		// While the third thinks, nobody else can start talks.
		EngineStorage.messagesToUI.Clear();
		new MsgProposeDeal(humans[1], new TradeOffer(), new TradeOffer { gold = 10 }) { playerID = humans[0].id }.send();
		ProcessEngineMessages();
		Assert.Empty(EngineStorage.messagesToUI.OfType<MsgShowDealProposal>());
		Assert.False(Assert.Single(EngineStorage.messagesToUI.OfType<MsgDealResult>()).accepted);

		// And the first human ending their turn leaves the deal waiting.
		new MsgEndTurn { playerID = humans[0].id }.send();
		ProcessEngineMessages();

		new MsgRespondToDeal(true) { playerID = humans[2].id }.send();
		ProcessEngineMessages();
		Assert.Equal(20, humans[1].gold);
		Assert.Equal(30, humans[2].gold);

		// A deal the ending player is part of is refused.
		new MsgProposeDeal(humans[2], new TradeOffer { gold = 10 }, new TradeOffer()) { playerID = humans[1].id }.send();
		new MsgEndTurn { playerID = humans[2].id }.send();
		ProcessEngineMessages();
		new MsgRespondToDeal(true) { playerID = humans[2].id }.send();
		ProcessEngineMessages();
		Assert.Equal(20, humans[1].gold);
	}

	[Fact]
	public async Task TurnsWithoutTheOptionAreTakenOneByOne() {
		C7GameData.GameData gameData = await CreateGame(SaveGameFixture.TwoHumanSave(), simultaneous: false);
		Player[] humans = Humans(gameData);

		Assert.Equal([humans[0]], TurnsStarted());
		Assert.Equal([humans[0]], TurnHandling.PlayersToMove(gameData));
		Assert.True(TurnHandling.IsPlayersTurn(gameData, humans[0].id));
		Assert.False(TurnHandling.IsPlayersTurn(gameData, humans[1].id));

		MapUnit unit = UnfortifiedUnit(humans[1]);
		new MsgSetFortification(unit.id, true) { playerID = humans[1].id }.send();
		ProcessEngineMessages();
		Assert.False(unit.isFortified);

		// Ending the first human's turn hands it to the second.
		new MsgEndTurn { playerID = humans[0].id }.send();
		ProcessEngineMessages();
		Assert.Equal([humans[1]], TurnsStarted());
		Assert.Equal(humans[1].id, EngineStorage.activePlayerID);
		Assert.Equal(0, gameData.turn);
		Assert.False(TurnHandling.IsPlayersTurn(gameData, humans[0].id));
	}

	[Fact]
	public async Task TheOptionIsSaved() {
		C7GameData.GameData gameData = await CreateGame(SaveGameFixture.TwoHumanSave(), simultaneous: true);
		Assert.True(gameData.simultaneousTurns);

		SaveGame save = SaveGame.FromGameData(gameData);
		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		Assert.True(loaded.SimultaneousTurns);
		Assert.True(loaded.ToGameData(fixture.behaviors).simultaneousTurns);

		// Games saved before the option take turns one by one.
		string json = Encoding.UTF8.GetString(save.ToCompactJSON());
		Assert.Contains("\"simultaneousTurns\":true,", json);
		string older = json.Replace("\"simultaneousTurns\":true,", "");
		Assert.False(SaveGame.FromJSON(Encoding.UTF8.GetBytes(older)).SimultaneousTurns);
	}

	// Runs the host, its engine and a client until the condition holds.
	private static void PumpUntil(LanHost host, LanClient client, Func<bool> condition) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (true) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			EngineStorage.messagesToUI.Clear();
			client.Poll();
			if (condition()) {
				return;
			}
			if (pumping.Elapsed > PumpTimeout) {
				throw new TimeoutException("The LAN game never got there");
			}
			Thread.Sleep(5);
		}
	}

	[Fact]
	public async Task TheHostTimesTheWholeRound() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		// Long enough that no turn runs out while the game is being set up,
		// however slow the machine.
		TimeSpan longLimit = TimeSpan.FromHours(1);
		host.TurnTimeLimit = longLimit;
		ID seatID = host.Seats[0].playerID;

		using LanClient client = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, client, () => client.Lobby != null);
		Assert.False(client.Lobby.simultaneousTurns);
		host.SimultaneousTurns = true;
		PumpUntil(host, client, () => client.Lobby.simultaneousTurns);
		client.ClaimSeat(seatID);
		PumpUntil(host, client, () => client.YourSeats.Contains(seatID));

		// The game was set up for turns one by one; the host's choice wins.
		C7GameData.GameData gameData = await CreateGame(save, simultaneous: false);
		Player[] humans = Humans(gameData);
		host.StartGame();
		Assert.True(gameData.simultaneousTurns);
		PumpUntil(host, client, () => client.StartingGame != null);
		Assert.True(client.StartingGame.SimultaneousTurns);
		client.SnapshotReceived = _ => { };
		client.UiMessageReceived = _ => { };

		// The guest hears both humans are moving.
		PumpUntil(host, client, () => client.CurrentClock()?.playersToMove?.Count == 2);
		Assert.Equal([humans[0].id, seatID], client.CurrentClock().playersToMove);
		Assert.Equal(humans[0].id, client.CurrentClock().activePlayerID);

		// The guest can act while the host is moving, and finishes first.
		MapUnit unit = UnfortifiedUnit(humans[1]);
		client.SendCommand(new MsgSetFortification(unit.id, true));
		client.SendCommand(new MsgEndTurn { turn = 0 });
		PumpUntil(host, client, () => client.CurrentClock().playersToMove.Count == 1);
		Assert.True(unit.isFortified);
		Assert.Equal([humans[0].id], client.CurrentClock().playersToMove);
		Assert.Equal(0, gameData.turn);

		// Nobody ends the host's turn, so once the round's time is short the
		// host's clock does, and the next round starts. The pump checks for
		// it straight after the engine handles it, before the host polls
		// again, so the time can go back up before the next round is timed.
		host.TurnTimeLimit = TimeSpan.FromMilliseconds(50);
		PumpUntil(host, client, () => gameData.turn == 1);
		host.TurnTimeLimit = longLimit;
		Assert.Equal(humans, TurnHandling.PlayersToMove(gameData));

		PumpUntil(host, client, () => client.CurrentClock().turn == 1);
		TurnClockInfo clock = client.CurrentClock();
		Assert.Equal([humans[0].id, seatID], clock.playersToMove);
		Assert.True(clock.secondsElapsed < longLimit.TotalSeconds);
	}

	[Fact]
	public async Task TheTimeLimitEndsEveryRemainingTurn() {
		SaveGame save = SaveGameFixture.ThreeHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) {
			SimultaneousTurns = true,
			TurnTimeLimit = TimeSpan.FromHours(1),
		};

		using LanClient guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, guest, () => guest.Lobby != null);
		foreach (SeatInfo seat in host.Seats) {
			guest.ClaimSeat(seat.playerID);
		}
		PumpUntil(host, guest, () => guest.YourSeats.Count == 2);

		C7GameData.GameData gameData = await CreateGame(save, simultaneous: false);
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = _ => { };

		// One of the guest's players finishes; the host and the other don't.
		guest.SendCommand(new MsgEndTurn { playerID = humans[2].id });
		PumpUntil(host, guest, () => humans[2].hasPlayedThisTurn);
		Assert.Equal([humans[0], humans[1]], TurnHandling.PlayersToMove(gameData));

		// Running out of time ends both their turns, and the round.
		host.TurnTimeLimit = TimeSpan.FromMilliseconds(50);
		PumpUntil(host, guest, () => gameData.turn == 1);
		host.TurnTimeLimit = TimeSpan.FromHours(1);
		Assert.Equal(humans, TurnHandling.PlayersToMove(gameData));
	}
}

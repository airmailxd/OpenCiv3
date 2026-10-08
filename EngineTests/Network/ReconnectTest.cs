using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.Network;

// LAN guests who lose their connection keep their seats, and get them back.
public class ReconnectTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public ReconnectTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private async Task<C7GameData.GameData> CreateGame(SaveGame save, bool simultaneous = false) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();

		save.SimultaneousTurns = simultaneous;
		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		EngineStorage.messagesToUI.Clear();
		return EngineStorage.gameData;
	}

	private static Player[] Humans(C7GameData.GameData gameData) {
		return gameData.players.Where(p => p.isHuman).ToArray();
	}

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	// Runs the host, its engine and the clients until the condition holds.
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
				throw new TimeoutException("The LAN game never got there");
			}
			Thread.Sleep(5);
		}
	}

	private static void PumpUntil(LanHost host, LanClient client, Func<bool> condition) {
		PumpUntil(host, [client], condition);
	}

	// Runs everything for a while, for checking that something doesn't
	// happen.
	private static void PumpFor(LanHost host, LanClient client, TimeSpan time) {
		Stopwatch pumping = Stopwatch.StartNew();
		PumpUntil(host, client, () => pumping.Elapsed >= time);
	}

	// A guest that has joined the host, taken its seats and been shown the
	// game, which it then ignores.
	private static LanClient JoinAndStart(LanHost host, int port, List<ID> seatIDs) {
		LanClient guest = LanClient.Connect("127.0.0.1", port, "Guest");
		PumpUntil(host, guest, () => guest.Lobby != null);
		foreach (ID id in seatIDs) {
			guest.ClaimSeat(id);
		}
		PumpUntil(host, guest, () => guest.YourSeats.Count == seatIDs.Count);
		return guest;
	}

	private static void Watch(LanClient guest) {
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = _ => { };
	}

	// Passes the frames between a client and the host, until it's told to
	// cut every connection through it, as when the network goes down for a
	// moment. Connections made afterwards pass again.
	private sealed class CuttableProxy : IDisposable {
		private readonly TcpListener listener = new(IPAddress.Loopback, 0);
		private readonly int targetPort;
		private readonly List<TcpClient> open = [];
		public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

		public CuttableProxy(int targetPort) {
			this.targetPort = targetPort;
			listener.Start();
			_ = AcceptLoop();
		}

		private async Task AcceptLoop() {
			while (true) {
				TcpClient inbound;
				try {
					inbound = await listener.AcceptTcpClientAsync();
				} catch (Exception) {
					return;
				}
				TcpClient outbound = new();
				try {
					await outbound.ConnectAsync(IPAddress.Loopback, targetPort);
				} catch (Exception) {
					inbound.Dispose();
					outbound.Dispose();
					continue;
				}
				lock (open) {
					open.Add(inbound);
					open.Add(outbound);
				}
				_ = Pipe(inbound, outbound);
				_ = Pipe(outbound, inbound);
			}
		}

		private static async Task Pipe(TcpClient from, TcpClient to) {
			try {
				await from.GetStream().CopyToAsync(to.GetStream());
			} catch (Exception) {
			}
			from.Dispose();
			to.Dispose();
		}

		public void Cut() {
			lock (open) {
				foreach (TcpClient client in open) {
					client.Dispose();
				}
				open.Clear();
			}
		}

		public void Dispose() {
			listener.Stop();
			Cut();
		}
	}

	[Fact]
	public async Task AGuestWhoDropsKeepsTheirSeat() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		LanClient guest = JoinAndStart(host, host.Port, [seatID]);
		string token = guest.ReconnectToken;
		Assert.NotNull(token);

		await CreateGame(save);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		Assert.Equal(token, guest.ReconnectToken);

		guest.Dispose();
		PumpUntil(host, guest, () => host.Seats[0].disconnected);
		Assert.Equal("Guest", host.Seats[0].takenBy);
		Assert.False(host.AllSeatsTaken);
		Assert.True(host.AllSeatsTakenOrHeld);
		Assert.False(EngineStorage.IsPlayerReachable(seatID));
		Assert.Equal([seatID], host.AbsentPlayers);

		// Nobody else can take it.
		using LanClient other = LanClient.Connect("127.0.0.1", host.Port, "Other");
		PumpUntil(host, other, () => other.Lobby != null);
		Assert.True(other.Lobby.seats.Single(s => s.playerID == seatID).disconnected);
		int lobbies = 0;
		other.LobbyChanged += () => ++lobbies;
		other.ClaimSeat(seatID);
		PumpUntil(host, other, () => lobbies > 0);
		Assert.Empty(other.YourSeats);
		Assert.Null(other.StartingGame);
		Assert.True(host.Seats[0].disconnected);

		// Nor someone with the wrong token.
		using LanClient impostor = LanClient.Connect("127.0.0.1", host.Port, "Impostor", "not the token");
		PumpUntil(host, impostor, () => impostor.RejectedReason != null);
		Assert.True(host.Seats[0].disconnected);

		// Once the host gives it up, anyone can.
		Assert.True(host.ReleaseSeat(seatID));
		PumpUntil(host, other, () => other.Lobby.seats.Single(s => s.playerID == seatID).takenBy == null);
		other.ClaimSeat(seatID);
		PumpUntil(host, other, () => other.StartingGame != null);
		Assert.Equal([seatID], other.PlayerIDs);
		Assert.True(host.AllSeatsTaken);
	}

	[Fact]
	public async Task AGuestBackWithItsTokenHasItsSeatsBack() {
		SaveGame save = SaveGameFixture.ThreeHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		List<ID> seatIDs = host.Seats.Select(s => s.playerID).ToList();
		LanClient guest = JoinAndStart(host, host.Port, seatIDs);
		string token = guest.ReconnectToken;

		C7GameData.GameData gameData = await CreateGame(save);
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);

		guest.Dispose();
		PumpUntil(host, guest, () => host.Seats.All(s => s.disconnected));

		// Back as before: both seats at once, and the game.
		using LanClient back = LanClient.Connect("127.0.0.1", host.Port, "Guest", token);
		PumpUntil(host, back, () => back.StartingGame != null);
		Assert.Equal(seatIDs, back.PlayerIDs);
		Assert.True(host.AllSeatsTaken);
		Assert.All(host.Seats, s => Assert.False(s.disconnected));
		Assert.True(EngineStorage.IsPlayerReachable(seatIDs[0]));
		PumpUntil(host, back, () => back.CurrentClock()?.connectedPlayers.Count == 3);

		// And the game goes on with them.
		List<MessageToUI> ui = [];
		back.SnapshotReceived = _ => { };
		back.UiMessageReceived = json => ui.Add(NetSerialization.DeserializeMessageToUI(json));
		new MsgEndTurn().send();
		PumpUntil(host, back, () => ui.OfType<MsgStartTurn>().Any(m => m.player == humans[1]));
		back.SendCommand(new MsgEndTurn { playerID = humans[1].id });
		PumpUntil(host, back, () => EngineStorage.activePlayerID == humans[2].id);
		back.SendCommand(new MsgEndTurn { playerID = humans[2].id });
		PumpUntil(host, back, () => gameData.turn == 1);
	}

	[Fact]
	public async Task AClientThatLosesTheHostReconnectsByItself() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		using CuttableProxy proxy = new(host.Port);
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = JoinAndStart(host, proxy.Port, [seatID]);

		C7GameData.GameData gameData = await CreateGame(save);
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		List<SaveGame> snapshots = [];
		List<MessageToUI> ui = [];
		guest.SnapshotReceived = snapshots.Add;
		guest.UiMessageReceived = json => ui.Add(NetSerialization.DeserializeMessageToUI(json));
		guest.ReconnectAutomatically = true;
		guest.FirstReconnectDelay = TimeSpan.FromMilliseconds(20);
		guest.MaxReconnectDelay = TimeSpan.FromMilliseconds(100);

		// The network goes down for a moment.
		proxy.Cut();
		PumpUntil(host, guest, () => guest.Reconnecting);
		Assert.False(guest.IsConnected);

		// The client is back in its seat, and shown the game afresh.
		snapshots.Clear();
		PumpUntil(host, guest, () => !guest.Reconnecting && host.AllSeatsTaken && snapshots.Count > 0);
		Assert.True(guest.IsConnected);
		Assert.True(guest.ReconnectAttempt >= 1);
		Assert.Equal([seatID], guest.PlayerIDs);
		Assert.Null(guest.RejectedReason);

		// And plays on.
		new MsgEndTurn().send();
		PumpUntil(host, guest, () => ui.OfType<MsgStartTurn>().Any(m => m.player == humans[1]));
		guest.SendCommand(new MsgEndTurn());
		PumpUntil(host, guest, () => gameData.turn == 1);
	}

	[Fact]
	public async Task AClientTurnedAwayStopsTrying() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		using CuttableProxy proxy = new(host.Port);
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = JoinAndStart(host, proxy.Port, [seatID]);

		await CreateGame(save);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		Watch(guest);
		guest.ReconnectAutomatically = true;
		guest.FirstReconnectDelay = TimeSpan.FromMilliseconds(20);
		guest.MaxReconnectDelay = TimeSpan.FromMilliseconds(100);

		// The host gives the seat up while the guest is gone, so it has
		// nothing to come back to.
		proxy.Cut();
		PumpUntil(host, guest, () => host.Seats[0].disconnected);
		host.ReleaseSeat(seatID);
		PumpUntil(host, guest, () => guest.RejectedReason != null);
		Assert.False(guest.Reconnecting);
		Assert.False(guest.IsConnected);
	}

	[Fact]
	public async Task TheHostCanGoOnWithoutADisconnectedPlayer() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		LanClient guest = JoinAndStart(host, host.Port, [seatID]);
		string token = guest.ReconnectToken;

		C7GameData.GameData gameData = await CreateGame(save);
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);

		// The guest drops; once it's their turn, the game waits for them.
		guest.Dispose();
		PumpUntil(host, guest, () => host.Seats[0].disconnected);
		new MsgEndTurn().send();
		PumpUntil(host, guest, () => EngineStorage.activePlayerID == seatID);
		PumpFor(host, guest, TimeSpan.FromMilliseconds(200));
		Assert.False(humans[1].hasPlayedThisTurn);
		Assert.Equal(0, gameData.turn);

		// Going on without them ends their turn for them, every turn.
		host.ContinueWithoutAbsentPlayers();
		Assert.True(host.Seats[0].away);
		Assert.Empty(host.AbsentPlayers);
		PumpUntil(host, guest, () => gameData.turn == 1 && EngineStorage.activePlayerID == humans[0].id);
		Assert.Contains(seatID, host.CurrentClock().awayPlayers);
		new MsgEndTurn().send();
		PumpUntil(host, guest, () => gameData.turn == 2 && EngineStorage.activePlayerID == humans[0].id);

		// Until they're back.
		using LanClient back = LanClient.Connect("127.0.0.1", host.Port, "Guest", token);
		PumpUntil(host, back, () => back.StartingGame != null);
		Watch(back);
		Assert.False(host.Seats[0].away);
		new MsgEndTurn().send();
		PumpUntil(host, back, () => EngineStorage.activePlayerID == seatID);
		PumpFor(host, back, TimeSpan.FromMilliseconds(200));
		Assert.False(humans[1].hasPlayedThisTurn);
		Assert.Equal(2, gameData.turn);
	}

	[Fact]
	public async Task TheHostCanGoOnWithoutADisconnectedPlayerInSimultaneousTurns() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { SimultaneousTurns = true };
		ID seatID = host.Seats[0].playerID;
		LanClient guest = JoinAndStart(host, host.Port, [seatID]);
		string token = guest.ReconnectToken;

		C7GameData.GameData gameData = await CreateGame(save);
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);

		// The host finishes; the round waits for the guest, who has gone.
		guest.Dispose();
		new MsgEndTurn().send();
		PumpUntil(host, guest, () => host.Seats[0].disconnected && humans[0].hasPlayedThisTurn);
		PumpFor(host, guest, TimeSpan.FromMilliseconds(200));
		Assert.Equal(0, gameData.turn);
		Assert.Equal([humans[1]], TurnHandling.PlayersToMove(gameData));

		host.ContinueWithoutAbsentPlayers();
		PumpUntil(host, guest, () => gameData.turn == 1);
		// The next round ends for them as soon as it starts.
		PumpUntil(host, guest, () => TurnHandling.PlayersToMove(gameData).SequenceEqual([humans[0]]));

		// Back, they play their turns again.
		using LanClient back = LanClient.Connect("127.0.0.1", host.Port, "Guest", token);
		PumpUntil(host, back, () => back.StartingGame != null);
		Watch(back);
		Assert.False(host.Seats[0].away);
		new MsgEndTurn().send();
		PumpUntil(host, back, () => gameData.turn == 2 && TurnHandling.PlayersToMove(gameData).Count == 2);
		new MsgEndTurn().send();
		PumpUntil(host, back, () => humans[0].hasPlayedThisTurn);
		PumpFor(host, back, TimeSpan.FromMilliseconds(200));
		Assert.Equal(2, gameData.turn);
		Assert.Equal([humans[1]], TurnHandling.PlayersToMove(gameData));
		back.SendCommand(new MsgEndTurn { turn = 2 });
		PumpUntil(host, back, () => gameData.turn == 3);
	}

	[Fact]
	public async Task NobodyProposesADealToADisconnectedPlayer() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		LanClient guest = JoinAndStart(host, host.Port, [seatID]);

		C7GameData.GameData gameData = await CreateGame(save);
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		guest.Dispose();
		PumpUntil(host, guest, () => host.Seats[0].disconnected);

		humans[0].gold = 50;
		new MsgProposeDeal(humans[1], new TradeOffer { gold = 30 }, new TradeOffer()).send();
		List<MessageToUI> hostUi = [];
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.ProcessNextMessageToEngine();
		}
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			hostUi.Add(msg);
		}
		Assert.False(Assert.Single(hostUi.OfType<MsgDealResult>()).accepted);
		Assert.Equal(50, humans[0].gold);
	}
}

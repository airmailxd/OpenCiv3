using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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

// Snapshots sent as patches to the one before, and how clients that can't
// apply one get the whole game back.
public class SnapshotDeltaTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public SnapshotDeltaTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private async Task<C7GameData.GameData> CreateGame(SaveGame save) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();

		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		EngineStorage.messagesToUI.Clear();
		return EngineStorage.gameData;
	}

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	// Runs the host, its engine and the clients until the condition holds.
	private static void PumpUntil(LanHost host, IEnumerable<LanClient> clients, Func<bool> condition, List<MessageToUI> hostUi = null) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (true) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
				hostUi?.Add(msg);
			}
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

	private static byte[] HostGameHash() {
		return SHA256.HashData(LanProtocol.SnapshotOf(EngineStorage.gameData).ToCompactJSON());
	}

	private static bool Has(LanClient client, byte[] hash) {
		return client.ReceivedSnapshotHash is byte[] received && received.AsSpan().SequenceEqual(hash);
	}

	[Fact]
	public async Task ClientsEndUpWithTheHostsGameThroughPatches() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		using LanClient spectator = LanClient.Connect("127.0.0.1", host.Port, "Watcher");
		LanClient[] clients = [guest, spectator];
		PumpUntil(host, clients, () => guest.Lobby != null);
		guest.ClaimSeat(seatID);
		spectator.Watch();
		PumpUntil(host, clients, () => guest.YourSeats.Contains(seatID) && host.Spectators.Count == 1);

		C7GameData.GameData gameData = await CreateGame(save);
		Player hostPlayer = gameData.players.First(p => p.isHuman && p.id != seatID);
		Player guestPlayer = gameData.GetPlayer(seatID);
		host.StartGame();
		PumpUntil(host, clients, () => guest.StartingGame != null && spectator.StartingGame != null);
		List<MessageToUI> guestUi = [];
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = json => guestUi.Add(NetSerialization.DeserializeMessageToUI(json));
		spectator.SnapshotReceived = _ => { };
		spectator.UiMessageReceived = _ => { };

		// A few turns, with orders from both humans and the AIs' moves.
		List<MessageToUI> hostUi = [];
		for (int turn = 1; turn <= 3; ++turn) {
			if (hostPlayer.units.FirstOrDefault(u => !u.isFortified) is MapUnit hostUnit) {
				new MsgSetFortification(hostUnit.id, true).send();
			}
			guestUi.Clear();
			new MsgEndTurn().send();
			PumpUntil(host, clients, () => guestUi.OfType<MsgStartTurn>().Any(m => m.player == guestPlayer), hostUi);
			if (guestPlayer.units.FirstOrDefault(u => !u.isFortified) is MapUnit guestUnit) {
				guest.SendCommand(new MsgSetFortification(guestUnit.id, true));
			}
			hostUi.Clear();
			guest.SendCommand(new MsgEndTurn());
			PumpUntil(host, clients, () => hostUi.OfType<MsgStartTurn>().Any(m => m.player == hostPlayer), hostUi);
			Assert.Equal(turn, gameData.turn);
		}

		// Once the host settles, both have exactly its game.
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.ProcessNextMessageToEngine();
		}
		byte[] expected = HostGameHash();
		PumpUntil(host, clients, () => Has(guest, expected) && Has(spectator, expected));

		// Each had the whole game once, and patches after that.
		Assert.Equal(1, guest.WholeSnapshotsReceived);
		Assert.True(guest.SnapshotDeltasReceived >= 3, $"The guest had {guest.SnapshotDeltasReceived} patches");
		Assert.Equal(1, spectator.WholeSnapshotsReceived);
		Assert.True(spectator.SnapshotDeltasReceived >= 1);
	}

	// Sends frames over loopback to a client, as a host made by hand.
	private sealed class FakeHost : IDisposable {
		public readonly LanClient client;
		public readonly LanConnection connection;

		public FakeHost() {
			TcpListener listener = new(IPAddress.Loopback, 0);
			listener.Start();
			client = LanClient.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "Guest");
			connection = new LanConnection(listener.AcceptTcpClient());
			listener.Stop();
		}

		// How many times the client has asked for the whole game.
		public int requests;

		public void Poll() {
			while (connection.TryReceive(out Frame frame)) {
				if (frame.kind == FrameKind.RequestSnapshot) {
					++requests;
				}
			}
		}

		public void Dispose() {
			client.Dispose();
			connection.Dispose();
		}
	}

	private static void PollUntil(FakeHost host, Func<bool> condition) {
		Stopwatch waiting = Stopwatch.StartNew();
		while (!condition()) {
			Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(20), "The client never got there");
			host.client.Poll();
			host.Poll();
			Thread.Sleep(5);
		}
	}

	private static EncodedSnapshot SnapshotOfTurn(int turn) {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		save.TurnNumber = turn;
		return LanProtocol.EncodeSnapshot(save);
	}

	[Fact]
	public void AClientThatCantApplyAPatchAsksForTheWholeGame() {
		using FakeHost host = new();
		LanClient client = host.client;
		EncodedSnapshot[] turns = Enumerable.Range(0, 6).Select(SnapshotOfTurn).ToArray();

		host.connection.Send(FrameKind.Start, new StartInfo(null));
		host.connection.Send(FrameKind.Snapshot, turns[0].Compressed);
		PollUntil(host, () => client.StartingGame != null);
		List<int> shown = [];
		List<string> messages = [];
		client.SnapshotReceived = save => shown.Add(save.TurnNumber);
		client.UiMessageReceived = json => messages.Add(Encoding.UTF8.GetString(json));
		void Mark(string message) {
			host.connection.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes(message));
			PollUntil(host, () => messages.Contains(message));
		}

		// A patch to the game the client has is applied.
		byte[] patch = turns[1].PatchFrom(turns[0]);
		Assert.True(patch.Length < turns[1].Compressed.Length / 10);
		host.connection.Send(FrameKind.SnapshotDelta, patch);
		Mark("patched");
		Assert.Equal([1], shown);
		Assert.Equal(turns[1].Hash, client.ReceivedSnapshotHash);

		// A patch to a game it doesn't have isn't: it asks for the whole
		// game, once, and skips the patches until it comes.
		host.connection.Send(FrameKind.SnapshotDelta, turns[3].PatchFrom(turns[2]));
		host.connection.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("between"));
		host.connection.Send(FrameKind.SnapshotDelta, turns[4].PatchFrom(turns[3]));
		Mark("skipped");
		PollUntil(host, () => host.requests == 1);
		Assert.Equal([1], shown);
		Assert.Null(client.ReceivedSnapshotHash);
		host.connection.Send(FrameKind.Snapshot, turns[4].Compressed);
		host.connection.Send(FrameKind.SnapshotDelta, turns[5].PatchFrom(turns[4]));
		Mark("whole");
		// The whole game may be shown before its patch arrives, or not.
		Assert.Equal(5, shown.Last());
		shown.RemoveAll(turn => turn == 4);
		Assert.Equal([1, 5], shown);
		Assert.Equal(1, host.requests);

		// A patch that doesn't give the game it says it does is refused too.
		byte[] corrupt = turns[0].PatchFrom(turns[5]);
		corrupt[^1] ^= 0xFF;
		host.connection.Send(FrameKind.SnapshotDelta, corrupt);
		Mark("corrupt");
		PollUntil(host, () => host.requests == 2);
		Assert.Equal([1, 5], shown);
		host.connection.Send(FrameKind.Snapshot, turns[0].Compressed);
		Mark("recovered");
		Assert.Equal([1, 5, 0], shown);
		Assert.Equal(turns[0].Hash, client.ReceivedSnapshotHash);
	}

	// Two ends of a loopback TCP connection.
	private static (LanConnection sender, LanConnection receiver) Connect() {
		TcpListener listener = new(IPAddress.Loopback, 0);
		listener.Start();
		try {
			TcpClient client = new();
			client.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
			TcpClient server = listener.AcceptTcpClient();
			return (new LanConnection(server), new LanConnection(client));
		} finally {
			listener.Stop();
		}
	}

	private static Frame ReceiveFrame(LanConnection connection) {
		Stopwatch waiting = Stopwatch.StartNew();
		Frame frame;
		while (!connection.TryReceive(out frame)) {
			Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(10), "Nothing came");
			Thread.Sleep(2);
		}
		return frame;
	}

	private static EncodedSnapshot FakeSnapshot(string content) {
		byte[] json = Encoding.UTF8.GetBytes(content);
		return new EncodedSnapshot(json, SHA256.HashData(json));
	}

	[Fact]
	public void AConnectionSendsTheWholeGameFirstThenPatches() {
		(LanConnection sender, LanConnection receiver) = Connect();
		using LanConnection _sender = sender, _receiver = receiver;
		// Like a game: large, and hardly compressible on its own.
		Random random = new(7);
		string game = string.Concat(Enumerable.Range(0, 20_000).Select(_ => random.Next().ToString("x8")));
		EncodedSnapshot first = FakeSnapshot(game), second = FakeSnapshot(game.Replace(game[100..108], "changed!"));

		sender.SendSnapshot(Task.FromResult(first));
		Frame whole = ReceiveFrame(receiver);
		Assert.Equal(FrameKind.Snapshot, whole.kind);
		ReceivedSnapshot received = LanProtocol.ReadSnapshot(whole.kind, whole.payload, null);
		Assert.Equal(first.Hash, received.Hash);

		// The same game isn't sent again; a change is sent as a patch.
		sender.SendSnapshot(Task.FromResult(first));
		sender.SendSnapshot(Task.FromResult(second));
		Frame patch = ReceiveFrame(receiver);
		Assert.Equal(FrameKind.SnapshotDelta, patch.kind);
		Assert.True(patch.payload.Length < 200, $"The patch is {patch.payload.Length} bytes");
		received = LanProtocol.ReadSnapshot(patch.kind, patch.payload, received);
		Assert.Equal(second.Hash, received.Hash);

		// Asked for the whole game, the connection sends it, even unchanged.
		sender.SendWholeSnapshotNext();
		sender.SendSnapshot(Task.FromResult(second));
		sender.Send(FrameKind.UiMessage, [1]);
		Frame again = ReceiveFrame(receiver);
		Assert.Equal(FrameKind.Snapshot, again.kind);
		Assert.Equal(second.Hash, LanProtocol.ReadSnapshot(again.kind, again.payload, null).Hash);
		Assert.Equal(FrameKind.UiMessage, ReceiveFrame(receiver).kind);
	}

	[Fact]
	public void BrokenSnapshotsAreRefused() {
		EncodedSnapshot first = SnapshotOfTurn(0), second = SnapshotOfTurn(1);
		byte[] whole = first.Compressed;
		Assert.Equal(first.Json, SnapshotCompression.Decompress(whole, LanProtocol.MaxSnapshotJsonBytes));
		Assert.Throws<System.IO.InvalidDataException>(() => SnapshotCompression.Decompress(whole, first.Json.Length - 1));
		Assert.Throws<System.IO.InvalidDataException>(() => SnapshotCompression.Decompress(whole[..^10], LanProtocol.MaxSnapshotJsonBytes));
		Assert.Throws<System.IO.InvalidDataException>(() => SnapshotCompression.Decompress([.. whole, 1, 2, 3], LanProtocol.MaxSnapshotJsonBytes));
		Assert.Throws<System.IO.InvalidDataException>(() => SnapshotCompression.Decompress(Encoding.UTF8.GetBytes("not zstd"), LanProtocol.MaxSnapshotJsonBytes));

		// A patch needs the snapshot it patches.
		byte[] patch = second.PatchFrom(first);
		ReceivedSnapshot base_ = new(first.Json, first.Hash);
		Assert.Equal(second.Json, LanProtocol.ReadSnapshot(FrameKind.SnapshotDelta, patch, base_).Json);
		Assert.Throws<System.IO.InvalidDataException>(() => LanProtocol.ReadSnapshot(FrameKind.SnapshotDelta, patch, null));
		Assert.Throws<System.IO.InvalidDataException>(() => LanProtocol.ReadSnapshot(FrameKind.SnapshotDelta, patch, new ReceivedSnapshot(new byte[first.Json.Length], first.Hash)));
		Assert.Throws<System.IO.InvalidDataException>(() => LanProtocol.ReadSnapshot(FrameKind.SnapshotDelta, patch[..40], base_));
	}

	[Fact]
	public async Task TheHostSendsTheWholeGameWhenAsked() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		using TcpClient tcp = new("127.0.0.1", host.Port);
		using LanConnection guest = new(tcp);
		guest.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, "Guest"));
		guest.Send(FrameKind.ClaimSeat, new ClaimSeatInfo(seatID));

		ReceivedSnapshot received = null;
		List<FrameKind> snapshots = [];
		void PumpUntil(Func<bool> condition) {
			Stopwatch pumping = Stopwatch.StartNew();
			while (!condition()) {
				Assert.True(pumping.Elapsed < PumpTimeout, "The host never got there");
				host.Poll();
				EngineStorage.ProcessNextMessageToEngine();
				while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
				while (guest.TryReceive(out Frame frame)) {
					if (frame.kind is FrameKind.Snapshot or FrameKind.SnapshotDelta) {
						received = LanProtocol.ReadSnapshot(frame.kind, frame.payload, received);
						snapshots.Add(frame.kind);
					}
				}
				Thread.Sleep(5);
			}
		}
		PumpUntil(() => host.AllSeatsTaken);

		C7GameData.GameData gameData = await CreateGame(save);
		host.StartGame();
		PumpUntil(() => snapshots.Count == 1);
		Assert.Equal(FrameKind.Snapshot, snapshots[0]);

		Player hostPlayer = gameData.players.First(p => p.isHuman && p.id != seatID);
		new MsgSetFortification(hostPlayer.units.First(u => !u.isFortified).id, true).send();
		PumpUntil(() => snapshots.Count == 2);
		Assert.Equal(FrameKind.SnapshotDelta, snapshots[1]);

		// Nothing has changed, but the guest is sent the whole game.
		guest.Send(FrameKind.RequestSnapshot, []);
		PumpUntil(() => snapshots.Count == 3);
		Assert.Equal(FrameKind.Snapshot, snapshots[2]);
		Assert.Equal(HostGameHash(), received.Hash);
	}
}

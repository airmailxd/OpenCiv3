using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.Network;

// How LAN games keep the network and snapshots off the game's main thread,
// and avoid sending and showing snapshots that change nothing.
public class PerfLanTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public PerfLanTests(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private async Task<C7GameData.GameData> CreateTwoHumanGame() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();

		await CreateGame.createGame(SaveGameFixture.TwoHumanSave(), (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		EngineStorage.messagesToUI.Clear();
		return EngineStorage.gameData;
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

	private static List<Frame> ReceiveFrames(LanConnection connection, int count) {
		List<Frame> frames = [];
		Stopwatch waiting = Stopwatch.StartNew();
		while (frames.Count < count && waiting.Elapsed < TimeSpan.FromSeconds(10)) {
			if (connection.TryReceive(out Frame frame)) {
				frames.Add(frame);
			} else {
				Thread.Sleep(2);
			}
		}
		return frames;
	}

	private static EncodedSnapshot FakeSnapshot(string content) {
		byte[] bytes = Encoding.UTF8.GetBytes(content);
		return new EncodedSnapshot(bytes, System.Security.Cryptography.SHA256.HashData(bytes));
	}

	private static string Describe(Frame frame) {
		return $"{frame.kind}:{Encoding.UTF8.GetString(frame.payload)}";
	}

	[Fact]
	public void FramesAreWrittenInOrderWhileSnapshotsAreEncoded() {
		(LanConnection sender, LanConnection receiver) = Connect();
		using LanConnection _sender = sender, _receiver = receiver;

		// The first snapshot is still being encoded, so everything after it
		// waits, in order.
		TaskCompletionSource<EncodedSnapshot> encoding = new();
		sender.SendSnapshot(encoding.Task);
		sender.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("1"));
		// B is replaced by C, queued straight after it with nothing between.
		sender.SendSnapshot(Task.FromResult(FakeSnapshot("B")));
		sender.SendSnapshot(Task.FromResult(FakeSnapshot("C")));
		sender.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("2"));
		// C again is skipped: the receiver already has it.
		sender.SendSnapshot(Task.FromResult(FakeSnapshot("C")));
		sender.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("3"));
		sender.SendSnapshot(Task.FromResult(FakeSnapshot("D")));
		// Marks the end, so the test knows nothing else was sent before it.
		sender.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("end"));

		// Nothing can be written while A is encoded; if anything were, it
		// would arrive before A and the order below would be wrong.
		Assert.False(receiver.TryPeek(out _));
		encoding.SetResult(FakeSnapshot("A"));

		List<Frame> frames = ReceiveFrames(receiver, 7);
		Assert.Equal(["Snapshot:A", "UiMessage:1", "Snapshot:C", "UiMessage:2", "UiMessage:3", "Snapshot:D", "UiMessage:end"], frames.Select(Describe));
		Assert.False(receiver.TryReceive(out _));
	}

	[Fact]
	public void ASnapshotThatFailsToEncodeIsSkipped() {
		(LanConnection sender, LanConnection receiver) = Connect();
		using LanConnection _sender = sender, _receiver = receiver;

		sender.SendSnapshot(Task.FromException<EncodedSnapshot>(new InvalidOperationException("broken")));
		sender.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("after"));
		Assert.Equal(["UiMessage:after"], ReceiveFrames(receiver, 1).Select(Describe));
		Assert.False(sender.IsClosed);
	}

	[Fact]
	public void ClosingSendsWhatWasQueuedFirst() {
		(LanConnection sender, LanConnection receiver) = Connect();
		using LanConnection _receiver = receiver;

		for (int i = 0; i < 200; ++i) {
			sender.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes(i.ToString()));
		}
		sender.Send(FrameKind.Rejected, Encoding.UTF8.GetBytes("bye"));
		sender.Dispose();
		Assert.True(sender.IsClosed);

		List<Frame> frames = ReceiveFrames(receiver, 201);
		Assert.Equal(Enumerable.Range(0, 200).Select(i => $"UiMessage:{i}").Append("Rejected:bye"), frames.Select(Describe));

		// Nothing more is sent once closed, and the other end sees it close.
		sender.Send(FrameKind.UiMessage, [1]);
		Stopwatch waiting = Stopwatch.StartNew();
		while (!receiver.IsClosed && waiting.Elapsed < TimeSpan.FromSeconds(10)) {
			Thread.Sleep(5);
		}
		Assert.True(receiver.IsClosed);
		Assert.False(receiver.TryReceive(out _));
	}

	[Fact]
	public void APeerThatDoesNotReadDoesNotHoldUpTheSender() {
		TcpListener listener = new(IPAddress.Loopback, 0);
		listener.Start();
		using TcpClient stalled = new();
		stalled.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
		using LanConnection sender = new(listener.AcceptTcpClient());
		listener.Stop();

		// Far more than the socket's buffers hold; the peer never reads. If
		// sending waited for the network it would never finish, so a generous
		// timeout tells the two apart without depending on the machine's speed.
		byte[] payload = new byte[4 * 1024 * 1024];
		Task sending = Task.Run(() => {
			for (int i = 0; i < 16; ++i) {
				sender.Send(FrameKind.UiMessage, payload);
			}
		});
		Assert.True(sending.Wait(TimeSpan.FromSeconds(60)), "Sending waited for a peer that never reads");
		Assert.False(sender.IsClosed);
	}

	[Fact]
	public void APeerThatFallsTooFarBehindIsDropped() {
		TcpListener listener = new(IPAddress.Loopback, 0);
		listener.Start();
		using TcpClient stalled = new();
		stalled.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
		using LanConnection sender = new(listener.AcceptTcpClient());
		listener.Stop();

		byte[] payload = new byte[16 * 1024 * 1024];
		for (int i = 0; i < 20 && !sender.IsClosed; ++i) {
			sender.Send(FrameKind.UiMessage, payload);
		}
		Assert.True(sender.IsClosed);
	}

	[Fact]
	public async Task SnapshotsShareNothingTheGameGoesOnToChange() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		byte[] expected = SaveGame.FromGameData(gameData).ToCompactJSON();

		SaveGame snapshot = LanProtocol.SnapshotOf(gameData);
		Assert.Equal(expected, snapshot.ToCompactJSON());

		// Change the game in the ways that FromGameData would have let
		// through to the snapshot.
		Player player = gameData.players.First(p => p.isHuman);
		gameData.GreatWondersBuilt.Add("The Great Test");
		player.knownTechs.Add(gameData.techs.First(t => !player.knownTechs.Contains(t.id)).id);
		gameData.history.Values.First().Add(new HistTurnRecord { Date = 1234, Score = 99 });
		PlayerRelationship relationship = player.playerRelationships.Values.FirstOrDefault();
		if (relationship != null) {
			relationship.warDeclarationCount += 7;
		}
		gameData.rules.ShowScoreboard = !gameData.rules.ShowScoreboard;
		Assert.NotEqual(expected, SaveGame.FromGameData(gameData).ToCompactJSON());

		Assert.Equal(expected, snapshot.ToCompactJSON());
		EncodedSnapshot encoded = await Task.Run(() => LanProtocol.EncodeSnapshot(snapshot));
		Assert.Equal(expected, LanProtocol.DecodeSnapshot(encoded.Compressed).ToCompactJSON());
	}

	[Fact]
	public async Task AnUnchangedSnapshotReusesTheLastEncoding() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		EncodedSnapshot first = LanProtocol.EncodeSnapshot(LanProtocol.SnapshotOf(gameData));
		Assert.Same(first, LanProtocol.EncodeSnapshot(LanProtocol.SnapshotOf(gameData), first));

		gameData.players[0].gold += 1;
		EncodedSnapshot changed = LanProtocol.EncodeSnapshot(LanProtocol.SnapshotOf(gameData), first);
		Assert.NotSame(first, changed);
		Assert.NotEqual(first.Hash, changed.Hash);
		Assert.Equal(gameData.players[0].gold, LanProtocol.DecodeSnapshot(changed.Compressed).Players[0].gold);
	}

	// Runs the host, its engine and a client until the condition holds.
	private static void PumpUntil(LanHost host, LanClient client, Func<bool> condition) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (true) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
			client.Poll();
			if (condition()) {
				return;
			}
			if (pumping.Elapsed > TimeSpan.FromSeconds(60)) {
				throw new TimeoutException("The LAN game never got there");
			}
			Thread.Sleep(5);
		}
	}

	[Fact]
	public async Task TheHostSendsNoSnapshotWhenNothingChanged() {
		using LanHost host = new("Host", SaveGameFixture.TwoHumanSave(), port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		using LanClient client = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, client, () => client.Lobby != null);
		client.ClaimSeat(seatID);
		PumpUntil(host, client, () => client.Lobby.yourSeat == seatID);

		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player hostPlayer = gameData.players.First(p => p.isHuman && p.id != seatID);
		host.StartGame();
		PumpUntil(host, client, () => client.StartingGame != null);
		List<SaveGame> snapshots = [];
		client.SnapshotReceived = snapshots.Add;
		client.UiMessageReceived = _ => { };

		// It isn't the guest's turn, so the host's engine refuses their
		// order: a message was processed, but the game is as it was.
		MapUnit guestUnit = gameData.GetPlayer(seatID).units.First();
		bool guestUnitFortified = guestUnit.isFortified;
		long processed = EngineStorage.processedMessageCount;
		client.SendCommand(new MsgSetFortification(guestUnit.id, true));
		PumpUntil(host, client, () => EngineStorage.processedMessageCount > processed);
		Assert.Equal(guestUnitFortified, guestUnit.isFortified);

		// A real change is sent. Snapshots reach the client in order, so the
		// first one it's shown is the first one sent since the game started:
		// had the refused order sent one, it would show the game without
		// this change.
		MapUnit unit = hostPlayer.units.First(u => !u.isFortified);
		new MsgSetFortification(unit.id, true) { playerID = hostPlayer.id }.send();
		PumpUntil(host, client, () => snapshots.Count > 0);
		Assert.Equal("fortified", snapshots.First().Units.Single(u => u.id == unit.id).action);
		Assert.Single(snapshots);
	}

	// A host made by hand, to send a client exactly the frames a test wants.
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

		public void Dispose() {
			client.Dispose();
			connection.Dispose();
		}
	}

	private static byte[] SnapshotOfTurn(int turn) {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		save.TurnNumber = turn;
		return LanProtocol.EncodeSnapshot(save).Compressed;
	}

	private static void PollUntil(LanClient client, Func<bool> condition) {
		Stopwatch waiting = Stopwatch.StartNew();
		while (!condition()) {
			Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(20), "The client never got there");
			client.Poll();
			Thread.Sleep(5);
		}
	}

	// Waits, without handling them, until the client has received this many
	// frames and finished reading every snapshot among them.
	private static void WaitUntilReceivedAndRead(LanClient client, int count) {
		LanConnection connection = (LanConnection)typeof(LanClient)
			.GetField("connection", BindingFlags.NonPublic | BindingFlags.Instance)
			.GetValue(client);
		bool Ready() {
			if (!connection.TryPeekAt(count - 1, out _)) {
				return false;
			}
			for (int i = 0; i < count; ++i) {
				if (connection.TryPeekAt(i, out Frame frame) && frame.Prepared is Task reading && !reading.IsCompleted) {
					return false;
				}
			}
			return true;
		}
		Stopwatch waiting = Stopwatch.StartNew();
		while (!Ready()) {
			Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(20), "The client never read the snapshots");
			Thread.Sleep(5);
		}
	}

	[Fact]
	public void ClientsSkipUnchangedSnapshotsAndDropSupersededOnes() {
		using FakeHost host = new();
		LanClient client = host.client;
		byte[] turn0 = SnapshotOfTurn(0), turn1 = SnapshotOfTurn(1), turn2 = SnapshotOfTurn(2);

		host.connection.Send(FrameKind.Start, new StartInfo(null));
		host.connection.Send(FrameKind.Snapshot, turn0);
		PollUntil(client, () => client.StartingGame != null);
		Assert.Equal(0, client.StartingGame.TurnNumber);

		List<int> shown = [];
		List<string> messages = [];
		client.SnapshotReceived = save => shown.Add(save.TurnNumber);
		client.UiMessageReceived = json => messages.Add(Encoding.UTF8.GetString(json));

		// The same game again isn't shown again.
		host.connection.Send(FrameKind.Snapshot, turn0);
		host.connection.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("a"));
		PollUntil(client, () => messages.Count == 1);
		Assert.Empty(shown);

		// Two snapshots in a row: once both are read, only the newer is shown.
		host.connection.Send(FrameKind.Snapshot, turn1);
		host.connection.Send(FrameKind.Snapshot, turn2);
		host.connection.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("b"));
		WaitUntilReceivedAndRead(client, 3);
		PollUntil(client, () => messages.Count == 2);
		Assert.Equal([2], shown);

		// A message between snapshots keeps both, in order, and the snapshot
		// after it waits until the message has been handled.
		host.connection.Send(FrameKind.Snapshot, turn1);
		host.connection.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("c"));
		host.connection.Send(FrameKind.Snapshot, turn2);
		host.connection.Send(FrameKind.Snapshot, turn2);
		host.connection.Send(FrameKind.UiMessage, Encoding.UTF8.GetBytes("d"));
		PollUntil(client, () => messages.Count == 4);
		Assert.Equal([2, 1, 2], shown);
		Assert.Equal(["a", "b", "c", "d"], messages);
	}

	[Fact]
	public async Task MessagesAcceptTilesInEveryFormat() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		MapUnit unit = gameData.players.First(p => p.isHuman).units.First();
		Tile tile = unit.location.neighbors.Values.First(t => t != Tile.NONE);

		string json = Encoding.UTF8.GetString(NetSerialization.Serialize(new MsgBombard(unit.id, tile)));
		string written = $"\"{tile.XCoordinate},{tile.YCoordinate}\"";
		Assert.Contains(written, json);

		MsgBombard Read(string text) => Assert.IsType<MsgBombard>(NetSerialization.DeserializeMessageToEngine(Encoding.UTF8.GetBytes(text)));
		Assert.Same(tile, Read(json).tile);
		Assert.Same(tile, Read(json.Replace(written, $"[{tile.XCoordinate},{tile.YCoordinate}]")).tile);
		Assert.Same(tile, Read(json.Replace(written, $"\"{tile.XCoordinate}\\u002C{tile.YCoordinate}\"")).tile);
		Assert.Null(Read(json.Replace(written, "null")).tile);
		Assert.ThrowsAny<System.Text.Json.JsonException>(() => Read(json.Replace(written, "\"1;2\"")));
	}

	private record Item(string key);

	[Fact]
	public void ListIndexesFindWhatASearchWould() {
		NetSerialization.ListIndex<string, Item> index = new(i => i.key);
		Item a = new("a"), b = new("b"), c = new("c");
		List<Item> items = [a, b, c];

		Assert.Same(b, index.Find(items, "b"));
		Assert.Null(index.Find(items, "z"));
		Assert.Null(index.Find(null, "a"));

		// Removing, inserting and replacing are all noticed.
		items.Remove(b);
		Assert.Null(index.Find(items, "b"));
		Assert.Same(c, index.Find(items, "c"));
		Item otherA = new("a");
		items.Insert(0, otherA);
		Assert.Same(otherA, index.Find(items, "a"));
		Item d = new("d");
		items.Add(d);
		Assert.Same(d, index.Find(items, "d"));
		// [otherA, a, c, d]: replacing one in place, even with an equal key.
		Item otherC = new("c");
		items[2] = otherC;
		Assert.Same(otherC, index.Find(items, "c"));
		items[1] = new Item("e");
		Assert.Same(otherA, index.Find(items, "a"));
		Assert.Same(items[1], index.Find(items, "e"));

		// As is a different list, like a replaced game's.
		List<Item> replaced = [new("a")];
		Assert.Same(replaced[0], index.Find(replaced, "a"));
		Assert.Null(index.Find(replaced, "c"));
	}
}

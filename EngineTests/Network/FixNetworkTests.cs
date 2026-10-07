using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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

// Frames that can't be read don't take a LAN host or client down, paths and
// tiles from the network are checked, and the network code doesn't hold on to
// more than it needs.
public class FixNetworkTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public FixNetworkTests(SaveGameFixture fixture) {
		this.fixture = fixture;
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	private static readonly Lazy<SaveGame> twoHumanSave = new(() => SaveGameFixture.LoadSave(new GameMode.Config("civ3"), humanPlayers: 2));

	private async Task<C7GameData.GameData> CreateTwoHumanGame() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();

		await CreateGame.createGame(twoHumanSave.Value.Clone(), (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		EngineStorage.messagesToUI.Clear();
		return EngineStorage.gameData;
	}

	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

	// Runs the host and its engine until the condition holds.
	private static void PumpHostUntil(LanHost host, Func<bool> condition) {
		Stopwatch waiting = Stopwatch.StartNew();
		while (!condition()) {
			Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(20), "The host never got there");
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
			Thread.Sleep(5);
		}
	}

	private static void PollUntil(LanClient client, Func<bool> condition) {
		Stopwatch waiting = Stopwatch.StartNew();
		while (!condition()) {
			Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(20), "The client never got there");
			client.Poll();
			Thread.Sleep(5);
		}
	}

	// A raw connection to a host, sending exactly what a test wants.
	private static LanConnection ConnectRaw(LanHost host) {
		return new LanConnection(new TcpClient("127.0.0.1", host.Port));
	}

	[Fact]
	public void BadFramesFromAGuestInTheLobbyAreIgnored() {
		using LanHost host = new("Host", twoHumanSave.Value.Clone(), port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		using LanConnection guest = ConnectRaw(host);

		guest.Send(FrameKind.Hello, Utf8("null"));
		guest.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, "Guest"));
		guest.Send(FrameKind.ClaimSeat, Utf8("null"));
		guest.Send(FrameKind.ClaimSeat, Utf8("{\"playerID\":\"Player-none\"}"));
		guest.Send(FrameKind.ClaimSeat, Utf8("{\"playerID\":\"Warrior-99999999999\"}"));
		guest.Send(FrameKind.ClaimSeat, Utf8("{\"playerID\":42}"));
		guest.Send(FrameKind.ClaimSeat, Utf8("{\"playerID\":\"nonsense\"}"));
		guest.Send(FrameKind.ClaimSeat, new ClaimSeatInfo(seatID));

		PumpHostUntil(host, () => host.AllSeatsTaken);
		Assert.Equal("Guest", host.Seats[0].takenBy);
		Assert.False(guest.IsClosed);
	}

	private async Task<(LanHost host, LanConnection guest, C7GameData.GameData gameData)> StartWithRawGuest() {
		LanHost host = new("Host", twoHumanSave.Value.Clone(), port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;
		LanConnection guest = ConnectRaw(host);
		guest.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, "Guest"));
		guest.Send(FrameKind.ClaimSeat, new ClaimSeatInfo(seatID));
		PumpHostUntil(host, () => host.AllSeatsTaken);

		C7GameData.GameData gameData = await CreateTwoHumanGame();
		host.StartGame();
		return (host, guest, gameData);
	}

	[Fact]
	public async Task BadCommandsFromASeatedGuestAreIgnored() {
		(LanHost host, LanConnection guest, C7GameData.GameData gameData) = await StartWithRawGuest();
		using LanHost ownedHost = host;
		using LanConnection ownedGuest = guest;
		MapUnit unit = gameData.GetPlayer(host.Seats[0].playerID).units.First();
		string command = Encoding.UTF8.GetString(NetSerialization.Serialize(new MsgSetFortification(unit.id, true)));
		string unitID = $"\"{unit.id}\"";
		Assert.Contains(unitID, command);

		guest.Send(FrameKind.Command, Utf8("null"));
		guest.Send(FrameKind.Command, Utf8(command.Replace(unitID, "\"Player-none\"")));
		guest.Send(FrameKind.Command, Utf8(command.Replace(unitID, "\"Warrior-99999999999\"")));
		guest.Send(FrameKind.Command, Utf8(command.Replace(unitID, "17")));
		guest.Send(FrameKind.Command, Utf8("{\"$type\":\"NoSuchMessage\"}"));
		guest.Send(FrameKind.Command, Utf8("{"));
		guest.Send(FrameKind.ChooseCivilization, Utf8("null"));
		// Then a good one, which the engine gets.
		long processed = EngineStorage.processedMessageCount;
		guest.Send(FrameKind.Command, Utf8(command));
		PumpHostUntil(host, () => EngineStorage.processedMessageCount > processed);
		Assert.Equal("Guest", host.Seats[0].takenBy);
		Assert.False(guest.IsClosed);
	}

	[Fact]
	public async Task AGuestSendingNothingButGarbageIsDropped() {
		(LanHost host, LanConnection guest, _) = await StartWithRawGuest();
		using LanHost ownedHost = host;
		using LanConnection ownedGuest = guest;
		for (int i = 0; i < LanConnection.MaxBadFramesInARow; ++i) {
			guest.Send(FrameKind.Command, Utf8("null"));
		}
		PumpHostUntil(host, () => host.Seats[0].takenBy == null);
		Assert.False(host.AllSeatsTaken);
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
		SaveGame save = twoHumanSave.Value.Clone();
		save.TurnNumber = turn;
		return LanProtocol.EncodeSnapshot(save).Compressed;
	}

	[Fact]
	public void BadFramesFromTheHostAreIgnored() {
		using FakeHost host = new();
		LanClient client = host.client;

		host.connection.Send(FrameKind.Lobby, Utf8("null"));
		host.connection.Send(FrameKind.Lobby, Utf8("{"));
		host.connection.Send(FrameKind.Lobby, Utf8("{\"hostName\":\"Host\",\"seats\":[],\"yourSeats\":[\"Player-none\"]}"));
		host.connection.Send(FrameKind.TurnClock, Utf8("null"));
		host.connection.Send(FrameKind.Lobby, new LobbyInfo("Host", [], null));
		PollUntil(client, () => client.Lobby != null);
		Assert.Equal("Host", client.Lobby.hostName);

		host.connection.Send(FrameKind.Start, Utf8("null"));
		host.connection.Send(FrameKind.Snapshot, Utf8("not a snapshot"));
		host.connection.Send(FrameKind.Start, new StartInfo(null));
		host.connection.Send(FrameKind.Snapshot, SnapshotOfTurn(3));
		PollUntil(client, () => client.StartingGame != null);
		Assert.Equal(3, client.StartingGame.TurnNumber);

		// What the game does with a frame failing doesn't stop the frames
		// after it.
		List<string> messages = [];
		client.SnapshotReceived = _ => throw new InvalidOperationException("The game choked");
		client.UiMessageReceived = json => {
			string text = Encoding.UTF8.GetString(json);
			messages.Add(text);
			if (text == "bad") {
				throw new JsonException("Not a message");
			}
		};
		host.connection.Send(FrameKind.UiMessage, Utf8("bad"));
		host.connection.Send(FrameKind.Snapshot, SnapshotOfTurn(4));
		host.connection.Send(FrameKind.UiMessage, Utf8("good"));
		PollUntil(client, () => messages.Count == 2);
		Assert.Equal(["bad", "good"], messages);
		Assert.True(client.IsConnected);
	}

	[Fact]
	public void SnapshotsQueuedWhileTheGameLoadsAreReadOnlyIfNeeded() {
		using FakeHost host = new();
		LanClient client = host.client;
		host.connection.Send(FrameKind.Start, new StartInfo(null));
		host.connection.Send(FrameKind.Snapshot, SnapshotOfTurn(0));
		PollUntil(client, () => client.StartingGame != null);

		// While the game screen loads, the client handles nothing, however
		// long it's polled.
		host.connection.Send(FrameKind.Snapshot, SnapshotOfTurn(1));
		host.connection.Send(FrameKind.UiMessage, Utf8("a"));
		host.connection.Send(FrameKind.Snapshot, SnapshotOfTurn(2));
		host.connection.Send(FrameKind.Snapshot, SnapshotOfTurn(3));
		host.connection.Send(FrameKind.Snapshot, SnapshotOfTurn(4));
		host.connection.Send(FrameKind.UiMessage, Utf8("b"));
		for (int i = 0; i < 100; ++i) {
			client.Poll();
			Thread.Sleep(5);
		}

		// Then each message follows its snapshot, and of the snapshots in a
		// row only the newest is shown.
		List<string> shown = [];
		client.SnapshotReceived = save => shown.Add($"turn {save.TurnNumber}");
		client.UiMessageReceived = json => shown.Add(Encoding.UTF8.GetString(json));
		PollUntil(client, () => shown.Contains("b"));
		Assert.Equal(["turn 1", "a", "turn 4", "b"], shown);
	}

	[Fact]
	public void APeerWhoseFramesPileUpUnreadIsDropped() {
		TcpListener listener = new(IPAddress.Loopback, 0);
		listener.Start();
		using TcpClient tcp = new("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
		using LanConnection receiver = new(listener.AcceptTcpClient());
		listener.Stop();
		receiver.maxReceivedFrames = 5;
		using LanConnection sender = new(tcp);

		for (int i = 0; i < 10; ++i) {
			sender.Send(FrameKind.UiMessage, Utf8($"{i}"));
		}
		Stopwatch waiting = Stopwatch.StartNew();
		while (!receiver.IsClosed) {
			Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(10), "The receiver kept every frame");
			Thread.Sleep(5);
		}
		// What arrived before it gave up can still be read.
		int read = 0;
		while (receiver.TryReceive(out _)) {
			++read;
		}
		Assert.Equal(5, read);
	}

	private static MsgSetUnitPath ReadPath(string tiles) {
		string json = $"{{\"$type\":\"MsgSetUnitPath\",\"unitID\":\"Warrior-1\",\"path\":[{tiles}]}}";
		return Assert.IsType<MsgSetUnitPath>(NetSerialization.DeserializeMessageToEngine(Utf8(json)));
	}

	private static string Write(Tile tile) => $"\"{tile.XCoordinate},{tile.YCoordinate}\"";

	[Fact]
	public async Task PathsFromTheNetworkMustBeWalkable() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Tile start = gameData.players.First(p => p.isHuman).units.First().location;
		Tile next = start.neighbors.Values.First(t => t != Tile.NONE);
		Tile after = next.neighbors.Values.First(t => t != Tile.NONE && t != start && !start.neighbors.ContainsValue(t));

		MsgSetUnitPath path = ReadPath($"{Write(next)},{Write(after)}");
		Assert.Equal([next, after], path.path.path);
		Assert.Same(after, path.path.destination);

		// Off the map, either side of it or between tiles.
		int beyond = gameData.map.numTilesTall * 4;
		Assert.Equal(Tile.NONE, gameData.map.tileAt(1, 2));
		Assert.Equal(Tile.NONE, gameData.map.tileAt(2, beyond));
		Assert.ThrowsAny<JsonException>(() => ReadPath($"{Write(next)},\"1,2\""));
		Assert.ThrowsAny<JsonException>(() => ReadPath($"\"2,{beyond}\""));
		Assert.ThrowsAny<JsonException>(() => ReadPath($"{Write(next)},null"));
		// Tiles that aren't next to each other.
		Assert.ThrowsAny<JsonException>(() => ReadPath($"{Write(start)},{Write(after)}"));
		Assert.ThrowsAny<JsonException>(() => ReadPath($"{Write(next)},{Write(next)}"));
		// Not tiles at all.
		Assert.ThrowsAny<JsonException>(() => ReadPath("[1,\"a\"]"));
		Assert.ThrowsAny<JsonException>(() => ReadPath("[1,2,3]"));

		// A single tile off the map is no tile.
		MsgBombard bombard = new(start.unitsOnTile.First().id, next);
		string json = Encoding.UTF8.GetString(NetSerialization.Serialize(bombard)).Replace(Write(next), "\"1,2\"");
		Assert.Null(Assert.IsType<MsgBombard>(NetSerialization.DeserializeMessageToEngine(Utf8(json))).tile);
	}

	[Fact]
	public async Task BadReferencesAreBadJson() {
		await CreateTwoHumanGame();
		string[] bad = [
			"{\"$type\":\"MsgEndTurn\",\"playerID\":\"Player-99999999999\"}",
			"{\"$type\":\"MsgEndTurn\",\"playerID\":\"Player\"}",
			"{\"$type\":\"MsgEndTurn\",\"playerID\":3}",
			"{\"$type\":\"MsgSetFortification\",\"unitID\":\"Warrior-x\",\"fortify\":true}",
		];
		foreach (string json in bad) {
			Assert.ThrowsAny<JsonException>(() => NetSerialization.DeserializeMessageToEngine(Utf8(json)));
		}
		Assert.ThrowsAny<JsonException>(() => NetSerialization.DeserializeRequired<HelloInfo>(Utf8("null")));

		// A "none" ID is a valid ID (ID.ToString writes it that way), so it
		// parses; the host replaces a command's playerID with its seat's anyway.
		MsgEndTurn none = (MsgEndTurn)NetSerialization.DeserializeMessageToEngine(Utf8("{\"$type\":\"MsgEndTurn\",\"playerID\":\"Player-none\"}"));
		Assert.Equal(ID.None("Player"), none.playerID);
	}

	private record Item(string key);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static (WeakReference list, WeakReference item) IndexAList(NetSerialization.ListIndex<string, Item> index) {
		List<Item> items = [new("a"), new("b")];
		Assert.Same(items[1], index.Find(items, "b"));
		return (new WeakReference(items), new WeakReference(items[1]));
	}

	[Fact]
	public void ListIndexesDoNotKeepAReplacedGameAlive() {
		NetSerialization.ListIndex<string, Item> index = new(i => i.key);
		(WeakReference list, WeakReference item) = IndexAList(index);
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		Assert.False(list.IsAlive);
		Assert.False(item.IsAlive);
		GC.KeepAlive(index);
	}

	[Fact]
	public async Task SnapshotsShareTheKnownTilesAsTheyAre() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		SaveGame save = SaveGame.FromGameData(gameData);
		byte[] expected = save.ToCompactJSON();
		List<string> known = save.Players.Select(p => p.knownTileIndices).ToList();
		Assert.Contains(known, k => !string.IsNullOrEmpty(k));

		SnapshotDetacher.Detach(save);
		for (int i = 0; i < known.Count; ++i) {
			Assert.Same(known[i], save.Players[i].knownTileIndices);
		}
		Assert.Equal(expected, save.ToCompactJSON());
	}
}

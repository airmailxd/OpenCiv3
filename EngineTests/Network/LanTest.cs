using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

public class LanTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public LanTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	// Map generation is slow, so share one two-human save across the tests.
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

	private static Player[] Humans(C7GameData.GameData gameData) {
		return gameData.players.Where(p => p.isHuman).ToArray();
	}

	private static T RoundTrip<T>(T msg) where T : MessageToEngine {
		return Assert.IsType<T>(NetSerialization.DeserializeMessageToEngine(NetSerialization.Serialize(msg)));
	}

	[Fact]
	public async Task MessagesToTheEngineSurviveTheNetwork() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player human = Humans(gameData)[0];
		Player other = Humans(gameData)[1];
		MapUnit unit = human.units.First();
		Tile neighbor = unit.location.neighbors.Values.First(t => t != Tile.NONE);

		MsgSetUnitPath path = RoundTrip(new MsgSetUnitPath(unit.id, new TilePath(neighbor, new Queue<Tile>([neighbor]))) { playerID = human.id });
		Assert.Equal(human.id, path.playerID);
		Assert.Equal(unit.id, path.unitID);
		Assert.Same(neighbor, path.path.destination);
		Assert.Equal([neighbor], path.path.path);

		Assert.Same(neighbor, RoundTrip(new MsgBombard(unit.id, neighbor)).tile);
		Assert.Same(unit, RoundTrip(new MsgBuildCity(unit, "Rome")).unit);
		Assert.Equal("Rome", RoundTrip(new MsgBuildCity(unit, "Rome")).name);
		Assert.Same(gameData.Terraforms[0], RoundTrip(new MsgStartWorkerJob(unit.id, gameData.Terraforms[0])).action);
		Assert.Same(gameData.governments[1], RoundTrip(new SelectGovernmentMsg(gameData.governments[1])).government);
		Assert.Equal(MsgUnitCommand.Command.Disband, RoundTrip(new MsgUnitCommand(unit.id, MsgUnitCommand.Command.Disband)).command);
		Assert.Null(RoundTrip(new MsgLoadToTransport(unit.id)).transportUnitId);

		MsgChooseResearch research = RoundTrip(new MsgChooseResearch(gameData.techs[3], MsgChooseResearch.AdvisorState.Show, MsgChooseResearch.SelectionMode.Multi));
		Assert.Same(gameData.techs[3], research.tech);
		Assert.Equal(MsgChooseResearch.SelectionMode.Multi, research.selectionMode);

		TradeOffer gives = new() { gold = 25, techs = [gameData.techs[0]] };
		MsgProposeDeal deal = RoundTrip(new MsgProposeDeal(other, gives, new TradeOffer()));
		Assert.Same(other, deal.opponent);
		Assert.Equal(25, deal.senderGives.gold);
		Assert.Same(gameData.techs[0], Assert.Single(deal.senderGives.techs));
		Assert.Null(deal.senderWants.gold);
	}

	[Fact]
	public async Task MessagesToTheUiSurviveTheNetwork() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player[] humans = Humans(gameData);
		Player ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians);

		MessageToUI startTurn = NetSerialization.DeserializeMessageToUI(NetSerialization.Serialize(new MsgStartTurn(humans[1])));
		Assert.Same(humans[1], Assert.IsType<MsgStartTurn>(startTurn).player);

		TradeOffer wants = new() { techs = [gameData.techs[1]] };
		MessageToUI offer = NetSerialization.DeserializeMessageToUI(NetSerialization.Serialize(new MsgShowTradeOffer(ai, humans[1], wants, new TradeOffer { gold = 10 })));
		MsgShowTradeOffer tradeOffer = Assert.IsType<MsgShowTradeOffer>(offer);
		Assert.Same(ai, tradeOffer.aiPlayer);
		Assert.Same(humans[1], tradeOffer.humanPlayer);
		Assert.Same(gameData.techs[1], Assert.Single(tradeOffer.aiWant.techs));
		Assert.Equal(10, tradeOffer.aiGive.gold);

		MessageToUI civDestroyed = NetSerialization.DeserializeMessageToUI(NetSerialization.Serialize(new MsgCivilizationDestroyed(ai.civilization)));
		Assert.Same(ai.civilization, Assert.IsType<MsgCivilizationDestroyed>(civDestroyed).civilization);

		MessageToUI victory = NetSerialization.DeserializeMessageToUI(NetSerialization.Serialize(new MsgVictory(humans[0], gameData.victories[0])));
		Assert.Same(gameData.victories[0], Assert.IsType<MsgVictory>(victory).victory);
	}

	// A message that can't be rebuilt from JSON would silently never reach a
	// LAN host, so check every message keeps what it needs in public fields.
	[Fact]
	public void EveryMessageCanBeRebuiltFromItsFields() {
		Type[] messageTypes = typeof(MessageToEngine).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && (t.IsSubclassOf(typeof(MessageToEngine)) || t.IsSubclassOf(typeof(MessageToUI))))
			.ToArray();
		Assert.NotEmpty(messageTypes);

		foreach (Type type in messageTypes) {
			ConstructorInfo[] constructors = type.GetConstructors();
			Assert.True(constructors.Length == 1, $"{type.Name} needs exactly one public constructor to be deserialized");

			HashSet<string> members = type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name)
				.Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).Select(p => p.Name))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (ParameterInfo parameter in constructors[0].GetParameters()) {
				Assert.True(members.Contains(parameter.Name), $"{type.Name}'s constructor parameter {parameter.Name} has no public field to be read from");
			}

			foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)) {
				Assert.False(typeof(Delegate).IsAssignableFrom(field.FieldType), $"{type.Name} holds code ({field.Name}), which can't be sent over the network");
			}
		}
	}

	[Fact]
	public async Task OnlyThePlayerWhoseTurnItIsCanAct() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player[] humans = Humans(gameData);
		Assert.Equal(humans[0].id, EngineStorage.activePlayerID);

		// The second human can't end the first human's turn, or order their units.
		MapUnit firstHumansUnit = humans[0].units.First(u => !u.isFortified);
		new MsgSetFortification(firstHumansUnit.id, true) { playerID = humans[1].id }.send();
		new MsgEndTurn { playerID = humans[1].id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		EngineStorage.ProcessNextMessageToEngine();
		Assert.False(firstHumansUnit.isFortified);
		Assert.False(humans[0].hasPlayedThisTurn);
		Assert.Equal(humans[0].id, EngineStorage.activePlayerID);

		// Nor order their own units out of turn.
		MapUnit secondHumansUnit = humans[1].units.First(u => !u.isFortified);
		new MsgSetFortification(secondHumansUnit.id, true) { playerID = humans[1].id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		Assert.False(secondHumansUnit.isFortified);

		// The first human can order their own units.
		new MsgSetFortification(firstHumansUnit.id, true) { playerID = humans[0].id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		Assert.True(firstHumansUnit.isFortified);
	}

	[Fact]
	public async Task HumansCanTradeWithEachOther() {
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player[] humans = Humans(gameData);
		humans[0].gold = 100;
		humans[1].gold = 0;

		new MsgProposeDeal(humans[1], new TradeOffer { gold = 40 }, new TradeOffer()) { playerID = humans[0].id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		MsgShowDealProposal proposal = Assert.IsType<MsgShowDealProposal>(Assert.Single(EngineStorage.messagesToUI));
		EngineStorage.messagesToUI.Clear();
		Assert.Same(humans[1], proposal.recipient);

		// Only the player asked can answer.
		new MsgRespondToDeal(true) { playerID = humans[0].id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		Assert.Equal(100, humans[0].gold);

		new MsgRespondToDeal(true) { playerID = humans[1].id }.send();
		EngineStorage.ProcessNextMessageToEngine();
		Assert.Equal(60, humans[0].gold);
		Assert.Equal(40, humans[1].gold);
		MsgDealResult result = Assert.IsType<MsgDealResult>(Assert.Single(EngineStorage.messagesToUI));
		Assert.True(result.accepted);
		Assert.Same(humans[0], result.recipient);
	}

	// Runs the host, its engine and a client until the condition holds.
	private static void PumpUntil(LanHost host, LanClient client, Func<bool> condition, List<MessageToUI> hostUi = null) {
		for (int i = 0; i < 2000; ++i) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
				hostUi?.Add(msg);
			}
			client.Poll();
			if (condition()) {
				return;
			}
			Thread.Sleep(5);
		}
		throw new TimeoutException("The LAN game never got there");
	}

	[Fact]
	public async Task ClientsJoinAndTakeTurnsThroughTheHost() {
		SaveGame save = twoHumanSave.Value.Clone();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		SeatInfo seat = Assert.Single(host.Seats);

		using LanClient client = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, client, () => client.Lobby != null);
		Assert.Equal(2, client.Lobby.seats.Count);
		Assert.True(client.Lobby.seats[0].isHost);
		Assert.Null(client.Lobby.yourSeat);

		client.ClaimSeat(seat.playerID);
		PumpUntil(host, client, () => client.Lobby.yourSeat == seat.playerID);
		Assert.True(host.AllSeatsTaken);
		Assert.Equal("Guest", host.Seats[0].takenBy);

		// The host loads the game and starts it; the client gets the game.
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player[] humans = Humans(gameData);
		host.StartGame();
		Assert.Equal(humans[0].id, EngineStorage.uiControllerID);
		PumpUntil(host, client, () => client.StartingGame != null);
		Assert.Equal(humans[1].id, client.PlayerID);
		Assert.Equal(gameData.turn, client.StartingGame.TurnNumber);

		List<SaveGame> snapshots = [];
		List<MessageToUI> clientUi = [];
		client.SnapshotReceived = snapshots.Add;
		client.UiMessageReceived = json => clientUi.Add(NetSerialization.DeserializeMessageToUI(json));

		// The client can't end the host's turn, even pretending to be them.
		long processedBefore = EngineStorage.processedMessageCount;
		client.SendCommand(new MsgEndTurn { playerID = humans[0].id });
		PumpUntil(host, client, () => EngineStorage.processedMessageCount > processedBefore);
		Assert.False(humans[0].hasPlayedThisTurn);

		// When the host ends their turn, the client hears it's theirs.
		List<MessageToUI> hostUi = [];
		new MsgEndTurn().send();
		PumpUntil(host, client, () => clientUi.OfType<MsgStartTurn>().Any(), hostUi);
		Assert.Same(humans[1], clientUi.OfType<MsgStartTurn>().Single().player);
		Assert.Same(humans[1], hostUi.OfType<MsgStartTurn>().Single().player);
		Assert.Equal(humans[1].id, EngineStorage.activePlayerID);
		Assert.Equal(humans[0].id, EngineStorage.uiControllerID);
		Assert.True(snapshots.Last().Players.Single(p => p.id == humans[0].id).hasPlayedCurrentTurn);

		// The client fortifies a unit, and sees it in the next snapshot.
		MapUnit unit = humans[1].units.First();
		bool fortify = !unit.isFortified;
		snapshots.Clear();
		client.SendCommand(new MsgSetFortification(unit.id, fortify));
		PumpUntil(host, client, () => snapshots.Any(s => (s.Units.Single(u => u.id == unit.id).action == "fortified") == fortify));
		Assert.Equal(fortify, unit.isFortified);

		// The client can't claim the host agreed to a deal: the host is asked.
		humans[0].gold = 50;
		humans[1].gold = 0;
		hostUi.Clear();
		client.SendCommand(new MsgProposeDeal(humans[0], new TradeOffer(), new TradeOffer { gold = 50 }) { opponentAgreed = true });
		PumpUntil(host, client, () => hostUi.OfType<MsgShowDealProposal>().Any(), hostUi);
		Assert.Equal(50, humans[0].gold);
		new MsgRespondToDeal(false).send();
		PumpUntil(host, client, () => clientUi.OfType<MsgDealResult>().Any(), hostUi);
		Assert.False(clientUi.OfType<MsgDealResult>().Single().accepted);
		Assert.Equal(50, humans[0].gold);

		// The client ends their turn: the AIs play and the host is up again.
		clientUi.Clear();
		hostUi.Clear();
		client.SendCommand(new MsgEndTurn());
		PumpUntil(host, client, () => hostUi.OfType<MsgStartTurn>().Any(), hostUi);
		Assert.Same(humans[0], hostUi.OfType<MsgStartTurn>().Single().player);
		Assert.Equal(1, gameData.turn);
		PumpUntil(host, client, () => clientUi.OfType<MsgStartTurn>().Any());
		Assert.Equal(1, snapshots.Last().TurnNumber);
	}

	[Fact]
	public async Task PlayersCanRejoinAGameInProgress() {
		SaveGame save = twoHumanSave.Value.Clone();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;

		LanClient first = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, first, () => first.Lobby != null);
		first.ClaimSeat(seatID);
		PumpUntil(host, first, () => first.Lobby.yourSeat == seatID);

		await CreateTwoHumanGame();
		host.StartGame();
		PumpUntil(host, first, () => first.StartingGame != null);

		first.Dispose();
		PumpUntil(host, first, () => host.Seats[0].takenBy == null);
		Assert.False(host.AllSeatsTaken);

		using LanClient second = LanClient.Connect("127.0.0.1", host.Port, "Guest again");
		PumpUntil(host, second, () => second.Lobby != null);
		second.ClaimSeat(seatID);
		PumpUntil(host, second, () => second.StartingGame != null);
		Assert.Equal(seatID, second.PlayerID);
		Assert.True(host.AllSeatsTaken);
	}

	[Fact]
	public void HostsTurnAwayOtherVersions() {
		using LanHost host = new("Host", twoHumanSave.Value.Clone(), port: 0, answerDiscovery: false);
		using System.Net.Sockets.TcpClient tcp = new("127.0.0.1", host.Port);
		using LanConnection connection = new(tcp);
		connection.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version + 1, "Time traveller"));

		Frame frame = null;
		for (int i = 0; i < 400 && frame == null; ++i) {
			host.Poll();
			connection.TryReceive(out frame);
			Thread.Sleep(5);
		}
		Assert.Equal(FrameKind.Rejected, frame?.kind);
	}
}

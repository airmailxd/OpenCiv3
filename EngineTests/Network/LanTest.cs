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
	public async Task SpectatorsWatchWithoutPlaying() {
		SaveGame save = twoHumanSave.Value.Clone();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;

		using LanClient player = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, player, () => player.Lobby != null);
		player.ClaimSeat(seatID);
		PumpUntil(host, player, () => player.Lobby.yourSeat == seatID);

		// Watching doesn't take a seat, and everyone in the lobby sees it.
		using LanClient spectator = LanClient.Connect("127.0.0.1", host.Port, "Watcher");
		spectator.Watch();
		PumpUntil(host, spectator, () => spectator.Lobby?.spectators?.Count == 1);
		Assert.Equal(["Watcher"], host.Spectators);
		Assert.True(host.AllSeatsTaken);
		Assert.Null(spectator.Lobby.yourSeat);
		PumpUntil(host, player, () => player.Lobby.spectators?.Count == 1);

		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, spectator, () => spectator.StartingGame != null);
		Assert.Null(spectator.PlayerID);
		Assert.True(spectator.IsSpectator);
		PumpUntil(host, player, () => player.StartingGame != null);

		List<SaveGame> snapshots = [];
		List<MessageToUI> spectatorUi = [];
		spectator.SnapshotReceived = snapshots.Add;
		spectator.UiMessageReceived = json => spectatorUi.Add(NetSerialization.DeserializeMessageToUI(json));
		player.SnapshotReceived = _ => { };
		player.UiMessageReceived = _ => { };

		// The spectator hears whose turn it is, and sees the game change.
		new MsgEndTurn().send();
		PumpUntil(host, spectator, () => spectatorUi.OfType<MsgStartTurn>().Any());
		Assert.Same(humans[1], spectatorUi.OfType<MsgStartTurn>().Single().player);
		Assert.True(snapshots.Last().Players.Single(p => p.id == humans[0].id).hasPlayedCurrentTurn);

		// A spectator's client sends nothing, and the host ignores a
		// spectator that sends commands anyway.
		using System.Net.Sockets.TcpClient tcp = new("127.0.0.1", host.Port);
		using LanConnection rogue = new(tcp);
		rogue.Send(FrameKind.Hello, new HelloInfo(LanProtocol.Version, "Rogue"));
		rogue.Send(FrameKind.Watch, []);
		spectator.SendCommand(new MsgEndTurn { playerID = humans[1].id });
		rogue.Send(FrameKind.Command, NetSerialization.Serialize(new MsgEndTurn { playerID = humans[1].id }));
		PumpUntil(host, spectator, () => host.Spectators.Count == 2);
		for (int i = 0; i < 100; ++i) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			Thread.Sleep(2);
		}
		Assert.False(humans[1].hasPlayedThisTurn);

		// Leaving stops the watching.
		spectator.Dispose();
		PumpUntil(host, player, () => host.Spectators.Count == 1);
	}

	[Fact]
	public async Task TheHostEndsTurnsThatRunOutOfTime() {
		SaveGame save = twoHumanSave.Value.Clone();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		host.TurnTimeLimit = TimeSpan.FromMilliseconds(300);
		ID seatID = host.Seats[0].playerID;

		using LanClient client = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, client, () => client.Lobby != null);
		client.ClaimSeat(seatID);
		PumpUntil(host, client, () => client.Lobby.yourSeat == seatID);

		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, client, () => client.StartingGame != null);
		client.SnapshotReceived = _ => { };
		client.UiMessageReceived = _ => { };

		// The client hears whose turn it is, how long they have, and who is
		// connected.
		PumpUntil(host, client, () => client.CurrentClock() != null);
		TurnClockInfo clock = client.CurrentClock();
		Assert.Equal(humans[0].id, clock.activePlayerID);
		Assert.Equal(0.3, clock.secondsAllowed.Value, 3);
		Assert.Equal([humans[0].id, seatID], clock.connectedPlayers);

		// Nobody ends the host's turn, so the host's clock does, and then the
		// client's turn is timed.
		PumpUntil(host, client, () => client.CurrentClock().activePlayerID == seatID);
		Assert.True(humans[0].hasPlayedThisTurn);
		Assert.True(client.CurrentClock().secondsElapsed < 0.3);
	}

	[Fact]
	public void GuestsChooseTheirCivilizationsBeforeTheGameIsCreated() {
		// A new game whose map is made but whose players aren't yet.
		SaveGame save = twoHumanSave.Value.Clone();
		save.Players.Clear();
		save.Units.Clear();
		List<Civilization> playable = save.Civilizations.Where(c => !c.isBarbarian).ToList();
		Civilization hostCiv = playable[0];
		Civilization picked = playable[3];

		using LanHost host = new("Host", hostCiv.name, guestSeats: 2, save.Civilizations, port: 0, answerDiscovery: false);
		Assert.True(host.GuestsChooseCivilizations);
		Assert.Throws<InvalidOperationException>(host.StartGame);

		using LanClient ann = LanClient.Connect("127.0.0.1", host.Port, "Ann");
		using LanClient bob = LanClient.Connect("127.0.0.1", host.Port, "Bob");
		PumpUntil(host, ann, () => ann.Lobby != null);
		Assert.Equal(playable.Select(c => c.name), ann.Lobby.civilizations.Select(c => c.name));
		Assert.Equal(hostCiv.leader, ann.Lobby.civilizations[0].leader);
		ann.ClaimSeat(host.Seats[0].playerID);
		PumpUntil(host, ann, () => ann.Lobby.yourSeat != null);
		bob.ClaimSeat(host.Seats[1].playerID);
		PumpUntil(host, bob, () => bob.Lobby?.yourSeat != null);

		// The host's civilization is taken, so Ann's first choice is refused.
		ann.ChooseCivilization(hostCiv.name);
		ann.ChooseCivilization(picked.name);
		PumpUntil(host, bob, () => bob.Lobby.seats.Any(s => s.civilization == picked.name));
		Assert.Equal(picked.name, host.Seats[0].civilization);

		// Bob can't have Ann's civilization, or one that doesn't exist.
		bob.ChooseCivilization(picked.name);
		bob.ChooseCivilization("Atlantis");
		for (int i = 0; i < 50; ++i) {
			host.Poll();
			Thread.Sleep(2);
		}
		Assert.Null(host.Seats[1].civilization);

		List<HotseatPlayer> guests = host.BeginCreatingGame();
		PumpUntil(host, ann, () => ann.Lobby.creatingGame);
		Assert.Equal([picked.name, null], guests.Select(g => g.civilization?.name));
		Assert.Equal(["Ann", "Bob"], guests.Select(g => g.name));

		// Choices are closed while the game is created.
		ann.ChooseCivilization(null);
		new GameSetup {
			playerCivilization = hostCiv,
			playerName = "Host",
			hotseatPlayers = guests,
			difficulty = save.Difficulties.First(),
			worldCharacteristics = new WorldCharacteristics(save) { mapSeed = 123456 },
			opponents = [new SelectedOpponent { isRandom = true }, new SelectedOpponent { isRandom = false, Name = picked.name }],
			victoryConditions = new VictoryConditions(),
		}.Populate(save);
		host.GameCreated(save);
		PumpUntil(host, ann, () => ann.Lobby.civilizations == null && !ann.Lobby.creatingGame);

		// Each seat has its player now: Ann's civilization as chosen, Bob a
		// random one nobody else has, and the AI that wanted Ann's
		// civilization another.
		SavePlayer[] humans = save.Players.Where(p => p.human).ToArray();
		Assert.Equal([hostCiv.name, picked.name], humans.Take(2).Select(p => p.civilization));
		Assert.Equal(host.Seats.Select(s => s.playerID), humans.Skip(1).Select(p => p.id));
		Assert.Equal(["Ann", "Bob"], host.Seats.Select(s => s.playerName));
		Assert.Equal(picked.name, host.Seats[0].civilization);
		Assert.Equal(save.Players.Count - 1, save.Players.Where(p => !p.isBarbarian).Select(p => p.civilization).Distinct().Count());
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

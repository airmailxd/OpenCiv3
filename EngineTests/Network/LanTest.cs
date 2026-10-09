using System;
using System.Collections.Generic;
using System.Diagnostics;
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

	private Task<C7GameData.GameData> CreateTwoHumanGame() {
		return CreateHumanGame(SaveGameFixture.TwoHumanSave());
	}

	private async Task<C7GameData.GameData> CreateHumanGame(SaveGame save) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();

		await CreateGame.createGame(save, (_) => fixture.behaviors);
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
		// Only players who have met can trade.
		humans[0].EnsureRelationshipExists(humans[1]);
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

	// Generous, since the condition never takes long to come true unless
	// something is broken, and a loaded machine can be slow.
	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	// Runs the host, its engine and a client until the condition holds.
	private static void PumpUntil(LanHost host, LanClient client, Func<bool> condition, List<MessageToUI> hostUi = null) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (true) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
				hostUi?.Add(msg);
			}
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

	// Lets the host's engine handle everything it has been sent so far.
	private static void ProcessEngineMessages() {
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.ProcessNextMessageToEngine();
		}
	}

	[Fact]
	public async Task ClientsJoinAndTakeTurnsThroughTheHost() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		SeatInfo seat = Assert.Single(host.Seats);

		using LanClient client = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, client, () => client.Lobby != null);
		Assert.Equal(2, client.Lobby.seats.Count);
		Assert.True(client.Lobby.seats[0].isHost);
		Assert.Empty(client.YourSeats);

		client.ClaimSeat(seat.playerID);
		PumpUntil(host, client, () => client.YourSeats.Contains(seat.playerID));
		Assert.True(host.AllSeatsTaken);
		Assert.Equal("Guest", host.Seats[0].takenBy);

		// The host loads the game and starts it; the client gets the game.
		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player[] humans = Humans(gameData);
		host.StartGame();
		Assert.Equal(humans[0].id, EngineStorage.uiControllerID);
		PumpUntil(host, client, () => client.StartingGame != null);
		Assert.Equal([humans[1].id], client.PlayerIDs);
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
		humans[0].EnsureRelationshipExists(humans[1]);
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
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;

		LanClient first = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, first, () => first.Lobby != null);
		first.ClaimSeat(seatID);
		PumpUntil(host, first, () => first.YourSeats.Contains(seatID));

		await CreateTwoHumanGame();
		host.StartGame();
		PumpUntil(host, first, () => first.StartingGame != null);

		// The seat is held for the guest who left, until the host gives it
		// up.
		first.Dispose();
		PumpUntil(host, first, () => host.Seats[0].disconnected);
		Assert.False(host.AllSeatsTaken);
		Assert.True(host.ReleaseSeat(seatID));
		Assert.Null(host.Seats[0].takenBy);

		using LanClient second = LanClient.Connect("127.0.0.1", host.Port, "Guest again");
		PumpUntil(host, second, () => second.Lobby != null);
		second.ClaimSeat(seatID);
		PumpUntil(host, second, () => second.StartingGame != null);
		Assert.Equal([seatID], second.PlayerIDs);
		Assert.True(host.AllSeatsTaken);
	}

	[Fact]
	public async Task AnAiStopsWaitingOnAGuestWhoLeaves() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;

		LanClient guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, guest, () => guest.Lobby != null);
		guest.ClaimSeat(seatID);
		PumpUntil(host, guest, () => guest.YourSeats.Contains(seatID));

		await CreateTwoHumanGame();
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		Assert.True(EngineStorage.IsPlayerReachable(seatID));

		// An AI asks the guest something, and the guest leaves.
		EngineStorage.diplomacyPlayerID = seatID;
		Task answered = EngineStorage.WaitForDiplomacyCompleted(seatID);
		guest.Dispose();
		PumpUntil(host, guest, () => answered.IsCompleted);
		EngineStorage.diplomacyPlayerID = null;

		// Nobody asks the empty seat anything more.
		Assert.False(EngineStorage.IsPlayerReachable(seatID));
		Assert.True(EngineStorage.IsPlayerReachable(EngineStorage.uiControllerID));
	}

	[Fact]
	public async Task SpectatorsWatchWithoutPlaying() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		ID seatID = host.Seats[0].playerID;

		using LanClient player = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, player, () => player.Lobby != null);
		player.ClaimSeat(seatID);
		PumpUntil(host, player, () => player.YourSeats.Contains(seatID));

		// Watching doesn't take a seat, and everyone in the lobby sees it.
		using LanClient spectator = LanClient.Connect("127.0.0.1", host.Port, "Watcher");
		spectator.Watch();
		PumpUntil(host, spectator, () => spectator.Lobby?.spectators?.Count == 1);
		Assert.Equal(["Watcher"], host.Spectators);
		Assert.True(host.AllSeatsTaken);
		Assert.Empty(spectator.YourSeats);
		PumpUntil(host, player, () => player.Lobby.spectators?.Count == 1);

		C7GameData.GameData gameData = await CreateTwoHumanGame();
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, spectator, () => spectator.StartingGame != null);
		Assert.Empty(spectator.PlayerIDs);
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

		// The rogue leaves straight after its command. A connection's frames
		// arrive in order, so once the host has seen it leave, it has read the
		// command too.
		rogue.Dispose();
		PumpUntil(host, spectator, () => host.Spectators.Count == 1);
		Assert.Equal(["Watcher"], host.Spectators);
		ProcessEngineMessages();
		Assert.False(humans[1].hasPlayedThisTurn);

		// Leaving stops the watching.
		spectator.Dispose();
		PumpUntil(host, player, () => host.Spectators.Count == 0);
		ProcessEngineMessages();
		Assert.False(humans[1].hasPlayedThisTurn);
	}

	[Fact]
	public async Task TheHostEndsTurnsThatRunOutOfTime() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		// Long enough that no turn runs out while the game is being set up,
		// however slow the machine.
		TimeSpan longLimit = TimeSpan.FromHours(1);
		host.TurnTimeLimit = longLimit;
		ID seatID = host.Seats[0].playerID;

		using LanClient client = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, client, () => client.Lobby != null);
		client.ClaimSeat(seatID);
		PumpUntil(host, client, () => client.YourSeats.Contains(seatID));

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
		Assert.Equal(longLimit.TotalSeconds, clock.secondsAllowed.Value, 3);
		Assert.Equal([humans[0].id, seatID], clock.connectedPlayers);
		Assert.False(humans[0].hasPlayedThisTurn);

		// Nobody ends the host's turn, so once its time is short the host's
		// clock does. The pump checks for the end of the turn straight after
		// the engine handles it, before the host polls again, so the time
		// can go back up before the client's turn is timed.
		host.TurnTimeLimit = TimeSpan.FromMilliseconds(50);
		PumpUntil(host, client, () => EngineStorage.activePlayerID == seatID);
		host.TurnTimeLimit = longLimit;
		Assert.True(humans[0].hasPlayedThisTurn);

		// Then the client's turn is timed.
		PumpUntil(host, client, () => client.CurrentClock().activePlayerID == seatID);
		clock = client.CurrentClock();
		Assert.Equal(longLimit.TotalSeconds, clock.secondsAllowed.Value, 3);
		Assert.True(clock.secondsElapsed < longLimit.TotalSeconds);
		Assert.False(humans[1].hasPlayedThisTurn);
	}

	[Fact]
	public void GuestsChooseTheirCivilizationsBeforeTheGameIsCreated() {
		// A new game whose map is made but whose players aren't yet.
		SaveGame save = SaveGameFixture.TwoHumanSave();
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
		PumpUntil(host, ann, () => ann.YourSeats.Count > 0);
		bob.ClaimSeat(host.Seats[1].playerID);
		PumpUntil(host, bob, () => bob.YourSeats.Count > 0);

		// The host's civilization is taken, so Ann's first choice is refused.
		ann.ChooseCivilization(hostCiv.name);
		ann.ChooseCivilization(picked.name);
		PumpUntil(host, bob, () => bob.Lobby.seats.Any(s => s.civilization == picked.name));
		Assert.Equal(picked.name, host.Seats[0].civilization);

		// Bob can't have Ann's civilization, or one that doesn't exist. The
		// host answers every choice, so record Bob's seat each time, and end
		// with a choice that's allowed: once it's taken, the two before it
		// have been refused.
		List<string> bobsChoices = [];
		host.LobbyChanged += () => bobsChoices.Add(host.Seats[1].civilization);
		Civilization allowed = playable[5];
		bob.ChooseCivilization(picked.name);
		bob.ChooseCivilization("Atlantis");
		bob.ChooseCivilization(allowed.name);
		PumpUntil(host, bob, () => host.Seats[1].civilization == allowed.name);
		Assert.Equal([null, null, allowed.name], bobsChoices);

		// And can go back to a random one.
		bob.ChooseCivilization(null);
		PumpUntil(host, bob, () => host.Seats[1].civilization == null);

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
		using LanHost host = new("Host", SaveGameFixture.TwoHumanSave(), port: 0, answerDiscovery: false);
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

	[Fact]
	public void AGuestCanTakeSeveralSeatsForPlayersAtTheirMachine() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		save.Players.Clear();
		save.Units.Clear();
		List<Civilization> playable = save.Civilizations.Where(c => !c.isBarbarian).ToList();
		using LanHost host = new("Host", playable[0].name, guestSeats: 2, save.Civilizations, port: 0, answerDiscovery: false);
		ID first = host.Seats[0].playerID;
		ID second = host.Seats[1].playerID;

		using LanClient ann = LanClient.Connect("127.0.0.1", host.Port, "Ann");
		PumpUntil(host, ann, () => ann.Lobby != null);
		ann.ClaimSeat(first);
		ann.ClaimSeat(second, "Cid");
		PumpUntil(host, ann, () => ann.YourSeats.Count == 2);
		Assert.Equal([first, second], ann.YourSeats);
		Assert.True(host.AllSeatsTaken);
		Assert.Equal(["Ann", "Cid"], host.Seats.Select(s => s.takenBy));

		// Each seat chooses its own civilization.
		ann.ChooseCivilization(playable[3].name, second);
		PumpUntil(host, ann, () => host.Seats[1].civilization == playable[3].name);
		Assert.Null(host.Seats[0].civilization);

		// A seat can be given back before the game starts, and someone else
		// can take it.
		ann.LeaveSeat(first);
		PumpUntil(host, ann, () => ann.YourSeats.Count == 1);
		Assert.Null(host.Seats[0].takenBy);
		using LanClient bob = LanClient.Connect("127.0.0.1", host.Port, "Bob");
		PumpUntil(host, bob, () => bob.Lobby != null);
		bob.ClaimSeat(first);
		PumpUntil(host, bob, () => bob.YourSeats.Contains(first));

		// Bob can't take Ann's seat: the host answers with the lobby as it was.
		int lobbies = 0;
		bob.LobbyChanged += () => ++lobbies;
		bob.ClaimSeat(second);
		PumpUntil(host, bob, () => lobbies > 0);
		Assert.Equal("Cid", host.Seats[1].takenBy);
		Assert.Equal([first], bob.YourSeats);

		Assert.Equal(["Bob", "Cid"], host.BeginCreatingGame().Select(g => g.name));
	}

	[Fact]
	public async Task AGuestWithSeveralSeatsPlaysEachOfThem() {
		SaveGame save = SaveGameFixture.ThreeHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false);
		Assert.Equal(2, host.Seats.Count);

		using LanClient guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, guest, () => guest.Lobby != null);
		foreach (SeatInfo seat in host.Seats) {
			guest.ClaimSeat(seat.playerID);
		}
		PumpUntil(host, guest, () => guest.YourSeats.Count == 2);
		Assert.True(host.AllSeatsTaken);

		C7GameData.GameData gameData = await CreateHumanGame(save);
		Player[] humans = Humans(gameData);
		host.StartGame();
		PumpUntil(host, guest, () => guest.StartingGame != null);
		Assert.Equal([humans[1].id, humans[2].id], guest.PlayerIDs);

		List<MessageToUI> guestUi = [];
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = json => guestUi.Add(NetSerialization.DeserializeMessageToUI(json));

		// The guest can't act as the host's player.
		guest.SendCommand(new MsgEndTurn { playerID = humans[0].id });
		new MsgEndTurn().send();
		PumpUntil(host, guest, () => guestUi.OfType<MsgStartTurn>().Any());
		Assert.Same(humans[1], guestUi.OfType<MsgStartTurn>().Single().player);

		// Each of the guest's players takes their turn.
		guestUi.Clear();
		guest.SendCommand(new MsgEndTurn { playerID = humans[1].id });
		PumpUntil(host, guest, () => guestUi.OfType<MsgStartTurn>().Any());
		Assert.Same(humans[2], guestUi.OfType<MsgStartTurn>().Single().player);
		Assert.Equal(humans[2].id, EngineStorage.activePlayerID);

		// Both play at the guest's machine, so one can agree to the other's
		// deal there, but not to a deal on the host's behalf.
		humans[2].EnsureRelationshipExists(humans[0]);
		humans[2].EnsureRelationshipExists(humans[1]);
		humans[0].gold = 50;
		humans[1].gold = 50;
		humans[2].gold = 0;
		guestUi.Clear();
		guest.SendCommand(new MsgProposeDeal(humans[1], new TradeOffer(), new TradeOffer { gold = 30 }) { playerID = humans[2].id, opponentAgreed = true });
		PumpUntil(host, guest, () => guestUi.OfType<MsgDealResult>().Any());
		Assert.True(guestUi.OfType<MsgDealResult>().Single().accepted);
		Assert.Equal(20, humans[1].gold);
		Assert.Equal(30, humans[2].gold);

		List<MessageToUI> hostUi = [];
		guest.SendCommand(new MsgProposeDeal(humans[0], new TradeOffer(), new TradeOffer { gold = 50 }) { playerID = humans[2].id, opponentAgreed = true });
		PumpUntil(host, guest, () => hostUi.OfType<MsgShowDealProposal>().Any(), hostUi);
		Assert.Equal(50, humans[0].gold);
		new MsgRespondToDeal(false).send();
		PumpUntil(host, guest, () => !EngineStorage.HasPendingMessagesToEngine(), hostUi);

		// Leaving holds both seats for them.
		guest.Dispose();
		PumpUntil(host, guest, () => host.Seats.All(s => s.disconnected));
		Assert.All(host.Seats, s => Assert.Equal("Guest", s.takenBy));
		Assert.False(EngineStorage.IsPlayerReachable(humans[1].id));
		Assert.False(EngineStorage.IsPlayerReachable(humans[2].id));
	}

	// Joining an address where nothing answers gives up after the timeout,
	// without holding up the caller (the lobby) meanwhile.
	[Fact]
	public async Task JoiningAHostThatDoesntAnswerGivesUpInTheBackground() {
		// 192.0.2.1 is in TEST-NET-1 (RFC 5737), which is never assigned to a
		// host, so connecting either times out or fails at once.
		LanAddressEndpoint nowhere = new("192.0.2.1", LanProtocol.DefaultPort);
		TimeSpan timeout = TimeSpan.FromSeconds(3);
		Stopwatch waited = Stopwatch.StartNew();
		Task<LanClient> joining = LanClient.ConnectAsync(nowhere, "Guest", timeout: timeout);
		// Returning before the timeout could have run out shows it didn't
		// wait for it.
		Assert.True(waited.Elapsed < timeout, $"ConnectAsync took {waited.Elapsed} to return");
		Exception e = await Record.ExceptionAsync(() => joining);
		Assert.True(e is TimeoutException or System.Net.Sockets.SocketException, e?.ToString());
		// Generous, for a loaded CI machine: it must give up, not hang.
		Assert.True(waited.Elapsed < timeout + TimeSpan.FromSeconds(27), $"Giving up took {waited.Elapsed}");

		// And joining can be called off.
		using CancellationTokenSource cancel = new();
		joining = LanClient.ConnectAsync(nowhere, "Guest", timeout: TimeSpan.FromSeconds(30), cancel: cancel.Token);
		cancel.Cancel();
		e = await Record.ExceptionAsync(() => joining);
		Assert.True(e is OperationCanceledException or System.Net.Sockets.SocketException, e?.ToString());
	}
}

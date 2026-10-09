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

// Which of the game's news spectators hear, by how they watch.
public class SpectatorNewsTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData = new();
	private readonly Player rome = MakeCiv("player-1", human: true);
	private readonly Player greece = MakeCiv("player-2", human: true);
	private readonly Player egypt = MakeCiv("player-3", human: false);

	private static readonly SpectatorViewInfo AllCivs = new(SpectatorViewMode.AllCivs);
	private static readonly SpectatorViewInfo Omniscient = new(SpectatorViewMode.Omniscient);
	private SpectatorViewInfo As(Player player) => new(SpectatorViewMode.OneCiv, player.id);

	public SpectatorNewsTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.players.AddRange([rome, greece, egypt]);
		foreach (Player p in gameData.players) {
			foreach (Player other in gameData.players) {
				p.EnsureRelationshipExists(other);
			}
		}
		EngineStorage.uiControllerID = rome.id;
	}

	public void Dispose() {
		EngineStorage.messagesToUI.Clear();
		EngineStorage.ResetNetworking();
	}

	private static Player MakeCiv(string id, bool human) {
		return new Player() {
			id = ID.FromString(id),
			civilization = new Civilization { noun = id },
			government = new Government(),
			isHuman = human,
		};
	}

	private bool Hears(SpectatorViewInfo view, MessageToUI msg) => LanHost.SpectatorHears(view, msg, gameData);

	[Fact]
	public void OneCivHearsOnlyItsOwnNews() {
		MsgShowMilitaryAdvisorPopup news = new(greece, "We have captured Thebes!", happy: true);

		Assert.True(Hears(As(greece), news));
		Assert.False(Hears(As(rome), news));
		Assert.False(Hears(As(egypt), news));
		Assert.True(Hears(AllCivs, news));
		Assert.True(Hears(Omniscient, news));
		Assert.Equal("player-2: We have captured Thebes!", news.SpectatorHeadline());

		// Watching as the civ, it reads as the civ's own news.
		Assert.Equal("We have captured Thebes!", news.SpectatorHeadline(greece.id));
		Assert.Equal("player-2: We have captured Thebes!", news.SpectatorHeadline(rome.id));
		Assert.Equal("player-2: We have captured Thebes!", news.SpectatorHeadline((ID)null));
	}

	[Fact]
	public void NewsWithoutARecipientIsForWhoeverIsAtTheHostsScreen() {
		MsgShowTemporaryPopup news = new("Artillery bombardment failed.", null);

		Assert.True(Hears(As(rome), news));
		Assert.False(Hears(As(greece), news));
		Assert.True(Hears(AllCivs, news));
	}

	[Fact]
	public void WhatsForEveryPlayerIsForEverySpectator() {
		foreach (MessageToUI msg in new MessageToUI[] { new MsgCivilizationDestroyed(egypt.civilization), new MsgStartTurn(rome) }) {
			Assert.True(Hears(As(greece), msg));
			Assert.True(Hears(AllCivs, msg));
			Assert.True(Hears(Omniscient, msg));
		}
	}

	[Fact]
	public void ASpectatorHearsOfAWarIfTheCivItWatchesAsWould() {
		MsgWarDeclaration copy = new(egypt, greece) { forSpectators = true };
		// The civ that declared it isn't told, as its player wouldn't be.
		Assert.False(Hears(As(egypt), copy));
		Assert.True(Hears(As(greece), copy));
		Assert.False(Hears(As(rome), copy));
		Assert.True(Hears(AllCivs, copy));
		Assert.True(Hears(Omniscient, copy));

		// An embassy brings the news, as it does to a player.
		rome.playerRelationships[egypt.id].hasEmbassy = true;
		Assert.True(Hears(As(rome), copy));

		// The players' own copies would tell it twice.
		MsgWarDeclaration players = new(egypt, greece) { recipient = greece };
		Assert.False(Hears(As(greece), players));
		Assert.False(Hears(Omniscient, players));
	}

	[Fact]
	public void EveryCivHearsOfAWonder() {
		MsgWonderCompleted copy = new(null, egypt, "The Pyramids", "Thebes") { forSpectators = true };
		Assert.True(copy.IsForSpectatorsOnly);
		Assert.True(Hears(As(rome), copy));
		Assert.True(Hears(As(egypt), copy));
		Assert.True(Hears(AllCivs, copy));
		Assert.Equal("The player-3 have completed The Pyramids in Thebes!", copy.SpectatorHeadline());

		MsgWonderCompleted players = new(rome, egypt, "The Pyramids", "Thebes");
		Assert.False(Hears(As(rome), players));
		Assert.False(Hears(Omniscient, players));
	}

	// The headline of a captured city is for spectators watching every
	// civ; one watching either side hears that side's own news of it.
	[Fact]
	public void ACapturedCitysHeadlineIsForSpectatorsWatchingEveryCiv() {
		City thebes = new(Tile.NONE, greece, "Thebes", ID.None(""));
		MsgCityCaptured news = new(thebes, egypt);

		Assert.False(Hears(As(greece), news));
		Assert.False(Hears(As(egypt), news));
		Assert.False(Hears(As(rome), news));
		Assert.True(Hears(AllCivs, news));
		Assert.True(Hears(Omniscient, news));
		Assert.Equal("The player-2 have taken Thebes from the player-3", news.SpectatorHeadline());
	}

	private static List<MessageToUI> SentToUI() {
		List<MessageToUI> sent = [];
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			sent.Add(msg);
		}
		return sent;
	}

	// What a spectator watching this way hears of the messages, as the host
	// passes them on one by one.
	private static List<MessageToUI> Heard(SpectatorViewInfo view, IEnumerable<MessageToUI> messages, C7GameData.GameData game) {
		LanHost.SpectatorNews news = new();
		return messages.Where(m => news.Hears(view, m, game)).ToList();
	}

	// Each civ is told of a spaceship part its own way; a spectator
	// watching them all hears of it once, and one watching a civ hears that
	// civ's own version.
	[Fact]
	public void ASpaceshipPartIsNewsOnceForSpectatorsWatchingEveryCiv() {
		City sparta = new(Tile.NONE, greece, "Sparta", ID.None(""));
		Building engine = new(new SaveBuilding { name = "SS Engine", spaceshipPart = 0 }, gameData);
		SpaceRace.OnPartCompleted(gameData, sparta, engine);
		List<MessageToUI> sent = SentToUI();
		Assert.Equal(2, sent.Count);

		// They hear the builder's version, however the players are ordered.
		foreach (SpectatorViewInfo view in new[] { AllCivs, Omniscient }) {
			Assert.Equal([sent.Single(m => m.recipient == greece)], Heard(view, sent, gameData));
		}
		Assert.Equal([sent.Single(m => m.recipient == greece)], Heard(As(greece), sent, gameData));
		Assert.Equal([sent.Single(m => m.recipient == rome)], Heard(As(rome), sent, gameData));
		Assert.Empty(Heard(As(egypt), sent, gameData));

		// Another part is news of its own.
		LanHost.SpectatorNews news = new();
		Assert.Single(sent.Where(m => news.Hears(Omniscient, m, gameData)));
		SpaceRace.OnPartCompleted(gameData, sparta, engine);
		Assert.Single(SentToUI().Where(m => news.Hears(Omniscient, m, gameData)));
	}

	// In a game of computer players, spectators hear the space race's news
	// too: those watching every civ as the builder is told it, and one
	// watching a civ as that civ is.
	[Fact]
	public void SpectatorsHearTheSpaceRaceOfComputerPlayers() {
		rome.isHuman = greece.isHuman = false;
		EngineStorage.newsForComputerPlayers = true;
		City memphis = new(Tile.NONE, egypt, "Memphis", ID.None(""));
		Building engine = new(new SaveBuilding { name = "SS Engine", spaceshipPart = 0 }, gameData);
		SpaceRace.OnPartCompleted(gameData, memphis, engine);
		List<MessageToUI> sent = SentToUI();
		Assert.Equal(3, sent.Count);

		MessageToUI egypts = sent.Single(m => m.recipient == egypt);
		foreach (SpectatorViewInfo view in new[] { AllCivs, Omniscient }) {
			Assert.Equal([egypts], Heard(view, sent, gameData));
		}
		Assert.Equal([egypts], Heard(As(egypt), sent, gameData));
		Assert.Equal([sent.Single(m => m.recipient == rome)], Heard(As(rome), sent, gameData));
		Assert.Equal("The player-3 have added SS Engine to their ship.", sent.Single(m => m.recipient == rome).SpectatorHeadline(rome.id));

		City thebes = new(Tile.NONE, egypt, "Thebes", ID.None(""));
		Building apollo = new(new SaveBuilding { name = "Apollo Program" }, gameData);
		SpaceRace.OnApolloCompleted(gameData, thebes, apollo);
		sent = SentToUI();
		Assert.Equal(3, sent.Count);
		Assert.StartsWith("player-3: We have completed the Apollo Program!", Assert.Single(Heard(AllCivs, sent, gameData)).SpectatorHeadline());
		Assert.StartsWith("The player-3 have completed", Assert.Single(Heard(As(greece), sent, gameData)).SpectatorHeadline(greece.id));
	}

	// So is losing a spaceship.
	[Fact]
	public void ADestroyedSpaceshipIsNewsOnceForSpectatorsWatchingEveryCiv() {
		greece.spaceshipParts.Add(1);
		SpaceRace.DestroySpaceship(greece, rome);
		List<MessageToUI> sent = SentToUI();
		Assert.Equal(2, sent.Count);
		Assert.Single(Heard(AllCivs, sent, gameData));
		Assert.Single(Heard(Omniscient, sent, gameData));
		Assert.Single(Heard(As(greece), sent, gameData));
		Assert.Single(Heard(As(rome), sent, gameData));
	}

	// A captured city is told to both sides, and each side is also told it
	// by its military advisor. A spectator watching every civ hears only the
	// headline; one watching either side hears only that side's own news,
	// computer players' too while spectators watch.
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void ACapturedCityIsNewsOnceForSpectatorsWatchingEveryCiv(bool humans) {
		C7GameData.GameData game = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(game);
		EngineStorage.newsForComputerPlayers = true;
		Player[] civs = game.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		Player captor = civs[0], loser = civs[1];
		captor.isHuman = loser.isHuman = humans;
		City FoundCity(Player player, int size) {
			Tile tile = player.units.First(u => u.unitType.isSettler).location;
			City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
			while (city.residents.Count < size) {
				city.AddCitizen(new CityResident() {
					city = city,
					citizenType = city.residents[0].citizenType,
					tileWorked = city.residents[0].tileWorked,
				});
			}
			return city;
		}
		FoundCity(captor, 1);
		City city = FoundCity(loser, 3);
		SentToUI();

		CityInteractions.CaptureCity(city, captor);
		List<MessageToUI> news = SentToUI().Where(m => m.newsEvent != null).ToList();
		Assert.Equal(3, news.Count);

		foreach (SpectatorViewInfo view in new[] { AllCivs, Omniscient }) {
			MessageToUI heard = Assert.Single(Heard(view, news, game));
			Assert.IsType<MsgCityCaptured>(heard);
			Assert.Equal($"The {captor.civilization.noun} have taken {city.name} from the {loser.civilization.noun}", heard.SpectatorHeadline());
		}

		SpectatorViewInfo asCaptor = new(SpectatorViewMode.OneCiv, captor.id);
		Assert.Equal([$"We have captured {city.name}"],
			Heard(asCaptor, news, game).Select(m => m is MsgShowMilitaryAdvisorPopup p ? p.message[..p.message.IndexOf(" and")] : m.GetType().Name));
		SpectatorViewInfo asLoser = new(SpectatorViewMode.OneCiv, loser.id);
		Assert.Equal([$"{city.name} has fallen to the {captor.civilization.noun}!"],
			Heard(asLoser, news, game).Select(m => m is MsgShowMilitaryAdvisorPopup p ? p.message : m.GetType().Name));
	}

	// What asks a player to decide something, or only redraws their
	// screens, is theirs alone, however a spectator watches.
	[Fact]
	public void SpectatorsAreNeverAskedAPlayersQuestions() {
		MessageToUI[] questions = [
			new MsgShowDealProposal(rome, greece, new TradeOffer(), new TradeOffer()) { recipient = greece },
			new MsgShowTradeOffer(egypt, greece, new TradeOffer(), new TradeOffer()),
			new MsgShowTerritoryDemand(egypt, greece, 2, false),
			new MsgShowScienceSelection(greece, null),
			new MsgDisplayRazeCityPopup(greece, null),
			new MsgShowUnitedNationsVote(greece, rome, egypt),
			new MsgShowUnitedNationsElectionOffer(greece, rome, egypt),
			new MsgCityProductionCompleted(greece, null, "Warrior", "Settler"),
			new MsgDisplayAbandonCityPopup(null) { recipient = greece },
			new MsgCityCreated(null) { recipient = greece },
			new MsgShowCityScreen(null) { recipient = greece },
			new MsgCityChanged(null) { recipient = greece },
			new MsgUpdateUiAfterDomesticChange { recipient = greece },
		];
		foreach (MessageToUI question in questions) {
			Assert.False(Hears(As(greece), question), question.GetType().Name);
			Assert.False(Hears(AllCivs, question), question.GetType().Name);
			Assert.False(Hears(Omniscient, question), question.GetType().Name);
		}
	}

	[Fact]
	public void ComputerPlayersAreToldTheirNewsOnlyWhileSpectatorsWatch() {
		Assert.True(rome.IsToldNews);
		Assert.False(egypt.IsToldNews);
		EngineStorage.newsForComputerPlayers = true;
		Assert.True(egypt.IsToldNews);
		Player barbarians = new() { civilization = new Civilization { isBarbarian = true } };
		Assert.False(barbarians.IsToldNews);
		EngineStorage.ResetNetworking();
		Assert.False(egypt.IsToldNews);
	}

	// Runs the host, its engine and the clients until the condition holds.
	private static void PumpUntil(LanHost host, LanClient[] clients, Func<bool> condition, List<MessageToUI> hostUi) {
		Stopwatch pumping = Stopwatch.StartNew();
		while (true) {
			host.Poll();
			EngineStorage.ProcessNextMessageToEngine();
			while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
				hostUi.Add(msg);
			}
			foreach (LanClient client in clients) {
				client.Poll();
			}
			if (condition()) {
				return;
			}
			if (pumping.Elapsed > TimeSpan.FromSeconds(60)) {
				throw new TimeoutException("The LAN game never got there");
			}
			Thread.Sleep(5);
		}
	}

	private static List<string> Headlines(List<MessageToUI> messages) {
		return messages.Select(m => m.SpectatorHeadline()).Where(h => h != null).ToList();
	}

	[Fact]
	public async Task TheHostTellsEachSpectatorTheNewsOfHowItWatches() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) {
			AllowSpectators = true,
			SpectatorViews = SpectatorViews.Any,
		};
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		using LanClient spectator = LanClient.Connect("127.0.0.1", host.Port, "Watcher");
		LanClient[] clients = [guest, spectator];
		List<MessageToUI> hostUi = [];
		PumpUntil(host, clients, () => guest.Lobby != null && spectator.Lobby != null, hostUi);
		guest.ClaimSeat(seatID);
		spectator.Watch();
		PumpUntil(host, clients, () => guest.YourSeats.Contains(seatID) && host.Spectators.Count == 1, hostUi);

		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		EngineStorage.messagesToUI.Clear();
		C7GameData.GameData game = EngineStorage.gameData;
		Player hostPlayer = game.players.First(p => p.isHuman && p.id != seatID);
		Player guestPlayer = game.GetPlayer(seatID);
		Player computer = game.players.First(p => !p.isHuman && !p.isBarbarians);

		host.StartGame();
		PumpUntil(host, clients, () => guest.StartingGame != null && spectator.StartingGame != null, hostUi);
		List<MessageToUI> guestUi = [];
		List<MessageToUI> spectatorUi = [];
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = json => guestUi.Add(NetSerialization.DeserializeMessageToUI(json));
		spectator.SnapshotReceived = _ => { };
		spectator.UiMessageReceived = json => spectatorUi.Add(NetSerialization.DeserializeMessageToUI(json));

		// With a spectator watching, computer players are told their news.
		Assert.True(EngineStorage.newsForComputerPlayers);

		spectator.ChooseSpectatorView(new SpectatorViewInfo(SpectatorViewMode.OneCiv, seatID));
		PumpUntil(host, clients, () => spectator.SpectatorView?.mode == SpectatorViewMode.OneCiv, hostUi);
		hostUi.Clear();

		// As the guest's civ: the guest's news, but not the host's or the
		// computer's, nor the guest's questions.
		void SendNews() {
			new MsgShowMilitaryAdvisorPopup(hostPlayer, "host news", happy: true).send();
			new MsgShowMilitaryAdvisorPopup(computer, "computer news", happy: true).send();
			new MsgShowScienceSelection(guestPlayer, null).send();
			new MsgShowMilitaryAdvisorPopup(guestPlayer, "guest news", happy: true).send();
		}
		SendNews();
		PumpUntil(host, clients, () => Headlines(spectatorUi).Any(h => h.EndsWith("guest news"))
			&& guestUi.OfType<MsgShowMilitaryAdvisorPopup>().Any(), hostUi);
		Assert.Equal([$"{guestPlayer.civilization.noun}: guest news"], Headlines(spectatorUi));
		Assert.Empty(spectatorUi.OfType<MsgShowScienceSelection>());

		// The players are told as before, and the computer's news is for
		// spectators only.
		Assert.Equal(["guest news"], guestUi.OfType<MsgShowMilitaryAdvisorPopup>().Select(m => m.message));
		Assert.Single(guestUi.OfType<MsgShowScienceSelection>());
		Assert.Equal(["host news"], hostUi.OfType<MsgShowMilitaryAdvisorPopup>().Select(m => m.message));

		// Watching the whole game: everyone's news, but still no questions.
		spectator.ChooseSpectatorView(new SpectatorViewInfo(SpectatorViewMode.Omniscient));
		PumpUntil(host, clients, () => spectator.SpectatorView?.mode == SpectatorViewMode.Omniscient, hostUi);
		spectatorUi.Clear();
		SendNews();
		PumpUntil(host, clients, () => Headlines(spectatorUi).Any(h => h.EndsWith("guest news")), hostUi);
		Assert.Equal(["host news", "computer news", "guest news"],
			spectatorUi.OfType<MsgShowMilitaryAdvisorPopup>().Select(m => m.message));
		Assert.Empty(spectatorUi.OfType<MsgShowScienceSelection>());
	}
}

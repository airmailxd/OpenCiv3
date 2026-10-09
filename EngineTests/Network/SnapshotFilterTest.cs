using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.Network;

// Guests sent only what their players may know of the game.
public class SnapshotFilterTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public SnapshotFilterTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	// A two-human game with simultaneous turns, in which each human has
	// founded a city.
	private async Task<C7GameData.GameData> CreateGameWithCities(SaveGame save) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		save.SimultaneousTurns = true;
		await CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		C7GameData.GameData gameData = EngineStorage.gameData;
		foreach (Player human in Humans(gameData)) {
			MapUnit settler = human.units.First(u => u.unitType.actions.Contains(UnitAction.BuildCity));
			new MsgBuildCity(settler, $"{human.civilization.name} City") { playerID = human.id }.send();
			EngineStorage.ProcessNextMessageToEngine();
		}
		Assert.Equal(2, gameData.cities.Count);
		EngineStorage.messagesToUI.Clear();
		return gameData;
	}

	private static Player[] Humans(C7GameData.GameData gameData) {
		return gameData.players.Where(p => p.isHuman).ToArray();
	}

	private static SaveGame FilteredFor(C7GameData.GameData gameData, params Player[] players) {
		return SnapshotFilter.Filter(LanProtocol.SnapshotOf(gameData), SnapshotFilter.ViewOf(gameData, players.Select(p => p.id)));
	}

	private static byte[] Hash(SaveGame save) => SHA256.HashData(save.ToCompactJSON());

	[Fact]
	public async Task AGuestIsSentOnlyWhatItsPlayerMayKnow() {
		C7GameData.GameData gameData = await CreateGameWithCities(SaveGameFixture.TwoHumanSave());
		(Player guest, Player other) = (Humans(gameData)[1], Humans(gameData)[0]);
		Assert.False(guest.playerRelationships.ContainsKey(other.id));
		SaveGame whole = LanProtocol.SnapshotOf(gameData);
		byte[] wholeHash = Hash(whole);
		SaveGame filtered = SnapshotFilter.Filter(whole, SnapshotFilter.ViewOf(gameData, [guest.id]));
		// Filtering leaves the snapshot it's given as it was.
		Assert.Equal(wholeHash, Hash(whole));

		// All of the guest's units, and others' only where it can see them.
		Assert.Equal(guest.units.Select(u => u.id).Order(), filtered.Units.Where(u => u.owner == guest.id).Select(u => u.id).Order());
		Assert.All(filtered.Units.Where(u => u.owner != guest.id),
			u => Assert.True(guest.tileKnowledge.isActiveTile(gameData.map.tileAt(u.currentLocation.X, u.currentLocation.Y))));
		Assert.Contains(gameData.mapUnits, u => u.owner == other);
		Assert.DoesNotContain(filtered.Units, u => u.owner == other.id);

		// The other human's city is there, for the map, borders and scores,
		// but without its insides.
		City otherCity = other.cities.Single();
		SaveCity hidden = filtered.Cities.Single(c => c.id == otherCity.id);
		Assert.Equal(otherCity.name, hidden.name);
		Assert.Equal((otherCity.location.XCoordinate, otherCity.location.YCoordinate), (hidden.location.X, hidden.location.Y));
		Assert.Equal(otherCity.residents.Count, hidden.size);
		Assert.Empty(hidden.residents);
		Assert.Equal(0, hidden.foodStored);
		Assert.Equal(0, hidden.shieldsStored);
		Assert.Empty(hidden.productionQueue);
		Assert.Equal(otherCity.constructed_buildings.Count, hidden.buildings.Count);
		Assert.Equal(otherCity.perPlayerCulture.Count, hidden.perPlayerCulture.Count);
		Assert.NotNull(hidden.producible);
		// The guest's own city is whole.
		City guestCity = guest.cities.Single();
		SaveCity own = filtered.Cities.Single(c => c.id == guestCity.id);
		Assert.Equal(guestCity.residents.Count, own.residents.Count);
		Assert.Equal(guestCity.itemBeingProduced.name, own.producible);

		// Every player is there, for the scoreboard, but those the guest
		// hasn't met keep their gold, techs and research to themselves, and
		// know of the map only their own territory.
		Assert.Equal(whole.Players.Select(p => p.id), filtered.Players.Select(p => p.id));
		Assert.Equal(whole.History.Keys.Order(), filtered.History.Keys.Order());
		SavePlayer otherSave = filtered.Players.Single(p => p.id == other.id);
		Assert.Equal(other.civilization.name, otherSave.civilization);
		Assert.Equal(other.government.id, otherSave.governmentId);
		Assert.Equal(0, otherSave.gold);
		Assert.Empty(otherSave.knownTechs);
		Assert.Null(otherSave.currentlyResearchedTech);
		Assert.Empty(otherSave.researchQueue);
		Assert.Equal(0, otherSave.beakers);
		Assert.Empty(otherSave.outdatedTiles);
		HashSet<int> otherKnows = SavePlayer.DecodeTileIndices(otherSave.knownTileIndices).ToHashSet();
		Assert.True(otherKnows.Count < other.tileKnowledge.knownTiles.Count);
		Assert.All(otherKnows, i => Assert.Equal(other.id, gameData.map.tiles[i].OwningPlayer()?.id));
		Assert.Empty(otherSave.playerRelationships.Keys.Where(k => k != guest.id.ToString()));
		// The guest's own player is whole.
		SavePlayer guestSave = filtered.Players.Single(p => p.id == guest.id);
		Assert.Same(whole.Players.Single(p => p.id == guest.id), guestSave);

		// Tiles the guest has never seen keep their terrain but nothing more.
		for (int i = 0; i < gameData.map.tiles.Count; ++i) {
			Tile tile = gameData.map.tiles[i];
			SaveTile sent = filtered.Map.tiles[i];
			Assert.Equal(tile.baseTerrainType.Key, sent.baseTerrain);
			Assert.Equal(tile.overlayTerrainType.Key, sent.overlayTerrain);
			if (!guest.tileKnowledge.isTileKnown(tile)) {
				Assert.Null(sent.resource);
				Assert.Empty(sent.overlays);
				Assert.DoesNotContain("barbarianCamp", sent.features);
			} else {
				Assert.Same(whole.Map.tiles[i], sent);
			}
		}
		Assert.Contains(gameData.map.tiles, t => !guest.tileKnowledge.isTileKnown(t) && t.Resource != Resource.NONE);

		// The other human is sent a different game.
		Assert.NotEqual(Hash(filtered), Hash(FilteredFor(gameData, other)));
		// And a machine with both seats sees what either of them does.
		SaveGame both = FilteredFor(gameData, guest, other);
		Assert.Contains(both.Units, u => u.owner == other.id);
		Assert.Contains(both.Units, u => u.owner == guest.id);
		Assert.NotEmpty(both.Cities.Single(c => c.id == otherCity.id).residents);
	}

	[Fact]
	public async Task AGuestPlaysOnWhatItIsSent() {
		C7GameData.GameData gameData = await CreateGameWithCities(SaveGameFixture.TwoHumanSave());
		(Player guest, Player other) = (Humans(gameData)[1], Humans(gameData)[0]);
		SaveGame filtered = FilteredFor(gameData, guest);
		City otherCity = other.cities.Single();
		int otherCitySize = otherCity.residents.Count;
		List<(int, int)> otherTerritory = gameData.map.tiles.Where(t => t.OwningPlayer() == other)
			.Select(t => (t.XCoordinate, t.YCoordinate)).Order().ToList();

		try {
			// The guest's machine builds its game from it, as it does every
			// snapshot, and works out borders, sight and moods.
			C7GameData.GameData shown = CreateGame.ReplaceWithSnapshot(filtered, fixture.behaviors);
			EngineStorage.uiControllerID = guest.id;
			Player shownOther = shown.GetPlayer(other.id);
			City shownCity = shown.GetCity(otherCity.id);
			Assert.Equal(otherCitySize, shownCity.residents.Count);
			Assert.NotNull(shownCity.itemBeingProduced);
			Assert.Equal(otherTerritory, shown.map.tiles.Where(t => t.OwningPlayer() == shownOther)
				.Select(t => (t.XCoordinate, t.YCoordinate)).Order().ToList());
			Assert.Empty(shownOther.units);
			Assert.NotNull(shownOther.government);

			// What the UI asks of the other civs works.
			Player shownGuest = shown.GetPlayer(guest.id);
			foreach (Player p in shown.players.Where(p => !p.isBarbarians)) {
				shownGuest.CompareMilitaryStrengthTo(p);
				foreach (City c in p.cities) {
					Assert.True(c.GetCulture() >= 0);
					c.RecalculateCitizenMoods(shown);
				}
				shown.TechCostFor(shown.techs[0], p);
			}
			shown.UpdateTileOwners();

			// And so does saving it, as the player may.
			SaveGame saved = SaveGame.FromGameData(shown);
			Assert.Equal(filtered.Cities.Count, saved.Cities.Count);
		} finally {
			EngineStorage.gameData = gameData;
		}
	}

	[Fact]
	public async Task AnEmbassyShowsACivsCitiesAndResearch() {
		C7GameData.GameData gameData = await CreateGameWithCities(SaveGameFixture.TwoHumanSave());
		(Player guest, Player other) = (Humans(gameData)[1], Humans(gameData)[0]);
		guest.EnsureRelationshipExists(other);
		other.gold = 77;

		// Met: their gold and techs, for trading.
		SaveGame met = FilteredFor(gameData, guest);
		SavePlayer metOther = met.Players.Single(p => p.id == other.id);
		Assert.Equal(77, metOther.gold);
		Assert.True(other.knownTechs.SetEquals(metOther.knownTechs));
		Assert.Empty(met.Cities.Single(c => c.owner == other.id).residents);
		Assert.Contains(guest.id.ToString(), metOther.playerRelationships.Keys);

		// An embassy: their cities' insides too.
		guest.playerRelationships[other.id].hasEmbassy = true;
		SaveGame embassy = FilteredFor(gameData, guest);
		Assert.Equal(other.cities.Single().residents.Count, embassy.Cities.Single(c => c.owner == other.id).residents.Count);
		Assert.Equal(other.currentlyResearchedTech, embassy.Players.Single(p => p.id == other.id).currentlyResearchedTech);
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
				throw new TimeoutException("The LAN game never got there");
			}
			Thread.Sleep(5);
		}
	}

	private static bool Has(LanClient client, byte[] hash) {
		return client.ReceivedSnapshotHash is byte[] received && received.AsSpan().SequenceEqual(hash);
	}

	[Fact]
	public async Task EachMachineIsSentItsOwnView() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { SimultaneousTurns = true, AllowSpectators = true };
		Assert.True(host.HideUnseen);
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		using LanClient spectator = LanClient.Connect("127.0.0.1", host.Port, "Watcher");
		LanClient[] clients = [guest, spectator];
		PumpUntil(host, clients, () => guest.Lobby != null && spectator.Lobby != null);
		Assert.True(guest.Lobby.hideUnseen);
		guest.ClaimSeat(seatID);
		spectator.Watch();
		PumpUntil(host, clients, () => guest.YourSeats.Contains(seatID) && host.Spectators.Count == 1);

		C7GameData.GameData gameData = await CreateGameWithCities(save);
		Player hostPlayer = gameData.players.First(p => p.isHuman && p.id != seatID);
		host.StartGame();
		PumpUntil(host, clients, () => guest.StartingGame != null && spectator.StartingGame != null);
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = _ => { };
		spectator.SnapshotReceived = _ => { };
		spectator.UiMessageReceived = _ => { };
		Assert.DoesNotContain(guest.StartingGame.Units, u => u.owner == hostPlayer.id);
		// The spectator watches as all the civilizations, unless it chooses
		// otherwise, which shows everyone's units.
		Assert.Equal(SpectatorViewMode.AllCivs, spectator.SpectatorView.mode);
		Assert.Contains(spectator.StartingGame.Units, u => u.owner == hostPlayer.id);
		List<ID> allCivs = gameData.players.Where(p => !p.isBarbarians).Select(p => p.id).ToList();

		// The guest plays: it's sent its view as patches, and the spectator
		// what all the civilizations know.
		Player guestPlayer = gameData.GetPlayer(seatID);
		foreach (MapUnit unit in guestPlayer.units.Where(u => !u.isFortified).ToList()) {
			long processed = EngineStorage.processedMessageCount;
			guest.SendCommand(new MsgSetFortification(unit.id, true));
			PumpUntil(host, clients, () => EngineStorage.processedMessageCount > processed);
		}
		byte[] guestView = LanHost.SnapshotHashFor([seatID]);
		byte[] spectatorView = LanHost.SnapshotHashFor(allCivs);
		PumpUntil(host, clients, () => Has(guest, guestView) && Has(spectator, spectatorView));
		Assert.Equal(1, guest.WholeSnapshotsReceived);
		Assert.True(guest.SnapshotDeltasReceived >= 1);

		// The host's moves out of the guest's sight aren't news to it.
		int guestSnapshots = guest.WholeSnapshotsReceived + guest.SnapshotDeltasReceived;
		MapUnit hostUnit = hostPlayer.units.First(u => !u.isFortified);
		new MsgSetFortification(hostUnit.id, true) { playerID = hostPlayer.id }.send();
		spectatorView = LanHost.SnapshotHashFor(allCivs);
		PumpUntil(host, clients, () => Has(spectator, spectatorView));
		Assert.Equal(guestSnapshots, guest.WholeSnapshotsReceived + guest.SnapshotDeltasReceived);
		Assert.Equal(guestView, LanHost.SnapshotHashFor([seatID]));

		// With nothing hidden, the guest has the whole game too.
		host.HideUnseen = false;
		Assert.False(host.ResumeInfo().hideUnseen);
		PumpUntil(host, clients, () => guest.Lobby?.hideUnseen == false);
		MapUnit another = guestPlayer.units.First();
		long before = EngineStorage.processedMessageCount;
		guest.SendCommand(new MsgSetFortification(another.id, false));
		PumpUntil(host, clients, () => EngineStorage.processedMessageCount > before);
		byte[] wholeGame = LanHost.SnapshotHashFor(null);
		spectatorView = LanHost.SnapshotHashFor(allCivs);
		PumpUntil(host, clients, () => Has(guest, wholeGame) && Has(spectator, spectatorView));
	}

	[Fact]
	public async Task SpectatorsSeeTheGameAsTheHostLetsThem() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { SimultaneousTurns = true, AllowSpectators = true };
		// Hiding what players can't see, spectators see only what the
		// civilizations know unless the host says otherwise.
		Assert.Equal(SpectatorViews.AsCivs, host.SpectatorViews);
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		using LanClient spectator = LanClient.Connect("127.0.0.1", host.Port, "Watcher");
		LanClient[] clients = [guest, spectator];
		PumpUntil(host, clients, () => guest.Lobby != null && spectator.Lobby != null);
		guest.ClaimSeat(seatID);
		spectator.Watch();
		PumpUntil(host, clients, () => guest.YourSeats.Contains(seatID) && spectator.SpectatorView != null);
		Assert.Equal(SpectatorViews.AsCivs, spectator.AllowedSpectatorViews);
		Assert.Equal(new SpectatorViewInfo(SpectatorViewMode.AllCivs), spectator.SpectatorView);

		C7GameData.GameData gameData = await CreateGameWithCities(save);
		Player hostPlayer = gameData.players.First(p => p.isHuman && p.id != seatID);
		List<ID> allCivs = gameData.players.Where(p => !p.isBarbarians).Select(p => p.id).ToList();
		host.StartGame();
		PumpUntil(host, clients, () => guest.StartingGame != null && spectator.StartingGame != null);
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = _ => { };
		int viewChanges = 0;
		spectator.SnapshotReceived = _ => { };
		spectator.UiMessageReceived = _ => { };
		spectator.SpectatorViewChanged = () => viewChanges++;
		PumpUntil(host, clients, () => Has(spectator, LanHost.SnapshotHashFor(allCivs)));

		// As all the civilizations: everyone's units and cities, but not
		// what none of them has seen.
		SaveGame asAll = spectator.StartingGame;
		Assert.Contains(asAll.Units, u => u.owner == hostPlayer.id);
		Assert.Contains(asAll.Units, u => u.owner == seatID);
		Assert.Equal(gameData.cities.Count, asAll.Cities.Count);
		Assert.Contains(gameData.map.tiles, t => t.Resource != Resource.NONE && !allCivs.Any(id => gameData.GetPlayer(id).tileKnowledge.isTileKnown(t)));
		for (int i = 0; i < gameData.map.tiles.Count; ++i) {
			Tile tile = gameData.map.tiles[i];
			if (!allCivs.Any(id => gameData.GetPlayer(id).tileKnowledge.isTileKnown(tile))) {
				Assert.Null(asAll.Map.tiles[i].resource);
			}
		}

		// The whole game isn't allowed, so asking for it changes nothing;
		// one civilization is, and is sent what the guest playing it is.
		spectator.ChooseSpectatorView(new SpectatorViewInfo(SpectatorViewMode.Omniscient));
		spectator.ChooseSpectatorView(new SpectatorViewInfo(SpectatorViewMode.OneCiv, seatID));
		PumpUntil(host, clients, () => spectator.SpectatorView?.mode == SpectatorViewMode.OneCiv);
		Assert.Equal(1, viewChanges);
		Assert.Equal(seatID, spectator.SpectatorView.playerID);
		byte[] guestView = LanHost.SnapshotHashFor([seatID]);
		PumpUntil(host, clients, () => Has(spectator, guestView) && Has(guest, guestView));

		// The host lets spectators see the whole game.
		host.SpectatorViews = SpectatorViews.Any;
		PumpUntil(host, clients, () => spectator.AllowedSpectatorViews == SpectatorViews.Any);
		Assert.Equal(SpectatorViewMode.OneCiv, spectator.SpectatorView.mode);
		spectator.ChooseSpectatorView(new SpectatorViewInfo(SpectatorViewMode.Omniscient));
		PumpUntil(host, clients, () => Has(spectator, LanHost.SnapshotHashFor(null)));

		// Taking a spectator's view away has it watch another way allowed.
		host.SpectatorViews = SpectatorViews.AllCivs;
		PumpUntil(host, clients, () => spectator.SpectatorView?.mode == SpectatorViewMode.AllCivs
			&& Has(spectator, LanHost.SnapshotHashFor(allCivs)));

		// Hosting the game again keeps how spectators may see it.
		LanResumeInfo info = NetSerialization.DeserializeData<LanResumeInfo>(NetSerialization.SerializeData(host.ResumeInfo()));
		Assert.Equal(SpectatorViews.AllCivs, info.spectatorViews);
		host.Dispose();
		using LanHost resumed = LanHost.Resume("Host", save, info, port: 0, answerDiscovery: false);
		Assert.Equal(SpectatorViews.AllCivs, resumed.SpectatorViews);
		// One that left it to hiding follows it.
		using LanHost older = LanHost.Resume("Host", save, info with { spectatorViews = null, hideUnseen = false }, port: 0,
			answerDiscovery: false);
		Assert.Equal(SpectatorViews.Any, older.SpectatorViews);
	}

	[Fact]
	public void AResumedGameKeepsWhetherItHides() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		LanResumeInfo info = new("Host", 0, null, false, [], hideUnseen: false);
		LanResumeInfo read = NetSerialization.DeserializeData<LanResumeInfo>(NetSerialization.SerializeData(info));
		Assert.False(read.hideUnseen);
		using LanHost host = LanHost.Resume("Host", save, read, port: 0, answerDiscovery: false);
		Assert.False(host.HideUnseen);

		// Resume info saved before it was kept hides, as new games do.
		LanResumeInfo old = NetSerialization.DeserializeData<LanResumeInfo>(
			System.Text.Encoding.UTF8.GetBytes("{\"hostName\":\"Host\",\"port\":0,\"simultaneousTurns\":false,\"seats\":[]}"));
		Assert.True(old.hideUnseen);
	}
}

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
		City otherCity = other.cities.Single();
		Assert.False(guest.tileKnowledge.isTileKnown(otherCity.location));
		SaveGame whole = LanProtocol.SnapshotOf(gameData);
		byte[] wholeHash = Hash(whole);
		SaveGame filtered = SnapshotFilter.Filter(whole, SnapshotFilter.ViewOf(gameData, [guest.id]));
		// Filtering leaves the snapshot it's given as it was.
		Assert.Equal(wholeHash, Hash(whole));
		Assert.Null(whole.HostFacts);

		// All of the guest's units, and others' only where it can see them.
		Assert.Equal(guest.units.Select(u => u.id).Order(), filtered.Units.Where(u => u.owner == guest.id).Select(u => u.id).Order());
		Assert.All(filtered.Units.Where(u => u.owner != guest.id),
			u => Assert.True(guest.tileKnowledge.isActiveTile(gameData.map.tileAt(u.currentLocation.X, u.currentLocation.Y))));
		Assert.Contains(gameData.mapUnits, u => u.owner == other);
		Assert.DoesNotContain(filtered.Units, u => u.owner == other.id);

		// The other human's city, on a tile the guest has never seen, isn't
		// there at all; no city the guest doesn't own is on such a tile.
		Assert.DoesNotContain(filtered.Cities, c => c.id == otherCity.id);
		Assert.All(filtered.Cities.Where(c => c.owner != guest.id),
			c => Assert.True(guest.tileKnowledge.isTileKnown(gameData.map.tileAt(c.location.X, c.location.Y))));
		// The guest's own city is whole.
		City guestCity = guest.cities.Single();
		SaveCity own = filtered.Cities.Single(c => c.id == guestCity.id);
		Assert.Equal(guestCity.residents.Count, own.residents.Count);
		Assert.Equal(guestCity.itemBeingProduced.name, own.producible);

		// Once the guest has seen where it is, the city is there, for the
		// map and its borders, but without its insides.
		guest.tileKnowledge.AddTileToKnown(otherCity.location);
		whole = LanProtocol.SnapshotOf(gameData);
		filtered = SnapshotFilter.Filter(whole, SnapshotFilter.ViewOf(gameData, [guest.id]));
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

		// Every player is there, for the scoreboard, but those the guest
		// hasn't met keep their gold, techs and research to themselves, and
		// know of the map only their own territory that the guest knows.
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
		Assert.Contains(gameData.map.tiles.IndexOf(otherCity.location), otherKnows);
		Assert.All(otherKnows, i => Assert.Equal(other.id, gameData.map.tiles[i].OwningPlayer()?.id));
		Assert.All(otherKnows, i => Assert.True(guest.tileKnowledge.isTileKnown(gameData.map.tiles[i])));
		Assert.Empty(otherSave.playerRelationships.Keys.Where(k => k != guest.id.ToString()));
		// The guest's own player is whole.
		SavePlayer guestSave = filtered.Players.Single(p => p.id == guest.id);
		Assert.Same(whole.Players.Single(p => p.id == guest.id), guestSave);

		// Tiles the guest has never seen lose all but their terrain next to
		// the tiles it knows, whose edges the map draws from them, and
		// further off, all but whether they're land or water.
		HashSet<Tile> edge = guest.tileKnowledge.knownTiles.SelectMany(t => t.neighbors.Values)
			.Where(t => t != Tile.NONE && !guest.tileKnowledge.isTileKnown(t)).ToHashSet();
		int placeholders = 0;
		for (int i = 0; i < gameData.map.tiles.Count; ++i) {
			Tile tile = gameData.map.tiles[i];
			SaveTile sent = filtered.Map.tiles[i];
			Assert.Equal((tile.XCoordinate, tile.YCoordinate), (sent.X, sent.Y));
			if (guest.tileKnowledge.isTileKnown(tile)) {
				Assert.Same(whole.Map.tiles[i], sent);
				continue;
			}
			Assert.Null(sent.resource);
			Assert.Empty(sent.overlays);
			Assert.DoesNotContain("barbarianCamp", sent.features);
			Assert.DoesNotContain("goodyHut", sent.features);
			if (edge.Contains(tile)) {
				Assert.Equal(tile.baseTerrainType.Key, sent.baseTerrain);
				Assert.Equal(tile.overlayTerrainType.Key, sent.overlayTerrain);
			} else {
				Assert.Equal(tile.baseTerrainType.IsWater ? "coast" : "grassland", sent.baseTerrain);
				Assert.Equal(sent.baseTerrain, sent.overlayTerrain);
				Assert.Empty(sent.features);
				Assert.Null(sent.extraInfo);
				Assert.Equal(tile.continent, sent.continent);
				++placeholders;
			}
		}
		Assert.True(placeholders > 0);
		Assert.Contains(gameData.map.tiles, t => !guest.tileKnowledge.isTileKnown(t) && t.Resource != Resource.NONE);
		Assert.All(filtered.Map.startingLocations, t => Assert.True(guest.tileKnowledge.isTileKnown(gameData.map.tileAt(t.X, t.Y))));

		// What the guest can't work out from that, the host works out.
		HostFacts facts = filtered.HostFacts;
		Assert.NotNull(facts);
		Assert.Equal(other.cities.Count, facts.players[other.id.ToString()].cities);
		Assert.Equal(other.cities.Sum(c => c.residents.Count), facts.players[other.id.ToString()].population);
		Assert.DoesNotContain(gameData.players.First(p => p.isBarbarians).id.ToString(), facts.players.Keys);

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
		City otherCity = other.cities.Single();
		// The guest has seen some of the other's territory, but not their
		// city.
		Tile seen = otherCity.location.neighbors.Values.First(t => t != Tile.NONE && t.OwningPlayer() == other);
		guest.tileKnowledge.AddTileToKnown(seen);
		Assert.False(guest.tileKnowledge.isTileKnown(otherCity.location));
		SaveGame filtered = FilteredFor(gameData, guest);
		Assert.DoesNotContain(filtered.Cities, c => c.id == otherCity.id);

		// What the guest's screens show of everyone, as the host has it.
		Dictionary<ID, (int culture, float turnScore, VictoryStatus domination)> onHost = gameData.players
			.Where(p => !p.isBarbarians)
			.ToDictionary(p => p.id, p => (CultureReport.TotalCulture(p), ScoreVictory.ComputeTurnScore(p, gameData),
				new DominationVictory(50, 50).Evaluate(p, gameData)));
		Player unitedNationsOwner = UnitedNations.Owner(gameData);
		List<Tile> known = guest.tileKnowledge.knownTiles.ToList();
		Dictionary<(int, int), ID> owners = known.ToDictionary(t => (t.XCoordinate, t.YCoordinate), t => t.OwningPlayer()?.id);
		List<(int, int)> guestTerritory = gameData.map.tiles.Where(t => t.OwningPlayer() == guest)
			.Select(t => (t.XCoordinate, t.YCoordinate)).Order().ToList();
		int guestWorkable = guest.cities.Single().GetWorkableTiles().Count;

		try {
			// The guest's machine builds its game from it, as it does every
			// snapshot, and works out sight and moods.
			C7GameData.GameData shown = CreateGame.ReplaceWithSnapshot(filtered, fixture.behaviors);
			EngineStorage.uiControllerID = guest.id;
			Player shownOther = shown.GetPlayer(other.id);
			Player shownGuest = shown.GetPlayer(guest.id);
			Assert.Null(shown.GetCity(otherCity.id));
			Assert.Empty(shownOther.cities);
			Assert.Empty(shownOther.units);
			Assert.NotNull(shownOther.government);

			// The borders of what the guest knows are the host's, the other's
			// city or not; and the guest's own territory and city are too.
			foreach (((int x, int y), ID owner) in owners) {
				Assert.Equal(owner, shown.map.tileAt(x, y).OwningPlayer()?.id);
			}
			Assert.Equal(other.id, shown.map.tileAt(seen.XCoordinate, seen.YCoordinate).OwningPlayer()?.id);
			Assert.Equal(guestTerritory, shown.map.tiles.Where(t => t.OwningPlayer() == shownGuest)
				.Select(t => (t.XCoordinate, t.YCoordinate)).Order().ToList());
			Assert.Equal(guestWorkable, shownGuest.cities.Single().GetWorkableTiles().Count);

			// And so are the scores, culture, domination and the United
			// Nations the screens show of everyone.
			Assert.Equal(gameData.history.Keys.Order(), shown.history.Keys.Order());
			foreach (Player p in shown.players.Where(p => !p.isBarbarians)) {
				(int culture, float turnScore, VictoryStatus domination) = onHost[p.id];
				Assert.Equal(culture, CultureReport.TotalCulture(p));
				Assert.Equal(turnScore, ScoreVictory.ComputeTurnScore(p, shown));
				VictoryStatus status = new DominationVictory(50, 50).Evaluate(p, shown);
				Assert.Equal(domination.TerritoryPercent, status.TerritoryPercent);
				Assert.Equal(domination.PopulationPercent, status.PopulationPercent);
				Assert.Equal(gameData.history[p.HistoryKey].LastOrDefault()?.Score, shown.history[p.HistoryKey].LastOrDefault()?.Score);
			}
			Assert.Equal(unitedNationsOwner?.id, UnitedNations.Owner(shown)?.id);
			// Their civilization has a city to send an ambassador to.
			Assert.NotEqual("They have no capital to send an ambassador to.",
				Espionage.Unavailable(shown, shownGuest, EspionageMission.EstablishEmbassy, shownOther, null));

			// What the UI asks of the other civs works.
			foreach (Player p in shown.players.Where(p => !p.isBarbarians)) {
				shownGuest.CompareMilitaryStrengthTo(p);
				foreach (City c in p.cities) {
					Assert.True(c.GetCulture() >= 0);
					c.RecalculateCitizenMoods(shown);
				}
				shown.TechCostFor(shown.techs[0], p);
			}

			// And so does saving it, as the player may.
			SaveGame saved = SaveGame.FromGameData(shown);
			Assert.Equal(filtered.Cities.Count, saved.Cities.Count);
		} finally {
			EngineStorage.gameData = gameData;
		}
	}

	[Fact]
	public async Task GreatWondersShowWhereTheyAreToEveryone() {
		C7GameData.GameData gameData = await CreateGameWithCities(SaveGameFixture.TwoHumanSave());
		(Player guest, Player other) = (Humans(gameData)[1], Humans(gameData)[0]);
		City otherCity = other.cities.Single();
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder());
		otherCity.AddBuilding(wonder);
		gameData.GreatWondersBuilt.Add(wonder.name);
		SaveGame filtered = FilteredFor(gameData, guest);
		Assert.DoesNotContain(filtered.Cities, c => c.id == otherCity.id);
		WonderFacts where = filtered.HostFacts.wonders.Single(w => w.wonder == wonder.name);
		Assert.Equal(other.id.ToString(), where.owner);
		Assert.Equal(otherCity.name, where.city);
	}

	[Fact]
	public async Task AnEmbassyShowsACivsCitiesAndResearch() {
		C7GameData.GameData gameData = await CreateGameWithCities(SaveGameFixture.TwoHumanSave());
		(Player guest, Player other) = (Humans(gameData)[1], Humans(gameData)[0]);
		guest.EnsureRelationshipExists(other);
		// Where their city is, which they would have seen on meeting.
		guest.tileKnowledge.AddTileToKnown(other.cities.Single().location);
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
		// Even hiding what players can't see, spectators may watch any way
		// unless the host says otherwise; this host keeps them to what the
		// civilizations know.
		Assert.Equal(SpectatorViews.Any, host.SpectatorViews);
		host.SpectatorViews = SpectatorViews.AsCivs;
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
		// One that left it to the default lets them watch any way, hiding
		// or not.
		foreach (bool hideUnseen in new[] { true, false }) {
			using LanHost older = LanHost.Resume("Host", save, info with { spectatorViews = null, hideUnseen = hideUnseen }, port: 0,
				answerDiscovery: false);
			Assert.Equal(SpectatorViews.Any, older.SpectatorViews);
		}
	}

	// A spectator may switch to any view at will, which it's sent whole
	// straight away, and watches the same way when it comes back.
	[Fact]
	public async Task ASpectatorSwitchesViewsAtWillAndKeepsItsChoice() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { SimultaneousTurns = true, AllowSpectators = true };
		Assert.True(host.HideUnseen);
		ID seatID = host.Seats[0].playerID;
		using LanClient guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		LanClient spectator = LanClient.Connect("127.0.0.1", host.Port, "Watcher");
		try {
			PumpUntil(host, [guest, spectator], () => guest.Lobby != null && spectator.Lobby != null);
			guest.ClaimSeat(seatID);
			spectator.Watch();
			PumpUntil(host, [guest, spectator], () => guest.YourSeats.Contains(seatID) && spectator.SpectatorView != null);
			Assert.Equal(SpectatorViews.Any, spectator.AllowedSpectatorViews);

			C7GameData.GameData gameData = await CreateGameWithCities(save);
			List<ID> allCivs = gameData.players.Where(p => !p.isBarbarians).Select(p => p.id).ToList();
			host.StartGame();
			PumpUntil(host, [guest, spectator], () => guest.StartingGame != null && spectator.StartingGame != null);
			guest.SnapshotReceived = _ => { };
			guest.UiMessageReceived = _ => { };
			spectator.SnapshotReceived = _ => { };
			spectator.UiMessageReceived = _ => { };
			PumpUntil(host, [guest, spectator], () => Has(spectator, LanHost.SnapshotHashFor(allCivs)));

			// Each civilization, and the whole game, while hiding what
			// players can't see.
			Player computer = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
			foreach (SpectatorViewInfo view in new SpectatorViewInfo[] {
				new(SpectatorViewMode.OneCiv, seatID), new(SpectatorViewMode.Omniscient), new(SpectatorViewMode.OneCiv, computer.id) }) {
				int whole = spectator.WholeSnapshotsReceived;
				spectator.ChooseSpectatorView(view);
				byte[] hash = LanHost.SnapshotHashFor(view.mode == SpectatorViewMode.Omniscient ? null : [view.playerID]);
				PumpUntil(host, [guest, spectator], () => spectator.SpectatorView == view && Has(spectator, hash));
				Assert.Equal(whole + 1, spectator.WholeSnapshotsReceived);
			}

			// Back after leaving, it watches as the civilization it chose.
			string token = spectator.ReconnectToken;
			Assert.NotNull(token);
			spectator.Dispose();
			spectator = LanClient.Connect("127.0.0.1", host.Port, "Watcher", token);
			PumpUntil(host, [guest, spectator], () => spectator.Lobby != null);
			spectator.Watch();
			PumpUntil(host, [guest, spectator], () => spectator.StartingGame != null);
			spectator.SnapshotReceived = _ => { };
			spectator.UiMessageReceived = _ => { };
			PumpUntil(host, [guest, spectator], () => spectator.SpectatorView != null);
			Assert.Equal(new SpectatorViewInfo(SpectatorViewMode.OneCiv, computer.id), spectator.SpectatorView);
		} finally {
			spectator.Dispose();
		}
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

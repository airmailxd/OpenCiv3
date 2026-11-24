using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

// Civ3's scaled turn timer (see ScaledTurnTimer): a base time and more for
// each city and unit, each player's own when they move one by one, the
// largest player's for a round moved at once.
public class ScaledTurnTimerTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public ScaledTurnTimerTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
	}

	// ---- The formula ----

	private static Rules TimerRules(int baseSeconds, int perCity, int perUnit) {
		return new Rules {
			MultiplayerTurnTimerBase = baseSeconds,
			MultiplayerTurnTimerPerCity = perCity,
			MultiplayerTurnTimerPerUnit = perUnit,
		};
	}

	private static Player Empire(string id, int cities, int units) {
		Player player = new() { id = ID.FromString(id) };
		for (int i = 0; i < cities; ++i) {
			player.cities.Add(new City(null, player, $"City {i}", ID.FromString($"{id}-city-{i}")));
		}
		for (int i = 0; i < units; ++i) {
			player.units.Add(new MapUnit(ID.FromString($"{id}-unit-{i}")));
		}
		return player;
	}

	[Fact]
	public void ATurnHasTheBaseTimeAndMoreForEachCityAndUnit() {
		Player player = Empire("player-1", cities: 3, units: 5);
		// MP Tournament: 16 + 2 x 3 + 1 x 5.
		Assert.Equal(27, ScaledTurnTimer.SecondsFor(TimerRules(16, 2, 1), player));
		// QC gives no time for units: 42 + 3 x 3.
		Assert.Equal(51, ScaledTurnTimer.SecondsFor(TimerRules(42, 3, 0), player));
		// Conquests' own: 24 + 3 x 3 + 1 x 5.
		Assert.Equal(38, ScaledTurnTimer.SecondsFor(new Rules(), player));
		Assert.Equal(16, ScaledTurnTimer.SecondsFor(TimerRules(16, 2, 1), Empire("player-2", 0, 0)));
	}

	[Fact]
	public void TheFormulaIsShownAsTheRulesGiveIt() {
		Assert.Equal("16 s + 2 s per city + 1 s per unit", ScaledTurnTimer.Describe(TimerRules(16, 2, 1)));
		Assert.Equal("42 s + 3 s per city", ScaledTurnTimer.Describe(TimerRules(42, 3, 0)));
		Assert.Equal("24 s + 3 s per city + 1 s per unit", ScaledTurnTimer.Describe(new Rules()));
		Assert.Equal("Scaled (37 s + 2 s per city + 1 s per unit)", ScaledTurnTimer.ChoiceText(TimerRules(37, 2, 1)));
		Assert.Equal("0 s", ScaledTurnTimer.Describe(TimerRules(0, 0, 0)));
	}

	[Fact]
	public void EachPlayerHasTheirOwnTimeAndARoundTheLargestPlayers() {
		Player small = Empire("small-1", cities: 1, units: 2);
		Player large = Empire("large-2", cities: 6, units: 20);
		C7GameData.GameData gameData = new() { rules = TimerRules(16, 2, 1), players = [small, large] };

		// One by one, each is timed by their own cities and units.
		Assert.Equal(TimeSpan.FromSeconds(16 + 2 + 2), ScaledTurnTimer.LimitFor(gameData, [small.id]));
		Assert.Equal(TimeSpan.FromSeconds(16 + 12 + 20), ScaledTurnTimer.LimitFor(gameData, [large.id]));
		// At once, everyone has the largest player's time.
		Assert.Equal(TimeSpan.FromSeconds(16 + 12 + 20), ScaledTurnTimer.LimitFor(gameData, [small.id, large.id]));
		// Nobody to time, or rules with no timer: no limit.
		Assert.Null(ScaledTurnTimer.LimitFor(gameData, []));
		gameData.rules = TimerRules(0, 0, 0);
		Assert.Null(ScaledTurnTimer.LimitFor(gameData, [small.id, large.id]));
	}

	// ---- Hosting ----

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	// Creates the game and plays up to the humans' first turn.
	private async Task<C7GameData.GameData> CreateGame(SaveGame save, bool simultaneous) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		save.SimultaneousTurns = simultaneous;
		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		await TurnHandling.AdvanceTurn();
		return EngineStorage.gameData;
	}

	private static Player[] Humans(C7GameData.GameData gameData) => gameData.players.Where(p => p.isHuman).ToArray();

	private static void SetTimer(C7GameData.GameData gameData, int baseSeconds, int perCity, int perUnit) {
		gameData.rules.MultiplayerTurnTimerBase = baseSeconds;
		gameData.rules.MultiplayerTurnTimerPerCity = perCity;
		gameData.rules.MultiplayerTurnTimerPerUnit = perUnit;
	}

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

	private static void PumpFor(LanHost host, IEnumerable<LanClient> clients, TimeSpan time) {
		Stopwatch pumping = Stopwatch.StartNew();
		PumpUntil(host, clients, () => pumping.Elapsed >= time);
	}

	private static LanClient JoinSeats(LanHost host, IEnumerable<ID> seatIDs) {
		LanClient client = LanClient.Connect("127.0.0.1", host.Port, "Guest");
		PumpUntil(host, [client], () => client.Lobby != null);
		foreach (ID seat in seatIDs) {
			client.ClaimSeat(seat);
		}
		PumpUntil(host, [client], () => seatIDs.All(client.YourSeats.Contains));
		return client;
	}

	[Fact]
	public async Task OneByOneEachPlayerIsTimedByTheirOwnEmpire() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { ScaledTurnTimer = true };
		ID seatID = host.Seats[0].playerID;
		using LanClient client = JoinSeats(host, [seatID]);
		// The guests see the formula, by the setup's rules, as no fixed time.
		PumpUntil(host, [client], () => client.Lobby?.details?.scaledTurnTimer != null);
		Assert.Equal(ScaledTurnTimer.Describe(save.Rules), client.Lobby.details.scaledTurnTimer);
		Assert.Null(client.Lobby.details.turnSeconds);

		C7GameData.GameData gameData = await CreateGame(save, simultaneous: false);
		Player[] humans = Humans(gameData);
		// A second, so the host's turn runs out.
		SetTimer(gameData, 1, 0, 0);
		host.StartGame();
		PumpUntil(host, [client], () => client.StartingGame != null);
		client.SnapshotReceived = _ => { };
		client.UiMessageReceived = _ => { };

		PumpUntil(host, [client], () => client.CurrentClock()?.secondsAllowed != null);
		Assert.Equal(humans[0].id, client.CurrentClock().activePlayerID);
		Assert.Equal(1, client.CurrentClock().secondsAllowed.Value, 3);

		// Nobody ends the host's turn, so its time runs out and the guest's
		// turn begins. The pump checks for it straight after the engine
		// handles it, before the host works out the guest's time.
		PumpUntil(host, [client], () => EngineStorage.activePlayerID == seatID);
		Assert.True(humans[0].hasPlayedThisTurn);
		SetTimer(gameData, 3600, 60, 7);

		// The guest's turn is timed by the guest's own cities and units.
		PumpUntil(host, [client], () => client.CurrentClock()?.activePlayerID == seatID);
		int expected = 3600 + 60 * humans[1].cities.Count + 7 * humans[1].units.Count;
		Assert.Equal(expected, ScaledTurnTimer.SecondsFor(gameData.rules, humans[1]));
		Assert.Equal(expected, client.CurrentClock().secondsAllowed.Value, 3);
		Assert.Equal(expected, host.CurrentTurnLimit.Value.TotalSeconds, 3);
		Assert.False(humans[1].hasPlayedThisTurn);
	}

	[Fact]
	public async Task AtOnceTheRoundHasTheLargestPlayersTimeAndEndsForEveryone() {
		SaveGame save = SaveGameFixture.ThreeHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) {
			SimultaneousTurns = true,
			ScaledTurnTimer = true,
		};
		using LanClient guest = JoinSeats(host, host.Seats.Select(s => s.playerID).ToList());

		C7GameData.GameData gameData = await CreateGame(save, simultaneous: false);
		Player[] humans = Humans(gameData);
		SetTimer(gameData, 1, 0, 0);
		host.StartGame();
		PumpUntil(host, [guest], () => guest.StartingGame != null);
		guest.SnapshotReceived = _ => { };
		guest.UiMessageReceived = _ => { };

		// One of the guest's players finishes; the host and the other don't,
		// and the round's second runs out for both of them.
		guest.SendCommand(new MsgEndTurn { playerID = humans[2].id });
		PumpUntil(host, [guest], () => humans[2].hasPlayedThisTurn || gameData.turn == 1);
		PumpUntil(host, [guest], () => gameData.turn == 1);
		// The next round's time, from its players' empires as it begins.
		SetTimer(gameData, 3600, 60, 7);
		Assert.Equal(humans, TurnHandling.PlayersToMove(gameData));

		PumpUntil(host, [guest], () => guest.CurrentClock()?.turn == 1 && guest.CurrentClock().secondsAllowed != null);
		int expected = humans.Max(p => ScaledTurnTimer.SecondsFor(gameData.rules, p));
		Assert.Equal(expected, guest.CurrentClock().secondsAllowed.Value, 3);
		Assert.Equal(humans.Select(p => p.id), guest.CurrentClock().playersToMove);
	}

	// ---- Saving and restoring ----

	[Fact]
	public async Task TheTimerIsSavedWithTheGame() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		using LanHost host = new("Host", save, port: 0, answerDiscovery: false) { ScaledTurnTimer = true };
		using LanClient client = JoinSeats(host, [host.Seats[0].playerID]);
		C7GameData.GameData gameData = await CreateGame(save, simultaneous: false);
		Assert.False(gameData.scaledTurnTimer);
		host.StartGame();
		Assert.True(gameData.scaledTurnTimer);
		Assert.Null(gameData.turnTimerSeconds);

		SaveGame saved = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.True(saved.ScaledTurnTimer);
		Assert.Null(saved.TurnTimerSeconds);
		SaveGame scaledSave = saved;

		// And so is a fixed one, when the host changes to it.
		host.TurnTimeLimit = TimeSpan.FromMinutes(5);
		host.ScaledTurnTimer = false;
		saved = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.False(saved.ScaledTurnTimer);
		Assert.Equal(300, saved.TurnTimerSeconds);

		// Games saved before the timer have none.
		string json = Encoding.UTF8.GetString(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.Contains("\"turnTimerSeconds\":300,", json);
		string older = json.Replace("\"turnTimerSeconds\":300,", "").Replace("\"scaledTurnTimer\":false,", "");
		SaveGame olderSave = SaveGame.FromJSON(Encoding.UTF8.GetBytes(older));
		Assert.Null(olderSave.TurnTimerSeconds);
		Assert.False(olderSave.ScaledTurnTimer);

		// Loaded, the game has it again (last: loading makes it the
		// engine's game).
		Assert.True(scaledSave.ToGameData(fixture.behaviors).scaledTurnTimer);
	}

	[Fact]
	public void AResumedOrReloadedGameKeepsTheTimer() {
		SaveGame save = SaveGameFixture.TwoHumanSave();
		LanResumeInfo info;
		using (LanHost host = new("Host", save, port: 0, answerDiscovery: false) { ScaledTurnTimer = true }) {
			info = NetSerialization.DeserializeData<LanResumeInfo>(NetSerialization.SerializeData(host.ResumeInfo()));
		}
		Assert.True(info.scaledTurnTimer);
		Assert.Null(info.turnSeconds);
		using (LanHost resumed = LanHost.Resume("Host", save, info, port: 0, answerDiscovery: false)) {
			Assert.True(resumed.ScaledTurnTimer);
			Assert.Equal(ScaledTurnTimer.Describe(save.Rules), resumed.CurrentDetails().scaledTurnTimer);
		}
		// Resume files from before the timer don't have it.
		LanResumeInfo older = info with { scaledTurnTimer = false };
		using (LanHost resumed = LanHost.Resume("Host", save, older, port: 0, answerDiscovery: false)) {
			Assert.False(resumed.ScaledTurnTimer);
		}

		// A saved game hosted again starts with the timer it was saved with.
		save.ScaledTurnTimer = true;
		using (LanHost reloaded = new("Host", save, port: 0, answerDiscovery: false)) {
			Assert.True(reloaded.ScaledTurnTimer);
			Assert.Null(reloaded.TurnTimeLimit);
		}
		save.ScaledTurnTimer = false;
		save.TurnTimerSeconds = 600;
		using (LanHost reloaded = new("Host", save, port: 0, answerDiscovery: false)) {
			Assert.False(reloaded.ScaledTurnTimer);
			Assert.Equal(TimeSpan.FromMinutes(10), reloaded.TurnTimeLimit);
		}
	}

	// ---- Server games ----

	private const string FirstToken = "first-seat-token";
	private const string SecondToken = "second-seat-token";

	[Fact]
	public async Task AServerGameIsTimedOnlyWhileItsPlayerIsConnected() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		SaveGame save = SaveGameFixture.TwoHumanSave();
		// As the setup screen puts it on the server.
		save.ScaledTurnTimer = true;
		await C7Engine.CreateGame.createGame(save, (_) => fixture.behaviors);
		TurnHandling.InitTurnData();
		C7GameData.GameData gameData = EngineStorage.gameData;
		Player[] humans = Humans(gameData);
		SetTimer(gameData, 1, 0, 0);
		using LanHost host = LanHost.Dedicated("Friday Game", save, [
			new LanHost.DedicatedSeat(humans[0].id, FirstToken, "alice"),
			new LanHost.DedicatedSeat(humans[1].id, SecondToken, "bob"),
		]);
		Assert.True(host.ScaledTurnTimer);
		host.StartGame();
		EngineStorage.messagesToUI.Clear();
		// The worker saves the game as it stands: still with its timer.
		Assert.True(SaveGame.FromGameData(gameData).ScaledTurnTimer);

		// Nobody is connected: however long it waits, nobody runs out.
		PumpFor(host, [], TimeSpan.FromSeconds(1.5));
		Assert.False(humans[0].hasPlayedThisTurn);

		// Alice comes, and her second runs out; Bob's turn waits for him.
		using LanClient alice = LanClient.Connect("127.0.0.1", host.Port, "alice", FirstToken);
		alice.SnapshotReceived = _ => { };
		alice.UiMessageReceived = _ => { };
		PumpUntil(host, [alice], () => humans[0].hasPlayedThisTurn);
		PumpFor(host, [alice], TimeSpan.FromSeconds(1.5));
		Assert.False(humans[1].hasPlayedThisTurn);
		Assert.Equal(humans[1].id, EngineStorage.activePlayerID);
	}
}

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

// A guest's own simple orders carried out on its game before the host's
// snapshot comes back. The host and the guest run in one process here, so
// the guest's game is made from its snapshots by hand, and made the current
// game while the guest acts on it, as it always is on a guest's machine.
public class MovePredictionTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;

	public MovePredictionTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		EngineStorage.animationMessages.Clear();
		EngineStorage.ResetNetworking();
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
		EngineStorage.animationMessages.Clear();
	}

	private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(60);

	// A host and a guest playing a two-human game with simultaneous turns,
	// and the guest's game as of its latest snapshot.
	private sealed class Game : IDisposable {
		public LanHost host;
		public LanClient guest;
		public C7GameData.GameData hostGame;
		public C7GameData.GameData guestGame;
		public Player guestPlayer;
		public SaveGame latestSnapshot;
		public int snapshotsShown;
		private long sentAt;
		private readonly SaveGameFixture fixture;

		public Game(SaveGameFixture fixture) {
			this.fixture = fixture;
		}

		public async Task Start() {
			SaveGame save = SaveGameFixture.TwoHumanSave();
			host = new LanHost("Host", save, port: 0, answerDiscovery: false) { SimultaneousTurns = true };
			ID seatID = host.Seats[0].playerID;
			guest = LanClient.Connect("127.0.0.1", host.Port, "Guest");
			Pump(() => guest.Lobby != null);
			guest.ClaimSeat(seatID);
			Pump(() => guest.YourSeats.Contains(seatID));

			new MsgSetAnimationsEnabled(false).send();
			EngineStorage.ProcessNextMessageToEngine();
			save.SimultaneousTurns = true;
			await CreateGame.createGame(save, (_) => fixture.behaviors);
			TurnHandling.OnBeginTurn();
			TurnHandling.InitTurnData();
			await TurnHandling.AdvanceTurn();
			EngineStorage.messagesToUI.Clear();
			hostGame = EngineStorage.gameData;

			host.StartGame();
			Pump(() => guest.StartingGame != null);
			guest.SnapshotReceived = s => latestSnapshot = s;
			guest.UiMessageReceived = _ => { };
			guestGame = Show(guest.StartingGame);
			guestPlayer = guestGame.GetPlayer(seatID);
			Assert.True(TurnHandling.IsPlayersTurn(guestGame, seatID));
		}

		// Replaces the guest's game with a snapshot, as Game.OnLanSnapshot
		// does, leaving the host's game current.
		public C7GameData.GameData Show(SaveGame snapshot) {
			C7GameData.GameData shown = CreateGame.ReplaceWithSnapshot(snapshot, fixture.behaviors);
			EngineStorage.gameData = hostGame;
			++snapshotsShown;
			return shown;
		}

		// Runs the host, its engine and the guest until the condition holds,
		// showing the guest each snapshot it receives.
		public void Pump(Func<bool> condition) {
			Stopwatch pumping = Stopwatch.StartNew();
			while (true) {
				host.Poll();
				EngineStorage.ProcessNextMessageToEngine();
				EngineStorage.messagesToUI.Clear();
				guest.Poll();
				if (latestSnapshot != null) {
					guestGame = Show(latestSnapshot);
					guestPlayer = guestGame.GetPlayer(guestPlayer.id);
					latestSnapshot = null;
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

		// Sends an order from the guest, with the guest's game current and
		// animations on, as a player's machine has them.
		public void Send(MessageToEngine msg) {
			sentAt = EngineStorage.processedMessageCount;
			msg.playerID = guestPlayer.id;
			EngineStorage.gameData = guestGame;
			EngineStorage.animationsEnabled = true;
			try {
				guest.SendCommand(msg);
			} finally {
				EngineStorage.gameData = hostGame;
				EngineStorage.animationsEnabled = false;
			}
		}

		// Once the host has settled, the guest has exactly the game the host
		// would send it.
		public void Settle() {
			Pump(() => EngineStorage.processedMessageCount > sentAt);
			while (EngineStorage.HasPendingMessagesToEngine()) {
				EngineStorage.ProcessNextMessageToEngine();
			}
			byte[] expected = LanHost.SnapshotHashFor([guestPlayer.id]);
			Pump(() => guest.ShownSnapshotHash?.AsSpan().SequenceEqual(expected) == true);
		}

		public void Dispose() {
			guest?.Dispose();
			host?.Dispose();
		}
	}

	// One of the player's units and a tile it can step onto plainly.
	private static (MapUnit, Tile) PredictableStep(Player player) {
		foreach (MapUnit unit in player.units) {
			foreach ((TileDirection _, Tile tile) in unit.location.neighbors) {
				if (unit.CanPredictStepTo(tile)) {
					return (unit, tile);
				}
			}
		}
		throw new InvalidOperationException($"{player} has no unit with a plain step to take");
	}

	[Fact]
	public async Task AnOwnStepShowsBeforeTheHostAnswers() {
		using Game game = new(fixture);
		await game.Start();
		game.guest.PredictMoves = true;
		(MapUnit unit, Tile to) = PredictableStep(game.guestPlayer);
		Tile from = unit.location;
		float movesBefore = unit.movementPoints.remaining;
		int shown = game.snapshotsShown;
		EngineStorage.animationMessages.Clear();

		game.Send(new MsgMoveUnit(unit.id, from.DirectionTo(to)));

		// Before any snapshot: the guest's game has the unit on its new tile,
		// the host's doesn't yet, and the only thing for the UI is the step's
		// animation.
		Assert.Equal(shown, game.snapshotsShown);
		Assert.Same(to, unit.location);
		Assert.Contains(unit, to.unitsOnTile);
		Assert.DoesNotContain(unit, from.unitsOnTile);
		Assert.True(unit.movementPoints.remaining < movesBefore);
		Assert.Equal(from.DirectionTo(to), unit.facingDirection);
		Assert.Empty(EngineStorage.messagesToUI);
		MsgStartUnitAnimation animation = Assert.IsType<MsgStartUnitAnimation>(Assert.Single(EngineStorage.animationMessages));
		Assert.Equal(unit.id, animation.unitID);
		Assert.Equal(MapUnit.AnimatedAction.RUN, animation.action);
		MapUnit hostUnit = game.hostGame.GetUnit(unit.id);
		Assert.Equal((from.XCoordinate, from.YCoordinate), (hostUnit.location.XCoordinate, hostUnit.location.YCoordinate));

		// The host's snapshot agrees.
		game.Settle();
		MapUnit confirmed = game.guestGame.GetUnit(unit.id);
		Assert.Equal((to.XCoordinate, to.YCoordinate), (confirmed.location.XCoordinate, confirmed.location.YCoordinate));
		Assert.Equal(unit.movementPoints.remaining, confirmed.movementPoints.remaining);
		Assert.Equal(unit.facingDirection, confirmed.facingDirection);
		Assert.Equal((to.XCoordinate, to.YCoordinate), (hostUnit.location.XCoordinate, hostUnit.location.YCoordinate));
	}

	[Fact]
	public async Task APathIsFollowedAsFarAsItsStepsArePlain() {
		using Game game = new(fixture);
		await game.Start();
		game.guest.PredictMoves = true;
		(MapUnit unit, Tile to) = PredictableStep(game.guestPlayer);
		Tile from = unit.location;
		// There and back again, and again: further than the unit can go this
		// turn.
		game.Send(new MsgSetUnitPath(unit.id, new TilePath(from, new Queue<Tile>([to, from, to, from]))));

		Assert.True(unit.location == to || unit.previousLocation == to);
		Assert.False(unit.movementPoints.canMove);
		int stepsLeft = unit.path.PathLength();
		Assert.InRange(stepsLeft, 1, 3);
		Tile predicted = unit.location;

		// The host goes as far.
		game.Settle();
		MapUnit confirmed = game.guestGame.GetUnit(unit.id);
		Assert.Equal((predicted.XCoordinate, predicted.YCoordinate), (confirmed.location.XCoordinate, confirmed.location.YCoordinate));
		Assert.Equal(unit.movementPoints.remaining, confirmed.movementPoints.remaining);
		Assert.Equal(stepsLeft, confirmed.path.PathLength());
	}

	[Fact]
	public async Task OrdersWithUnknownOutcomesWaitForTheHost() {
		using Game game = new(fixture);
		await game.Start();
		game.guest.PredictMoves = true;
		Player guestPlayer = game.guestPlayer;
		MapUnit settler = guestPlayer.units.First(u => u.unitType.actions.Contains(UnitAction.BuildCity));
		Tile settlerTile = settler.location;
		int cities = game.guestGame.cities.Count;

		// Founding a city isn't predicted.
		game.Send(new MsgBuildCity(settler, "Guessed"));
		Assert.Equal(cities, game.guestGame.cities.Count);
		Assert.Same(settler, game.guestGame.GetUnit(settler.id));

		// Nor is a step onto a tile out of sight, or onto someone else's unit.
		EngineStorage.gameData = game.guestGame;
		try {
			MapUnit unit = guestPlayer.units.First(u => !u.unitType.actions.Contains(UnitAction.BuildCity));
			Tile tile = unit.location;
			Tile unseen = game.guestGame.map.tiles.First(t => !guestPlayer.tileKnowledge.isActiveTile(t));
			Assert.False(MovePrediction.Predict(new MsgSetUnitPath(unit.id, new TilePath(unseen, new Queue<Tile>([unseen]))) { playerID = guestPlayer.id }));
			Assert.Same(tile, unit.location);

			Tile neighbor = tile.neighbors.Values.First(t => t != Tile.NONE && unit.CanPredictStepTo(t));
			// The guest sees nobody else's units yet, so one of its own
			// changes hands.
			MapUnit stranger = guestPlayer.units.First(u => u != unit);
			stranger.owner = game.guestGame.players.First(p => p != guestPlayer && !p.isBarbarians);
			stranger.location.unitsOnTile.Remove(stranger);
			neighbor.unitsOnTile.Add(stranger);
			stranger.location = neighbor;
			Assert.False(MovePrediction.Predict(new MsgMoveUnit(unit.id, tile.DirectionTo(neighbor)) { playerID = guestPlayer.id }));
			Assert.Same(tile, unit.location);

			// Nor anything for a unit that isn't the player's.
			Assert.False(MovePrediction.Predict(new MsgSetFortification(stranger.id, true) { playerID = guestPlayer.id }));
			Assert.False(stranger.isFortified);
		} finally {
			EngineStorage.gameData = game.hostGame;
		}

		// The host founds the city, and the guest has it from the snapshot.
		game.Settle();
		Assert.Equal(cities + 1, game.guestGame.cities.Count);
		Assert.Contains(game.guestGame.cities, c => c.name == "Guessed"
			&& c.location.XCoordinate == settlerTile.XCoordinate && c.location.YCoordinate == settlerTile.YCoordinate);
	}

	[Fact]
	public async Task TheSnapshotCorrectsAWrongGuess() {
		using Game game = new(fixture);
		await game.Start();
		game.guest.PredictMoves = true;
		(MapUnit unit, Tile to) = PredictableStep(game.guestPlayer);
		Tile from = unit.location;

		// The unit has no moves left on the host, which the guest doesn't know
		// yet, so the host turns the step down.
		MapUnit hostUnit = game.hostGame.GetUnit(unit.id);
		hostUnit.movementPoints.onConsumeAll();
		game.Send(new MsgMoveUnit(unit.id, from.DirectionTo(to)));
		Assert.Same(to, unit.location);

		game.Settle();
		MapUnit corrected = game.guestGame.GetUnit(unit.id);
		Assert.Equal((from.XCoordinate, from.YCoordinate), (corrected.location.XCoordinate, corrected.location.YCoordinate));
		Assert.False(corrected.movementPoints.canMove);
		Assert.Contains(corrected, game.guestGame.map.tileAt(from.XCoordinate, from.YCoordinate).unitsOnTile);
	}

	// Turning an order down leaves the host's game as it was, which the
	// host doesn't usually send again, but the guest has to be told.
	[Fact]
	public async Task TheHostAnswersAWrongGuessThatChangesNothing() {
		using Game game = new(fixture);
		await game.Start();
		game.guest.PredictMoves = true;
		(MapUnit unit, Tile to) = PredictableStep(game.guestPlayer);
		Tile from = unit.location;
		game.Send(new MsgUnitCommand(unit.id, MsgUnitCommand.Command.SkipTurn));
		game.Settle();
		unit = game.guestGame.GetUnit(unit.id);
		to = game.guestGame.map.tileAt(to.XCoordinate, to.YCoordinate);
		Assert.False(unit.movementPoints.canMove);

		// The guest's game goes wrong, as a guess might.
		unit.movementPoints.reset(1);
		int shown = game.snapshotsShown;
		game.Send(new MsgMoveUnit(unit.id, unit.location.DirectionTo(to)));
		Assert.Same(to, unit.location);

		game.Settle();
		Assert.True(game.snapshotsShown > shown);
		MapUnit corrected = game.guestGame.GetUnit(unit.id);
		Assert.Equal((from.XCoordinate, from.YCoordinate), (corrected.location.XCoordinate, corrected.location.YCoordinate));
	}

	[Fact]
	public async Task FortifyingAndSkippingShowAtOnce() {
		using Game game = new(fixture);
		await game.Start();
		game.guest.PredictMoves = true;
		MapUnit fortifier = game.guestPlayer.units.First(u => !u.isFortified);
		game.Send(new MsgSetFortification(fortifier.id, true));
		Assert.True(fortifier.isFortified);
		Assert.Empty(EngineStorage.messagesToUI);

		MapUnit skipper = game.guestPlayer.units.First(u => u != fortifier && u.movementPoints.canMove);
		game.Send(new MsgUnitCommand(skipper.id, MsgUnitCommand.Command.SkipTurn));
		Assert.False(skipper.movementPoints.canMove);

		game.Settle();
		Assert.True(game.guestGame.GetUnit(fortifier.id).isFortified);
		Assert.False(game.guestGame.GetUnit(skipper.id).movementPoints.canMove);
	}

	[Fact]
	public async Task NothingIsPredictedWhenTurnedOff() {
		using Game game = new(fixture);
		await game.Start();
		Assert.False(game.guest.PredictMoves);
		(MapUnit unit, Tile to) = PredictableStep(game.guestPlayer);
		Tile from = unit.location;
		game.Send(new MsgMoveUnit(unit.id, from.DirectionTo(to)));
		Assert.Same(from, unit.location);

		game.Settle();
		MapUnit moved = game.guestGame.GetUnit(unit.id);
		Assert.Equal((to.XCoordinate, to.YCoordinate), (moved.location.XCoordinate, moved.location.YCoordinate));
	}
}

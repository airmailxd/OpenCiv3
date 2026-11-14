using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Which units a stack move (the J key) sends, and that they all go, into a
// rival's territory as anywhere else.
public class StackMoveTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player human;
	private readonly Player ai;

	public StackMoveTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		human = gameData.players.First(p => p.isHuman);
		ai = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		// A city, for some territory to move into.
		CityInteractions.BuildCity(ai.units.First(u => u.unitType.isSettler).location, ai, ai.GetNextCityName());
	}

	private MapUnit Warrior(Tile tile) {
		return gameData.SpawnUnit(human, gameData.unitPrototypes.Single(p => p.name == "Warrior"), tile);
	}

	private static bool IsEmptyLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp
			&& !t.hasGoodyHut && !t.IsImpassable();
	}

	// An empty tile outside anyone's borders, next to an empty one inside
	// the AI's.
	private (Tile outside, Tile inside) FindBorder() {
		return (from outside in gameData.map.tiles
				where IsEmptyLand(outside) && outside.OwningPlayer() == null
				from inside in outside.neighbors.Values
				where IsEmptyLand(inside) && inside.OwningPlayer() == ai
				select (outside, inside)).First();
	}

	// A stack that has already used some of its moves this turn, as one that
	// walked up to the border along a road would have, still goes into the
	// territory beyond together, at war or at peace.
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task AStackThatHasMovedGoesIntoRivalTerritoryTogether(bool atWar) {
		if (atWar) {
			human.DeclareWarOn(ai, gameData.turn);
		}
		(Tile outside, Tile inside) = FindBorder();
		MapUnit leader = Warrior(outside);
		MapUnit follower = Warrior(outside);
		human.tileKnowledge.AddTilesToKnown(outside);
		leader.movementPoints.onUnitMove(1f / 3);
		follower.movementPoints.onUnitMove(1f / 3);

		TilePath leaderPath = PathingAlgorithmChooser.GetAlgorithm(leader).PathFrom(outside, inside, leader);
		var orders = UnitInteractions.StackMoveOrders(leader, leaderPath, inside);

		Assert.Equal([leader, follower], orders.Select(o => o.unit));
		foreach ((MapUnit unit, TilePath path) in orders) {
			await unit.SetUnitPath(path);
		}
		Assert.Equal(inside, leader.location);
		Assert.Equal(inside, follower.location);
	}

	// The rest of a stack's paths wait while its first unit moves. The UI
	// mustn't pick one of those units next, as selecting it would cancel its
	// path once it's handled, leaving it to stop at the end of the turn.
	[Fact]
	public void AUnitWhosePathIsStillWaitingIsntSelectedNext() {
		(Tile outside, Tile inside) = FindBorder();
		MapUnit follower = Warrior(outside);
		EngineStorage.uiControllerID = human.id;
		TilePath path = PathingAlgorithmChooser.GetAlgorithm(follower).PathFrom(outside, inside, follower);

		Assert.Equal(follower, UnitInteractions.getNextSelectedUnit(outside, follower.unitType));
		new MsgSetUnitPath(follower.id, path).send();
		Assert.NotEqual(follower, UnitInteractions.getNextSelectedUnit(outside, follower.unitType));
	}

	// Units out of moves, fortified units and other civs' units stay.
	[Fact]
	public void OnlyOurUnitsThatCanMoveGo() {
		(Tile outside, Tile inside) = FindBorder();
		MapUnit leader = Warrior(outside);
		MapUnit spent = Warrior(outside);
		spent.movementPoints.onConsumeAll();
		MapUnit fortified = Warrior(outside);
		fortified.isFortified = true;
		MapUnit theirs = gameData.SpawnUnit(ai, gameData.unitPrototypes.Single(p => p.name == "Warrior"), outside);
		MapUnit working = gameData.SpawnUnit(human, gameData.unitPrototypes.First(p => p.isWorker), outside);
		working.WorkerJob = gameData.Terraforms[0];
		MapUnit automated = gameData.SpawnUnit(human, gameData.unitPrototypes.First(p => p.isWorker), outside);
		automated.isAutomated = true;

		TilePath leaderPath = PathingAlgorithmChooser.GetAlgorithm(leader).PathFrom(outside, inside, leader);
		var orders = UnitInteractions.StackMoveOrders(leader, leaderPath, inside);

		Assert.Equal([leader], orders.Select(o => o.unit));
	}

	// Ctrl-J (or Ctrl-X) sends only the units of the leader's type.
	[Fact]
	public void SameTypeOnlyLeavesTheOtherTypes() {
		(Tile outside, Tile inside) = FindBorder();
		MapUnit leader = Warrior(outside);
		MapUnit another = Warrior(outside);
		MapUnit worker = gameData.SpawnUnit(human, gameData.unitPrototypes.First(p => p.isWorker), outside);

		TilePath leaderPath = PathingAlgorithmChooser.GetAlgorithm(leader).PathFrom(outside, inside, leader);

		Assert.Equal([leader, another], UnitInteractions.StackMoveOrders(leader, leaderPath, inside, sameTypeOnly: true).Select(o => o.unit));
		Assert.Equal([leader, another, worker], UnitInteractions.StackMoveOrders(leader, leaderPath, inside).Select(o => o.unit));
	}
}

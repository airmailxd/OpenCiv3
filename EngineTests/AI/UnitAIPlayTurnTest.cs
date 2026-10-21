using System.Threading.Tasks;
using C7GameData;
using Xunit;
using IUnitAI = C7GameData.UnitAI;

namespace EngineTests.AI;

public class UnitAIPlayTurnTest {
	// A plan that steps back and forth between two tiles for free, as a unit
	// on a railroad can.
	private sealed class ShuttleAI(Tile a, Tile b) : IUnitAI {
		public int steps;

		IUnitAI.MoveResult IUnitAI.PlayTurnImpl(Player player, MapUnit unit) {
			++steps;
			unit.location = unit.location == a ? b : a;
			return IUnitAI.Result.InProgress;
		}

		public string SummarizePlan() => "shuttle";

		public void UpdateOnDeath() { }
	}

	// Steps that cost no movement can't go on forever: an AI trial hung with
	// an explorer doing this.
	[Fact]
	public async Task FreeStepsBackAndForthEndTheTurn() {
		Tile a = new(ID.None("a"));
		Tile b = new(ID.None("b"));
		MapUnit unit = new(ID.None("unit")) { unitType = new UnitPrototype { movement = 1 }, location = a };
		unit.movementPoints.reset(1);
		ShuttleAI ai = new(a, b);

		IUnitAI.Result result = await ((IUnitAI)ai).PlayTurn(new Player(), unit);

		Assert.Equal(IUnitAI.Result.InProgress, result);
		Assert.InRange(ai.steps, 1, 1000);
	}
}

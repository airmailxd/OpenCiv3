using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class BlitzTest {
	private static MapUnit MakeUnit(bool blitz) {
		MapUnit unit = new(ID.None("unit")) {
			unitType = new UnitPrototype() { attack = 1, movement = 3, hasBlitz = blitz },
		};
		return unit;
	}

	[Fact]
	public void UnitWithoutBlitzCanOnlyAttackOncePerTurn() {
		MapUnit unit = MakeUnit(blitz: false);
		Assert.True(unit.CanAttackAgainThisTurn());

		unit.hasAttackedThisTurn = true;
		Assert.False(unit.CanAttackAgainThisTurn());

		unit.OnBeginTurn(skipTurn: true);
		Assert.True(unit.CanAttackAgainThisTurn());
	}

	[Fact]
	public void UnitWithBlitzCanAttackAgain() {
		MapUnit unit = MakeUnit(blitz: true);
		unit.hasAttackedThisTurn = true;
		Assert.True(unit.CanAttackAgainThisTurn());
	}
}

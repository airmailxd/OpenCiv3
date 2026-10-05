using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class MovementPointsTest {
	[Theory]
	[InlineData(0f, "0")]
	[InlineData(2f, "2")]
	[InlineData(1f / 3f, "(1/3)")]
	[InlineData(2f / 3f, "(2/3)")]
	[InlineData(0.4f, "(2/5)")]
	[InlineData(0.1f, "(1/10)")]
	[InlineData(0.75f, "(3/4)")]
	[InlineData(1.5f, "(1 1/2)")]
	[InlineData(2f + 1f / 3f, "(2 1/3)")]
	public void MixedNumberShowsTheRightFraction(float remaining, string expected) {
		MovementPoints mp = new();
		mp.reset(remaining);
		Assert.Equal(expected, mp.getMixedNumber());
	}
}

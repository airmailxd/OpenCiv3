using C7GameData;
using Xunit;

namespace EngineTests.GameData;

public class GovernmentRateCapTest {
	private static Player MakePlayer(int rateCap, int tax, int science, int luxury) {
		return new Player() {
			government = new Government() { rateCap = rateCap },
			taxRate = tax,
			scienceRate = science,
			luxuryRate = luxury,
		};
	}

	[Fact]
	public void SlidersAboveTheCapMoveToSlidersWithRoom() {
		Player player = MakePlayer(rateCap: 6, tax: 0, science: 10, luxury: 0);

		player.ApplyGovernmentRateCap();

		Assert.Equal(6, player.scienceRate);
		Assert.Equal(4, player.taxRate);
		Assert.Equal(0, player.luxuryRate);
	}

	[Fact]
	public void TaxOverflowsIntoScienceThenLuxury() {
		Player player = MakePlayer(rateCap: 4, tax: 10, science: 0, luxury: 0);

		player.ApplyGovernmentRateCap();

		Assert.Equal(4, player.taxRate);
		Assert.Equal(4, player.scienceRate);
		Assert.Equal(2, player.luxuryRate);
	}

	[Fact]
	public void SlidersWithinTheCapAreUnchanged() {
		Player player = MakePlayer(rateCap: 10, tax: 3, science: 7, luxury: 0);

		player.ApplyGovernmentRateCap();

		Assert.Equal(3, player.taxRate);
		Assert.Equal(7, player.scienceRate);
		Assert.Equal(0, player.luxuryRate);
	}

	[Fact]
	public void MaxRateFollowsTheGovernment() {
		Assert.Equal(7, MakePlayer(rateCap: 7, tax: 5, science: 5, luxury: 0).maxScienceRate);
		Assert.Equal(10, new Player().maxRate);
	}

}

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

	[Fact]
	public void BankruptcyWithTaxAtTheCapDoesNotStopTheGame() {
		C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty() };
		C7Engine.EngineStorage.InitializeGameDataForTests(gameData);

		// A city paying maintenance it can't afford, with tax already at a
		// cap of 40% so nothing can be moved into it.
		Player player = MakePlayer(rateCap: 4, tax: 4, science: 4, luxury: 2);
		player.civilization = new Civilization();
		player.rules = new Rules();
		City city = new(Tile.NONE, player, "Broke", ID.None("city"));
		player.cities.Add(city);
		city.AddBuilding(new Building(new C7GameData.Save.SaveBuilding() { name = "Costly", maintenanceCost = 5 }, gameData));
		gameData.players.Add(player);

		player.DoPerTurnFinanceUpdates(gameData);

		Assert.Equal(0, player.gold);
		Assert.Equal(4, player.taxRate);
	}
}

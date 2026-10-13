using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using static C7Engine.MsgChangeSliders;

namespace EngineTests.GameData;

// The Domestic advisor's sliders ask the engine for a rate with
// MsgChangeSliders. In a standalone release they once didn't change
// anything, and were back where they were when the advisor was opened again
// (owner's report, 2026-10-12). That was the advisor screen failing to build
// (see DomesticAdvisor.CreateUI); these check the engine's side.
public class SliderMessageTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;
	private readonly ID previousActivePlayer;

	public SliderMessageTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		player.taxRate = 5;
		player.scienceRate = 5;
		player.luxuryRate = 0;
		player.hasPlayedThisTurn = false;
		previousActivePlayer = EngineStorage.activePlayerID;
		EngineStorage.activePlayerID = player.id;
	}

	public void Dispose() {
		EngineStorage.activePlayerID = previousActivePlayer;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private void Send(DomesticPolicyChoice choice, int? target = null) {
		new MsgChangeSliders(choice, target) { playerID = player.id }.process();
	}

	private int Commerce() {
		CommerceBreakdown c = city.CurrentCommerceYield();
		return c.taxes + c.beakers + c.happiness;
	}

	[Fact]
	public void MovingTheScienceSliderChangesTheCitysBeakers() {
		Assert.True(Commerce() > 0);

		Send(DomesticPolicyChoice.MoreScience, 10);
		Assert.Equal((0, 10, 0), (player.taxRate, player.scienceRate, player.luxuryRate));
		Assert.Equal(Commerce(), city.CurrentCommerceYield().beakers);

		Send(DomesticPolicyChoice.LessScience, 0);
		Assert.Equal((10, 0, 0), (player.taxRate, player.scienceRate, player.luxuryRate));
		Assert.Equal(0, city.CurrentCommerceYield().beakers);
	}

	[Fact]
	public void MovingTheLuxurySliderChangesTheCitysLuxuries() {
		Send(DomesticPolicyChoice.MoreLuxury, 10);
		Assert.Equal((0, 0, 10), (player.taxRate, player.scienceRate, player.luxuryRate));
		Assert.Equal(Commerce(), city.CurrentCommerceYield().happiness);
		Assert.True(city.CurrentCommerceYield().happiness > 0);
	}

	[Fact]
	public void ThePlusAndMinusButtonsMoveOneStep() {
		Send(DomesticPolicyChoice.MoreLuxury);
		Assert.Equal((4, 5, 1), (player.taxRate, player.scienceRate, player.luxuryRate));
		Send(DomesticPolicyChoice.LessScience);
		Assert.Equal((5, 4, 1), (player.taxRate, player.scienceRate, player.luxuryRate));
	}

	[Fact]
	public void AChangeTellsTheUIToShowTheNewRates() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
		Send(DomesticPolicyChoice.MoreScience, 7);
		bool told = false;
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			told |= msg is MsgUpdateUiAfterDomesticChange;
		}
		Assert.True(told);
	}

	// A government's rate cap comes from the BIQ (Government.rateCap); a
	// slider asked to go past it stops there.
	[Fact]
	public void TheSliderStopsAtTheGovernmentsRateCap() {
		Government capped = new() { rateCap = 6 };
		player.government = capped;
		Send(DomesticPolicyChoice.MoreScience, 10);
		Assert.Equal((4, 6, 0), (player.taxRate, player.scienceRate, player.luxuryRate));
	}

	[Fact]
	public void TheRatesSurviveSaveAndLoad() {
		Send(DomesticPolicyChoice.MoreScience, 7);
		Send(DomesticPolicyChoice.MoreLuxury, 2);
		Assert.Equal((1, 7, 2), (player.taxRate, player.scienceRate, player.luxuryRate));
		int beakers = city.CurrentCommerceYield().beakers;

		SaveGame saved = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		C7GameData.GameData loaded = saved.ToGameData(fixture.behaviors);

		Player reloaded = loaded.GetPlayer(player.id);
		Assert.Equal((1, 7, 2), (reloaded.taxRate, reloaded.scienceRate, reloaded.luxuryRate));
		Assert.Equal(beakers, loaded.cities.Single(c => c.id == city.id).CurrentCommerceYield().beakers);
	}
}

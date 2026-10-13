using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Civ3's City Governor ("Contact Governor"): managing citizen moods keeps a
// human's city out of disorder, and its options are kept in saves.
public class CityGovernorTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;

	public CityGovernorTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	// A human's size 4 city with no luxuries, under a government with high
	// war weariness and no military police, and every citizen on a tile.
	private (Player player, City city) MakeCity() {
		Player player = gameData.players.First(p => p.isHuman && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		CitizenType laborer = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
		for (int i = 0; i < 3; ++i) {
			CityResident resident = new() { city = city, nationality = player.civilization, citizenType = laborer };
			city.AddCitizen(resident);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, resident);
		}
		player.government = gameData.governments.First(g => g.warWeariness == 2);
		player.taxRate += player.luxuryRate;
		player.luxuryRate = 0;
		Assert.All(city.residents, r => Assert.True(r.citizenType.IsDefaultCitizen));
		Assert.Equal(City.Mood.Happy, city.RecalculateCitizenMoods(gameData));
		return (player, city);
	}

	// Raises the player's war weariness, a level at a time, until the city
	// would riot.
	private static void MakeWarWeary(C7GameData.GameData gameData, Player player, City city) {
		foreach (int points in new[] { 31, 61, 91, 121 }) {
			player.warWeariness = points;
			if (city.RecalculateCitizenMoods(gameData) == City.Mood.Unhappy) {
				return;
			}
		}
		Assert.Fail("War weariness didn't make the city riot");
	}

	private static int Entertainers(City city) => city.residents.Count(r => !r.citizenType.IsDefaultCitizen && r.citizenType.Luxuries > 0);

	[Fact]
	public void GovernorKeepsWarWearyCityOutOfDisorder() {
		(Player player, City city) = MakeCity();
		city.governor.manageMoods = true;
		MakeWarWeary(gameData, player, city);

		// What the turn does before deciding disorder.
		player.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);

		Assert.False(city.isInCivilDisorder);
		Assert.True(Entertainers(city) > 0);
		Assert.All(city.residents.Where(r => !r.citizenType.IsDefaultCitizen), r => Assert.Equal(Tile.NONE, r.tileWorked));
	}

	[Fact]
	public void WithoutGovernorWarWearyCityRiots() {
		(Player player, City city) = MakeCity();
		MakeWarWeary(gameData, player, city);

		player.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);

		Assert.True(city.isInCivilDisorder);
		Assert.Equal(0, Entertainers(city));
	}

	[Fact]
	public void EntertainersGoBackToWorkOnceNotNeeded() {
		(Player player, City city) = MakeCity();
		city.governor.manageMoods = true;
		MakeWarWeary(gameData, player, city);
		player.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);
		Assert.True(Entertainers(city) > 0);

		// Peace: the weariness is gone.
		player.warWeariness = 0;
		player.RecalculateCitizenMoods(gameData, goIntoDisorderIfUnhappy: true);

		Assert.Equal(0, Entertainers(city));
		Assert.False(city.isInCivilDisorder);
	}

	[Fact]
	public void GrowingCityWithGovernorDoesntRiot() {
		(Player player, City city) = MakeCity();
		city.governor.manageMoods = true;
		MakeWarWeary(gameData, player, city);
		CityTileAssignmentAI.GovernMoods(gameData, city);
		Assert.Equal(City.Mood.Happy, city.RecalculateCitizenMoods(gameData));

		city.foodStored = city.FoodNeededToGrow() - city.FoodGrowthPerTurn();
		city.HandleCityGrowth(gameData);

		Assert.Equal(5, city.residents.Count);
		Assert.Equal(City.Mood.Happy, city.RecalculateCitizenMoods(gameData));
	}

	[Fact]
	public void GovernorSurvivesSaveAndLoad() {
		(_, City city) = MakeCity();
		city.governor = new CityGovernor { manageMoods = true, emphasizeProduction = true };

		C7GameData.GameData loaded = CreateGame.ReplaceWithSnapshot(SaveGame.FromGameData(gameData), fixture.behaviors);

		CityGovernor governor = loaded.cities.Single(c => c.id == city.id).governor;
		Assert.True(governor.manageMoods);
		Assert.True(governor.emphasizeProduction);
		Assert.False(governor.emphasizeFood);
		Assert.False(governor.emphasizeCommerce);
	}

	// The governor is set through the engine, as a LAN client's would be,
	// and can be set for all the player's cities at once.
	[Fact]
	public void MessageSetsGovernorOfAllCities() {
		(Player player, City city) = MakeCity();
		MakeWarWeary(gameData, player, city);

		ID previous = EngineStorage.activePlayerID;
		EngineStorage.activePlayerID = player.id;
		try {
			Assert.True(new MsgSetCityGovernor(city, new CityGovernor { manageMoods = true }, GovernorScope.AllCities) { playerID = player.id }.process());
		} finally {
			EngineStorage.activePlayerID = previous;
		}

		Assert.All(player.cities, c => Assert.True(c.governor.manageMoods));
		// It sees to the city's moods at once.
		Assert.True(Entertainers(city) > 0);
		Assert.Equal(City.Mood.Happy, city.RecalculateCitizenMoods(gameData));
	}

	[Fact]
	public void MessageForSomeoneElsesCityIsIgnored() {
		(Player player, City city) = MakeCity();
		Player other = gameData.players.First(p => p != player && !p.isBarbarians);

		ID previous = EngineStorage.activePlayerID;
		EngineStorage.activePlayerID = other.id;
		try {
			new MsgSetCityGovernor(city, new CityGovernor { manageMoods = true }) { playerID = other.id }.process();
		} finally {
			EngineStorage.activePlayerID = previous;
		}

		Assert.False(city.governor.manageMoods);
	}

	// "Capital Governor is Default for New Cities": the founder's preference
	// comes with the order to build the city.
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void NewCityTakesCapitalsGovernorIfPreferred(bool preferred) {
		(Player player, City capital) = MakeCity();
		capital.capital = true;
		capital.governor = new CityGovernor { manageMoods = true, emphasizeFood = true };
		MapUnit settler = null;
		foreach (Tile t in gameData.map.tiles.Where(t => t.IsLand() && t.cityAtTile == null)
				.OrderBy(t => System.Math.Abs(t.DistanceTo(capital.location) - 6))) {
			settler = gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Settler"), t);
			if (settler.canBuildCity()) {
				break;
			}
			settler.RemoveFromPlay();
			settler = null;
		}
		Assert.NotNull(settler);

		ID previous = EngineStorage.activePlayerID;
		EngineStorage.activePlayerID = player.id;
		try {
			Assert.True(new MsgBuildCity(settler, "Governed") { playerID = player.id, capitalGovernorIsDefault = preferred }.process());
		} finally {
			EngineStorage.activePlayerID = previous;
		}

		City city = player.cities.Single(c => c.name == "Governed");
		Assert.Equal(preferred, city.governor.manageMoods);
		Assert.Equal(preferred, city.governor.emphasizeFood);
		Assert.NotSame(capital.governor, city.governor);
	}

	// With an emphasis, citizens placed afresh favour that yield.
	[Fact]
	public void EmphasizingCommerceFavoursCommerce() {
		(_, City city) = MakeCity();
		int Commerce() => city.residents.Where(r => r.tileWorked != Tile.NONE).Sum(r => r.tileWorked.CommerceYield(city.owner).yield);
		int Production() => city.residents.Where(r => r.tileWorked != Tile.NONE).Sum(r => r.tileWorked.ProductionYield(city.owner).yield);

		CityInteractions.SetGovernor(gameData, city, new CityGovernor { emphasizeProduction = true });
		int productionWhenProductionEmphasized = Production();
		int commerceWhenProductionEmphasized = Commerce();
		CityInteractions.SetGovernor(gameData, city, new CityGovernor { emphasizeCommerce = true });

		Assert.True(Commerce() >= commerceWhenProductionEmphasized);
		Assert.True(Production() <= productionWhenProductionEmphasized);
	}
}

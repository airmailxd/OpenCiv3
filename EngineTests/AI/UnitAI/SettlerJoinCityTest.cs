using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI.UnitAI;

// When the AI's settlers may join a city. Civ3 refuses a unit joining a city
// at size 6 without fresh water or an aqueduct, at size 12 without a
// hospital, or a starving city
// (https://forums.civfanatics.com/threads/cant-get-settler-to-join-city.68722/).
public class SettlerJoinCityTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public SettlerJoinCityTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
	}

	[Fact]
	public void ASettlerJoinsAGrowingCityWithRoom() {
		Assert.True(city.FoodGrowthPerTurn() >= 0);
		Assert.True(SettlerAI.HasRoomToJoin(city, 2, player));
	}

	[Fact]
	public void ASettlerDoesntJoinPastTheFirstSizeCap() {
		int room = player.rules.MaximumLevel1CitySize - city.residents.Count;
		Assert.False(SettlerAI.HasRoomToJoin(city, room + 1, player));
	}

	[Fact]
	public void ASettlerDoesntJoinAStarvingCity() {
		CitizenType defaultCitizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
		// Citizens who work no tile eat without adding food.
		while (city.FoodGrowthPerTurn() >= 0) {
			city.AddCitizen(new CityResident { citizenType = defaultCitizen, nationality = player.civilization, city = city });
		}
		Assert.True(city.residents.Count + 1 <= player.rules.MaximumLevel1CitySize, "the city starves before reaching the size cap");

		Assert.False(SettlerAI.HasRoomToJoin(city, 1, player));
	}
}

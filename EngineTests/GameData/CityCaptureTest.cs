using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class CityCaptureTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player attacker;
	private readonly Player defender;

	public CityCaptureTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		attacker = civs[0];
		defender = civs[1];
	}

	// Captures send messages to the UI; don't leave them for other tests.
	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City FoundCity(Player player, int size) {
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
			});
		}
		return city;
	}

	[Fact]
	public void CapturedCityChangesHandsAndShrinks() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 3);
		Assert.True(city.IsCapital());

		CityInteractions.CaptureCity(city, attacker);

		Assert.Equal(attacker, city.owner);
		Assert.Contains(city, attacker.cities);
		Assert.DoesNotContain(city, defender.cities);
		Assert.Equal(2, city.residents.Count);
		Assert.False(city.IsCapital());
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building.isCenterOfEmpire);
		Assert.Equal(city, city.location.cityAtTile);
	}

	[Fact]
	public void CapturingASizeOneCityDestroysIt() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 1);
		Tile tile = city.location;

		CityInteractions.CaptureCity(city, attacker);

		Assert.Null(tile.cityAtTile);
		Assert.DoesNotContain(city, attacker.cities);
		Assert.DoesNotContain(city, gameData.cities);
	}

	// A captured city comes without a palace, so a civ whose only city was
	// captured gets its palace in the first city it founds, which then has
	// no corruption.
	[Fact]
	public void FirstFoundedCityAfterACaptureIsTheCapital() {
		City captured = FoundCity(defender, 3);
		CityInteractions.CaptureCity(captured, attacker);
		Assert.False(captured.IsCapital());

		City founded = FoundCity(attacker, 1);

		Assert.True(founded.IsCapital());
		Assert.Contains(founded.constructed_buildings, cb => cb.building.isCenterOfEmpire);
		Assert.Equal(0, founded.corruption);
		Assert.Equal(0, founded.CurrentProductionYield().corrupt);
	}

	[Fact]
	public void LosingTheCapitalMovesThePalace() {
		FoundCity(attacker, 1);
		City capital = FoundCity(defender, 3);
		Tile secondSite = defender.units.First(u => u.unitType.isSettler).location.neighbors.Values
			.First(t => t.IsLand() && !t.HasCity());
		City other = CityInteractions.BuildCity(secondSite, defender, defender.GetNextCityName());
		Assert.False(other.IsCapital());

		CityInteractions.CaptureCity(capital, attacker);

		Assert.True(other.IsCapital());
		Assert.Contains(other.constructed_buildings, cb => cb.building.isCenterOfEmpire);
	}

	[Fact]
	public void RebuiltCityContinuesDownTheNameList() {
		FoundCity(attacker, 1);
		City capital = FoundCity(defender, 3);
		Tile secondSite = defender.units.First(u => u.unitType.isSettler).location.neighbors.Values
			.First(t => t.IsLand() && !t.HasCity());
		City other = CityInteractions.BuildCity(secondSite, defender, defender.GetNextCityName());

		CityInteractions.CaptureCity(capital, attacker);

		Assert.Equal(defender.civilization.cityNames[2], defender.GetNextCityName());
		Assert.NotEqual(capital.name, defender.GetNextCityName());
		Assert.NotEqual(other.name, defender.GetNextCityName());
	}
}

using System.Collections.Generic;
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

	[Fact]
	public void SizeOneCityWithExpandedBordersIsKeptWithTheCaptorsBorders() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 1);
		city.perPlayerCulture[defender] = 25;
		gameData.UpdateTileOwners();
		Assert.Equal(2, city.GetBorderExpansionLevel());
		List<Tile> outerRing = city.GetTilesWithinBorders().Where(t => t != Tile.NONE && t.owningCity == city).ToList();

		CityInteractions.CaptureCity(city, attacker);
		List<Tile> innerTiles = city.GetTilesWithinBorders();
		outerRing.RemoveAll(innerTiles.Contains);
		Assert.NotEmpty(outerRing);
		Assert.All(outerRing, t => Assert.NotEqual(city, t.owningCity));

		Assert.Equal(attacker, city.owner);
		Assert.Equal(city, city.location.cityAtTile);
		Assert.Single(city.residents);
		Assert.Equal(1, city.GetBorderExpansionLevel());
		Assert.Equal(25, city.GetCultureFor(defender));
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

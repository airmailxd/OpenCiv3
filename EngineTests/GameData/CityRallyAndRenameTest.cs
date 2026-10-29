using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Civ3's city "Rename" and rally points, and the palace screen's button.
public class CityRallyAndRenameTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;

	public CityRallyAndRenameTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City MakeCity() {
		Player player = gameData.players.First(p => p.isHuman && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		return CityInteractions.BuildCity(tile, player, player.GetNextCityName());
	}

	private static Tile LandTileAway(City city) =>
		city.location.GetTilesWithinRankDistance(3).First(t => t != city.location && t.IsLand() && !t.IsImpassable()
			&& t.continent == city.location.continent && city.owner.tileKnowledge.knownTiles.Contains(t));

	[Fact]
	public void RenameTrimsAndRefusesEmptyNames() {
		City city = MakeCity();
		string old = city.name;
		Assert.False(city.TryRename("   "));
		Assert.False(city.TryRename(null));
		Assert.Equal(old, city.name);
		Assert.True(city.TryRename("  New Rome  "));
		Assert.Equal("New Rome", city.name);
		Assert.True(city.TryRename(new string('x', 40)));
		Assert.Equal(City.MaxNameLength, city.name.Length);
	}

	[Fact]
	public void RallyPointMustBeKnownLandAwayFromTheCity() {
		City city = MakeCity();
		Assert.NotNull(city.WhyCannotRallyAt(city.location));
		Assert.NotNull(city.WhyCannotRallyAt(null));
		Assert.Null(city.WhyCannotRallyAt(LandTileAway(city)));
	}

	[Fact]
	public void BuiltUnitsHeadForTheRallyPoint() {
		City city = MakeCity();
		Tile rally = LandTileAway(city);
		UnitPrototype warrior = gameData.unitPrototypes.First(u => u.name == "Warrior");
		MapUnit before = city.AddUnit(warrior, gameData);
		city.SendToRallyPoint(before);
		Assert.True(before.path == null || before.path.PathLength() == 0, "no rally point, no orders");

		city.rallyPoint = rally;
		MapUnit after = city.AddUnit(warrior, gameData);
		city.SendToRallyPoint(after);
		Assert.True(after.path?.PathLength() > 0);
		Assert.Equal(rally, after.path.destination);
	}

	[Fact]
	public void RallyPointIsKeptInSaves() {
		City city = MakeCity();
		Tile rally = LandTileAway(city);
		city.rallyPoint = rally;
		SaveCity saved = new(city);
		Assert.NotNull(saved.rallyPoint);
		Assert.Equal(rally.XCoordinate, saved.rallyPoint.Value.X);
		Assert.Equal(rally.YCoordinate, saved.rallyPoint.Value.Y);
		Assert.Equal(city.rallyPoint, rally);
	}

	[Fact]
	public void ContinentalScopeCoversTheCitiesOnTheContinent() {
		City city = MakeCity();
		Assert.Equal([city], CityInteractions.CitiesInRallyScope(city.owner, city, false));
		Assert.Contains(city, CityInteractions.CitiesInRallyScope(city.owner, city, true));
	}

	[Fact]
	public void PalaceScreenNeedsAPalaceOrACityThatMayBuildOne() {
		Player player = gameData.players.First(p => p.isHuman);
		foreach (City c in player.cities.ToList()) {
			c.capital = false;
		}
		City city = MakeCity();
		city.capital = true;
		Assert.True(city.owner.CanStartPalace(gameData));
		Player empty = gameData.players.First(p => p.cities.Count == 0);
		Assert.False(empty.CanStartPalace(gameData));
	}
}

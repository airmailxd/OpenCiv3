using System.Collections.Generic;
using System.Linq;
using System.Text;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class CoreCitiesCorruptionTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player player;

	public CoreCitiesCorruptionTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
	}

	// The capital, then six more cities on the nearest free land around it,
	// in rank order.
	private List<City> FoundCities() {
		Tile capitalTile = player.units.First(u => u.unitType.isSettler).location;
		City capital = CityInteractions.BuildCity(capitalTile, player, player.GetNextCityName());
		IEnumerable<Tile> sites = gameData.map.tiles
			.Where(t => t.IsLand() && !t.HasCity() && t.RankDistanceTo(capitalTile) >= 3)
			.OrderBy(t => t.RankDistanceTo(capitalTile))
			.Take(6);
		foreach (Tile site in sites.ToList()) {
			CityInteractions.BuildCity(site, player, player.GetNextCityName());
		}
		player.DoCorruptionCalculations(gameData);
		return player.cities.OrderBy(c => c.rankIndex).ToList();
	}

	[Fact]
	public void TheFiveCitiesNearestTheCapitalHaveNoCorruption() {
		gameData.rules.CoreCitiesFreeOfCorruption = true;
		List<City> cities = FoundCities();

		Assert.Equal(7, cities.Count);
		Assert.All(cities.Take(6), c => Assert.Equal(0, c.corruption));
		Assert.True(cities[6].corruption > 0);
	}

	[Fact]
	public void WithTheOptionOffNearbyCitiesAreCorrupt() {
		gameData.rules.CoreCitiesFreeOfCorruption = false;
		List<City> cities = FoundCities();

		Assert.All(cities.Skip(1), c => Assert.True(c.corruption > 0));
	}

	[Fact]
	public void TheChoiceAtSetupIsSaved() {
		SaveGame save = fixture.saveGame.Clone();
		new GameSetup { coreCitiesFreeOfCorruption = true, difficulty = save.GameDifficulty }.Populate(save);
		Assert.True(save.Rules.CoreCitiesFreeOfCorruption);

		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		Assert.True(loaded.Rules.CoreCitiesFreeOfCorruption);
	}

	[Fact]
	public void SavesFromBeforeTheOptionKeepCorruption() {
		SaveGame save = fixture.saveGame.Clone();
		save.Rules.CoreCitiesFreeOfCorruption = true;
		string json = Encoding.UTF8.GetString(save.ToCompactJSON());
		Assert.Contains("\"coreCitiesFreeOfCorruption\":true,", json);

		string older = json.Replace("\"coreCitiesFreeOfCorruption\":true,", "");
		Assert.False(SaveGame.FromJSON(Encoding.UTF8.GetBytes(older)).Rules.CoreCitiesFreeOfCorruption);
	}
}

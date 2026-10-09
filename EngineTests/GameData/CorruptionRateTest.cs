using System.Collections.Generic;
using System.Linq;
using System.Text;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The corruption rate chosen when setting up a game scales every city's
// corruption and waste.
public class CorruptionRateTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public CorruptionRateTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	// The corruption of the capital and six more cities on the nearest free
	// land around it, in rank order, at the given rate.
	private List<float> CorruptionAt(float rate) {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.rules.CorruptionRate = rate;
		Player player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile capitalTile = player.units.First(u => u.unitType.isSettler).location;
		CityInteractions.BuildCity(capitalTile, player, player.GetNextCityName());
		IEnumerable<Tile> sites = gameData.map.tiles
			.Where(t => t.IsLand() && !t.HasCity() && t.RankDistanceTo(capitalTile) >= 3)
			.OrderBy(t => t.RankDistanceTo(capitalTile))
			.Take(6);
		foreach (Tile site in sites.ToList()) {
			CityInteractions.BuildCity(site, player, player.GetNextCityName());
		}
		player.DoCorruptionCalculations(gameData);
		return player.cities.OrderBy(c => c.rankIndex).Select(c => c.corruption).ToList();
	}

	[Fact]
	public void TheRateScalesCorruption() {
		List<float> civ3 = CorruptionAt(1.0f);
		List<float> half = CorruptionAt(0.5f);

		Assert.Contains(civ3, c => c > 0);
		for (int i = 0; i < civ3.Count; ++i) {
			// Below the cap, half the rate is half the corruption.
			if (civ3[i] < 0.9f) {
				Assert.Equal(civ3[i] / 2, half[i], 4);
			}
		}
	}

	[Fact]
	public void NewGamesDefaultToNinetyPercent() {
		Assert.Equal(0.9f, new Rules().CorruptionRate);
	}

	[Fact]
	public void TheChoiceAtSetupIsSaved() {
		SaveGame save = fixture.saveGame.Clone();
		new GameSetup { corruptionRate = 1.25f, difficulty = save.GameDifficulty }.Populate(save);
		Assert.Equal(1.25f, save.Rules.CorruptionRate);

		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		Assert.Equal(1.25f, loaded.Rules.CorruptionRate);
	}

	[Fact]
	public void TheChoiceIsKeptInRange() {
		SaveGame save = fixture.saveGame.Clone();
		new GameSetup { corruptionRate = 5f, difficulty = save.GameDifficulty }.Populate(save);
		Assert.Equal(Rules.MaxCorruptionRate, save.Rules.CorruptionRate);
	}

	[Fact]
	public void SavesFromBeforeTheRateUseNinetyPercent() {
		SaveGame save = fixture.saveGame.Clone();
		save.Rules.CorruptionRate = 1.25f;
		string json = Encoding.UTF8.GetString(save.ToCompactJSON());
		Assert.Contains("\"corruptionRate\":1.25,", json);

		string older = json.Replace("\"corruptionRate\":1.25,", "");
		Assert.Equal(0.9f, SaveGame.FromJSON(Encoding.UTF8.GetBytes(older)).Rules.CorruptionRate);
	}
}

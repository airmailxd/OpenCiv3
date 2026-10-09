using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData.Victory;

public class CulturalVictoryTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public CulturalVictoryTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		foreach (Player p in gameData.players) {
			foreach (City c in p.cities) {
				c.perPlayerCulture[p] = 0;
			}
		}
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner, int culture) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		City city = CityInteractions.BuildCity(tile, owner, owner.GetNextCityName());
		city.perPlayerCulture[owner] = culture;
		return city;
	}

	[Fact]
	public void OneCityWithEnoughCultureWins() {
		CulturalVictory victory = new(oneCityWin: 20000, allCitiesWin: 100000);
		BuildCity(us, 19999);
		Assert.False(victory.HasVictory(victory.Evaluate(us, gameData)));

		BuildCity(us, 20000);
		VictoryStatus status = victory.Evaluate(us, gameData);
		Assert.Equal(20000, status.TopCityCulture);
		Assert.True(victory.HasVictory(status));
	}

	[Fact]
	public void TheWholeCivNeedsTheThresholdAndTwiceEveryRival() {
		CulturalVictory victory = new(oneCityWin: 20000, allCitiesWin: 100000);
		for (int i = 0; i < 6; ++i) {
			BuildCity(us, 17000);
		}
		City theirs = BuildCity(them, 51001);

		// 102,000 culture, but not twice the rival's 51,001.
		Assert.False(victory.HasVictory(victory.Evaluate(us, gameData)));

		theirs.perPlayerCulture[them] = 51000;
		Assert.True(victory.HasVictory(victory.Evaluate(us, gameData)));
	}

	[Fact]
	public void TheWholeCivNeedsTheThreshold() {
		CulturalVictory victory = new(oneCityWin: 20000, allCitiesWin: 100000);
		for (int i = 0; i < 5; ++i) {
			BuildCity(us, 19000);
		}
		Assert.False(victory.HasVictory(victory.Evaluate(us, gameData)));
	}

	[Fact]
	public void CulturalVictoryIsRegisteredOnlyWhenAllowed() {
		SaveGame save = fixture.saveGame;
		save.VictoryConditions = VictoryConditions.NewGameDefaults();
		save.VictoryConditions.CultureOneCityWin = 15000;
		CulturalVictory registered = save.ToGameData(fixture.behaviors).victories.OfType<CulturalVictory>().Single();
		Assert.True(registered.HasVictory(new VictoryStatus { TopCityCulture = 15000 }));

		save = fixture.saveGame;
		save.VictoryConditions = new VictoryConditions { AllowCulturalVictory = false };
		Assert.DoesNotContain(save.ToGameData(fixture.behaviors).victories, v => v is CulturalVictory);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[Fact]
	public void DiplomaticVictoryIsRegisteredOnlyWhenAllowed() {
		SaveGame save = fixture.saveGame;
		save.VictoryConditions = VictoryConditions.NewGameDefaults();
		Assert.Contains(save.ToGameData(fixture.behaviors).victories, v => v is DiplomaticVictory);

		save = fixture.saveGame;
		save.VictoryConditions = new VictoryConditions { AllowDiplomaticVictory = false };
		Assert.DoesNotContain(save.ToGameData(fixture.behaviors).victories, v => v is DiplomaticVictory);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[Fact]
	public void ThresholdsSurviveASaveAndOldSavesGetTheDefaults() {
		VictoryConditions conditions = VictoryConditions.NewGameDefaults();
		conditions.CultureOneCityWin = 12345;
		conditions.CultureAllCitiesWin = 54321;
		string json = System.Text.Json.JsonSerializer.Serialize(conditions);
		VictoryConditions loaded = System.Text.Json.JsonSerializer.Deserialize<VictoryConditions>(json);
		Assert.Equal(12345, loaded.CultureOneCityWin);
		Assert.Equal(54321, loaded.CultureAllCitiesWin);

		VictoryConditions old = System.Text.Json.JsonSerializer.Deserialize<VictoryConditions>("{\"AllowCulturalVictory\":true}");
		Assert.Equal(CulturalVictory.DefaultOneCityWin, old.CultureOneCityWin);
		Assert.Equal(CulturalVictory.DefaultAllCitiesWin, old.CultureAllCitiesWin);
	}

	[Fact]
	public void NewGameKeepsTheRulesetsThresholds() {
		VictoryConditions ruleset = new() { CultureOneCityWin = 5000, CultureAllCitiesWin = 7000, DominationTerritoryPercent = 50 };
		VictoryConditions chosen = VictoryConditions.NewGameDefaults().CopyThresholdsFrom(ruleset);
		Assert.True(chosen.AllowCulturalVictory);
		Assert.Equal(5000, chosen.CultureOneCityWin);
		Assert.Equal(7000, chosen.CultureAllCitiesWin);
		Assert.Equal(50, chosen.DominationTerritoryPercent);
	}
	[Fact]
	public void TheWholeCivsThresholdScalesWithTheWorldsSize() {
		Assert.Equal(60000, CulturalVictory.ScaleForWorldSize(100000, 0, 5));
		Assert.Equal(80000, CulturalVictory.ScaleForWorldSize(100000, 1, 5));
		Assert.Equal(100000, CulturalVictory.ScaleForWorldSize(100000, 2, 5));
		Assert.Equal(130000, CulturalVictory.ScaleForWorldSize(100000, 3, 5));
		Assert.Equal(160000, CulturalVictory.ScaleForWorldSize(100000, 4, 5));
		// Unknown world sizes aren't scaled.
		Assert.Equal(100000, CulturalVictory.ScaleForWorldSize(100000, -1, 5));
		Assert.Equal(100000, CulturalVictory.ScaleForWorldSize(100000, 1, 3));
	}

	[Fact]
	public void TheUnitedNationsCanOnlyBeBuiltWithDiplomaticVictory() {
		foreach (Tech t in gameData.techs) {
			us.knownTechs.Add(t.id);
		}
		City city = BuildCity(us, 0);
		Building unitedNations = gameData.Buildings.Single(b => b.allowDiplomaticVictory);
		var resources = gameData.Resources.ToHashSet();

		gameData.victoryConditions = VictoryConditions.NewGameDefaults();
		Assert.True(unitedNations.CanProduce(city, resources));

		gameData.victoryConditions.AllowDiplomaticVictory = false;
		Assert.False(unitedNations.CanProduce(city, resources));
	}
}

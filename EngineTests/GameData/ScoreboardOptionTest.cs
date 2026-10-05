using System.Text;
using C7Engine;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class ScoreboardOptionTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public ScoreboardOptionTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Fact]
	public void NewGamesShowTheScoreboardByDefault() {
		Assert.True(fixture.saveGame.Rules.ShowScoreboard);
	}

	[Fact]
	public void TheChoiceAtSetupIsSaved() {
		SaveGame save = fixture.saveGame.Clone();
		new GameSetup { showScoreboard = false, difficulty = save.GameDifficulty }.Populate(save);
		Assert.False(save.Rules.ShowScoreboard);

		SaveGame loaded = SaveGame.FromJSON(save.ToCompactJSON());
		Assert.False(loaded.Rules.ShowScoreboard);
	}

	[Fact]
	public void SavesFromBeforeTheOptionShowTheScoreboard() {
		SaveGame save = fixture.saveGame.Clone();
		save.Rules.ShowScoreboard = false;
		string json = Encoding.UTF8.GetString(save.ToCompactJSON());
		Assert.Contains("\"showScoreboard\":false,", json);

		string older = json.Replace("\"showScoreboard\":false,", "");
		Assert.True(SaveGame.FromJSON(Encoding.UTF8.GetBytes(older)).Rules.ShowScoreboard);
	}
}

using System.Linq;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Anarchy has "No maintenance costs or unit upkeep needed"
// (https://civfanatics.com/civ3/civilopedia/governments/); every other
// government charges building upkeep.
public class GovernmentMaintenanceTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public GovernmentMaintenanceTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Fact]
	public void OnlyAnarchyIsMaintenanceFree() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Assert.All(gameData.governments, g => Assert.Equal(!g.transitionType, g.RequiresMaintenance()));
		Assert.Contains(gameData.governments, g => g.transitionType);
	}

	[Fact]
	public void OnlyAnarchyIsMaintenanceFreeInStandaloneMode() {
		C7GameData.GameData gameData = fixture.standaloneSaveGame.ToGameData(fixture.behaviors);
		Assert.All(gameData.governments, g => Assert.Equal(!g.transitionType, g.RequiresMaintenance()));
		Assert.All(gameData.governments, g => Assert.NotNull(g.requiresMaintenance));
	}
}

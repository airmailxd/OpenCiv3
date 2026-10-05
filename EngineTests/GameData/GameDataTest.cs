using System;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class GameDataTest : RemoteSaveLoader, IClassFixture<SaveGameFixture> {
	C7GameData.GameData gameData;
	private BehaviorEngine behaviorEngine;

	public GameDataTest(SaveGameFixture fixture) {
		this.behaviorEngine = fixture.behaviors;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);

		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[SkippableFact]
	public async Task SeedInGameData_SAV() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		(SaveGame game, Exception ex, string savePath) = await LoadGameAndData(RemoteSaves.Conquests16PlayersSav);
		Assert.Null(ex);
		Assert.NotNull(game);

		C7GameData.GameData gd = game.ToGameData(behaviorEngine);
		EngineStorage.InitializeGameDataForTests(gd);

		Assert.Equal(33127520, game.Seed);
		Assert.Equal(33127520, gd.seed);
	}

	[SkippableFact]
	public async Task SeedInGameData_JSON() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		(SaveGame game, Exception ex, string savePath) = await LoadGameAndData(RemoteSaves.Conquests16PlayersJson);
		Assert.Null(ex);
		Assert.NotNull(game);

		C7GameData.GameData gd = game.ToGameData(behaviorEngine);
		EngineStorage.InitializeGameDataForTests(gd);

		Assert.Equal(-1, game.Seed);
		Assert.NotEqual(-1, gd.seed);
	}

	[Fact]
	public void SeedInGameData_Default() {
		C7GameData.GameData gd = new C7GameData.GameData();
		EngineStorage.InitializeGameDataForTests(gd);

		Assert.NotEqual(-1, gd.seed);
	}

	[Fact]
	public void SeedInGameData_Custom() {
		C7GameData.GameData gd = new C7GameData.GameData(654972132);
		EngineStorage.InitializeGameDataForTests(gd);

		Assert.Equal(654972132, gd.seed);
	}
}

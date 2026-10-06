using System.Linq;
using System.Text.RegularExpressions;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Free techs from Philosophy or Theory of Evolution are kept until the player
// chooses something to research, so they have to survive a save, and a host
// snapshot for LAN clients.
public class FreeTechSaveTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;

	public FreeTechSaveTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	[Fact]
	public void FreeTechsSurviveSaveAndLoad() {
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.freeTechsRemaining = 2;

		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);

		Assert.Equal(2, loaded.players.Single(p => p.id == player.id).freeTechsRemaining);
	}

	[Fact]
	public void FreeTechsSurviveASnapshot() {
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.freeTechsRemaining = 1;

		C7GameData.GameData loaded = CreateGame.ReplaceWithSnapshot(SaveGame.FromGameData(gameData), fixture.behaviors);

		Assert.Equal(1, loaded.players.Single(p => p.id == player.id).freeTechsRemaining);
	}

	[Fact]
	public void SavesWithoutFreeTechsStillLoad() {
		Player player = gameData.players.First(p => !p.isBarbarians);
		player.freeTechsRemaining = 3;
		string json = System.Text.Encoding.UTF8.GetString(SaveGame.FromGameData(gameData).ToCompactJSON());
		string oldJson = Regex.Replace(json, @"""freeTechsRemaining"":\d+,?", "");
		Assert.DoesNotContain("freeTechsRemaining", oldJson);

		C7GameData.GameData loaded = SaveGame.LoadFromJSON(oldJson).ToGameData(fixture.behaviors);

		Assert.Equal(0, loaded.players.Single(p => p.id == player.id).freeTechsRemaining);
	}
}

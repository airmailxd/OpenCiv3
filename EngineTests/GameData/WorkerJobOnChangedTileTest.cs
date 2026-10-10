using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// A worker job whose improvement can no longer go on its tile when it
// finishes, as when a city was founded there meanwhile, comes to nothing,
// rather than throwing out of the turn loop and stopping the game.
public class WorkerJobOnChangedTileTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;

	public WorkerJobOnChangedTileTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[Fact]
	public void IrrigationFinishedOnACityTileIsDropped() {
		Terraform irrigate = gameData.Terraforms.First(t => t.Improvement?.key == "irrigation");
		Player player = gameData.players.First(p => !p.isBarbarians);
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && t.cityAtTile == null && !t.overlays.HasBeenImproved());
		Assert.True(tile.overlays.CanAdd(tile, irrigate.Improvement));

		tile.cityAtTile = new City(tile, player, "Founded meanwhile", ID.None("city"));
		try {
			tile.FinishWorkerJob(irrigate);
			Assert.Null(tile.overlays.ImprovementAtLayer(irrigate.Improvement.layer));
		} finally {
			tile.cityAtTile = null;
		}
	}
}

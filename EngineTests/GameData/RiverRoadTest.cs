using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.AIData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Roads don't carry units over rivers before Engineering ("{New Ability}
// Roads now bridge rivers", Civilopedia TECH_Engineering), whether the step
// crosses a river along a tile edge or through a tile corner, and even into a
// city (owner's decision, 2026-10-21).
public class RiverRoadTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;

	public RiverRoadTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		// An AI, which knows the map (a human's unexplored tiles cost 1).
		us = gameData.players.First(p => !p.isBarbarians && !p.isHuman);
		Tech bridges = Tech.FindBridgeTech(gameData.techs);
		us.knownTechs.Remove(bridges.id);
	}

	private static void ClearRivers(Tile t) {
		t.riverNorth = t.riverNortheast = t.riverEast = t.riverSoutheast = false;
		t.riverSouth = t.riverSouthwest = t.riverWest = t.riverNorthwest = false;
	}

	// A land tile with land to its north, northeast and northwest, all with
	// roads and no rivers.
	private (Tile from, Tile to) RoadedCorner() {
		TerrainImprovement road = gameData.terrainImprovements.Single(ti => ti.key == Tile.TileOverlays.ROAD);
		foreach (Tile from in gameData.map.tiles.Where(t => t.IsLand() && !t.HasCity())) {
			Tile to = from.neighbors.TryGetValue(TileDirection.NORTH, out Tile n) ? n : null;
			Tile left = from.neighbors.TryGetValue(TileDirection.NORTHWEST, out Tile l) ? l : null;
			Tile right = from.neighbors.TryGetValue(TileDirection.NORTHEAST, out Tile r) ? r : null;
			if (to == null || left == null || right == null || !to.IsLand() || !left.IsLand() || !right.IsLand() || to.HasCity()) {
				continue;
			}
			foreach (Tile t in new[] { from, to, left, right }) {
				ClearRivers(t);
			}
			from.overlays.Add(road);
			to.overlays.Add(road);
			return (from, to);
		}
		throw new System.Exception("No site found");
	}

	[Fact]
	public void ARiverThroughTheCornerStopsTheRoad() {
		(Tile from, Tile to) = RoadedCorner();
		float road = TilePath.RoadMovementCost();
		Assert.Equal(road, TilePath.GetMovementCost(us, from, TileDirection.NORTH, to), 3);

		// A river along both of our northern edges runs through the corner.
		from.riverNortheast = from.riverNorthwest = true;
		Assert.True(from.HasRiverCrossing(TileDirection.NORTH));
		Assert.Equal(to.MovementCost(), TilePath.GetMovementCost(us, from, TileDirection.NORTH, to), 3);

		// With Engineering the road bridges it.
		us.knownTechs.Add(Tech.FindBridgeTech(gameData.techs).id);
		Assert.Equal(road, TilePath.GetMovementCost(us, from, TileDirection.NORTH, to), 3);
	}

	[Fact]
	public void ARiverRoundOneSideTileLeavesTheWayOpen() {
		(Tile from, Tile to) = RoadedCorner();
		// Along our northwest edge and the far tile's southwest one: around
		// the northwest tile only.
		from.riverNorthwest = true;
		to.riverSouthwest = true;
		Assert.False(from.HasRiverCrossing(TileDirection.NORTH));
		Assert.Equal(TilePath.RoadMovementCost(), TilePath.GetMovementCost(us, from, TileDirection.NORTH, to), 3);
	}

	[Fact]
	public void TheRiverStopsARoadIntoACity() {
		(Tile from, Tile to) = RoadedCorner();
		CityInteractions.BuildCity(to, us, "Riverbank");
		// Our northwest edge and the far tile's southeast one cross the
		// corner diagonally.
		from.riverNorthwest = true;
		to.riverSoutheast = true;
		Assert.True(from.HasRiverCrossing(TileDirection.NORTH));
		Assert.Equal(to.MovementCost(), TilePath.GetMovementCost(us, from, TileDirection.NORTH, to), 3);
	}
}

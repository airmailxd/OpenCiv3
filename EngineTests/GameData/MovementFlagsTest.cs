using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Civ3's movement flags: wheeled units, foot soldiers, all terrain as roads
// and immobile units
// (https://forums.civfanatics.com/threads/is-there-a-guide-on-unit-abilities.57822/).
public class MovementFlagsTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;

	public MovementFlagsTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		us = gameData.players.First(p => !p.isBarbarians && !p.isHuman);
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.HasRoad();
	}

	// Two neighboring empty land tiles outside anyone's borders. The second
	// is made the given terrain.
	private (Tile from, TileDirection dir, Tile to) FindAdjacentLand(string terrain) {
		foreach (Tile from in gameData.map.tiles.Where(t => IsEmptyLand(t) && t.OwningPlayer() == null)) {
			foreach ((TileDirection dir, Tile to) in from.neighbors) {
				if (to != Tile.NONE && IsEmptyLand(to) && to.OwningPlayer() == null) {
					TerrainType type = gameData.terrainTypes.Single(t => t.Key == terrain);
					to.baseTerrainType = type;
					to.overlayTerrainType = type;
					return (from, dir, to);
				}
			}
		}
		throw new System.Exception("No adjacent land tiles found");
	}

	private MapUnit Spawn(string prototype, Tile tile) {
		return gameData.SpawnUnit(us, gameData.unitPrototypes.Single(p => p.name == prototype), tile);
	}

	private TerrainImprovement Road() {
		return gameData.terrainImprovements.Single(i => i.key == Tile.TileOverlays.ROAD);
	}

	private static TilePath PathFor(MapUnit unit, Tile to) {
		return PathingAlgorithmChooser.GetAlgorithm(unit).PathFrom(unit.location, to, unit);
	}

	// Wheeled units "cannot cross mountains or jungle tiles without a road"
	// (https://sullla.com/Civ3/CQ/CQ_units.html).
	[Fact]
	public async Task ACatapultCantEnterMountainsWithoutARoad() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand("mountains");
		MapUnit catapult = Spawn("Catapult", from);

		Assert.True(catapult.unitType.isWheeled);
		Assert.False(catapult.CanEnter(to));
		Assert.Equal(0, PathFor(catapult, to).PathLength());
		Assert.False(await catapult.Move(dir));
		Assert.Equal(from, catapult.location);

		// A warrior isn't wheeled.
		MapUnit warrior = Spawn("Warrior", from);
		Assert.True(await warrior.Move(dir));
	}

	[Fact]
	public async Task ACatapultCanEnterMountainsAlongARoad() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand("mountains");
		from.overlays.Add(Road());
		to.overlays.Add(Road());
		MapUnit catapult = Spawn("Catapult", from);

		Assert.Equal(1, PathFor(catapult, to).PathLength());
		Assert.True(await catapult.Move(dir));
		Assert.Equal(to, catapult.location);
	}

	// "If you are on an unroaded tile, you can't go on a mountain tile even
	// if it's roaded" (https://github.com/C7-Game/OpenCiv3/pull/1029).
	[Fact]
	public async Task ACatapultCantStepOntoARoadedMountainFromOffTheRoad() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand("mountains");
		to.overlays.Add(Road());
		MapUnit catapult = Spawn("Catapult", from);

		Assert.False(await catapult.Move(dir));
		Assert.Equal(from, catapult.location);
	}

	// "Transports Only Foot units: Can only transport units w/ Foot unit
	// Flag", like the Helicopter.
	[Fact]
	public void AFootOnlyTransportRefusesACatapult() {
		(Tile tile, _, _) = FindAdjacentLand("grassland");
		MapUnit helicopter = Spawn("Helicopter", tile);
		MapUnit catapult = Spawn("Catapult", tile);
		MapUnit warrior = Spawn("Warrior", tile);
		Assert.True(helicopter.unitType.canCarryFootUnitsOnly);

		catapult.BoardTransport(helicopter);
		warrior.BoardTransport(helicopter);

		Assert.Null(catapult.loadedOnUnitId);
		Assert.Equal(helicopter.id, warrior.loadedOnUnitId);
	}

	// "All Terrain As Roads: whether improved w/ road or not, will lose only
	// as much movement as improved terrain", like the Explorer.
	[Fact]
	public async Task AnExplorerCrossesHillsAsIfAlongARoad() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand("hills");
		MapUnit explorer = Spawn("Explorer", from);
		float before = explorer.movementPoints.remaining;

		Assert.True(await explorer.Move(dir));

		Assert.Equal(TilePath.RoadMovementCost(), before - explorer.movementPoints.remaining, 4);
	}

	// The Chasqui Scout "ignores movement costs for both hills and
	// mountains" (PRTO_Chasquis_Scout; the BIQ's Ignore Movement Cost), so
	// it pays 1 for each and crosses two a turn. A Scout pays the terrain's
	// cost.
	[Theory]
	[InlineData("hills")]
	[InlineData("mountains")]
	public async Task AChasquiScoutCrossesHillsAndMountainsAsFlatLand(string terrain) {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentLand(terrain);
		MapUnit chasqui = Spawn("Chasqui Scout", from);
		Assert.True(await chasqui.Move(dir));
		Assert.Equal(1, chasqui.MaxMovementPoints() - chasqui.movementPoints.remaining, 4);

		MapUnit scout = Spawn("Scout", from);
		Assert.True(await scout.Move(dir));
		Assert.Equal(System.Math.Min(to.MovementCost(), scout.MaxMovementPoints()), scout.MaxMovementPoints() - scout.movementPoints.remaining, 4);
	}

	// "Immobile: Can't move", like the Princess.
	[Fact]
	public async Task AnImmobileUnitCantMove() {
		(Tile from, TileDirection dir, _) = FindAdjacentLand("grassland");
		MapUnit princess = Spawn("Princess", from);
		Assert.True(princess.unitType.isImmobile);

		Assert.False(await princess.Move(dir));
		Assert.Equal(from, princess.location);
	}
}

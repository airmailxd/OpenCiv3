using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// How paths and gotos keep ships out of unsafe water, and get ships already
// in it back out. Also that only a unit's own move opens a goody hut.
public class UnsafeWaterPathTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player human;
	private readonly Player ai;

	public UnsafeWaterPathTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		human = gameData.players.First(p => p.isHuman);
		ai = gameData.players.First(p => !p.isBarbarians && !p.isHuman);
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private static bool IsEmptyWater(Tile t) {
		return t != Tile.NONE && t.IsWater() && !t.isFreshWater && t.unitsOnTile.Count == 0 && !t.HasCity();
	}

	private static bool IsDeepWater(Tile t) {
		return IsEmptyWater(t) && (t.IsSea() || t.baseTerrainType.IsOcean);
	}

	// A Sea or Ocean tile with only Sea or Ocean around it, so that a Galley
	// there is in unsafe water at least two tiles from the coast.
	private Tile FindOpenWater() {
		return gameData.map.tiles.First(t => IsDeepWater(t) && t.neighbors.Values.All(IsDeepWater));
	}

	private static TilePath PathOf(Tile destination, params Tile[] steps) {
		return new TilePath(destination, new Queue<Tile>(steps));
	}

	[Fact]
	public void StrandedShipCanPathBackToTheCoast() {
		Tile open = FindOpenWater();
		MapUnit galley = gameData.SpawnUnit(ai, Prototype("Galley"), open);
		Assert.True(galley.IsInUnsafeWater());

		TilePath path = null;
		foreach (Tile coast in gameData.map.tiles.Where(t => IsEmptyWater(t) && t.IsCoast()).OrderBy(t => t.DistanceTo(open)).Take(20)) {
			path = PathingAlgorithmChooser.GetAlgorithm(galley).PathFrom(open, coast, galley);
			if (path.PathLength() > 0) {
				break;
			}
		}
		Assert.True(path?.PathLength() > 0);
		Assert.True(path.destination.IsCoast());

		// A ship already safe still keeps out of unsafe water.
		Tile safe = path.destination;
		MapUnit safeGalley = gameData.SpawnUnit(ai, Prototype("Galley"), safe);
		Assert.True(safeGalley.PathAvoids(open, open));
		Assert.Equal(0, PathingAlgorithmChooser.GetAlgorithm(safeGalley).PathFrom(safe, open, safeGalley).PathLength());
	}

	[Fact]
	public async Task StrandedHumanShipFollowsAGotoOutOfUnsafeWater() {
		Tile open = FindOpenWater();
		MapUnit galley = gameData.SpawnUnit(human, Prototype("Galley"), open);
		Tile next = open.neighbors.Values.First(IsDeepWater);
		human.tileKnowledge.AddTilesToKnown(open);

		await galley.SetUnitPath(PathOf(next.neighbors.Values.First(t => t != open && IsDeepWater(t)), next));

		Assert.Equal(next, galley.location);
	}

	// A goto whose path, made when the water was unexplored, runs through
	// what turns out to be unsafe water doesn't step into it.
	[Fact]
	public async Task GotoDoesNotSailIntoUnsafeWaterOnTheWay() {
		// The destination is deep water with no safe water next to it, so
		// there's no way around and the goto stops.
		static bool Landlocked(Tile t) => IsDeepWater(t) && t.neighbors.Values.All(n => n == Tile.NONE || n.IsLand() || IsDeepWater(n));
		(Tile coast, Tile sea, Tile beyond) = gameData.map.tiles
			.Where(t => IsEmptyWater(t) && t.IsCoast())
			.SelectMany(c => c.neighbors.Values.Where(s => IsEmptyWater(s) && s.IsSea())
				.SelectMany(s => s.neighbors.Values.Where(b => b != c && !c.neighbors.ContainsValue(b) && Landlocked(b))
					.Select(b => (c, s, b))))
			.First();
		MapUnit galley = gameData.SpawnUnit(human, Prototype("Galley"), coast);
		// Explored, or a way around through unexplored water would be taken.
		human.tileKnowledge.AddTilesToKnown(coast);
		human.tileKnowledge.AddTilesToKnown(beyond);
		Assert.True(galley.IsUnsafeWater(sea));
		Assert.True(galley.PathAvoids(sea, beyond));

		await galley.SetUnitPath(PathOf(beyond, sea, beyond));

		Assert.Equal(coast, galley.location);
		Assert.Null(galley.path);
	}

	// The player may still send a ship into unsafe water on purpose.
	[Fact]
	public async Task GotoMayEndInUnsafeWaterTheHumanChose() {
		Tile coast = gameData.map.tiles.First(t => IsEmptyWater(t) && t.IsCoast()
			&& t.neighbors.Values.Any(n => IsEmptyWater(n) && n.IsSea()));
		Tile sea = coast.neighbors.Values.First(n => IsEmptyWater(n) && n.IsSea());
		MapUnit galley = gameData.SpawnUnit(human, Prototype("Galley"), coast);
		human.tileKnowledge.AddTilesToKnown(coast);

		await galley.SetUnitPath(PathOf(sea, sea));

		Assert.Equal(sea, galley.location);
	}

	// ---- Goody huts ----

	private static bool IsEmptyLand(Tile t) {
		return t != Tile.NONE && t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	// A unit put somewhere, as by withdrawing or retreating from combat,
	// doesn't open a hut there.
	[Fact]
	public void WithdrawingOntoAHutLeavesItAlone() {
		Tile from = gameData.map.tiles.First(t => IsEmptyLand(t) && t.OwningPlayer() == null
			&& t.neighbors.Values.All(n => IsEmptyLand(n) && n.OwningPlayer() == null));
		foreach (Tile n in from.neighbors.Values) {
			n.hasGoodyHut = true;
		}
		MapUnit warrior = gameData.SpawnUnit(ai, Prototype("Warrior"), from);

		Assert.True(warrior.WithdrawToNearestFreeTile());

		Assert.NotEqual(from, warrior.location);
		Assert.True(warrior.location.hasGoodyHut);
	}
}

using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// "[Submarines] can operate invisibly to enemy units ... However, other
// submarines and AEGIS Cruisers and Destroyers can see them plainly"
// (Conquests Civilopedia, PRTO_Submarine). See MapUnit_Invisible.cs.
public class InvisibleUnitTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public InvisibleUnitTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		Prototype("Submarine").isInvisible = true;
		Prototype("Submarine").detectsInvisible = true;
		Prototype("Destroyer").detectsInvisible = true;
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		gameData.SpawnUnit(owner, Prototype(prototype), tile);
		return tile.unitsOnTile.Last();
	}

	private static bool IsEmptySea(Tile t) {
		return t != Tile.NONE && t.IsWater() && !t.isFreshWater && t.unitsOnTile.Count == 0 && t.OwningPlayer() == null;
	}

	// Four empty sea tiles in a line, outside anyone's borders.
	private (Tile a, TileDirection dir, Tile b, Tile c, Tile d) FindSeaInALine() {
		foreach (Tile a in gameData.map.tiles.Where(IsEmptySea)) {
			foreach ((TileDirection dir, Tile b) in a.neighbors) {
				if (!IsEmptySea(b) || !b.neighbors.TryGetValue(dir, out Tile c) || !IsEmptySea(c)
						|| !c.neighbors.TryGetValue(dir, out Tile d) || !IsEmptySea(d)) {
					continue;
				}
				return (a, dir, b, c, d);
			}
		}
		throw new System.Exception("No line of sea tiles found");
	}

	[Fact]
	public void OthersDontSeeASubmarineWithoutADetector() {
		(Tile a, _, Tile b, _, _) = FindSeaInALine();
		MapUnit sub = Spawn(them, "Submarine", b);
		Spawn(us, "Frigate", a);

		Assert.True(sub.IsSeenBy(them));
		Assert.False(sub.IsSeenBy(us));
		Assert.Empty(b.UnitsSeenBy(us));
		Assert.Same(b.unitsOnTile, b.UnitsSeenBy(them));
	}

	[Fact]
	public void ADestroyerOrSubmarineNearbySeesIt() {
		(Tile a, _, Tile b, _, _) = FindSeaInALine();
		MapUnit sub = Spawn(them, "Submarine", b);
		MapUnit destroyer = Spawn(us, "Destroyer", a);

		Assert.True(sub.IsSeenBy(us));
		Assert.Single(b.UnitsSeenBy(us));

		gameData.RemoveUnit(destroyer);
		Spawn(us, "Submarine", a);
		Assert.True(sub.IsSeenBy(us));
	}

	[Fact]
	public void ADetectorTooFarAwayDoesnt() {
		(Tile a, TileDirection dir, Tile b, _, _) = FindSeaInALine();
		MapUnit sub = Spawn(them, "Submarine", a);
		// Five tiles off, past any unit's sight.
		Tile far = b;
		for (int i = 0; i < 4 && far.neighbors.TryGetValue(dir, out Tile next) && next != Tile.NONE; ++i) {
			far = next;
		}
		Assert.True(a.DistanceTo(far) > 2);
		Spawn(us, "Destroyer", far);

		Assert.False(sub.IsSeenBy(us));
	}

	// A path may lead through the tile, as nobody is seen there.
	[Fact]
	public void AnUnseenSubmarinesTileLooksEmpty() {
		(Tile a, _, Tile b, _, _) = FindSeaInALine();
		Spawn(them, "Submarine", b);
		MapUnit frigate = Spawn(us, "Frigate", a);

		Assert.True(frigate.CanEnterPeacefully(b, out MapUnit.Intent intent));
		Assert.Equal(MapUnit.Intent.MoveFreely, intent);
	}

	// UNVERIFIED: a ship running into an unseen submarine of a civ it is at
	// peace with stops short of it.
	[Fact]
	public async Task AShipStopsAtAnUnseenSubmarineOfACivAtPeace() {
		(Tile a, TileDirection dir, Tile b, _, _) = FindSeaInALine();
		MapUnit sub = Spawn(them, "Submarine", b);
		MapUnit frigate = Spawn(us, "Frigate", a);
		Assert.True(us.IsAtPeaceWith(them));

		Assert.True(await frigate.Move(dir));

		Assert.Equal(a, frigate.location);
		Assert.Equal(b, sub.location);
		Assert.False(AtWar(us, them));
	}

	// At war, the ship runs into the submarine and fights it, as AI ships on
	// a goto do in Civ3 ("submarine bug").
	[Fact]
	public async Task AShipFightsAnUnseenEnemySubmarineInItsWay() {
		(Tile a, TileDirection dir, Tile b, _, _) = FindSeaInALine();
		MapUnit sub = Spawn(them, "Submarine", b);
		MapUnit frigate = Spawn(us, "Frigate", a);
		us.DeclareWarOn(them, gameData.turn);

		await frigate.Move(dir);

		// It fought: it either attacked, or died trying.
		Assert.True(frigate.hasAttackedThisTurn || !gameData.mapUnits.Contains(frigate));
		Assert.NotEqual(b, gameData.mapUnits.Contains(sub) && gameData.mapUnits.Contains(frigate) ? frigate.location : Tile.NONE);
	}

	// UNVERIFIED: bombardment can't single out an unseen submarine.
	[Fact]
	public void BombardmentMissesAnUnseenSubmarine() {
		(Tile a, _, Tile b, _, _) = FindSeaInALine();
		MapUnit sub = Spawn(them, "Submarine", b);
		MapUnit battleship = Spawn(us, "Battleship", a);

		Assert.Equal(MapUnit.NONE, b.FindTopDefenderForBombard(battleship));
		Spawn(us, "Destroyer", a);
		Assert.Equal(sub, b.FindTopDefenderForBombard(battleship));
	}

	// A LAN guest isn't sent the units its players can't see.
	[Fact]
	public void AGuestIsntSentAnUnseenSubmarine() {
		(Tile a, _, Tile b, _, _) = FindSeaInALine();
		MapUnit sub = Spawn(them, "Submarine", b);
		MapUnit frigate = Spawn(them, "Frigate", b);
		Spawn(us, "Frigate", a);
		us.tileKnowledge.AddTilesToKnown(a);

		SaveGame filtered = SnapshotFilter.Filter(LanProtocol.SnapshotOf(gameData), SnapshotFilter.ViewOf(gameData, [us.id]));
		Assert.Contains(filtered.Units, u => u.id == frigate.id);
		Assert.DoesNotContain(filtered.Units, u => u.id == sub.id);

		Spawn(us, "Destroyer", a);
		filtered = SnapshotFilter.Filter(LanProtocol.SnapshotOf(gameData), SnapshotFilter.ViewOf(gameData, [us.id]));
		Assert.Contains(filtered.Units, u => u.id == sub.id);
	}

	private static bool AtWar(Player a, Player b) => PlayerRelationship.AtWar(a, b);
}

using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// "[Missile attacks] are like bombardment, but the unit never returns to its
// base and the attacks can destroy enemy units" (Conquests Civilopedia,
// GCON_Combat): a cruise missile is used up by its attack.
public class CruiseMissileTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public CruiseMissileTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		us = civs[0];
		them = civs[1];
		us.EnsureRelationshipExists(them);
		us.DeclareWarOn(them, gameData.turn);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Prototype(string name) => gameData.unitPrototypes.Single(p => p.name == name);

	private MapUnit Spawn(Player owner, string prototype, Tile tile) => gameData.SpawnUnit(owner, Prototype(prototype), tile);

	private (Tile from, Tile target) Tiles() {
		Tile from = us.units.First(u => u.unitType.isSettler).location;
		Tile target = gameData.map.tiles.First(t => t.IsLand() && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable()
			&& from.DistanceTo(t) == 2 && t.unitsOnTile.Count == 0);
		return (from, target);
	}

	[Fact]
	public void TheBiqMarksTheCruiseMissile() {
		Assert.True(Prototype("Cruise Missile").isCruiseMissile);
		Assert.Contains(SaveUnitPrototype.Flag.CruiseMissile, Prototype("Cruise Missile").flags);
		Assert.False(Prototype("Artillery").isCruiseMissile);
	}

	[Fact]
	public async Task ACruiseMissileIsUsedUpByItsAttack() {
		(Tile from, Tile target) = Tiles();
		MapUnit missile = Spawn(us, "Cruise Missile", from);
		MapUnit tank = Spawn(them, "Tank", target);
		Assert.True(missile.CanBombardTile(target));

		await missile.Bombard(target);

		Assert.DoesNotContain(missile, us.units);
		Assert.DoesNotContain(missile, from.unitsOnTile);
	}

	[Fact]
	public async Task ArtilleryIsntUsedUp() {
		(Tile from, Tile target) = Tiles();
		MapUnit artillery = Spawn(us, "Artillery", from);
		Spawn(them, "Tank", target);
		Assert.True(artillery.CanBombardTile(target));

		await artillery.Bombard(target);

		Assert.Contains(artillery, us.units);
	}
}

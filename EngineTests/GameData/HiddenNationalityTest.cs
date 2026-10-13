using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// "you can attack another civilization's shipping without precipitating war"
// (Conquests Civilopedia, PRTO_Privateer); "they can attack without starting a
// war, but if you attack them it starts a war"
// (https://forums.civfanatics.com/threads/hidden-nationality-observations.434753/).
// See MapUnit_HiddenNationality.cs.
public class HiddenNationalityTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public HiddenNationalityTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		us.EnsureRelationshipExists(them);
		Prototype("Privateer").hasHiddenNationality = true;
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

	private (Tile from, TileDirection dir, Tile to) FindAdjacentSea() {
		foreach (Tile from in gameData.map.tiles.Where(IsEmptySea)) {
			foreach ((TileDirection dir, Tile to) in from.neighbors) {
				if (IsEmptySea(to)) {
					return (from, dir, to);
				}
			}
		}
		throw new System.Exception("No adjacent sea tiles found");
	}

	[Fact]
	public void APrivateerMayAttackShipsOfACivAtPeace() {
		(Tile from, _, Tile to) = FindAdjacentSea();
		MapUnit privateer = Spawn(us, "Privateer", from);
		Spawn(them, "Galleon", to);
		Assert.True(us.IsAtPeaceWith(them));

		Assert.True(privateer.CanEnter(to, out MapUnit.Intent intent));
		Assert.Equal(MapUnit.Intent.Fight, intent);
	}

	// Others still have to declare war, to attack either a privateer or
	// another ship.
	[Fact]
	public void OthersMustDeclareWar() {
		(Tile from, _, Tile to) = FindAdjacentSea();
		MapUnit frigate = Spawn(us, "Frigate", from);
		MapUnit privateer = Spawn(them, "Privateer", to);

		Assert.False(frigate.CanEnter(to, out MapUnit.Intent intent));
		Assert.Equal(MapUnit.Intent.WarDeclaration, intent);
		Assert.False(frigate.MayAttack(privateer));
	}

	[Fact]
	public async Task TheAttackDoesntStartAWar() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentSea();
		MapUnit privateer = Spawn(us, "Privateer", from);
		MapUnit galleon = Spawn(them, "Galleon", to);
		int declarations = them.playerRelationships[us.id].warDeclarationCount;

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ZeroRandom();
		try {
			await privateer.Move(dir);
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.True(privateer.hasAttackedThisTurn);
		Assert.True(!gameData.mapUnits.Contains(galleon) || galleon.hitPointsRemaining < galleon.maxHitPoints || galleon.location != to);
		Assert.True(us.IsAtPeaceWith(them));
		Assert.Equal(declarations, them.playerRelationships[us.id].warDeclarationCount);
	}

	// Makes every random roll come out as 0, so attackers win every round.
	private class ZeroRandom : System.Random {
		protected override double Sample() => 0.0;
	}
}

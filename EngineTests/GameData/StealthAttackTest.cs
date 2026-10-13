using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// "Stealth attack allows a unit to specify which enemy unit -- in a stack --
// it will attack" (Conquests Civilopedia, GCON_Stealth_Attack); "it only
// works, if you have at least two preset stealth attack targets in a stack"
// (https://forums.civfanatics.com/threads/stealth-attack.406555/). See
// MapUnit_StealthAttack.cs.
public class StealthAttackTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public StealthAttackTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		us.DeclareWarOn(them, gameData.turn);
		UnitPrototype sub = Prototype("Submarine");
		sub.hasStealthAttack = true;
		sub.stealthTargets = ["Transport", "Frigate", "Galleon"];
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
	public void ItNeedsTwoTargetsInTheStack() {
		(Tile from, _, Tile to) = FindAdjacentSea();
		MapUnit sub = Spawn(us, "Submarine", from);
		Spawn(them, "Battleship", to);
		MapUnit transport = Spawn(them, "Transport", to);

		Assert.Empty(sub.StealthAttackTargets(to));
		Assert.Null(sub.ChooseStealthAttackTarget(to));

		MapUnit frigate = Spawn(them, "Frigate", to);
		Assert.Equal([transport, frigate], sub.StealthAttackTargets(to));
	}

	[Fact]
	public void OnlyUnitsWithStealthAttackPickTargets() {
		(Tile from, _, Tile to) = FindAdjacentSea();
		MapUnit destroyer = Spawn(us, "Destroyer", from);
		Spawn(them, "Transport", to);
		Spawn(them, "Frigate", to);

		Assert.Empty(destroyer.StealthAttackTargets(to));
	}

	// "the AI has a tendency to stealth-attack the preset target with the
	// highest attack factor" (same thread).
	[Fact]
	public void TheAIPicksTheTargetWithTheHighestAttack() {
		(Tile from, _, Tile to) = FindAdjacentSea();
		MapUnit sub = Spawn(us, "Submarine", from);
		Spawn(them, "Transport", to);
		MapUnit frigate = Spawn(them, "Frigate", to);
		Assert.True(frigate.unitType.attack > Prototype("Transport").attack);

		Assert.Equal(frigate, sub.ChooseStealthAttackTarget(to));
	}

	// The attack goes against the chosen unit, not the stack's top defender.
	[Fact]
	public async Task TheChosenTargetDefends() {
		(Tile from, TileDirection dir, Tile to) = FindAdjacentSea();
		MapUnit sub = Spawn(us, "Submarine", from);
		MapUnit battleship = Spawn(them, "Battleship", to);
		MapUnit transport = Spawn(them, "Transport", to);
		Spawn(them, "Frigate", to);
		Assert.Equal(battleship, to.FindTopDefender(sub));
		int battleshipHitPoints = battleship.hitPointsRemaining;

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ZeroRandom();
		try {
			await sub.Move(dir, false, transport.id);
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.DoesNotContain(transport, gameData.mapUnits);
		Assert.Equal(battleshipHitPoints, battleship.hitPointsRemaining);
	}

	// Saves keep the stealth targets and the new flags.
	[Fact]
	public void SavesKeepStealthTargets() {
		UnitPrototype sub = Prototype("Submarine");
		sub.isInvisible = true;
		C7GameData.Save.SaveUnitPrototype saved = new(sub);
		Assert.Equal(["Transport", "Frigate", "Galleon"], saved.stealthTargets);
		UnitPrototype loaded = new(saved, gameData.Terraforms);
		Assert.Equal(sub.stealthTargets, loaded.stealthTargets);
		Assert.True(loaded.hasStealthAttack);
		Assert.True(loaded.isInvisible);
		Assert.Null(new C7GameData.Save.SaveUnitPrototype(Prototype("Destroyer")).stealthTargets);
	}

	// Makes every random roll come out as 0, so attackers win every round.
	private class ZeroRandom : System.Random {
		protected override double Sample() => 0.0;
	}
}

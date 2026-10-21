using System.Linq;
using C7Engine;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// In an AI-vs-AI war each side's war weariness comes from its own losses
// and the attacks on it, whichever of the two moves later in the turn
// order. Civ3's bug (the article in Player.cs: an AI-AI war gives both AIs
// the first's points) is not reproduced (owner's decision, 2026-10-21).
public class AiWarWearinessTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player early;
	private readonly Player late;

	public AiWarWearinessTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player[] ais = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).ToArray();
		early = ais[0];
		late = ais[^1];
		Assert.True(gameData.players.IndexOf(early) < gameData.players.IndexOf(late));
		early.EnsureRelationshipExists(late);
		late.EnsureRelationshipExists(early);
	}

	private MapUnit Spawn(Player owner, string name) {
		Tile tile = owner.units.First().location;
		return gameData.SpawnUnit(owner, gameData.unitPrototypes.Single(p => p.name == name), tile);
	}

	[Fact]
	public void EachAiAtWarGetsItsOwnPoints() {
		// The later civ declares war: the earlier, its victim, rallies
		// (-30) and the aggressor gets nothing for it.
		late.DeclareWarOn(early, gameData.turn);
		Assert.Equal(-30, early.WarWearinessPointsAgainst(late));
		Assert.Equal(0, late.WarWearinessPointsAgainst(early));

		// The later civ's spearman attacks the earlier civ's and loses: 2
		// to each, the defender's for being attacked, the attacker's for
		// its defeat.
		MapUnit lateSpear = Spawn(late, "Spearman");
		MapUnit earlySpear = Spawn(early, "Spearman");
		Player.AddWarWearinessForAttack(lateSpear, earlySpear, attackerDefeated: true);
		Assert.Equal(-28, early.WarWearinessPointsAgainst(late));
		Assert.Equal(2, late.WarWearinessPointsAgainst(early));

		// The earlier civ attacks a worker and wins: nothing for either.
		Player.AddWarWearinessForAttack(earlySpear, Spawn(late, "Worker"), attackerDefeated: false);
		Assert.Equal(-28, early.WarWearinessPointsAgainst(late));
		Assert.Equal(2, late.WarWearinessPointsAgainst(early));

		// The earlier civ attacks and wins: only the later one, whose unit
		// was attacked, wearies.
		Player.AddWarWearinessForAttack(earlySpear, lateSpear, attackerDefeated: false);
		Assert.Equal(-28, early.WarWearinessPointsAgainst(late));
		Assert.Equal(4, late.WarWearinessPointsAgainst(early));

		// A city lost goes to its former owner alone.
		Player.AddWarWearinessForLostCity(late, early, size: 3);
		Assert.Equal(21, late.WarWearinessPointsAgainst(early));
		Assert.Equal(-28, early.WarWearinessPointsAgainst(late));
		Player.AddWarWearinessForLostCity(early, late, size: 1);
		Assert.Equal(-12, early.WarWearinessPointsAgainst(late));
		Assert.Equal(21, late.WarWearinessPointsAgainst(early));
	}
}

using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.AI;

// Civ3's own war plotting, as Flintlock read it from the executable
// (compendium 08, section 3.4.1; see StockWarPlotting).
public sealed class StockWarPlottingTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly Random originalRandom;

	// Every roll comes out 0, so every one in N passes; or the highest, so
	// every one in N above 1 fails.
	private class LowRandom : Random {
		protected override double Sample() => 0.0;
	}

	private class HighRandom : Random {
		protected override double Sample() => 0.999999;
	}

	public StockWarPlottingTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		originalRandom = gameData.random;
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		us.EnsureRelationshipExists(them);
	}

	public void Dispose() {
		gameData.random = originalRandom;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	private void SetDifficulty(int index) {
		gameData.gameDifficulty = gameData.difficulties[index];
	}

	// "level = civ.AggressionLevel + GameDifficulty // 2 -
	// DifficultyLevelCount // 6", clamped to -2..2.
	[Fact]
	public void TheEffectiveAggressionLevelRisesWithTheDifficulty() {
		Assert.Equal(8, gameData.difficulties.Count);
		us.civilization.aggressionLevel = 0;
		SetDifficulty(0);
		Assert.Equal(-1, StockWarPlotting.EffectiveAggressionLevel(gameData, us));
		SetDifficulty(2);
		Assert.Equal(0, StockWarPlotting.EffectiveAggressionLevel(gameData, us));
		SetDifficulty(4);
		Assert.Equal(1, StockWarPlotting.EffectiveAggressionLevel(gameData, us));
		SetDifficulty(6);
		Assert.Equal(2, StockWarPlotting.EffectiveAggressionLevel(gameData, us));
		us.civilization.aggressionLevel = 2;
		Assert.Equal(2, StockWarPlotting.EffectiveAggressionLevel(gameData, us));
	}

	// "Have a capital city", and then every roll must pass.
	[Fact]
	public void PlottingNeedsACapitalAndEveryRoll() {
		gameData.random = new LowRandom();
		foreach (City c in us.cities.ToList()) {
			CityInteractions.DestroyCity(c);
		}
		Assert.False(StockWarPlotting.RollsToPlot(gameData, us));

		BuildCity(us);
		Assert.True(StockWarPlotting.RollsToPlot(gameData, us));
		// Weaker than the strongest civ, the power roll can fail.
		gameData.random = new HighRandom();
		if (StockWarPlotting.PowerRank(gameData, us) < 31) {
			Assert.False(StockWarPlotting.RollsToPlot(gameData, us));
		}
	}

	// "Other players with whom the AI has a mutual protection pact are
	// excluded"; others at peace are left out unless "one in X" comes up.
	[Fact]
	public void PactPartnersAreNeverTargetsAndOthersOnlyOnARoll() {
		gameData.random = new LowRandom();
		Assert.False(StockWarPlotting.Excluded(gameData, us, them));
		gameData.random = new HighRandom();
		Assert.True(StockWarPlotting.Excluded(gameData, us, them));

		gameData.random = new LowRandom();
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.MutualProtectionPact,
			DealDetails.Exchange, 0, null, 20, gameData.turn, null));
		Assert.True(StockWarPlotting.Excluded(gameData, us, them));
	}

	// "X = X + 4 if LandUnitsPerCity is 0, + 2 if it's 1, + 1 if it's 2,
	// and + 0 if it's 3 or more." A fleet makes them the strongest civ
	// either way, so the power term stays the same.
	[Fact]
	public void ATargetWithoutDefendersScoresHigher() {
		BuildCity(us);
		City theirs = BuildCity(them);
		foreach (MapUnit u in them.units.ToList()) {
			gameData.RemoveUnit(u);
		}
		UnitPrototype ship = gameData.unitPrototypes.Where(p => p.IsSeaUnit()).MaxBy(p => p.attack);
		Tile sea = gameData.map.tiles.First(t => t.baseTerrainType.IsWater && t.unitsOnTile.Count == 0);
		for (int i = 0; i < 100; ++i) {
			gameData.SpawnUnit(them, ship, sea);
		}
		Assert.Equal(31, StockWarPlotting.PowerRank(gameData, them));
		int bare = StockWarPlotting.TargetScore(gameData, us, them);
		UnitPrototype spearman = gameData.unitPrototypes.First(p => p.name == "Spearman");
		for (int i = 0; i < 3; ++i) {
			gameData.SpawnUnit(them, spearman, theirs.location);
		}
		Assert.Equal(31, StockWarPlotting.PowerRank(gameData, them));
		Assert.True(StockWarPlotting.TargetScore(gameData, us, them) < bare);
	}

	// AIs talk trade with each other only on "the base game's 1-in-4 gate
	// before ai_negotiate_with_other_ai" (C3X); a human is always talked to.
	[Fact]
	public void AIsNegotiateWithEachOtherOnAOneInFourRoll() {
		gameData.random = new HighRandom();
		Assert.False(PlayerAI.RollsToNegotiate(them));
		Player human = gameData.players.First(p => p.isHuman);
		Assert.True(PlayerAI.RollsToNegotiate(human));
		gameData.random = new LowRandom();
		Assert.True(PlayerAI.RollsToNegotiate(them));
	}
}

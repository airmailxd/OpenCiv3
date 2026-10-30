using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.Advanced;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The phase-plan switch (see PhasePlan): expand, muster, war, consolidate;
// the muster cities; the army's size.
public sealed class GamePhaseTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;
	private readonly Random originalRandom;

	// Every roll fails, so Civ3's war plotting starts nothing by itself.
	private class HighRandom : Random {
		protected override double Sample() => 0.999999;
	}

	public GamePhaseTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		originalRandom = gameData.random;
		gameData.random = new HighRandom();
		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman && !p.IsIndependent).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		us.aiType = AiType.Advanced;
		us.EnsureRelationshipExists(them);
		them.EnsureRelationshipExists(us);
		AiExperiments.Enable(us, PhasePlan.Switch);
	}

	public void Dispose() {
		gameData.random = originalRandom;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City BuildCity(Player owner, Func<Tile, bool> where) {
		Tile tile = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp
			&& !t.IsImpassable() && t.IsAllowCities() && t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()) && where(t));
		return CityInteractions.BuildCity(tile, owner, $"City {owner.cities.Count}");
	}

	// Our cities (count of them) and one of theirs, on one landmass, theirs
	// 5 to 10 tiles from our first, and known to us.
	private City Setup(int ourCities) {
		foreach (Player p in new[] { us, them }) {
			foreach (City c in p.cities.ToList()) {
				CityInteractions.DestroyCity(c);
			}
		}
		City first = BuildCity(us, _ => true);
		for (int i = 1; i < ourCities; ++i) {
			BuildCity(us, t => t.continent == first.location.continent);
		}
		City theirs = BuildCity(them, t => t.continent == first.location.continent && t.DistanceTo(first.location) >= 5 && t.DistanceTo(first.location) <= 10);
		us.tileKnowledge.AddTilesToKnown(theirs.location);
		return theirs;
	}

	private static void SetPhase(Player p, GamePhase phase, int turn) {
		p.gamePhase = new GamePhaseState { phase = phase, startTurn = turn };
	}

	[Fact]
	public void TheSwitchStartsInExpandAndOffHasNoPhase() {
		Setup(1);
		Assert.True(PhasePlan.Update(us, gameData));
		Assert.Equal(GamePhase.Expand, us.gamePhase.phase);

		Player other = them;
		other.aiType = AiType.Advanced;
		Assert.False(PhasePlan.Update(other, gameData));
		Assert.Null(other.gamePhase);
	}

	[Fact]
	public void ExpandEndsAtThePhaseCitiesAndMustersWithARivalToMarchOn() {
		Setup(2);
		SetPhase(us, GamePhase.Expand, gameData.turn);
		Assert.False(PhasePlan.Update(us, gameData));
		for (int i = us.cities.Count; i < PhasePlan.Cities(us); ++i) {
			BuildCity(us, t => t.continent == us.cities[0].location.continent);
		}
		Assert.True(PhasePlan.Update(us, gameData));
		Assert.Equal(GamePhase.Muster, us.gamePhase.phase);
		Assert.NotEmpty(us.gamePhase.musterCities);
	}

	[Fact]
	public void ExpandWithNoRivalToMarchOnGoesStraightToConsolidate() {
		City theirs = Setup(PhasePlan.Cities(us));
		CityInteractions.DestroyCity(theirs);
		SetPhase(us, GamePhase.Expand, gameData.turn);
		Assert.True(PhasePlan.Update(us, gameData));
		Assert.Equal(GamePhase.Consolidate, us.gamePhase.phase);
	}

	[Fact]
	public async Task MusterStartsTheScriptsCampaignAndWarEndsInConsolidate() {
		Setup(PhasePlan.Cities(us));
		SetPhase(us, GamePhase.Muster, gameData.turn);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.True(WarCampaign.HasPhaseCampaign(us));
		Assert.True(WarCampaign.IsPlannedTarget(us, them));
		// Still gathering: still mustering.
		Assert.False(PhasePlan.Update(us, gameData));
		Assert.Equal(GamePhase.Muster, us.gamePhase.phase);

		// The war's phase ends in Consolidate once its campaign is over.
		SetPhase(us, GamePhase.War, gameData.turn);
		us.gamePhase.firstWarTurn = gameData.turn;
		Assert.False(PhasePlan.Update(us, gameData));
		Assert.Equal(GamePhase.War, us.gamePhase.phase);
		SetPhase(us, GamePhase.War, gameData.turn);
		CityInteractions.DestroyCity(them.cities[0]);
		await WarCampaign.PlayTurn(us, gameData);
		Assert.False(WarCampaign.HasPhaseCampaign(us));
		Assert.True(PhasePlan.Update(us, gameData));
		Assert.Equal(GamePhase.Consolidate, us.gamePhase.phase);
	}

	[Fact]
	public void ConsolidateLastsTheKnobsTurnsThenMustersAgain() {
		Setup(PhasePlan.Cities(us));
		// A turn that doesn't look for open sites.
		gameData.turn = 101;
		SetPhase(us, GamePhase.Consolidate, gameData.turn - (PhasePlan.ConsolidateTurns(us) - 1));
		Assert.False(PhasePlan.Update(us, gameData));
		us.gamePhase.startTurn--;
		Assert.True(PhasePlan.Update(us, gameData));
		Assert.Equal(GamePhase.Muster, us.gamePhase.phase);
	}

	[Fact]
	public void MusterCitiesAreTheCapitalAndTheBestProducers() {
		Setup(3);
		List<City> three = PhasePlan.ChooseMusterCities(us);
		Assert.Equal(2, three.Count);
		Assert.True(three[0].IsCapital());
		City best = us.cities.Where(c => c != three[0]).OrderByDescending(c => c.CurrentProductionYield().useful).ThenBy(c => c.id.ToString()).First();
		Assert.Equal(best, three[1]);

		BuildCity(us, t => t.continent == us.cities[0].location.continent);
		List<City> four = PhasePlan.ChooseMusterCities(us);
		Assert.Equal(3, four.Count);
		Assert.Equal(four.Skip(1).Select(c => c.CurrentProductionYield().useful).OrderByDescending(y => y), four.Skip(1).Select(c => c.CurrentProductionYield().useful));
		Assert.All(four, c => Assert.Equal(four[0].location.continent, c.location.continent));
	}

	[Fact]
	public void OnlyMusterCitiesWantTheScriptsBarracks() {
		Setup(3);
		SetPhase(us, GamePhase.Muster, gameData.turn);
		us.gamePhase.musterCities = PhasePlan.ChooseMusterCities(us).Select(c => c.id.ToString()).ToList();
		City inside = PhasePlan.ChooseMusterCities(us)[0];
		City outside = us.cities.First(c => !us.gamePhase.musterCities.Contains(c.id.ToString()));
		Assert.True(PhasePlan.WantsBarracks(us, inside));
		Assert.False(PhasePlan.WantsBarracks(us, outside));
		SetPhase(us, GamePhase.Expand, gameData.turn);
		us.gamePhase.musterCities = [inside.id.ToString()];
		Assert.False(PhasePlan.WantsBarracks(us, inside));
	}

	[Fact]
	public void ArmySizeFollowsTheKnobTheDifficultyAndTheDefenders() {
		AdvancedPolicy policy = new();
		us.advancedPolicy = policy;
		Difficulty regent = gameData.AiRulesDifficulty();
		int regentIndex = gameData.difficulties.IndexOf(regent);
		gameData.gameDifficulty = regent;
		Assert.Equal(9, PhasePlan.ArmySize(us, gameData, 0));
		Assert.Equal(12, PhasePlan.ArmySize(us, gameData, 4));
		Assert.Equal(16, PhasePlan.ArmySize(us, gameData, 9));

		// One more for each level above Regent, to 12.
		gameData.gameDifficulty = gameData.difficulties[regentIndex + 1];
		Assert.Equal(10, PhasePlan.ArmySize(us, gameData, 0));
		gameData.gameDifficulty = gameData.difficulties[Math.Min(regentIndex + 5, gameData.difficulties.Count - 1)];
		Assert.Equal(12, PhasePlan.ArmySize(us, gameData, 0));

		// A higher knob is the cap.
		policy.knobs["PhaseArmy"] = 14;
		Assert.Equal(14, PhasePlan.ArmySize(us, gameData, 0));
		policy.knobs["PhaseArmy"] = 6;
		gameData.gameDifficulty = regent;
		Assert.Equal(6, PhasePlan.ArmySize(us, gameData, 0));
		Assert.Equal(2, PhasePlan.AttackersToContinue(us, gameData));
	}

	[Fact]
	public void TheSettlerSiteBonusRisesTowardTheRival() {
		City theirs = Setup(1);
		City ours = us.cities[0];
		Tile between = gameData.map.tiles.Where(t => t.continent == ours.location.continent && t.DistanceTo(theirs.location) < ours.location.DistanceTo(theirs.location))
			.OrderBy(t => t.DistanceTo(theirs.location)).First();
		Tile away = gameData.map.tiles.First(t => t.continent == ours.location.continent && t.DistanceTo(theirs.location) > ours.location.DistanceTo(theirs.location));
		Assert.Equal((ours, theirs), PhasePlan.NearestRival(us));
		Assert.True(PhasePlan.SiteBonus(between, (ours, theirs)) > 0);
		Assert.Equal(0, PhasePlan.SiteBonus(away, (ours, theirs)));
	}

	[Fact]
	public void ThePhaseIsSavedAndShownInTheBriefing() {
		Setup(2);
		SetPhase(us, GamePhase.Consolidate, 7);
		Assert.Contains("Game phase: Consolidate (since turn 7)", PlanBriefing.Situation(us, gameData).text);
		C7GameData.Save.SaveAiState saved = AiSaveState.Save(us);
		Assert.Equal(GamePhase.Consolidate, saved.gamePhase.phase);
		us.gamePhase = null;
		AiSaveState.Restore(us, gameData, saved);
		Assert.Equal(GamePhase.Consolidate, us.gamePhase.phase);
		AiSaveState.Restore(us, gameData, null);
		Assert.Null(us.gamePhase);
	}

	[Fact]
	public void OffHasNoPhaseAndTheRulesPlanIsUnchanged() {
		Setup(2);
		SetPhase(us, GamePhase.Muster, gameData.turn);
		StrategicPlan with = RulePlanner.Plan(us, gameData);
		Player off = them;
		off.aiType = AiType.Advanced;
		Assert.Null(PhasePlan.PhaseOf(off));
		Assert.DoesNotContain("phase ", RulePlanner.Plan(off, gameData).rationale);
		Assert.Contains("phase Muster", with.rationale);
	}
}

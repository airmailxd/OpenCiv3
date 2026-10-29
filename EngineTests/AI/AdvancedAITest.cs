using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.Advanced;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The Advanced AI: a plan that steers the Standard AI, and no advantage
// over a human.
public sealed class AdvancedAITest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly Player rival;

	public AdvancedAITest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player[] civs = gameData.players.Where(p => !p.isHuman && !p.IsIndependent).ToArray();
		player = civs[0];
		rival = civs[1];
		player.EnsureRelationshipExists(rival);
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Unit(string name) => gameData.unitPrototypes.First(p => p.name == name);

	private StrategicPlan PlanWith(PlanFocus focus, int value) {
		StrategicPlan plan = new() { focus = { [focus] = value } };
		AdvancedAI.Stamp(plan, player, gameData);
		return plan;
	}

	[Fact]
	public void APlanSteersOnlyAnAdvancedAi() {
		player.strategicPlan = PlanWith(PlanFocus.Settlers, 100);
		Assert.Equal(0, PlanEffects.ProductionAdjustment(player, Unit("Settler")));
		player.aiType = AiType.Advanced;
		Assert.Equal(PlanEffects.FocusPoints, PlanEffects.ProductionAdjustment(player, Unit("Settler")));
		Assert.Equal(0, PlanEffects.ProductionAdjustment(player, Unit("Warrior")));
	}

	[Fact]
	public void ANeutralPlanChangesNothing() {
		player.aiType = AiType.Advanced;
		player.strategicPlan = PlanWith(PlanFocus.Settlers, StrategicPlan.NeutralFocus);
		foreach (UnitPrototype unit in gameData.unitPrototypes) {
			Assert.Equal(0, PlanEffects.ProductionAdjustment(player, unit));
		}
		Assert.Equal(0, PlanEffects.AggressionShift(player));
		Assert.Equal(RivalStance.Neutral, PlanEffects.Stance(player, rival));
	}

	[Fact]
	public void AnExpiredPlanStopsSteering() {
		player.aiType = AiType.Advanced;
		player.strategicPlan = PlanWith(PlanFocus.Settlers, 100);
		gameData.turn = player.strategicPlan.expiresTurn;
		Assert.Equal(0, PlanEffects.ProductionAdjustment(player, Unit("Settler")));
	}

	[Fact]
	public void PlanValuesAreKeptInRange() {
		StrategicPlan plan = new() { focus = { [PlanFocus.Offense] = 500 }, aggressionShift = 9, victory = "Pizza" };
		plan.Clamp();
		Assert.Equal(100, plan.Focus(PlanFocus.Offense));
		Assert.Equal(2, plan.aggressionShift);
		Assert.Null(plan.victory);
	}

	[Fact]
	public void AFriendIsNeverAWarTargetAndATargetAlwaysIs() {
		player.aiType = AiType.Advanced;
		StrategicPlan plan = PlanWith(PlanFocus.Offense, 50);
		plan.stances[rival.id.ToString()] = RivalStance.Friend;
		player.strategicPlan = plan;
		Assert.True(StockWarPlotting.Excluded(gameData, player, rival));
		plan.stances[rival.id.ToString()] = RivalStance.Target;
		Assert.False(StockWarPlotting.Excluded(gameData, player, rival));
		Assert.True(StockWarPlotting.TargetScore(gameData, player, rival) >= StockWarPlotting.PlanTargetBonus / 2);
	}

	[Fact]
	public void ThePlanSurvivesSaveAndLoad() {
		player.aiType = AiType.Advanced;
		StrategicPlan plan = PlanWith(PlanFocus.Science, 80);
		plan.stances[rival.id.ToString()] = RivalStance.Target;
		plan.aggressionShift = 1;
		plan.rationale = "test";
		player.strategicPlan = plan;

		C7GameData.GameData loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON()).ToGameData(fixture.behaviors);
		StrategicPlan back = loaded.players.Single(p => p.id == player.id).strategicPlan;

		Assert.NotNull(back);
		Assert.Equal(80, back.Focus(PlanFocus.Science));
		Assert.Equal(RivalStance.Target, back.StanceToward(rival.id));
		Assert.Equal(1, back.aggressionShift);
		Assert.Equal(plan.expiresTurn, back.expiresTurn);
		Assert.Equal("test", back.rationale);
	}

	[Fact]
	public void TheAdvancedAiGetsNoDifficultyBonusOrHouseRule() {
		Difficulty regent = gameData.AiRulesDifficulty();
		gameData.gameDifficulty = gameData.difficulties.Last();
		gameData.rules.AiCostPercent = 50;
		Assert.NotEqual(regent, gameData.AiBonusDifficulty(player));
		Assert.Equal(.5f, AiHouseRules.CostFactor(player));

		player.aiType = AiType.Advanced;
		Assert.Equal(regent, gameData.AiBonusDifficulty(player));
		Assert.Equal(1f, AiHouseRules.CostFactor(player));
		Assert.True(AIFogOfWar.EnabledFor(player));
	}

	[Fact]
	public void TheRulePlannerReplansOnItsCadenceAndWhenAWarStarts() {
		player.aiType = AiType.Advanced;
		AdvancedAI.PlayTurn(player, gameData);
		StrategicPlan first = player.strategicPlan;
		Assert.NotNull(first);
		Assert.Equal("rules", first.source);

		gameData.turn += 1;
		Assert.False(AdvancedAI.NeedsNewPlan(player, gameData));
		PlayerRelationship.DeclareWar(rival, player, false, gameData.turn);
		Assert.True(AdvancedAI.NeedsNewPlan(player, gameData));

		AdvancedAI.PlayTurn(player, gameData);
		Assert.NotSame(first, player.strategicPlan);
		gameData.turn += AdvancedAI.PlanEveryTurns;
		Assert.True(AdvancedAI.NeedsNewPlan(player, gameData));
	}

	[Fact]
	public void AModelAnswerIsCheckedAndClamped() {
		System.Collections.Generic.Dictionary<string, ID> rivals = new() { ["Rome"] = rival.id };
		string json = """
			{"focus": {"Offense": 140, "Science": 70, "Teleporters": 90}, "aggressionShift": 5,
			 "stances": [{"rival": "Rome", "stance": "Target"}, {"rival": "Atlantis", "stance": "Target"}],
			 "victory": "Conquest", "rationale": "Rome is weak", "notes": "take Rome, then build"}
			""";
		StrategicPlan plan = ModelPlanner.Parse(json, rivals);
		Assert.Equal("model", plan.source);
		Assert.Equal(100, plan.Focus(PlanFocus.Offense));
		Assert.Equal(70, plan.Focus(PlanFocus.Science));
		Assert.Equal(2, plan.aggressionShift);
		Assert.Single(plan.stances);
		Assert.Equal(RivalStance.Target, plan.StanceToward(rival.id));
		Assert.Equal("take Rome, then build", plan.notes);
		Assert.Null(ModelPlanner.Parse("not json", rivals));
	}

	[Fact]
	public void TheBriefingNamesOnlyRivalsTheCivHasMet() {
		player.aiType = AiType.Advanced;
		PlanBriefing.Result briefing = PlanBriefing.Build(player, gameData, RulePlanner.Plan(player, gameData));
		Assert.Contains(rival.civilization.name, briefing.text);
		Assert.Equal(rival.id, briefing.rivals[rival.civilization.name]);
		foreach (Player stranger in gameData.players.Where(p => p != player && !player.playerRelationships.ContainsKey(p.id))) {
			Assert.DoesNotContain(stranger.id, briefing.rivals.Values);
		}
	}

	[Fact]
	public void WithNoKeyTheModelIsNeverAsked() {
		Assert.False(ModelPlanner.Available);
		player.aiType = AiType.Advanced;
		AdvancedAI.PlayTurn(player, gameData);
		Assert.False(ModelPlanner.Waiting(player));
		Assert.Equal("rules", player.strategicPlan.source);
	}
}

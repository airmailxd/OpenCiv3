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
		Assert.Equal(PlanEffects.FocusPoints(player), PlanEffects.ProductionAdjustment(player, Unit("Settler")));
		Assert.Equal(0, PlanEffects.ProductionAdjustment(player, Unit("Warrior")));
	}

	private void Flags(int often, int never, params PlanFocus[] focuses) {
		player.civilization.buildOften.Clear();
		player.civilization.buildNever.Clear();
		for (int i = 0; i < focuses.Length; ++i) {
			(i < often ? player.civilization.buildOften : player.civilization.buildNever).Add(focuses[i]);
		}
		player.civilization.buildFlagCount = focuses.Length;
	}

	[Fact]
	public void BuildOftenScoresHigherAndBuildNeverLower() {
		player.aiType = AiType.Advanced;
		Flags(1, 1, PlanFocus.Settlers, PlanFocus.Offense);
		Assert.Equal(PlanEffects.FlagPoints(player), PlanEffects.ProductionAdjustment(player, Unit("Settler")));
		Assert.Equal(-PlanEffects.FlagPoints(player), PlanEffects.ProductionAdjustment(player, Unit("Horseman")));
		Assert.Equal(0, PlanEffects.ProductionAdjustment(player, Unit("Worker")));
		Assert.True(PlanEffects.FlagPoints(player) < PlanEffects.FocusPoints(player));
	}

	[Fact]
	public void FlagsAndPlanAdd() {
		player.aiType = AiType.Advanced;
		Flags(1, 0, PlanFocus.Settlers);
		player.strategicPlan = PlanWith(PlanFocus.Settlers, 100);
		Assert.Equal(PlanEffects.FocusPoints(player) + PlanEffects.FlagPoints(player),
			PlanEffects.ProductionAdjustment(player, Unit("Settler")), 3);
	}

	[Fact]
	public void NoFlagsOrAStandardAiChangeNothing() {
		player.aiType = AiType.Advanced;
		Flags(0, 0);
		Assert.Equal(0, PlanEffects.ProductionAdjustment(player, Unit("Settler")));
		Flags(1, 0, PlanFocus.Settlers);
		player.aiType = AiType.Standard;
		Assert.Equal(0, PlanEffects.ProductionAdjustment(player, Unit("Settler")));
	}

	[Fact]
	public void MoreFlagsThanTheLimitAreAllIgnored() {
		player.aiType = AiType.Advanced;
		Flags(1, 0, PlanFocus.Settlers, PlanFocus.Offense, PlanFocus.Defense);
		Assert.NotEqual(0, PlanEffects.ProductionAdjustment(player, Unit("Settler")));
		Flags(4, 0, PlanFocus.Settlers, PlanFocus.Offense, PlanFocus.Defense, PlanFocus.Naval);
		Assert.Equal(0, PlanEffects.ProductionAdjustment(player, Unit("Settler")));
	}

	[Fact]
	public void ANeutralPlanChangesNothing() {
		player.aiType = AiType.Advanced;
		Flags(0, 0);
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
		gameData.turn += AdvancedAI.PlanEveryTurns(player);
		Assert.True(AdvancedAI.NeedsNewPlan(player, gameData));
	}

	// The reviewer may change at most 3 knobs, each at most 2 steps from
	// where the player has it, within range, and only knobs that exist.
	[Fact]
	public void AReviewIsCheckedAndKeptSmall() {
		player.aiType = AiType.Advanced;
		string json = """
			{"changes": [
			  {"knob": "ExpandSettlers", "value": 5},
			  {"knob": "Teleporters", "value": 9},
			  {"knob": "war-production", "value": 1},
			  {"knob": "FocusPoints", "value": 100},
			  {"knob": "PreyAggression", "value": 2}],
			 "rationale": "expand faster", "notes": "watch the north", "lesson": "claim land early"}
			""";
		PolicyReview.Answer answer = PolicyReview.Parse(json, player);
		Assert.Equal(3, answer.changes.Count);
		Assert.Equal(85 - 2 * 10, answer.changes["ExpandSettlers"]);
		Assert.Equal(1, answer.changes["war-production"]);
		Assert.Equal(15 + 2 * 5, answer.changes["FocusPoints"]);
		Assert.False(answer.changes.ContainsKey("Teleporters"));
		Assert.Equal("claim land early", answer.lesson);
		Assert.Null(PolicyReview.Parse("not json", player));
	}

	[Fact]
	public void AReviewedPolicyIsThePlayersOwnAndIsSaved() {
		player.aiType = AiType.Advanced;
		PolicyReview.Answer answer = PolicyReview.Parse("""{"changes": [{"knob": "war-production", "value": 1}], "rationale": "r", "notes": "n", "lesson": "l"}""", player);
		PolicyReview.Adopt(player, gameData, answer, PolicyReview.Snapshot(player, gameData));

		Assert.Contains("war-production", AdvancedTuning.BehavioursOn(player));
		Assert.DoesNotContain("war-production", AdvancedTuning.BehavioursOn(rival));
		C7GameData.GameData loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON()).ToGameData(fixture.behaviors);
		AdvancedPolicy back = loaded.players.Single(p => p.id == player.id).advancedPolicy;
		Assert.Equal(1, back.knobs["war-production"]);
		Assert.Single(back.reviews);
		Assert.Equal("n", back.notes);
	}

	// Each review records how the last one turned out in the playbook, and
	// the next review's briefing reads it back.
	[Fact]
	public void ThePlaybookCarriesLessonsToLaterReviews() {
		string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"playbook-{Guid.NewGuid():N}.jsonl");
		PolicyPlaybook.path = path;
		try {
			player.aiType = AiType.Advanced;
			PolicySnapshot first = PolicyReview.Snapshot(player, gameData);
			PolicyReview.Adopt(player, gameData, PolicyReview.Parse(
				"""{"changes": [{"knob": "ExpandSettlers", "value": 95}], "rationale": "r", "notes": "", "lesson": "settle hard early"}""", player), first);
			Assert.False(System.IO.File.Exists(path));

			gameData.turn += PolicyReview.ReviewEveryTurns;
			PolicyReview.Adopt(player, gameData, PolicyReview.Parse(
				"""{"changes": [], "rationale": "fine", "notes": "", "lesson": "x"}""", player), PolicyReview.Snapshot(player, gameData));
			PolicyPlaybook.Entry entry = Assert.Single(PolicyPlaybook.Read(first.era));
			Assert.Equal("settle hard early", entry.lesson);
			Assert.Equal(95, entry.changes["ExpandSettlers"]);

			string briefing = PolicyReview.Briefing(player, gameData, PolicyReview.Snapshot(player, gameData));
			Assert.Contains("settle hard early", briefing);
			Assert.Contains("ExpandSettlers = 95", briefing);
		} finally {
			PolicyPlaybook.path = null;
			System.IO.File.Delete(path);
		}
	}

	[Fact]
	public void TheBriefingNamesOnlyRivalsTheCivHasMet() {
		player.aiType = AiType.Advanced;
		PlanBriefing.Result briefing = PlanBriefing.Situation(player, gameData);
		Assert.Contains(rival.civilization.name, briefing.text);
		Assert.Equal(rival.id, briefing.rivals[rival.civilization.name]);
		foreach (Player stranger in gameData.players.Where(p => p != player && !player.playerRelationships.ContainsKey(p.id))) {
			Assert.DoesNotContain(stranger.id, briefing.rivals.Values);
		}
	}

	[Fact]
	public void AReviewIsDueEveryFiftyTurns() {
		player.aiType = AiType.Advanced;
		gameData.turn = PolicyReview.ReviewEveryTurns - 1;
		Assert.False(PolicyReview.Due(player, gameData));
		gameData.turn = PolicyReview.ReviewEveryTurns;
		Assert.True(PolicyReview.Due(player, gameData));
		PolicyReview.Adopt(player, gameData, new PolicyReview.Answer(), PolicyReview.Snapshot(player, gameData));
		Assert.False(PolicyReview.Due(player, gameData));
		gameData.turn += PolicyReview.ReviewEveryTurns;
		Assert.True(PolicyReview.Due(player, gameData));
	}

	[Fact]
	public void WithNoKeyTheModelIsNeverAsked() {
		Assert.False(ModelPlanner.Available);
		player.aiType = AiType.Advanced;
		gameData.turn = PolicyReview.ReviewEveryTurns;
		AdvancedAI.PlayTurn(player, gameData);
		Assert.False(ModelPlanner.Waiting(player));
		Assert.Null(player.advancedPolicy);
		Assert.Equal("rules", player.strategicPlan.source);
	}
}

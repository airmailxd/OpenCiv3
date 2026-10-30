using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using C7Engine;
using C7Engine.AI.Advanced;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The category weights: the effective-weight arithmetic, and the 50 = no
// change rule.
public sealed class RuleWeightsTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;

	public RuleWeightsTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		player = gameData.players.First(p => !p.isHuman && !p.IsIndependent);
		player.aiType = AiType.Advanced;
		AdvancedTuning.Override("");
	}

	public void Dispose() {
		AdvancedTuning.Override("");
		RuleWeights.ResetUse();
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static readonly RuleWeights.Situation Calm = new(era: 0, openSites: 5, expanding: true);

	[Fact]
	public void BaseTimesEraTimesStateTimesOpportunity() {
		RuleWeights.Situation s = new(era: 1, openSites: 3, scarceLand: true, behindInTech: true);
		Assert.Equal(80 * .8, RuleWeights.Effective(RuleCategory.Expansion, 80, new(era: 1, openSites: 3)), 6);
		Assert.Equal(80 * .8 * RuleWeights.ScarceLand, RuleWeights.Effective(RuleCategory.Expansion, 80, s), 6);
		Assert.Equal(60 * 1.1 * 1.2, RuleWeights.Effective(RuleCategory.Research, 60, s), 6);
	}

	[Fact]
	public void ExpansionIsFullInTheAncientEraWithLandAndFallsWithoutSitesOrUnderThreat() {
		double open = RuleWeights.Effective(RuleCategory.Expansion, 95, Calm);
		double none = RuleWeights.Effective(RuleCategory.Expansion, 95, Calm with { openSites = 0 });
		double invaded = RuleWeights.Effective(RuleCategory.Expansion, 95, Calm with { invasionThreat = true });
		Assert.Equal(95, open, 6);
		Assert.Equal(95 * RuleWeights.NoSites, none, 6);
		Assert.Equal(95 * RuleWeights.InvasionThreat, invaded, 6);
		Assert.True(none < 30 && invaded < 40);
	}

	[Fact]
	public void EachStateMultiplierActsOnItsCategory() {
		RuleWeights.Situation s = new(openSites: 4);
		Assert.Equal(50 * RuleWeights.InvasionThreat, RuleWeights.Effective(RuleCategory.Military, 50, s with { invasionThreat = true }), 6);
		Assert.Equal(50 * RuleWeights.ContestedLand, RuleWeights.Effective(RuleCategory.Expansion, 50, s with { contestedLand = true }), 6);
		Assert.Equal(40 * RuleWeights.ResearchExpansionPhase, RuleWeights.Effective(RuleCategory.Research, 40, s with { expanding = true }), 6);
		Assert.Equal(40 * RuleWeights.WondersExpansionPhase, RuleWeights.Effective(RuleCategory.Wonders, 40, s with { expanding = true }), 6);
		Assert.Equal(50 * RuleWeights.CorruptCity, RuleWeights.Effective(RuleCategory.Infrastructure, 50, s with { corruptCity = true }), 6);
		Assert.Equal(50 * RuleWeights.StrongestRival, RuleWeights.Effective(RuleCategory.Diplomacy, 50, s with { strongestRivalCanFight = true }), 6);
		Assert.Equal(50 * RuleWeights.MissingResources, RuleWeights.Effective(RuleCategory.Diplomacy, 50, s with { missingResources = true }), 6);
		Assert.Equal(50, RuleWeights.Effective(RuleCategory.Workers, 50, s), 6);
	}

	[Fact]
	public void EraSlowsExpansionAndSpeedsResearch() {
		RuleWeights.Situation s = new(openSites: 4);
		Assert.True(RuleWeights.Effective(RuleCategory.Expansion, 90, s with { era = 3 }) < RuleWeights.Effective(RuleCategory.Expansion, 90, s with { era = 1 }));
		Assert.True(RuleWeights.Effective(RuleCategory.Research, 50, s with { era = 3 }) > RuleWeights.Effective(RuleCategory.Research, 50, s));
		Assert.Equal(RuleWeights.Effective(RuleCategory.Research, 50, s with { era = 3 }), RuleWeights.Effective(RuleCategory.Research, 50, s with { era = 9 }));
	}

	[Fact]
	public void ResultsAreClampedToZeroToOneHundred() {
		RuleWeights.Situation s = new(era: 2, openSites: 2, behindInTech: true, expanding: true);
		Assert.Equal(100, RuleWeights.Effective(RuleCategory.Research, 100, s));
		Assert.Equal(0, RuleWeights.Effective(RuleCategory.Research, -20, s));
		Assert.Equal(100, RuleWeights.Effective(RuleCategory.Wonders, 200, s));
	}

	[Fact]
	public void AWeightOfFiftyChangesNoLever() {
		Assert.Equal(85, RuleWeights.Lean(50, 85));
		Assert.Equal(65, RuleWeights.Lean(50, 65, 50));
		Assert.Equal(20, RuleWeights.Lean(50, 20, 0));
		Assert.Equal(100, RuleWeights.Lean(100, 75));
		Assert.Equal(50, RuleWeights.Lean(0, 85));
		Assert.Equal(0, RuleWeights.Lean(0, 20, 0));
	}

	[Fact]
	public void DefaultWeightsAreFiftyAndEffectiveIsTheBaseWithoutTheStateSwitch() {
		foreach (RuleCategory c in Enum.GetValues<RuleCategory>()) {
			Assert.Equal(50, AdvancedTuning.Get("Weight" + c));
			Assert.Equal(50, RuleWeights.Effective(player, c));
		}
		AdvancedTuning.Override("WeightExpansion=95,WeightWonders=40");
		Assert.Equal(95, RuleWeights.Effective(player, RuleCategory.Expansion));
		Assert.Equal(40, RuleWeights.Effective(player, RuleCategory.Wonders));
	}

	[Fact]
	public void AStandardAiAlwaysHasFifty() {
		AdvancedTuning.Override("WeightExpansion=95");
		player.aiType = AiType.Standard;
		Assert.Equal(50, RuleWeights.Effective(player, RuleCategory.Expansion));
	}

	[Fact]
	public void TheStateSwitchWorksTheWeightsOutAtReplanTime() {
		AdvancedTuning.Override($"{RuleWeights.ByState}=1");
		RuleWeights.Update(player, gameData);
		foreach (RuleCategory c in Enum.GetValues<RuleCategory>()) {
			Assert.InRange(RuleWeights.Effective(player, c), 0, 100);
		}
		RuleWeights.Situation s = RuleWeights.Observe(player, gameData);
		Assert.Equal(RuleWeights.Effective(RuleCategory.Expansion, 50, s), RuleWeights.Effective(player, RuleCategory.Expansion), 6);
	}

	[Fact]
	public void AllFiftyMakesTheSamePlanAsBefore() {
		StrategicPlan before = RulePlanner.Plan(player, gameData);
		AdvancedTuning.Override("WeightExpansion=50,WeightWorkers=50,WeightResearch=50,WeightMilitary=50,WeightDiplomacy=50,WeightInfrastructure=50,WeightWonders=50");
		StrategicPlan after = RulePlanner.Plan(player, gameData);
		Assert.Equal(AdvancedAI.Describe(before), AdvancedAI.Describe(after));
		Assert.Empty(RuleWeights.UseOf(player));
	}

	[Fact]
	public void ExpansionWeightLeansTheSettlerFocusAndIsLogged() {
		StrategicPlan baseline = RulePlanner.Plan(player, gameData);
		AdvancedTuning.Override("WeightExpansion=100,WeightResearch=100,WeightInfrastructure=100,WeightMilitary=100");
		StrategicPlan heavy = RulePlanner.Plan(player, gameData);
		AdvancedTuning.Override("WeightExpansion=0,WeightResearch=0,WeightInfrastructure=0,WeightMilitary=0");
		StrategicPlan light = RulePlanner.Plan(player, gameData);
		foreach (PlanFocus f in new[] { PlanFocus.Settlers, PlanFocus.Science, PlanFocus.Production, PlanFocus.Offense }) {
			Assert.True(heavy.Focus(f) >= baseline.Focus(f) && baseline.Focus(f) >= light.Focus(f), f.ToString());
		}
		Assert.NotEmpty(RuleWeights.UseOf(player));
	}

	// The registry's modifier means, as used in RuleWeights.
	[Fact]
	public void TheMultipliersMatchTheRuleRegistry() {
		string dir = AppContext.BaseDirectory;
		while (dir != null && !File.Exists(Path.Combine(dir, "doc", "suede-rules.json"))) {
			dir = Path.GetDirectoryName(dir);
		}
		if (dir == null) {
			return;
		}
		using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "doc", "suede-rules.json")));
		double Mean(string category, string fact) {
			var factors = doc.RootElement.GetProperty("rules").EnumerateArray()
				.Where(r => r.GetProperty("category").GetString() == category && r.GetProperty("validation").GetString() != "disabled")
				.SelectMany(r => r.GetProperty("modifiers").EnumerateArray())
				.Where(m => m.GetProperty("fact").GetString() == fact)
				.Select(m => m.GetProperty("factor").GetDouble()).ToList();
			return factors.Average();
		}
		Assert.Equal(RuleWeights.ScarceLand, Mean("expansion", "scarceLand"), 2);
		Assert.Equal(RuleWeights.ContestedLand, Mean("expansion", "contestedLand"), 2);
		Assert.Equal(RuleWeights.InvasionThreat, Mean("expansion", "invasionThreat"), 2);
		Assert.Equal(RuleWeights.InvasionThreat, Mean("military", "invasionThreat"), 2);
		Assert.Equal(RuleWeights.ResearchExpansionPhase, Mean("research-economy", "expansionPhase"), 2);
		Assert.Equal(RuleWeights.WondersExpansionPhase, Mean("wonders", "expansionPhase"), 2);
		Assert.Equal(RuleWeights.CorruptCity, Mean("infrastructure", "corruptCity"), 2);
		Assert.Equal(RuleWeights.StrongestRival, Mean("diplomacy", "strongestRival"), 2);
	}
}

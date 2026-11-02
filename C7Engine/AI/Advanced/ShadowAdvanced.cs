using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine.AI.Advanced {
	// Asks the AI what it would do for a player it doesn't play, as the
	// Advanced AI with its default tuning, without changing the game: the
	// play recorder's "shadow" decisions for a human (see PlayLog.PlayRecorder).
	//
	// While Evaluate runs, the player counts as an Advanced AI
	// (Player.IsAdvancedAi), with the on-trial behaviours its tuning turns on
	// (AiExperiments.Has, nothing enabled for it) and the AI's fog of war
	// (AIFogOfWar), and is steered by a plan the caller keeps for it
	// (StrategicPlan). Nothing else changes, and nothing outside Evaluate
	// sees any of it:
	// - The game's random numbers are untouched. The AI's dice (GameData.rng,
	//   such as ChooseProducible's +-10%) roll a stand-in that always comes up
	//   in the middle of its range, so the AI's choice is its expected one
	//   rather than a roll, the same every time, and the game's own random
	//   number generator, which a save and a LAN game depend on, is restored
	//   untouched.
	// - The player's plan is put back as it was.
	// - The AI's fog of war doesn't recompute what the player sees (the game
	//   keeps a human's view up to date itself), and the rule-use counts
	//   (RuleWeights.Used) aren't counted.
	// The engine handles one message at a time on one thread and the AIs
	// don't play meanwhile, so nothing else reads the swapped state.
	public static class ShadowAdvanced {
		private static Player shadowed;
		private static HashSet<string> behaviours;

		// Whether an evaluation is running.
		public static bool IsActive => shadowed != null;

		// Whether the player is the one being evaluated as an Advanced AI.
		internal static bool Covers(Player player) => player != null && ReferenceEquals(player, shadowed);

		internal static bool HasBehaviour(string name) => behaviours != null && behaviours.Contains(name);

		// What the caller keeps for a player between evaluations: the plan the
		// Advanced AI would be playing by.
		public sealed class State {
			public StrategicPlan plan;
		}

		public static T Evaluate<T>(GameData gameData, Player player, State state, Func<T> evaluate) {
			if (shadowed != null) {
				return evaluate();
			}
			Random gameRandom = gameData.random;
			StrategicPlan ownPlan = player.strategicPlan;
			shadowed = player;
			behaviours = AdvancedTuning.BehavioursOn(player).ToHashSet();
			gameData.random = MiddleRandom.Instance;
			player.strategicPlan = state?.plan;
			try {
				// Re-plan as AdvancedAI.PlayTurn would.
				if (state != null && AdvancedAI.NeedsNewPlan(player, gameData)) {
					StrategicPlan plan = RulePlanner.Plan(player, gameData);
					AdvancedAI.Stamp(plan, player, gameData);
					player.strategicPlan = state.plan = plan;
				}
				return evaluate();
			} finally {
				gameData.random = gameRandom;
				player.strategicPlan = ownPlan;
				shadowed = null;
				behaviours = null;
			}
		}

		// Random numbers that are always the middle of the range asked for.
		internal sealed class MiddleRandom : Random {
			public static readonly MiddleRandom Instance = new();

			protected override double Sample() => 0.5;
			public override double NextDouble() => 0.5;
			public override float NextSingle() => 0.5f;
			public override int Next() => int.MaxValue / 2;
			public override int Next(int maxValue) => maxValue / 2;
			public override int Next(int minValue, int maxValue) => minValue + (maxValue - minValue) / 2;
			public override long NextInt64() => long.MaxValue / 2;
			public override long NextInt64(long maxValue) => maxValue / 2;
			public override long NextInt64(long minValue, long maxValue) => minValue + (maxValue - minValue) / 2;
			public override void NextBytes(byte[] buffer) => Array.Fill(buffer, (byte)128);
			public override void NextBytes(Span<byte> buffer) => buffer.Fill(128);
		}
	}
}

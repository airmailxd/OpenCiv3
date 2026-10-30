using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using C7GameData;
using Serilog;

namespace C7Engine.AI.Advanced {
	// The Advanced AI's line to Claude, used by its policy reviews (see
	// PolicyReview). Off by default: only with the switch (enabled) on AND
	// an API key is anything sent; otherwise the Advanced AI plays by its
	// rules and the trained policy alone. (Trials haven't shown the model's
	// advice helps, so a key alone no longer turns it on.)
	//
	// The game doesn't wait on an answer (but see waitForAnswers): a request
	// runs in the background and its answer is picked up on a later turn.
	// A failed or refused call is logged and dropped.
	public static class ModelPlanner {
		private static readonly ILogger log = Log.ForContext(typeof(ModelPlanner));

		public const string DefaultModel = "claude-opus-5-5";

		// Set by the game from its settings (see AdvancedAiSettings), or by
		// an AI trial that asks for the model (C7_TRIAL_MODEL). Nothing else
		// sets it, so tests never make paid calls.
		public static string apiKey;
		// The switch; off unless the player (Settings -> Advanced AI) or a
		// trial (C7_TRIAL_MODEL) turns it on.
		public static bool enabled;
		public static string model = DefaultModel;
		public static Effort effort = Effort.Medium;
		// AI trials play hundreds of turns a minute, so an answer would land
		// long after the turn it was asked on; they wait for it instead.
		public static bool waitForAnswers;

		public static bool Available => enabled && !string.IsNullOrWhiteSpace(apiKey);

		// Lets AI trials total up calls and tokens. Null outside a trial.
		internal static Action<long, long> onUsage;

		private static readonly ConditionalWeakTable<Player, Task<string>> pending = new();

		// Tokens (in, out) of the call whose answer was last taken, for the
		// review log.
		private sealed class Usage { public long input, output; }
		private static readonly ConditionalWeakTable<Player, Usage> usage = new();

		public static (long input, long output) LastUsage(Player player) =>
			usage.TryGetValue(player, out Usage u) ? (u.input, u.output) : (0, 0);

		public static bool Waiting(Player player) => pending.TryGetValue(player, out Task<string> t) && !t.IsCompleted;

		// Sends a request for this player; its answer (JSON text) comes back
		// from TakeAnswer.
		public static void Ask(Player player, string system, string user, Dictionary<string, JsonElement> schema) {
			if (!Available || Waiting(player)) {
				return;
			}
			string civ = player.civilization.name;
			string key = apiKey;
			string modelName = model;
			Effort level = effort;
			Usage used = new();
			usage.AddOrUpdate(player, used);
			pending.AddOrUpdate(player, Task.Run(() => Call(key, modelName, level, civ, system, user, schema, used)));
		}

		// The answer to this player's last request, if it has come (or, with
		// waitForAnswers, once it comes); null if none, or if it failed.
		public static string TakeAnswer(Player player) {
			if (!pending.TryGetValue(player, out Task<string> task)) {
				return null;
			}
			if (waitForAnswers && !task.IsCompleted) {
				task.Wait(TimeSpan.FromMinutes(5));
			}
			if (!task.IsCompleted) {
				return null;
			}
			pending.Remove(player);
			return task.IsCompletedSuccessfully ? task.Result : null;
		}

		private static async Task<string> Call(string key, string modelName, Effort level, string civ, string system, string user,
				Dictionary<string, JsonElement> schema, Usage used) {
			Stopwatch watch = Stopwatch.StartNew();
			try {
				AnthropicClient client = new() { ApiKey = key };
				Message response = await client.Messages.Create(new MessageCreateParams {
					Model = modelName,
					MaxTokens = 16000,
					System = system,
					OutputConfig = new OutputConfig { Effort = level, Format = new JsonOutputFormat { Schema = schema } },
					Messages = [new UserMessageParam { Content = user }],
				});
				used.input = response.Usage.InputTokens;
				used.output = response.Usage.OutputTokens;
				onUsage?.Invoke(response.Usage.InputTokens, response.Usage.OutputTokens);
				log.Information("Model call for {Civ}: {Ms} ms, {In} in / {Out} out tokens", civ, watch.ElapsedMilliseconds,
					response.Usage.InputTokens, response.Usage.OutputTokens);
				if (response.StopReason == "refusal") {
					log.Warning("Model call for {Civ} was refused", civ);
					return null;
				}
				return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
			} catch (Exception e) {
				log.Warning(e, "Model call for {Civ} failed after {Ms} ms", civ, watch.ElapsedMilliseconds);
				return null;
			}
		}

		public static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
	}
}

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
	// The Advanced AI's planner when an Anthropic API key is set up: Claude
	// reads a briefing of what the civ knows (see PlanBriefing) and answers
	// with a plan in a fixed JSON shape. Without a key the rule planner
	// alone plans (see RulePlanner).
	//
	// The game never waits on it. When the AI wants a new plan it takes the
	// rule planner's at once and sends the briefing; the model's answer,
	// checked and clamped, replaces it when it arrives (the next turn,
	// usually). A failed or refused call just leaves the rules' plan, and a
	// model plan lapses after AdvancedAI.PlanLastsTurns like any other, so
	// a dead connection hands the AI back to the rules.
	public static class ModelPlanner {
		private static readonly ILogger log = Log.ForContext(typeof(ModelPlanner));

		public const string DefaultModel = "claude-opus-5-5";

		// Set by the game from its settings (see AdvancedAiSettings), or by
		// an AI trial that asks for the model (C7_TRIAL_MODEL). Nothing else
		// sets it, so tests never make paid calls.
		public static string apiKey;
		public static string model = DefaultModel;

		public static bool Available => !string.IsNullOrWhiteSpace(apiKey);

		private static readonly ConditionalWeakTable<Player, Task<StrategicPlan>> pending = new();

		// The model's answer for this player, if one has come in since the
		// last call; null otherwise.
		public static StrategicPlan TakeAnswer(Player player) {
			if (!pending.TryGetValue(player, out Task<StrategicPlan> task) || !task.IsCompleted) {
				return null;
			}
			pending.Remove(player);
			return task.IsCompletedSuccessfully ? task.Result : null;
		}

		public static bool Waiting(Player player) => pending.TryGetValue(player, out Task<StrategicPlan> t) && !t.IsCompleted;

		// Sends the briefing; the answer is picked up by TakeAnswer.
		public static void Ask(Player player, GameData gameData, StrategicPlan suggestion) {
			if (!Available || Waiting(player)) {
				return;
			}
			PlanBriefing.Result briefing = PlanBriefing.Build(player, gameData, suggestion);
			string civ = player.civilization.name;
			string key = apiKey;
			string modelName = model;
			pending.AddOrUpdate(player, Task.Run(() => Call(key, modelName, civ, briefing)));
		}

		private const string Instructions = """
			You are the grand strategist for one civilization in a game of Civilization III (Conquests rules).
			You do not move units or pick builds yourself. The game's own AI does that, and your plan tilts its choices:
			which kinds of builds its cities favour, how readily it starts wars, and which rival it attacks or leaves alone.
			Your aim is to win the game against the other civilizations, which may include strong human players.

			Judge the situation as it is. Avoid wishful thinking: do not plan offence in a war you are losing, and do not
			pick a war target that is not clearly weaker than you (with its allies) and reachable over land. A war on two
			fronts is usually a mistake. Early on, land is the best investment: claim open city sites before rivals do.
			Too many raised priorities dilute each other, so raise only the few that matter now. A plan lasts about 10 turns.

			Reply only with the JSON the schema asks for. In "rationale", say in a sentence or two why. In "notes", keep
			anything you want to remember at your next briefing (your longer-term intentions), in under 60 words.
			""";

		private static async Task<StrategicPlan> Call(string key, string modelName, string civ, PlanBriefing.Result briefing) {
			Stopwatch watch = Stopwatch.StartNew();
			try {
				AnthropicClient client = new() { ApiKey = key };
				Message response = await client.Messages.Create(new MessageCreateParams {
					Model = modelName,
					MaxTokens = 16000,
					System = Instructions,
					OutputConfig = new OutputConfig { Effort = Effort.Low, Format = new JsonOutputFormat { Schema = Schema(briefing) } },
					Messages = [new UserMessageParam { Content = briefing.text }],
				});
				if (response.StopReason == "refusal") {
					log.Warning("Model planner for {Civ}: refused; keeping the rules' plan", civ);
					return null;
				}
				string json = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
				StrategicPlan plan = Parse(json, briefing.rivals);
				log.Information("Model planner for {Civ}: {Ms} ms, {In} in / {Out} out tokens", civ, watch.ElapsedMilliseconds,
					response.Usage.InputTokens, response.Usage.OutputTokens);
				return plan;
			} catch (Exception e) {
				log.Warning(e, "Model planner for {Civ} failed after {Ms} ms; keeping the rules' plan", civ, watch.ElapsedMilliseconds);
				return null;
			}
		}

		// The answer's shape. Rival names are an enum of those in the
		// briefing, so the model can't name one it hasn't met.
		private static Dictionary<string, JsonElement> Schema(PlanBriefing.Result briefing) {
			Dictionary<string, object> focusProps = Enum.GetNames<PlanFocus>().ToDictionary(n => n, _ => (object)new { type = "integer" });
			object stanceItem = briefing.rivals.Count == 0
				? new { type = "object", properties = new { rival = new { type = "string" }, stance = new { type = "string" } }, required = new[] { "rival", "stance" }, additionalProperties = false }
				: new {
					type = "object",
					properties = new {
						rival = new { type = "string", @enum = briefing.rivals.Keys.ToArray() },
						stance = new { type = "string", @enum = Enum.GetNames<RivalStance>() },
					},
					required = new[] { "rival", "stance" },
					additionalProperties = false,
				};
			return new Dictionary<string, JsonElement> {
				["type"] = JsonSerializer.SerializeToElement("object"),
				["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> {
					["focus"] = new { type = "object", properties = focusProps, required = Enum.GetNames<PlanFocus>(), additionalProperties = false },
					["aggressionShift"] = new { type = "integer" },
					["stances"] = new { type = "array", items = stanceItem },
					["victory"] = new { type = "string", @enum = StrategicPlan.Victories },
					["rationale"] = new { type = "string" },
					["notes"] = new { type = "string" },
				}),
				["required"] = JsonSerializer.SerializeToElement(new[] { "focus", "aggressionShift", "stances", "victory", "rationale", "notes" }),
				["additionalProperties"] = JsonSerializer.SerializeToElement(false),
			};
		}

		// Reads the model's JSON into a plan, keeping only what is legal:
		// known categories, met rivals, values clamped (see
		// StrategicPlan.Clamp). Null if it can't be read at all.
		public static StrategicPlan Parse(string json, IReadOnlyDictionary<string, ID> rivals) {
			try {
				using JsonDocument doc = JsonDocument.Parse(json);
				JsonElement root = doc.RootElement;
				StrategicPlan plan = new() { source = "model" };
				if (root.TryGetProperty("focus", out JsonElement focus) && focus.ValueKind == JsonValueKind.Object) {
					foreach (JsonProperty f in focus.EnumerateObject()) {
						if (Enum.TryParse(f.Name, true, out PlanFocus pf) && f.Value.TryGetInt32(out int v)) {
							plan.focus[pf] = v;
						}
					}
				}
				if (root.TryGetProperty("aggressionShift", out JsonElement a) && a.TryGetInt32(out int shift)) {
					plan.aggressionShift = shift;
				}
				if (root.TryGetProperty("stances", out JsonElement stances) && stances.ValueKind == JsonValueKind.Array) {
					foreach (JsonElement s in stances.EnumerateArray()) {
						if (s.TryGetProperty("rival", out JsonElement r) && rivals.TryGetValue(r.GetString() ?? "", out ID id)
								&& s.TryGetProperty("stance", out JsonElement st) && Enum.TryParse(st.GetString(), true, out RivalStance stance)) {
							plan.stances[id.ToString()] = stance;
						}
					}
				}
				// Only one war target: the first named.
				foreach (string extra in plan.stances.Where(s => s.Value == RivalStance.Target).Select(s => s.Key).Skip(1).ToList()) {
					plan.stances[extra] = RivalStance.Neutral;
				}
				plan.victory = root.TryGetProperty("victory", out JsonElement vic) ? vic.GetString() : null;
				plan.rationale = root.TryGetProperty("rationale", out JsonElement why) ? why.GetString() : null;
				plan.notes = root.TryGetProperty("notes", out JsonElement notes) ? notes.GetString() : null;
				plan.Clamp();
				return plan;
			} catch (JsonException) {
				return null;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using C7GameData;
using Serilog;
using static C7GameData.PlayerRelationship;

namespace C7Engine.AI.Advanced {
	// Every ReviewEveryTurns turns, Claude reviews an Advanced AI's policy
	// (see AdvancedPolicy): the knobs its rule planner plays by and the
	// behaviours it uses. It doesn't play the game. It sees the situation,
	// how the civ has fared since its last review, what it changed then,
	// and lessons from earlier games (see PolicyPlaybook), and changes a few
	// knobs a step or two: keep what worked, undo what didn't, adjust to
	// the era and the situation. RuneBench's best agents worked this way,
	// tuning a running script now and then rather than acting every step,
	// and Vox Deorum found deciding less often lost nothing.
	public static class PolicyReview {
		private static readonly ILogger log = Log.ForContext(typeof(PolicyReview));

		public const int ReviewEveryTurns = 50;
		public const int MaxChanges = 3;
		public const int MaxStepsPerChange = 2;

		// How the civ stood when a review was asked for, for the record.
		private static readonly ConditionalWeakTable<Player, PolicySnapshot> askedAt = new();

		// Early in an Advanced AI's turn: adopts a review that has come in,
		// and asks for one when it is due.
		public static void PlayTurn(Player player, GameData gameData) {
			if (!ModelPlanner.Available) {
				return;
			}
			string answer = ModelPlanner.TakeAnswer(player);
			if (answer != null && askedAt.TryGetValue(player, out PolicySnapshot standing)) {
				Adopt(player, gameData, Parse(answer, player), standing);
			}
			if (Due(player, gameData) && !ModelPlanner.Waiting(player)) {
				Ask(player, gameData);
				// A trial waits for the answer, so it takes effect this turn.
				if (ModelPlanner.waitForAnswers) {
					PlayTurn(player, gameData);
				}
			}
		}

		public static bool Due(Player player, GameData gameData) {
			int last = player.advancedPolicy?.reviews.LastOrDefault()?.turn ?? 0;
			return gameData.turn >= ReviewEveryTurns && gameData.turn - last >= ReviewEveryTurns
				&& !(askedAt.TryGetValue(player, out PolicySnapshot s) && ModelPlanner.Waiting(player));
		}

		private static void Ask(Player player, GameData gameData) {
			PolicySnapshot standing = Snapshot(player, gameData);
			askedAt.AddOrUpdate(player, standing);
			ModelPlanner.Ask(player, Instructions, Briefing(player, gameData, standing), Schema());
		}

		public static PolicySnapshot Snapshot(Player player, GameData gameData) {
			List<Player> met = PlanBriefing.Met(player, gameData);
			List<Player> field = [player, .. met];
			float score = ScoreOf(player, gameData);
			float strongest = met.Count == 0 ? 0 : met.Max(p => p.CalculateMilitaryStrength());
			return new PolicySnapshot {
				score = (int)score,
				scoreRank = 1 + field.Count(p => p != player && ScoreOf(p, gameData) > score),
				civsKnown = field.Count,
				cities = player.cities.Count,
				averageRivalCities = met.Count == 0 ? 0 : met.Average(p => p.cities.Count),
				citizens = player.cities.Sum(c => c.residents.Count),
				techs = player.knownTechs.Count,
				averageRivalTechs = met.Count == 0 ? 0 : met.Average(p => p.knownTechs.Count),
				strengthVsStrongest = strongest <= 0 ? 0 : player.CalculateMilitaryStrength() / strongest,
				atWarWith = met.Count(p => AtWar(player, p)),
				era = PlanBriefing.EraName(player),
			};
		}

		private static float ScoreOf(Player p, GameData gameData) => p.defeated ? 0 : ScoreVictory.ComputeTurnScore(p, gameData);

		private const string Instructions = """
			You advise one civilization in a game of Civilization III (Conquests rules). You do not play the game:
			the game's own AI does, turn by turn, following a small set of policy knobs. Every 50 turns you review
			that policy and may change a few knobs. Your aim is for this civilization to win against all others,
			including strong human players.

			Work incrementally, like an experimenter:
			- Keep changes that were followed by a better standing; undo or reverse changes followed by a worse one.
			- Change at most 3 knobs, each by at most 2 of its steps. Changing nothing is a fine answer when things go well.
			- Adjust to the era and the situation: land early, economy and defence when boxed in, war only against a
			  clearly weaker neighbour, recovery when losing.
			- Use the lessons from earlier games, but trust what this game shows you over them.
			- Judge your standing as it is. Avoid wishful thinking.

			Reply only with the JSON the schema asks for. "rationale": a sentence or two on why. "notes": what to
			remember at your next review (under 60 words). "lesson": one general sentence for future games, about what
			to do in a situation like this one.
			""";

		public static string Briefing(Player player, GameData gameData, PolicySnapshot now) {
			PlanBriefing.Result situation = PlanBriefing.Situation(player, gameData);
			AdvancedPolicy policy = player.advancedPolicy;
			StringBuilder b = new(situation.text);
			b.AppendLine();

			b.AppendLine("## How you have done");
			b.AppendLine("| Turn | Era | Score rank | Cities (rivals' average) | Techs (rivals' average) | Military vs strongest rival | Wars |");
			b.AppendLine("|---|---|---|---|---|---|---|");
			foreach (PolicyReviewRecord r in policy?.reviews ?? []) {
				b.AppendLine(Row(r.turn, r.standing));
			}
			b.AppendLine(Row(gameData.turn, now));
			b.AppendLine();

			b.AppendLine("## Your policy");
			foreach (AdvancedTuning.Knob k in AdvancedTuning.Knobs) {
				double v = AdvancedTuning.Get(player, k.name);
				string value = k.IsBehaviour ? (v >= .5 ? "on (1)" : "off (0)") : Num(v);
				string range = k.IsBehaviour ? "0 or 1" : $"{Num(k.min)} to {Num(k.max)}, step {Num(k.step)}";
				b.AppendLine($"- {k.name} = {value} (started at {Num(AdvancedTuning.Get(k.name))}; {range}): {k.meaning}");
			}
			b.AppendLine();

			List<PolicyReviewRecord> reviews = policy?.reviews ?? [];
			if (reviews.Count > 0) {
				b.AppendLine("## Your past reviews this game, and what followed");
				for (int i = Math.Max(0, reviews.Count - 4); i < reviews.Count; ++i) {
					PolicyReviewRecord r = reviews[i];
					PolicySnapshot after = i + 1 < reviews.Count ? reviews[i + 1].standing : now;
					b.AppendLine($"- Turn {r.turn}: {Changes(r.changes)}. Why: {r.rationale} Then: {Outcome(r.standing, after)}");
				}
				b.AppendLine();
			}

			List<PolicyPlaybook.Entry> lessons = PolicyPlaybook.Read(now.era);
			if (lessons.Count > 0) {
				b.AppendLine("## Lessons from earlier games");
				foreach (PolicyPlaybook.Entry e in lessons) {
					b.AppendLine($"- {e.era}, {e.situation}: {Changes(e.changes)} → {e.outcome}. Lesson: {e.lesson}");
				}
				b.AppendLine();
			}

			if (!string.IsNullOrWhiteSpace(policy?.notes)) {
				b.AppendLine("## Your notes");
				b.AppendLine(policy.notes);
			}
			return b.ToString();
		}

		private static string Row(int turn, PolicySnapshot s) =>
			$"| {turn} | {s.era} | {s.scoreRank} of {s.civsKnown} | {s.cities} ({s.averageRivalCities:0.#}) | {s.techs} ({s.averageRivalTechs:0.#}) "
			+ $"| {(s.strengthVsStrongest <= 0 ? "no rival met" : PlanBriefing.StrengthWords((float)s.strengthVsStrongest))} | {s.atWarWith} |";

		private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

		private static string Changes(Dictionary<string, double> changes) =>
			changes == null || changes.Count == 0 ? "no change" : string.Join(", ", changes.Select(c => $"{c.Key} to {Num(c.Value)}"));

		public static string Outcome(PolicySnapshot before, PolicySnapshot after) =>
			$"score rank {before.scoreRank} to {after.scoreRank} of {after.civsKnown}; cities {before.cities} to {after.cities} "
			+ $"(rivals' average {before.averageRivalCities:0.#} to {after.averageRivalCities:0.#}); techs {before.techs} to {after.techs} "
			+ $"(rivals' average {before.averageRivalTechs:0.#} to {after.averageRivalTechs:0.#})";

		// Places gained in score rank, plus the gain in cities relative to
		// the rivals' average (a city more than them is worth a tenth of a
		// place).
		public static double Gain(PolicySnapshot before, PolicySnapshot after) =>
			before.scoreRank - after.scoreRank
			+ .1 * ((after.cities - after.averageRivalCities) - (before.cities - before.averageRivalCities));

		private static Dictionary<string, JsonElement> Schema() => new() {
			["type"] = ModelPlanner.Json("object"),
			["properties"] = ModelPlanner.Json(new Dictionary<string, object> {
				["changes"] = new {
					type = "array",
					items = new {
						type = "object",
						properties = new {
							knob = new { type = "string", @enum = AdvancedTuning.Knobs.Select(k => k.name).ToArray() },
							value = new { type = "number" },
						},
						required = new[] { "knob", "value" },
						additionalProperties = false,
					},
				},
				["rationale"] = new { type = "string" },
				["notes"] = new { type = "string" },
				["lesson"] = new { type = "string" },
			}),
			["required"] = ModelPlanner.Json(new[] { "changes", "rationale", "notes", "lesson" }),
			["additionalProperties"] = ModelPlanner.Json(false),
		};

		public sealed class Answer {
			public Dictionary<string, double> changes = new();
			public string rationale;
			public string notes;
			public string lesson;
		}

		// Reads the model's JSON, keeping only legal changes: known knobs, at
		// most MaxChanges, each at most MaxStepsPerChange steps from the
		// player's current value and within the knob's range. Null if it
		// can't be read.
		public static Answer Parse(string json, Player player) {
			try {
				using JsonDocument doc = JsonDocument.Parse(json);
				JsonElement root = doc.RootElement;
				Answer answer = new() {
					rationale = Text(root, "rationale"),
					notes = Text(root, "notes"),
					lesson = Text(root, "lesson"),
				};
				if (root.TryGetProperty("changes", out JsonElement changes) && changes.ValueKind == JsonValueKind.Array) {
					foreach (JsonElement c in changes.EnumerateArray()) {
						if (answer.changes.Count >= MaxChanges) {
							break;
						}
						string name = c.TryGetProperty("knob", out JsonElement k) ? k.GetString() : null;
						if (!AdvancedTuning.IsKnob(name) || answer.changes.ContainsKey(name)
								|| !c.TryGetProperty("value", out JsonElement v) || !v.TryGetDouble(out double value)) {
							continue;
						}
						AdvancedTuning.Knob knob = AdvancedTuning.KnobNamed(name);
						double current = AdvancedTuning.Get(player, name);
						double most = MaxStepsPerChange * knob.step;
						value = AdvancedTuning.Clamp(name, Math.Clamp(value, current - most, current + most));
						if (Math.Abs(value - current) > 1e-9) {
							answer.changes[name] = value;
						}
					}
				}
				return answer;
			} catch (JsonException) {
				return null;
			}
		}

		private static string Text(JsonElement root, string name) =>
			root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

		// Applies a review: records how the last one turned out (in the
		// playbook too), then the new changes; the rules re-plan next turn.
		public static void Adopt(Player player, GameData gameData, Answer answer, PolicySnapshot standing) {
			if (answer == null) {
				return;
			}
			AdvancedPolicy policy = player.advancedPolicy ??= new AdvancedPolicy();
			if (policy.reviews.LastOrDefault() is PolicyReviewRecord last) {
				PolicyPlaybook.Add(new PolicyPlaybook.Entry {
					civ = player.civilization.name,
					era = last.standing.era,
					fromTurn = last.turn,
					toTurn = gameData.turn,
					situation = Situation(last.standing),
					changes = last.changes,
					outcome = Outcome(last.standing, standing),
					gain = Gain(last.standing, standing),
					lesson = last.lesson,
				});
			}
			foreach ((string name, double value) in answer.changes) {
				policy.knobs[name] = value;
			}
			policy.notes = answer.notes;
			policy.reviews.Add(new PolicyReviewRecord {
				turn = gameData.turn,
				standing = standing,
				changes = answer.changes,
				rationale = answer.rationale,
				lesson = answer.lesson,
			});
			if (player.strategicPlan != null) {
				player.strategicPlan.expiresTurn = gameData.turn;
			}
			log.Information("{Player} reviewed its policy on turn {Turn}: {Changes}. {Why}", player, gameData.turn,
				Changes(answer.changes), answer.rationale);
		}

		public static string Situation(PolicySnapshot s) {
			string war = s.atWarWith > 0 ? $"at war with {s.atWarWith}" : "at peace";
			string land = s.cities >= s.averageRivalCities * 1.2 ? "more cities than rivals"
				: s.cities * 1.2 <= s.averageRivalCities ? "fewer cities than rivals" : "as many cities as rivals";
			string tech = s.techs >= s.averageRivalTechs * 1.1 ? "ahead in techs"
				: s.techs * 1.1 <= s.averageRivalTechs ? "behind in techs" : "level in techs";
			return $"{war}, rank {s.scoreRank} of {s.civsKnown}, {land}, {tech}";
		}
	}
}

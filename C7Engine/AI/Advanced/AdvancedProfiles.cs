using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using C7GameData;
using Serilog;

namespace C7Engine.AI.Advanced {
	// The Advanced AI's map profiles: knob values (see AdvancedTuning) for a
	// kind of map, which the AI plays by on such a map in place of the
	// knobs' defaults. A profile's key names any of the map's world size,
	// landform and water percent, "/" between them, in any order and any
	// case: "Pangaea", "Huge", "Pangaea/70", "Huge/Archipelago/80". A profile
	// fits a map when every part of its key is the map's. Of the profiles
	// that fit, the most specific (the most parts) is the one played by; at
	// equal parts, one naming the world size beats one that doesn't, then
	// one naming the landform. Only that profile applies: its knobs over the
	// defaults, the knobs it doesn't name at their defaults.
	//
	// The profiles ship with the game mode, in advanced-ai-profiles.json
	// beside its ruleset (C7/Lua/civ3/), as
	//   { "profiles": { "Pangaea/70": { "ExpandSettlers": 95, "fast-expand": 1 } } }
	// and are made by training (tools/advanced-ai-training/train.py
	// --export-profile). A map the game didn't generate (an imported Civ3
	// save or scenario, or a save from before maps kept what they were
	// generated as) has no profile.
	public sealed class AdvancedProfiles {
		private static readonly ILogger log = Log.ForContext<AdvancedProfiles>();

		public const string FileName = "advanced-ai-profiles.json";

		public static readonly AdvancedProfiles None = new(new Dictionary<string, Dictionary<string, double>>());

		// By key, as written in the file.
		public readonly IReadOnlyDictionary<string, Dictionary<string, double>> profiles;

		public AdvancedProfiles(Dictionary<string, Dictionary<string, double>> profiles) {
			this.profiles = profiles;
		}

		// The profiles in a file; none if there is no file. A profile naming
		// a knob there is none of is an error, as is a file that can't be
		// read, so a typo can't silently tune nothing.
		public static AdvancedProfiles Load(string path) {
			if (path == null || !File.Exists(path)) {
				return None;
			}
			return Parse(File.ReadAllText(path), path);
		}

		public static AdvancedProfiles Parse(string json, string source = "profiles") {
			using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions {
				CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
			});
			Dictionary<string, Dictionary<string, double>> profiles = new();
			if (doc.RootElement.TryGetProperty("profiles", out JsonElement all)) {
				foreach (JsonProperty p in all.EnumerateObject()) {
					Dictionary<string, double> knobs = new();
					foreach (JsonProperty k in p.Value.EnumerateObject()) {
						if (!AdvancedTuning.IsKnob(k.Name)) {
							throw new ArgumentException($"{source}: profile {p.Name} has an unknown Advanced AI knob, {k.Name}");
						}
						knobs[k.Name] = k.Value.GetDouble();
					}
					profiles[p.Name] = knobs;
				}
			}
			return new AdvancedProfiles(profiles);
		}

		// A game mode's profiles: those beside the rules it plays by, else
		// beside its base's. A file that can't be read is logged, and the
		// game played with none.
		public static AdvancedProfiles ForGameMode(string gameModesDir, string baseModeDir, string rulesetDir) {
			string path = Path.Combine(gameModesDir, rulesetDir ?? baseModeDir, FileName);
			if (rulesetDir != null && !File.Exists(path)) {
				path = Path.Combine(gameModesDir, baseModeDir, FileName);
			}
			try {
				return Load(path);
			} catch (Exception e) when (e is JsonException or ArgumentException or IOException or InvalidOperationException or FormatException) {
				log.Error(e, "Couldn't read the Advanced AI's map profiles at {Path}; playing with none", path);
				return None;
			}
		}

		// Whether a profile key fits a map, and how specific it is.
		public static bool Fits(string key, MapGeneration map, out int parts, out int rank) {
			parts = 0;
			rank = 0;
			if (map == null || string.IsNullOrWhiteSpace(key)) {
				return false;
			}
			string[] mapParts = map.Parts().ToArray();
			HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
			foreach (string raw in key.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) {
				int at = Array.FindIndex(mapParts, m => string.Equals(m, raw, StringComparison.OrdinalIgnoreCase));
				if (at < 0 || !seen.Add(raw)) {
					return false;
				}
				++parts;
				// The world size outranks the landform, which outranks the
				// water.
				rank += PartRank(map, mapParts[at]);
			}
			return parts > 0;
		}

		private static int PartRank(MapGeneration map, string part) {
			if (string.Equals(part, map.worldSize, StringComparison.OrdinalIgnoreCase)) {
				return 4;
			}
			return string.Equals(part, map.landform, StringComparison.OrdinalIgnoreCase) ? 2 : 1;
		}

		// The profile a map plays by: its key and knobs, or (null, null).
		public (string key, Dictionary<string, double> knobs) Resolve(MapGeneration map) {
			string bestKey = null;
			int bestParts = 0, bestRank = 0;
			foreach (string key in profiles.Keys.OrderBy(k => k, StringComparer.Ordinal)) {
				if (Fits(key, map, out int parts, out int rank) && (parts > bestParts || (parts == bestParts && rank > bestRank))) {
					(bestKey, bestParts, bestRank) = (key, parts, rank);
				}
			}
			return bestKey == null ? (null, null) : (bestKey, profiles[bestKey]);
		}
	}
}

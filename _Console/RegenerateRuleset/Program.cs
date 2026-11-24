using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using QueryCiv3;
using C7GameData;
using C7GameData.Save;

namespace RegenerateRuleset {
	// Regenerates the bundled ruleset, C7/Lua/civ3/ruleset.json, from Civ3's
	// conquests.biq. Only the sections that come from the BIQ, or from C7's
	// own defaults, are replaced; the hand-maintained ones are kept as they
	// are (see readme.md for which and why). The file keeps its order of
	// sections, its formatting and its line endings, so that a regeneration
	// shows only real changes.
	class Program {
		// The sections taken from the imported conquests.biq.
		static readonly string[] FromBiq = {
			"terrainTypes",
			"resources",
			"unitPrototypes",
			"buildings",
			"barbarianInfo",
			"experienceLevels",
			"defaultExperienceLevel",
			"inflows",
			"cultureGroups",
			"strengthBonuses",
			"healRates",
			"rules",
			"techs",
			"citizenTypes",
			"governments",
		};

		// The sections kept as they are in ruleset.json, because the import
		// would undo deliberate edits, add only noise, or doesn't make them.
		static readonly string[] Kept = {
			// The ruleset's own version.
			"version",
			// The import has the same improvements in another order.
			"terrainImprovements",
			// The railroad has a Lua AI score the BIQ doesn't have.
			"terraForms",
			// The import adds the default settler AI adjustments to each
			// civilization.
			"civilizations",
			// The highest difficulty is renamed from Sid to Creator.
			"difficulties",
			// The import pads the last era to 50000 turns; the file has
			// 10000.
			"timeOptions",
			// The world sizes aren't imported from the BIQ.
			"worldSizes",
		};

		// The sections C7 sets itself, whatever the BIQ says.
		static Dictionary<string, JsonNode> FromC7Defaults() {
			// Serialized as a save serializes them.
			SaveGame defaults = new() {
				// The victory conditions a new game starts with.
				VictoryConditions = VictoryConditions.NewGameDefaults(),
			};
			JsonObject json = JsonNode.Parse(defaults.ToCompactJSON()).AsObject();
			return new() {
				["victoryConditions"] = json["victoryConditions"].DeepClone(),
			};
		}

		// The repository's root: the folder with C7/C7.csproj, looked for
		// above the working directory and the program's own folder.
		static string RepositoryRoot {
			get {
				foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
					for (DirectoryInfo dir = new DirectoryInfo(start); dir != null; dir = dir.Parent) {
						if (File.Exists(Path.Combine(dir.FullName, "C7", "C7.csproj"))) {
							return dir.FullName;
						}
					}
				}
				throw new DirectoryNotFoundException("Unable to find the C7 repository (a folder with C7/C7.csproj) above the working directory or the program; pass the ruleset path as the second argument.");
			}
		}

		static int Main(string[] args) {
			// --mdj: MDJ's rules (see C7Engine/GameSetup.Mdj.cs), from
			// Conquests/Scenarios/MPTournamentMDJ.biq, into
			// C7/Lua/mdj/ruleset.json.
			// --mpt, --qc, --modern and --future <BIQ>: the Civ Players
			// League's ladder mods (see C7Engine/GameSetup.BundledRules.cs),
			// from the BIQ given, wherever it is, into C7/Lua/mptournament,
			// qc, modern and future.
			Scenario scenario = Scenarios.FirstOrDefault(s => args.Contains(s.Option));
			if (args.Count(a => Scenarios.Any(s => s.Option == a)) > 1) {
				Console.Error.WriteLine("Pass one of " + string.Join(", ", Scenarios.Select(s => s.Option)) + " at a time.");
				return 1;
			}
			string modBiq = null;
			if (scenario != null && scenario.BiqGiven) {
				int at = Array.IndexOf(args, scenario.Option);
				if (at + 1 >= args.Length || args[at + 1].StartsWith("--")) {
					Console.Error.WriteLine($"{scenario.Option} needs the path of the mod's BIQ ({scenario.BiqName}).");
					return 1;
				}
				modBiq = Path.GetFullPath(args[at + 1]);
				args = args.Where((_, i) => i != at + 1).ToArray();
			}
			args = args.Where(a => !Scenarios.Any(s => s.Option == a)).ToArray();
			if (args.Length > 2 || args.Any(a => a == "-h" || a == "--help")) {
				Console.WriteLine("usage: RegenerateRuleset [--mdj | (--mpt | --qc | --modern | --future) <BIQ path>] [Civ3 install path] [ruleset.json path]");
				Console.WriteLine("the Civ3 install path defaults to the one Civ3Location finds, and the ruleset to C7/Lua/civ3/ruleset.json in the repository");
				foreach (Scenario s in Scenarios) {
					Console.WriteLine($"(with {s.Option}, {s.BiqName}'s rules into C7/Lua/{s.RulesetDir}/ruleset.json, laid out as C7/Lua/civ3/ruleset.json)");
				}
				return args.Length > 2 ? 1 : 0;
			}
			if (scenario != null) {
				return RegenerateScenario(scenario, modBiq, args.Length > 0 ? args[0] : Civ3Location.GetCiv3Path(),
					Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(RepositoryRoot, "C7", "Lua", scenario.RulesetDir, "ruleset.json")));
			}
			string civ3Path = args.Length > 0 ? args[0] : Civ3Location.GetCiv3Path();
			string conquests = Path.Combine(civ3Path, "Conquests");
			string biqPath = Path.Combine(conquests, "conquests.biq");
			string rulesetPath = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(RepositoryRoot, "C7", "Lua", "civ3", "ruleset.json"));
			if (!File.Exists(biqPath)) {
				Console.Error.WriteLine($"No conquests.biq at {biqPath}; pass the Civ3 install path as the first argument.");
				return 1;
			}

			SaveGame imported = ImportCiv3.ImportBiq(biqPath, biqPath, _ => Path.Combine(conquests, "Text", "PediaIcons.txt"));
			JsonObject biq = JsonNode.Parse(imported.ToCompactJSON()).AsObject();

			byte[] bytes = File.ReadAllBytes(rulesetPath);
			bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
			string text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
			JsonObject ruleset = JsonNode.Parse(text).AsObject();

			Dictionary<string, JsonNode> defaults = FromC7Defaults();
			JsonObject merged = new();
			foreach ((string key, JsonNode value) in ruleset.ToList()) {
				JsonNode section;
				if (defaults.TryGetValue(key, out JsonNode fromDefaults)) {
					section = fromDefaults;
				} else if (FromBiq.Contains(key)) {
					section = biq[key] ?? throw new InvalidDataException($"The import of conquests.biq has no {key}");
					biq.Remove(key);
				} else if (Kept.Contains(key)) {
					section = value;
					ruleset.Remove(key);
				} else {
					Console.Error.WriteLine($"ruleset.json has a section {key} that is neither taken from the BIQ, nor from C7's defaults, nor kept; add it to one of the lists in Program.cs.");
					return 1;
				}
				merged[key] = section;
			}

			string json = merged.ToJsonString(new JsonSerializerOptions {
				WriteIndented = true,
				// ruleset.json has its accented letters and apostrophes as
				// they are, not escaped.
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			});

			// Keep the file's line endings and final newline.
			string newLine = text.Contains("\r\n") ? "\r\n" : "\n";
			json = json.Replace("\r\n", "\n").Replace("\n", newLine);
			if (text.EndsWith("\n")) {
				json += newLine;
			}

			File.WriteAllText(rulesetPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom));
			Console.WriteLine($"wrote {rulesetPath} from {biqPath}");
			PrintSections(defaults.Keys);
			return 0;
		}

		static void PrintSections(IEnumerable<string> defaults, IEnumerable<string> fromBiq = null, IEnumerable<string> kept = null) {
			Console.WriteLine($"\tfrom the BIQ: {string.Join(", ", fromBiq ?? FromBiq)}");
			Console.WriteLine($"\tfrom C7's defaults: {string.Join(", ", defaults)}");
			Console.WriteLine($"\tkept: {string.Join(", ", kept ?? Kept)}");
		}

		// A scenario whose rules are bundled: its option, its BIQ, the folder
		// under C7/Lua its ruleset.json goes in, how it is imported (as the
		// game does: from the BIQ path given, or for MDJ from the Civ3
		// install, and the Civ3 install path), the sections of Civ3's
		// bundled ruleset it takes from its import as well as those all take
		// (a section of C7's defaults among them takes the BIQ's instead),
		// and whether the BIQ's path must be given.
		record Scenario(string Option, string BiqName, string RulesetDir, Func<string, string, SaveGame> Import, string[] AlsoFromBiq, bool BiqGiven);

		// What a League mod takes from its BIQ besides the usual sections:
		// its civilizations (Future's own, Modern's with their own advances),
		// world sizes (by player count), time scale and turn limit, and
		// victory conditions (each mod sets its own).
		static readonly string[] ModAlsoFromBiq = { "civilizations", "worldSizes", "timeOptions", "victoryConditions" };

		static Scenario Mod(string option, C7Engine.GameSetup.BundledRules rules) =>
			new(option, rules.BiqName, rules.RulesetDir, (biq, civ3) => C7Engine.GameSetup.ImportBundledBiq(biq, civ3, rules), ModAlsoFromBiq, true);

		static readonly Scenario[] Scenarios = {
			// MDJ (C7Engine/GameSetup.Mdj.cs): its own civilizations
			// (Medieval Japan's clans) and world sizes (by player count, 1v1
			// to 8 Player).
			new("--mdj", C7Engine.GameSetup.MdjBiqName, C7Engine.GameSetup.MdjRulesetDir, (_, civ3) => C7Engine.GameSetup.ImportMdjBiq(civ3),
				new[] { "civilizations", "worldSizes" }, false),
			Mod("--mpt", C7Engine.GameSetup.MpTournamentRules),
			Mod("--qc", C7Engine.GameSetup.QcRules),
			Mod("--modern", C7Engine.GameSetup.ModernRules),
			Mod("--future", C7Engine.GameSetup.FutureRules),
		};

		// A scenario's ruleset: Civ3's bundled ruleset's sections, in its
		// order, those from the BIQ (and the scenario's own) from the
		// scenario's BIQ as the game imports it, and the rest as Civ3's. Also
		// the scenario's search folders, where Civ3's own art for it is
		// (used when playing with Civ3's art, not in standalone mode), and a
		// mod's start for every civ (Rules.ScenarioStart, in its rules).
		static int RegenerateScenario(Scenario scenario, string biqPath, string civ3Path, string outPath) {
			SaveGame imported = scenario.Import(biqPath, civ3Path);
			if (imported == null) {
				Console.Error.WriteLine(scenario.BiqGiven ? $"No BIQ at {biqPath}."
					: $"No {scenario.BiqName} in {civ3Path}/Conquests/Scenarios; pass the Civ3 install path as the first argument.");
				return 1;
			}
			JsonObject biq = JsonNode.Parse(imported.ToCompactJSON()).AsObject();
			string templatePath = Path.Combine(RepositoryRoot, "C7", "Lua", "civ3", "ruleset.json");
			JsonObject template = JsonNode.Parse(File.ReadAllText(templatePath)).AsObject();
			Dictionary<string, JsonNode> defaults = FromC7Defaults();
			string[] fromBiq = FromBiq.Concat(scenario.AlsoFromBiq).ToArray();
			string[] kept = Kept.Except(scenario.AlsoFromBiq).ToArray();
			JsonObject merged = new();
			foreach ((string key, JsonNode value) in template.ToList()) {
				if (defaults.TryGetValue(key, out JsonNode fromDefaults) && !scenario.AlsoFromBiq.Contains(key)) {
					merged[key] = fromDefaults.DeepClone();
				} else if (fromBiq.Contains(key)) {
					merged[key] = (biq[key] ?? throw new InvalidDataException($"The import of {scenario.BiqName} has no {key}")).DeepClone();
				} else if (kept.Contains(key)) {
					merged[key] = value.DeepClone();
				} else {
					Console.Error.WriteLine($"ruleset.json has a section {key} that is neither taken from the BIQ, nor from C7's defaults, nor kept; add it to one of the lists in Program.cs.");
					return 1;
				}
			}
			// The terrain improvements are kept as Civ3's, but with the
			// scenario's bonuses (MDJ's TERR: desert irrigation +2, mountain
			// mines +3; QC's: irrigation +2).
			foreach (JsonNode improvement in merged["terrainImprovements"].AsArray()) {
				string improvementKey = improvement["key"].GetValue<string>();
				JsonNode ours = biq["terrainImprovements"].AsArray().FirstOrDefault(i => i["key"].GetValue<string>() == improvementKey)
					?? throw new InvalidDataException($"The import of {scenario.BiqName} has no terrain improvement {improvementKey}");
				if (ours["bonusYields"] is JsonObject yields && yields.Count > 0) {
					improvement["bonusYields"] = yields.DeepClone();
				} else {
					improvement.AsObject().Remove("bonusYields");
				}
			}
			// The terraforms too are kept as Civ3's, but with the scenario's
			// own advances, resources and turns: Civ3's would name advances of
			// Civ3's tree by number, which are others in MDJ's and the League
			// mods', and Coal for the railroad, which MDJ hasn't got; QC's
			// jobs take a quarter of the turns.
			foreach (JsonNode terraform in merged["terraForms"].AsArray()) {
				string id = terraform["id"].GetValue<string>();
				JsonNode ours = biq["terraForms"].AsArray().FirstOrDefault(t => t["id"].GetValue<string>() == id)
					?? throw new InvalidDataException($"The import of {scenario.BiqName} has no terraform {id}");
				foreach (string field in new[] { "requiredTech", "requiredResources", "turnsToComplete" }) {
					if (ours[field] is JsonNode value && !(value is JsonArray { Count: 0 })) {
						terraform[field] = value.DeepClone();
					} else {
						terraform.AsObject().Remove(field);
					}
				}
			}
			// A time scale from the BIQ is padded, as Civ3's in ruleset.json
			// is, to 10000 turns in its last era, not the import's 50000.
			if (scenario.AlsoFromBiq.Contains("timeOptions")
					&& merged["timeOptions"]?["timeScale"]?[0] is JsonArray turns && turns.Count > 0
					&& template["timeOptions"]?["timeScale"]?[0] is JsonArray civ3Turns && civ3Turns.Count == turns.Count) {
				turns[turns.Count - 1] = civ3Turns[civ3Turns.Count - 1].DeepClone();
			}
			foreach (string key in new[] { "scenarioSearchPath", "scenarioName" }) {
				if (biq[key] is JsonNode node) {
					merged[key] = node.DeepClone();
				}
			}
			string json = merged.ToJsonString(new JsonSerializerOptions {
				WriteIndented = true,
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			}).Replace("\r\n", "\n") + "\n";
			Directory.CreateDirectory(Path.GetDirectoryName(outPath));
			File.WriteAllText(outPath, json, new UTF8Encoding(false));
			Console.WriteLine($"wrote {outPath} from {scenario.BiqName}");
			PrintSections(defaults.Keys.Except(scenario.AlsoFromBiq), fromBiq, kept);
			return 0;
		}
	}
}

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
			if (args.Length > 2 || args.Any(a => a == "-h" || a == "--help")) {
				Console.WriteLine("usage: RegenerateRuleset [Civ3 install path] [ruleset.json path]");
				Console.WriteLine("the Civ3 install path defaults to the one Civ3Location finds, and the ruleset to C7/Lua/civ3/ruleset.json in the repository");
				return args.Length > 2 ? 1 : 0;
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
			Console.WriteLine($"\tfrom the BIQ: {string.Join(", ", FromBiq)}");
			Console.WriteLine($"\tfrom C7's defaults: {string.Join(", ", defaults.Keys)}");
			Console.WriteLine($"\tkept: {string.Join(", ", Kept)}");
			return 0;
		}
	}
}

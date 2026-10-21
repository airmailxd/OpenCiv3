using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace C7GameData {

	// An advisor's face, as script.txt's "#advisor <Advisor> <Mood>" lines
	// name it. The order is Civ3's: the advisor code sets mood 0 for good
	// news and 2 for bad (Civ3 FUN_0052b200, FUN_0052b450, FUN_00502530),
	// which the "#advisor ... Happy" and "... Sad" lines agree with.
	public enum AdvisorMood {
		Happy,
		Angry,
		Sad,
		Surprised,
	}

	// One entry of Civ3's script.txt: the text under a #KEY line, and the
	// face the "#advisor" line under it asks for, if any.
	public class ScriptEntry {
		public string Key;

		// The entry's text: its first run of lines that aren't directives
		// (like "#advisor Happy" or "#comment ..."), joined by spaces. The
		// $ placeholders and [highlights] are kept; see ScriptText.Format.
		public string Text = "";

		// The mood of the entry's "#advisor" line, or null if it has none.
		public AdvisorMood? Mood;
	}

	// The entries of Civ3's Conquests\Text\script.txt, by key. The game reads
	// an entry by finding its "#KEY" line, skipping the directive lines under
	// it ("#advisor", "#comment", ...) and taking the line after them (Civ3
	// FUN_00629d20, FUN_00629d50 and FUN_004a67b0, as used by the advisor
	// speech code at FUN_0042ad00 and its neighbours).
	public class ScriptText {
		private static readonly ILogger log = Log.ForContext<ScriptText>();

		private readonly Dictionary<string, ScriptEntry> entries = new(StringComparer.OrdinalIgnoreCase);

		public int Count => entries.Count;

		public ScriptEntry Get(string key) => key == null ? null : entries.GetValueOrDefault(key.Trim());

		public bool Has(string key) => Get(key) != null;

		// Reads a script.txt, which is Windows-1252 text. A missing or
		// unreadable file gives an empty ScriptText rather than an error, so
		// the game still runs without Civ3's text (e.g. in standalone mode).
		public static ScriptText Load(string path) {
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
				log.Warning("No script text at {path}", path);
				return new ScriptText();
			}
			try {
				return Parse(File.ReadAllLines(path, QueryCiv3.Util.Civ3Encoding));
			} catch (Exception e) {
				log.Error(e, "Could not read the script text at {path}", path);
				return new ScriptText();
			}
		}

		public static ScriptText Parse(string text) {
			return Parse(text.Replace("\r\n", "\n").Split('\n'));
		}

		// Keys are written in capitals (#DOMESTICADVICERANK, #FA_CULTURAL,
		// #CIVADJ0 lines don't start with #); directives in lower case
		// (#advisor, #comment, #map_center, #itemlist, #xs ...).
		private static bool IsDirective(string afterHash) => afterHash.Length == 0 || char.IsLower(afterHash[0]);

		public static ScriptText Parse(IEnumerable<string> fileLines) {
			ScriptText result = new();
			ScriptEntry entry = null;
			List<string> textLines = null;
			bool textDone = false;

			void Finish() {
				if (entry != null) {
					entry.Text = string.Join(" ", textLines);
				}
			}

			foreach (string rawLine in fileLines) {
				string line = rawLine.TrimEnd('\r', '\n');
				if (line.StartsWith(';')) {
					continue;
				}
				if (line.StartsWith('#')) {
					string afterHash = line.Substring(1);
					if (!IsDirective(afterHash)) {
						Finish();
						string key = afterHash.Trim();
						entry = new ScriptEntry { Key = key };
						textLines = new();
						textDone = false;
						// Where a key is in the file twice, the first is the
						// one the game finds.
						result.entries.TryAdd(key, entry);
						continue;
					}
					if (entry != null && textLines.Count == 0 && afterHash.StartsWith("advisor", StringComparison.Ordinal)) {
						entry.Mood ??= ParseMood(afterHash);
					}
					if (textLines?.Count > 0) {
						textDone = true;
					}
					continue;
				}
				if (entry == null || textDone) {
					continue;
				}
				if (line.Trim().Length == 0) {
					textDone = textLines.Count > 0;
					continue;
				}
				textLines.Add(line.Trim());
			}
			Finish();
			return result;
		}

		// The mood named last on an "advisor <Advisor> <Mood>" line, as in
		// "advisor Foreign Sad" or "advisor Happy".
		private static AdvisorMood? ParseMood(string directive) {
			string[] words = directive.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			for (int i = words.Length - 1; i > 0; i--) {
				if (Enum.TryParse(words[i], ignoreCase: true, out AdvisorMood mood)) {
					return mood;
				}
			}
			return null;
		}

		private static readonly Regex Placeholder = new(@"\$([A-Za-z_]+)(\d)", RegexOptions.Compiled);
		private static readonly Regex Link = new(@"\$LINK<([^=>]*)=[^>]*>", RegexOptions.Compiled);

		// Fills in an entry's placeholders the way Civ3 does: the digit after
		// a $NAME is the slot it's filled from and the name only says what
		// goes there, with $NUMn filled from the number slots and every other
		// name from the text slots (Civ3 FUN_0063e390 sets text slot n,
		// FUN_0063e350 number slot n, and FUN_0063d770 formats the line).
		// A $LINK<text=key> shows its text, and the [ ] and { } that Civ3
		// draws its highlights with are dropped.
		public static string Format(string text, IReadOnlyList<string> texts, IReadOnlyList<int> numbers = null) {
			if (string.IsNullOrEmpty(text)) {
				return "";
			}
			string result = Link.Replace(text, m => m.Groups[1].Value);
			result = Placeholder.Replace(result, m => {
				int slot = m.Groups[2].Value[0] - '0';
				if (m.Groups[1].Value.StartsWith("NUM", StringComparison.OrdinalIgnoreCase)) {
					return numbers != null && slot < numbers.Count ? numbers[slot].ToString() : "";
				}
				return texts != null && slot < texts.Count ? texts[slot] ?? "" : "";
			});
			StringBuilder sb = new(result.Length);
			foreach (char c in result) {
				if (c is not ('[' or ']' or '{' or '}')) {
					sb.Append(c);
				}
			}
			return sb.ToString().Trim();
		}
	}
}

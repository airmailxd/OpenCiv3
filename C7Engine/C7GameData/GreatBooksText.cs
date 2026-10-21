using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace C7GameData {
	// Civ3's Text/Great Books.txt, which words the Great Books (see
	// GreatBooks): the GREAT_HISTORY_BOOK popup (its #ADVISOR, #CAPTION and
	// #Text) and the lists HISTORIAN, BOOK_TITLE and HISTORY_RANK. Civ3 reads
	// the lists' first 11, 5 and 8 lines (Civ3 FUN_00548860). The text comes
	// from the player's Civ3; without it there are no books to show.
	public class GreatBooksText {
		public const string BookSection = "GREAT_HISTORY_BOOK";

		private readonly Dictionary<string, List<string>> sections = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, Dictionary<string, string>> directives = new(StringComparer.OrdinalIgnoreCase);

		public List<string> Historians => List("HISTORIAN", GreatBooks.HistorianCount);
		public List<string> BookTitles => List("BOOK_TITLE", GreatBooks.TitleCount);
		public List<string> HistoryRanks => List("HISTORY_RANK", GreatBooks.RankCount);

		// The popup's caption ("History of the World") and advisor line
		// ("Cultural Happy"), or null.
		public string Caption => Directive(BookSection, "CAPTION");
		public string Advisor => Directive(BookSection, "ADVISOR");

		public bool HasBook => sections.ContainsKey(BookSection) && Historians.Count > 0 && BookTitles.Count > 0;

		private List<string> List(string section, int max) {
			if (!sections.TryGetValue(section, out List<string> lines)) {
				return new List<string>();
			}
			return lines.Select(l => l.Trim()).Where(l => l.Length > 0).Take(max).ToList();
		}

		private string Directive(string section, string name) {
			return directives.TryGetValue(section, out Dictionary<string, string> d) && d.TryGetValue(name, out string value) ? value : null;
		}

		// Reads the file, which is Windows-1252 text; null if it can't be read.
		public static GreatBooksText Load(string path) {
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
				return null;
			}
			try {
				return Parse(File.ReadAllLines(path, QueryCiv3.Util.Civ3Encoding));
			} catch (Exception) {
				return null;
			}
		}

		public static GreatBooksText Parse(string text) => Parse(text.Replace("\r\n", "\n").Split('\n'));

		// A "#NAME" line starts a section. In GREAT_HISTORY_BOOK, "#ADVISOR",
		// "#CAPTION" and the like give the popup's settings and "#Text" starts
		// its text; the lists are a line per entry.
		public static GreatBooksText Parse(IEnumerable<string> fileLines) {
			GreatBooksText result = new();
			List<string> lines = null;
			string section = null;
			foreach (string raw in fileLines) {
				string line = raw.TrimEnd('\r', '\n');
				if (line.StartsWith(';')) {
					continue;
				}
				if (line.StartsWith('#')) {
					string rest = line.Substring(1).Trim();
					int space = rest.IndexOfAny([' ', '\t']);
					string name = space < 0 ? rest : rest.Substring(0, space);
					string value = space < 0 ? "" : rest.Substring(space + 1).Trim();
					if (section != null && value.Length > 0 && result.sections.ContainsKey(section) && !name.Equals("Text", StringComparison.OrdinalIgnoreCase)) {
						if (!result.directives.TryGetValue(section, out Dictionary<string, string> d)) {
							d = new(StringComparer.OrdinalIgnoreCase);
							result.directives[section] = d;
						}
						d[name] = value;
						continue;
					}
					if (name.Equals("Text", StringComparison.OrdinalIgnoreCase) && section != null) {
						lines = result.sections[section];
						lines.Clear();
						continue;
					}
					section = name;
					lines = new List<string>();
					result.sections[section] = lines;
					continue;
				}
				lines?.Add(line);
			}
			return result;
		}

		// The book's text, its $HISTORIAN0, $BOOKTITLE1 and $HISTORY_RANK2
		// to $HISTORY_RANK9 filled in: each ranked civ is called by the rank
		// of its place and its name ("Glorious Romans"; Civ3 FUN_005490f0
		// joins the two with a space). Lines for places no civ takes are left
		// out, as Civ3 FUN_00548d80 removes them. A line starting with "^"
		// starts a new line; Civ3's {} and [] marks are dropped. Null if the
		// file has no book.
		public string Format(GreatBooks.Book book, Func<Player, string> civName) {
			if (!HasBook || !sections.TryGetValue(BookSection, out List<string> template)) {
				return null;
			}
			List<string> historians = Historians, titles = BookTitles, ranks = HistoryRanks;
			Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) {
				["HISTORIAN0"] = historians[Math.Clamp(book.historian, 0, historians.Count - 1)],
				["BOOKTITLE1"] = titles[Math.Clamp((int)book.title, 0, titles.Count - 1)],
			};
			for (int i = 0; i < book.ranking.Count && i < GreatBooks.RankCount; ++i) {
				string rank = i < ranks.Count ? ranks[i] : "";
				values[$"HISTORY_RANK{i + 2}"] = $"{rank} {civName(book.ranking[i])}".Trim();
			}

			List<string> output = new();
			foreach (string line in template) {
				Match unused = Regex.Match(line, @"\$HISTORY_RANK(\d+)");
				if (unused.Success && !values.ContainsKey("HISTORY_RANK" + unused.Groups[1].Value)) {
					continue;
				}
				string filled = Regex.Replace(line, @"\$([A-Za-z_]+\d+)", m => values.TryGetValue(m.Groups[1].Value, out string v) ? v : m.Value);
				filled = filled.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "");
				if (filled.StartsWith('^')) {
					int carets = filled.TakeWhile(c => c == '^').Count();
					for (int i = 1; i < carets; ++i) {
						output.Add("");
					}
					output.Add(filled.Substring(carets).TrimEnd());
				} else if (filled.Trim().Length == 0) {
					continue;
				} else if (output.Count == 0) {
					output.Add(filled.TrimEnd());
				} else {
					output[^1] = (output[^1] + " " + filled.Trim()).Trim();
				}
			}
			while (output.Count > 0 && output[^1].Length == 0) {
				output.RemoveAt(output.Count - 1);
			}
			return string.Join("\n", output);
		}
	}
}

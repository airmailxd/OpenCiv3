using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace C7GameData {

	// One label of Civ3's diplomacy.txt (#AIWEAKREJECT and so on): its lines,
	// how they are laid out, and what its placeholders stand for.
	public class DiplomacyEntry {
		public string Key;

		// The header's "#civ 1", "#power 1" and "#mood 1": whether the lines
		// come in a group for each civ, and within it three sets by the AI's
		// power or mood. "#random n" is how many lines each set has, of which
		// one is picked at random. So for #civ 1 #mood 1 #random 2 the lines
		// are 2 for each mood, 6 for each civ, in the BIQ's order of civs:
		// the reject labels have 192 = 32 x 3 x 2 lines, #AIFIRSTCONTACT
		// (#civ 1 #random 1) one for each of the 32 civs, and
		// #AICOUNTERPEACE (#mood 1 #random 12) 36 = 3 x 12. UNVERIFIED (read
		// from the file's layout and tone, as compendium 06 notes): the sets
		// go from friendly to hostile, as the lines read.
		public bool ByCiv;
		public bool ByPower;
		public bool ByMood;
		public int Random = 1;

		// The lines, without their quotes.
		public List<string> Lines = new();

		// What each placeholder stands for, by its name without the "$"
		// ("CIVNAME4" -> "AI's Civ"), from the "; $CIVNAME4 = AI's Civ"
		// comments above the label. The numbers differ from label to label.
		public Dictionary<string, string> Placeholders = new(StringComparer.OrdinalIgnoreCase);

		public int Sets => ByPower || ByMood ? 3 : 1;

		// The line for a civ (its place in the BIQ's list of civs), mood or
		// power (0 to 2, friendliest or strongest first) and pick (any
		// number, taken modulo Random), or null if the entry hasn't one.
		public string Line(int civIndex, int set, int pick) {
			if (Lines.Count == 0) {
				return null;
			}
			int random = Math.Max(1, Random);
			int perCiv = Sets * random;
			int start = (ByCiv ? Math.Max(0, civIndex) * perCiv : 0) + Math.Clamp(set, 0, Sets - 1) * random;
			int index = start + Math.Abs(pick % random);
			if (index >= Lines.Count) {
				// A civ beyond the file's list (as in a mod with more civs)
				// takes the first civ's lines.
				index = Math.Clamp(set, 0, Sets - 1) * random + Math.Abs(pick % random);
			}
			return index < Lines.Count ? Lines[index] : Lines[0];
		}
	}

	// The people a diplomacy line may name: the human player ("player's")
	// and the computer player ("AI's").
	public record DiplomacySpeakers(string PlayerName, string PlayerCiv, string PlayerAdjective, string PlayerNoun,
		string AiName, string AiCiv, string AiAdjective, string AiNoun);

	// The entries of Civ3's Conquests\Text\diplomacy.txt, the leaders' words
	// on the diplomacy screens, by label.
	public class DiplomacyText {
		private static readonly ILogger log = Log.ForContext<DiplomacyText>();

		private readonly Dictionary<string, DiplomacyEntry> entries = new(StringComparer.OrdinalIgnoreCase);

		public int Count => entries.Count;

		public DiplomacyEntry Get(string key) => key == null ? null : entries.GetValueOrDefault(key.Trim().TrimStart('#'));

		// Reads a diplomacy.txt, which is Windows-1252 text. A missing or
		// unreadable file gives an empty DiplomacyText, so the game still
		// runs without Civ3's text (e.g. in standalone mode).
		public static DiplomacyText Load(string path) {
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
				log.Warning("No diplomacy text at {path}", path);
				return new DiplomacyText();
			}
			try {
				return Parse(File.ReadAllLines(path, QueryCiv3.Util.Civ3Encoding));
			} catch (Exception e) {
				log.Error(e, "Could not read the diplomacy text at {path}", path);
				return new DiplomacyText();
			}
		}

		public static DiplomacyText Parse(string text) {
			return Parse(text.Replace("\r\n", "\n").Split('\n'));
		}

		private static readonly Regex PlaceholderComment = new(@"^;\s*\$([A-Za-z_]+[0-9]+)\s*=\s*(.+?)\s*$");
		private static readonly Regex Header = new(@"^#(civ|power|mood|random)\s+(\d+)", RegexOptions.IgnoreCase);

		public static DiplomacyText Parse(IEnumerable<string> fileLines) {
			DiplomacyText result = new();
			DiplomacyEntry entry = null;
			Dictionary<string, string> comments = new(StringComparer.OrdinalIgnoreCase);
			foreach (string rawLine in fileLines) {
				string line = rawLine.Trim();
				if (line.Length == 0) {
					continue;
				}
				if (line.StartsWith(';')) {
					Match m = PlaceholderComment.Match(line);
					if (m.Success) {
						// Comments after a label's lines are for the next one.
						if (entry != null && entry.Lines.Count > 0) {
							entry = null;
						}
						comments[m.Groups[1].Value] = m.Groups[2].Value;
					}
					continue;
				}
				if (line.StartsWith('#')) {
					Match h = Header.Match(line);
					if (h.Success && entry != null) {
						int value = int.Parse(h.Groups[2].Value);
						switch (h.Groups[1].Value.ToLowerInvariant()) {
							case "civ": entry.ByCiv = value != 0; break;
							case "power": entry.ByPower = value != 0; break;
							case "mood": entry.ByMood = value != 0; break;
							case "random": entry.Random = Math.Max(1, value); break;
						}
						continue;
					}
					string key = line.Substring(1).Trim();
					if (key.Length == 0 || !char.IsUpper(key[0])) {
						entry = null;
						continue;
					}
					entry = new DiplomacyEntry { Key = key, Placeholders = new(comments, StringComparer.OrdinalIgnoreCase) };
					result.entries[key] = entry;
					comments.Clear();
					continue;
				}
				if (entry != null && IsQuoted(line)) {
					entry.Lines.Add(Unquote(line));
				}
			}
			return result;
		}

		// The lines are in straight or curly quotes (Windows-1252 0x93 and
		// 0x94, read as U+201C and U+201D).
		private static bool IsQuoted(string line) => line[0] is '"' or '“' or '\u0093';

		private static string Unquote(string line) {
			return line.Trim().Trim('"', '“', '”', '\u0093', '\u0094').Trim();
		}

		private static readonly Regex Token = new(@"\$([A-Za-z_]+?)([0-9]+)");

		// The line with its placeholders filled in, by what the label's
		// comments say they stand for, or else by the placeholder's name
		// ($PLAYER is the player's name, $AI the computer player's, and
		// $CIVNAME, $CIVADJ and $CIVNOUN the computer player's civ, as the
		// file's first comments have them).
		public static string Fill(DiplomacyEntry entry, string line, DiplomacySpeakers who) {
			if (line == null) {
				return null;
			}
			return Token.Replace(line, m => {
				string name = m.Groups[1].Value.ToUpperInvariant();
				string meaning = entry?.Placeholders.GetValueOrDefault(m.Groups[1].Value + m.Groups[2].Value);
				return Value(name, meaning, who) ?? m.Value;
			});
		}

		private static string Value(string name, string meaning, DiplomacySpeakers who) {
			if (meaning != null) {
				string text = meaning.ToLowerInvariant();
				bool player = text.Contains("player");
				bool noun = text.Contains("(noun)");
				bool adjective = text.Contains("(adj)");
				if (text.Contains("name")) {
					return player ? who.PlayerName : who.AiName;
				}
				if (noun) {
					return player ? who.PlayerNoun : who.AiNoun;
				}
				if (adjective) {
					return player ? who.PlayerAdjective : who.AiAdjective;
				}
				if (text.Contains("civ")) {
					return player ? who.PlayerCiv : who.AiCiv;
				}
			}
			return name switch {
				"PLAYER" => who.PlayerName,
				"AI" or "LEADER" => who.AiName,
				"CIVNAME" => who.AiCiv,
				"CIVADJ" => who.AiAdjective,
				"CIVNOUN" => who.AiNoun,
				_ => null,
			};
		}
	}
}

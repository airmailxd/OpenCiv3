using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace C7GameData {
	// One of Conquests' campaigns in the Campaign Record: the best win at it,
	// or none yet. From Civ3's record entry (Civ3 FUN_004af220, FUN_004b04d0).
	public record CampaignRecordEntry {
		// The campaign's scenario file without its extension, as
		// "1 Mesopotamia" or "7 Sengoku - Sword of the Shogun".
		public string Campaign { get; init; } = "";
		// The score the campaign was won with, before the difficulty
		// adjustment, or NotYetVictorious.
		public int Score { get; init; } = CampaignRecord.NotYetVictorious;
		// The difficulty's place in the scenario's list of them, Chieftain
		// first, which sets how the score counts (CampaignRecord.AdjustedScore).
		public int DifficultyLevel { get; init; }
		public string Difficulty { get; init; } = "";
		public string Civilization { get; init; } = "";
		// How it was won, in the Hall of Fame's words (HallOfFame.VictoryTypeName).
		// Not shown on the screen; Civ3 keeps it too.
		public string VictoryType { get; init; } = "";
		public DateTime? WonAt { get; init; }

		// The score shown: Score scaled for the difficulty.
		[JsonIgnore]
		public int AdjustedScore => CampaignRecord.AdjustedScore(Score, DifficultyLevel);

		// Whether the campaign has been won. Civ3 shows "Not Yet Victorious"
		// for an adjusted score below 1 (Civ3 FUN_004b1140).
		[JsonIgnore]
		public bool Victorious => AdjustedScore >= 1;
	}

	// Conquests' Campaign Record: each of its nine campaigns and the best win
	// at it. Civ3 keeps it in Conquests\CampaignRecord.hof (Civ3
	// FUN_004af6e0); OpenCiv3 keeps its own, in a file of its own, and only
	// reads Civ3's.
	public static class CampaignRecord {
		private static readonly ILogger log = Log.ForContext(typeof(CampaignRecord));

		public const string FileName = "campaign_record.json";
		public const string Civ3FileName = "CampaignRecord.hof";
		public const int NotYetVictorious = -1;

		// The labels.txt section with the screen's words, ending with the
		// campaigns' names (Civ3 FUN_004af6e0 reads "#CAMPAIGN_RECORD").
		public const string LabelsSection = "CAMPAIGN_RECORD";
		// Where the campaigns' names start in the section: after "CAMPAIGNS",
		// "Hall of Fame", "Exit", the four column headings, "Not Yet
		// Victorious", "Next" and "Play the Next Campaign". Civ3 matches nine
		// names (Civ3 FUN_004afd50 reads labels 10 to 18).
		public const int FirstCampaignLabel = 10;
		public const int CampaignCount = 9;

		private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

		// The score as the record counts it: the game's score scaled for the
		// difficulty, rounded down (Civ3 FUN_004af150, whose multipliers are
		// the floats at 0x685ea4-0x685eb4 and 0x683b10). Chieftain and any
		// level past Sid's count as 0.6.
		public static int AdjustedScore(int score, int difficultyLevel) {
			float multiplier = difficultyLevel switch {
				1 => 0.8f,
				2 => 1.0f,
				3 => 1.2f,
				4 => 1.4f,
				5 => 1.6f,
				6 => 1.8f,
				7 => 2.0f,
				_ => 0.6f,
			};
			// x87 multiplies the int by the single-precision constant and
			// truncates, so 1000 at Emperor (1.4f, a hair under 1.4) is 1399.
			return (int)(score * (double)multiplier);
		}

		// The campaigns' names from the text of labels.txt: the lines of its
		// #CAMPAIGN_RECORD section from FirstCampaignLabel on. None if the
		// section isn't there.
		public static List<string> CampaignNames(string labelsText) {
			List<string> lines = Section(labelsText, LabelsSection);
			return lines.Skip(FirstCampaignLabel).Take(CampaignCount).Where(l => l.Length > 0).ToList();
		}

		// The lines of a labels.txt section, after its "#NAME" line and up to
		// the next section.
		private static List<string> Section(string text, string name) {
			List<string> result = [];
			bool inSection = false;
			foreach (string raw in (text ?? "").Split('\n')) {
				string line = raw.TrimEnd('\r');
				if (line.StartsWith('#')) {
					if (inSection) {
						break;
					}
					inSection = line.Substring(1).Trim() == name;
					continue;
				}
				if (inSection) {
					result.Add(line);
				}
			}
			// Blank lines end the section in the file.
			while (result.Count > 0 && result[^1].Trim().Length == 0) {
				result.RemoveAt(result.Count - 1);
			}
			return result;
		}

		// Whether a scenario is one of the campaigns: its file name, without
		// the extension, starts with one of their names. Civ3 compares as
		// strncmp(name, file, strlen(name)), so "7 Sengoku" takes in
		// "7 Sengoku - Sword of the Shogun" (Civ3 FUN_004afd50).
		public static bool IsCampaign(string scenario, IEnumerable<string> campaignNames) =>
			scenario != null && campaignNames.Any(n => n.Length > 0 && scenario.StartsWith(n, StringComparison.Ordinal));

		// Adds an entry, not yet won, for each scenario that's a campaign and
		// isn't in the record yet. Civ3 does this for the .biq files beside
		// the game when there's no record (Civ3 FUN_004afd50); the scenarios
		// are taken in the order given. Returns whether any were added.
		public static bool AddCampaigns(List<CampaignRecordEntry> entries, IEnumerable<string> scenarios,
				IEnumerable<string> campaignNames) {
			List<string> names = campaignNames.ToList();
			bool added = false;
			foreach (string scenario in scenarios) {
				if (!IsCampaign(scenario, names) || entries.Any(e => e.Campaign == scenario)) {
					continue;
				}
				entries.Add(new CampaignRecordEntry { Campaign = scenario });
				added = true;
			}
			return added;
		}

		// Records a win at a campaign, if it's in the record and the win is
		// worth at least as much as the one there, once both are adjusted for
		// difficulty (Civ3 FUN_004b04d0). Returns whether it was recorded.
		public static bool RecordVictory(List<CampaignRecordEntry> entries, string campaign, int score,
				int difficultyLevel, string difficulty, string civilization, string victoryType, DateTime now) {
			int index = entries.FindIndex(e => e.Campaign == campaign);
			if (index < 0) {
				return false;
			}
			if (entries[index].AdjustedScore > AdjustedScore(score, difficultyLevel)) {
				return false;
			}
			entries[index] = entries[index] with {
				Score = score,
				DifficultyLevel = difficultyLevel,
				Difficulty = difficulty ?? "",
				Civilization = civilization ?? "",
				VictoryType = victoryType ?? "",
				WonAt = now,
			};
			return true;
		}

		// Records the player's win at the game, if it's one of the campaigns.
		// Civ3 does when the winner is the human player, unless they retired
		// (Civ3 FUN_004f9fb0 at 0x4fa8dc-0x4faa02).
		public static bool RecordVictory(List<CampaignRecordEntry> entries, GameData gameData, Player player,
				IVictory victory, int score, DateTime now) {
			if (string.IsNullOrEmpty(gameData?.scenarioName) || player == null) {
				return false;
			}
			string difficulty = gameData.gameDifficulty?.Name ?? "";
			int level = Math.Max(0, gameData.difficulties.FindIndex(d => d.Name == difficulty));
			return RecordVictory(entries, gameData.scenarioName, score, level, difficulty,
				player.civilization?.noun ?? player.civilization?.name ?? "", HallOfFame.VictoryTypeName(victory), now);
		}

		// Whether every campaign has been won. Civ3 checks after recording a
		// win, and plays its victory movie when they all are (Civ3
		// FUN_004b03f0, FUN_004eafc0).
		public static bool AllVictorious(IEnumerable<CampaignRecordEntry> entries) => entries.All(e => e.Victorious);

		// The campaign the screen's "Next" button offers: the first not yet
		// won that can be played (Civ3 FUN_004b1140 asks FUN_004b1880 whether
		// its scenario can be found).
		public static CampaignRecordEntry NextCampaign(IEnumerable<CampaignRecordEntry> entries, Func<string, bool> playable) =>
			entries.FirstOrDefault(e => !e.Victorious && playable(e.Campaign));

		// The entries in the file, or null if there's no file or it can't be
		// read.
		public static List<CampaignRecordEntry> Load(string path) {
			try {
				if (!File.Exists(path)) {
					return null;
				}
				return JsonSerializer.Deserialize<List<CampaignRecordEntry>>(File.ReadAllText(path), jsonOptions)?
					.Where(e => e != null).ToList() ?? [];
			} catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) {
				log.Warning(e, "Couldn't read the Campaign Record at {Path}", path);
				return null;
			}
		}

		// The record in the file, made if there's none: from Civ3's own record
		// if it has one (which is only read), else with every campaign not yet
		// won. Campaigns among the scenarios that the record lacks are added,
		// as Civ3 does when it makes its record. An empty list if there are no
		// campaigns, as without Civ3.
		public static List<CampaignRecordEntry> LoadOrCreate(string path, string civ3RecordPath,
				IEnumerable<string> scenarios, IEnumerable<string> campaignNames) {
			List<CampaignRecordEntry> entries = Load(path);
			bool changed = false;
			if (entries == null) {
				entries = (civ3RecordPath != null ? ReadCiv3(civ3RecordPath) : null) ?? [];
				changed = entries.Count > 0;
			}
			changed |= AddCampaigns(entries, scenarios, campaignNames);
			if (changed) {
				Save(path, entries);
			}
			return entries;
		}

		// Writes the record to the file. Returns whether it was saved.
		public static bool Save(string path, List<CampaignRecordEntry> entries) {
			try {
				string directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory)) {
					Directory.CreateDirectory(directory);
				}
				string temp = path + ".tmp";
				File.WriteAllText(temp, JsonSerializer.Serialize(entries, jsonOptions));
				File.Move(temp, path, overwrite: true);
				return true;
			} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
				log.Warning(e, "Couldn't save the Campaign Record at {Path}", path);
				return false;
			}
		}

		// Civ3's CampaignRecord.hof, as Civ3 writes it (Civ3 FUN_004afa40 and
		// the entry's own read and write). Every part is a chunk: a four-letter
		// tag, its length as a 32-bit little-endian number, then its bytes.
		//   "CMRC" 4: the record's version (0)
		//   "CMRC" 4: the number of entries
		//   then for each entry:
		//     "GMRC" 4: the entry's version (1)
		//     "GMRC" 288: the campaign's name (char[260]), then seven int32s:
		//         +0x124 a flag (1), +0x128 the score (-1 until won), +0x12c
		//         unused, +0x130 the civilization's index in the BIQ, +0x134
		//         the difficulty level, +0x138 the kind of victory, +0x13c the
		//         civilization's index again
		//     "GMRC" 520: the civilization's name and the difficulty's name
		//         (char[260] each)
		// Civ3 sizes the file generously, so zeros may follow the entries.
		public static List<CampaignRecordEntry> ReadCiv3(byte[] data) {
			int offset = 0;
			byte[] Chunk(string tag) {
				if (offset + 8 > data.Length || Encoding.ASCII.GetString(data, offset, 4) != tag) {
					throw new InvalidDataException($"Expected a {tag} chunk at {offset}");
				}
				int length = BitConverter.ToInt32(data, offset + 4);
				if (length < 0 || offset + 8 + length > data.Length) {
					throw new InvalidDataException($"The {tag} chunk at {offset} runs past the end");
				}
				byte[] result = data.AsSpan(offset + 8, length).ToArray();
				offset += 8 + length;
				return result;
			}

			Chunk("CMRC");
			int count = BitConverter.ToInt32(Chunk("CMRC"), 0);
			List<CampaignRecordEntry> entries = [];
			for (int i = 0; i < count; i++) {
				Chunk("GMRC");
				byte[] fields = Chunk("GMRC");
				byte[] names = Chunk("GMRC");
				if (fields.Length < 288 || names.Length < 520) {
					throw new InvalidDataException($"Entry {i} is too short");
				}
				int score = BitConverter.ToInt32(fields, 0x128 - 0x20);
				int level = BitConverter.ToInt32(fields, 0x134 - 0x20);
				entries.Add(new CampaignRecordEntry {
					Campaign = CString(fields, 0, 260),
					Score = score,
					DifficultyLevel = level,
					Civilization = CString(names, 0, 260),
					Difficulty = CString(names, 260, 260),
				});
			}
			return entries;
		}

		// Civ3's record at the path, or null if there's none or it can't be
		// read. The file is only read.
		public static List<CampaignRecordEntry> ReadCiv3(string path) {
			try {
				if (!File.Exists(path)) {
					return null;
				}
				return ReadCiv3(File.ReadAllBytes(path));
			} catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException
					or ArgumentException) {
				log.Warning(e, "Couldn't read Civ3's Campaign Record at {Path}", path);
				return null;
			}
		}

		private static string CString(byte[] bytes, int start, int length) {
			int end = Array.IndexOf(bytes, (byte)0, start, length);
			return Encoding.Latin1.GetString(bytes, start, (end < 0 ? start + length : end) - start);
		}
	}
}

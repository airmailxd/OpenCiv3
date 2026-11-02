using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using C7GameData;

namespace C7Engine.PlayLog {
	// A recorded game's own folder (see doc/play-log.md). With "Record play
	// log" on, the player names the game as it starts, or as a save that
	// wasn't recorded loads, and the game gets a folder of that name in the
	// saves folder, which holds its saves, its autosaves and its log,
	// <name>.jsonl. The game remembers its name and folder (GameData.playLog),
	// so loading any of its saves carries on the same log.
	public static class PlayLogFolders {
		public const int MaxNameLength = 60;

		// Characters no folder name may have on Windows, macOS or Linux.
		private static readonly char[] Illegal = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

		// Windows' reserved device names, which no file may be called.
		private static readonly string[] Reserved = [
			"CON", "PRN", "AUX", "NUL",
			"COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
			"LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
		];

		// The name offered for a new game: the human's civilization and the
		// date, such as "Rome 2026-11-02".
		public static string DefaultName(GameData gameData, DateTime now) {
			Player human = gameData.players.FirstOrDefault(p => p.isHuman && !p.IsIndependent);
			string civ = human?.civilization?.name;
			return Sanitize($"{(string.IsNullOrWhiteSpace(civ) ? "Game" : civ)} {now:yyyy-MM-dd}", "Game");
		}

		// The name made fit for a folder on any system: without the characters
		// a folder can't have, trimmed (Windows drops trailing dots and
		// spaces), at most MaxNameLength long, and not a name Windows reserves.
		// Empty, it's the fallback.
		public static string Sanitize(string name, string fallback = "Game") {
			StringBuilder text = new();
			foreach (char c in name ?? "") {
				if (char.IsControl(c) || Illegal.Contains(c)) {
					text.Append(' ');
				} else {
					text.Append(c);
				}
			}
			// Runs of spaces from what was taken out become one.
			string result = string.Join(" ", text.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
			if (result.Length > MaxNameLength) {
				result = result[..MaxNameLength];
			}
			result = result.Trim().TrimEnd('.', ' ');
			if (result.Length == 0) {
				return fallback == null ? "" : Sanitize(fallback, null);
			}
			string stem = result.Split('.')[0].Trim();
			if (Reserved.Contains(stem, StringComparer.OrdinalIgnoreCase)) {
				result = "_" + result;
			}
			return result;
		}

		// A folder for the game in the saves folder that isn't there yet:
		// its name, or "name (2)", "name (3)" and so on. Not created.
		public static string UniqueFolder(string savesDirectory, string name) {
			string path = Path.Combine(savesDirectory, name);
			for (int i = 2; Directory.Exists(path) || File.Exists(path); ++i) {
				path = Path.Combine(savesDirectory, $"{name} ({i})");
			}
			return path;
		}

		// Names the game and makes its folder in the saves folder; returns
		// what the game now remembers (GameData.playLog).
		public static PlayLogInfo Start(GameData gameData, string savesDirectory, string name) {
			string clean = Sanitize(name, DefaultName(gameData, DateTime.Now));
			string folder = UniqueFolder(savesDirectory, clean);
			Directory.CreateDirectory(folder);
			// The folder may have had to be "name (2)": the game takes its
			// folder's name, so its saves and log say which it is.
			string finalName = Path.GetFileName(folder);
			PlayLogInfo info = new() { name = finalName, folder = Path.GetFullPath(folder), logFile = finalName + ".jsonl" };
			gameData.playLog = info;
			return info;
		}

		// The game's folder as it is now: the folder the save was loaded from
		// if the log is there (the folder may have been moved or copied, with
		// the saves in it), otherwise the folder the game remembers, otherwise
		// (it's gone) the save's own folder. Remembered in the game.
		public static string Locate(GameData gameData, string loadedFrom) {
			PlayLogInfo info = gameData.playLog;
			if (info == null) {
				return null;
			}
			string saveFolder = string.IsNullOrEmpty(loadedFrom) ? null : Path.GetDirectoryName(Path.GetFullPath(loadedFrom));
			string folder;
			if (saveFolder != null && File.Exists(Path.Combine(saveFolder, info.logFile ?? ""))) {
				folder = saveFolder;
			} else if (!string.IsNullOrEmpty(info.folder) && Directory.Exists(info.folder)) {
				folder = info.folder;
			} else {
				folder = saveFolder ?? info.folder;
			}
			info.folder = folder;
			if (string.IsNullOrEmpty(info.logFile)) {
				info.logFile = Sanitize(info.name) + ".jsonl";
			}
			return folder;
		}

		public static string LogPath(PlayLogInfo info) => info == null ? null : Path.Combine(info.folder, info.logFile);

		// The name the save dialog offers for the game: "<name> Turn <turn>",
		// as its autosaves are "Autosave Turn <turn>".
		public static string SaveName(GameData gameData) =>
			gameData.playLog == null ? null : $"{gameData.playLog.name} Turn {gameData.turn}";

		// The last turn the log has a row of, or null for a log with none (or
		// none there).
		public static int? LastLoggedTurn(string logPath) {
			if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath)) {
				return null;
			}
			int? last = null;
			// Shared, in case something is writing it.
			using StreamReader reader = new(new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
			for (string line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
				if (string.IsNullOrWhiteSpace(line)) {
					continue;
				}
				try {
					using JsonDocument row = JsonDocument.Parse(line);
					if (row.RootElement.ValueKind == JsonValueKind.Object
							&& row.RootElement.TryGetProperty("turn", out JsonElement turn) && turn.TryGetInt32(out int t)) {
						last = t;
					}
				} catch (JsonException) {
					// A line cut short, as by a crash, says nothing.
				}
			}
			return last;
		}

		// Whether the log already has turns after the game's: a save from
		// before the latest was loaded, so recording it would repeat turns.
		public static bool LogIsAhead(GameData gameData) {
			int? last = LastLoggedTurn(LogPath(gameData.playLog));
			return last.HasValue && gameData.turn < last.Value;
		}
	}
}

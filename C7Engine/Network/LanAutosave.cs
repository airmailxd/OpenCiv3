using System;
using System.Collections.Generic;
using System.IO;
using C7GameData;
using C7GameData.Save;
using Serilog;

namespace C7Engine.Network;

// What a LAN host needs to host its game again after its own game ended:
// where and how it was hosted, and the guest in each human seat, with the
// token that guest says hello with to have the seat back.
public record LanResumeInfo(string hostName, int port, double? turnSeconds, bool simultaneousTurns, List<LanResumeSeat> seats);

public record LanResumeSeat(ID playerID, string playerName, string reconnectToken);

// A LAN host saves its game as each turn begins, so that if the host's game
// ends, by a crash or by mistake, it can host it again and its guests,
// still trying to reconnect, find their seats waiting. The save sits next to
// the one before it, in case the last one was cut short, and next to what
// resuming needs besides the game.
public static class LanAutosave {
	private static readonly ILogger log = Log.ForContext(typeof(LanAutosave));

	public const string SaveName = "LAN Autosave.json";
	public const string PreviousSaveName = "LAN Autosave (previous).json";
	public const string ResumeInfoName = "LAN Autosave.resume";

	// Where the game keeps its LAN autosaves.
	public static string DefaultDirectory => Path.Combine(C7Settings.WritableDirectory, "Saves");

	public static string SavePath(string directory) => Path.Combine(directory, SaveName);
	public static string PreviousSavePath(string directory) => Path.Combine(directory, PreviousSaveName);
	public static string ResumeInfoPath(string directory) => Path.Combine(directory, ResumeInfoName);

	// Whether there is a game to resume.
	public static bool Exists(string directory) {
		return File.Exists(ResumeInfoPath(directory))
			&& (File.Exists(SavePath(directory)) || File.Exists(PreviousSavePath(directory)));
	}

	// Writes the save and what resuming it needs. Each file is written whole
	// beside the old one and then put in its place, so a write cut short
	// leaves the last good one. Failing is logged and otherwise ignored:
	// it's no reason to stop the game.
	public static void Write(string directory, SaveGame save, LanResumeInfo info) {
		try {
			Directory.CreateDirectory(directory);
			string latest = SavePath(directory);
			string temporary = latest + ".tmp";
			save.Save(temporary);
			if (File.Exists(latest)) {
				File.Move(latest, PreviousSavePath(directory), overwrite: true);
			}
			File.Move(temporary, latest, overwrite: true);

			string infoPath = ResumeInfoPath(directory);
			File.WriteAllBytes(infoPath + ".tmp", NetSerialization.SerializeData(info));
			File.Move(infoPath + ".tmp", infoPath, overwrite: true);
			log.Information("Saved the LAN game for turn {Turn} to {Path}", save.TurnNumber, latest);
		} catch (Exception e) {
			log.Error(e, "Couldn't save the LAN game to {Directory}", directory);
		}
	}

	// What resuming needs besides the game, or null if there is nothing to
	// resume or it can't be read.
	public static LanResumeInfo ReadResumeInfo(string directory) {
		try {
			string path = ResumeInfoPath(directory);
			return File.Exists(path) ? NetSerialization.DeserializeData<LanResumeInfo>(File.ReadAllBytes(path)) : null;
		} catch (Exception e) {
			log.Warning("Couldn't read {Path}: {Error}", ResumeInfoPath(directory), e.Message);
			return null;
		}
	}

	// The paths of the saves to try resuming from, the latest first.
	public static List<string> SavesToResume(string directory) {
		List<string> paths = [];
		foreach (string path in new[] { SavePath(directory), PreviousSavePath(directory) }) {
			if (File.Exists(path)) {
				paths.Add(path);
			}
		}
		return paths;
	}
}

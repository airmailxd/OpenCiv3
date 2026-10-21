using System;
using System.Collections.Generic;

namespace C7Engine;

// Civ3's in-game music: which tracks play, in what order, for the human
// player's era and culture group, or from a scenario's text\music.txt. The
// tracks are files in the Civ3 install's sounds\build folders; these are the
// paths to look up there (the scenario's folder first, then Conquests, PTW
// and the root, as for any other media).
//
// Recovered from Civ3Conquests.exe 1.22: FUN_00540670 builds the playlist,
// FUN_00540b50 reads music.txt and FUN_00540370 moves on to the next track.
public static class Civ3Music {
	// Civ3 joins "sounds\build\" (0x7485f0) to every track's name.
	public const string BuildFolder = "Sounds/build/";

	// A Civ3 playlist holds at most 40 tracks (FUN_0060d8f0 refuses a 41st).
	public const int MaxTracks = 40;

	public const int EraCount = 4;
	public const int CultureGroupCount = 5;

	// The track for each era (rows) and culture group (columns: American,
	// European, Mediterranean, Mid East, Asian), the table of 28-byte entries
	// at 0x747ee8 indexed by culture group + era * 5. Each is played as the
	// name + "Full.mp3" (0x7485dc). The Industrial Age has no American track:
	// Civ3's table gives Americans the Mid East one (IndME) there.
	private static readonly string[,] Tracks = {
		{ "Ancient/AncNA", "Ancient/AncEC", "Ancient/AncGR", "Ancient/AncME", "Ancient/AncOR" },
		{ "Middle Ages/MidNA", "Middle Ages/MidEC", "Middle Ages/MidGR", "Middle Ages/MidME", "Middle Ages/MidOR" },
		{ "IndModern/IndME", "IndModern/IndEC", "IndModern/IndGR", "IndModern/IndME", "IndModern/IndOR" },
		{ "IndModern/Techno Mix", "Misc/Nightengale", "IndModern/Stars", "Misc/Modern", "IndModern/Smash" },
	};

	// The Ancient tracks also come cut into short pieces, played one after
	// another as the name + 'a', 'b', ... + ".wav" (0x7485e8) before the full
	// tracks when a game starts. The count is each entry's int at +0x18
	// (0x747f00); it is 0 for every later era.
	private static readonly int[] AncientPieceCounts = { 7, 9, 9, 10, 5 };

	// The order the era's five tracks play in (0x747e98, five ints per era):
	// the playlist starts with the player's own culture group's track and goes
	// on round this list from there.
	private static readonly int[,] Rotation = {
		{ 3, 2, 4, 0, 1 },
		{ 3, 1, 4, 2, 0 },
		{ 3, 2, 4, 0, 1 },
		{ 3, 2, 0, 1, 4 },
	};

	// The playlist for a player of the given era and culture group
	// (FUN_00540670). withPieces adds the Ancient pieces first: Civ3 asks for
	// them when a game starts and when the player enters a new era, not when
	// a game is loaded.
	public static MusicPlaylist ForEraAndCulture(int era, int cultureGroup, bool withPieces) {
		era = Math.Clamp(era, 0, EraCount - 1);
		cultureGroup = Math.Clamp(cultureGroup, 0, CultureGroupCount - 1);

		List<string> tracks = new();
		if (withPieces && era == 0) {
			string name = Tracks[era, cultureGroup];
			for (int i = 0; i < AncientPieceCounts[cultureGroup]; i++) {
				tracks.Add(BuildFolder + name + (char)('a' + i) + ".wav");
			}
		}
		int loopStart = tracks.Count;

		int start = 0;
		while (start < CultureGroupCount && Rotation[era, start] != cultureGroup) {
			start++;
		}
		start %= CultureGroupCount;
		for (int i = 0; i < CultureGroupCount; i++) {
			tracks.Add(BuildFolder + Tracks[era, Rotation[era, (start + i) % CultureGroupCount]] + "Full.mp3");
		}
		return new MusicPlaylist(tracks, loopStart);
	}

	// A scenario's text\music.txt (FUN_00540b50): one track per line, a path
	// under sounds\build\, with blank lines skipped and the rest trimmed. A
	// scenario that has one plays it instead of the era and culture tracks.
	public static List<string> ParseMusicTxt(string text) {
		List<string> tracks = new();
		foreach (string rawLine in text.Split('\n')) {
			string line = rawLine.Trim();
			if (line.Length == 0) {
				continue;
			}
			if (tracks.Count == MaxTracks) {
				break;
			}
			tracks.Add(BuildFolder + line.Replace('\\', '/'));
		}
		return tracks;
	}

	// The playlist from a scenario's music.txt, played in its order and over
	// again from the top (FUN_00540370).
	public static MusicPlaylist FromMusicTxt(string text) {
		return new MusicPlaylist(ParseMusicTxt(text), 0);
	}

	// The culture groups' codes in the Diplomusic file names.
	private static readonly string[] EarlyDiplomacyCodes = { "NA", "EC", "GR", "ME", "AS" };
	private static readonly string[] LateDiplomacyCodes = { "NA", "Eu", "Eu", "ME", "AS" };

	// The diplomacy screen's music (FUN_0050f970): the other civ's culture
	// group's theme, its early one while the human player is in the Ancient
	// Times or Middle Ages and its late one after, and its war or peace
	// version by whether the two are at war. The European and Mediterranean
	// civs share the late themes (the tables at 0x7481a8, 0x7481bc, 0x7481d0
	// and 0x7481e4).
	public static string DiplomacyTrack(int humanEra, int otherCultureGroup, bool atWar) {
		otherCultureGroup = Math.Clamp(otherCultureGroup, 0, CultureGroupCount - 1);
		bool early = humanEra < 2;
		string code = early ? EarlyDiplomacyCodes[otherCultureGroup] : LateDiplomacyCodes[otherCultureGroup];
		string name = "Dip" + code + (early ? "Early" : "Late") + (atWar ? "War" : "Peace");
		// The Asian late peace theme alone has a "-2" on its file name.
		if (!early && !atWar && otherCultureGroup == 4) {
			name += "-2";
		}
		return "Sounds/Diplomusic/" + name + ".mp3";
	}
}

// The tracks of a Civ3 playlist. They play in order; after the last one the
// playlist goes back to LoopStart, so pieces before it play only once
// (FUN_00540370).
public class MusicPlaylist {
	public readonly IReadOnlyList<string> Tracks;
	public readonly int LoopStart;

	public MusicPlaylist(IReadOnlyList<string> tracks, int loopStart) {
		Tracks = tracks;
		LoopStart = loopStart;
	}

	// The track to play after the given one, or -1 for an empty playlist.
	public int Next(int current) {
		if (Tracks.Count == 0) {
			return -1;
		}
		int next = current + 1;
		return next < Tracks.Count ? next : Math.Min(LoopStart, Tracks.Count - 1);
	}
}

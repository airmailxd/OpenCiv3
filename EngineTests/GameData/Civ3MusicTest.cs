using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C7Engine;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

public class Civ3MusicTest {
	private const int American = 0, European = 1, Mediterranean = 2, MidEast = 3, Asian = 4;

	[Fact]
	public void ThePlaylistStartsWithTheCultureGroupsOwnTrack() {
		MusicPlaylist playlist = Civ3Music.ForEraAndCulture(0, American, withPieces: false);

		// The Ancient order is ME, GR, OR, NA, EC; Americans start at NA and
		// go round from there.
		Assert.Equal(new[] {
			"Sounds/build/Ancient/AncNAFull.mp3",
			"Sounds/build/Ancient/AncECFull.mp3",
			"Sounds/build/Ancient/AncMEFull.mp3",
			"Sounds/build/Ancient/AncGRFull.mp3",
			"Sounds/build/Ancient/AncORFull.mp3",
		}, playlist.Tracks);
		Assert.Equal(0, playlist.LoopStart);
	}

	[Fact]
	public void EachEraHasItsOwnOrder() {
		Assert.Equal(new[] { "MidME", "MidEC", "MidOR", "MidGR", "MidNA" },
			Names(Civ3Music.ForEraAndCulture(1, MidEast, false)));
		Assert.Equal(new[] { "IndOR", "IndME", "IndEC", "IndME", "IndGR" },
			Names(Civ3Music.ForEraAndCulture(2, Asian, false)));
		Assert.Equal(new[] { "Stars", "Techno Mix", "Nightengale", "Smash", "Modern" },
			Names(Civ3Music.ForEraAndCulture(3, Mediterranean, false)));
	}

	[Fact]
	public void IndustrialAmericansGetTheMidEastTrack() {
		Assert.Equal("IndME", Names(Civ3Music.ForEraAndCulture(2, American, false))[0]);
	}

	[Fact]
	public void ANewGamePlaysTheAncientPiecesOnceFirst() {
		MusicPlaylist playlist = Civ3Music.ForEraAndCulture(0, European, withPieces: true);

		Assert.Equal(9 + 5, playlist.Tracks.Count);
		Assert.Equal("Sounds/build/Ancient/AncECa.wav", playlist.Tracks[0]);
		Assert.Equal("Sounds/build/Ancient/AncECi.wav", playlist.Tracks[8]);
		Assert.Equal("Sounds/build/Ancient/AncECFull.mp3", playlist.Tracks[9]);
		Assert.Equal(9, playlist.LoopStart);

		// After the last full track the playlist goes back to the first full
		// track, not the pieces.
		Assert.Equal(1, playlist.Next(0));
		Assert.Equal(9, playlist.Next(8));
		Assert.Equal(9, playlist.Next(13));
	}

	[Fact]
	public void OnlyTheAncientEraHasPieces() {
		Assert.Equal(5, Civ3Music.ForEraAndCulture(1, European, withPieces: true).Tracks.Count);
		Assert.Equal(7 + 5, Civ3Music.ForEraAndCulture(0, American, withPieces: true).Tracks.Count);
		Assert.Equal(10 + 5, Civ3Music.ForEraAndCulture(0, MidEast, withPieces: true).Tracks.Count);
		Assert.Equal(5 + 5, Civ3Music.ForEraAndCulture(0, Asian, withPieces: true).Tracks.Count);
	}

	[Fact]
	public void OutOfRangeErasAndCultureGroupsAreClamped() {
		Assert.Equal(Civ3Music.ForEraAndCulture(3, Asian, false).Tracks, Civ3Music.ForEraAndCulture(7, 9, false).Tracks);
		Assert.Equal(Civ3Music.ForEraAndCulture(0, American, false).Tracks, Civ3Music.ForEraAndCulture(-1, -1, false).Tracks);
	}

	[Fact]
	public void MusicTxtListsTracksUnderSoundsBuild() {
		MusicPlaylist playlist = Civ3Music.FromMusicTxt("Misc\\Greco Roman.mp3\r\n  Ancient Music\\AncGR.mp3 \r\n\r\nMiddle Ages\\MidGRFull.MP3\r\n\r\n");

		Assert.Equal(new[] {
			"Sounds/build/Misc/Greco Roman.mp3",
			"Sounds/build/Ancient Music/AncGR.mp3",
			"Sounds/build/Middle Ages/MidGRFull.MP3",
		}, playlist.Tracks);
		// It plays in order, over again from the top.
		Assert.Equal(1, playlist.Next(0));
		Assert.Equal(0, playlist.Next(2));
	}

	[Fact]
	public void MusicTxtHoldsAtMostFortyTracks() {
		string text = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"Misc\\{i}.mp3"));
		Assert.Equal(Civ3Music.MaxTracks, Civ3Music.ParseMusicTxt(text).Count);
	}

	[Fact]
	public void AnEmptyPlaylistHasNoNextTrack() {
		Assert.Equal(-1, Civ3Music.FromMusicTxt("\n\n").Next(-1));
	}

	[Fact]
	public void DiplomacyPlaysTheOtherCivsTheme() {
		Assert.Equal("Sounds/Diplomusic/DipGREarlyPeace.mp3", Civ3Music.DiplomacyTrack(0, Mediterranean, false));
		Assert.Equal("Sounds/Diplomusic/DipNAEarlyWar.mp3", Civ3Music.DiplomacyTrack(1, American, true));
		Assert.Equal("Sounds/Diplomusic/DipEuLateWar.mp3", Civ3Music.DiplomacyTrack(2, Mediterranean, true));
		Assert.Equal("Sounds/Diplomusic/DipEuLatePeace.mp3", Civ3Music.DiplomacyTrack(3, European, false));
		Assert.Equal("Sounds/Diplomusic/DipASLatePeace-2.mp3", Civ3Music.DiplomacyTrack(2, Asian, false));
		Assert.Equal("Sounds/Diplomusic/DipASLateWar.mp3", Civ3Music.DiplomacyTrack(2, Asian, true));
	}

	// Every track the playlists and the diplomacy screen name is in the
	// install, found the way the game finds media: Conquests, then PTW, then
	// the root.
	[SkippableFact]
	public void EveryTrackIsInTheInstall() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), Civ3TestData.SkipReason);
		string root = Civ3Location.GetCiv3Path();

		List<string> tracks = new();
		for (int era = 0; era < Civ3Music.EraCount; era++) {
			for (int culture = 0; culture < Civ3Music.CultureGroupCount; culture++) {
				tracks.AddRange(Civ3Music.ForEraAndCulture(era, culture, withPieces: true).Tracks);
				tracks.Add(Civ3Music.DiplomacyTrack(era, culture, false));
				tracks.Add(Civ3Music.DiplomacyTrack(era, culture, true));
			}
		}

		List<string> missing = tracks.Distinct()
			.Where(t => !new[] { "Conquests", "civ3PTW", "" }.Any(dir => FindIgnoringCase(root, Path.Combine(dir, t))))
			.ToList();
		Assert.True(missing.Count == 0, $"Not in {root}: {string.Join(", ", missing)}");
	}

	// The Conquests scenarios' music.txt files name tracks in both the
	// Conquests and the original sounds\build folders.
	[SkippableFact]
	public void EveryScenarioMusicTxtTrackIsInTheInstall() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), Civ3TestData.SkipReason);
		string root = Civ3Location.GetCiv3Path();
		string scenarios = Path.Combine(root, "Conquests", "Conquests");
		Skip.IfNot(Directory.Exists(scenarios), "No Conquests scenarios");

		List<string> files = Directory.EnumerateFiles(scenarios, "music.txt", SearchOption.AllDirectories).ToList();
		Skip.If(files.Count == 0, "No scenario has a music.txt");
		foreach (string file in files) {
			MusicPlaylist playlist = Civ3Music.FromMusicTxt(File.ReadAllText(file));
			Assert.NotEmpty(playlist.Tracks);
			List<string> missing = playlist.Tracks
				.Where(t => !new[] { "Conquests", "civ3PTW", "" }.Any(dir => FindIgnoringCase(root, Path.Combine(dir, t))))
				.ToList();
			Assert.True(missing.Count == 0, $"{file} names missing tracks: {string.Join(", ", missing)}");
		}
	}

	private static string[] Names(MusicPlaylist playlist) {
		return playlist.Tracks.Select(t => Path.GetFileName(t)[..^"Full.mp3".Length]).ToArray();
	}

	private static bool FindIgnoringCase(string root, string relativePath) {
		string current = root;
		foreach (string part in relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)) {
			if (!Directory.Exists(current)) {
				return false;
			}
			string match = Directory.EnumerateFileSystemEntries(current)
				.FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, StringComparison.OrdinalIgnoreCase));
			if (match == null) {
				return false;
			}
			current = match;
		}
		return File.Exists(current);
	}
}

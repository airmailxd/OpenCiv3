using System;
using System.IO;
using C7Engine;
using Godot;
using Serilog;

// Plays Civ3's in-game music: the playlist for the human player's era and
// culture group (or a scenario's music.txt), one track after another, and
// the diplomacy screen's music over it. The tracks are the Civ3 install's own
// files, found at runtime; any that can't be found are skipped, so without an
// install (or in standalone mode) this stays silent. Both players are on the
// Music bus, so the Music volume and the Music On preference apply.
public partial class InGameMusic : Node {
	private ILogger log = LogManager.ForContext<InGameMusic>();

	// Civ3 fades the playlist out over 3 seconds when the diplomacy screen
	// opens and back in over 2 when it closes, and fades the diplomacy music
	// in over 1.5 seconds and out over 2 (FUN_0050f970, FUN_0050fb80).
	private const double DiplomacyFadeOutSeconds = 3.0;
	private const double DiplomacyFadeInSeconds = 1.5;
	private const double ReturnFadeSeconds = 2.0;
	private const float SilentDb = -60f;

	private AudioStreamPlayer playlistPlayer;
	private AudioStreamPlayer diplomacyPlayer;
	private Tween playlistFade;
	private Tween diplomacyFade;

	private MusicPlaylist playlist;
	private int currentTrack = -1;
	private string diplomacyTrack;

	// What the playlist was built for, so it's only rebuilt when that changes.
	private (int Era, int CultureGroup)? playlistKey;

	public bool IsPlaying => playlist != null;

	public override void _Ready() {
		playlistPlayer = new AudioStreamPlayer { Bus = AudioManager.MusicBus };
		playlistPlayer.Finished += PlayNextTrack;
		AddChild(playlistPlayer);
		diplomacyPlayer = new AudioStreamPlayer { Bus = AudioManager.MusicBus };
		AddChild(diplomacyPlayer);
	}

	// Plays the playlist for the human player's era and culture group, unless
	// it's already playing. withPieces starts with the Ancient pieces, as
	// Civ3 does when a game starts or the player enters a new era.
	public void PlayFor(int era, int cultureGroup, bool withPieces) {
		if (playlist != null && playlistKey == (era, cultureGroup)) {
			return;
		}
		playlistKey = (era, cultureGroup);
		Play(ScenarioPlaylist() ?? Civ3Music.ForEraAndCulture(era, cultureGroup, withPieces));
	}

	// A scenario's text\music.txt replaces the era and culture tracks
	// (FUN_00540b50).
	private static MusicPlaylist ScenarioPlaylist() {
		if (!Util.HasModPath || !Util.HasCiv3Media("Text/music.txt")) {
			return null;
		}
		try {
			MusicPlaylist fromFile = Civ3Music.FromMusicTxt(File.ReadAllText(Util.Civ3MediaPath("Text/music.txt")));
			return fromFile.Tracks.Count > 0 ? fromFile : null;
		} catch (Exception e) when (e is IOException or UnauthorizedAccessException or ApplicationException) {
			return null;
		}
	}

	private void Play(MusicPlaylist newPlaylist) {
		playlist = newPlaylist;
		currentTrack = -1;
		playlistPlayer.Stop();
		PlayNextTrack();
	}

	public void Stop() {
		playlist = null;
		playlistKey = null;
		currentTrack = -1;
		playlistFade?.Kill();
		playlistPlayer.Stop();
		playlistPlayer.VolumeDb = 0;
		StopDiplomacyMusicNow();
	}

	private void PlayNextTrack() {
		if (playlist == null) {
			return;
		}
		// Skip the tracks that can't be found or played, but stop once every
		// one of them has been tried.
		for (int tries = 0; tries < playlist.Tracks.Count; tries++) {
			currentTrack = playlist.Next(currentTrack);
			if (currentTrack < 0) {
				break;
			}
			AudioStream stream = LoadTrack(playlist.Tracks[currentTrack]);
			if (stream != null) {
				playlistPlayer.Stream = stream;
				playlistPlayer.Play();
				return;
			}
		}
		log.Debug("No in-game music could be found");
		playlist = null;
	}

	// Loads a track from the Civ3 install, or null if it isn't there. These
	// play once, unlike the menu music, so the playlist moves on.
	private AudioStream LoadTrack(string path) {
		if (!Util.HasCiv3Media(path)) {
			return null;
		}
		try {
			string fullPath = Util.Civ3MediaPath(path);
			if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) {
				return Util.LoadWAVFromDisk(fullPath);
			}
			return new AudioStreamMP3 { Data = File.ReadAllBytes(fullPath), Loop = false };
		} catch (Exception e) {
			log.Warning(e, "Could not load the music {path}", path);
			return null;
		}
	}

	// The diplomacy screen opened (or moved on to another civ): fade the
	// playlist out and play that civ's theme.
	public void PlayDiplomacyMusic(int humanEra, int otherCultureGroup, bool atWar) {
		string track = Civ3Music.DiplomacyTrack(humanEra, otherCultureGroup, atWar);
		if (track == diplomacyTrack && diplomacyPlayer.Playing) {
			return;
		}
		diplomacyTrack = track;
		playlistFade?.Kill();
		playlistFade = Fade(playlistPlayer, SilentDb, DiplomacyFadeOutSeconds);

		diplomacyFade?.Kill();
		diplomacyPlayer.Stop();
		AudioStream stream = LoadTrack(track);
		if (stream == null) {
			return;
		}
		diplomacyPlayer.Stream = stream;
		diplomacyPlayer.VolumeDb = SilentDb;
		diplomacyPlayer.Play();
		diplomacyFade = Fade(diplomacyPlayer, 0, DiplomacyFadeInSeconds);
	}

	// The diplomacy screen closed: fade its music out and the playlist back in.
	public void StopDiplomacyMusic() {
		if (diplomacyTrack == null) {
			return;
		}
		diplomacyTrack = null;
		playlistFade?.Kill();
		playlistFade = Fade(playlistPlayer, 0, ReturnFadeSeconds);
		diplomacyFade?.Kill();
		if (diplomacyPlayer.Playing) {
			diplomacyFade = Fade(diplomacyPlayer, SilentDb, ReturnFadeSeconds);
			diplomacyFade.TweenCallback(Callable.From(diplomacyPlayer.Stop));
		}
	}

	private void StopDiplomacyMusicNow() {
		diplomacyTrack = null;
		diplomacyFade?.Kill();
		diplomacyPlayer.Stop();
	}

	private Tween Fade(AudioStreamPlayer player, float toDb, double seconds) {
		Tween tween = CreateTween();
		tween.TweenProperty(player, "volume_db", toDb, seconds);
		return tween;
	}
}

using System;
using System.IO;
using C7Engine;
using Godot;

// The intro movie, played full screen over the main menu when the game starts.
//
// The movie isn't part of the repo or the download: it's played if there's an
// Ogg Theora file, Intro/OpenCiv3_Intro.ogv, in the folder the settings are
// saved in or in the per-user OpenCiv3 folder (%AppData%\OpenCiv3 on Windows).
// Neither is touched by an update. It can be turned off in the settings, and
// any key or click skips it.
public partial class IntroVideo : CanvasLayer {
	public const string SettingsSection = "intro";
	public const string SettingsKey = "playOnLaunch";
	public const string FileName = "OpenCiv3_Intro.ogv";

	// Once per launch, not each time the menu is shown again.
	private static bool played = false;

	private VideoStreamPlayer player;
	private bool finished = false;

	public event Action Finished;

	public static bool Enabled =>
		C7Settings.GetSettingsValueOrDefault(SettingsSection, SettingsKey, "true") == "true";

	public static void SetEnabled(bool on) {
		C7Settings.SetValue(SettingsSection, SettingsKey, on ? "true" : "false");
		C7Settings.SaveSettings();
	}

	// The movie's file, or null if there isn't one.
	public static string FindFile() {
		string[] folders = {
			C7Settings.WritableDirectory,
			Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "OpenCiv3"),
		};
		foreach (string folder in folders) {
			string path = Path.Combine(folder, "Intro", FileName);
			if (File.Exists(path)) {
				return path;
			}
		}
		return null;
	}

	// An intro to play now, or null if it's been played, is turned off or
	// isn't there.
	public static IntroVideo TakeForLaunch() {
		if (played || !Enabled) {
			return null;
		}
		played = true;
		string path = FindFile();
		return path == null ? null : new IntroVideo(path);
	}

	private readonly string path;

	public IntroVideo() { }

	private IntroVideo(string path) {
		this.path = path;
		Layer = 100;
	}

	public override void _Ready() {
		ColorRect black = new() { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Stop };
		black.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(black);

		player = new VideoStreamPlayer {
			Stream = new VideoStreamTheora { File = path },
			Expand = true,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		black.AddChild(player);
		player.Finished += Finish;
		Fit();
		player.Play();
		if (!player.IsPlaying()) {
			Serilog.Log.ForContext<IntroVideo>().Warning("Could not play the intro movie {Path}", path);
			CallDeferred(nameof(Finish));
		}
	}

	// Fills the screen, keeping the movie's shape.
	private void Fit() {
		if (player == null) {
			return;
		}
		Vector2 screen = GetViewport().GetVisibleRect().Size;
		Texture2D frame = player.GetVideoTexture();
		Vector2 size = frame != null && frame.GetWidth() > 0 ? frame.GetSize() : new Vector2(1920, 1080);
		float scale = Math.Min(screen.X / size.X, screen.Y / size.Y);
		player.Size = size * scale;
		player.Position = (screen - player.Size) / 2;
	}

	public override void _Process(double delta) {
		// The frame's size is known once the movie starts, and the window
		// can be resized while it plays.
		Fit();
	}

	public override void _Input(InputEvent e) {
		if ((e is InputEventKey key && key.Pressed && !key.Echo)
				|| (e is InputEventMouseButton mouse && mouse.Pressed)
				|| (e is InputEventJoypadButton pad && pad.Pressed)) {
			GetViewport().SetInputAsHandled();
			Finish();
		}
	}

	private void Finish() {
		if (finished) {
			return;
		}
		finished = true;
		player?.Stop();
		Finished?.Invoke();
		QueueFree();
	}
}

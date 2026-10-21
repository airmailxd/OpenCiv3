using System;
using System.Collections.Generic;
using C7GameData;
using Godot;
using Serilog;

// Civ3's wonder splash (FUN_005de750), shown to a player who has completed a
// great or small wonder (see WonderSplash): the wonder's picture fading in on
// a frame, its name below it, and "$RULER_TITLE0, we have completed $LINK0 in
// $CITY1." (script.txt, WONDERSPLASH), to the sound of Sounds/Wonder.wav. It
// goes away with its OK button, Enter or Escape (FUN_005df150).
//
// Everything is drawn in the frame's 1024x768 window, where Civ3 puts it:
// the picture's place is set by FUN_005de670, the rest by the paint function
// at 0x5ded90. Without the frame (Art/Wonder Splash/wonderBackground.pcx, so
// in standalone mode too) the news comes in a plain popup instead.
public partial class WonderSplashScreen : Popup {
	private static readonly ILogger log = LogManager.ForContext<WonderSplashScreen>();

	public const string BackgroundPath = "Art/Wonder Splash/wonderBackground.pcx";

	private const int FrameWidth = 1024;
	private const int FrameHeight = 768;

	// The picture (320x320) and the white mat below it, where the name goes.
	private static readonly Vector2 picturePosition = new(351, 109);
	private static readonly Vector2 namePosition = new(334, 444);
	private const int NameWidth = 353;
	private const int NameFontSize = 24;

	// The bar below the frame, for the message.
	private static readonly Vector2 messagePosition = new(307, 543);
	private const int MessageWidth = 340;
	private const int MessageFontSize = 16;

	// The OK button, in the small box in the bottom right corner.
	private static readonly Vector2 okPosition = new(678, 620);

	// Civ3 steps the picture's opacity, and the name from white to black, by
	// 1/16 every 80 ms.
	private const double FadeSeconds = 16 * 0.080;

	private readonly Texture2D background;
	private readonly Texture2D picture;
	private readonly string wonderName;
	private readonly string message;

	private WonderSplashScreen(Texture2D background, Texture2D picture, string wonderName, string message) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins();
		this.background = background;
		this.picture = picture;
		this.wonderName = wonderName;
		this.message = message;
	}

	// The popup telling the player of their wonder: the splash, or a plain
	// popup when the frame is missing or in a network game (Civ3's
	// MP_WONDER_COMPLETION). Null if the building isn't a wonder.
	public static Popup For(Building wonder, City city, Player player, bool networkGame) {
		if (!WonderSplash.IsShownFor(wonder) || city == null) {
			return null;
		}
		if (!networkGame) {
			string text = WonderSplash.Message(WonderSplash.RulerTitle(player, Random.Shared), wonder.name, city.name);
			Texture2D background = LoadTexture(BackgroundPath);
			if (background != null) {
				return new WonderSplashScreen(background, LoadPicture(wonder), wonder.name, text);
			}
			return new InformationalPopup(text, AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Happy);
		}
		return new InformationalPopup(WonderSplash.PlainMessage(wonder.name, city.name),
			AdvisorHead.Advisor.Domestic, AdvisorHead.Mood.Happy);
	}

	public override void _Ready() {
		base._Ready();
		AddTexture(FrameWidth, FrameHeight);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		MouseFilter = MouseFilterEnum.Stop;

		AddChild(new TextureRect { Texture = background, MouseFilter = MouseFilterEnum.Ignore });

		TextureRect pictureRect = null;
		if (picture != null) {
			pictureRect = new TextureRect {
				Texture = picture,
				Position = picturePosition,
				Modulate = new Color(1, 1, 1, 0),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			AddChild(pictureRect);
		}

		Label name = MakeLabel(wonderName, NameFontSize, Colors.White);
		name.HorizontalAlignment = HorizontalAlignment.Center;
		name.Position = namePosition;
		name.Size = new Vector2(NameWidth, 0);
		AddChild(name);

		Label messageLabel = MakeLabel(message, MessageFontSize, Colors.Black);
		messageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		messageLabel.Position = messagePosition;
		messageLabel.Size = new Vector2(MessageWidth, 0);
		AddChild(messageLabel);

		AddConfirmButton(okPosition, Close);

		Tween fade = CreateTween().SetParallel();
		if (pictureRect != null) {
			fade.TweenProperty(pictureRect, "modulate:a", 1.0, FadeSeconds);
		}
		fade.TweenProperty(name, "theme_override_colors/font_color", Colors.Black, FadeSeconds);

		if (GetParent() is PopupOverlay overlay) {
			try {
				if (AudioLoader.Load("popups.wonder") is AudioStream sound) {
					overlay.PlaySound(sound);
				}
			} catch (Exception e) {
				log.Debug("No wonder sound: {message}", e.Message);
			}
		}
	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventKey { Pressed: true, Echo: false } key
				&& key.Keycode is Key.Enter or Key.KpEnter) {
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	private void Close() {
		if (IsInsideTree()) {
			GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
		}
	}

	private static Label MakeLabel(string text, int fontSize, Color color) {
		Label label = new() { Text = text, MouseFilter = MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	// The wonder's picture, listed in PediaIcons.txt under WON_SPLASH_ and
	// its Civilopedia entry. Civ3 shows the splash without it when it's
	// missing (as Conquests' Internet is).
	private static Texture2D LoadPicture(Building wonder) {
		IReadOnlyList<string> block = LoadPediaIcons()?.GetBlock(WonderSplash.ArtKey(wonder));
		if (block == null || block.Count == 0) {
			log.Debug("No wonder splash for {wonder} in PediaIcons.txt", wonder.name);
			return null;
		}
		return LoadTexture(block[0]);
	}

	private static Texture2D LoadTexture(string path) {
		try {
			return Util.HasCiv3Media(path) ? TextureLoader.LoadByPath(path) : null;
		} catch (Exception e) {
			log.Debug("Could not load {path}: {message}", path, e.Message);
			return null;
		}
	}

	// The PediaIcons.txt of the game's rules (a scenario may have its own),
	// read once per file.
	private static string pediaIconsPath;
	private static PediaIcons pediaIcons;

	private static PediaIcons LoadPediaIcons() {
		const string mediaPath = "Text/PediaIcons.txt";
		string path = Util.HasCiv3Media(mediaPath) ? Util.Civ3MediaPath(mediaPath) : null;
		if (path != pediaIconsPath) {
			pediaIconsPath = path;
			pediaIcons = null;
			if (path != null) {
				try {
					pediaIcons = new PediaIcons(path);
				} catch (Exception e) {
					log.Warning(e, "Could not read {path}", path);
				}
			}
		}
		return pediaIcons;
	}
}

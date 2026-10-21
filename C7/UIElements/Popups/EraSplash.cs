using System;
using System.Collections.Generic;
using C7GameData;
using Godot;
using Serilog;

// Civ3's era splash (Civ3 FUN_0052d140), shown to a player entering a new
// era: a 670x640 window with the old era's picture at (15, 15), which after
// two seconds fades into the new era's in sixteen steps of 80 ms (the
// window's tick, FUN_0052de30, and paint, FUN_0052dcd0); under the
// pictures a line from script.txt (#TECH_NEW_ERA1 or 2) at (15, 510), and
// the two choices at (15, 555). "Great, let's play the game." just closes
// it; "What's the big picture?" sets the player researching a tech of the
// new era and opens the Science Advisor (the choice handler at 0x52cea0).
//
// The pictures are each era's first line under #ERA_SPLASH_<era> in
// PediaIcons.txt (art\erasplash\*.pcx, 640x480). Without them (standalone
// mode, or a scenario without the entries) it is a plain popup with just
// the text and the choices.
public partial class EraSplash : Popup {
	private static readonly ILogger log = LogManager.ForContext<EraSplash>();

	private const int Width = 670;
	private const int Height = 640;
	private const int PictureWidth = 640;
	private const int PictureHeight = 480;

	// Civ3 holds the old era's picture for 2 seconds, then fades in the new
	// one by 1/16 every 80 ms.
	private const double HoldSeconds = 2.0;
	private const double FadeSeconds = 16 * 0.080;

	private readonly string previousEra;
	private readonly string era;
	private readonly string text;
	private readonly Action bigPicture;

	private static PediaIcons pediaIcons;
	private static string pediaIconsPath;

	public EraSplash(string previousEra, string era, string text, Action bigPicture) {
		alignment = BoxContainer.AlignmentMode.Center;
		margins = new Margins();
		this.previousEra = previousEra;
		this.era = era;
		this.text = text;
		this.bigPicture = bigPicture;
	}

	public override void _Ready() {
		base._Ready();

		Texture2D oldPicture = LoadPicture(previousEra);
		Texture2D newPicture = LoadPicture(era) ?? oldPicture;
		// Without pictures, the text and choices go under a header instead.
		int height = newPicture == null ? 190 : Height;

		AddTexture(Width, height);
		AddBackground(Width, height);

		int top = 15;
		if (newPicture != null) {
			if (oldPicture != null && oldPicture != newPicture) {
				AddPicture(oldPicture);
				TextureRect fadingIn = AddPicture(newPicture);
				fadingIn.Modulate = new Color(1, 1, 1, 0);
				Tween tween = CreateTween();
				tween.TweenInterval(HoldSeconds);
				tween.TweenProperty(fadingIn, "modulate:a", 1.0f, FadeSeconds);
			} else {
				AddPicture(newPicture);
			}
			top += PictureHeight + 15;
		} else {
			AddHeader(EraUtils.GetNiceEraName(era), 10);
			top = 55;
		}

		Label message = new() {
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Size = new Vector2(PictureWidth, 0),
		};
		message.SetPosition(new Vector2(15, top));
		AddChild(message);

		int choicesTop = top + 45;
		AddChoice(NewEra.BigPicture, choicesTop, () => {
			Close();
			bigPicture();
		});
		AddChoice(NewEra.LetsPlay, choicesTop + 25, Close);
	}

	private TextureRect AddPicture(Texture2D picture) {
		TextureRect rect = new() {
			Texture = picture,
			Size = new Vector2(PictureWidth, PictureHeight),
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		rect.SetPosition(new Vector2(15, 15));
		AddChild(rect);
		return rect;
	}

	private void AddChoice(string label, int top, Action action) {
		Civ3MenuButton button = new() {
			Text = label,
			FontSize = 14,
		};
		button.SetPosition(new Vector2(15, top));
		AddChild(button);
		button.Pressed += action;
	}

	private void Close() {
		GetParent().EmitSignal(PopupOverlay.SignalName.HidePopup);
	}

	// The era's picture: the first line of its #ERA_SPLASH_ entry in
	// PediaIcons.txt (FUN_0052d140 looks up "ERA_SPLASH_" plus the era's
	// civilopedia name), or null if there is none.
	private static Texture2D LoadPicture(string era) {
		if (string.IsNullOrEmpty(era)) {
			return null;
		}
		try {
			IReadOnlyList<string> entry = Icons()?.GetBlock("ERA_SPLASH_" + era);
			if (entry == null || entry.Count == 0 || !Util.HasCiv3Media(entry[0])) {
				return null;
			}
			return TextureLoader.LoadByPath(entry[0]);
		} catch (Exception e) {
			log.Warning(e, "Could not load the era splash for {Era}", era);
			return null;
		}
	}

	// PediaIcons.txt, read again only if a scenario or a switch to or from
	// standalone mode means another one.
	private static PediaIcons Icons() {
		string path = Util.HasCiv3Media("Text/PediaIcons.txt") ? Util.Civ3MediaPath("Text/PediaIcons.txt") : null;
		if (path != pediaIconsPath) {
			pediaIconsPath = path;
			pediaIcons = path == null ? null : new PediaIcons(path);
		}
		return pediaIcons;
	}
}

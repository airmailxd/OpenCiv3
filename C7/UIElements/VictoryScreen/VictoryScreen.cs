using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using ConvertCiv3Media;
using Godot;
using Serilog;

// Civ3's end-of-game screen (see VictoryScreenLayout): the background for
// how the game ended, the leaders' heads with faces to suit, and every few
// seconds one of the other leaders has something to say in a speech
// bubble. Escape or the close button in the corner closes it, and Enter
// brings on the next remark (Civ3 FUN_005de580, FUN_005de5c0).
public partial class VictoryScreen : Control {
	private static readonly ILogger log = LogManager.ForContext<VictoryScreen>();

	public event Action Closed;

	// A head to draw, with what its leader can say.
	private sealed record Head(VictoryScreenHead Place, ImageTexture Face, ImageTexture Loved, List<string> Remarks);

	private readonly int screen;
	private readonly ImageTexture background;
	private readonly List<Head> heads;
	private readonly ImageTexture bubbleTop, bubbleMiddle, bubbleBottom;
	private readonly ImageTexture exitNormal, exitHover;

	private Control board;
	private Control bubble;
	private ulong bubbleShownAt;
	private ulong nextBubbleAt;
	private bool closed;

	private VictoryScreen(int screen, ImageTexture background, List<Head> heads, ImageTexture bubbleTop,
			ImageTexture bubbleMiddle, ImageTexture bubbleBottom, ImageTexture exitNormal, ImageTexture exitHover) {
		this.screen = screen;
		this.background = background;
		this.heads = heads;
		this.bubbleTop = bubbleTop;
		this.bubbleMiddle = bubbleMiddle;
		this.bubbleBottom = bubbleBottom;
		this.exitNormal = exitNormal;
		this.exitHover = exitHover;
		Name = "VictoryScreen";
		MouseFilter = MouseFilterEnum.Stop;
	}

	// The screen for the player, or null if the Civ3 art it needs isn't
	// there. Reads the game, so call it with the game data at hand.
	public static VictoryScreen TryCreate(GameData gameData, Player player, Player winner, GameEndReason reason,
			Civ3EndGameText text) {
		try {
			return Create(gameData, player, winner, reason, text);
		} catch (Exception e) {
			log.Warning(e, "Couldn't build the victory screen");
			return null;
		}
	}

	private static VictoryScreen Create(GameData gameData, Player player, Player winner, GameEndReason reason,
			Civ3EndGameText text) {
		if (player == null || text == null) {
			return null;
		}
		int screen = VictoryScreenLayout.ScreenFor(reason, player == winner);
		ImageTexture background = LoadArt(VictoryScreenLayout.Background(screen));
		PediaIcons icons = LoadPediaIcons();
		if (background == null || icons == null) {
			return null;
		}

		List<Player> civs = gameData.players.Where(p => !p.isBarbarians && p.civilization != null).ToList();
		bool lockedAlliances = civs.Any(p => p.alliance != null);
		bool Alive(Player p) => !p.defeated;
		bool Allied(Player a, Player b) => gameData.AreInLockedPeace(a, b);
		List<VictoryScreenHead> places = VictoryScreenLayout.Heads(screen, civs, player, winner, Alive, lockedAlliances, Allied);

		// The winner's small "loved" face, drawn over the rivals' hearts on
		// the cultural victory screen.
		ImageTexture loved = screen == VictoryScreenLayout.CulturalScreen && winner != null
			? LoadFace(icons, VictoryScreenLayout.Loved, winner) : null;

		List<Head> heads = [];
		foreach (VictoryScreenHead place in places) {
			ImageTexture face = LoadFace(icons, place.Mood, place.Player);
			if (face == null) {
				// Civ3 complains of a missing PediaIcons entry; we just
				// leave the head out.
				continue;
			}
			Player speaker = place.Player;
			// Who destroyed whom isn't kept, so the remarks that depend on
			// it are only the ones for civs that never met the player.
			List<string> remarks = place.Slot == 0 ? [] : VictoryScreenLayout.Remarks(screen, player, winner, speaker,
					Alive(speaker), lockedAlliances, winner != null && Allied(speaker, winner),
					speakerDestroyer: null, playerDestroyer: null, metPlayer: player.playerRelationships.ContainsKey(speaker.id))
				.Select(text.Label).Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
			bool showLoved = loved != null && VictoryScreenLayout.ShowsLovedFace(screen, place, Alive(speaker));
			heads.Add(new Head(place, face, showLoved ? loved : null, remarks));
		}

		ImageTexture exitNormal = LoadArt(VictoryScreenLayout.ExitButtonArt, new CropRegion(1, 1, 36, 38));
		ImageTexture exitHover = LoadArt(VictoryScreenLayout.ExitButtonArt, new CropRegion(38, 1, 36, 38));
		return new VictoryScreen(screen, background, heads,
			LoadArt(VictoryScreenLayout.BubbleTop), LoadArt(VictoryScreenLayout.BubbleMiddle), LoadArt(VictoryScreenLayout.BubbleBottom),
			exitNormal, exitHover);
	}

	private static string pediaIconsPath;
	private static PediaIcons pediaIcons;

	private static PediaIcons LoadPediaIcons() {
		if (!Util.HasCiv3Media("Text/PediaIcons.txt")) {
			return null;
		}
		string path = Util.Civ3MediaPath("Text/PediaIcons.txt");
		if (path != pediaIconsPath) {
			pediaIcons = new PediaIcons(path);
			pediaIconsPath = path;
		}
		return pediaIcons;
	}

	// A leader's victory-screen face, like HAPPY_RACE_ROMANS in
	// PediaIcons.txt.
	private static ImageTexture LoadFace(PediaIcons icons, string mood, Player player) {
		string race = player.civilization?.civilopediaEntry;
		if (string.IsNullOrEmpty(race)) {
			return null;
		}
		IReadOnlyList<string> block = icons.GetBlock(mood + race);
		return block is { Count: > 0 } ? LoadArt(block[0]) : null;
	}

	// Civ3 art from the install, or null if it isn't there. Palette
	// entries 254 and 255 are see-through, as in all of Civ3's art (the
	// loved faces' black surround, the bubble's and button's magenta).
	private static ImageTexture LoadArt(string path, CropRegion? crop = null) {
		if (!Util.HasCiv3Media(path)) {
			return null;
		}
		Pcx pcx = TextureLoader.LoadPCX(path);
		PCXToGodot.ColorOptions options = new(shadows: false, transparentColorIndexes: [254, 255]);
		return crop is CropRegion region
			? PCXToGodot.getImageTextureFromPCX(pcx, region, options)
			: PCXToGodot.getImageTextureFromPCX(pcx, options);
	}

	public override void _Ready() {
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		// Civ3 draws the screen's 1024x768 in the middle of a black one.
		ColorRect black = new() { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
		black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(black);
		CenterContainer center = new() { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);
		board = new Control {
			CustomMinimumSize = new Vector2(VictoryScreenLayout.Width, VictoryScreenLayout.Height),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.AddChild(board);

		board.AddChild(new TextureRect { Texture = background, MouseFilter = MouseFilterEnum.Ignore });
		foreach (Head head in heads) {
			(int x, int y) = VictoryScreenLayout.Place(screen, head.Place.Slot);
			board.AddChild(new TextureRect { Texture = head.Face, Position = new Vector2(x, y), MouseFilter = MouseFilterEnum.Ignore });
			if (head.Loved != null) {
				board.AddChild(new TextureRect { Texture = head.Loved, Position = new Vector2(x, y), MouseFilter = MouseFilterEnum.Ignore });
			}
		}

		TextureButton exit = new() {
			Position = new Vector2(VictoryScreenLayout.ExitButtonX, VictoryScreenLayout.ExitButtonY),
			CustomMinimumSize = new Vector2(VictoryScreenLayout.ExitButtonSize, VictoryScreenLayout.ExitButtonSize),
		};
		if (exitNormal != null) {
			exit.TextureNormal = exitNormal;
			exit.TextureHover = exitHover ?? exitNormal;
			exit.TexturePressed = exitHover ?? exitNormal;
		} else {
			TextureLoader.SetButtonTextures(exit, "ui.cancel");
		}
		exit.Pressed += Close;
		board.AddChild(exit);

		nextBubbleAt = Time.GetTicksMsec() + VictoryScreenLayout.FirstBubbleDelay + Jitter();
	}

	private static ulong Jitter() => (ulong)(Random.Shared.Next(2) * VictoryScreenLayout.BubbleJitter);

	public override void _Process(double delta) {
		ulong now = Time.GetTicksMsec();
		if (now >= nextBubbleAt) {
			ShowNextRemark(now);
		}
		if (bubble != null) {
			ulong shown = now - bubbleShownAt;
			if (shown >= VictoryScreenLayout.BubbleFadeEnd) {
				bubble.Visible = false;
			} else if (shown > VictoryScreenLayout.BubbleShown) {
				float fade = (shown - VictoryScreenLayout.BubbleShown) / (float)(VictoryScreenLayout.BubbleFadeEnd - VictoryScreenLayout.BubbleShown);
				bubble.Modulate = new Color(1, 1, 1, 1 - fade);
			}
		}
	}

	// One of the other leaders, at random, says one of their lines.
	private void ShowNextRemark(ulong now) {
		nextBubbleAt = now + VictoryScreenLayout.BubbleInterval + Jitter();
		bubble?.QueueFree();
		bubble = null;

		List<Head> speakers = heads.Where(h => h.Place.Slot > 0 && h.Remarks.Count > 0).ToList();
		if (speakers.Count == 0) {
			return;
		}
		Head speaker = speakers[Random.Shared.Next(speakers.Count)];
		string remark = speaker.Remarks[Random.Shared.Next(speaker.Remarks.Count)];
		// In the tree before it's filled in, so its text is measured in the
		// theme's font.
		bubble = new Control { MouseFilter = MouseFilterEnum.Ignore };
		board.AddChild(bubble);
		FillBubble(bubble, remark, VictoryScreenLayout.Place(screen, speaker.Place.Slot));
		bubbleShownAt = now;
	}

	// The bubble is centered on the speaker's head's top left corner, kept
	// on the screen (FUN_005dd960).
	private void FillBubble(Control box, string remark, (int x, int y) place) {
		const int width = 200;
		const int fallbackLine = 16;

		int inset = VictoryScreenLayout.BubbleTextInset;
		Label label = new() {
			Text = remark,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Position = new Vector2(inset, inset),
			Size = new Vector2(width - 2 * inset, 0),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeColorOverride("font_color", Colors.Black);
		label.AddThemeFontSizeOverride("font_size", 13);
		box.AddChild(label);
		int textHeight = Math.Max(1, label.GetLineCount()) * label.GetLineHeight();

		int topHeight = bubbleTop?.GetHeight() ?? inset;
		int middleHeight = bubbleMiddle?.GetHeight() ?? fallbackLine;
		int bottomHeight = bubbleBottom?.GetHeight() ?? inset;
		int middles = Math.Max(1, (textHeight + inset - topHeight + middleHeight - 1) / middleHeight);
		int height = topHeight + middles * middleHeight + bottomHeight;

		if (bubbleTop != null && bubbleMiddle != null && bubbleBottom != null) {
			box.AddChild(new TextureRect { Texture = bubbleTop, MouseFilter = MouseFilterEnum.Ignore });
			for (int i = 0; i < middles; i++) {
				box.AddChild(new TextureRect {
					Texture = bubbleMiddle,
					Position = new Vector2(0, topHeight + i * middleHeight),
					MouseFilter = MouseFilterEnum.Ignore,
				});
			}
			box.AddChild(new TextureRect {
				Texture = bubbleBottom,
				Position = new Vector2(0, topHeight + middles * middleHeight),
				MouseFilter = MouseFilterEnum.Ignore,
			});
		} else {
			// Without PTW's bubble art, a plain rounded box.
			Panel panel = new() { Size = new Vector2(width, height - bottomHeight / 2), MouseFilter = MouseFilterEnum.Ignore };
			StyleBoxFlat style = new() { BgColor = Color.Color8(233, 224, 198), BorderColor = Color.Color8(120, 100, 80) };
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(10);
			panel.AddThemeStyleboxOverride("panel", style);
			box.AddChild(panel);
		}
		box.MoveChild(label, -1);

		int x = place.x - width / 2;
		int y = Math.Max(0, place.y - height / 2);
		box.Position = new Vector2(x, y);
	}

	public override void _Input(InputEvent @event) {
		if (closed || @event is not InputEventKey { Pressed: true } key) {
			return;
		}
		// The screen takes every key, so none reach the map underneath.
		GetViewport().SetInputAsHandled();
		if (key.Keycode == Key.Escape) {
			Close();
		} else if (key.Keycode is Key.Enter or Key.KpEnter) {
			nextBubbleAt = Time.GetTicksMsec();
		}
	}

	private void Close() {
		if (closed) {
			return;
		}
		closed = true;
		Closed?.Invoke();
		QueueFree();
	}
}

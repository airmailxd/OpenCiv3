using System;
using System.Linq;
using C7Engine;
using C7Engine.Network;
using Godot;
using Serilog;

// The game's chat line, bottom left over the map in a LAN or server game
// (owner's design, 2026-11-25; see GameChat): the lines heard, which fade
// when it's quiet, over a dark field to type in, the look of the lobby's
// chat line. Enter opens it when nothing else has the keys (Game asks
// TryOpen), Enter sends and closes it, Esc closes it. What it says and does
// is the game's ChatSession, which the lobby's chat line shares: the lines
// said there show here at the start, the channel open sticks (/team alone
// makes Team the channel, /all All), direct messages to any account (/w, /r)
// when signed in to the game server, and /ignore.
public partial class GameChatBox : VBoxContainer {
	private static readonly ILogger log = Log.ForContext<GameChatBox>();

	private const int Width = 470;
	private const int FontSize = 14;
	// Seconds a line stays before the log fades, and the fade.
	private const double ShowFor = 12;
	private const double FadeFor = 1.5;

	private static readonly Color LogBackground = new(0.07f, 0.055f, 0.04f, 0.6f);

	private RichTextLabel lines;
	private PanelContainer logPanel;
	private HBoxContainer inputRow;
	private Label channelLabel;
	private LineEdit input;

	private ChatSession chat;
	private double quietFor;
	private ulong keysTakenFrame = ulong.MaxValue;

	public bool IsOpen => inputRow?.Visible == true;

	// Whether the chat took this frame's keys, opening or closing, so that
	// the key that did it isn't taken as a command too (Enter ends the turn
	// and Esc opens the menu, as actions read every frame).
	public bool TookKeysThisFrame => keysTakenFrame == Engine.GetProcessFrames();

	public override void _Ready() {
		MouseFilter = MouseFilterEnum.Ignore;
		AddThemeConstantOverride("separation", 3);
		SetAnchorsPreset(LayoutPreset.BottomLeft);
		GrowVertical = GrowDirection.Begin;
		OffsetLeft = 6;
		OffsetRight = 6 + Width;
		// Above the minimap and its frame's knobs.
		OffsetBottom = -152;
		OffsetTop = OffsetBottom;
		CustomMinimumSize = new Vector2(Width, 0);

		logPanel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		logPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
			BgColor = LogBackground,
			ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4,
			CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
		});
		AddChild(logPanel);
		lines = new RichTextLabel {
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			ScrollFollowing = true,
			MouseFilter = MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(Width - 16, 0),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		lines.AddThemeFontSizeOverride("normal_font_size", FontSize);
		lines.AddThemeColorOverride("default_color", ChatSession.OnDark(ChatSession.Kind.All));
		lines.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
		lines.AddThemeConstantOverride("shadow_offset_x", 1);
		lines.AddThemeConstantOverride("shadow_offset_y", 1);
		logPanel.AddChild(lines);

		inputRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		inputRow.AddThemeConstantOverride("separation", 6);
		AddChild(inputRow);
		channelLabel = new Label { VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(58, 0) };
		channelLabel.AddThemeFontSizeOverride("font_size", FontSize);
		channelLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
		inputRow.AddChild(channelLabel);
		input = SetupStyle.MakeDarkField();
		input.MaxLength = GameChat.MaxLength + 40;
		input.PlaceholderText = "Enter sends, Esc closes. /team, /all, /w name";
		input.TextSubmitted += Submit;
		input.GuiInput += OnInputKey;
		inputRow.AddChild(input);

		chat = ChatSession.Current;
		if (chat != null) {
			chat.LineAdded += OnLineAdded;
		}
		ShowChannel();
		Close();
		// The lines said in the lobby show for a while, then fade.
		logPanel.Visible = chat?.Lines.Count > 0;
		if (Array.Find(OS.GetCmdlineUserArgs(), a => a.StartsWith("--chat-shot=")) is string shot) {
			TakeShot(shot["--chat-shot=".Length..]);
		}
	}

	public override void _ExitTree() {
		if (chat != null) {
			chat.LineAdded -= OnLineAdded;
		}
	}

	// A developer option, for a picture of the chat with a few lines: with
	// --chat-shot=<png> after "--" (and --lan-host=<save> to be in a LAN
	// game), the host says a line, others are made up as if heard, the line
	// opens with something typed, and the picture is saved; then the game
	// quits.
	private async void TakeShot(string path) {
		SceneTree tree = GetTree();
		await ToSignal(tree.CreateTimer(4), SceneTreeTimer.SignalName.Timeout);
		if (chat == null) {
			log.Warning("No chat to take a picture of: not in a LAN game");
			tree.Quit();
			return;
		}
		LanSession.Host?.Say(ChatChannel.All, "Good luck everyone, have fun");
		chat.Hear(new ChatLineInfo(ChatChannel.All, "Ann", "gl hf! no early rushes please"));
		chat.Hear(new ChatLineInfo(ChatChannel.Team, "Bob", "Their capital only has two spearmen, I can take it in 3 turns"));
		chat.Show(GameChat.FormatWhisper("carol", "are you coming to Rome after this game?", fromMe: false), ChatSession.Kind.Whisper);
		chat.Show(GameChat.FormatWhisper("carol", "yes, after turn 100", fromMe: true), ChatSession.Kind.Whisper);
		chat.Channel = ChatChannel.Team;
		ShowChannel();
		Open();
		input.Text = "Sending my knights to help, hold the river";
		input.CaretColumn = input.Text.Length;
		await ToSignal(tree.CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error saved = GetViewport().GetTexture().GetImage().SavePng(path);
		log.Information("Saved the chat's picture to {Path}: {Result}", path, saved);
		tree.Quit();
	}

	public override void _Process(double delta) {
		chat?.Pump();
		if (IsOpen || chat == null || chat.Lines.Count == 0) {
			return;
		}
		quietFor += delta;
		float alpha = quietFor < ShowFor ? 1 : (float)Math.Max(0, 1 - (quietFor - ShowFor) / FadeFor);
		logPanel.Modulate = new Color(1, 1, 1, alpha);
		logPanel.Visible = alpha > 0;
	}

	// Opens the chat line, if the key is Enter (Game has made sure nothing
	// else has the keys). Returns whether it did.
	public bool TryOpen(InputEventKey key) {
		if (!key.Pressed || key.Echo || key.Keycode is not (Key.Enter or Key.KpEnter)
			|| key.ShiftPressed || key.CtrlPressed || key.AltPressed || key.MetaPressed) {
			return false;
		}
		// Open still, with the keys gone elsewhere: they come back.
		Open();
		return true;
	}

	public void Open() {
		keysTakenFrame = Engine.GetProcessFrames();
		inputRow.Visible = true;
		logPanel.Visible = chat?.Lines.Count > 0;
		logPanel.Modulate = Colors.White;
		Redraw();
		input.GrabFocus();
	}

	private void Close() {
		inputRow.Visible = false;
		input.Text = "";
		if (input.HasFocus()) {
			input.ReleaseFocus();
		}
		quietFor = 0;
		Redraw();
	}

	private void OnInputKey(InputEvent e) {
		if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) {
			keysTakenFrame = Engine.GetProcessFrames();
			Close();
			input.AcceptEvent();
		}
	}

	private void Submit(string typed) {
		keysTakenFrame = Engine.GetProcessFrames();
		Close();
		if (chat == null) {
			return;
		}
		bool keepOpen = chat.Submit(typed);
		ShowChannel();
		if (keepOpen) {
			// Straight back to typing on the channel opened.
			Open();
		}
	}

	private void ShowChannel() {
		ChatChannel channel = chat?.Channel ?? ChatChannel.All;
		channelLabel.Text = $"[{GameChat.ChannelName(channel)}]";
		channelLabel.AddThemeColorOverride("font_color",
			ChatSession.OnDark(channel == ChatChannel.Team ? ChatSession.Kind.Team : ChatSession.Kind.All));
	}

	// A line added to the log shows it again.
	private void OnLineAdded(ChatSession.Line line) {
		Redraw();
		quietFor = 0;
		logPanel.Visible = true;
		logPanel.Modulate = Colors.White;
	}

	// The last few lines, or more while the line is open.
	private void Redraw() {
		int count = IsOpen ? 14 : 8;
		if (lines == null || chat == null) {
			return;
		}
		lines.Clear();
		bool first = true;
		foreach (ChatSession.Line line in chat.Lines.Skip(Math.Max(0, chat.Lines.Count - count))) {
			if (!first) {
				lines.Newline();
			}
			first = false;
			lines.PushColor(ChatSession.OnDark(line.kind));
			lines.AddText(line.text);
			lines.Pop();
		}
	}
}

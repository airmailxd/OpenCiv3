using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Network;
using C7GameServer;
using Godot;
using Serilog;

// The game's chat line, bottom left over the map in a LAN or server game
// (owner's design, 2026-11-25; see GameChat): the lines heard, which fade
// when it's quiet, over a dark field to type in, the look of the lobby's
// chat line. Enter opens it when nothing else has the keys (Game asks
// TryOpen), Enter sends and closes it, Esc closes it. The channel open
// sticks: /team alone makes Team the channel, /all All. Signed in to the
// game server, direct messages to any account (/w, /r) show here too, in
// their own colour, and /ignore hides a player's lines.
public partial class GameChatBox : VBoxContainer {
	private static readonly ILogger log = Log.ForContext<GameChatBox>();

	private const int Width = 470;
	private const int MaxLines = 100;
	private const int FontSize = 14;
	// Seconds a line stays before the log fades, and the fade.
	private const double ShowFor = 12;
	private const double FadeFor = 1.5;

	private static readonly Color AllColor = new(0.97f, 0.94f, 0.84f);
	private static readonly Color TeamColor = new(0.55f, 0.82f, 1f);
	private static readonly Color WhisperColor = new(0.96f, 0.6f, 0.86f);
	private static readonly Color NoticeColor = new(1f, 0.83f, 0.42f);
	private static readonly Color LogBackground = new(0.07f, 0.055f, 0.04f, 0.6f);

	private RichTextLabel lines;
	private PanelContainer logPanel;
	private HBoxContainer inputRow;
	private Label channelLabel;
	private LineEdit input;

	private ChatChannel channel = ChatChannel.All;
	private double quietFor;
	private ulong keysTakenFrame = ulong.MaxValue;
	private readonly List<(string text, Color color)> shown = [];

	// Ignored by name for this game, when not signed in to the server.
	private readonly HashSet<string> ignoredHere = new(StringComparer.OrdinalIgnoreCase);

	private DirectMessages directMessages;

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
		OffsetBottom = -112;
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
		lines.AddThemeColorOverride("default_color", AllColor);
		lines.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
		lines.AddThemeConstantOverride("shadow_offset_x", 1);
		lines.AddThemeConstantOverride("shadow_offset_y", 1);
		logPanel.AddChild(lines);

		inputRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		inputRow.AddThemeConstantOverride("separation", 6);
		AddChild(inputRow);
		channelLabel = new Label { VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(46, 0) };
		channelLabel.AddThemeFontSizeOverride("font_size", FontSize);
		channelLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
		inputRow.AddChild(channelLabel);
		input = SetupStyle.MakeDarkField();
		input.MaxLength = GameChat.MaxLength + 40;
		input.PlaceholderText = "Enter sends, Esc closes. /team, /all, /w name";
		input.TextSubmitted += Submit;
		input.GuiInput += OnInputKey;
		inputRow.AddChild(input);

		ShowChannel();
		Close();
		logPanel.Visible = false;
		HookUp();
		if (Array.Find(OS.GetCmdlineUserArgs(), a => a.StartsWith("--chat-shot=")) is string shot) {
			TakeShot(shot["--chat-shot=".Length..]);
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
		LanSession.Host?.Say(ChatChannel.All, "Good luck everyone, have fun");
		Hear(new ChatLineInfo(ChatChannel.All, "Ann", "gl hf! no early rushes please"));
		Hear(new ChatLineInfo(ChatChannel.Team, "Bob", "Their capital only has two spearmen, I can take it in 3 turns"));
		Show(GameChat.FormatWhisper("carol", "are you coming to Rome after this game?", fromMe: false), WhisperColor);
		Show(GameChat.FormatWhisper("carol", "yes, after turn 100", fromMe: true), WhisperColor);
		channel = ChatChannel.Team;
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

	// Listens to the game's host or client, and to the server's messages if
	// signed in.
	private void HookUp() {
		if (LanSession.Host is LanHost host) {
			host.ChatReceived += Hear;
		} else if (LanSession.Client is LanClient client) {
			client.ChatLineReceived = Hear;
		}
		try {
			if (GameServerSettings.SavedSession(GameServerSettings.Url) != null) {
				directMessages = DirectMessages.For(ServerGames.Client());
				directMessages.Start();
			}
		} catch (Exception e) {
			log.Information("No direct messages in this game: {Error}", e.Message);
			directMessages = null;
		}
	}

	public override void _ExitTree() {
		if (LanSession.Host is LanHost host) {
			host.ChatReceived -= Hear;
		} else if (LanSession.Client is LanClient client && client.ChatLineReceived == Hear) {
			client.ChatLineReceived = null;
		}
		directMessages?.Dispose();
		directMessages = null;
	}

	public override void _Process(double delta) {
		while (directMessages != null && directMessages.TryTake(out DirectMessageEvent e)) {
			Show(e.Text, e.Kind == DirectMessageEventKind.Message ? WhisperColor : NoticeColor);
		}
		if (IsOpen || shown.Count == 0) {
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
		logPanel.Visible = shown.Count > 0;
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
		Act(GameChat.Parse(typed, channel));
	}

	private void Act(ChatCommand command) {
		switch (command.Kind) {
			case ChatCommandKind.Say:
				channel = command.Channel;
				ShowChannel();
				Say(command.Channel, command.Text);
				break;
			case ChatCommandKind.OpenChannel:
				channel = command.Channel;
				ShowChannel();
				// Straight back to typing on it.
				Open();
				break;
			case ChatCommandKind.Whisper:
				if (directMessages == null) {
					Show(GameChat.NeedsServer, NoticeColor);
				} else {
					_ = directMessages.SendAsync(command.Name, command.Text);
				}
				break;
			case ChatCommandKind.Reply:
				if (directMessages == null) {
					Show(GameChat.NeedsServer, NoticeColor);
				} else {
					_ = directMessages.ReplyAsync(command.Text);
				}
				break;
			case ChatCommandKind.Ignore:
			case ChatCommandKind.Unignore:
				Ignore(command.Name, command.Kind == ChatCommandKind.Ignore);
				break;
			case ChatCommandKind.Unknown:
				Show(GameChat.Unknown(command.Text), NoticeColor);
				break;
		}
	}

	private void Say(ChatChannel on, string text) {
		if (LanSession.IsSpectator) {
			Show("Spectators can't talk in the game.", NoticeColor);
		} else if (LanSession.Host is LanHost host) {
			host.Say(on, text);
		} else if (LanSession.Client is LanClient client) {
			// As the player at the screen, if several play at this machine.
			ID speaker = client.PlayerIDs.Contains(EngineStorage.uiControllerID) ? EngineStorage.uiControllerID : null;
			client.Say(on, text, speaker);
		}
	}

	// Signed in, ignoring is the account's, kept on the server; otherwise it
	// hides the player's lines in this game only.
	private void Ignore(string name, bool ignore) {
		if (directMessages != null) {
			_ = directMessages.IgnoreAsync(name, ignore);
			return;
		}
		if (string.IsNullOrWhiteSpace(name)) {
			Show(ignore ? "Who to ignore? Type /ignore and their name." : "Who to stop ignoring? Type /unignore and their name.", NoticeColor);
			return;
		}
		name = name.Trim();
		if (ignore) {
			ignoredHere.Add(name);
			Show($"You're ignoring {name} in this game.", NoticeColor);
		} else {
			ignoredHere.Remove(name);
			Show($"You're no longer ignoring {name}.", NoticeColor);
		}
	}

	private bool IsIgnored(string name) {
		return name != null && (ignoredHere.Contains(name) || directMessages?.IsIgnored(name) == true);
	}

	private void Hear(ChatLineInfo line) {
		if (line.from != null && IsIgnored(line.from)) {
			return;
		}
		Color color = line.from == null ? NoticeColor : line.channel == ChatChannel.Team ? TeamColor : AllColor;
		Show(GameChat.Format(line), color);
	}

	private void ShowChannel() {
		channelLabel.Text = $"[{GameChat.ChannelName(channel)}]";
		channelLabel.AddThemeColorOverride("font_color", channel == ChatChannel.Team ? TeamColor : AllColor);
	}

	// Adds a line to the log, and shows it again.
	public void Show(string text, Color color) {
		shown.Add((text, color));
		if (shown.Count > MaxLines) {
			shown.RemoveRange(0, shown.Count - MaxLines);
		}
		Redraw();
		quietFor = 0;
		logPanel.Visible = true;
		logPanel.Modulate = Colors.White;
	}

	// The last few lines, or more while the line is open.
	private void Redraw() {
		int count = IsOpen ? 14 : 8;
		if (lines == null) {
			return;
		}
		lines.Clear();
		bool first = true;
		foreach ((string text, Color color) in shown.Skip(Math.Max(0, shown.Count - count))) {
			if (!first) {
				lines.Newline();
			}
			first = false;
			lines.PushColor(color);
			lines.AddText(text);
			lines.Pop();
		}
	}
}

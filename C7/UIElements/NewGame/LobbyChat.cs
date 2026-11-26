using System;
using C7Engine.Network;
using Godot;
using Serilog;

// The lobby's chat line, on the Multiplayer Setup screen's bottom border:
// the game's ChatSession, as the game's chat line (GameChatBox) has it, with
// the same commands. Its lines go into the screen's messages, which keep
// them, and carry into the game. It works while the screen hosts or has
// joined a game; without one (hotseat, by email, a server game being set up)
// there's nobody to talk to, and spectators don't talk, as in the game.
public partial class LobbyChat : HBoxContainer {
	private static readonly ILogger log = Log.ForContext<LobbyChat>();

	// The screen's messages, which the lines are added to.
	public RichTextLabel Messages;

	private Label channelLabel;
	private LineEdit input;
	private ChatSession chat;
	private bool? spectating;

	public override void _Ready() {
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		AddThemeConstantOverride("separation", 6);

		channelLabel = new Label { VerticalAlignment = VerticalAlignment.Center, Visible = false };
		channelLabel.AddThemeFontSizeOverride("font_size", 14);
		channelLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
		AddChild(channelLabel);

		input = SetupStyle.MakeDarkField();
		input.MaxLength = GameChat.MaxLength + 40;
		input.KeepEditingOnTextSubmit = true;
		input.TextSubmitted += Submit;
		input.GuiInput += OnInputKey;
		AddChild(input);
		Follow(null);
		if (Array.Find(OS.GetCmdlineUserArgs(), a => a.StartsWith("--lobby-chat-shot=")) is string shot) {
			TakeShot(shot["--lobby-chat-shot=".Length..]);
		}
	}

	// A developer option, for a picture of the lobby's chat: with
	// --lobby-chat-shot=<png> after "--", and --lan-host=<save> of a game
	// with three or more human players to be hosting its lobby, two players
	// join from this process and talk, the host answers, a line is typed,
	// and the picture is saved; then the game quits. One seat stays open, so
	// that the game doesn't start.
	private async void TakeShot(string path) {
		SceneTree tree = GetTree();
		await ToSignal(tree.CreateTimer(3), SceneTreeTimer.SignalName.Timeout);
		LanHost host = LanSession.Host;
		if (host == null || host.Seats.Count < 3) {
			log.Warning("No lobby to take a picture of: --lan-host=<save> needs three or more human players");
			tree.Quit();
			return;
		}
		// Connecting waits for the host, which this thread polls.
		System.Threading.Tasks.Task<LanClient> joining = System.Threading.Tasks.Task.Run(() => (
			LanClient.Connect("127.0.0.1", host.Port, "Ann")));
		while (!joining.IsCompleted) {
			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
		LanClient ann = joining.Result;
		joining = System.Threading.Tasks.Task.Run(() => LanClient.Connect("127.0.0.1", host.Port, "Bob"));
		while (!joining.IsCompleted) {
			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
		LanClient bob = joining.Result;
		LanClient[] guests = [ann, bob];
		async System.Threading.Tasks.Task Pump(double seconds) {
			ulong until = Time.GetTicksMsec() + (ulong)(seconds * 1000);
			while (Time.GetTicksMsec() < until) {
				foreach (LanClient guest in guests) {
					guest.Poll();
				}
				await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
		}
		await Pump(1);
		ann.ClaimSeat(host.Seats[0].playerID);
		bob.ClaimSeat(host.Seats[1].playerID);
		await Pump(1);
		ann.Say(ChatChannel.All, "hi all, gl hf");
		await Pump(0.4);
		bob.Say(ChatChannel.All, "Can we keep it to no early rushes?");
		await Pump(0.4);
		host.Say(ChatChannel.All, "Fine by me. We start once Carol is in.");
		await Pump(0.4);
		chat?.Submit("/team");
		chat?.Submit("anyone on my side?");
		chat?.Show(GameChat.FormatWhisper("carol", "joining in a minute", fromMe: false), ChatSession.Kind.Whisper);
		ann.Say(ChatChannel.All, "Persia again, Bob?");
		await Pump(0.6);
		if (chat != null) {
			chat.Channel = ChatChannel.All;
		}
		ShowChannel();
		input.GrabFocus();
		input.Text = "Carol says a minute";
		input.CaretColumn = input.Text.Length;
		await Pump(1);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error saved = GetViewport().GetTexture().GetImage().SavePng(path);
		log.Information("Saved the lobby chat's picture to {Path}: {Result}", path, saved);
		foreach (LanClient guest in guests) {
			guest.Dispose();
		}
		tree.Quit();
	}

	public override void _ExitTree() {
		if (chat != null) {
			chat.LineAdded -= OnLineAdded;
		}
	}

	public override void _Process(double delta) {
		ChatSession now = ChatSession.Current;
		if (now != chat || (now != null && spectating != LanSession.IsSpectator)) {
			Follow(now);
		}
		chat?.Pump();
	}

	// Listens to the chat of the game the screen is in now, if any.
	private void Follow(ChatSession now) {
		if (chat != null && chat != now) {
			chat.LineAdded -= OnLineAdded;
		}
		if (now != null && now != chat) {
			now.LineAdded += OnLineAdded;
			// Said before this screen came up.
			foreach (ChatSession.Line line in now.Lines) {
				OnLineAdded(line);
			}
		}
		chat = now;
		spectating = LanSession.IsSpectator;
		bool canTalk = chat != null && spectating != true;
		input.Editable = canTalk;
		channelLabel.Visible = canTalk;
		if (!canTalk && input.HasFocus()) {
			input.ReleaseFocus();
		}
		input.PlaceholderText = chat == null ? "No one to chat with here yet."
			: spectating == true ? "Spectators don't chat."
			: "Type to chat, Enter sends. /team, /all, /w name";
		input.TooltipText = chat == null ? "Chat opens once players can join: in a LAN game's lobby, or in the game."
			: "/all to everyone, /team to your team, /w name to message an account, /r to reply, /ignore name.";
		ShowChannel();
	}

	private void Submit(string typed) {
		input.Text = "";
		if (chat == null || spectating == true) {
			return;
		}
		chat.Submit(typed);
		ShowChannel();
	}

	// Esc leaves the line, rather than the screen, which it also closes.
	private void OnInputKey(InputEvent e) {
		if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) {
			input.ReleaseFocus();
			input.AcceptEvent();
		}
	}

	private void ShowChannel() {
		ChatChannel channel = chat?.Channel ?? ChatChannel.All;
		channelLabel.Text = $"[{GameChat.ChannelName(channel)}]";
		channelLabel.AddThemeColorOverride("font_color",
			ChatSession.OnDark(channel == ChatChannel.Team ? ChatSession.Kind.Team : ChatSession.Kind.All));
	}

	private void OnLineAdded(ChatSession.Line line) {
		if (Messages != null && IsInstanceValid(Messages)) {
			SetupStyle.AddMessage(Messages, line.text, ChatSession.OnParchment(line.kind));
		}
	}
}

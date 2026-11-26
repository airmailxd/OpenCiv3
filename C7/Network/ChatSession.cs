using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.Network;
using C7GameData;
using Godot;
using Serilog;

// The chat of the LAN or server game this machine is in (see GameChat): what
// the lobby's chat line and the game's (GameChatBox) both show and do. One
// lives as long as LanSession's host or client, so the lines said in the
// lobby are still there in the game, as are the channel open and those
// ignored. It hears the game's lines from the host or client, and direct
// messages from the game server when signed in to it.
public sealed class ChatSession : IDisposable {
	private static readonly ILogger log = Log.ForContext<ChatSession>();

	public const int MaxLines = 100;

	public enum Kind { All, Team, Whisper, Notice }

	public readonly record struct Line(string text, Kind kind);

	// The lines' colours over the game's map, as the game's chat line has
	// them, and the same, darker, on the lobby's parchment.
	public static Color OnDark(Kind kind) => kind switch {
		Kind.Team => new Color(0.55f, 0.82f, 1f),
		Kind.Whisper => new Color(0.96f, 0.6f, 0.86f),
		Kind.Notice => new Color(1f, 0.83f, 0.42f),
		_ => new Color(0.97f, 0.94f, 0.84f),
	};

	public static Color OnParchment(Kind kind) => kind switch {
		Kind.Team => new Color(0.08f, 0.3f, 0.6f),
		Kind.Whisper => new Color(0.6f, 0.12f, 0.45f),
		Kind.Notice => new Color(0.55f, 0.33f, 0.02f),
		_ => SetupStyle.Ink,
	};

	private static ChatSession current;

	// The chat of the game LanSession is in now, or null outside one.
	public static ChatSession Current {
		get {
			object connection = (object)LanSession.Host ?? LanSession.Client;
			if (connection == null) {
				End();
				return null;
			}
			if (current?.connection != connection) {
				End();
				current = new ChatSession(connection);
			}
			return current;
		}
	}

	// Called as LanSession ends.
	public static void End() {
		current?.Dispose();
		current = null;
	}

	private readonly object connection;
	private readonly List<Line> lines = [];
	private readonly HashSet<string> ignoredHere = new(StringComparer.OrdinalIgnoreCase);
	private DirectMessages directMessages;

	public IReadOnlyList<Line> Lines => lines;
	public ChatChannel Channel { get; set; } = ChatChannel.All;

	// Each line as it's added.
	public event Action<Line> LineAdded;

	private ChatSession(object connection) {
		this.connection = connection;
		if (connection is LanHost host) {
			host.ChatReceived += Hear;
		} else if (connection is LanClient client) {
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

	public void Dispose() {
		if (connection is LanHost host) {
			host.ChatReceived -= Hear;
		} else if (connection is LanClient client && client.ChatLineReceived == Hear) {
			client.ChatLineReceived = null;
		}
		directMessages?.Dispose();
		directMessages = null;
		LineAdded = null;
	}

	// Shows the direct messages and notices come from the game server; call
	// each frame.
	public void Pump() {
		while (directMessages != null && directMessages.TryTake(out DirectMessageEvent e)) {
			Show(e.Text, e.Kind == DirectMessageEventKind.Message ? Kind.Whisper : Kind.Notice);
		}
	}

	// Adds a line to the log.
	public void Show(string text, Kind kind) {
		Line line = new(text, kind);
		lines.Add(line);
		if (lines.Count > MaxLines) {
			lines.RemoveRange(0, lines.Count - MaxLines);
		}
		LineAdded?.Invoke(line);
	}

	// A line of the game's chat, unless its speaker is ignored.
	public void Hear(ChatLineInfo line) {
		if (line.from != null && IsIgnored(line.from)) {
			return;
		}
		Show(GameChat.Format(line), line.from == null ? Kind.Notice : line.channel == ChatChannel.Team ? Kind.Team : Kind.All);
	}

	// Does what was typed in a chat line. Returns true when it only opened
	// a channel, so the line stays open to type on it.
	public bool Submit(string typed) {
		ChatCommand command = GameChat.Parse(typed, Channel);
		switch (command.Kind) {
			case ChatCommandKind.Say:
				Channel = command.Channel;
				Say(command.Channel, command.Text);
				break;
			case ChatCommandKind.OpenChannel:
				Channel = command.Channel;
				return true;
			case ChatCommandKind.Whisper:
				if (directMessages == null) {
					Show(GameChat.NeedsServer, Kind.Notice);
				} else {
					_ = directMessages.SendAsync(command.Name, command.Text);
				}
				break;
			case ChatCommandKind.Reply:
				if (directMessages == null) {
					Show(GameChat.NeedsServer, Kind.Notice);
				} else {
					_ = directMessages.ReplyAsync(command.Text);
				}
				break;
			case ChatCommandKind.Ignore:
			case ChatCommandKind.Unignore:
				Ignore(command.Name, command.Kind == ChatCommandKind.Ignore);
				break;
			case ChatCommandKind.Unknown:
				Show(GameChat.Unknown(command.Text), Kind.Notice);
				break;
		}
		return false;
	}

	public void Say(ChatChannel on, string text) {
		if (connection is LanClient { IsSpectator: true }) {
			Show("Spectators can't chat.", Kind.Notice);
		} else if (connection is LanHost host) {
			host.Say(on, text);
		} else if (connection is LanClient client) {
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
			Show(ignore ? "Who to ignore? Type /ignore and their name." : "Who to stop ignoring? Type /unignore and their name.", Kind.Notice);
			return;
		}
		name = name.Trim();
		if (ignore) {
			ignoredHere.Add(name);
			Show($"You're ignoring {name} in this game.", Kind.Notice);
		} else {
			ignoredHere.Remove(name);
			Show($"You're no longer ignoring {name}.", Kind.Notice);
		}
	}

	private bool IsIgnored(string name) {
		return name != null && (ignoredHere.Contains(name) || directMessages?.IsIgnored(name) == true);
	}
}

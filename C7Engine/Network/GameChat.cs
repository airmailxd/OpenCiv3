using System;
using System.Diagnostics;
using System.Text;

namespace C7Engine.Network;

// The game's chat (owner's design, 2026-11-25; see CHAT.md in Roma): in a
// LAN or server game, a player talks to every human player in it (All) or
// to those on their team (Team), over the game's own connection, through
// the host, which passes each line on to the right seats and never to the
// computer's. Direct messages to any account on the game server share the
// chat line (see DirectMessages). This is what the host and the chat line
// both need: tidying what's typed, reading commands, and the lines' words.
public static class GameChat {
	// The most characters a line or a direct message keeps, as the game
	// server does.
	public const int MaxLength = 300;

	// What players are told by the host.
	public const string NotOnTeam = "You aren't on a team.";
	public const string TooFast = "You're sending too fast.";
	public const string NeedsServer = "Messages need you to be signed in to the server.";

	// What's typed as it's sent: without control characters, trimmed, and
	// no longer than MaxLength; null if nothing is left.
	public static string Tidy(string text) {
		if (text == null) {
			return null;
		}
		StringBuilder kept = new(Math.Min(text.Length, MaxLength + 8));
		foreach (char c in text) {
			// Line breaks and tabs are spaces; other control characters go.
			if (c is '\n' or '\r' or '\t') {
				kept.Append(' ');
			} else if (!char.IsControl(c)) {
				kept.Append(c);
			}
		}
		string tidy = kept.ToString().Trim();
		if (tidy.Length > MaxLength) {
			tidy = tidy[..MaxLength].TrimEnd();
			// Not half a surrogate pair at the end.
			if (tidy.Length > 0 && char.IsHighSurrogate(tidy[^1])) {
				tidy = tidy[..^1];
			}
		}
		return tidy.Length == 0 ? null : tidy;
	}

	public static string ChannelName(ChatChannel channel) => channel == ChatChannel.Team ? "Team" : "All";

	// A line as the chat log shows it.
	public static string Format(ChatLineInfo line) {
		return line.from == null ? line.text : $"[{ChannelName(line.channel)}] {line.from}: {line.text}";
	}

	public static string FormatWhisper(string name, string text, bool fromMe) {
		return fromMe ? $"[To {name}]: {text}" : $"[From {name}]: {text}";
	}

	// Reads a line typed in the chat line. Plain text is said on the channel
	// open now; a command says who to:
	//   /all text, /a text      to everyone (and All stays open)
	//   /team text, /t text     to the team (and Team stays open)
	//   /all, /team alone       open the channel for what's typed next
	//   /w name text            a direct message (also /whisper, /msg)
	//   /r text                 to whoever last messaged you
	//   /ignore name, /unignore name
	public static ChatCommand Parse(string typed, ChatChannel open) {
		string line = typed?.Trim() ?? "";
		if (line.Length == 0) {
			return new ChatCommand(ChatCommandKind.Nothing);
		}
		if (line[0] != '/') {
			return new ChatCommand(ChatCommandKind.Say, open, Text: line);
		}
		int space = line.IndexOf(' ');
		string word = (space < 0 ? line[1..] : line[1..space]).ToLowerInvariant();
		string rest = space < 0 ? "" : line[(space + 1)..].Trim();
		switch (word) {
			case "all":
			case "a":
			case "team":
			case "t":
				ChatChannel channel = word is "team" or "t" ? ChatChannel.Team : ChatChannel.All;
				return rest.Length == 0 ? new ChatCommand(ChatCommandKind.OpenChannel, channel)
					: new ChatCommand(ChatCommandKind.Say, channel, Text: rest);
			case "w":
			case "whisper":
			case "msg":
				(string name, string text) = SplitName(rest);
				return new ChatCommand(ChatCommandKind.Whisper, open, name, text);
			case "r":
			case "reply":
				return new ChatCommand(ChatCommandKind.Reply, open, Text: rest);
			case "ignore":
				return new ChatCommand(ChatCommandKind.Ignore, open, SplitName(rest).name);
			case "unignore":
				return new ChatCommand(ChatCommandKind.Unignore, open, SplitName(rest).name);
			default:
				return new ChatCommand(ChatCommandKind.Unknown, open, Text: "/" + word);
		}
	}

	private static (string name, string text) SplitName(string rest) {
		if (rest.Length == 0) {
			return (null, null);
		}
		int space = rest.IndexOf(' ');
		return space < 0 ? (rest, null) : (rest[..space], rest[(space + 1)..].Trim());
	}

	public static string Unknown(string command) {
		return $"{command} isn't a command. Try /all, /team, /w, /r, /ignore or /unignore.";
	}

	// How many lines one speaker may say: one a second, and five at once,
	// as the game server allows direct messages.
	public sealed class Bucket {
		private const double PerSecond = 1;
		private const double Burst = 5;
		private double lines = Burst;
		private long refilledAt = Stopwatch.GetTimestamp();

		public bool Take() {
			long now = Stopwatch.GetTimestamp();
			lines = Math.Min(Burst, lines + Stopwatch.GetElapsedTime(refilledAt, now).TotalSeconds * PerSecond);
			refilledAt = now;
			if (lines < 1) {
				return false;
			}
			lines -= 1;
			return true;
		}
	}
}

public enum ChatCommandKind {
	// Nothing typed.
	Nothing,
	// Text to say on Channel.
	Say,
	// Channel is open from now on.
	OpenChannel,
	// A direct message to Name.
	Whisper,
	// A direct message to whoever last sent one.
	Reply,
	Ignore,
	Unignore,
	// Text is the command that isn't one.
	Unknown,
}

// What a line typed in the chat line asks for (see GameChat.Parse).
public sealed record ChatCommand(ChatCommandKind Kind, ChatChannel Channel = ChatChannel.All, string Name = null, string Text = null);

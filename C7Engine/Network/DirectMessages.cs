using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using C7GameServer;
using Serilog;

namespace C7Engine.Network;

// Direct messages between accounts on the game server (see CHAT.md in Roma):
// one conversation per pair of accounts, the same in OpenCiv, Roma and the
// AirGames launcher. Signed in to the server, the game keeps its live socket
// open (GameServerProtocol.MessagesLivePath), reconnecting after a while if
// it drops, and shows what it pushes; messages are sent and the ignore list
// changed over HTTP, whose answers say whether they went. Everything to show
// is queued as a DirectMessageEvent for the chat line to take on the main
// thread (TryTake). Neither the messages nor who they're with are logged.
public sealed class DirectMessages : IDisposable {
	private static readonly ILogger log = Log.ForContext<DirectMessages>();

	// The player's account.
	public string Username { get; }

	private readonly IMessagesApi api;
	private readonly Uri liveUri;
	private readonly string token;
	private readonly Func<IMessageSocket> newSocket;
	private readonly CancellationTokenSource stop = new();
	private readonly ConcurrentQueue<DirectMessageEvent> events = new();

	// The messages shown, by ID, so one pushed after the answer to sending
	// it (or twice) shows once.
	private readonly HashSet<long> shown = new();
	private readonly HashSet<string> ignored = new(StringComparer.OrdinalIgnoreCase);
	private readonly object gate = new();

	private volatile IMessageSocket socket;
	private volatile string lastFrom;
	private bool unreadToldThisConnection;
	private Task running;

	// How long to wait before connecting again, doubling from the first to
	// the last while the server can't be reached.
	internal static TimeSpan FirstRetry = TimeSpan.FromSeconds(2);
	internal static TimeSpan LastRetry = TimeSpan.FromSeconds(60);

	public DirectMessages(IMessagesApi api, string username, Uri liveUri, string token, Func<IMessageSocket> newSocket) {
		this.api = api;
		Username = username;
		this.liveUri = liveUri;
		this.token = token;
		this.newSocket = newSocket;
	}

	// For the session the client has.
	public static DirectMessages For(GameServerClient client) {
		if (client?.Session == null) {
			throw new ArgumentException("Direct messages need a session on the game server");
		}
		return new DirectMessages(client, client.Session.Username, client.MessagesLiveUri(), client.Session.Token,
			() => new WebSocketMessageSocket());
	}

	// Whoever last messaged the player, for /r; null if nobody has.
	public string LastFrom => lastFrom;

	// Whether the live socket is open.
	public bool Connected => socket != null;

	// Opens the live socket, and loads the ignore list.
	public void Start() {
		running ??= Task.Run(RunAsync);
		_ = LoadIgnoresAsync();
	}

	public bool TryTake(out DirectMessageEvent e) => events.TryDequeue(out e);

	public bool IsIgnored(string name) {
		if (string.IsNullOrEmpty(name)) {
			return false;
		}
		lock (gate) {
			return ignored.Contains(name);
		}
	}

	private async Task LoadIgnoresAsync() {
		try {
			List<string> names = await api.IgnoresAsync(stop.Token).ConfigureAwait(false);
			lock (gate) {
				ignored.UnionWith(names ?? []);
			}
		} catch (Exception e) when (e is GameServerException or OperationCanceledException) {
			log.Information("Couldn't load the ignore list: {Error}", e.Message);
		}
	}

	private async Task RunAsync() {
		TimeSpan wait = FirstRetry;
		while (!stop.IsCancellationRequested) {
			IMessageSocket connecting = newSocket();
			try {
				await connecting.ConnectAsync(liveUri, token, stop.Token).ConfigureAwait(false);
				lock (gate) {
					unreadToldThisConnection = false;
				}
				socket = connecting;
				wait = FirstRetry;
				while (await connecting.ReceiveAsync(stop.Token).ConfigureAwait(false) is string frame) {
					Handle(frame);
				}
			} catch (OperationCanceledException) when (stop.IsCancellationRequested) {
				break;
			} catch (Exception e) {
				log.Information("The messages socket closed: {Error}", e.Message);
			} finally {
				socket = null;
				connecting.Dispose();
			}
			try {
				await Task.Delay(wait, stop.Token).ConfigureAwait(false);
			} catch (OperationCanceledException) {
				break;
			}
			wait = TimeSpan.FromTicks(Math.Min(LastRetry.Ticks, wait.Ticks * 2));
		}
	}

	// Takes a frame the server pushed.
	internal void Handle(string frame) {
		JsonElement root;
		try {
			using JsonDocument document = JsonDocument.Parse(frame);
			root = document.RootElement.Clone();
		} catch (JsonException) {
			log.Information("Ignoring a messages frame that isn't JSON");
			return;
		}
		if (root.ValueKind != JsonValueKind.Object) {
			return;
		}
		switch (Text(root, "t")) {
			case "dm":
				DirectMessage message = new(Number(root, "id"), Text(root, "from"), Text(root, "to"), Text(root, "text"), Text(root, "sent"));
				if (message.From == null || message.To == null || GameChat.Tidy(message.Text) is not string text) {
					return;
				}
				Show(message with { Text = text });
				break;
			case "unread":
				int count = (int)Number(root, "count");
				lock (gate) {
					if (count <= 0 || unreadToldThisConnection) {
						return;
					}
					unreadToldThisConnection = true;
				}
				events.Enqueue(new DirectMessageEvent(DirectMessageEventKind.Unread,
					count == 1 ? "You have 1 unread message." : $"You have {count} unread messages."));
				break;
			case "error":
				if (GameChat.Tidy(Text(root, "text")) is string error) {
					Notice(error);
				}
				break;
			case "ping":
				_ = SendFrameAsync(new Dictionary<string, object> { ["t"] = "pong" });
				break;
		}
	}

	// Shows a message once, as from the player or to them. One from someone
	// else is the one /r answers, and is marked read, since it's shown.
	private void Show(DirectMessage message) {
		bool fromMe = string.Equals(message.From, Username, StringComparison.OrdinalIgnoreCase);
		lock (gate) {
			if (message.Id != 0 && !shown.Add(message.Id)) {
				return;
			}
			if (!fromMe && ignored.Contains(message.From)) {
				return;
			}
		}
		if (!fromMe) {
			lastFrom = message.From;
			_ = SendFrameAsync(new Dictionary<string, object> { ["t"] = "read", ["with"] = message.From });
		}
		events.Enqueue(new DirectMessageEvent(DirectMessageEventKind.Message,
			GameChat.FormatWhisper(fromMe ? message.To : message.From, message.Text, fromMe), message, fromMe));
	}

	private void Notice(string text) {
		events.Enqueue(new DirectMessageEvent(DirectMessageEventKind.Notice, text));
	}

	private async Task SendFrameAsync(Dictionary<string, object> frame) {
		IMessageSocket open = socket;
		if (open == null) {
			return;
		}
		try {
			await open.SendAsync(JsonSerializer.Serialize(frame), stop.Token).ConfigureAwait(false);
		} catch (Exception e) {
			log.Information("Couldn't send on the messages socket: {Error}", e.Message);
		}
	}

	// Sends a message; what comes of it is queued, as the message sent or a
	// notice saying why it wasn't.
	public async Task SendAsync(string to, string text) {
		string name = to?.Trim();
		string tidy = GameChat.Tidy(text);
		if (string.IsNullOrEmpty(name)) {
			Notice("Who to? Type /w and their name, then the message.");
			return;
		}
		if (tidy == null) {
			Notice($"Type the message after the name: /w {name} hello");
			return;
		}
		if (string.Equals(name, Username, StringComparison.OrdinalIgnoreCase)) {
			Notice("You can't message yourself.");
			return;
		}
		try {
			DirectMessage sent = await api.SendMessageAsync(name, tidy, stop.Token).ConfigureAwait(false);
			Show(sent ?? new DirectMessage(0, Username, name, tidy, null));
		} catch (GameServerException e) {
			Notice(Refusal(e, name));
		} catch (OperationCanceledException) {
		}
	}

	// Answers whoever last messaged the player.
	public Task ReplyAsync(string text) {
		string to = lastFrom;
		if (to == null) {
			Notice("Nobody has messaged you yet.");
			return Task.CompletedTask;
		}
		return SendAsync(to, text);
	}

	public async Task IgnoreAsync(string name, bool ignore = true) {
		name = name?.Trim();
		if (string.IsNullOrEmpty(name)) {
			Notice(ignore ? "Who to ignore? Type /ignore and their name." : "Who to stop ignoring? Type /unignore and their name.");
			return;
		}
		if (ignore && string.Equals(name, Username, StringComparison.OrdinalIgnoreCase)) {
			Notice("You can't ignore yourself.");
			return;
		}
		try {
			if (ignore) {
				await api.IgnoreAsync(name, stop.Token).ConfigureAwait(false);
			} else {
				await api.UnignoreAsync(name, stop.Token).ConfigureAwait(false);
			}
			lock (gate) {
				if (ignore) {
					ignored.Add(name);
				} else {
					ignored.Remove(name);
				}
			}
			Notice(ignore ? $"You're ignoring {name}." : $"You're no longer ignoring {name}.");
		} catch (GameServerException e) {
			Notice(Refusal(e, name));
		} catch (OperationCanceledException) {
		}
	}

	// Why the server refused: as it says, or else in the words every client
	// uses.
	internal static string Refusal(GameServerException e, string name) {
		bool said = !string.IsNullOrWhiteSpace(e.Message) && !e.Message.StartsWith("The game server refused");
		return e.Status switch {
			HttpStatusCode.TooManyRequests => GameChat.TooFast,
			HttpStatusCode.Unauthorized => "Your session on the server has ended. Sign in again from Server Games.",
			_ when said => e.Message,
			HttpStatusCode.NotFound => $"No one is called {name}.",
			HttpStatusCode.Forbidden => $"{name} isn't taking messages.",
			_ => e.Message,
		};
	}

	private static string Text(JsonElement o, string name) {
		return o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
	}

	private static long Number(JsonElement o, string name) {
		return o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : 0;
	}

	public void Dispose() {
		stop.Cancel();
		socket?.Dispose();
	}
}

public enum DirectMessageEventKind {
	// A message to or from the player (Message, FromMe).
	Message,
	// How many messages came while the player was away.
	Unread,
	// Why something didn't go, or what came of it.
	Notice,
}

// A direct message as the server keeps it. Sent is when, as ISO 8601 UTC.
// (Kept out of C7GameServer, whose server has its own of the same name.)
public sealed record DirectMessage(long Id, string From, string To, string Text, string Sent);

public sealed record MessageText(string Text);

// Something for the chat line to show: Text is the line.
public sealed record DirectMessageEvent(DirectMessageEventKind Kind, string Text, DirectMessage Message = null, bool FromMe = false);

// What DirectMessages asks the server over HTTP; GameServerClient does it.
public interface IMessagesApi {
	Task<DirectMessage> SendMessageAsync(string username, string text, CancellationToken cancel = default);
	Task<List<string>> IgnoresAsync(CancellationToken cancel = default);
	Task IgnoreAsync(string username, CancellationToken cancel = default);
	Task UnignoreAsync(string username, CancellationToken cancel = default);
}

// The live socket, which tests stand in for.
public interface IMessageSocket : IDisposable {
	Task ConnectAsync(Uri uri, string token, CancellationToken cancel);
	Task SendAsync(string text, CancellationToken cancel);
	// The next text frame, or null once the socket has closed.
	Task<string> ReceiveAsync(CancellationToken cancel);
}

// The live socket over a WebSocket, with the session as a bearer token.
public sealed class WebSocketMessageSocket : IMessageSocket {
	private const int MaxFrameBytes = 64 * 1024;
	private readonly ClientWebSocket socket = new();
	private readonly SemaphoreSlim sending = new(1, 1);

	public async Task ConnectAsync(Uri uri, string token, CancellationToken cancel) {
		socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
		socket.Options.SetRequestHeader(OpenCivVersion.Header, OpenCivVersion.Sent);
		socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		timeout.CancelAfter(TimeSpan.FromSeconds(20));
		await socket.ConnectAsync(uri, timeout.Token).ConfigureAwait(false);
	}

	public async Task SendAsync(string text, CancellationToken cancel) {
		await sending.WaitAsync(cancel).ConfigureAwait(false);
		try {
			await socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, cancel).ConfigureAwait(false);
		} finally {
			sending.Release();
		}
	}

	public async Task<string> ReceiveAsync(CancellationToken cancel) {
		byte[] buffer = new byte[8192];
		using System.IO.MemoryStream frame = new();
		while (true) {
			WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancel).ConfigureAwait(false);
			if (result.MessageType == WebSocketMessageType.Close) {
				return null;
			}
			frame.Write(buffer, 0, result.Count);
			if (frame.Length > MaxFrameBytes) {
				throw new WebSocketException("A messages frame was too large");
			}
			if (result.EndOfMessage) {
				if (result.MessageType != WebSocketMessageType.Text) {
					frame.SetLength(0);
					continue;
				}
				return Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length);
			}
		}
	}

	public void Dispose() {
		socket.Dispose();
		sending.Dispose();
	}
}

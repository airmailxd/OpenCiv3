using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using C7Engine.Network;
using C7GameServer;
using Xunit;

namespace EngineTests.Network;

// Direct messages as the game shows them, against a stand-in for the game
// server's live socket and HTTP routes.
public class DirectMessagesTest {
	private sealed class FakeSocket : IMessageSocket {
		public readonly Channel<string> incoming = Channel.CreateUnbounded<string>();
		public readonly ConcurrentQueue<string> sent = new();
		public Uri uri;
		public string token;
		public bool disposed;

		public Task ConnectAsync(Uri uri, string token, CancellationToken cancel) {
			this.uri = uri;
			this.token = token;
			return Task.CompletedTask;
		}

		public Task SendAsync(string text, CancellationToken cancel) {
			sent.Enqueue(text);
			return Task.CompletedTask;
		}

		public async Task<string> ReceiveAsync(CancellationToken cancel) {
			return await incoming.Reader.WaitToReadAsync(cancel) && incoming.Reader.TryRead(out string frame) ? frame : null;
		}

		public void Push(object frame) => incoming.Writer.TryWrite(JsonSerializer.Serialize(frame));
		public void Close() => incoming.Writer.TryComplete();

		public void Dispose() => disposed = true;
	}

	private sealed class FakeServer : IMessagesApi {
		public long nextId = 100;
		public readonly List<(string to, string text)> sent = [];
		public readonly HashSet<string> ignores = new(StringComparer.OrdinalIgnoreCase) { "eve" };
		public HttpStatusCode? refuse;
		public string refusal;

		public Task<DirectMessage> SendMessageAsync(string username, string text, CancellationToken cancel = default) {
			if (refuse is HttpStatusCode status) {
				throw new GameServerException(refusal ?? $"The game server refused ({(int)status}).", status);
			}
			sent.Add((username, text));
			return Task.FromResult(new DirectMessage(nextId++, "me", username, text, "2026-11-25T12:00:00Z"));
		}

		public Task<List<string>> IgnoresAsync(CancellationToken cancel = default) => Task.FromResult(ignores.ToList());

		public Task IgnoreAsync(string username, CancellationToken cancel = default) {
			if (username == "nobody") {
				throw new GameServerException("The game server refused (404).", HttpStatusCode.NotFound);
			}
			ignores.Add(username);
			return Task.CompletedTask;
		}

		public Task UnignoreAsync(string username, CancellationToken cancel = default) {
			ignores.Remove(username);
			return Task.CompletedTask;
		}
	}

	private static readonly Uri Live = new("wss://games.example.org/api/v1/messages/live?v=1");

	private static (DirectMessages dms, FakeServer server, List<FakeSocket> sockets) Start() {
		FakeServer server = new();
		List<FakeSocket> sockets = [];
		DirectMessages dms = new(server, "me", Live, "secret", () => {
			FakeSocket socket = new();
			lock (sockets) {
				sockets.Add(socket);
			}
			return socket;
		});
		dms.Start();
		WaitFor(() => dms.Connected && !dms.IsIgnored("") && dms.IsIgnored("eve"));
		return (dms, server, sockets);
	}

	private static void WaitFor(Func<bool> condition) {
		Stopwatch waiting = Stopwatch.StartNew();
		while (!condition()) {
			if (waiting.Elapsed > TimeSpan.FromSeconds(20)) {
				throw new TimeoutException("Never got there");
			}
			Thread.Sleep(5);
		}
	}

	private static List<DirectMessageEvent> Take(DirectMessages dms) {
		List<DirectMessageEvent> taken = [];
		while (dms.TryTake(out DirectMessageEvent e)) {
			taken.Add(e);
		}
		return taken;
	}

	// Waits for the next event the chat line would show.
	private static DirectMessageEvent Next(DirectMessages dms) {
		DirectMessageEvent next = null;
		WaitFor(() => dms.TryTake(out next));
		return next;
	}

	private static object Dm(long id, string from, string to, string text) {
		return new { t = "dm", id, from, to, text, sent = "2026-11-25T12:00:00Z" };
	}

	[Fact]
	public async Task MessagesArriveAndGo() {
		(DirectMessages dms, FakeServer server, List<FakeSocket> sockets) = Start();
		using (dms) {
			FakeSocket socket = sockets.Single();
			Assert.Equal(Live, socket.uri);
			Assert.Equal("secret", socket.token);

			// Unread on connecting, once.
			socket.Push(new { t = "unread", count = 3 });
			socket.Push(new { t = "unread", count = 3 });
			Assert.Equal("You have 3 unread messages.", Next(dms).Text);

			// A message from carol shows, is marked read, and is the one /r answers.
			socket.Push(Dm(1, "carol", "me", "hi there"));
			DirectMessageEvent fromCarol = Next(dms);
			Assert.Equal("[From carol]: hi there", fromCarol.Text);
			Assert.False(fromCarol.FromMe);
			Assert.Equal("carol", dms.LastFrom);
			WaitFor(() => socket.sent.Any());
			Assert.Equal("{\"t\":\"read\",\"with\":\"carol\"}", socket.sent.Single());

			// The same message pushed again doesn't show again.
			socket.Push(Dm(1, "carol", "me", "hi there"));
			// Sent from another of my clients: shown as to them.
			socket.Push(Dm(2, "me", "dave", "from my launcher"));
			Assert.Equal("[To dave]: from my launcher", Next(dms).Text);

			// Sending goes over HTTP and shows as the server keeps it; the
			// server's push of it afterwards doesn't show it twice.
			await dms.ReplyAsync("  hello\ncarol ");
			Assert.Equal(("carol", "hello carol"), server.sent.Single());
			DirectMessageEvent toCarol = Next(dms);
			Assert.Equal("[To carol]: hello carol", toCarol.Text);
			Assert.True(toCarol.FromMe);
			socket.Push(Dm(100, "me", "carol", "hello carol"));
			socket.Push(new { t = "error", text = "Something the server says." });
			Assert.Equal("Something the server says.", Next(dms).Text);
			Assert.Empty(Take(dms));
		}
	}

	[Fact]
	public async Task RefusalsSayWhy() {
		(DirectMessages dms, FakeServer server, _) = Start();
		using (dms) {
			await dms.ReplyAsync("anyone?");
			Assert.Equal("Nobody has messaged you yet.", Next(dms).Text);

			server.refuse = HttpStatusCode.NotFound;
			await dms.SendAsync("Zed", "hi");
			Assert.Equal("No one is called Zed.", Next(dms).Text);
			server.refuse = HttpStatusCode.Forbidden;
			await dms.SendAsync("eve", "hi");
			Assert.Equal("eve isn't taking messages.", Next(dms).Text);
			server.refuse = HttpStatusCode.TooManyRequests;
			await dms.SendAsync("carol", "hi");
			Assert.Equal("You're sending too fast.", Next(dms).Text);
			// As the server says it, when it does.
			server.refuse = HttpStatusCode.NotFound;
			server.refusal = "No one is called zed.";
			await dms.SendAsync("zed", "hi");
			Assert.Equal("No one is called zed.", Next(dms).Text);
			server.refuse = null;

			await dms.SendAsync("me", "hi");
			Assert.Equal("You can't message yourself.", Next(dms).Text);
			await dms.SendAsync("carol", "   ");
			Assert.StartsWith("Type the message after the name", Next(dms).Text);
			await dms.SendAsync(null, "hi");
			Assert.StartsWith("Who to?", Next(dms).Text);
			Assert.Empty(server.sent);
		}
	}

	[Fact]
	public async Task IgnoredPlayersAreHidden() {
		(DirectMessages dms, FakeServer server, List<FakeSocket> sockets) = Start();
		using (dms) {
			FakeSocket socket = sockets.Single();
			// eve was ignored already, from another client.
			Assert.True(dms.IsIgnored("EVE"));
			socket.Push(Dm(5, "eve", "me", "hidden"));

			await dms.IgnoreAsync("dave");
			Assert.Equal("You're ignoring dave.", Next(dms).Text);
			Assert.True(dms.IsIgnored("dave"));
			Assert.Contains("dave", server.ignores);
			socket.Push(Dm(6, "dave", "me", "also hidden"));
			// Frames are handled in order, so once this shows, that was handled.
			socket.Push(new { t = "error", text = "marker" });
			Assert.Equal("marker", Next(dms).Text);

			await dms.IgnoreAsync("dave", ignore: false);
			Assert.Equal("You're no longer ignoring dave.", Next(dms).Text);
			Assert.False(dms.IsIgnored("dave"));
			socket.Push(Dm(7, "dave", "me", "shown"));
			Assert.Equal("[From dave]: shown", Next(dms).Text);

			// Another of my clients ignores frank.
			socket.Push(new { t = "ignore", name = "frank", on = true });
			socket.Push(new { t = "error", text = "marker 2" });
			Assert.Equal("marker 2", Next(dms).Text);
			Assert.True(dms.IsIgnored("frank"));

			await dms.IgnoreAsync("nobody");
			Assert.Equal("No one is called nobody.", Next(dms).Text);
			await dms.IgnoreAsync("me");
			Assert.Equal("You can't ignore yourself.", Next(dms).Text);
			Assert.Empty(Take(dms));
		}
	}

	[Fact]
	public void TheSocketOpensAgainAfterClosing() {
		TimeSpan first = DirectMessages.FirstRetry;
		DirectMessages.FirstRetry = TimeSpan.FromMilliseconds(20);
		try {
			(DirectMessages dms, _, List<FakeSocket> sockets) = Start();
			using (dms) {
				sockets.Single().Close();
				WaitFor(() => {
					lock (sockets) {
						return sockets.Count == 2;
					}
				});
				Assert.True(sockets[0].disposed);
				WaitFor(() => dms.Connected);
				sockets[1].Push(Dm(9, "carol", "me", "back"));
				Assert.Equal("[From carol]: back", Next(dms).Text);
			}
		} finally {
			DirectMessages.FirstRetry = first;
		}
	}

	[Fact]
	public void TheIgnoreListReadsAsTheServerMayGiveIt() {
		Assert.Equal(["a", "b"], GameServerClient.IgnoredNames(JsonDocument.Parse("[\"a\",\"b\"]").RootElement));
		Assert.Equal(["a", "b"], GameServerClient.IgnoredNames(JsonDocument.Parse("[{\"username\":\"a\",\"since\":\"x\"},{\"name\":\"b\"}]").RootElement));
		Assert.Equal(["a"], GameServerClient.IgnoredNames(JsonDocument.Parse("{\"ignores\":[\"a\"]}").RootElement));
		Assert.Empty(GameServerClient.IgnoredNames(JsonDocument.Parse("{}").RootElement));
		Assert.Equal(new Uri("wss://games.example.org/api/v1/messages/live?v=1"),
			new GameServerClient("https://games.example.org").MessagesLiveUri());
		Assert.Equal(new Uri("ws://localhost:5000/api/v1/messages/live?v=1"),
			new GameServerClient("http://localhost:5000/").MessagesLiveUri());
	}
}

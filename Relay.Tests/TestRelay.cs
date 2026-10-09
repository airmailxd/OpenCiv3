extern alias relay;

using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using C7Engine.Network;
using C7Relay;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using RelayHub = relay::C7Relay.RelayHub;
using RelayOptions = relay::C7Relay.RelayOptions;
using RelayServer = relay::C7Relay.RelayServer;
using Room = relay::C7Relay.Room;

namespace Relay.Tests;

// A relay running in this process, on a port of its own on localhost.
internal sealed class TestRelay : IAsyncDisposable {
	public WebApplication App { get; }

	// Where the game connects, like ws://127.0.0.1:1234.
	public string Url { get; }
	public string HttpUrl { get; }

	internal RelayHub Hub => App.Services.GetRequiredService<RelayHub>();

	private TestRelay(WebApplication app) {
		App = app;
		HttpUrl = app.Urls.First();
		Url = "ws" + HttpUrl["http".Length..];
	}

	public static async Task<TestRelay> Start(Action<RelayOptions> configure = null) {
		WebApplication app = RelayServer.Build(["--urls=http://127.0.0.1:0", "--Logging:LogLevel:Default=Warning"], options => {
			options.KeySecret = "a secret for testing";
			configure?.Invoke(options);
		});
		await app.StartAsync();
		return new TestRelay(app);
	}

	internal Room RoomFor(string code) {
		Assert.True(Hub.Rooms.TryGet(RelayProtocol.NormalizeCode(code), out Room room), $"No room {code}");
		return room;
	}

	// Closes the host's connection to the relay, as when its network drops.
	public void DropHost(string code) {
		Room room = RoomFor(code);
		lock (room) {
			room.Host?.Close((int)WebSocketCloseStatus.EndpointUnavailable, "Dropped by the test.");
		}
	}

	// Closes the connection of the guest with that ID in the room.
	public void DropGuest(string code, uint id) {
		Room room = RoomFor(code);
		lock (room) {
			Assert.True(room.Guests.TryGetValue(id, out var guest), $"No guest {id} in {code}");
			guest.Close((int)WebSocketCloseStatus.EndpointUnavailable, "Dropped by the test.");
		}
	}

	// Closes every guest's connection in the room.
	public void DropGuests(string code) {
		Room room = RoomFor(code);
		lock (room) {
			foreach (var guest in room.Guests.Values) {
				guest.Close((int)WebSocketCloseStatus.EndpointUnavailable, "Dropped by the test.");
			}
		}
	}

	// The room's public listing as the relay keeps it, as JSON, or null when
	// it isn't listed.
	public string ListingOf(string code) {
		Room room = RoomFor(code);
		lock (room) {
			return room.Listing == null ? null : System.Text.Json.JsonSerializer.Serialize(room.Listing);
		}
	}

	public int GuestsIn(string code) {

		Room room = RoomFor(code);
		lock (room) {
			return room.Guests.Count;
		}
	}

	public async ValueTask DisposeAsync() {
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		await App.StopAsync(timeout.Token);
		await App.DisposeAsync();
	}
}

// A bare WebSocket to the relay, for seeing what the relay says without the
// game in the way.
internal sealed class RawClient : IAsyncDisposable {
	public ClientWebSocket Socket { get; } = new();
	private readonly byte[] buffer = new byte[1024 * 1024];

	// Whether to answer the relay's pings, as the game does.
	public bool AnswerPings = true;

	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	public static async Task<RawClient> Connect(Uri uri) {
		RawClient client = new();
		client.Socket.Options.CollectHttpResponseDetails = true;
		using CancellationTokenSource timeout = new(Timeout);
		await client.Socket.ConnectAsync(uri, timeout.Token);
		return client;
	}

	// The next message other than a ping; a Close with no data once the relay
	// closes the connection.
	public async Task<(WebSocketMessageType type, byte[] data)> Receive() {
		using CancellationTokenSource timeout = new(Timeout);
		while (true) {
			int length = 0;
			ValueWebSocketReceiveResult result;
			do {
				result = await Socket.ReceiveAsync(buffer.AsMemory(length), timeout.Token);
				length += result.Count;
			} while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);
			if (result.MessageType == WebSocketMessageType.Close) {
				return (WebSocketMessageType.Close, []);
			}
			byte[] data = buffer.AsSpan(0, length).ToArray();
			if (result.MessageType == WebSocketMessageType.Text && RelayControl.Parse(data)?.type == RelayControl.Ping) {
				if (AnswerPings) {
					await SendText(new RelayControl(RelayControl.Pong));
				}
				continue;
			}
			return (result.MessageType, data);
		}
	}

	public async Task<byte[]> ReceiveBinary() {
		(WebSocketMessageType type, byte[] data) = await Receive();
		Assert.True(type == WebSocketMessageType.Binary, $"Expected data, got {type}: {Socket.CloseStatus} {Socket.CloseStatusDescription}");
		return data;
	}

	public async Task<RelayControl> Welcome() {
		(WebSocketMessageType type, byte[] data) = await Receive();
		Assert.True(type == WebSocketMessageType.Text, $"Expected a welcome, got {type}: {Socket.CloseStatus} {Socket.CloseStatusDescription}");
		RelayControl welcome = RelayControl.Parse(data);
		Assert.Equal(RelayControl.Welcome, welcome?.type);
		return welcome;
	}

	// Waits for the relay to close the connection, and says why it did.
	public async Task<(int? status, string reason)> Closed() {
		while (true) {
			(WebSocketMessageType type, _) = await Receive();
			if (type == WebSocketMessageType.Close) {
				return ((int?)Socket.CloseStatus, Socket.CloseStatusDescription);
			}
		}
	}

	public Task Send(byte[] data) {
		return Socket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
	}

	public Task SendText(RelayControl control) {
		return Socket.SendAsync(control.ToBytes(), WebSocketMessageType.Text, true, CancellationToken.None);
	}

	public async ValueTask DisposeAsync() {
		try {
			if (Socket.State == WebSocketState.Open) {
				using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
				await Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", timeout.Token);
			}
		} catch (Exception) {
		}
		Socket.Dispose();
	}

	public static Uri HostUri(TestRelay relay, string code = null, string key = null) => RelayConnection.HostUri(relay.Url, code, key);
	public static Uri JoinUri(TestRelay relay, string code) => RelayConnection.JoinUri(relay.Url, code);
}

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace C7Relay;

// Brings hosts and guests together: gives each host a room with a join code,
// and passes the bytes between the host and each guest in it, without
// reading them. See RelayProtocol. Only who connects where, and how much they
// sent, is logged; never what.
internal sealed class RelayHub {
	private readonly RelayOptions options;
	private readonly ILogger<RelayHub> log;
	private readonly CancellationToken shutdown;

	public RoomRegistry Rooms { get; }
	public RateLimits Limits { get; }

	// What the connections hold of large messages as they arrive.
	public ByteBudget ReceiveBuffers { get; }

	public RelayHub(IOptions<RelayOptions> options, ILogger<RelayHub> log, IHostApplicationLifetime lifetime) {
		this.options = options.Value;
		this.log = log;
		shutdown = lifetime.ApplicationStopping;
		Rooms = new RoomRegistry(this.options);
		Limits = new RateLimits(this.options);
		ReceiveBuffers = new ByteBudget(this.options.MaxReceiveBufferBytes);
		if (string.IsNullOrEmpty(this.options.KeySecret)) {
			log.LogWarning("No Relay:KeySecret is set, so hosts can't claim their codes again once the relay restarts");
		}
	}

	private static string AddressOf(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

	// Takes up a connection and runs it, after turning away what isn't a
	// WebSocket, or comes from an address that's trying too often or has
	// too many open, or when the relay has as many as it can take.
	private async Task Serve(HttpContext context, bool joining, Func<RelayPeer, string, Task> run) {
		string address = AddressOf(context);
		if (!context.WebSockets.IsWebSocketRequest) {
			context.Response.StatusCode = StatusCodes.Status400BadRequest;
			await context.Response.WriteAsync("This is an OpenCiv3 relay. Connect to it from the game.");
			return;
		}
		if (!Limits.AllowConnection(address) || (joining && Limits.IsLockedOut(address))) {
			log.LogInformation("Turned away {Address}, which is trying too often", address);
			context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
			return;
		}
		switch (Limits.Open(address)) {
			case RateLimits.Opening.TooManyFromAddress:
				log.LogInformation("Turned away {Address}, which has too many connections open", address);
				context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
				return;
			case RateLimits.Opening.TooManyInAll:
				log.LogWarning("Turned away {Address}: the relay has as many connections as it takes", address);
				context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
				return;
		}
		try {
			WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
			int maxMessageBytes = joining ? options.MaxGuestMessageBytes : options.MaxHostMessageBytes;
			await run(new RelayPeer(socket, address, options, maxMessageBytes, ReceiveBuffers), address);
		} finally {
			Limits.Closed(address);
		}
	}

	// Why the game can't use this relay, or null if it can.
	private static string CheckVersions(HttpContext context, out string gameVersion) {
		gameVersion = context.Request.Query[RelayProtocol.GameVersionParameter].ToString();
		if (!int.TryParse(context.Request.Query[RelayProtocol.VersionParameter], out int version)
			|| version < RelayProtocol.MinVersion || version > RelayProtocol.Version) {
			return "This relay runs a different version than your game. Update the game, or use another relay.";
		}
		if (string.IsNullOrEmpty(gameVersion) || gameVersion.Length > 32) {
			return "Your game didn't say which version it is.";
		}
		return null;
	}

	// Says why the connection is turned away, and closes it.
	private static Task Reject(RelayPeer peer, int status, string reason) {
		peer.Close(status, reason);
		return peer.RunAsync((_, _) => { }, CancellationToken.None);
	}

	public Task Host(HttpContext context) => Serve(context, joining: false, (peer, address) => RunHost(context, peer, address));

	public Task Join(HttpContext context, string typedCode) => Serve(context, joining: true, (peer, address) => RunGuest(context, peer, address, typedCode));

	private async Task RunHost(HttpContext context, RelayPeer peer, string address) {
		if (CheckVersions(context, out string gameVersion) is string versionError) {
			await Reject(peer, RelayCloseCodes.UnsupportedVersion, versionError);
			return;
		}

		string claimed = context.Request.Query[RelayProtocol.CodeParameter];
		bool reclaiming = !string.IsNullOrEmpty(claimed);
		Room room;
		if (reclaiming) {
			string code = RelayProtocol.NormalizeCode(claimed);
			if (Limits.IsLockedOut(address) || code == null || !Rooms.IsKeyFor(code, context.Request.Query[RelayProtocol.KeyParameter])) {
				Limits.NoteFailedJoin(address);
				log.LogInformation("{Address} tried to claim {Code} without its key", address, code ?? "an invalid code");
				await Reject(peer, RelayCloseCodes.WrongKey, "That isn't the key to this join code.");
				return;
			}
			room = Rooms.Reclaim(code, gameVersion);
		} else {
			if (!Limits.AllowRoom(address)) {
				await Reject(peer, RelayCloseCodes.TooManyAttempts, "Too many games hosted from your address lately. Try again later.");
				return;
			}
			room = Rooms.Create(gameVersion);
		}
		if (room == null) {
			log.LogWarning("Turned away a host at {Address}: the relay has {Rooms} rooms already", address, Rooms.Count);
			await Reject(peer, RelayCloseCodes.RelayFull, "The relay is full. Try again later.");
			return;
		}

		RelayPeer previous;
		List<RelayPeer> guests;
		lock (room) {
			previous = room.Host;
			room.Host = peer;
			room.GameVersion = gameVersion;
			guests = [.. room.Guests.Values];
			room.Guests.Clear();
		}
		// A host back before the relay noticed it had gone starts afresh, and
		// its guests connect again.
		previous?.Close(RelayCloseCodes.Replaced, "The host connected again from elsewhere.");
		foreach (RelayPeer guest in guests) {
			guest.Close(RelayCloseCodes.HostAway, "The host connected to the relay again.");
		}

		log.LogInformation(reclaiming ? "Host at {Address} is back in room {Code}" : "Host at {Address} opened room {Code}",
			address, room.Code);
		peer.Send(new RelayControl(RelayControl.Welcome, room.Code, Rooms.KeyFor(room.Code), options.PingIntervalSeconds));

		await peer.RunAsync((message, type) => FromHost(room, peer, message, type), shutdown);

		lock (room) {
			if (room.Host == peer) {
				room.Host = null;
				room.HostLeftAt = Environment.TickCount64;
				guests = [.. room.Guests.Values];
				room.Guests.Clear();
			} else {
				guests = [];
			}
		}
		foreach (RelayPeer guest in guests) {
			guest.Close(RelayCloseCodes.HostAway, "The host lost its connection to the relay.");
		}
		log.LogInformation("Host at {Address} left room {Code}, having sent {Received} bytes and been sent {Sent}",
			address, room.Code, peer.BytesReceived, peer.BytesSent);
	}

	private void FromHost(Room room, RelayPeer host, ReadOnlyMemory<byte> message, WebSocketMessageType type) {
		if (type == WebSocketMessageType.Text) {
			Answer(host, message);
			return;
		}
		ReadOnlySpan<byte> span = message.Span;
		if (!RelayProtocol.TryReadHeader(span, out byte kind, out int count) || kind is not (RelayProtocol.Data or RelayProtocol.Close)) {
			log.LogWarning("The host of room {Code} sent a message the relay can't read", room.Code);
			host.Close(RelayCloseCodes.BadMessage, "The game sent the relay a message it can't read.");
			return;
		}
		// Every guest it's for gets the same bytes, so they're copied once.
		ReadOnlyMemory<byte> payload = kind == RelayProtocol.Data ? RelayProtocol.Payload(span, count).ToArray() : default;
		lock (room) {
			for (int i = 0; i < count; ++i) {
				// A guest that has gone already is skipped: the host hears
				// that it has, if it hasn't yet.
				if (!room.Guests.TryGetValue(RelayProtocol.GuestAt(span, i), out RelayPeer guest)) {
					continue;
				}
				if (kind == RelayProtocol.Data) {
					guest.Send(payload);
				} else {
					guest.Close(RelayCloseCodes.ClosedByHost, "The host closed the connection.");
				}
			}
		}
	}

	private enum Admission { Admitted, HostAway, OtherVersion, Full }

	private async Task RunGuest(HttpContext context, RelayPeer peer, string address, string typedCode) {
		if (CheckVersions(context, out string gameVersion) is string versionError) {
			await Reject(peer, RelayCloseCodes.UnsupportedVersion, versionError);
			return;
		}
		string code = RelayProtocol.NormalizeCode(typedCode);
		if (code == null || !Rooms.TryGet(code, out Room room)) {
			Limits.NoteFailedJoin(address);
			await Reject(peer, RelayCloseCodes.UnknownRoom,
				$"No game has the code {RelayProtocol.FormatCode(code) ?? typedCode}. Check it with the host.");
			return;
		}

		RelayPeer host;
		uint id = 0;
		Admission admission;
		lock (room) {
			host = room.Host;
			if (host == null) {
				admission = Admission.HostAway;
			} else if (room.GameVersion != gameVersion) {
				admission = Admission.OtherVersion;
			} else if (room.Guests.Count >= options.MaxGuestsPerRoom) {
				admission = Admission.Full;
			} else {
				admission = Admission.Admitted;
				id = room.NextGuestId++;
				room.Guests[id] = peer;
			}
		}
		switch (admission) {
			case Admission.HostAway:
				await Reject(peer, RelayCloseCodes.HostAway, "The host isn't connected to the relay right now.");
				return;
			case Admission.OtherVersion:
				await Reject(peer, RelayCloseCodes.GameVersionMismatch, "The host is running a different version of the game.");
				return;
			case Admission.Full:
				await Reject(peer, RelayCloseCodes.RoomFull, "This game has as many players as it can take through the relay.");
				return;
		}

		log.LogInformation("Guest {Guest} at {Address} joined room {Code}", id, address, room.Code);
		peer.Send(new RelayControl(RelayControl.Welcome, pingSeconds: options.PingIntervalSeconds));
		host.Send(RelayProtocol.Encode(RelayProtocol.Open, id));

		await peer.RunAsync((message, type) => {
			if (type == WebSocketMessageType.Text) {
				Answer(peer, message);
			} else {
				host.Send(RelayProtocol.Encode(RelayProtocol.Data, id, message.Span));
			}
		}, shutdown);

		bool wasIn;
		lock (room) {
			wasIn = room.Guests.Remove(id);
		}
		if (wasIn) {
			host.Send(RelayProtocol.Encode(RelayProtocol.Close, id));
		}
		log.LogInformation("Guest {Guest} at {Address} left room {Code}, having sent {Received} bytes and been sent {Sent}",
			id, address, room.Code, peer.BytesReceived, peer.BytesSent);
	}

	// Answers a ping; nothing else said in text needs an answer.
	private static void Answer(RelayPeer peer, ReadOnlyMemory<byte> message) {
		if (RelayControl.Parse(message.Span)?.type == RelayControl.Ping) {
			peer.Send(new RelayControl(RelayControl.Pong));
		}
	}

	// What /health reports.
	public object Health() {
		int hosts = 0, guests = 0;
		foreach (Room room in Rooms.All) {
			lock (room) {
				hosts += room.Host == null ? 0 : 1;
				guests += room.Guests.Count;
			}
		}
		return new { status = "ok", version = RelayProtocol.Version, rooms = Rooms.Count, hosts, guests };
	}

	public void Sweep() {
		foreach (string code in Rooms.Sweep()) {
			log.LogInformation("Room {Code} closed: its host didn't come back", code);
		}
		Limits.Sweep();
	}
}

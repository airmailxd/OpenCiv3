using System;
using System.Collections.Generic;
using System.Linq;

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
// reading them; and keeps the public list of the games hosts list there. See
// RelayProtocol. Only who connects where, and how much they sent, is logged;
// never what, nor anything of a listed game but its code.
internal sealed class RelayHub {
	private readonly RelayOptions options;
	private readonly ILogger<RelayHub> log;
	private readonly CancellationToken shutdown;

	public RoomRegistry Rooms { get; }
	public RateLimits Limits { get; }

	// What the connections hold of large messages as they arrive.
	public ByteBudget ReceiveBuffers { get; }

	// Taken while listing a game (see List).
	private readonly object listing = new();

	public RelayHub(IOptions<RelayOptions> options, ILogger<RelayHub> log, IHostApplicationLifetime lifetime, IHostEnvironment environment) {
		this.options = options.Value;
		this.log = log;
		shutdown = lifetime.ApplicationStopping;
		// Without a secret, hosts lose their codes and bans whenever the
		// relay restarts, which a relay players use can't have.
		if (string.IsNullOrEmpty(this.options.KeySecret) && environment.IsProduction()) {
			throw new InvalidOperationException("Set Relay:KeySecret (or Relay__KeySecret) to a long random string to run the relay in production");
		}
		Rooms = new RoomRegistry(this.options);
		Limits = new RateLimits(this.options);
		ReceiveBuffers = new ByteBudget(this.options.MaxReceiveBufferBytes);
		if (string.IsNullOrEmpty(this.options.KeySecret)) {
			log.LogWarning("No Relay:KeySecret is set, so hosts can't claim their codes again once the relay restarts");
		}
	}

	// A client's address, as the relay counts and bans it: an IPv6 address
	// by its /64, which one client usually has all of.
	private static string AddressOf(HttpContext context) => RateLimits.KeyOf(context.Connection.RemoteIpAddress);

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

		bool admits = context.Request.Query[RelayProtocol.AdmitsParameter] == "1";
		string scope = context.Request.Query[RelayProtocol.BanScopeParameter];
		RelayPeer previous = null;
		List<RelayPeer> guests = null;
		// A room swept away just as it was claimed is made again.
		while (!TakeRoom(room)) {
			room = reclaiming ? Rooms.Reclaim(room.Code, gameVersion) : Rooms.Create(gameVersion);
			if (room == null) {
				await Reject(peer, RelayCloseCodes.RelayFull, "The relay is full. Try again later.");
				return;
			}
		}

		bool TakeRoom(Room room) {
			lock (room) {
				if (room.Removed) {
					return false;
				}
				previous = room.Host;
				room.Host = peer;
				room.GameVersion = gameVersion;
				room.HostAdmits = admits;
				room.MovedTo = null;
				// Kept apart from codes, so that a host can't make its keys a
				// room without a scope makes.
				room.BanScope = RelayProtocol.IsBanScope(scope) ? $"host:{scope}" : room.Code;
				guests = [.. room.Guests.Values];
				room.Guests.Clear();
				room.Admitted.Clear();
				return true;
			}
		}

		// A host back before the relay noticed it had gone starts afresh, and
		// its guests connect again.
		previous?.Close(RelayCloseCodes.Replaced, "The host connected again from elsewhere.");
		foreach (RelayPeer guest in guests) {
			guest.Close(RelayCloseCodes.HostAway, "The host connected to the relay again.");
		}

		log.LogInformation(reclaiming ? "Host at {Address} is back in room {Code}" : "Host at {Address} opened room {Code}",
			address, room.Code);
		if (!reclaiming) {
			NoteMoved(context, address, room, gameVersion);
		}
		peer.Send(new RelayControl(RelayControl.Welcome, room.Code, Rooms.KeyFor(room.Code), options.PingIntervalSeconds,
			maxMessageBytes: options.MaxHostMessageBytes));

		await peer.RunAsync((message, type) => FromHost(room, peer, address, message, type), shutdown);

		bool wasListed = false;
		lock (room) {
			if (room.Host == peer) {
				room.Host = null;
				// A game whose host has gone can't be joined: it's listed
				// again once the host is back.
				wasListed = room.Listing != null;
				room.Listing = null;
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
		if (wasListed) {
			log.LogInformation("Room {Code} is no longer listed", room.Code);
		}
		log.LogInformation("Host at {Address} left room {Code}, having sent {Received} bytes and been sent {Sent}",
			address, room.Code, peer.BytesReceived, peer.BytesSent);
	}

	// A host in a new room that had another, as when it couldn't claim the
	// old one again, has the old one's guests told where it went, given the
	// old one's key.
	private void NoteMoved(HttpContext context, string address, Room room, string gameVersion) {
		string from = RelayProtocol.NormalizeCode(context.Request.Query[RelayProtocol.MovedFromParameter]);
		if (from == null || from == room.Code) {
			return;
		}
		if (!Rooms.IsKeyFor(from, context.Request.Query[RelayProtocol.MovedFromKeyParameter])) {
			Limits.NoteFailedJoin(address);
			log.LogInformation("{Address} said it moved from {Code} without its key", address, from);
			return;
		}
		Room old = Rooms.Reclaim(from, gameVersion);
		if (old == null) {
			return;
		}
		lock (old) {
			if (old.Host == null) {
				old.MovedTo = room.Code;
			}
		}
		log.LogInformation("The host of room {Code} moved to room {NewCode}", from, room.Code);
	}

	private void FromHost(Room room, RelayPeer host, string address, ReadOnlyMemory<byte> message, WebSocketMessageType type) {
		if (type == WebSocketMessageType.Text) {
			FromHost(room, host, address, RelayControl.Parse(message.Span));
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

	// What the host says in text: answers to pings, and listing its game and
	// banning guests.
	private void FromHost(Room room, RelayPeer host, string address, RelayControl control) {
		switch (control?.type) {
			case RelayControl.Ping:
			case RelayControl.Pong:
				Answer(host, control);
				break;
			case RelayControl.List:
				host.Send(new RelayControl(RelayControl.Listed, error: List(room, host, address, control.listing)));
				break;
			case RelayControl.Unlist:
				bool wasListed;
				lock (room) {
					wasListed = room.Listing != null && room.Host == host;
					if (wasListed) {
						room.Listing = null;
					}
				}
				if (wasListed) {
					log.LogInformation("Room {Code} is no longer listed", room.Code);
				}
				host.Send(new RelayControl(RelayControl.Listed));
				break;
			case RelayControl.Ban:
				string givenKey = control.bans is [string one] && one is { Length: > 0 and <= 64 } ? one : null;
				Ban(room, host, control.guest, givenKey);
				break;
			case RelayControl.Admit:
				lock (room) {
					if (room.Host == host && room.Guests.ContainsKey(control.guest)) {
						room.Admitted.Add(control.guest);
					}
				}
				break;
			case RelayControl.Bans:
				lock (room) {
					foreach (string key in (control.bans ?? []).Take(RelayProtocol.MaxBans)) {
						if (room.Bans.Count < RelayProtocol.MaxBans && key is { Length: > 0 and <= 64 }) {
							room.Bans.Add(key);
						}
					}
				}
				break;
		}
	}

	// Lists the room's game publicly, or lists it again as it is now.
	// Returns why it wasn't listed, or null if it was.
	private string List(Room room, RelayPeer host, string address, GameListing listing) {
		GameListing tidy = GameList.Tidy(listing);
		if (tidy == null) {
			return "A public game needs a name.";
		}
		bool listedBefore;
		lock (room) {
			if (room.Host != host) {
				return "The game isn't connected to the relay.";
			}
			listedBefore = room.Listing != null;
		}
		// Counted outside the room's lock, since counting takes each room's,
		// but under the listing lock, so that two listings at once from one
		// address can't both get in past the limit.
		lock (listing) {
			if (!listedBefore && Rooms.ListedFrom(address) >= options.MaxListingsPerAddress) {
				return $"Your address already lists {options.MaxListingsPerAddress} games, as many as this relay takes from one address.";
			}
			lock (room) {
				if (room.Host != host) {
					return "The game isn't connected to the relay.";
				}
				room.Listing = tidy;
				room.ListedAt = Environment.TickCount64;
				room.ListedFrom = address;
			}
		}
		if (!listedBefore) {
			log.LogInformation("Room {Code} is listed publicly", room.Code);
		}
		return null;
	}

	// Closes a guest's connection and turns its address away from the room
	// from now on, telling the host the key it can give back to ban that
	// address again. A guest that has left lately can be banned too.
	//
	// A host that knows the guest's key (from "guest", as it joined) gives
	// it too: the key is banned whether or not the guest is still here, and
	// the guest with the ID is closed only if it has that key, so that an ID
	// from before the relay restarted can't ban someone else.
	private void Ban(Room room, RelayPeer host, uint id, string givenKey) {
		RelayPeer guest;
		string address;
		string key;
		lock (room) {
			if (room.Guests.TryGetValue(id, out guest)) {
				address = guest.Address;
			} else if (!room.RecentGuests.TryGetValue(id, out address) && givenKey == null) {
				return;
			}
			if (givenKey != null) {
				if (address != null && Rooms.BanKeyFor(room.BanScope, address) != givenKey) {
					guest = null;
				}
				key = givenKey;
			} else {
				key = Rooms.BanKeyFor(room.BanScope, address);
			}
			if (room.Bans.Count >= RelayProtocol.MaxBans && !room.Bans.Contains(key)) {
				return;
			}
			room.Bans.Add(key);
		}
		guest?.Close(RelayCloseCodes.Banned, "The host has banned you from this game.");
		log.LogInformation("The host of room {Code} banned guest {Guest}", room.Code, id);
		host.Send(new RelayControl(RelayControl.Banned, guest: id, bans: [key]));
	}

	private enum Admission { Admitted, HostAway, Moved, OtherVersion, Full, Banned }

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
		uint madeWay = 0;
		RelayPeer madeWayFor = null;
		string movedTo;
		lock (room) {
			host = room.Host;
			movedTo = room.MovedTo;
			if (host == null && movedTo != null) {
				admission = Admission.Moved;
			} else if (host == null) {
				admission = Admission.HostAway;
			} else if (room.Bans.Count > 0 && room.Bans.Contains(Rooms.BanKeyFor(room.BanScope, address))) {
				admission = Admission.Banned;
			} else if (room.GameVersion != gameVersion) {
				admission = Admission.OtherVersion;
			} else if (room.Guests.Count >= options.MaxGuestsPerRoom && !room.TakeOutWaitingGuest(out madeWay, out madeWayFor)) {
				admission = Admission.Full;
			} else {
				admission = Admission.Admitted;
				id = room.NextGuestId++;
				room.Guests[id] = peer;
			}
		}
		if (madeWayFor != null) {
			// The host hears it has gone, as it would once it closed.
			log.LogInformation("Guest {Guest} in room {Code} made way for a newcomer, not having been let in", madeWay, room.Code);
			madeWayFor.Close(RelayCloseCodes.RoomFull, "The game has too many players waiting to join. Try again in a moment.");
			host.Send(RelayProtocol.Encode(RelayProtocol.Close, madeWay));
		}
		switch (admission) {
			case Admission.HostAway:
				await Reject(peer, RelayCloseCodes.HostAway, "The host isn't connected to the relay right now.");
				return;
			case Admission.Moved:
				await Reject(peer, RelayCloseCodes.Moved, RelayProtocol.MovedReason(movedTo));
				return;
			case Admission.OtherVersion:
				await Reject(peer, RelayCloseCodes.GameVersionMismatch, "The host is running a different version of the game.");
				return;
			case Admission.Banned:
				log.LogInformation("Turned away a banned guest at {Address} from room {Code}", address, room.Code);
				await Reject(peer, RelayCloseCodes.Banned, "The host has banned you from this game.");
				return;
			case Admission.Full:
				await Reject(peer, RelayCloseCodes.RoomFull, "This game has as many players as it can take through the relay.");
				return;
		}

		log.LogInformation("Guest {Guest} at {Address} joined room {Code}", id, address, room.Code);
		peer.Send(new RelayControl(RelayControl.Welcome, pingSeconds: options.PingIntervalSeconds, maxMessageBytes: options.MaxGuestMessageBytes));
		// The guest's key first, for the host to ban it by.
		string banKey;
		lock (room) {
			banKey = Rooms.BanKeyFor(room.BanScope, address);
		}
		host.Send(new RelayControl(RelayControl.Guest, guest: id, key: banKey));
		host.Send(RelayProtocol.Encode(RelayProtocol.Open, id));

		// A guest sending too much is dropped; one sending while its host
		// can't keep up waits, rather than have the host dropped for it.
		TokenBucket bytes = new(options.GuestBytesPerSecond, options.GuestBurstBytes);
		TokenBucket messages = new(options.GuestMessagesPerSecond, options.GuestBurstMessages);
		await peer.RunAsync((message, type) => {
			if (!bytes.Take(message.Length) || !messages.Take(1)) {
				log.LogInformation("Guest {Guest} at {Address} in room {Code} sent too much, dropping it", id, address, room.Code);
				peer.Close(RelayCloseCodes.TooMuch, "The game sent the relay too much, too fast.");
				return;
			}
			if (type == WebSocketMessageType.Text) {
				Answer(peer, RelayControl.Parse(message.Span));
			} else {
				host.Send(RelayProtocol.Encode(RelayProtocol.Data, id, message.Span));
			}
		}, shutdown, cancel => host.WaitForRoom(options.HostBackpressureBytes, cancel));

		bool wasIn;
		lock (room) {
			wasIn = room.Guests.Remove(id);
			room.NoteGuestLeft(id, address);
		}
		if (wasIn) {
			host.Send(RelayProtocol.Encode(RelayProtocol.Close, id));
		}
		log.LogInformation("Guest {Guest} at {Address} left room {Code}, having sent {Received} bytes and been sent {Sent}",
			id, address, room.Code, peer.BytesReceived, peer.BytesSent);
	}

	// Answers a ping, and times the answer to ours; nothing else a guest
	// says in text needs an answer.
	private static void Answer(RelayPeer peer, RelayControl control) {
		if (control?.type == RelayControl.Ping) {
			peer.Send(new RelayControl(RelayControl.Pong));
		} else if (control?.type == RelayControl.Pong) {
			peer.NotePong();
		}
	}

	// The games listed publicly now: those whose hosts are connected and
	// have listed them lately.
	private List<PublicGame> ListedGames() {
		long now = Environment.TickCount64;
		List<PublicGame> listed = [];
		foreach (Room room in Rooms.All) {
			lock (room) {
				if (room.Host == null || room.Listing == null || now - room.ListedAt >= options.ListingTtl.TotalMilliseconds) {
					continue;
				}
				int ping = room.Host.RoundTripMs;
				listed.Add(new PublicGame(room.Code, room.GameVersion, room.Listing,
					room.Listing.seatsTotal - room.Listing.seatsTaken, ping < 0 ? null : ping));
			}
		}
		return listed;
	}

	// Answers GET /games with the public list, filtered as asked.
	public IResult Games(HttpContext context) {
		if (!Limits.AllowGameList(AddressOf(context))) {
			return Results.StatusCode(StatusCodes.Status429TooManyRequests);
		}
		IQueryCollection query = context.Request.Query;
		string version = query[RelayProtocol.VersionFilter];
		GameList.Query asked = new(
			string.IsNullOrEmpty(version) ? null : version,
			IsTrue(query[RelayProtocol.NotFullFilter]),
			IsTrue(query[RelayProtocol.NoPasswordFilter]),
			int.TryParse(query[RelayProtocol.LimitFilter], out int limit) ? limit : null);
		(List<PublicGame> games, int total) = GameList.Choose(ListedGames(), asked, options.MaxPublicGames);
		return Results.Json(new PublicGameList(RelayProtocol.Version, games, total));
	}

	private static bool IsTrue(string value) => value == "1" || (bool.TryParse(value, out bool on) && on);

	// What /health reports.
	public object Health() {
		int hosts = 0, guests = 0;
		foreach (Room room in Rooms.All) {
			lock (room) {
				hosts += room.Host == null ? 0 : 1;
				guests += room.Guests.Count;
			}
		}
		return new { status = "ok", version = RelayProtocol.Version, rooms = Rooms.Count, hosts, guests, listed = ListedGames().Count };
	}

	public void Sweep() {
		foreach (string code in Rooms.Sweep()) {
			log.LogInformation("Room {Code} closed: its host didn't come back", code);
		}
		// Listings their hosts stopped listing again.
		long now = Environment.TickCount64;
		foreach (Room room in Rooms.All) {
			bool expired;
			lock (room) {
				expired = room.Listing != null && now - room.ListedAt >= options.ListingTtl.TotalMilliseconds;
				if (expired) {
					room.Listing = null;
				}
			}
			if (expired) {
				log.LogInformation("Room {Code} is no longer listed: its host stopped listing it", room.Code);
			}
		}
		Limits.Sweep();
	}
}

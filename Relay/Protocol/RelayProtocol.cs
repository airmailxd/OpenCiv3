using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace C7Relay;

// What an OpenCiv3 relay and the games using it say to each other. The game
// compiles this file too, so it uses nothing beyond the base library.
//
// A host connects a WebSocket to /host, and the relay answers with the join
// code of the host's new room and a key for claiming that room again
// (/host?code=...&key=...), as after losing the connection. A guest connects
// one to /join/{code}. Both say which version of this protocol they speak
// (?v=), and which version of the game's own protocol (?game=), which the
// relay only compares between a room's host and its guests.
//
// Binary messages carry the game's bytes, which the relay passes on without
// reading; text messages are RelayControl JSON. Between a guest and the
// relay, a binary message is bytes to or from the host, as they are. Between
// the host and the relay, a binary message starts with a header: its kind
// (one byte), how many guests it's about (two bytes), and their IDs (four
// bytes each, little-endian), then any payload:
//   Open (to the host): the guests connected.
//   Data: to the host, the payload came from the guest; from the host, the
//     payload goes to each of the guests, so the host sends bytes everyone
//     gets only once.
//   Close: the guests' connections closed (to the host), or the host closes
//     them (from the host).
// The relay pings everyone now and then, and they answer, so that it can
// drop whoever has gone quiet; they can tell the relay has gone if its pings
// stop. A connection the relay turns away or drops is closed with one of
// RelayCloseCodes and a reason to show the player.
//
// A host may also list its game publicly, for players to find in the game's
// server browser: it sends a RelayControl "list" with a GameListing, again
// whenever the game changes and at least every ListingRefreshSeconds, and
// "unlist" to take it down. The relay answers each with "listed", with an
// error if it wouldn't list the game. A listing goes when the host unlists
// it or leaves, or stops refreshing it. GET /games returns the listings
// (PublicGameList), and players join a game there with its code as usual.
//
// A guest takes one of the room's places from when it joins. A host that
// connects with ?admits=1 says "admit" with a guest's ID once it has let the
// guest into its game (as after its hello and password); when the room is
// full, the guest that has waited longest without being let in gives way to
// a newcomer, so that guests nobody let in can't keep out those coming back.
//
// A host can ban one of its guests: "ban" with the guest's ID closes its
// connection, and the relay turns away its address from the room from then
// on. The relay answers "banned" with a key standing for that address in
// this room, which only the relay can make; the host keeps it, and gives
// its keys back with "bans" whenever it connects, as after the relay
// restarts or when resuming a game. The relay also tells the host each
// guest's key as it joins ("guest", with its ID and key, before Open), and
// the host bans with the key as well as the ID ("ban" with bans: [key]):
// the key is banned even if the guest has gone, and only a guest with that
// key is closed, whatever its ID now stands for.
public static class RelayProtocol {
	// Bump when the messages change incompatibly.
	public const int Version = 1;

	// The oldest version the relay still speaks.
	public const int MinVersion = 1;

	public const string HostPath = "/host";
	public const string JoinPath = "/join/";
	public const string VersionParameter = "v";
	public const string GameVersionParameter = "game";
	public const string CodeParameter = "code";
	public const string KeyParameter = "key";
	public const string AdmitsParameter = "admits";

	// The public list of games, and its filters: games of this version of
	// the game only, those with open seats, those without a password, and
	// how many to return at most.
	public const string GamesPath = "/games";
	public const string HealthPath = "/health";
	public const string VersionFilter = "version";
	public const string NotFullFilter = "notFull";
	public const string NoPasswordFilter = "noPassword";
	public const string LimitFilter = "limit";

	// How long a game's name, description and the other text in its listing
	// may be; the relay cuts anything longer.
	public const int MaxGameNameLength = 48;
	public const int MaxGameDescriptionLength = 200;
	public const int MaxListingTextLength = 32;

	// How often a host lists its game again while nothing changes. The
	// relay drops a listing it hasn't heard of for a few times this.
	public const double ListingRefreshSeconds = 30;

	// Text messages larger than this aren't read.
	public const int MaxControlBytes = 16 * 1024;

	// The most ban keys a host gives back at once.
	public const int MaxBans = 256;

	public const byte Open = 1;
	public const byte Data = 2;
	public const byte Close = 3;

	// The largest message the relay takes by default: from a guest, whose
	// game only sends commands and the like (a path across a whole
	// 100x100 map is 39 KB; see GuestFrameSizeTest), and from a host, which
	// sends whole snapshots of the game (160 KB for a new 16-player game).
	public const int DefaultMaxGuestMessageBytes = 256 * 1024;
	public const int DefaultMaxHostMessageBytes = 8 * 1024 * 1024;

	// The most guests a message from the host can be for.
	public const int MaxGuestsPerMessage = 1024;

	// Join codes are this many characters from an alphabet without the ones
	// easily mistaken for each other (0 and O, 1 and I and L).
	public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
	public const int CodeLength = 6;

	// A join code as typed, in any case and with or without its dash or
	// spaces, as the relay knows it; or null if it can't be one.
	public static string NormalizeCode(string typed) {
		if (typed == null) {
			return null;
		}
		StringBuilder code = new(CodeLength);
		foreach (char c in typed) {
			if (c == '-' || char.IsWhiteSpace(c)) {
				continue;
			}
			char upper = char.ToUpperInvariant(c);
			if (CodeAlphabet.IndexOf(upper) < 0 || code.Length == CodeLength) {
				return null;
			}
			code.Append(upper);
		}
		return code.Length == CodeLength ? code.ToString() : null;
	}

	// A join code as shown to players, like KQ7-4MZ.
	public static string FormatCode(string code) {
		return code != null && code.Length == CodeLength ? $"{code[..3]}-{code[3..]}" : code;
	}

	public static int HeaderLength(int guests) => 3 + 4 * guests;

	// A message between the host and the relay.
	public static byte[] Encode(byte kind, IReadOnlyList<uint> guests, ReadOnlySpan<byte> payload = default) {
		if (guests.Count == 0 || guests.Count > MaxGuestsPerMessage) {
			throw new ArgumentException($"A message is for 1 to {MaxGuestsPerMessage} guests, not {guests.Count}");
		}
		byte[] message = new byte[HeaderLength(guests.Count) + payload.Length];
		message[0] = kind;
		BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(1), (ushort)guests.Count);
		for (int i = 0; i < guests.Count; ++i) {
			BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(3 + 4 * i), guests[i]);
		}
		payload.CopyTo(message.AsSpan(HeaderLength(guests.Count)));
		return message;
	}

	public static byte[] Encode(byte kind, uint guest, ReadOnlySpan<byte> payload = default) {
		return Encode(kind, [guest], payload);
	}

	// Reads the header of a message between the host and the relay: its
	// kind and how many guests it's about. False if it's too short for it.
	public static bool TryReadHeader(ReadOnlySpan<byte> message, out byte kind, out int guests) {
		kind = 0;
		guests = 0;
		if (message.Length < 3) {
			return false;
		}
		kind = message[0];
		guests = BinaryPrimitives.ReadUInt16LittleEndian(message[1..]);
		return guests > 0 && guests <= MaxGuestsPerMessage && message.Length >= HeaderLength(guests);
	}

	public static uint GuestAt(ReadOnlySpan<byte> message, int index) {
		return BinaryPrimitives.ReadUInt32LittleEndian(message[(3 + 4 * index)..]);
	}

	public static ReadOnlySpan<byte> Payload(ReadOnlySpan<byte> message, int guests) {
		return message[HeaderLength(guests)..];
	}
}

// Text from others, as the relay keeps what hosts list and the game keeps
// players' names.
public static class RelayText {
	// The text without control or formatting characters (which could reorder
	// or hide what's shown), with whitespace collapsed and trimmed, and cut
	// to the length; null if nothing is left.
	public static string Tidy(string text, int maxLength) {
		if (text == null) {
			return null;
		}
		StringBuilder tidy = new(Math.Min(text.Length, maxLength));
		bool space = false;
		foreach (char c in text) {
			if (char.IsWhiteSpace(c)) {
				space = tidy.Length > 0;
				continue;
			}
			UnicodeCategory category = char.GetUnicodeCategory(c);
			if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.OtherNotAssigned
				or UnicodeCategory.PrivateUse) {
				continue;
			}
			if (space) {
				tidy.Append(' ');
				space = false;
			}
			tidy.Append(c);
			if (tidy.Length >= maxLength) {
				break;
			}
		}
		// Don't leave half of a surrogate pair at the end.
		if (tidy.Length > 0 && char.IsHighSurrogate(tidy[^1])) {
			tidy.Length--;
		}
		return tidy.Length == 0 ? null : tidy.ToString();
	}
}

// Why the relay closed a connection, as its WebSocket close status. The
// reason that comes with it is for the player.
public static class RelayCloseCodes {
	// The relay doesn't speak the version of the protocol asked for.
	public const int UnsupportedVersion = 4000;
	public const int UnknownRoom = 4001;
	// The room's host isn't connected to the relay right now.
	public const int HostAway = 4002;
	public const int RoomFull = 4003;
	public const int TooManyAttempts = 4004;
	public const int WrongKey = 4005;
	// The guest runs another version of the game than the host.
	public const int GameVersionMismatch = 4006;
	public const int RelayFull = 4007;
	public const int ClosedByHost = 4008;
	// The host claimed its room again from another connection.
	public const int Replaced = 4009;
	public const int TimedOut = 4010;
	public const int TooSlow = 4011;
	public const int BadMessage = 4012;
	// The host banned this guest from its game.
	public const int Banned = 4013;
	// The guest sent the host more, or faster, than the relay passes on.
	public const int TooMuch = 4014;
}

// A text message between the relay and a host or guest. The relay welcomes
// a host with its room's code and key, and everyone with how often it pings.
// The rest are for listing a game publicly and banning guests (see
// RelayProtocol): listing is the game, error why it wasn't listed, guest the
// guest to ban, and bans the keys of the addresses banned.
public sealed record RelayControl(string type, string code = null, string key = null, double pingSeconds = 0,
	GameListing listing = null, string error = null, uint guest = 0, List<string> bans = null) {
	public const string Welcome = "welcome";
	public const string Ping = "ping";
	public const string Pong = "pong";
	public const string List = "list";
	public const string Unlist = "unlist";
	public const string Listed = "listed";
	public const string Ban = "ban";
	public const string Banned = "banned";
	public const string Bans = "bans";
	public const string Admit = "admit";
	public const string Guest = "guest";

	private static readonly JsonSerializerOptions Options = new() {
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
	};

	public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this, Options);

	// The message, or null if it isn't one.
	public static RelayControl Parse(ReadOnlySpan<byte> json) {
		if (json.Length > RelayProtocol.MaxControlBytes) {
			return null;
		}
		try {
			return JsonSerializer.Deserialize<RelayControl>(json);
		} catch (JsonException) {
			return null;
		}
	}
}

// What a host says of its game in the public list. seatsTotal counts every
// human player, the host's own included, and seatsTaken those someone has
// taken or is coming back to. turn is the game's turn, in the lobby too for
// a saved game. turnSeconds is how long each turn may take, or null for no
// limit; hideUnseen whether guests are sent only what their players may
// know. locale is like "en-US", a hint at where the host is.
public sealed record GameListing(string name, string description = null, bool hasPassword = false,
	int seatsTotal = 0, int seatsTaken = 0, bool spectatorsAllowed = true, int turn = 0, bool started = false,
	string mapSize = null, bool simultaneousTurns = false, double? turnSeconds = null, bool hideUnseen = true,
	string hostName = null, string locale = null);

// A game in the public list: its join code, the game's version (as the host
// connected with it, which guests must match), its listing with the
// relay's own seatsOpen, and how long the host takes to answer the relay's
// pings, in milliseconds, or null until it has.
public sealed record PublicGame(string code, string gameVersion, GameListing game, int seatsOpen, int? hostPingMs = null);

// What GET /games returns: the games, up to the limit, and how many matched
// in all.
public sealed record PublicGameList(int relayVersion, List<PublicGame> games, int total);

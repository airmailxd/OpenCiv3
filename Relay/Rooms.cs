using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace C7Relay;

// A host's room: its join code, the host while it's connected, and the
// guests that joined it through the relay.
internal sealed class Room {
	public string Code { get; }

	// What the host said of the game's version, which its guests must match.
	public string GameVersion;

	// Everything else is guarded by locking the room.
	public RelayPeer Host;
	public long HostLeftAt = Environment.TickCount64;
	public readonly Dictionary<uint, RelayPeer> Guests = new();

	// Guest IDs aren't reused in a room, so a message for a guest that has
	// gone can't reach one that came after it.
	public uint NextGuestId = 1;

	// The game as the host lists it publicly, already tidied, with when the
	// host last listed it and from where; null while it isn't listed.
	public GameListing Listing;
	public long ListedAt;
	public string ListedFrom;

	// The keys of the addresses the host has banned (see
	// RoomRegistry.BanKeyFor).
	public readonly HashSet<string> Bans = new();

	// The addresses of the guests that left lately, so that the host can ban
	// one that has gone, oldest first.
	public readonly Dictionary<uint, string> RecentGuests = new();
	public readonly Queue<uint> RecentGuestOrder = new();
	public const int MaxRecentGuests = 64;

	public void NoteGuestLeft(uint id, string address) {
		if (RecentGuests.TryAdd(id, address)) {
			RecentGuestOrder.Enqueue(id);
		}
		while (RecentGuestOrder.Count > MaxRecentGuests) {
			RecentGuests.Remove(RecentGuestOrder.Dequeue());
		}
	}

	public Room(string code, string gameVersion) {
		Code = code;
		GameVersion = gameVersion;
	}
}

// The rooms, by join code. A room lives while its host is connected, and for
// a while after, for the host to come back to; and a host with the key to a
// code can claim it again even after its room is gone.
internal sealed class RoomRegistry {
	private readonly ConcurrentDictionary<string, Room> rooms = new();
	private readonly RelayOptions options;
	private readonly byte[] keySecret;

	public RoomRegistry(RelayOptions options) {
		this.options = options;
		keySecret = string.IsNullOrEmpty(options.KeySecret)
			? RandomNumberGenerator.GetBytes(32)
			: Encoding.UTF8.GetBytes(options.KeySecret);
	}

	public int Count => rooms.Count;

	// How many rooms are listed publicly from the address.
	public int ListedFrom(string address) {
		int count = 0;
		foreach (Room room in rooms.Values) {
			lock (room) {
				if (room.Listing != null && room.ListedFrom == address) {
					++count;
				}
			}
		}
		return count;
	}

	public IEnumerable<Room> All => rooms.Values;

	// The key to a code, which only the relay can make: it's the code
	// signed with the relay's secret.
	public string KeyFor(string code) {
		byte[] mac = HMACSHA256.HashData(keySecret, Encoding.UTF8.GetBytes(code));
		return Convert.ToHexString(mac, 0, 16).ToLowerInvariant();
	}

	public bool IsKeyFor(string code, string key) {
		if (key == null) {
			return false;
		}
		return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(KeyFor(code)), Encoding.UTF8.GetBytes(key));
	}

	// The key standing for a guest's address in a room's bans, which only
	// the relay can make, so the host never learns the address.
	public string BanKeyFor(string code, string address) {
		byte[] mac = HMACSHA256.HashData(keySecret, Encoding.UTF8.GetBytes($"ban:{code}:{address}"));
		return Convert.ToHexString(mac, 0, 16).ToLowerInvariant();
	}

	public bool TryGet(string code, out Room room) => rooms.TryGetValue(code, out room);

	// A new room with an unused code, or null if the relay has all the rooms
	// it can take.
	public Room Create(string gameVersion) {
		if (rooms.Count >= options.MaxRooms) {
			return null;
		}
		while (true) {
			Room room = new(NewCode(), gameVersion);
			if (rooms.TryAdd(room.Code, room)) {
				return room;
			}
		}
	}

	// The room with the code, made again if it has gone; or null if it has
	// and the relay is full. Check the key first.
	public Room Reclaim(string code, string gameVersion) {
		if (rooms.TryGetValue(code, out Room room)) {
			return room;
		}
		if (rooms.Count >= options.MaxRooms) {
			return null;
		}
		return rooms.GetOrAdd(code, _ => new Room(code, gameVersion));
	}

	// Forgets the rooms whose host has been gone too long.
	public List<string> Sweep() {
		long now = Environment.TickCount64;
		List<string> expired = [];
		foreach (Room room in rooms.Values) {
			lock (room) {
				if (room.Host != null || room.Guests.Count > 0 || now - room.HostLeftAt < options.RoomTtl.TotalMilliseconds) {
					continue;
				}
				rooms.TryRemove(new KeyValuePair<string, Room>(room.Code, room));
			}
			expired.Add(room.Code);
		}
		return expired;
	}

	private static string NewCode() {
		return string.Create(RelayProtocol.CodeLength, 0, (code, _) => {
			for (int i = 0; i < code.Length; ++i) {
				code[i] = RelayProtocol.CodeAlphabet[RandomNumberGenerator.GetInt32(RelayProtocol.CodeAlphabet.Length)];
			}
		});
	}
}

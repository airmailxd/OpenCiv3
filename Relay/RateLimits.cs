using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace C7Relay;

// Counts what each client address does in fixed windows of time, to turn
// away an address that connects too often, makes too many rooms, tries too
// many codes that aren't anyone's, or asks for the list of games too often.
internal sealed class RateLimits {
	private sealed class Window {
		public long start;
		public int count;
	}

	private readonly RelayOptions options;
	private readonly ConcurrentDictionary<string, Window> connections = new();
	private readonly ConcurrentDictionary<string, Window> roomsMade = new();
	private readonly ConcurrentDictionary<string, Window> failedJoins = new();
	private readonly ConcurrentDictionary<string, Window> listRequests = new();

	// The connections open from each address, and in all.
	private readonly ConcurrentDictionary<string, int> open = new();
	private int openInAll;

	private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
	private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

	public RateLimits(RelayOptions options) {
		this.options = options;
	}

	// An address as the relay counts it: IPv4 as it is, and IPv6 by its
	// /64, since one client usually has a whole /64 to pick addresses from.
	public static string KeyOf(IPAddress address) {
		if (address == null) {
			return "unknown";
		}
		if (address.IsIPv4MappedToIPv6) {
			address = address.MapToIPv4();
		}
		if (address.AddressFamily != AddressFamily.InterNetworkV6 || IPAddress.IsLoopback(address)) {
			return address.ToString();
		}
		byte[] bytes = address.GetAddressBytes();
		Array.Clear(bytes, 8, 8);
		return $"{new IPAddress(bytes)}/64";
	}

	// Counts a connection, and says whether to allow it.
	public bool AllowConnection(string address) => Take(connections, address, Minute, options.ConnectionsPerMinute);

	// Counts a room made, and says whether to allow it.
	public bool AllowRoom(string address) => Take(roomsMade, address, Hour, options.RoomsPerHour);

	// Counts a request for the public list of games, and says whether to
	// answer it.
	public bool AllowGameList(string address) => Take(listRequests, address, Minute, options.GameListRequestsPerMinute);

	// Whether the address has tried too many wrong codes or keys lately.
	public bool IsLockedOut(string address) => Count(failedJoins, address, TenMinutes) >= options.FailedJoinsPerTenMinutes;

	public void NoteFailedJoin(string address) => Take(failedJoins, address, TenMinutes, int.MaxValue);

	public enum Opening { Allowed, TooManyFromAddress, TooManyInAll }

	// Counts a connection open, if there's room for it: call Closed once it
	// closes.
	public Opening Open(string address) {
		if (Interlocked.Increment(ref openInAll) > options.MaxConnections) {
			Interlocked.Decrement(ref openInAll);
			return Opening.TooManyInAll;
		}
		bool allowed = true;
		open.AddOrUpdate(address, 1, (_, count) => {
			allowed = count < options.MaxConnectionsPerAddress;
			return allowed ? count + 1 : count;
		});
		if (!allowed) {
			Interlocked.Decrement(ref openInAll);
			return Opening.TooManyFromAddress;
		}
		return Opening.Allowed;
	}

	public void Closed(string address) {
		Interlocked.Decrement(ref openInAll);
		// The address is forgotten once it has nothing open.
		while (open.TryGetValue(address, out int count)) {
			if (count <= 1 ? open.TryRemove(new KeyValuePair<string, int>(address, count)) : open.TryUpdate(address, count - 1, count)) {
				return;
			}
		}
	}

	public int OpenFrom(string address) => open.TryGetValue(address, out int count) ? count : 0;

	private static bool Take(ConcurrentDictionary<string, Window> windows, string address, TimeSpan length, int limit) {
		Window window = windows.GetOrAdd(address, _ => new Window { start = Environment.TickCount64 });
		lock (window) {
			long now = Environment.TickCount64;
			if (now - window.start >= length.TotalMilliseconds) {
				window.start = now;
				window.count = 0;
			}
			if (window.count >= limit) {
				return false;
			}
			++window.count;
			return true;
		}
	}

	private static int Count(ConcurrentDictionary<string, Window> windows, string address, TimeSpan length) {
		if (!windows.TryGetValue(address, out Window window)) {
			return 0;
		}
		lock (window) {
			return Environment.TickCount64 - window.start >= length.TotalMilliseconds ? 0 : window.count;
		}
	}

	// Forgets the windows that have ended.
	public void Sweep() {
		Sweep(connections, Minute);
		Sweep(roomsMade, Hour);
		Sweep(failedJoins, TenMinutes);
		Sweep(listRequests, Minute);
	}

	private static void Sweep(ConcurrentDictionary<string, Window> windows, TimeSpan length) {
		long now = Environment.TickCount64;
		foreach ((string address, Window window) in windows) {
			if (now - StartOf(window) >= length.TotalMilliseconds) {
				windows.TryRemove(address, out _);
			}
		}
	}

	private static long StartOf(Window window) {
		lock (window) {
			return window.start;
		}
	}
}

// What one connection may do over time: up to burst at once, refilling at
// perSecond. Used from one thread at a time.
internal sealed class TokenBucket(double perSecond, double burst) {
	private double tokens = burst;
	private long refilledAt = Environment.TickCount64;

	// Takes the amount if there's that much left, and says whether it did.
	public bool Take(double amount) {
		long now = Environment.TickCount64;
		tokens = Math.Min(burst, tokens + (now - refilledAt) / 1000.0 * perSecond);
		refilledAt = now;
		if (tokens < amount) {
			return false;
		}
		tokens -= amount;
		return true;
	}
}

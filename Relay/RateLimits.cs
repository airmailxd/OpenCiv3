using System;
using System.Collections.Concurrent;

namespace C7Relay;

// Counts what each client address does in fixed windows of time, to turn
// away an address that connects too often, makes too many rooms, or tries
// too many codes that aren't anyone's.
internal sealed class RateLimits {
	private sealed class Window {
		public long start;
		public int count;
	}

	private readonly RelayOptions options;
	private readonly ConcurrentDictionary<string, Window> connections = new();
	private readonly ConcurrentDictionary<string, Window> roomsMade = new();
	private readonly ConcurrentDictionary<string, Window> failedJoins = new();

	private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
	private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

	public RateLimits(RelayOptions options) {
		this.options = options;
	}

	// Counts a connection, and says whether to allow it.
	public bool AllowConnection(string address) => Take(connections, address, Minute, options.ConnectionsPerMinute);

	// Counts a room made, and says whether to allow it.
	public bool AllowRoom(string address) => Take(roomsMade, address, Hour, options.RoomsPerHour);

	// Whether the address has tried too many wrong codes or keys lately.
	public bool IsLockedOut(string address) => Count(failedJoins, address, TenMinutes) >= options.FailedJoinsPerTenMinutes;

	public void NoteFailedJoin(string address) => Take(failedJoins, address, TenMinutes, int.MaxValue);

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

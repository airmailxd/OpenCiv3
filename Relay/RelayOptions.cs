using System;

namespace C7Relay;

// The relay's settings, from the "Relay" section of appsettings.json or
// environment variables like Relay__MaxRooms=500.
public sealed class RelayOptions {
	public const string Section = "Relay";

	// Larger messages close the connection. A whole snapshot of a big game
	// is a few megabytes; most messages are a few kilobytes.
	public int MaxMessageBytes { get; set; } = 16 * 1024 * 1024;

	// A connection with this much waiting to be sent to it isn't keeping up,
	// and is dropped.
	public long MaxQueuedBytes { get; set; } = 64L * 1024 * 1024;

	public int MaxRooms { get; set; } = 1000;
	public int MaxGuestsPerRoom { get; set; } = 16;

	// How long a room waits for its host to come back, after which its code
	// is free.
	public double RoomTtlSeconds { get; set; } = 600;

	// How often everyone is pinged, and how long a connection may go without
	// a word before it's dropped.
	public double PingIntervalSeconds { get; set; } = 15;
	public double IdleTimeoutSeconds { get; set; } = 45;

	// Limits for each client address: connections a minute, rooms created an
	// hour, and joins with a code no room has (or a wrong key) in ten
	// minutes, after which it can't join for the rest of those ten minutes.
	public int ConnectionsPerMinute { get; set; } = 120;
	public int RoomsPerHour { get; set; } = 30;
	public int FailedJoinsPerTenMinutes { get; set; } = 20;

	// The secret a room's key is made from, so that a host can claim its
	// code again even after the relay restarts. Set it to a long random
	// string; when empty, a new one is made each time the relay starts.
	public string KeySecret { get; set; } = "";

	// Behind a reverse proxy like Caddy, take the client's address from the
	// X-Forwarded-For header it adds. Only turn this on when the relay can
	// only be reached through the proxy.
	public bool TrustForwardedHeaders { get; set; }

	// How often rooms whose host is gone too long, and old rate-limit
	// counts, are cleared away.
	public double SweepIntervalSeconds { get; set; } = 30;

	internal TimeSpan RoomTtl => TimeSpan.FromSeconds(RoomTtlSeconds);
	internal TimeSpan PingInterval => TimeSpan.FromSeconds(PingIntervalSeconds);
	internal TimeSpan IdleTimeout => TimeSpan.FromSeconds(IdleTimeoutSeconds);
}

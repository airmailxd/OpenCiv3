using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using C7Relay;

namespace C7Engine.Network;

// Something went wrong with an online relay: it couldn't be reached, or it
// turned us away, saying why (see RelayCloseCodes).
public class RelayException : IOException {
	// The relay's reason for closing the connection, or null if it didn't
	// give one.
	public int? CloseCode { get; }

	public RelayException(string message, int? closeCode = null, Exception inner = null) : base(message, inner) {
		CloseCode = closeCode;
	}

	// Whether trying again can't help.
	public bool IsPermanent => CloseCode is RelayCloseCodes.WrongKey or RelayCloseCodes.UnsupportedVersion
		or RelayCloseCodes.Replaced or RelayCloseCodes.GameVersionMismatch or RelayCloseCodes.Banned;

}

// Connecting to an online relay (see RelayProtocol), for a host or a guest.
public static class RelayConnection {
	// How long a connection waits to hear from the relay, which pings every
	// so often, before taking it to have gone: a few missed pings.
	internal static TimeSpan QuietLimit(double pingSeconds) {
		return TimeSpan.FromSeconds((pingSeconds > 0 ? pingSeconds : 20) * 3 + 5);
	}

	// The relay's address as a WebSocket URL, taking http(s) for ws(s), and
	// wss when there's no scheme. Null if it isn't an address.
	public static Uri BaseUri(string relayUrl) {
		string url = relayUrl?.Trim();
		if (string.IsNullOrEmpty(url)) {
			return null;
		}
		if (!url.Contains("://")) {
			url = "wss://" + url;
		}
		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) {
			return null;
		}
		string scheme = uri.Scheme switch {
			"https" or "wss" => "wss",
			"http" or "ws" => "ws",
			_ => null,
		};
		if (scheme == null) {
			return null;
		}
		UriBuilder builder = new(uri) { Scheme = scheme };
		// Keep the port when it was given, which UriBuilder drops when the
		// scheme changes.
		builder.Port = uri.IsDefaultPort ? -1 : uri.Port;
		builder.Path = builder.Path.TrimEnd('/');
		builder.Query = "";
		return builder.Uri;
	}

	private static Uri WithPath(string relayUrl, string path, string query) {
		Uri relay = BaseUri(relayUrl) ?? throw new RelayException($"\"{relayUrl}\" isn't the address of a relay.");
		UriBuilder builder = new(relay) {
			Path = relay.AbsolutePath.TrimEnd('/') + path,
			Query = $"{RelayProtocol.VersionParameter}={RelayProtocol.Version}&{RelayProtocol.GameVersionParameter}={LanProtocol.Version}{query}",
		};
		return builder.Uri;
	}

	// Where a host opens a room, or claims its room again with its code and
	// key.
	public static Uri HostUri(string relayUrl, string code = null, string key = null) {
		string claim = code == null ? ""
			: $"&{RelayProtocol.CodeParameter}={Uri.EscapeDataString(code)}&{RelayProtocol.KeyParameter}={Uri.EscapeDataString(key ?? "")}";
		// This host says which guests it has let in.
		claim += $"&{RelayProtocol.AdmitsParameter}=1";
		return WithPath(relayUrl, RelayProtocol.HostPath, claim);
	}

	// Where a guest joins the room with the code.
	public static Uri JoinUri(string relayUrl, string code) {
		return WithPath(relayUrl, RelayProtocol.JoinPath + Uri.EscapeDataString(code), "");
	}

	// Connects to the relay and waits for its welcome. Throws a
	// RelayException if the relay can't be reached or turns us away, and a
	// TimeoutException if it doesn't answer in time.
	public static async Task<(ClientWebSocket, RelayControl)> ConnectAsync(Uri uri, TimeSpan timeout, CancellationToken cancel) {
		ClientWebSocket socket = new();
		socket.Options.CollectHttpResponseDetails = true;
		// The relay's pings keep the connection alive.
		socket.Options.KeepAliveInterval = TimeSpan.Zero;
		using CancellationTokenSource timer = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		timer.CancelAfter(timeout);
		string relay = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
		try {
			try {
				await socket.ConnectAsync(uri, timer.Token).ConfigureAwait(false);
			} catch (WebSocketException e) when (socket.HttpStatusCode == HttpStatusCode.TooManyRequests) {
				throw new RelayException("The relay has had too many tries from your address. Wait a minute, then try again.",
					RelayCloseCodes.TooManyAttempts, e);
			} catch (WebSocketException e) when (socket.HttpStatusCode == HttpStatusCode.ServiceUnavailable) {
				throw new RelayException("The relay is too busy right now. Try again later.", RelayCloseCodes.RelayFull, e);
			} catch (Exception e) when (e is WebSocketException or HttpRequestException) {
				string why = e.InnerException?.Message ?? e.Message;
				throw new RelayException($"Couldn't reach the relay at {relay}: {why}", null, e);
			}
			WebSocketMessageReader reader = new();
			while (true) {
				(WebSocketMessageType type, ReadOnlyMemory<byte> message) = await reader.ReadAsync(socket, 64 * 1024, timer.Token).ConfigureAwait(false);
				if (type == WebSocketMessageType.Close) {
					throw ClosedBy(socket);
				}
				if (type == WebSocketMessageType.Text && RelayControl.Parse(message.Span) is RelayControl control
					&& control.type == RelayControl.Welcome) {
					return (socket, control);
				}
			}
		} catch (OperationCanceledException) when (!cancel.IsCancellationRequested) {
			socket.Dispose();
			throw new TimeoutException($"The relay at {relay} didn't answer within {timeout.TotalSeconds:0} seconds.");
		} catch (Exception e) when (e is WebSocketException or IOException) {
			socket.Dispose();
			throw e as RelayException ?? new RelayException($"Lost the connection to the relay at {relay}: {e.Message}", null, e);
		} catch {
			socket.Dispose();
			throw;
		}
	}

	// Why the relay closed the connection.
	internal static RelayException ClosedBy(WebSocket socket) {
		string reason = string.IsNullOrWhiteSpace(socket.CloseStatusDescription) ? "The relay closed the connection." : socket.CloseStatusDescription;
		return new RelayException(reason, (int?)socket.CloseStatus);
	}
}

// Reads whole messages from a WebSocket, into a buffer that grows as needed.
internal sealed class WebSocketMessageReader {
	private byte[] buffer = new byte[16 * 1024];

	// The next message, valid until the next read; or a Close with no
	// message once the other side closes. A message larger than maxBytes
	// breaks the connection.
	public async Task<(WebSocketMessageType, ReadOnlyMemory<byte>)> ReadAsync(WebSocket socket, int maxBytes, CancellationToken cancel) {
		int length = 0;
		while (true) {
			if (length == buffer.Length) {
				if (length >= maxBytes) {
					throw new IOException($"The relay sent a message larger than {maxBytes} bytes");
				}
				Array.Resize(ref buffer, (int)Math.Min((long)buffer.Length * 2, maxBytes));
			}
			ValueWebSocketReceiveResult result = await socket.ReceiveAsync(buffer.AsMemory(length), cancel).ConfigureAwait(false);
			if (result.MessageType == WebSocketMessageType.Close) {
				return (WebSocketMessageType.Close, ReadOnlyMemory<byte>.Empty);
			}
			length += result.Count;
			if (result.EndOfMessage) {
				return (result.MessageType, buffer.AsMemory(0, length));
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace C7Engine.Network;

// The other machines on this player's Tailscale network, asked of the
// Tailscale app. Tailscale doesn't carry broadcasts, so discovery asks each
// of them directly instead (owner's report, 2026-10-28: friends on the
// tailnet only found a hosted game by typing its 100.x address).
public static class TailnetPeers {
	private static readonly ILogger log = Log.ForContext(typeof(TailnetPeers));

	// Plenty for a group of friends; a big company tailnet isn't asked
	// all at once.
	public const int MaxPeers = 64;

	// The online peers' Tailscale IPv4 addresses, or none when Tailscale
	// isn't installed, isn't running or doesn't answer in time.
	public static async Task<List<IPAddress>> FindAsync(TimeSpan timeout) {
		using CancellationTokenSource cancel = new(timeout);
		foreach (string cli in CliCandidates()) {
			string json;
			try {
				json = await RunStatus(cli, cancel.Token);
			} catch (Exception e) when (e is Win32Exception or FileNotFoundException or InvalidOperationException) {
				// Not there; try the next place it may be.
				continue;
			} catch (OperationCanceledException) {
				log.Information("Tailscale didn't list its peers within {Seconds}s", timeout.TotalSeconds);
				return [];
			} catch (Exception e) {
				log.Information("Couldn't ask Tailscale for its peers: {Error}", e.Message);
				return [];
			}
			if (json == null) {
				return [];
			}
			List<IPAddress> peers = ParseStatus(json);
			log.Debug("Tailscale has {Count} online peers to look for games on", peers.Count);
			return peers;
		}
		log.Debug("No Tailscale command line found");
		return [];
	}

	// Where the tailscale command may be. A game started from the desktop
	// may not have Homebrew's or the like on its PATH, so the usual places
	// are tried by name too.
	private static IEnumerable<string> CliCandidates() {
		if (OperatingSystem.IsWindows()) {
			string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			yield return Path.Combine(string.IsNullOrEmpty(programFiles) ? @"C:\Program Files" : programFiles, "Tailscale", "tailscale.exe");
			yield return "tailscale.exe";
			yield break;
		}
		yield return "tailscale";
		if (OperatingSystem.IsMacOS()) {
			yield return "/Applications/Tailscale.app/Contents/MacOS/Tailscale";
			yield return "/opt/homebrew/bin/tailscale";
		}
		yield return "/usr/local/bin/tailscale";
		yield return "/usr/bin/tailscale";
	}

	// The output of "tailscale status --json", or null when it failed (as
	// when Tailscale is installed but logged out or stopped).
	private static async Task<string> RunStatus(string cli, CancellationToken cancel) {
		if (Path.IsPathRooted(cli) && !File.Exists(cli)) {
			throw new FileNotFoundException(cli);
		}
		using Process process = new() {
			StartInfo = new ProcessStartInfo(cli, "status --json") {
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			},
		};
		process.Start();
		try {
			Task<string> output = process.StandardOutput.ReadToEndAsync(cancel);
			Task<string> errors = process.StandardError.ReadToEndAsync(cancel);
			await process.WaitForExitAsync(cancel);
			string json = await output;
			if (process.ExitCode != 0) {
				log.Information("Tailscale isn't connected: {Error}", (await errors).Trim());
				return null;
			}
			return json;
		} catch (OperationCanceledException) {
			try {
				process.Kill(entireProcessTree: true);
			} catch (Exception e) when (e is InvalidOperationException or Win32Exception) {
				// Finished meanwhile.
			}
			throw;
		}
	}

	// The online peers' IPv4 addresses on the tailnet (100.64.0.0/10), from
	// what "tailscale status --json" prints. Its Peer map is keyed by node
	// key; each peer has TailscaleIPs (IPv4 and IPv6), HostName and Online.
	public static List<IPAddress> ParseStatus(string json) {
		List<IPAddress> addresses = [];
		JsonDocument document;
		try {
			document = JsonDocument.Parse(json);
		} catch (JsonException) {
			return addresses;
		}
		using (document) {
			JsonElement root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object) {
				return addresses;
			}
			// Logged out or stopped, peers listed from before can't be reached.
			if (root.TryGetProperty("BackendState", out JsonElement state) && state.ValueKind == JsonValueKind.String
				&& state.GetString() != "Running") {
				return addresses;
			}
			if (!root.TryGetProperty("Peer", out JsonElement peers) || peers.ValueKind != JsonValueKind.Object) {
				return addresses;
			}
			foreach (JsonProperty peer in peers.EnumerateObject()) {
				JsonElement node = peer.Value;
				if (node.ValueKind != JsonValueKind.Object
					|| !node.TryGetProperty("Online", out JsonElement online) || online.ValueKind != JsonValueKind.True) {
					continue;
				}
				if (!node.TryGetProperty("TailscaleIPs", out JsonElement ips) || ips.ValueKind != JsonValueKind.Array) {
					continue;
				}
				foreach (JsonElement ip in ips.EnumerateArray()) {
					if (ip.ValueKind == JsonValueKind.String && IPAddress.TryParse(ip.GetString(), out IPAddress address)
						&& LanDiscovery.IsTailscaleAddress(address) && !addresses.Contains(address)) {
						addresses.Add(address);
					}
				}
				if (addresses.Count >= MaxPeers) {
					break;
				}
			}
		}
		return addresses;
	}
}

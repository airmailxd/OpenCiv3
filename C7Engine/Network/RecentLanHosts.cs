using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace C7Engine.Network;

// The addresses of the hosts this player has joined, kept in the settings so
// that looking for games asks them directly too: a host reached over a
// virtual network, which carries no broadcasts, is then listed the next time
// without typing its address again.
public static class RecentLanHosts {
	public const string SettingsSection = "RecentLanHosts";
	private const string SettingsKey = "Addresses";
	public const int MaxAddresses = 8;

	public static List<string> Load() {
		return Parse(C7Settings.GetSettingsValueOrDefault(SettingsSection, SettingsKey, ""));
	}

	// Remembers a host joined at that address, most recent first.
	public static void Remember(string address) {
		List<string> before = Load();
		List<string> after = Add(before, address);
		if (after.SequenceEqual(before)) {
			return;
		}
		C7Settings.SetValue(SettingsSection, SettingsKey, Format(after));
		C7Settings.SaveSettings();
	}

	public static List<string> Parse(string value) {
		return (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Take(MaxAddresses)
			.ToList();
	}

	public static string Format(IEnumerable<string> addresses) {
		return string.Join(",", addresses);
	}

	// The list with the address put first. This machine's own address isn't
	// kept, since looking for games always asks it.
	public static List<string> Add(IEnumerable<string> addresses, string address) {
		address = address?.Trim();
		if (string.IsNullOrEmpty(address) || address.Contains(',')
			|| (IPAddress.TryParse(address, out IPAddress ip) && IPAddress.IsLoopback(ip))
			|| address.Equals("localhost", StringComparison.OrdinalIgnoreCase)) {
			return addresses.ToList();
		}
		return addresses.Where(a => !a.Equals(address, StringComparison.OrdinalIgnoreCase))
			.Prepend(address)
			.Take(MaxAddresses)
			.ToList();
	}
}

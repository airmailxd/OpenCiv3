using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace QueryCiv3 {
	public class Civ3Location {
		public static readonly string RegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Infogrames Interactive\Civilization III";
		private const string SteamRegistryKey = @"HKEY_CURRENT_USER\Software\Valve\Steam";

		// What GetCiv3Path returns when no install is found. Callers combine
		// the result with relative paths and check whether the files exist,
		// so this is a path that won't exist rather than null. Use
		// TryGetCiv3Path to tell "not found" apart.
		public const string NotFoundPath = "/civ3/path/not/found";

		// The Steam installation folders to look in, most likely first.
		private static IEnumerable<string> SteamRoots() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				string fromRegistry = null;
				try {
					fromRegistry = Microsoft.Win32.Registry.GetValue(SteamRegistryKey, "SteamPath", null) as string;
				} catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException) {
				}
				if (!string.IsNullOrEmpty(fromRegistry)) {
					yield return fromRegistry;
				}
				yield return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
				yield return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");
				yield break;
			}

			string home = GetHome();
			if (string.IsNullOrEmpty(home)) {
				yield break;
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				yield return Path.Combine(home, "Library/Application Support/Steam");
			} else {
				yield return Path.Combine(home, ".steam/steam");
				yield return Path.Combine(home, ".local/share/Steam");
				yield return Path.Combine(home, ".steam/root");
				// The Flatpak build of Steam.
				yield return Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam");
			}
		}

		// Every steamapps/common folder: each Steam installation's own, plus
		// the extra libraries listed in its libraryfolders.vdf.
		private static IEnumerable<string> SteamCommonDirs() {
			HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
			foreach (string root in SteamRoots()) {
				foreach (string library in SteamLibraries(root)) {
					string common = Path.Combine(library, "steamapps", "common");
					if (seen.Add(Path.GetFullPath(common))) {
						yield return common;
					}
				}
			}
		}

		private static IEnumerable<string> SteamLibraries(string steamRoot) {
			yield return steamRoot;
			foreach (string vdf in new[] { Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"), Path.Combine(steamRoot, "config", "libraryfolders.vdf") }) {
				string text;
				try {
					if (!File.Exists(vdf)) {
						continue;
					}
					text = File.ReadAllText(vdf);
				} catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
					continue;
				}
				foreach (string library in ParseLibraryFolders(text)) {
					yield return library;
				}
			}
		}

		// Library paths from a libraryfolders.vdf, which lists them as
		//   "path"		"D:\\SteamLibrary"
		// (or, in older files, as "1"		"D:\\SteamLibrary"), with
		// backslashes escaped.
		internal static IEnumerable<string> ParseLibraryFolders(string vdf) {
			foreach (Match m in Regex.Matches(vdf, "^\\s*\"(path|\\d+)\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"\\s*$", RegexOptions.Multiline)) {
				string value = m.Groups[2].Value.Replace("\\\\", "\\");
				// Old files list numbered libraries directly; anything that
				// isn't a path (e.g. a numbered sub-block) is skipped.
				if (value.Length > 0 && (value.Contains('/') || value.Contains('\\'))) {
					yield return value;
				}
			}
		}

		private static string GetHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

		private static bool FolderIsCiv3(DirectoryInfo di) {
			try {
				return di.EnumerateFiles().Any(f => f.Name.Equals("civ3id.mb", StringComparison.OrdinalIgnoreCase));
			} catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
				return false;
			}
		}

		private static string ConvertUnixVarsToWindowsVars(string input) {
			if (string.IsNullOrEmpty(input)) return input;

			// Replace all instances of $VARIABLE with %VARIABLE%
			return Regex.Replace(input, @"\$(\w+)", match => $"%{match.Groups[1].Value}%");
		}

		private static string GetExpandedPath(string path) {
			bool isUnixLike = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
			if (isUnixLike && path[0] == '~') path = GetHome() + path.Substring(1);
			if (isUnixLike) path = ConvertUnixVarsToWindowsVars(path);
			path = Environment.ExpandEnvironmentVariables(path);
			path = Path.GetFullPath(path);
			return path;
		}

		// The Civ3 install folder, or NotFoundPath when there isn't one.
		public static string GetCiv3Path() {
			return TryGetCiv3Path(out string path) ? path : NotFoundPath;
		}

		// Finds the Civ3 install folder: CIV3_HOME if set (even if it doesn't
		// exist, so a wrong setting shows up as missing files rather than
		// another install being picked), then the Windows registry, then
		// every Steam library.
		public static bool TryGetCiv3Path(out string path) {
			string fromEnvironment = Environment.GetEnvironmentVariable("CIV3_HOME");
			if (!string.IsNullOrWhiteSpace(fromEnvironment)) {
				try {
					path = GetExpandedPath(fromEnvironment.Trim());
					return true;
				} catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) {
					// Not a usable path; look elsewhere.
				}
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
				// Look up in Windows registry if present
				string fromRegistry = null;
				try {
					fromRegistry = Microsoft.Win32.Registry.GetValue(RegistryKey, "install_path", "") as string;
				} catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException) {
				}
				// The registry entry outlives an uninstall or a moved install.
				if (!string.IsNullOrEmpty(fromRegistry) && Directory.Exists(fromRegistry)) {
					path = fromRegistry;
					return true;
				}
			}

			// Check for a civ3 folder in each steamapps/common
			foreach (string steam in SteamCommonDirs()) {
				DirectoryInfo root = new(steam);
				DirectoryInfo[] candidates;
				try {
					if (!root.Exists) {
						continue;
					}
					candidates = root.GetDirectories();
				} catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
					continue;
				}
				foreach (DirectoryInfo di in candidates) {
					if (FolderIsCiv3(di)) {
						path = di.FullName;
						return true;
					}
				}
			}

			path = null;
			return false;
		}
	}
}

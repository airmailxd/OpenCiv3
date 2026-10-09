using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QueryCiv3;

namespace EngineTests.Utils;

public static class Civ3TestData {
	private static readonly string[] DefaultRequiredFiles = {
		"Conquests/conquests.biq",
		"Conquests/Text/PediaIcons.txt",
	};

	// Whether to skip the tests that read Civ3's own files: they are skipped
	// when those files can't be found (via CIV3_HOME, the registry or Steam),
	// as on CI, where Civ3 isn't installed, and not merely because CI is set,
	// so a CI runner given CIV3_HOME runs them. xunit reports each one as
	// skipped, so the run's summary counts them.
	public static bool ShouldSkipCiv3DependentTests() {
		return MissingRequiredFile() != null;
	}

	// Why the Civ3-dependent tests are skipped, or null if they aren't.
	public static string SkipReason {
		get {
			string missing = MissingRequiredFile();
			return missing == null ? null : $"No Civ3 install found (missing {missing}; set CIV3_HOME to the Civ3 folder)";
		}
	}

	private static string MissingRequiredFile() {
		return DefaultRequiredFiles.Select(GetCiv3Path).FirstOrDefault(path => !File.Exists(path));
	}

	// Files under the Civ3 folder matching pattern, ignoring case (Civ3's
	// file names mix cases, which matters on Linux), in a stable order.
	public static List<string> EnumerateFiles(string directory, string pattern) {
		if (!Directory.Exists(directory)) {
			return new List<string>();
		}
		EnumerationOptions options = new() {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = true,
			IgnoreInaccessible = true,
		};
		return Directory.EnumerateFiles(directory, pattern, options).OrderBy(f => f, StringComparer.Ordinal).ToList();
	}

	private static string GetCiv3Path(string relativePath) {
		string normalizedPath = relativePath
			.Replace('\\', Path.DirectorySeparatorChar)
			.Replace('/', Path.DirectorySeparatorChar);

		return Path.Combine(Civ3Location.GetCiv3Path(), normalizedPath);
	}
}

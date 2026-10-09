using System;
using System.IO;
using System.Linq;
using QueryCiv3;

namespace EngineTests.Utils;

public class PathUtils {
	// The test projects' folders: Relay.Tests shares this file to make games.
	private static readonly string[] C7GameDataTestsFolderNames = ["EngineTests", "Relay.Tests"];

	public static string getBasePath(string file) => Path.Combine(testDirectory, file);

	public static string getDataPath(string file) => Path.Combine(testDirectory, "data", file);

	public static string GameModesDir => getBasePath("../C7/Lua/");

	public static string defaultBicPath {
		get => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "conquests.biq");
	}

	public static string defaultPediaIconsPath {
		get => Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Text", "PediaIcons.txt");
	}

	// The test project's folder (e.g. EngineTests), found by walking up from
	// the test assembly's folder, so it doesn't depend on the current
	// directory. Ends with a directory separator.
	public static string testDirectory {
		get {
			for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent) {
				if (C7GameDataTestsFolderNames.Contains(dir.Name)) {
					return Path.TrimEndingDirectorySeparator(dir.FullName) + Path.DirectorySeparatorChar;
				}
			}
			throw new DirectoryNotFoundException($"None of the folders above the test assembly's folder {AppContext.BaseDirectory} "
				+ $"is a test project folder ({string.Join(", ", C7GameDataTestsFolderNames)})");
		}
	}
}

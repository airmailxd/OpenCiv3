using System.Linq;
using QueryCiv3;
using Xunit;

namespace EngineTests.PerfParsers;

public class Civ3LocationTests {
	[Fact]
	public void LibraryFoldersListsEveryLibraryPath() {
		// The current format, with nested numbered blocks and escaped
		// backslashes.
		string current = "\"libraryfolders\"\r\n{\r\n\t\"0\"\r\n\t{\r\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\r\n\t\t\"label\"\t\t\"\"\r\n\t}\r\n"
			+ "\t\"1\"\r\n\t{\r\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\r\n\t}\r\n}\r\n";
		Assert.Equal(new[] { @"C:\Program Files (x86)\Steam", @"D:\SteamLibrary" }, Civ3Location.ParseLibraryFolders(current).ToArray());

		// The old format, with numbered keys holding the paths directly.
		string old = "\"LibraryFolders\"\n{\n\t\"TimeNextStatsReport\"\t\t\"1234567890\"\n\t\"1\"\t\t\"/mnt/games/steam\"\n}\n";
		Assert.Equal(new[] { "/mnt/games/steam" }, Civ3Location.ParseLibraryFolders(old).ToArray());
	}
}

using System;
using System.Diagnostics;
using System.IO;
using QueryCiv3;
using C7GameData;
using C7GameData.Save;

namespace BuildDevSave {
	class Program {

		static string GetCiv3Path { get => Civ3Location.GetCiv3Path(); }

		// The repository's C7/Text folder. It is found by looking up from the working directory and from the program's
		// own folder for the repository root (the folder containing C7/C7.csproj), so it doesn't depend on where the
		// program is run from.
		static string C7DefaultSaveDir {
			get {
				foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }) {
					for (DirectoryInfo dir = new DirectoryInfo(start); dir != null; dir = dir.Parent) {
						if (File.Exists(Path.Combine(dir.FullName, "C7", "C7.csproj"))) {
							return Path.Combine(dir.FullName, "C7", "Text");
						}
					}
				}
				throw new DirectoryNotFoundException("Unable to find the C7 repository (a folder with C7/C7.csproj) above the working directory or the program; pass the output path as the second argument.");
			}
		}

		static void Info(string path, SaveGame save) {
			Console.WriteLine($"generated save file from {path}:");
			Console.WriteLine($"\tmap dimensions: with = {save.Map.tilesWide}, height = {save.Map.tilesTall}");
			Console.WriteLine($"\tfound {save.Civilizations.Count} civilizations");
			Console.WriteLine($"\tfound {save.Players.Count} players");
			Console.WriteLine($"\tfound {save.Cities.Count} cities");
			Console.WriteLine($"\tfound {save.UnitPrototypes.Count} unit prototypes");
			Console.WriteLine($"\tfound {save.Units.Count} units");
		}

		static void Main(string[] args) {
			if (args.Length < 1) {
				Console.WriteLine("usage: BuildDevSave <civ3 SAV path> [output path]");
				Console.WriteLine("the output path defaults to C7/Text/c7-static-map-save.json in the repository");
				return;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			string fullSavePath = Path.GetFullPath(args[0]);
			string outputPath = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(C7DefaultSaveDir, "c7-static-map-save.json");
			SaveGame output = ImportCiv3.ImportSav(fullSavePath, GetCiv3Path + @"/Conquests/conquests.biq", (scenarioSearchPath) => {
				return GetCiv3Path + @"/Conquests/Text/PediaIcons.txt";
			});
			output.Save(outputPath);
			long elapsed = stopwatch.ElapsedMilliseconds;
			Console.WriteLine($"finished generating save in {elapsed} milliseconds");
			Console.WriteLine($"wrote {outputPath}");
			Info(fullSavePath, output);
		}
	}
}

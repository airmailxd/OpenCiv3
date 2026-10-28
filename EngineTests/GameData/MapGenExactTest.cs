using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// Compares OpenCiv3's map generator with Civ3's tile for tile: for each
// Civ3 save in a corpus made by tools/civ3_maps/generate.py (the 4000 BC
// autosave of a new game), it generates the map of the same seed and
// settings and counts, step by step, the tiles that agree. Civ3's generator
// is ported in Civ3MapGenerator, so every step should agree on every tile.
//
//   C7_MAPGEN_EXACT=C:/Dev/civ3_map_corpus/std   the saves (required)
//   C7_MAPGEN_OUT=<folder>        where the report goes (the corpus folder)
//   C7_MAPGEN_LIMIT=<n>           at most n maps
//   C7_MAPGEN_RECHECK=1           check every map, reviewed or not
//
// It checks two ways: Civ3MapGenerator itself with the rules of the save's
// BIQ, against the save's own tile data and start slots; and the GameMap
// MapGenerator makes with OpenCiv3's Civ3 rules, against the save as
// ImportCiv3 imports it. It writes mapgen-exact.txt and fails if any map
// differs anywhere.
//
// A map found exact is marked reviewed (reviewed.csv in the report folder),
// with a fingerprint of the map generation code, and isn't checked again
// until that code changes (the owner's request): only new maps, and maps
// that differed, are checked.
public class MapGenExactTest {
	private static readonly Regex CorpusName = new(@"_(?<land>Pan|Con|Arc)(?<water>\d\d)_[A-Za-z]+-[A-Za-z]+-\dB_s\d+\.SAV$");

	private sealed class Tally {
		public readonly SortedDictionary<string, (long agree, long total, int maps, int exactMaps)> steps = new(StringComparer.Ordinal);
		private readonly Dictionary<string, (int agree, int total)> map = new();

		public void Add(string step, bool agree) {
			var v = map.GetValueOrDefault(step);
			map[step] = (v.agree + (agree ? 1 : 0), v.total + 1);
		}

		// Ends a map, returning the steps where it differs.
		public List<string> EndMap() {
			List<string> differing = new();
			foreach (var (step, (agree, total)) in map) {
				var s = steps.GetValueOrDefault(step);
				steps[step] = (s.agree + agree, s.total + total, s.maps + 1, s.exactMaps + (agree == total ? 1 : 0));
				if (agree != total) {
					differing.Add($"{step} {total - agree} of {total}");
				}
			}
			map.Clear();
			return differing;
		}
	}

	[Fact]
	public void SameMapAsCiv3FromTheSameSeed() {
		string corpus = Environment.GetEnvironmentVariable("C7_MAPGEN_EXACT");
		if (string.IsNullOrEmpty(corpus)) {
			return;
		}
		string outDir = Environment.GetEnvironmentVariable("C7_MAPGEN_OUT") ?? corpus;
		int limit = int.TryParse(Environment.GetEnvironmentVariable("C7_MAPGEN_LIMIT"), out int l) ? l : int.MaxValue;
		SaveGame rules = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		byte[] bic = QueryCiv3.Util.ReadFile(PathUtils.defaultBicPath);

		Directory.CreateDirectory(outDir);
		string fingerprint = GeneratorFingerprint();
		bool recheck = Environment.GetEnvironmentVariable("C7_MAPGEN_RECHECK") == "1";
		string ledgerPath = Path.Combine(outDir, "reviewed.csv");
		Dictionary<string, string> reviewed = recheck ? new() : ReadLedger(ledgerPath, fingerprint);

		Tally tally = new();
		List<string> problems = new();
		int maps = 0, skipped = 0;
		SaveGame mdjRules = null;
		using StreamWriter ledger = new(ledgerPath, append: File.Exists(ledgerPath) && !recheck && reviewed.Count > 0);
		if (ledger.BaseStream.Length == 0) {
			ledger.WriteLine("name,bytes,fingerprint,reviewed");
		}
		foreach (string path in Directory.GetFiles(corpus, "*.SAV", SearchOption.AllDirectories).Where(p => CorpusName.IsMatch(p)).OrderBy(p => p).Take(limit)) {
			string key = LedgerKey(path);
			if (reviewed.ContainsKey(key)) {
				++skipped;
				continue;
			}
			SavData sav;
			try {
				sav = new SavData(QueryCiv3.Util.ReadFile(path), bic);
			} catch (Exception e) {
				problems.Add($"{Path.GetFileName(path)}: {e.Message}");
				continue;
			}
			++maps;
			CompareCiv3Generator(sav, tally);
			// MDJ's maps (generate.py --rules=MDJ) with OpenCiv3's MDJ rules.
			bool mdj = Path.GetFileName(path).StartsWith("MDJ_", StringComparison.Ordinal);
			mdjRules ??= mdj ? GameSetup.LoadMdjRules(PathUtils.GameModesDir, new GameMode.Config("civ3")) : null;
			CompareGameMap(path, sav, mdj ? mdjRules : rules, tally);
			List<string> differing = tally.EndMap();
			if (differing.Count > 0) {
				problems.Add($"{Path.GetFileName(path)} (seed {sav.Wrld.WorldSeed}): {string.Join(", ", differing)}");
			} else {
				reviewed[key] = fingerprint;
				ledger.WriteLine($"{key},{fingerprint},{DateTime.Now:yyyy-MM-dd HH:mm}");
			}
		}

		StringBuilder report = new();
		report.AppendLine($"Map generation against Civ3's own maps, tile for tile, from {corpus}: {maps} maps checked, "
			+ $"{skipped} already reviewed exact with this map generation code ({fingerprint})");
		report.AppendLine("Each step: the share of tiles (or starts) that agree, and the maps that agree on every one.");
		foreach (var (step, (agree, total, inMaps, exactMaps)) in tally.steps) {
			report.AppendLine($"  {step,-32} {100.0 * agree / Math.Max(1, total),8:0.000}%  {exactMaps} of {inMaps} maps exact");
		}
		if (problems.Count > 0) {
			report.AppendLine();
			report.AppendLine($"Maps that differ ({problems.Count}):");
			problems.ForEach(p => report.AppendLine("  " + p));
		}
		File.WriteAllText(Path.Combine(outDir, "mapgen-exact.txt"), report.ToString());
		Assert.True(problems.Count == 0, report.ToString());
	}

	// The map generation code a review stands for: a hash of the source files
	// that make the maps. A change to any of them means a map reviewed before
	// is checked again.
	private static readonly string[] GeneratorSources = { "C7Engine/Civ3MapGenerator.cs", "C7Engine/MapGenerator.cs" };

	private static string GeneratorFingerprint() {
		string dir = AppContext.BaseDirectory;
		while (dir != null && !File.Exists(Path.Combine(dir, GeneratorSources[0]))) {
			dir = Path.GetDirectoryName(dir);
		}
		Assert.True(dir != null, "can't find the map generation sources above " + AppContext.BaseDirectory);
		using var sha = System.Security.Cryptography.SHA256.Create();
		foreach (string source in GeneratorSources) {
			// Line endings aside, so a checkout on another machine agrees.
			byte[] text = Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(dir, source)).Replace("\r\n", "\n"));
			sha.TransformBlock(text, 0, text.Length, null, 0);
		}
		sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
		return Convert.ToHexString(sha.Hash)[..12];
	}

	// A save as the ledger knows it: its name and size.
	private static string LedgerKey(string path) => $"{Path.GetFileName(path)},{new FileInfo(path).Length}";

	// The maps reviewed exact with this fingerprint.
	private static Dictionary<string, string> ReadLedger(string path, string fingerprint) {
		Dictionary<string, string> reviewed = new();
		if (!File.Exists(path)) {
			return reviewed;
		}
		foreach (string line in File.ReadLines(path).Skip(1)) {
			string[] parts = line.Split(',');
			if (parts.Length >= 3 && parts[2] == fingerprint) {
				reviewed[$"{parts[0]},{parts[1]}"] = fingerprint;
			}
		}
		return reviewed;
	}

	// What the Civ3 port takes from a BIQ.
	internal static Civ3MapGenerator.Rules RulesOf(BiqData biq) {
		Civ3MapGenerator.Rules rules = new();
		for (int t = 0; t < 14; ++t) {
			var terrain = biq.Terr[t];
			rules.irrigation[t] = terrain.IrrigationBonus;
			rules.mining[t] = terrain.MiningBonus;
			rules.road[t] = terrain.RoadBonus;
			rules.food[t] = terrain.Food;
			rules.shields[t] = terrain.Shields;
			rules.commerce[t] = terrain.Commerce;
			rules.allowCities[t] = terrain.AllowCities != 0;
			rules.workerJob[t] = terrain.WorkerJob != -1;
		}
		for (int g = 0; g < biq.Good.Length; ++g) {
			var good = biq.Good[g];
			Civ3MapGenerator.Good ours = new() {
				type = good.Type,
				appearanceRatio = good.AppearanceRatio,
				food = good.FoodBonus,
				shields = good.ShieldsBonus,
				commerce = good.CommerceBonus,
				needsAdvance = good.Prerequisite != -1,
			};
			for (int t = 0; t < 14; ++t) {
				ours.allowedOn[t] = biq.TerrGood[t, g];
			}
			rules.goods.Add(ours);
		}
		return rules;
	}

	// The settings of a save's map, as Civ3 numbers them.
	internal static Civ3MapGenerator.Settings SettingsOf(SavData sav) {
		var w = sav.Wrld;
		return new Civ3MapGenerator.Settings {
			width = w.Width,
			height = w.Height,
			wrapX = w.XWrapping,
			wrapY = w.YWrapping,
			seed = w.WorldSeed,
			landform = w.ActualLandform,
			oceanCoverage = w.ActualOceanCoverage,
			climate = w.ActualClimate,
			temperature = w.ActualTemperature,
			age = w.ActualAge,
			barbarians = w.ActualBarbarians,
			numberOfCivs = w.NumberOfCivs,
			civDistance = w.CivDistance,
			coastalStarts = Enumerable.Range(1, w.NumberOfCivs).Count(p => IsSeafaring(sav, p)),
		};
	}

	private static bool IsSeafaring(SavData sav, int player) {
		int race = sav.Lead[player].RaceID;
		return race >= 0 && race < sav.Bic.Race.Length && sav.Bic.Race[race].Seafaring;
	}

	private static void CompareCiv3Generator(SavData sav, Tally tally) {
		Civ3MapGenerator g = new(SettingsOf(sav), RulesOf(sav.Bic));
		g.Generate();
		g.GiveSeafaringCivsCoastalStarts(p => IsSeafaring(sav, p), sav.Wrld.NumberOfCivs);
		for (int i = 0; i < g.tileCount; ++i) {
			var t = sav.Tile[i];
			bool civ3Water = t.OverlayTerrain >= Civ3MapGenerator.Coast, ourWater = g.terrain[i] >= Civ3MapGenerator.Coast;
			tally.Add("1 land and water", civ3Water == ourWater);
			if (civ3Water && ourWater) {
				tally.Add("2 coast, sea, ocean", t.OverlayTerrain == g.terrain[i]);
			}
			if (!civ3Water && !ourWater) {
				static int Relief(int x) => x is Civ3MapGenerator.Hills or Civ3MapGenerator.Mountains or Civ3MapGenerator.Volcano ? x : -1;
				tally.Add("3 hills, mountains, volcanoes", Relief(t.OverlayTerrain) == Relief(g.terrain[i]));
				tally.Add("4 terrain", t.OverlayTerrain == g.terrain[i] && t.BaseTerrain == g.baseTerrain[i]);
				if (t.OverlayTerrain is Civ3MapGenerator.Forest or Civ3MapGenerator.Jungle or Civ3MapGenerator.Marsh) {
					tally.Add("5 forest, jungle, marsh", t.OverlayTerrain == g.terrain[i]);
				}
				if (t.OverlayTerrain == Civ3MapGenerator.Forest && g.terrain[i] == Civ3MapGenerator.Forest) {
					tally.Add("5 pine forests", t.PineForest == ((g.flags[i] & Civ3MapGenerator.ColdFlag) != 0));
				}
				if (t.OverlayTerrain == Civ3MapGenerator.Mountains && g.terrain[i] == Civ3MapGenerator.Mountains) {
					tally.Add("6 snow caps", t.SnowCapped == ((g.flags[i] & Civ3MapGenerator.SnowCappedFlag) != 0));
				}
				tally.Add("8 goody huts", t.GoodyHut == g.hut[i]);
				tally.Add("9 bonus grassland", t.BonusShield == ((g.flags[i] & Civ3MapGenerator.BonusGrasslandFlag) != 0));
			}
			tally.Add("7 rivers", t.RiverInfo == g.river[i]);
			tally.Add("8 resources", t.ResourceID == g.resource[i]);
			tally.Add("10 start flag", t.StartLocation == ((g.flags[i] & Civ3MapGenerator.StartFlag) != 0));
		}
		for (int p = 1; p <= sav.Wrld.NumberOfCivs; ++p) {
			tally.Add("11 start of each player", sav.Wrld.StartLocation(p) == g.startSlots[p]);
			// The units of a new game stand on their civ's start.
			var unit = sav.Unit.FirstOrDefault(u => u.OwnerID == p);
			tally.Add("11 start where the units are", g.startSlots[p] >= 0 && unit.X == g.X(g.startSlots[p]) && unit.Y == g.Y(g.startSlots[p]));
		}
	}

	// A scenario's PediaIcons.txt (MDJ's is in Conquests\Conquests\Sengoku),
	// or Civ3's own.
	private static string ScenarioPediaIcons(string searchFolders) {
		foreach (string folder in (searchFolders ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
			string path = Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Conquests", folder, "Text", "PediaIcons.txt");
			if (File.Exists(path)) {
				return path;
			}
		}
		return PathUtils.defaultPediaIconsPath;
	}

	// The same map through MapGenerator, with OpenCiv3's Civ3 rules (or its
	// MDJ rules, for a map of MDJ's), against ImportCiv3's import of the save.
	private static void CompareGameMap(string path, SavData sav, SaveGame rules, Tally tally) {
		var w = sav.Wrld;
		WorldCharacteristics wc = new(rules) {
			landform = (WorldCharacteristics.Landform)w.ActualLandform,
			oceanCoverage = w.ActualOceanCoverage switch {
				0 => WorldCharacteristics.OceanCoverage.Percent_80,
				1 => WorldCharacteristics.OceanCoverage.Percent_70,
				_ => WorldCharacteristics.OceanCoverage.Percent_60,
			},
			climate = (WorldCharacteristics.Climate)w.ActualClimate,
			temperature = (WorldCharacteristics.Temperature)w.ActualTemperature,
			age = (WorldCharacteristics.Age)w.ActualAge,
			barbarianActivity = (BarbarianActivity)w.ActualBarbarians,
			worldSize = rules.WorldSizes.Single(s => s.width == w.Width && s.height == w.Height),
			mapSeed = w.WorldSeed,
			coastalStartsNeeded = Enumerable.Range(1, w.NumberOfCivs).Count(p => IsSeafaring(sav, p)),
		};
		wc.worldSize = wc.worldSize.Clone();
		wc.worldSize.numberOfCivs = w.NumberOfCivs;
		wc.worldSize.distanceBetweenCivs = w.CivDistance;
		SaveMap ours = new(MapGenerator.GenerateMap(wc, out _));
		SaveMap civ3 = ImportCiv3.ImportSav(path, PathUtils.defaultBicPath, ScenarioPediaIcons).Map;
		Dictionary<(int, int), SaveTile> theirs = civ3.tiles.ToDictionary(t => (t.X, t.Y));
		foreach (SaveTile t in ours.tiles) {
			SaveTile c = theirs[(t.X, t.Y)];
			tally.Add("12 GameMap: terrain", t.baseTerrain == c.baseTerrain && t.overlayTerrain == c.overlayTerrain);
			tally.Add("12 GameMap: resource", (t.resource ?? "") == (c.resource ?? ""));
			string[] kept = ["goodyHut", "bonusShield", "snowCapped", "pineForest", "river"];
			IEnumerable<string> Features(SaveTile x) => x.features.Where(f => kept.Any(f.StartsWith)).OrderBy(f => f, StringComparer.Ordinal);
			tally.Add("12 GameMap: rivers and features", Features(t).SequenceEqual(Features(c)));
		}
		HashSet<(int, int)> civ3Starts = Enumerable.Range(1, w.NumberOfCivs).Select(p => w.StartLocation(p)).Where(i => i >= 0)
			.Select(i => (2 * (i % (w.Width / 2)) + ((i / (w.Width / 2)) & 1), i / (w.Width / 2))).ToHashSet();
		foreach (SaveTile s in ours.startingLocations) {
			tally.Add("12 GameMap: starts", civ3Starts.Contains((s.X, s.Y)));
		}
		tally.Add("12 GameMap: number of starts", ours.startingLocations.Count == civ3Starts.Count);
	}
}

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
using Xunit;

namespace EngineTests.GameData;

// Compares OpenCiv3's map generator with Civ3's, by the numbers: for each
// Civ3 save in a corpus made by tools/civ3_maps/generate.py, it generates
// our map from the same seed and settings and measures both (the share of
// land, of each terrain, the landmasses, rivers, resources, starts...). The
// maps can't be the same, so what is compared is how each measure is spread
// over many maps, landform by landform.
//
//   C7_MAPGEN_CORPUS=C:/Dev/civ3_map_corpus/std   the saves (required)
//   C7_MAPGEN_OUT=<folder>        where the report goes (the corpus folder)
//   C7_MAPGEN_RULES=mdj           MDJ's rules (GameSetup.Mdj.cs), not Civ3's
//   C7_MAPGEN_LIMIT=<n>           at most n maps per landform and water
//
// It writes mapgen-report.txt (each measure's mean and spread for Civ3 and
// for us, ordered by how far apart they are) and mapgen-maps.csv (every
// measure of every map).
public class MapGenParityTest {
	// The corpus's file names (see generate.py's name_of), e.g.
	// STD_Standard_Pan70_Nrm-Tmp-4B_s000123.SAV.
	private static readonly Regex CorpusName = new(@"^(?<rules>[A-Z]+)_(?<size>.+)_(?<land>Pan|Con|Arc)(?<water>\d\d)_(?<climate>Ari|Nrm|Wet)-(?<temp>Wrm|Tmp|Col)-(?<age>\dB)_s(?<seed>\d+)\.SAV$");

	private static readonly string[] LandTerrains = ["grassland", "plains", "desert", "tundra", "flood plain", "forest", "jungle", "marsh", "hills", "mountains", "volcano"];

	// The eight neighbors, in map coordinates.
	private static readonly (int, int)[] Around = [(1, -1), (1, 1), (-1, 1), (-1, -1), (2, 0), (-2, 0), (0, 2), (0, -2)];

	private sealed record Settings(string Rules, string Size, string Landform, int Water, string Climate, string Temperature, string Age, int Seed) {
		public string Group => $"{Landform}{Water}";
	}

	[Fact]
	public void CompareWithCiv3Corpus() {
		string corpus = Environment.GetEnvironmentVariable("C7_MAPGEN_CORPUS");
		if (string.IsNullOrEmpty(corpus)) {
			return;
		}
		string outDir = Environment.GetEnvironmentVariable("C7_MAPGEN_OUT") ?? corpus;
		int limit = int.TryParse(Environment.GetEnvironmentVariable("C7_MAPGEN_LIMIT"), out int l) ? l : int.MaxValue;
		bool mdj = Environment.GetEnvironmentVariable("C7_MAPGEN_RULES") == "mdj";
		SaveGame rules = mdj
			? GameSetup.LoadMdjRules(PathUtils.GameModesDir, new GameMode.Config("civ3"))
			: SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();

		List<(Settings settings, string source, Dictionary<string, double> measures)> rows = new();
		List<string> problems = new();
		// The corpus and its subfolders (the VMs' vm_<name>), each seed of
		// each kind of map once.
		var files = Directory.GetFiles(corpus, "*.SAV", SearchOption.AllDirectories)
			.Select(path => (path, match: CorpusName.Match(Path.GetFileName(path))))
			.Where(f => f.match.Success)
			.Select(f => (f.path, settings: SettingsOf(f.match)))
			.DistinctBy(f => f.settings)
			.GroupBy(f => f.settings.Group)
			.SelectMany(g => g.OrderBy(f => f.settings.Seed).Take(limit));
		foreach ((string path, Settings settings) in files) {
			try {
				SaveGame civ3 = ImportCiv3.ImportSav(path, PathUtils.defaultBicPath, _ => PathUtils.defaultPediaIconsPath);
				List<(int, int)> civ3Starts = civ3.Players.Where(p => !p.isBarbarian)
					.Select(p => civ3.Units.FirstOrDefault(u => u.owner == p.id)?.currentLocation)
					.Where(at => at != null)
					.Select(at => (at.Value.X, at.Value.Y)).ToList();
				rows.Add((settings, "Civ3", Measure(civ3.Map, civ3Starts)));

				GameMap ours = MapGenerator.GenerateMap(Characteristics(rules, settings), out string problem);
				if (problem != null) {
					problems.Add($"{settings.Group} seed {settings.Seed}: {problem}");
				}
				rows.Add((settings, "OpenCiv", Measure(new SaveMap(ours), ours.startingLocations.Select(t => (t.XCoordinate, t.YCoordinate)).ToList())));
			} catch (Exception e) {
				problems.Add($"{Path.GetFileName(path)}: {e.GetType().Name}: {e.Message}");
			}
		}

		Directory.CreateDirectory(outDir);
		List<string> names = rows.SelectMany(r => r.measures.Keys).Distinct().OrderBy(k => k.StartsWith("res:") ? 1 : 0).ThenBy(k => k, StringComparer.Ordinal).ToList();
		StringBuilder csv = new();
		csv.AppendLine("source,group,seed," + string.Join(",", names.Select(n => n.Replace(',', ';'))));
		foreach (var r in rows) {
			csv.AppendLine($"{r.source},{r.settings.Group},{r.settings.Seed}," + string.Join(",", names.Select(n => r.measures.GetValueOrDefault(n).ToString("0.####", CultureInfo.InvariantCulture))));
		}
		File.WriteAllText(Path.Combine(outDir, "mapgen-maps.csv"), csv.ToString());

		StringBuilder report = new();
		report.AppendLine($"Map generation, Civ3 and OpenCiv, from {corpus} ({(mdj ? "MDJ" : "Civ3")} rules): {rows.Count(r => r.source == "Civ3")} maps");
		report.AppendLine("Each measure: Civ3's mean (sd), ours, and d = the difference in pooled sds; |d| > 0.8 is a big difference.");
		if (problems.Count > 0) {
			report.AppendLine();
			report.AppendLine($"Problems ({problems.Count}):");
			problems.ForEach(p => report.AppendLine("  " + p));
		}
		foreach (string group in rows.Select(r => r.settings.Group).Distinct().OrderBy(g => g).Prepend("all")) {
			var inGroup = rows.Where(r => group == "all" || r.settings.Group == group).ToList();
			var civ3Rows = inGroup.Where(r => r.source == "Civ3").Select(r => r.measures).ToList();
			var ourRows = inGroup.Where(r => r.source == "OpenCiv").Select(r => r.measures).ToList();
			report.AppendLine();
			report.AppendLine($"== {group}: {civ3Rows.Count} Civ3 maps, {ourRows.Count} ours");
			var lines = names.Select(n => {
				(double m1, double s1) = MeanSd(civ3Rows.Select(m => m.GetValueOrDefault(n)));
				(double m2, double s2) = MeanSd(ourRows.Select(m => m.GetValueOrDefault(n)));
				double pooled = Math.Sqrt((s1 * s1 + s2 * s2) / 2);
				double d = pooled > 1e-9 ? (m2 - m1) / pooled : (Math.Abs(m2 - m1) > 1e-9 ? double.PositiveInfinity * Math.Sign(m2 - m1) : 0);
				return (n, m1, s1, m2, s2, d);
			}).ToList();
			foreach (bool resources in new[] { false, true }) {
				report.AppendLine(resources ? "  -- resources (tiles per map)" : "  -- the map");
				foreach (var (n, m1, s1, m2, s2, d) in lines.Where(x => x.n.StartsWith("res:") == resources).OrderByDescending(x => Math.Abs(x.d))) {
					report.AppendLine($"  {n,-28} Civ3 {Fmt(m1),8} ({Fmt(s1),7})  ours {Fmt(m2),8} ({Fmt(s2),7})  d {(double.IsInfinity(d) ? (d > 0 ? "+inf" : "-inf") : d.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)),6}");
				}
			}
		}
		File.WriteAllText(Path.Combine(outDir, "mapgen-report.txt"), report.ToString());
	}

	// generate.py's Quick Start maps, named for when they were made
	// (..._t<time>.SAV): renamed to their seed, ..._s<seed>.SAV, once their
	// settings are checked against the name. A seed already in the corpus
	// is the same map again (a seed and its settings make the same map), so
	// the copy is deleted.
	//   C7_MAPGEN_RENAME=C:/Dev/civ3_map_corpus   (and its subfolders)
	private static readonly Regex QuickStartName = new(@"^(?<prefix>[A-Z]+_.+_(?<land>Pan|Con|Arc)(?<water>\d\d)_(?<climate>Ari|Nrm|Wet)-(?<temp>Wrm|Tmp|Col)-(?<age>\dB))_t\d+\.SAV$");

	[Fact]
	public void RenameQuickStartMaps() {
		string folder = Environment.GetEnvironmentVariable("C7_MAPGEN_RENAME");
		if (string.IsNullOrEmpty(folder)) {
			return;
		}
		byte[] bic = QueryCiv3.Util.ReadFile(PathUtils.defaultBicPath);
		List<string> wrong = new();
		int renamed = 0, duplicates = 0;
		foreach (string path in Directory.GetFiles(folder, "*.SAV", SearchOption.AllDirectories)) {
			Match m = QuickStartName.Match(Path.GetFileName(path));
			if (!m.Success) {
				continue;
			}
			QueryCiv3.SavData sav;
			try {
				sav = new(QueryCiv3.Util.ReadFile(path), bic);
			} catch (Exception) {
				// Still being written by generate.py: renamed next time.
				continue;
			}
			var w = sav.Wrld;
			// Civ3's settings in the order of its world setup screen.
			(string, int, int)[] expected = [
				("landform", Array.IndexOf(new[] { "Arc", "Con", "Pan" }, m.Groups["land"].Value), w.ActualLandform),
				("water", Array.IndexOf(new[] { "80", "70", "60" }, m.Groups["water"].Value), w.ActualOceanCoverage),
				("climate", Array.IndexOf(new[] { "Ari", "Nrm", "Wet" }, m.Groups["climate"].Value), w.ActualClimate),
				("temperature", Array.IndexOf(new[] { "Col", "Tmp", "Wrm" }, m.Groups["temp"].Value), w.ActualTemperature),
				("age", Array.IndexOf(new[] { "3B", "4B", "5B" }, m.Groups["age"].Value), w.ActualAge),
			];
			string mismatch = string.Join(", ", expected.Where(e => e.Item2 != e.Item3).Select(e => $"{e.Item1} {e.Item3}, not {e.Item2}"));
			if (mismatch != "") {
				wrong.Add($"{Path.GetFileName(path)}: {mismatch}");
				continue;
			}
			string dest = Path.Combine(Path.GetDirectoryName(path), $"{m.Groups["prefix"].Value}_s{w.WorldSeed:D6}.SAV");
			if (File.Exists(dest)) {
				File.Delete(path);
				++duplicates;
			} else {
				File.Move(path, dest);
				++renamed;
			}
		}
		File.WriteAllText(Path.Combine(folder, "rename-log.txt"),
			$"renamed {renamed}, duplicate seeds deleted {duplicates}, settings not as named {wrong.Count}\n" + string.Join("\n", wrong));
		Assert.True(wrong.Count == 0, string.Join("\n", wrong));
	}

	private static Settings SettingsOf(Match m) {
		return new Settings(m.Groups["rules"].Value, m.Groups["size"].Value, m.Groups["land"].Value, int.Parse(m.Groups["water"].Value),
			m.Groups["climate"].Value, m.Groups["temp"].Value, m.Groups["age"].Value, int.Parse(m.Groups["seed"].Value));
	}

	private static WorldCharacteristics Characteristics(SaveGame rules, Settings s) {
		return new WorldCharacteristics(rules) {
			landform = s.Landform switch {
				"Pan" => WorldCharacteristics.Landform.Pangaea,
				"Con" => WorldCharacteristics.Landform.Continents,
				_ => WorldCharacteristics.Landform.Archipelago,
			},
			oceanCoverage = (WorldCharacteristics.OceanCoverage)s.Water,
			climate = s.Climate switch {
				"Ari" => WorldCharacteristics.Climate.Arid,
				"Wet" => WorldCharacteristics.Climate.Wet,
				_ => WorldCharacteristics.Climate.Normal,
			},
			temperature = s.Temperature switch {
				"Wrm" => WorldCharacteristics.Temperature.Warm,
				"Col" => WorldCharacteristics.Temperature.Cool,
				_ => WorldCharacteristics.Temperature.Temperate,
			},
			age = s.Age switch {
				"3B" => WorldCharacteristics.Age.Billion_3,
				"5B" => WorldCharacteristics.Age.Billion_5,
				_ => WorldCharacteristics.Age.Billion_4,
			},
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = rules.WorldSizes.Single(w => w.name == s.Size),
			mapSeed = s.Seed,
		};
	}

	private static bool IsWater(SaveTile t) => t.baseTerrain is "coast" or "sea" or "ocean";

	// The measures of a map, shares as percentages. Keys starting "res:" are
	// a resource's tiles.
	private static Dictionary<string, double> Measure(SaveMap map, List<(int X, int Y)> starts) {
		Dictionary<string, double> m = new();
		Dictionary<(int, int), SaveTile> at = map.tiles.ToDictionary(t => (t.X, t.Y));
		IEnumerable<SaveTile> Neighbors(SaveTile t) {
			foreach ((int dx, int dy) in Around) {
				int x = t.X + dx;
				if (map.wrapHorizontally) {
					x = (x % map.tilesWide + map.tilesWide) % map.tilesWide;
				}
				if (at.TryGetValue((x, t.Y + dy), out SaveTile n)) {
					yield return n;
				}
			}
		}
		double Pct(int part, int whole) => whole == 0 ? 0 : 100.0 * part / whole;

		List<SaveTile> land = map.tiles.Where(t => !IsWater(t)).ToList();
		List<SaveTile> water = map.tiles.Where(IsWater).ToList();
		m["land %"] = Pct(land.Count, map.tiles.Count);
		foreach (string w in new[] { "coast", "sea", "ocean" }) {
			m[$"water: {w} %"] = Pct(water.Count(t => t.baseTerrain == w), water.Count);
		}
		foreach (string t in LandTerrains) {
			m[$"land: {t} %"] = Pct(land.Count(x => x.overlayTerrain == t), land.Count);
		}
		m["land: bonus grassland % of grassland"] = Pct(land.Count(t => t.overlayTerrain == "grassland" && t.features.Contains("bonusShield")), land.Count(t => t.overlayTerrain == "grassland"));
		m["land: shore %"] = Pct(land.Count(t => Neighbors(t).Any(IsWater)), land.Count);
		m["land: river %"] = Pct(land.Count(t => t.features.Any(f => f.StartsWith("river"))), land.Count);
		m["land: snow-capped mountains %"] = Pct(land.Count(t => t.features.Contains("snowCapped")), land.Count(t => t.overlayTerrain == "mountains"));

		// Landmasses and bodies of water, by flood fill.
		List<int> Components(List<SaveTile> tiles, Func<SaveTile, bool> inside) {
			HashSet<SaveTile> seen = new();
			List<int> sizes = new();
			foreach (SaveTile t in tiles) {
				if (!seen.Add(t)) {
					continue;
				}
				int n = 0;
				Stack<SaveTile> stack = new([t]);
				while (stack.Count > 0) {
					SaveTile c = stack.Pop();
					++n;
					foreach (SaveTile nb in Neighbors(c).Where(inside)) {
						if (seen.Add(nb)) {
							stack.Push(nb);
						}
					}
				}
				sizes.Add(n);
			}
			return sizes;
		}
		List<int> landmasses = Components(land, t => !IsWater(t));
		m["landmasses"] = landmasses.Count;
		m["landmasses of 10+ tiles"] = landmasses.Count(s => s >= 10);
		m["landmasses of 100+ tiles"] = landmasses.Count(s => s >= 100);
		m["largest landmass % of land"] = Pct(landmasses.DefaultIfEmpty(0).Max(), land.Count);
		m["second landmass % of land"] = Pct(landmasses.OrderByDescending(s => s).Skip(1).DefaultIfEmpty(0).First(), land.Count);
		m["islets (<10) % of land"] = Pct(landmasses.Where(s => s < 10).Sum(), land.Count);
		List<int> waters = Components(water, IsWater);
		// Civ3's lakes are bodies of water of 20 tiles or fewer (see the
		// compendium's map chapter, Disputed 13).
		List<int> lakes = waters.Where(s => s <= 20).ToList();
		m["lakes (water bodies <= 20)"] = lakes.Count;
		m["lakes: tiles"] = lakes.Sum();
		m["lakes: one tile"] = lakes.Count(s => s == 1);

		// How the land is spread: over blocks of 10 by 10, and toward the poles.
		int bw = Math.Max(1, map.tilesWide / 10), bh = Math.Max(1, map.tilesTall / 10);
		var blocks = map.tiles.GroupBy(t => (t.X / bw, t.Y / bh)).Select(g => Pct(g.Count(t => !IsWater(t)), g.Count())).ToList();
		(double bm, double bs) = MeanSd(blocks);
		m["spread: blocks with land %"] = Pct(blocks.Count(b => b > 0), blocks.Count);
		m["spread: block land sd / mean"] = bm > 0 ? bs / bm : 0;
		int band = map.tilesTall / 10;
		m["spread: land in the polar tenths %"] = Pct(land.Count(t => t.Y < band || t.Y >= map.tilesTall - band), land.Count);
		m["spread: land in the middle fifth of rows %"] = Pct(land.Count(t => Math.Abs(t.Y - map.tilesTall / 2) < map.tilesTall / 10), land.Count);

		m["resource tiles"] = map.tiles.Count(t => !string.IsNullOrEmpty(t.resource));
		foreach (var g in map.tiles.Where(t => !string.IsNullOrEmpty(t.resource)).GroupBy(t => t.resource)) {
			m[$"res:{g.Key}"] = g.Count();
		}
		m["goody huts"] = map.tiles.Count(t => t.features.Contains("goodyHut"));
		m["barbarian camps"] = map.tiles.Count(t => t.features.Contains("barbarianCamp"));

		m["starts"] = starts.Count;
		if (starts.Count > 1) {
			int Distance((int X, int Y) a, (int X, int Y) b) {
				int dx = Math.Abs(a.X - b.X);
				if (map.wrapHorizontally) {
					dx = Math.Min(dx, map.tilesWide - dx);
				}
				return (dx + Math.Abs(a.Y - b.Y)) / 2;
			}
			var nearest = starts.Select(s => starts.Where(o => o != s).Min(o => Distance(s, o))).ToList();
			m["starts: nearest other, tiles"] = nearest.Average();
			m["starts: closest pair, tiles"] = nearest.Min();
		}
		List<SaveTile> startTiles = starts.Where(at.ContainsKey).Select(s => at[s]).ToList();
		m["starts: on the shore %"] = Pct(startTiles.Count(t => Neighbors(t).Any(IsWater)), startTiles.Count);
		m["starts: on a river %"] = Pct(startTiles.Count(t => t.features.Any(f => f.StartsWith("river"))), startTiles.Count);
		m["starts: landmasses with a start"] = startTiles.Select(t => Components([t], x => !IsWater(x)).Count).Count() == 0 ? 0
			: StartLandmasses(startTiles, land, Neighbors);
		return m;
	}

	// How many landmasses the starts are spread over.
	private static int StartLandmasses(List<SaveTile> starts, List<SaveTile> land, Func<SaveTile, IEnumerable<SaveTile>> neighbors) {
		Dictionary<SaveTile, int> landmassOf = new();
		int id = 0;
		foreach (SaveTile t in land) {
			if (landmassOf.ContainsKey(t)) {
				continue;
			}
			Stack<SaveTile> stack = new([t]);
			landmassOf[t] = id;
			while (stack.Count > 0) {
				foreach (SaveTile nb in neighbors(stack.Pop()).Where(n => !IsWater(n))) {
					if (landmassOf.TryAdd(nb, id)) {
						stack.Push(nb);
					}
				}
			}
			++id;
		}
		return starts.Where(landmassOf.ContainsKey).Select(s => landmassOf[s]).Distinct().Count();
	}

	private static (double mean, double sd) MeanSd(IEnumerable<double> values) {
		List<double> v = values.ToList();
		if (v.Count == 0) {
			return (0, 0);
		}
		double mean = v.Average();
		double sd = v.Count > 1 ? Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / (v.Count - 1)) : 0;
		return (mean, sd);
	}

	private static string Fmt(double x) => x.ToString(Math.Abs(x) >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture);
}

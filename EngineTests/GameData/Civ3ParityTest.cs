using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using C7Engine;
using C7GameData;
using C7Engine.Lua;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Sav = QueryCiv3.Sav;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.GameData;

// Compares our city yields with the ones Civ3 stored in real saves.
//
// Each Civ3 save holds the results of Civ3's own calculations for every city,
// so loading a save and recalculating a city from the same tiles, buildings,
// government and sliders shows where our rules differ from Civ3's. The test
// doesn't fail on a mismatch: it writes a report counting, for each value,
// how often we match exactly, how often we're off by one and the biggest
// misses, along with the buildings and governments the mismatches cluster in.
//
// It reads every .SAV under Conquests/Saves in the Civ3 install, or under
// C7_PARITY_SAVES if that is set, and writes the report to
// EngineTests/data/output/civ3-parity.md.
//
// What some save fields mean is still a guess (see the notes on each field
// below), so check a field's meaning before trusting its mismatches.
public class Civ3ParityTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;

	public Civ3ParityTest(SaveGameFixture fixture, ITestOutputHelper output) {
		this.fixture = fixture;
		this.output = output;
	}

	// What we know about a city for the purposes of the report.
	private sealed record CityInfo(string Save, string Name, string Civ, string Government, List<string> Buildings, List<string> Tags);

	private sealed record Mismatch(CityInfo City, int Ours, int Civ3);

	private sealed class FieldStats {
		public readonly string Name;
		public readonly string Notes;
		public int compared, exact, offByOne;
		public long totalAbsDiff;
		public readonly List<Mismatch> mismatches = new();
		public readonly List<CityInfo> all = new();

		public FieldStats(string name, string notes) {
			Name = name;
			Notes = notes;
		}

		public void Add(CityInfo city, int ours, int civ3) {
			++compared;
			all.Add(city);
			int diff = Math.Abs(ours - civ3);
			totalAbsDiff += diff;
			if (diff == 0) {
				++exact;
			} else {
				if (diff == 1) {
					++offByOne;
				}
				mismatches.Add(new Mismatch(city, ours, civ3));
			}
		}
	}

	// A value we calculate and the value Civ3 stored for it.
	private sealed record Field(string Name, string Notes, Func<City, Sav.CITY, int> Ours, Func<Sav.CITY, int> Civ3);

	private static int GrossShields(City c) {
		int yield = c.location.ProductionYield(c).yield;
		foreach (CityResident r in c.residents) {
			if (r.tileWorked != Tile.NONE) {
				yield += r.tileWorked.ProductionYield(c).yield;
			}
		}
		return yield;
	}

	private static int GrossCommerce(City c) {
		int yield = c.location.CommerceYield(c).yield;
		foreach (CityResident r in c.residents) {
			if (r.tileWorked != Tile.NONE) {
				yield += r.tileWorked.CommerceYield(c).yield;
			}
		}
		return yield;
	}

	private static readonly Field[] Fields = {
		new("Food", "FoodPerTurn: all the food the city's tiles produce.",
			(c, _) => c.CurrentFoodYield(), s => s.FoodPerTurn),
		new("FoodEaten", "FoodPerTurnForPopulation: food eaten by the citizens.",
			(c, _) => c.FoodConsumedPerTurn(), s => s.FoodPerTurnForPopulation),
		new("FoodSurplus", "ExcessFoodPerTurn: food left after the citizens eat.",
			(c, _) => c.FoodGrowthPerTurn(), s => s.ExcessFoodPerTurn),
		new("FoodSurplus (Unwasted)", "UnwastedFoodPerTurn, compared with our surplus. Meaning unconfirmed.",
			(c, _) => c.FoodGrowthPerTurn(), s => s.UnwastedFoodPerTurn),
		new("Shields", "ShieldsPerTurn, compared with the shields from tiles before waste. Whether Civ3 counts factories here is unconfirmed.",
			(c, _) => GrossShields(c), s => s.ShieldsPerTurn),
		new("ShieldWaste", "CorruptShieldsPerTurn: shields lost to waste.",
			(c, _) => c.CurrentProductionYield().corrupt, s => s.CorruptShieldsPerTurn),
		new("Commerce", "CommercePerTurn: commerce from tiles before corruption.",
			(c, _) => GrossCommerce(c), s => s.CommercePerTurn),
		new("Corruption", "CorruptGoldPerTurn: commerce lost to corruption.",
			(c, _) => c.CurrentCommerceYield().corrupted, s => s.CorruptGoldPerTurn),
		new("UncorruptCommerce", "UncorruptGoldPerTurn, compared with commerce minus corruption.",
			(c, _) => GrossCommerce(c) - c.CurrentCommerceYield().corrupted, s => s.UncorruptGoldPerTurn),
		new("Luxuries", "LuxuryGoldPerTurn. May be stored before marketplaces and the like.",
			(c, _) => c.CurrentCommerceYield().happiness, s => s.LuxuryGoldPerTurn),
		new("Science", "ScienceGoldPerTurn. Seems to include libraries and the like.",
			(c, _) => c.CurrentCommerceYield().beakers, s => s.ScienceGoldPerTurn),
		new("Taxes", "TreasuryGoldPerTurn. Seems to include marketplaces and the like.",
			(c, _) => c.CurrentCommerceYield().taxes, s => s.TreasuryGoldPerTurn),
		new("Maintenance", "MaintenanceGPT: building upkeep.",
			(c, _) => c.MaintenanceCosts(), s => s.MaintenanceGPT),
		new("Culture", "CulturePerTurn.",
			(c, _) => c.GetCulturePerTurn(), s => s.CulturePerTurn),
	};

	private static string SavesFolder() {
		return Environment.GetEnvironmentVariable("C7_PARITY_SAVES")
			?? Path.Combine(Civ3Location.GetCiv3Path(), "Conquests", "Saves");
	}

	// A city from a Civ3 save, loaded into our engine, whose inputs agree with
	// Civ3's.
	internal sealed record ParityCity(string Save, SavData Sav, int Index, C7GameData.GameData GameData, City City) {
		public Sav.CITY SavCity => Sav.City[Index];
	}

	// Loads every save under the saves folder and returns the cities whose
	// outputs can be compared with Civ3's. Cities whose inputs differ are
	// passed to onSkip with the reason, and saves that fail to load to
	// onFail. Each save's cities come before the next save is loaded, which
	// replaces the game in EngineStorage.
	internal static IEnumerable<ParityCity> LoadComparableCities(BehaviorEngine behaviors, Action<string> onSkip, Action<string, Exception> onFail) {
		string folder = SavesFolder();
		byte[] defaultBic = QueryCiv3.Util.ReadFile(PathUtils.defaultBicPath);
		foreach (string savePath in SavePaths()) {
			string saveName = Path.GetRelativePath(folder, savePath);
			List<ParityCity> cities;
			try {
				cities = LoadSave(savePath, saveName, defaultBic, behaviors, onSkip);
			} catch (Exception e) {
				onFail(saveName, e);
				continue;
			}
			foreach (ParityCity city in cities) {
				yield return city;
			}
		}
	}

	internal static List<string> SavePaths() {
		string folder = SavesFolder();
		if (!Directory.Exists(folder)) {
			return new();
		}
		return Directory.EnumerateFiles(folder, "*.SAV", SearchOption.AllDirectories).OrderBy(p => p).ToList();
	}

	[SkippableFact]
	public void CompareCityValuesWithCiv3Saves() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");
		string folder = SavesFolder();
		Skip.If(SavePaths().Count == 0, $"No .SAV files under {folder}.");

		List<FieldStats> stats = Fields.Select(f => new FieldStats(f.Name, f.Notes)).ToList();
		Dictionary<string, int> skippedCities = new();
		List<string> failedSaves = new();
		HashSet<string> savesCompared = new();
		int citiesCompared = 0;

		foreach (ParityCity pc in LoadComparableCities(fixture.behaviors,
				reason => Count(skippedCities, reason),
				(save, e) => failedSaves.Add($"{save}: {e.GetType().Name}: {e.Message.Split('\n')[0]}"))) {
			savesCompared.Add(pc.Save);
			City city = pc.City;
			Sav.CITY savCity = pc.SavCity;
			CityInfo info = new(pc.Save, city.name, city.owner.civilization?.name ?? "?",
				city.owner.government?.name ?? "?",
				city.GetBuildings().Select(b => b.building.name).Distinct().ToList(),
				Tags(city, savCity));
			for (int f = 0; f < Fields.Length; ++f) {
				stats[f].Add(info, Fields[f].Ours(city, savCity), Fields[f].Civ3(savCity));
			}
			++citiesCompared;
		}

		string report = WriteReport(folder, savesCompared.Count, citiesCompared, failedSaves, skippedCities, stats);
		string reportPath = PathUtils.getDataPath(Path.Combine("output", "civ3-parity.md"));
		Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
		File.WriteAllText(reportPath, report);

		output.WriteLine($"Report written to {reportPath}");
		foreach (FieldStats s in stats) {
			output.WriteLine($"{s.Name,-24} {Percent(s.exact, s.compared),6} exact, {Percent(s.offByOne, s.compared),6} off by one ({s.compared} cities)");
		}
		Assert.True(citiesCompared > 0, $"No cities could be compared. See {reportPath}.");
	}

	private static List<ParityCity> LoadSave(string savePath, string saveName, byte[] defaultBic, BehaviorEngine behaviors, Action<string> onSkip) {
		SavData sav = new(QueryCiv3.Util.ReadFile(savePath), defaultBic);
		SaveGame save = ImportCiv3.ImportSav(savePath, PathUtils.defaultBicPath, _ => PathUtils.defaultPediaIconsPath);
		C7GameData.GameData gameData = save.ToGameData(behaviors);

		// Pair each Civ3 city with ours, and carry over the city states that
		// change its yields but that the import doesn't bring across.
		List<(City city, int index)> pairs = new();
		for (int i = 0; i < sav.City.Length; ++i) {
			Sav.CITY savCity = sav.City[i];
			City city = gameData.map.tileAt(savCity.X, savCity.Y)?.cityAtTile;
			if (city == null) {
				onSkip("No matching city after import");
				continue;
			}
			city.isInCivilDisorder = savCity.CivilDisorder;
			city.celebrating = savCity.WeLoveTheKingDay;
			pairs.Add((city, i));
		}

		foreach (Player player in gameData.players) {
			player.DoCorruptionCalculations(gameData);
		}

		List<ParityCity> result = new();
		foreach ((City city, int i) in pairs) {
			string skipReason = InputMismatch(city, sav.City[i], sav.CityCtzn[i]);
			if (skipReason != null) {
				onSkip(skipReason);
			} else {
				result.Add(new ParityCity(saveName, sav, i, gameData, city));
			}
		}
		return result;
	}

	// Why the city's inputs differ from Civ3's, so that comparing its outputs
	// would say nothing about our rules, or null if they agree.
	private static string InputMismatch(City city, Sav.CITY savCity, Sav.CTZN[] citizens) {
		// We don't import resisters yet.
		if (citizens.Any(c => c.Type == 3 || (c.Type != 4 && c.TileWorked == 0))) {
			return "Has resisters";
		}

		int civ3Specialists = savCity.EntertainerCount + savCity.ScientistCount + savCity.TaxCollectorCount;
		int ourSpecialists = city.residents.Count(r => !r.citizenType.IsDefaultCitizen);
		if (civ3Specialists != ourSpecialists || citizens.Length != city.residents.Count) {
			return "Citizens differ after import";
		}

		// Each worked tile should be worked by one citizen.
		List<Tile> worked = city.residents.Where(r => r.citizenType.IsDefaultCitizen).Select(r => r.tileWorked).ToList();
		if (worked.Any(t => t == null || t == Tile.NONE) || worked.Distinct().Count() != worked.Count) {
			return "Worked tiles differ after import";
		}
		return null;
	}

	private static List<string> Tags(City city, Sav.CITY savCity) {
		List<string> tags = new();
		if (city.IsCapital()) tags.Add("capital");
		if (savCity.CivilDisorder) tags.Add("disorder");
		if (savCity.WeLoveTheKingDay) tags.Add("celebrating");
		if (city.owner.government?.transitionType == true) tags.Add("anarchy");
		if (city.residents.Any(r => !r.citizenType.IsDefaultCitizen)) tags.Add("specialists");
		if (city.residents.Count > 12) tags.Add("size13+");
		else if (city.residents.Count > 6) tags.Add("size7-12");
		return tags;
	}

	private static void Count(Dictionary<string, int> counts, string key) {
		counts[key] = counts.GetValueOrDefault(key) + 1;
	}

	private static string Percent(int part, int whole) {
		return whole == 0 ? "-" : $"{100.0 * part / whole:0.0}%";
	}

	private static string WriteReport(string folder, int saves, int cities, List<string> failedSaves,
			Dictionary<string, int> skippedCities, List<FieldStats> stats) {
		StringBuilder sb = new();
		sb.AppendLine("# Civ3 parity report");
		sb.AppendLine();
		sb.AppendLine($"Saves from `{folder}`, written {DateTime.Now:yyyy-MM-dd HH:mm}.");
		sb.AppendLine();
		sb.AppendLine($"- {saves} saves loaded, {failedSaves.Count} failed to load");
		sb.AppendLine($"- {cities} cities compared, {skippedCities.Values.Sum()} skipped because their inputs differ");
		foreach ((string reason, int count) in skippedCities.OrderByDescending(kv => kv.Value)) {
			sb.AppendLine($"  - {reason}: {count}");
		}
		sb.AppendLine();

		sb.AppendLine("## Summary");
		sb.AppendLine();
		sb.AppendLine("| Value | Exact | Off by one | Off by more | Mean miss | Cities |");
		sb.AppendLine("|---|---|---|---|---|---|");
		foreach (FieldStats s in stats) {
			int more = s.compared - s.exact - s.offByOne;
			string mean = s.compared == 0 ? "-" : $"{(double)s.totalAbsDiff / s.compared:0.00}";
			sb.AppendLine($"| {s.Name} | {Percent(s.exact, s.compared)} | {Percent(s.offByOne, s.compared)} | {Percent(more, s.compared)} | {mean} | {s.compared} |");
		}
		sb.AppendLine();

		foreach (FieldStats s in stats) {
			sb.AppendLine($"## {s.Name}");
			sb.AppendLine();
			sb.AppendLine(s.Notes);
			sb.AppendLine();
			if (s.mismatches.Count == 0) {
				sb.AppendLine("Every city matches.");
				sb.AppendLine();
				continue;
			}

			int over = s.mismatches.Count(m => m.Ours > m.Civ3);
			sb.AppendLine($"{s.mismatches.Count} mismatches: ours is higher in {over}, lower in {s.mismatches.Count - over}.");
			sb.AppendLine();

			// The traits mismatching cities share more often than cities in
			// general, which is how a rule that's wrong for one building or
			// government shows up.
			double baseRate = (double)s.mismatches.Count / s.compared;
			List<(double rate, string line)> clusters = new();
			AddClusters(clusters, s, baseRate, "building", c => c.Buildings);
			AddClusters(clusters, s, baseRate, "government", c => new[] { c.Government });
			AddClusters(clusters, s, baseRate, "tag", c => c.Tags);
			if (clusters.Count > 0) {
				sb.AppendLine($"Mismatches are more common than the overall {baseRate:P0} in cities with:");
				sb.AppendLine();
				foreach ((double _, string line) in clusters.OrderByDescending(c => c.rate).Take(8)) {
					sb.AppendLine(line);
				}
				sb.AppendLine();
			}

			sb.AppendLine("| Save | City | Civ | Government | Ours | Civ3 | Tags | Buildings |");
			sb.AppendLine("|---|---|---|---|---|---|---|---|");
			foreach (Mismatch m in s.mismatches.OrderByDescending(m => Math.Abs(m.Ours - m.Civ3)).Take(15)) {
				CityInfo c = m.City;
				sb.AppendLine($"| {c.Save} | {c.Name} | {c.Civ} | {c.Government} | {m.Ours} | {m.Civ3} | {string.Join(", ", c.Tags)} | {string.Join(", ", c.Buildings)} |");
			}
			sb.AppendLine();
		}

		if (failedSaves.Count > 0) {
			sb.AppendLine("## Saves that failed to load");
			sb.AppendLine();
			foreach (string failure in failedSaves) {
				sb.AppendLine($"- {failure}");
			}
		}
		return sb.ToString();
	}

	// Adds a line for each trait whose cities mismatch notably more often than
	// cities in general, with how often they mismatch.
	private static void AddClusters(List<(double rate, string line)> lines, FieldStats s, double baseRate, string kind, Func<CityInfo, IEnumerable<string>> traits) {
		Dictionary<string, int> total = new(), missed = new();
		foreach (CityInfo c in s.all) {
			foreach (string t in traits(c)) Count(total, t);
		}
		foreach (Mismatch m in s.mismatches) {
			foreach (string t in traits(m.City)) Count(missed, t);
		}

		IEnumerable<(string trait, int missed, int total, double rate)> notable = total
			.Where(kv => kv.Value >= 5)
			.Select(kv => (trait: kv.Key, missed: missed.GetValueOrDefault(kv.Key), total: kv.Value, rate: (double)missed.GetValueOrDefault(kv.Key) / kv.Value))
			.Where(x => x.missed >= 3 && x.rate >= Math.Min(1, baseRate * 1.5));
		foreach (var x in notable) {
			lines.Add((x.rate, $"- {kind} **{x.trait}**: {x.missed} of {x.total} ({x.rate:P0})"));
		}
	}
}

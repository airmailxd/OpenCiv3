using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QueryCiv3;
using QueryCiv3.Biq;
using CITY = QueryCiv3.Sav.CITY;
using UNIT = QueryCiv3.Sav.UNIT;
using LEAD = QueryCiv3.Sav.LEAD;

// Summarizes Civ3 Conquests saves: for each civ, its cities, population,
// units by kind, government, techs, gold and wars. Turn-0 starts are only
// counted. Args: <conquests.biq> <folder with .SAV files, searched recursively>
class Program {
	static int Main(string[] args) {
		if (args.Length < 2) {
			Console.Error.WriteLine("usage: HumanGames <conquests.biq> <save folder>");
			return 1;
		}
		byte[] biq = Util.ReadFile(args[0]);
		var files = Directory.GetFiles(args[1], "*.SAV", SearchOption.AllDirectories).OrderBy(f => f).ToList();
		var starts = new List<string>();
		foreach (string file in files) {
			string name = Path.GetRelativePath(args[1], file);
			SavData sav;
			try {
				sav = new SavData(Util.ReadFile(file), biq);
			} catch (Exception e) {
				Console.WriteLine($"== {name}: COULD NOT READ: {e.GetType().Name}: {e.Message}");
				continue;
			}
			if (sav.Game.TurnNumber == 0) {
				starts.Add(name);
				continue;
			}
			try {
				Print(name, sav);
			} catch (Exception e) {
				Console.WriteLine($"   (summary failed: {e.GetType().Name}: {e.Message})");
			}
		}
		Console.WriteLine();
		Console.WriteLine($"Turn-0 starts skipped ({starts.Count}): {string.Join(", ", starts)}");
		return 0;
	}

	static void Print(string name, SavData sav) {
		BiqData bic = sav.Bic;
		int embeddedBiq = sav.Sav.ReadInt32(38);
		string diff = sav.Game.DifficultyID >= 0 && sav.Game.DifficultyID < bic.Diff.Length ? bic.Diff[sav.Game.DifficultyID].Name : "?";
		string scen = bic.Game != null && bic.Game.Length > 0 ? bic.Game[0].ScenarioSearchFolders.Trim() : "";
		int humans = 0;
		for (int i = 0; i < 32; i++) {
			if (sav.Game.HumanPlayers[i]) humans++;
		}
		Console.WriteLine();
		Console.WriteLine($"== {name}: turn {sav.Game.TurnNumber}, {diff}, map {sav.Wrld.Width}x{sav.Wrld.Height}" +
			$" ({sav.Wrld.Width * sav.Wrld.Height / 2} tiles), {sav.Wrld.NumberOfCivs} civs set, humans {humans}," +
			$" embedded rules {embeddedBiq} bytes{(scen.Length > 0 ? ", scenario folder '" + scen + "'" : "")}," +
			$" {(sav.Game.AcceleratedProduction ? "accelerated, " : "")}{(sav.Game.Regicide ? "regicide, " : "")}{(sav.Game.AllowCulturalConversions ? "" : "no flips, ")}landform {sav.Wrld.ActualLandform}");

		// Land tiles, to give density
		int land = sav.Tile.Count(t => (t.BaseTerrain) < 11);
		Console.WriteLine($"   land tiles {land}");

		var cities = sav.City ?? Array.Empty<CITY>();
		var units = sav.Unit ?? Array.Empty<UNIT>();
		Console.WriteLine("   civ                       H  cities  pop  maxSz avgSz  sett work  mil  ship other  units(LEAD) govt               techs  gold  era  wars");
		var ai = new List<int[]>(); // cities, pop, settlers, workers, military, techs, foreign-named cities, owned land
		for (int i = 0; i < sav.Lead.Length; i++) {
			LEAD l = sav.Lead[i];
			if (l.RaceID < 0 || l.RaceID >= bic.Race.Length) continue;
			if (i == 0) continue; // barbarians
			if (!sav.Game.RemainingPlayers[i]) continue;
			var mine = cities.Where(c => c.Owner == i).ToList();
			int pop = mine.Sum(c => c.Popd.CitizenCount);
			int max = mine.Count > 0 ? mine.Max(c => c.Popd.CitizenCount) : 0;
			double avg = mine.Count > 0 ? (double)pop / mine.Count : 0;
			int sett = 0, work = 0, mil = 0, ship = 0, other = 0;
			var kinds = new Dictionary<string, int>();
			// Cities named from another civ's city list were most likely captured (or flipped)
			var ownNames = new HashSet<string>((bic.RaceCityName?[l.RaceID] ?? Array.Empty<QueryCiv3.Biq.RACE_City>()).Select(c => c.Name));
			var foreignNames = new HashSet<string>();
			for (int r = 0; r < bic.Race.Length; r++) {
				if (r != l.RaceID && bic.RaceCityName != null) foreignNames.UnionWith(bic.RaceCityName[r].Select(c => c.Name));
			}
			int foreign = mine.Count(c => !ownNames.Contains(c.Name) && foreignNames.Contains(c.Name));
			int ownedLand = sav.Tile.Count(t => t.Owner == i && t.BaseTerrain < 11);
			foreach (UNIT u in units.Where(u => u.OwnerID == i)) {
				if (u.UnitType < 0 || u.UnitType >= bic.Prto.Length) { other++; continue; }
				PRTO p = bic.Prto[u.UnitType];
				// By name: the PRTO strategy flags didn't separate settlers from workers
				string n = p.Name;
				if (p.Type == PRTO.TYPE_SEA) ship++;
				else if (n.Contains("Settler") || n.Contains("Migrant")) sett++;
				else if (n.Contains("Worker") || n.Contains("Slave") || n.Contains("Engineer")) work++;
				else if (p.Attack > 0 || p.Defense > 0 || p.BombardStrength > 0) mil++;
				else other++;
				if (sav.Game.HumanPlayers[i]) {
					kinds[n] = kinds.GetValueOrDefault(n) + 1;
				}
			}
			int techs = 0;
			for (int k = 0; k < sav.KnownTechFlags.Length; k++) {
				if (sav.KnownTechFlags[k][i]) techs++;
			}
			var war = l.GetWarStatuses();
			var wars = new List<string>();
			for (int j = 1; j < 32; j++) {
				if (j != i && war[j] && sav.Game.RemainingPlayers[j] && sav.Lead[j].RaceID >= 0 && sav.Lead[j].RaceID < bic.Race.Length) {
					wars.Add(bic.Race[sav.Lead[j].RaceID].Name);
				}
			}
			string govt = l.Government >= 0 && l.Government < bic.Govt.Length ? bic.Govt[l.Government].Name : "?";
			if (l.AnarchyTurnsLeft > 0) govt = "Anarchy->" + govt;
			string era = l.Era >= 0 && l.Era < bic.Eras.Length ? bic.Eras[l.Era].Name : "?";
			string civ = $"{i,2} {bic.Race[l.RaceID].Name}";
			Console.WriteLine($"   {civ,-25} {(sav.Game.HumanPlayers[i] ? "H" : " ")}  {mine.Count,5}  {pop,4}  {max,4}  {avg,5:F1}  {sett,4} {work,4} {mil,4} {ship,4} {other,4}  {units.Count(u => u.OwnerID == i),3}({l.NumberOfUnits,3})  {govt,-18} {techs,4} {l.Gold,6}  {era,-4} {string.Join("/", wars)}");
			Console.WriteLine($"        foreign-named (captured?) cities {foreign}, owned land tiles {ownedLand}{(mine.Count > 0 ? $" ({(double)ownedLand / mine.Count:F1} per city)" : "")}");
			// The tax, science and luxury rates (tenths), and the great wonders in
			// this civ's cities: a civ with the Great Library can run science at 0
			// and bank the gold.
			HashSet<int> myCityIds = mine.Select(c => c.ID).ToHashSet();
			List<string> wonders = new();
			for (int w = 0; w < (sav.GreatWonderCityIDs?.Length ?? 0); w++) {
				if (sav.GreatWondersBuilt != null && w < sav.GreatWondersBuilt.Length && sav.GreatWondersBuilt[w]
						&& myCityIds.Contains(sav.GreatWonderCityIDs[w]) && w < bic.Bldg.Length) {
					wonders.Add(bic.Bldg[w].Name);
				}
			}
			Console.WriteLine($"        rates tax {l.TaxRate} science {l.ScienceRate} luxury {l.LuxuryRate}; wonders: {(wonders.Count > 0 ? string.Join(", ", wonders) : "none")}");
			if (!sav.Game.HumanPlayers[i]) {
				ai.Add(new[] { mine.Count, pop, sett, work, mil, techs, foreign, ownedLand });
			}
			if (kinds.Count > 0) {
				Console.WriteLine("        units: " + string.Join(", ", kinds.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}")));
			}
		}
		if (ai.Count > 0) {
			string Avg(int k) => ai.Average(a => a[k]).ToString("F1");
			int best = ai.Max(a => a[0]);
			Console.WriteLine($"   AI average of {ai.Count}: cities {Avg(0)} (most {best}), pop {Avg(1)}, settlers {Avg(2)}, workers {Avg(3)}, military {Avg(4)}, techs {Avg(5)}, foreign-named cities {Avg(6)}, land per city {(double)ai.Sum(a => a[7]) / Math.Max(1, ai.Sum(a => a[0])):F1}");
		}
	}
}

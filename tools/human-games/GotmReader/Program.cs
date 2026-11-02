// GotmReader: reads Civ3 Conquests saves (CivFanatics GOTM/COTM submissions) with QueryCiv3 and prints
// one JSON object per line per save. The saves are untrusted data: they are only parsed, never run.
//
// Usage: dotnet run --project tools/human-games/GotmReader -- <conquests.biq> <save.SAV>...
//
// What it reads:
// - The replay (RPLS): Civ3 records every city founded, captured and destroyed and every tile whose owner
//   changes, by turn. Playing the events forward (as C7Engine/C7GameData/ReplayPlayback.cs does) gives each
//   civ's cities and territory at any turn. A city belongs to whoever owns its tile, so culture flips and
//   cities traded in deals count for the new owner too.
// - The histograph (HIST): score and power (Civ3's military graph) per civ per turn.
// - The final state: government, cities, units by type, victory.
// Check: "citiesCheck" compares the cities the replay gives at the save's turn with LEAD.NumberOfCities.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using QueryCiv3;
using QueryCiv3.Sav;

static class Program {
	const int CityPlaced = 0, CityFounded = 1, CityDestroyed = 2, CityDestroyedKeepsTile = 3, CityCaptured = 4, TerritoryChanged = 6;

	static int Main(string[] args) {
		if (args.Length < 2) {
			Console.Error.WriteLine("usage: GotmReader <conquests.biq> <save.SAV>...");
			return 2;
		}
		byte[] biq = Util.ReadFile(args[0]);
		foreach (string path in args.Skip(1)) {
			Dictionary<string, object> row;
			try {
				row = Read(path, biq);
			} catch (Exception e) {
				row = new() { ["file"] = Path.GetFileName(path), ["dir"] = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path))), ["error"] = e.GetType().Name + ": " + e.Message };
			}
			Console.WriteLine(JsonSerializer.Serialize(row));
		}
		return 0;
	}

	record Ev(int Turn, int Type, int Civ, int X, int Y, int Prev, string Text);

	static Dictionary<string, object> Read(string path, byte[] biq) {
		SavData sav = new(Util.ReadFile(path), biq);
		GAME g = sav.Game;
		var row = new Dictionary<string, object> {
			["file"] = Path.GetFileName(path),
			["dir"] = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path))),
			["turn"] = g.TurnNumber,
			["year"] = g.GameYear,
			["difficulty"] = Safe(() => sav.Bic.Diff[g.DifficultyID].Name),
			["mapWidth"] = sav.Wrld.Width,
			["mapHeight"] = sav.Wrld.Height,
			["percentWater"] = sav.Wrld.PercentWater,
			["victoryType"] = g.VictoryType,
			["winner"] = g.Winner,
		};

		// Civs: LEAD indexes. 0 is the barbarians.
		int leads = sav.Lead.Length;
		var civs = new List<Dictionary<string, object>>();
		int human = -1;
		for (int i = 1; i < leads; i++) {
			LEAD l = sav.Lead[i];
			if (l.RaceID < 0 || sav.Bic.Race == null || l.RaceID >= sav.Bic.Race.Length) continue;
			bool isHuman = g.HumanPlayers[i];
			if (isHuman && human < 0) human = i;
			civs.Add(new() {
				["lead"] = i, ["civ"] = sav.Bic.Race[l.RaceID].Name, ["human"] = isHuman,
				["alive"] = g.RemainingPlayers[i], ["cities"] = l.NumberOfCities,
				["government"] = Safe(() => sav.Bic.Govt[l.Government].Name),
				["military"] = l.NumberOfMilitaryUnits, ["units"] = l.NumberOfUnits,
			});
		}
		row["civs"] = civs;
		row["human"] = human;
		if (human < 0) return row;
		row["humanCiv"] = sav.Bic.Race[sav.Lead[human].RaceID].Name;
		row["humanGovernment"] = Safe(() => sav.Bic.Govt[sav.Lead[human].Government].Name);

		// Units of the human at the save, by type
		var units = new SortedDictionary<string, int>();
		foreach (UNIT u in sav.Unit ?? Array.Empty<UNIT>()) {
			if (u.OwnerID != human) continue;
			string name = Safe(() => sav.Bic.Prto[u.UnitType].Name) ?? ("type" + u.UnitType);
			units[name] = units.GetValueOrDefault(name) + 1;
		}
		row["humanUnits"] = units;

		// Replay events
		var evs = new List<Ev>();
		if (sav.Rplt != null) {
			for (int i = 0; i < sav.Rplt.Length; i++) {
				for (int j = 0; j < sav.RpltRple[i].Length; j++) {
					RPLE e = sav.RpltRple[i][j];
					evs.Add(new Ev(sav.Rplt[i].TurnNumber, e.EventType, e.CivID, e.XLocation, e.YLocation, e.PreviousOwner, sav.RpltRpleDescription[i][j]));
				}
			}
		}
		row["replayEvents"] = evs.Count;
		if (evs.Count == 0) return row;

		int last = Math.Max(g.TurnNumber, evs.Max(e => e.Turn));
		var checkpoints = new List<int>();
		for (int t = 1; t <= last; t++) checkpoints.Add(t);  // every turn; the series keeps every 25th and the last
		int[] thresholds = { 5, 10, 20, 30 };
		var reachTurn = new Dictionary<string, int>();

		var owner = new Dictionary<(int, int), int>();
		var cities = new HashSet<(int, int)>();
		int founded = 0, captured = 0, otherGained = 0, lost = 0;
		int firstCapture = -1, firstLoss = -1, firstFlipIn = -1;
		var capturesByTurn = new List<object>();
		var foundTurns = new List<int>();
		var series = new List<Dictionary<string, object>>();
		var ordered = evs.OrderBy(e => e.Turn).ToList();
		int k = 0;
		foreach (int cp in checkpoints) {
			for (; k < ordered.Count && ordered[k].Turn <= cp; k++) {
				Ev e = ordered[k];
				var at = (e.X, e.Y);
				switch (e.Type) {
					case CityPlaced:
					case CityFounded:
						cities.Add(at);
						if (e.Type == CityFounded && e.Civ == human) {
							founded++;
							foundTurns.Add(e.Turn);
						}
						break;
					case CityDestroyed:
					case CityDestroyedKeepsTile:
						if (cities.Remove(at) && owner.GetValueOrDefault(at, 0) == human) lost++;
						break;
					case CityCaptured:
						if (e.Civ == human) {
							captured++;
							if (firstCapture < 0) firstCapture = e.Turn;
							// The tile's owner may already have changed this turn; the territory event keeps who had it.
							Ev change = ordered.FirstOrDefault(x => x.Turn == e.Turn && x.Type == TerritoryChanged && x.X == e.X && x.Y == e.Y && x.Civ == human);
							int from = change != null ? change.Prev : owner.GetValueOrDefault(at, 0);
							capturesByTurn.Add(new object[] { e.Turn, CivName(sav, from), e.Text });
						}
						break;
					case TerritoryChanged: {
						int was = owner.GetValueOrDefault(at, 0);
						int now = e.Civ;
						if (cities.Contains(at) && was != now) {
							if (was == human && now != human) {
								lost++;
								if (firstLoss < 0) firstLoss = e.Turn;
							}
							// A city tile turning to the human with no capture this turn: flip, trade or gift.
							if (now == human && was != 0 && !ordered.Any(x => x.Turn == e.Turn && x.Type == CityCaptured && x.X == e.X && x.Y == e.Y)) {
								otherGained++;
								if (firstFlipIn < 0) firstFlipIn = e.Turn;
							}
						}
						if (now == 0) owner.Remove(at); else owner[at] = now;
						break;
					}
				}
			}
			var perCiv = new Dictionary<int, int>();
			foreach (var c in cities) {
				int o = owner.GetValueOrDefault(c, 0);
				perCiv[o] = perCiv.GetValueOrDefault(o) + 1;
			}
			var tiles = owner.Values.GroupBy(v => v).ToDictionary(x => x.Key, x => x.Count());
			var aiCities = civs.Where(c => !(bool)c["human"]).Select(c => perCiv.GetValueOrDefault((int)c["lead"])).Where(n => n > 0).ToList();
			foreach (int n in thresholds) {
				if (perCiv.GetValueOrDefault(human) >= n && !reachTurn.ContainsKey($"city{n}turn")) reachTurn[$"city{n}turn"] = cp;
			}
			if (cp % 25 != 0 && cp != last) continue;
			var point = new Dictionary<string, object> {
				["turn"] = cp,
				["cities"] = perCiv.GetValueOrDefault(human),
				["tiles"] = tiles.GetValueOrDefault(human),
				["founded"] = founded,
				["captured"] = captured,
				["otherGained"] = otherGained,
				["lost"] = lost,
				["aiCitiesMean"] = aiCities.Count > 0 ? Math.Round(aiCities.Average(), 1) : 0,
				["aiCitiesMax"] = aiCities.Count > 0 ? aiCities.Max() : 0,
				["aiAlive"] = aiCities.Count,
			};
			AddHist(sav, human, cp, point);
			series.Add(point);
		}
		row["series"] = series;
		row["reach"] = reachTurn;
		row["firstCaptureTurn"] = firstCapture;
		row["foundTurns"] = foundTurns;
		row["lastFoundTurn"] = foundTurns.Count > 0 ? foundTurns[^1] : -1;
		row["firstLossTurn"] = firstLoss;
		row["firstFlipOrTradeInTurn"] = firstFlipIn;
		row["captures"] = capturesByTurn.Take(60).ToList();
		int replayCities = (int)series[^1]["cities"];
		row["citiesCheck"] = new { replay = replayCities, lead = sav.Lead[human].NumberOfCities };
		return row;
	}

	// Score and power of the human and the strongest AI at the turn, from the histograph
	static void AddHist(SavData sav, int human, int turn, Dictionary<string, object> point) {
		if (sav.HistTurn == null) return;
		for (int i = 0; i < sav.HistTurn.Length; i++) {
			if (sav.HistTurn[i].TurnNumber != turn) continue;
			int[] civ = sav.TurnCiv[i];
			int hi = Array.IndexOf(civ, human);
			if (hi < 0) return;
			point["power"] = sav.TurnPower[i][hi];
			point["score"] = sav.TurnScore[i][hi];
			int aiPower = 0;
			for (int j = 0; j < civ.Length; j++) if (j != hi && civ[j] != 0) aiPower = Math.Max(aiPower, sav.TurnPower[i][j]);
			point["aiPowerMax"] = aiPower;
			return;
		}
	}

	static string CivName(SavData sav, int lead) {
		if (lead <= 0 || lead >= sav.Lead.Length) return lead == 0 ? "none" : "?";
		return Safe(() => sav.Bic.Race[sav.Lead[lead].RaceID].Name);
	}

	static T Safe<T>(Func<T> f) {
		try { return f(); } catch { return default; }
	}
}

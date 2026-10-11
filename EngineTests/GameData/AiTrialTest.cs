using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.GameData;

// Measures whether an AI change (an AiExperiments switch) makes the AI
// stronger. In each of several generated games, half the civs play with the
// switches on and half without, alternating which half from game to game so
// that neither gets the better starts, and the two halves are compared after
// a number of turns.
//
// It does nothing unless C7_TRIAL_OUT names a file for the results.
//   C7_TRIAL_EXPERIMENTS  the switches, comma separated ("" compares the AI
//                         with itself, to see how much the numbers vary)
//   C7_TRIAL_GAMES        games to play (default 8)
//   C7_TRIAL_SIZE         map width and height (default 100)
//   C7_TRIAL_CIVS         civs per game (default 8)
//   C7_TRIAL_TURNS        turns per game (default 200)
//   C7_TRIAL_DIFFICULTY   index into the difficulties (default 3, Monarch)
//   C7_TRIAL_LANDFORM     Pangaea, Continents or Archipelago (default Continents)
//   C7_TRIAL_SEED         seed of the first game (default 1000)
// For example:
//   C7_TRIAL_OUT=trial.txt C7_TRIAL_EXPERIMENTS=cheap-maintenance dotnet test EngineTests --filter "FullyQualifiedName~AiTrialTest"
public class AiTrialTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;

	public AiTrialTest(SaveGameFixture fixture, ITestOutputHelper output) {
		this.fixture = fixture;
		this.output = output;
		// Engine message queues are static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	private static int Setting(string name, int fallback) {
		return int.TryParse(Environment.GetEnvironmentVariable(name), out int n) ? n : fallback;
	}

	// What is measured of each civ at the end of a game.
	private sealed class Measures {
		public static readonly string[] Names = [
			"score", "cities", "citizens", "land", "techs", "wonders", "military", "captured", "lost", "alive",
			// Great wonder builds: begun, lost to a rival who finished the
			// wonder first, the shields in the box when they were lost, and
			// the city-turns spent building great wonders (lost ones too).
			"wStarted", "wLost", "wLostSh", "wCityTurns",
		];
		public readonly double[] values = new double[Names.Length];
	}

	// Follows each city's great wonder builds turn by turn: when one starts,
	// and whether it ends completed, lost to a rival or dropped.
	private sealed class WonderTracker {
		private sealed class Build {
			public Building wonder;
			public Player owner;
			public int startTurn;
			public int shields;
		}

		private readonly Dictionary<ID, Build> builds = new();
		public readonly Dictionary<Player, int> started = new();
		public readonly Dictionary<Player, int> lost = new();
		public readonly Dictionary<Player, int> lostShields = new();
		public readonly Dictionary<Player, int> cityTurns = new();
		// Per civ, the name of each completed wonder and the turns it took.
		public readonly Dictionary<Player, List<(string name, int turns)>> completed = new();

		private static void Add(Dictionary<Player, int> d, Player p, int n) {
			d[p] = d.GetValueOrDefault(p) + n;
		}

		public void Update(C7GameData.GameData gameData) {
			int turn = gameData.turn;
			foreach (City city in gameData.cities) {
				Building current = city.itemBeingProduced is Building b && b.IsGreatWonder() ? b : null;
				builds.TryGetValue(city.id, out Build build);
				if (build != null && (build.owner != city.owner || current != build.wonder)) {
					if (build.owner == city.owner && city.constructed_buildings.Exists(cb => cb.building == build.wonder)) {
						if (!completed.TryGetValue(build.owner, out List<(string name, int turns)> list)) {
							completed[build.owner] = list = new();
						}
						list.Add((build.wonder.name, turn - build.startTurn));
					} else if (gameData.GreatWondersBuilt.Contains(build.wonder.name)) {
						Add(lost, build.owner, 1);
						Add(lostShields, build.owner, build.shields);
					}
					builds.Remove(city.id);
					build = null;
				}
				if (current != null) {
					if (build == null) {
						build = new Build { wonder = current, owner = city.owner, startTurn = turn };
						builds[city.id] = build;
						Add(started, city.owner, 1);
					}
					build.shields = city.shieldsStored;
					Add(cityTurns, city.owner, 1);
				}
			}
		}
	}

	private static SaveGame NewSave(int size, int civs, int seed, int difficulty, WorldCharacteristics.Landform landform) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = landform,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() {
				width = size,
				height = size,
				numberOfCivs = civs,
				distanceBetweenCivs = Math.Max(6, size / 8),
				techRate = 240,
				optimalNumberOfCities = 20,
			},
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			difficulty = save.Difficulties[Math.Clamp(difficulty, 0, save.Difficulties.Count - 1)],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, civs - 1).ToList(),
			victoryConditions = new VictoryConditions(),
		}.Populate(save);
		return save;
	}

	[Fact]
	public async Task CompareAiWithAndWithoutExperiments() {
		string outPath = Environment.GetEnvironmentVariable("C7_TRIAL_OUT");
		if (string.IsNullOrEmpty(outPath)) {
			return;
		}
		string[] experiments = (Environment.GetEnvironmentVariable("C7_TRIAL_EXPERIMENTS") ?? "")
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		int games = Setting("C7_TRIAL_GAMES", 8);
		int size = Setting("C7_TRIAL_SIZE", 100);
		int civs = Setting("C7_TRIAL_CIVS", 8);
		int turns = Setting("C7_TRIAL_TURNS", 200);
		int difficulty = Setting("C7_TRIAL_DIFFICULTY", 3);
		int firstSeed = Setting("C7_TRIAL_SEED", 1000);
		WorldCharacteristics.Landform landform = Enum.TryParse(Environment.GetEnvironmentVariable("C7_TRIAL_LANDFORM"), out WorldCharacteristics.Landform l)
			? l : WorldCharacteristics.Landform.Continents;

		List<string> lines = [$"experiments={string.Join(",", experiments)} games={games} size={size} civs={civs} turns={turns} difficulty={difficulty} landform={landform}"];
		// Per game, the average of each measure over the trial civs minus
		// the average over the others.
		List<double[]> differences = new();
		double[] trialSums = new double[Measures.Names.Length];
		double[] otherSums = new double[Measures.Names.Length];
		double[] allTechs = new double[games];
		// Over all games: the turns each completed great wonder took, and
		// how often each wonder was built, by the trial civs and the others.
		List<int> trialWonderTurns = new(), otherWonderTurns = new();
		Dictionary<string, (int trial, int others)> wondersByName = new();
		Stopwatch total = Stopwatch.StartNew();

		for (int game = 0; game < games; ++game) {
			int seed = firstSeed + game;
			(List<Measures> trial, List<Measures> others, double averageTechs, WonderTracker wonders, HashSet<Player> trialCivs) =
				await PlayGame(NewSave(size, civs, seed, difficulty, landform), experiments, turns, trialHalf: game % 2);
			foreach ((Player p, List<(string name, int turns)> list) in wonders.completed) {
				bool isTrial = trialCivs.Contains(p);
				foreach ((string name, int t) in list) {
					(isTrial ? trialWonderTurns : otherWonderTurns).Add(t);
					(int tr, int ot) = wondersByName.GetValueOrDefault(name);
					wondersByName[name] = isTrial ? (tr + 1, ot) : (tr, ot + 1);
				}
			}
			double[] difference = new double[Measures.Names.Length];
			for (int m = 0; m < Measures.Names.Length; ++m) {
				double t = trial.Average(x => x.values[m]);
				double o = others.Average(x => x.values[m]);
				difference[m] = t - o;
				trialSums[m] += t;
				otherSums[m] += o;
			}
			differences.Add(difference);
			allTechs[game] = averageTechs;
			string line = $"game {game} seed {seed}: " + string.Join(" ", Measures.Names.Select((n, m) => $"{n}={difference[m]:+0.0;-0.0}"));
			lines.Add(line);
			output.WriteLine(line);
			File.WriteAllLines(outPath, lines);
		}

		lines.Add("");
		lines.Add("measure: trial civs vs the others (average per civ), difference +- standard error over games");
		for (int m = 0; m < Measures.Names.Length; ++m) {
			double mean = differences.Average(d => d[m]);
			double sd = games > 1 ? Math.Sqrt(differences.Sum(d => (d[m] - mean) * (d[m] - mean)) / (games - 1)) : 0;
			double se = sd / Math.Sqrt(games);
			lines.Add($"{Measures.Names[m],-9} {trialSums[m] / games,8:0.0} vs {otherSums[m] / games,8:0.0}   {mean:+0.00;-0.00} +- {se:0.00}{(se > 0 && Math.Abs(mean) > 2 * se ? "  *" : "")}");
		}
		lines.Add($"techs known per civ at the end, all civs: {allTechs.Average():0.0}");
		static string TurnsSummary(List<int> t) => t.Count == 0 ? "none" : $"{t.Count} wonders, {t.Average():0.0} turns on average (median {t.OrderBy(x => x).ElementAt(t.Count / 2)})";
		lines.Add($"great wonders completed by the trial civs: {TurnsSummary(trialWonderTurns)}");
		lines.Add($"great wonders completed by the others:     {TurnsSummary(otherWonderTurns)}");
		lines.Add("great wonders built (trial/others): " + string.Join(", ", wondersByName.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value.trial}/{kv.Value.others}")));
		lines.Add($"total ms={total.ElapsedMilliseconds}");
		File.WriteAllLines(outPath, lines);
		foreach (string line in lines) {
			output.WriteLine(line);
		}
	}

	private async Task<(List<Measures> trial, List<Measures> others, double averageTechs, WonderTracker wonders, HashSet<Player> trialCivs)> PlayGame(SaveGame save, string[] experiments, int turns, int trialHalf) {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await CreateGame.createGame(save, (_) => fixture.behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;

		gameData.observerMode = true;
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		HashSet<Player> trialCivs = new();
		for (int i = 0; i < civs.Count; ++i) {
			civs[i].isHuman = false;
			if (i % 2 == trialHalf) {
				trialCivs.Add(civs[i]);
				foreach (string experiment in experiments) {
					AiExperiments.Enable(civs[i], experiment);
				}
			}
		}

		Dictionary<Player, int> captured = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> lost = civs.ToDictionary(p => p, _ => 0);
		Dictionary<ID, Player> cityOwners = new();
		void CountCaptures() {
			foreach (City city in gameData.cities) {
				if (cityOwners.TryGetValue(city.id, out Player before) && before != city.owner) {
					if (captured.ContainsKey(city.owner)) {
						++captured[city.owner];
					}
					if (lost.ContainsKey(before)) {
						++lost[before];
					}
				}
				cityOwners[city.id] = city.owner;
			}
		}

		WonderTracker wonders = new();
		try {
			TurnHandling.OnBeginTurn();
			TurnHandling.InitTurnData();
			Task firstTurn = TurnHandling.AdvanceTurn();
			EngineWaits.WaitForStartTurnMessage(_ => { });
			await firstTurn;
			for (int turn = 0; turn < turns && !gameData.gameOver; ++turn) {
				new MsgEndTurn { turn = gameData.turn }.send();
				EngineWaits.WaitForStartTurnMessage(_ => { });
				CountCaptures();
				wonders.Update(gameData);
			}
		} finally {
			gameData.observerMode = false;
		}

		Dictionary<Player, int> land = civs.ToDictionary(p => p, _ => 0);
		foreach (Tile t in gameData.map.tiles) {
			if (t.OwningPlayer() is Player owner && land.ContainsKey(owner)) {
				++land[owner];
			}
		}

		Measures Measure(Player p) {
			Measures m = new();
			int i = 0;
			m.values[i++] = p.defeated ? 0 : ScoreVictory.ComputeTurnScore(p, gameData);
			m.values[i++] = p.cities.Count;
			m.values[i++] = p.cities.Sum(c => c.residents.Count);
			m.values[i++] = land[p];
			m.values[i++] = p.knownTechs.Count;
			m.values[i++] = p.cities.Sum(c => c.constructed_buildings.Count(b => b.building.IsGreatWonder()));
			m.values[i++] = p.units.Where(u => u.IsLandUnit() && u.unitType.attack > 0).Sum(u => u.unitType.attack + u.unitType.defense);
			m.values[i++] = captured[p];
			m.values[i++] = lost[p];
			m.values[i++] = p.defeated ? 0 : 1;
			m.values[i++] = wonders.started.GetValueOrDefault(p);
			m.values[i++] = wonders.lost.GetValueOrDefault(p);
			m.values[i++] = wonders.lostShields.GetValueOrDefault(p);
			m.values[i++] = wonders.cityTurns.GetValueOrDefault(p);
			return m;
		}

		List<Measures> trial = civs.Where(trialCivs.Contains).Select(Measure).ToList();
		List<Measures> others = civs.Where(p => !trialCivs.Contains(p)).Select(Measure).ToList();
		return (trial, others, civs.Average(p => p.knownTechs.Count), wonders, trialCivs);
	}
}

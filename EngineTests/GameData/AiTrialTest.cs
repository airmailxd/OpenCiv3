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
			// How the civ makes war: wars it declared, turns it spent at war
			// with another civ, attacks it made (and won), units it killed
			// and lost in battle, and the turn it first took a city (the
			// game's length if it never did).
			"wars", "warTurns", "attacks", "attackWins", "kills", "unitsLost", "firstCap",
		];
		public readonly double[] values = new double[Names.Length];
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
		Stopwatch total = Stopwatch.StartNew();

		for (int game = 0; game < games; ++game) {
			int seed = firstSeed + game;
			(List<Measures> trial, List<Measures> others, double averageTechs) =
				await PlayGame(NewSave(size, civs, seed, difficulty, landform), experiments, turns, trialHalf: game % 2);
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
		lines.Add($"total ms={total.ElapsedMilliseconds}");
		File.WriteAllLines(outPath, lines);
		foreach (string line in lines) {
			output.WriteLine(line);
		}
	}

	private async Task<(List<Measures> trial, List<Measures> others, double averageTechs)> PlayGame(SaveGame save, string[] experiments, int turns, int trialHalf) {
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
		Dictionary<Player, int> firstCapture = civs.ToDictionary(p => p, _ => turns);
		Dictionary<Player, int> wars = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> warTurns = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> attacks = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> attackWins = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> kills = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> unitsLost = civs.ToDictionary(p => p, _ => 0);
		void Add(Dictionary<Player, int> counts, Player p) {
			if (p != null && counts.ContainsKey(p)) {
				++counts[p];
			}
		}
		AiExperiments.onWarDeclared = (aggressor, _) => Add(wars, aggressor);
		AiExperiments.onCombat = (attacker, defender, result) => {
			Add(attacks, attacker.owner);
			if (result == CombatResult.DefenderKilled || result == CombatResult.DefenderRetreated) {
				Add(attackWins, attacker.owner);
			}
			if (result == CombatResult.DefenderKilled) {
				Add(kills, attacker.owner);
				Add(unitsLost, defender.owner);
			} else if (result == CombatResult.AttackerKilled) {
				Add(kills, defender.owner);
				Add(unitsLost, attacker.owner);
			}
		};
		void CountCaptures() {
			foreach (City city in gameData.cities) {
				if (cityOwners.TryGetValue(city.id, out Player before) && before != city.owner) {
					if (captured.ContainsKey(city.owner)) {
						++captured[city.owner];
						firstCapture[city.owner] = Math.Min(firstCapture[city.owner], gameData.turn);
					}
					if (lost.ContainsKey(before)) {
						++lost[before];
					}
				}
				cityOwners[city.id] = city.owner;
			}
		}

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
				foreach (Player p in civs) {
					if (!p.defeated && civs.Any(o => o != p && !o.defeated && p.playerRelationships.TryGetValue(o.id, out PlayerRelationship r) && r.AtWar())) {
						++warTurns[p];
					}
				}
			}
		} finally {
			gameData.observerMode = false;
			AiExperiments.onWarDeclared = null;
			AiExperiments.onCombat = null;
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
			m.values[i++] = wars[p];
			m.values[i++] = warTurns[p];
			m.values[i++] = attacks[p];
			m.values[i++] = attackWins[p];
			m.values[i++] = kills[p];
			m.values[i++] = unitsLost[p];
			m.values[i++] = firstCapture[p];
			return m;
		}

		List<Measures> trial = civs.Where(trialCivs.Contains).Select(Measure).ToList();
		List<Measures> others = civs.Where(p => !trialCivs.Contains(p)).Select(Measure).ToList();
		return (trial, others, civs.Average(p => p.knownTechs.Count));
	}
}

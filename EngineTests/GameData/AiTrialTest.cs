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
//   C7_TRIAL_PAIRED       1 to also play each game with no switches on and
//                         take its trial-vs-others difference off: games are
//                         deterministic, so the halves' unequal starts cancel
//                         out, which removes much of the noise (about half the
//                         standard error) for twice the time
//   C7_TRIAL_FLIPS        1 to list every culture flip, with its chance
// For example:
//   C7_TRIAL_OUT=trial.txt C7_TRIAL_EXPERIMENTS=cheap-maintenance dotnet test EngineTests --filter "FullyQualifiedName~AiTrialTest"
public class AiTrialTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;
	// With C7_TRIAL_FLIPS, the culture flips of the game being played.
	private readonly List<string> flipLines = new();

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
			// Economy: the treasury, buildings (not wonders), building
			// upkeep per turn and units.
			"gold", "buildings", "upkeep", "units",
			// Great wonder builds: begun, lost to a rival who finished the
			// wonder first, the shields in the box when they were lost, and
			// the city-turns spent building great wonders (lost ones too).
			"wStarted", "wLost", "wLostSh", "wCityTurns",
			// How fast the empire develops, which shows well before the
			// end: the turns by which it had 5 and 10 cities (the game's
			// length if it never did), its techs, cities and citizens
			// halfway through, and the turns the civ spent in anarchy and in
			// a government with Despotism's tile penalty, and its cities
			// spent in disorder. At the end: gold and beakers per turn, and
			// the percentage of its worked tiles that are unimproved.
			"city5turn", "city10turn", "midtechs", "midcities", "midcitizens", "anarchy", "despotism", "disorder",
			"gpt", "beakers", "unimproved",
			// How the civ makes war: wars it declared, turns it spent at war
			// with another civ, attacks it made (and won), units it killed
			// and lost in battle, and the turn it first took a city (the
			// game's length if it never did).
			"wars", "warTurns", "attacks", "attackWins", "kills", "unitsLost", "firstCap",
			// Great leaders and armies: elite land units at the end (only
			// they produce leaders), leaders that appeared, armies the
			// civ had (formed by a leader or built), whether an army of its
			// won a battle, armies it lost, and
			// cities it captured with an army on the city's tile.
			"elites", "leaders", "armies", "armywon", "armylost", "armycaps",
			// Hurried production: items the AI hurried, and the gold it
			// paid for them.
			"rushes", "rushGold",
			// Rights of passage the civ signed when it asked for one (see
			// RightOfPassageAI), as either side.
			"rops",
			// Warships (see NavalAI) at the end, and ships the civ sank in
			// battle.
			"warships", "shipKills",
			// Cities the civ gained and lost by culture flips (also
			// counted in captured and lost), and its cities at the end
			// with another city within 2 tiles.
			"flipsIn", "flipsOut", "closeCities",
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

	// Per civ, how its development went over a game (see Measures).
	private sealed class Development {
		public int city5Turn, city10Turn, midTechs, midCities, midCitizens, anarchy, despotism, disorder;
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
		bool paired = Setting("C7_TRIAL_PAIRED", 0) != 0;

		List<string> lines = [$"experiments={string.Join(",", experiments)} games={games} size={size} civs={civs} turns={turns} difficulty={difficulty} landform={landform}{(paired ? " paired" : "")}"];
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
			if (paired) {
				// The same game with every civ playing as it does now: how the
				// trial half fares anyway.
				(List<Measures> baseTrial, List<Measures> baseOthers, _, _, _) =
					await PlayGame(NewSave(size, civs, seed, difficulty, landform), [], turns, trialHalf: game % 2);
				for (int m = 0; m < Measures.Names.Length; ++m) {
					double t = baseTrial.Average(x => x.values[m]);
					double o = baseOthers.Average(x => x.values[m]);
					difference[m] -= t - o;
					// The summary then compares the trial civs with
					// themselves without the switches.
					otherSums[m] += t - others.Average(x => x.values[m]);
				}
			}
			lines.AddRange(flipLines);
			flipLines.Clear();
			differences.Add(difference);
			allTechs[game] = averageTechs;
			string line = $"game {game} seed {seed}: " + string.Join(" ", Measures.Names.Select((n, m) => $"{n}={difference[m]:+0.0;-0.0}"));
			lines.Add(line);
			output.WriteLine(line);
			File.WriteAllLines(outPath, lines);
		}

		lines.Add("");
		lines.Add(paired
			? "measure: trial civs vs the same civs without the switches (average per civ), difference net of the others' +- standard error over games"
			: "measure: trial civs vs the others (average per civ), difference +- standard error over games");
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
		Dictionary<Player, Development> development = civs.ToDictionary(p => p, _ => new Development { city5Turn = turns, city10Turn = turns });
		void RecordDevelopment(int turn) {
			foreach (Player p in civs) {
				Development d = development[p];
				if (p.cities.Count >= 5 && d.city5Turn == turns) {
					d.city5Turn = turn;
				}
				if (p.cities.Count >= 10 && d.city10Turn == turns) {
					d.city10Turn = turn;
				}
				if (turn == turns / 2) {
					d.midTechs = p.knownTechs.Count;
					d.midCities = p.cities.Count;
					d.midCitizens = p.cities.Sum(c => c.residents.Count);
				}
				if (p.defeated) {
					continue;
				}
				if (p.government.transitionType) {
					++d.anarchy;
				} else if (p.government.hasTilePenalty) {
					++d.despotism;
				}
				d.disorder += p.cities.Count(c => c.isInCivilDisorder);
			}
		}
		Dictionary<Player, int> firstCapture = civs.ToDictionary(p => p, _ => turns);
		Dictionary<Player, int> wars = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> warTurns = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> attacks = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> attackWins = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> kills = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> unitsLost = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> shipKills = civs.ToDictionary(p => p, _ => 0);
		void Add(Dictionary<Player, int> counts, Player p) {
			if (p != null && counts.ContainsKey(p)) {
				++counts[p];
			}
		}
		AiExperiments.onWarDeclared = (aggressor, _) => Add(wars, aggressor);
		Dictionary<Player, int> rushes = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> rushGold = civs.ToDictionary(p => p, _ => 0);
		AiExperiments.onHurry = (city, gold) => {
			if (rushes.ContainsKey(city.owner)) {
				++rushes[city.owner];
				rushGold[city.owner] += gold;
			}
		};
		Dictionary<Player, int> rops = civs.ToDictionary(p => p, _ => 0);
		RightOfPassageAI.onSigned = (asker, partner) => {
			Add(rops, asker);
			Add(rops, partner);
		};
		AiExperiments.onCombat = (attacker, defender, result) => {
			Add(attacks, attacker.owner);
			if (result == CombatResult.DefenderKilled || result == CombatResult.DefenderRetreated) {
				Add(attackWins, attacker.owner);
			}
			if (result == CombatResult.DefenderKilled) {
				Add(kills, attacker.owner);
				Add(unitsLost, defender.owner);
				if (defender.IsWaterUnit()) {
					Add(shipKills, attacker.owner);
				}
			} else if (result == CombatResult.AttackerKilled) {
				Add(kills, defender.owner);
				Add(unitsLost, attacker.owner);
				if (attacker.IsWaterUnit()) {
					Add(shipKills, defender.owner);
				}
			}
		};
		Dictionary<Player, int> flipsIn = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> flipsOut = civs.ToDictionary(p => p, _ => 0);
		bool listFlips = Setting("C7_TRIAL_FLIPS", 0) != 0;
		AiExperiments.onCultureFlip = (city, from, c) => {
			Add(flipsIn, city.owner);
			Add(flipsOut, from);
			if (listFlips) {
				flipLines.Add($"flip turn {gameData.turn}: {city.name} size {city.residents.Count} {from.civilization.name} -> {city.owner.civilization.name}"
					+ $" F={c.foreigners} T={c.tiles} Cc={c.memoryFactor} H={c.moodFactor} culture={c.otherCulture}/{c.ownerCulture}={c.cultureRatio:0.00} owner cities={from.cities.Count} G={c.garrison} d={c.distanceRatio:0.00} p={c.probability:0.0000}");
			}
		};
		Dictionary<Player, HashSet<ID>> leaders = civs.ToDictionary(p => p, _ => new HashSet<ID>());
		Dictionary<Player, HashSet<ID>> armies = civs.ToDictionary(p => p, _ => new HashSet<ID>());
		Dictionary<Player, int> armyCaptures = civs.ToDictionary(p => p, _ => 0);
		void CountCaptures() {
			foreach (Player p in civs) {
				foreach (MapUnit u in p.units) {
					if (u.IsLeader()) {
						leaders[p].Add(u.id);
					} else if (u.IsArmy()) {
						armies[p].Add(u.id);
					}
				}
			}
			foreach (City city in gameData.cities) {
				if (cityOwners.TryGetValue(city.id, out Player before) && before != city.owner) {
					if (captured.ContainsKey(city.owner)) {
						++captured[city.owner];
						firstCapture[city.owner] = Math.Min(firstCapture[city.owner], gameData.turn);
						if (city.location.unitsOnTile.Any(u => u.IsArmy() && u.owner == city.owner)) {
							++armyCaptures[city.owner];
						}
					}
					if (lost.ContainsKey(before)) {
						++lost[before];
					}
				}
				cityOwners[city.id] = city.owner;
			}
		}

		WonderTracker wonders = new();

		// A game can end during the AIs' turns, by a victory or by the
		// destruction of the civ being observed, and then no turn starts
		// again.
		void WaitForTurnOrGameOver() {
			while (EngineWaits.WaitForMessageToUI<MessageToUI>() is not MsgStartTurn && !gameData.gameOver) {
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
				WaitForTurnOrGameOver();
				CountCaptures();
				wonders.Update(gameData);
				RecordDevelopment(turn + 1);
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
			AiExperiments.onHurry = null;
			AiExperiments.onCultureFlip = null;
			RightOfPassageAI.onSigned = null;
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
			m.values[i++] = p.gold;
			m.values[i++] = p.cities.Sum(c => c.constructed_buildings.Count(b => !b.building.IsGreatWonder() && !b.building.isSmallWonder));
			m.values[i++] = p.MaintenanceCosts();
			m.values[i++] = p.units.Count;
			m.values[i++] = wonders.started.GetValueOrDefault(p);
			m.values[i++] = wonders.lost.GetValueOrDefault(p);
			m.values[i++] = wonders.lostShields.GetValueOrDefault(p);
			m.values[i++] = wonders.cityTurns.GetValueOrDefault(p);
			Development d = development[p];
			m.values[i++] = d.city5Turn;
			m.values[i++] = d.city10Turn;
			m.values[i++] = d.midTechs;
			m.values[i++] = d.midCities;
			m.values[i++] = d.midCitizens;
			m.values[i++] = d.anarchy;
			m.values[i++] = d.despotism;
			m.values[i++] = d.disorder;
			m.values[i++] = p.defeated ? 0 : p.CalculateGoldPerTurn();
			m.values[i++] = p.defeated ? 0 : p.BeakersPerTurn();
			int worked = 0, unimproved = 0;
			foreach (City c in p.cities) {
				foreach (CityResident r in c.residents) {
					if (r.tileWorked != null && r.tileWorked != Tile.NONE) {
						++worked;
						if (!r.tileWorked.overlays.HasBeenImproved()) {
							++unimproved;
						}
					}
				}
			}
			m.values[i++] = worked == 0 ? 0 : 100.0 * unimproved / worked;
			m.values[i++] = wars[p];
			m.values[i++] = warTurns[p];
			m.values[i++] = attacks[p];
			m.values[i++] = attackWins[p];
			m.values[i++] = kills[p];
			m.values[i++] = unitsLost[p];
			m.values[i++] = firstCapture[p];
			m.values[i++] = p.units.Count(u => u.IsLandUnit() && !u.IsArmy() && u.IsElite());
			m.values[i++] = leaders[p].Count;
			m.values[i++] = armies[p].Count;
			m.values[i++] = p.hasVictoriousArmy ? 1 : 0;
			m.values[i++] = armies[p].Count(id => !p.units.Any(u => u.id == id));
			m.values[i++] = armyCaptures[p];
			m.values[i++] = rushes[p];
			m.values[i++] = rushGold[p];
			m.values[i++] = rops[p];
			m.values[i++] = p.units.Count(C7Engine.AI.UnitAI.NavalAI.IsWarship);
			m.values[i++] = shipKills[p];
			m.values[i++] = flipsIn[p];
			m.values[i++] = flipsOut[p];
			m.values[i++] = p.cities.Count(c => gameData.cities.Any(o => o != c && o.location.DistanceTo(c.location) <= 2));
			return m;
		}

		List<Measures> trial = civs.Where(trialCivs.Contains).Select(Measure).ToList();
		List<Measures> others = civs.Where(p => !trialCivs.Contains(p)).Select(Measure).ToList();
		return (trial, others, civs.Average(p => p.knownTechs.Count), wonders, trialCivs);
	}
}

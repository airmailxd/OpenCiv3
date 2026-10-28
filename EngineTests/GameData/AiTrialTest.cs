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
//   C7_TRIAL_SCENARIO     None, EternalWar, RisingTide or KingsAndNomads
//                         (default None), to play the games as that scenario
//   C7_TRIAL_PAIRED       1 to also play each game with no switches on and
//                         take its trial-vs-others difference off: games are
//                         deterministic, so the halves' unequal starts cancel
//                         out, which removes much of the noise (about half the
//                         standard error) for twice the time
//   C7_TRIAL_FLIPS        1 to list every culture flip, with its chance
//   C7_TRIAL_RULES        mdj to play under MPTournamentMDJ.biq's rules
//                         (see GameSetup.Mdj.cs) rather than Civ3's
//   C7_TRIAL_TIDE         1 to list, under Rising Tide, each civ's state
//                         every few turns: its cities with the waves each
//                         has left, its settlers and other units, and the
//                         deepest land left
//   C7_TRIAL_WARS         1 to list every war between civs: who declared it
//                         on whom and when, whether the target was overseas
//                         (no city on a landmass where the aggressor had
//                         one), the engine class that declared it, the attacks and city captures between the
//                         two while it lasted, and when it ended
//   C7_TRIAL_ALL          1 to turn the switches on for every civ: there is
//                         then no half to compare with, so the summary's two
//                         columns are the same average over all civs, to
//                         compare with a run without the switches (the same
//                         seeds, C7_TRIAL_EXPERIMENTS="") for whether the
//                         world as a whole still plays sensibly
// For example:
//   C7_TRIAL_OUT=trial.txt C7_TRIAL_EXPERIMENTS=cheap-maintenance dotnet test EngineTests --filter "FullyQualifiedName~AiTrialTest"
public class AiTrialTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;
	// With C7_TRIAL_FLIPS, the culture flips of the game being played.
	private readonly List<string> flipLines = new();
	// With C7_TRIAL_TIDE, the civs' state through a Rising Tide game.
	private readonly List<string> tideLines = new();
	// With C7_TRIAL_WARS, the wars declared in the game being played.
	private readonly List<string> warLines = new();

	// A war declared during a game, for C7_TRIAL_WARS.
	private sealed class WarRecord {
		public Player aggressor, defender;
		public int start, end = -1;
		// Whether the defender had no city on a landmass where the
		// aggressor had one when war was declared.
		public bool overseas;
		public int attacks, captures;
		// Whether it ended in a peace that the two broke again at once.
		public bool renewed;
		// The engine class whose code declared it.
		public string via;

		public bool Between(Player a, Player b) {
			return (a == aggressor && b == defender) || (a == defender && b == aggressor);
		}
	}

	// The engine class that is declaring war, from the call stack: the
	// first frame outside Player and the test.
	private static string DeclaredBy() {
		foreach (StackFrame frame in new StackTrace().GetFrames()) {
			Type type = frame.GetMethod()?.DeclaringType;
			while (type?.DeclaringType != null) {
				type = type.DeclaringType;
			}
			if (type == null || type == typeof(Player) || type == typeof(AiTrialTest) || type.Namespace?.StartsWith("C7") != true) {
				continue;
			}
			return type.Name;
		}
		return "?";
	}

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

	// Writes each log message, on a line of its own, to a file.
	private sealed class FileSink(string path) : Serilog.Core.ILogEventSink {
		private readonly StreamWriter writer = new(path) { AutoFlush = true };
		public void Emit(Serilog.Events.LogEvent e) {
			lock (writer) {
				writer.WriteLine(e.RenderMessage());
			}
		}
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
			// Turns the civ spent mobilized for war (see Mobilization).
			"mobilized",
			// The Mercenary Market (Eternal War): units the civ bought, and
			// the gold it paid for them.
			"mktBought", "mktGold",
			// Kings & Nomads: cities the civ took from the independents
			// (city-states, and fallen civs' cities), and the turn it fell
			// (the game's length if it didn't).
			"freeCaps", "fellTurn",
			// The turn the game was on at the end, which is short of the
			// turns asked for when someone won.
			"endTurn",
			// Rising Tide (see RisingTide): the civ's cities the tide
			// destroyed and its units it drowned, and the turn the civ was
			// destroyed (the game's length if it never was); its score as
			// the game ended (a fallen civ's as it fell, see
			// RisingTide.ScoreBonus) and whether it won.
			"tideCities", "drowned", "deathTurn", "rtScore", "won",
			// Kings & Nomads' trade with the city-states (see
			// CityStateTrade): the city-states the civ's roads reach at the
			// end and halfway through, and the gold the trade brought in
			// over the game.
			"csConn", "midCsConn", "csGold",
			// Workers: those the civ lost (captured or destroyed by an
			// enemy walking onto them, or killed in battle), those of them
			// taken outside its borders, the worker-turns its workers ended
			// outside its borders, those of them with no defender of ours on
			// the tile, and the attacks city-state garrisons made on its
			// units.
			"wkLost", "wkLostOut", "wkOut", "wkOutAlone", "csAttacks",
			// Kings & Nomads: the attacks the civ made with its King on or
			// next to the attacker's tile (so at +100%, see
			// MapUnit.KingsPresenceBonus).
			"kingAtk",
			// Kings & Nomads: the world's city-states' citizens and
			// specialists on average at the end, and the citizens of those
			// the civ trades with.
			"csSize", "csSpec", "partSize",
			// Kings & Nomads: the turns the civ took its first and second
			// city-states (the game's length if it never did).
			"cs1Turn", "cs2Turn",
			// Cities the civ took from other civs (not city-states or
			// barbarians), and of those, cities it had held before.
			"civCaps", "retaken",
			// Wars the civ started that saw it attack, and that won it a
			// city.
			"warsFought", "warsWon",
			// The civ's cities with barracks at the end.
			"barracks",
			// The share of the civ's land fighters at the end that are
			// veterans or better, in percent.
			"vetPct",
			// Bombard units (catapults and the like) the civ holds at the end.
			"bombarders",
			// Attacks the civ made across a river.
			"riverAtk",
			// The civs the civ has met at the end, and the average turn it
			// met the other civs (the game's length for one it never met).
			"met", "metTurn",
			// Barbarian units the civ killed.
			"barbKills",
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
		public int city5Turn, city10Turn, midTechs, midCities, midCitizens, anarchy, despotism, disorder, mobilized, deathTurn;
		public int midCityStates, cityStateGold, workerTurnsOut, workerTurnsOutAlone;
	}

	private static SaveGame NewSave(int size, int civs, int seed, int difficulty, WorldCharacteristics.Landform landform) {
		GameScenario scenario = Enum.TryParse(Environment.GetEnvironmentVariable("C7_TRIAL_SCENARIO"), out GameScenario sc) ? sc : GameScenario.None;
		// C7_TRIAL_RULES=mdj plays under MPTournamentMDJ.biq's rules (see
		// GameSetup.Mdj.cs).
		SaveGame save = Environment.GetEnvironmentVariable("C7_TRIAL_RULES") == "mdj"
			? GameSetup.LoadMdjRules(PathUtils.GameModesDir, new GameMode.Config("civ3"))
			: SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
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
			playerCivilization = save.Civilizations.First(c => !c.IsIndependent),
			difficulty = save.Difficulties[Math.Clamp(difficulty, 0, save.Difficulties.Count - 1)],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, civs - 1).ToList(),
			victoryConditions = new VictoryConditions(),
			scenario = scenario,
		}.Populate(save);
		return save;
	}

	// For C7_TRIAL_TIDE: each civ's cities (size/waves left), settlers,
	// workers and other land units, and what it builds.
	private void TraceTide(C7GameData.GameData gameData, List<Player> civs) {
		tideLines.Add($"tide turn {gameData.turn}: wave {gameData.risingTide.waves}, next in {RisingTide.TurnsUntilNextWave(gameData)}, deepest {TideAI.DeepestLand()}");
		foreach (Player p in civs.Where(p => !p.defeated)) {
			string cities = string.Join(" ", p.cities.OrderByDescending(c => TideAI.WavesUntilDrowned(c.location))
				.Select(c => $"{c.residents.Count}/{TideAI.WavesUntilDrowned(c.location)}:{(c.itemBeingProduced as UnitPrototype)?.name ?? (c.itemBeingProduced is Building b ? "B-" + b.name : "?")}"));
			int settlers = p.units.Count(u => u.unitType.isSettler);
			int workers = p.units.Count(u => u.unitType.isWorker && !u.unitType.isSettler);
			int others = p.units.Count(u => u.IsLandUnit() && !u.unitType.isSettler && !u.unitType.isWorker);
			int war = civs.Count(o => o != p && !o.defeated && p.playerRelationships.TryGetValue(o.id, out PlayerRelationship r) && r.AtWar());
			tideLines.Add($"  {p.civilization.name}: cities [{cities}] settlers={settlers} workers={workers} units={others} wars={war} gold={p.gold}");
		}
	}

	[Fact]
	public async Task CompareAiWithAndWithoutExperiments() {
		string outPath = Environment.GetEnvironmentVariable("C7_TRIAL_OUT");
		if (string.IsNullOrEmpty(outPath)) {
			return;
		}
		// The engine's log, to a file, to see what the AIs do.
		string logPath = Environment.GetEnvironmentVariable("C7_TRIAL_LOG");
		if (!string.IsNullOrEmpty(logPath)) {
			Serilog.Log.Logger = new Serilog.LoggerConfiguration().MinimumLevel.Information()
				.WriteTo.Sink(new FileSink(logPath)).CreateLogger();
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
		bool all = Setting("C7_TRIAL_ALL", 0) != 0;
		if (all) {
			paired = false;
		}

		List<string> lines = [$"scenario={Environment.GetEnvironmentVariable("C7_TRIAL_SCENARIO") ?? "None"} experiments={string.Join(",", experiments)} games={games} size={size} civs={civs} turns={turns} difficulty={difficulty} landform={landform}{(paired ? " paired" : "")}{(all ? " all" : "")}"];
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
				await PlayGame(NewSave(size, civs, seed, difficulty, landform), experiments, turns, trialHalf: all ? -1 : game % 2);
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
			lines.AddRange(tideLines);
			tideLines.Clear();
			lines.AddRange(warLines);
			warLines.Clear();
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
		List<Player> civs = gameData.players.Where(p => !p.IsIndependent).ToList();
		HashSet<Player> trialCivs = new();
		// A trial half of -1 is every civ (C7_TRIAL_ALL).
		for (int i = 0; i < civs.Count; ++i) {
			civs[i].isHuman = false;
			if (trialHalf < 0 || i % 2 == trialHalf) {
				trialCivs.Add(civs[i]);
			}
			if (trialHalf < 0 || i % 2 == trialHalf || Setting("C7_TRIAL_ALL", 0) != 0) {
				foreach (string experiment in experiments) {
					AiExperiments.Enable(civs[i], experiment);
				}
			}
		}

		Dictionary<Player, int> captured = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> lost = civs.ToDictionary(p => p, _ => 0);
		Dictionary<ID, Player> cityOwners = new();
		Dictionary<Player, Development> development = civs.ToDictionary(p => p, _ => new Development { city5Turn = turns, city10Turn = turns, deathTurn = turns });
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
					d.midCityStates = p.defeated ? 0 : gameData.GetTradeNetwork().ConnectedCityStates(p).Count;
					d.midTechs = p.knownTechs.Count;
					d.midCities = p.cities.Count;
					d.midCitizens = p.cities.Sum(c => c.residents.Count);
				}
				if (p.defeated) {
					d.deathTurn = Math.Min(d.deathTurn, turn);
					continue;
				}
				if (p.government.transitionType) {
					++d.anarchy;
				} else if (p.government.hasTilePenalty) {
					++d.despotism;
				}
				d.disorder += p.cities.Count(c => c.isInCivilDisorder);
				if (p.mobilized) {
					++d.mobilized;
				}
				d.cityStateGold += CityStateTrade.Income(gameData, p);
				foreach (MapUnit u in p.units) {
					if (u.unitType.isWorker && u.IsLandUnit() && !u.IsLoaded() && u.location.OwningPlayer() != p && u.location.cityAtTile?.owner != p) {
						++d.workerTurnsOut;
						if (!u.location.unitsOnTile.Any(o => o.owner == p && o.CanDefendOnLand() && !o.unitType.isWorker && !o.unitType.isSettler)) {
							++d.workerTurnsOutAlone;
						}
					}
				}
			}
		}
		Dictionary<Player, int> firstCapture = civs.ToDictionary(p => p, _ => turns);
		Dictionary<Player, List<int>> cityStateCaptures = civs.ToDictionary(p => p, _ => new List<int>());
		Dictionary<Player, int> civCaptures = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> retaken = civs.ToDictionary(p => p, _ => 0);
		// Each city's owners so far.
		Dictionary<ID, HashSet<Player>> heldBy = new();
		Dictionary<Player, int> wars = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> warTurns = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> attacks = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> attackWins = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> kills = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> unitsLost = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> shipKills = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> workersLost = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> workersLostOutside = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> cityStateAttacks = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> kingAttacks = civs.ToDictionary(p => p, _ => 0);
		void Add(Dictionary<Player, int> counts, Player p) {
			if (p != null && counts.ContainsKey(p)) {
				++counts[p];
			}
		}
		bool listWars = Setting("C7_TRIAL_WARS", 0) != 0;
		List<WarRecord> warRecords = new();
		WarRecord OpenWar(Player a, Player b) => warRecords.LastOrDefault(w => w.end < 0 && w.Between(a, b));
		static bool AtWarWith(Player a, Player b) => a.playerRelationships.TryGetValue(b.id, out PlayerRelationship r) && r.AtWar();
		AiExperiments.onWarDeclared = (aggressor, defender) => {
			Add(wars, aggressor);
			if (civs.Contains(aggressor) && civs.Contains(defender)) {
				// Peace made and war declared again within one round of
				// turns, before the check at the end of the round saw the
				// peace.
				if (OpenWar(aggressor, defender) is WarRecord old) {
					old.end = gameData.turn;
					old.renewed = true;
				}
				HashSet<int> land = aggressor.cities.Select(c => c.location.continent).ToHashSet();
				warRecords.Add(new WarRecord {
					aggressor = aggressor,
					defender = defender,
					start = gameData.turn,
					overseas = !defender.cities.Any(c => land.Contains(c.location.continent)),
					via = DeclaredBy(),
				});
			}
		};
		Dictionary<Player, int> rushes = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> rushGold = civs.ToDictionary(p => p, _ => 0);
		AiExperiments.onHurry = (city, gold) => {
			if (rushes.ContainsKey(city.owner)) {
				++rushes[city.owner];
				rushGold[city.owner] += gold;
			}
		};
		Dictionary<Player, int> marketBought = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> marketGold = civs.ToDictionary(p => p, _ => 0);
		Staging.onBuy = (buyer, _, gold) => {
			if (marketBought.ContainsKey(buyer)) {
				++marketBought[buyer];
				marketGold[buyer] += gold;
			}
		};
		Dictionary<Player, int> tideCities = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> drowned = civs.ToDictionary(p => p, _ => 0);
		RisingTide.onCityDrowned = city => Add(tideCities, city.owner);
		RisingTide.onUnitDrowned = unit => Add(drowned, unit.owner);
		Dictionary<Player, int> rops = civs.ToDictionary(p => p, _ => 0);
		RightOfPassageAI.onSigned = (asker, partner) => {
			Add(rops, asker);
			Add(rops, partner);
		};
		AiExperiments.onUnitTaken = (unit, _) => {
			if (unit.unitType.isWorker) {
				Add(workersLost, unit.owner);
				if (unit.location.OwningPlayer() != unit.owner) {
					Add(workersLostOutside, unit.owner);
				}
			}
		};
		Dictionary<Player, int> riverAttacks = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> barbKills = civs.ToDictionary(p => p, _ => 0);
		AiExperiments.onCombat = (attacker, defender, result) => {
			Add(attacks, attacker.owner);
			if (Tile.IsTileValid(attacker.location) && Tile.IsTileValid(defender.location)
					&& C7Engine.AI.UnitAI.CombatAI.CrossesRiver(attacker.location, defender.location)) {
				Add(riverAttacks, attacker.owner);
			}
			if (Tile.IsTileValid(attacker.location) && attacker.owner.units.Any(u => u.unitType.isKing && Tile.IsTileValid(u.location)
					&& u.location.DistanceTo(attacker.location) <= 1)) {
				Add(kingAttacks, attacker.owner);
			}
			if (attacker.owner.isCityStates && attacker.location.cityAtTile != null) {
				Add(cityStateAttacks, defender.owner);
			}
			if (result == CombatResult.DefenderKilled && defender.owner.isBarbarians) {
				Add(barbKills, attacker.owner);
			}
			if (result == CombatResult.DefenderKilled && defender.unitType.isWorker) {
				Add(workersLost, defender.owner);
			}
			if (OpenWar(attacker.owner, defender.owner) is WarRecord war) {
				++war.attacks;
			}
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
		bool listTide = Setting("C7_TRIAL_TIDE", 0) != 0;
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
		Dictionary<Player, int> freeCaptures = civs.ToDictionary(p => p, _ => 0);
		Dictionary<Player, int> fellTurn = civs.ToDictionary(p => p, _ => turns);
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
					if (OpenWar(before, city.owner) is WarRecord war) {
						++war.captures;
					}
					if (captured.ContainsKey(city.owner)) {
						++captured[city.owner];
						firstCapture[city.owner] = Math.Min(firstCapture[city.owner], gameData.turn);
						if (!before.IsIndependent) {
							++civCaptures[city.owner];
							if (heldBy.TryGetValue(city.id, out HashSet<Player> held) && held.Contains(city.owner)) {
								++retaken[city.owner];
							}
						}
						if (before.IsIndependent) {
							++freeCaptures[city.owner];
							if (before.isCityStates) {
								cityStateCaptures[city.owner].Add(gameData.turn);
							}
						}
						if (city.location.unitsOnTile.Any(u => u.IsArmy() && u.owner == city.owner)) {
							++armyCaptures[city.owner];
						}
					}
					if (lost.ContainsKey(before)) {
						++lost[before];
					}
				}
				cityOwners[city.id] = city.owner;
				if (!heldBy.TryGetValue(city.id, out HashSet<Player> owners)) {
					heldBy[city.id] = owners = new();
				}
				owners.Add(city.owner);
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
				if (listTide && RisingTide.IsOn(gameData)) {
					TraceTide(gameData, civs);
				}
				foreach (Player p in civs) {
					if (p.defeated && fellTurn[p] == turns) {
						fellTurn[p] = turn + 1;
					}
				}
				foreach (Player p in civs) {
					if (!p.defeated && civs.Any(o => o != p && !o.defeated && p.playerRelationships.TryGetValue(o.id, out PlayerRelationship r) && r.AtWar())) {
						++warTurns[p];
					}
				}
				foreach (WarRecord w in warRecords) {
					if (w.end < 0 && (w.aggressor.defeated || w.defender.defeated || !AtWarWith(w.aggressor, w.defender))) {
						w.end = gameData.turn;
					}
				}
			}
			foreach (WarRecord w in listWars ? warRecords : []) {
				warLines.Add($"war turn {w.start}: {w.aggressor.civilization.name} -> {w.defender.civilization.name}"
					+ $"{(w.overseas ? " overseas" : "")} via={w.via} attacks={w.attacks} captures={w.captures} "
					+ (w.end < 0 ? "ongoing" : $"ended turn {w.end}{(w.renewed ? " (peace, then war again within the round)" : w.defender.defeated ? " (defender destroyed)" : w.aggressor.defeated ? " (aggressor destroyed)" : "")}"));
			}
		} finally {
			gameData.observerMode = false;
			AiExperiments.onWarDeclared = null;
			AiExperiments.onCombat = null;
			AiExperiments.onHurry = null;
			AiExperiments.onCultureFlip = null;
			AiExperiments.onUnitTaken = null;
			RightOfPassageAI.onSigned = null;
			Staging.onBuy = null;
			RisingTide.onCityDrowned = null;
			RisingTide.onUnitDrowned = null;
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
			m.values[i++] = d.mobilized;
			m.values[i++] = marketBought[p];
			m.values[i++] = marketGold[p];
			m.values[i++] = freeCaptures[p];
			m.values[i++] = p.defeated ? fellTurn[p] : gameData.turn;
			m.values[i++] = gameData.turn;
			m.values[i++] = tideCities[p];
			m.values[i++] = drowned[p];
			m.values[i++] = d.deathTurn;
			m.values[i++] = HallOfFame.ScoreOf(gameData, p);
			m.values[i++] = gameData.winner == p ? 1 : 0;
			m.values[i++] = p.defeated ? 0 : gameData.GetTradeNetwork().ConnectedCityStates(p).Count;
			m.values[i++] = d.midCityStates;
			m.values[i++] = d.cityStateGold;
			m.values[i++] = workersLost[p];
			m.values[i++] = workersLostOutside[p];
			m.values[i++] = d.workerTurnsOut;
			m.values[i++] = d.workerTurnsOutAlone;
			m.values[i++] = cityStateAttacks[p];
			m.values[i++] = kingAttacks[p];
			List<City> cityStates = gameData.cities.Where(c => CityStateTrade.IsCityState(c)).ToList();
			m.values[i++] = cityStates.Count == 0 ? 0 : cityStates.Average(c => c.residents.Count);
			m.values[i++] = cityStates.Count == 0 ? 0 : cityStates.Average(CityStates.Specialists);
			HashSet<City> partners = p.defeated ? [] : gameData.GetTradeNetwork().ConnectedCityStates(p);
			m.values[i++] = partners.Count == 0 ? 0 : partners.Average(c => c.residents.Count);
			m.values[i++] = cityStateCaptures[p].Count > 0 ? cityStateCaptures[p][0] : turns;
			m.values[i++] = cityStateCaptures[p].Count > 1 ? cityStateCaptures[p][1] : turns;
			m.values[i++] = civCaptures[p];
			m.values[i++] = retaken[p];
			m.values[i++] = warRecords.Count(w => w.aggressor == p && w.attacks > 0);
			m.values[i++] = warRecords.Count(w => w.aggressor == p && w.captures > 0);
			m.values[i++] = p.cities.Count(c => c.constructed_buildings.Any(b => b.building.providesVeteranGroundUnits));
			List<MapUnit> landFighters = p.units.Where(u => u.IsLandUnit() && u.unitType.attack + u.unitType.defense > 0 && !u.unitType.isKing && !u.unitType.isWorker).ToList();
			m.values[i++] = landFighters.Count == 0 ? 0 : 100.0 * landFighters.Count(u => u.experienceLevel.baseHitPoints > gameData.defaultExperienceLevel.baseHitPoints) / landFighters.Count;
			m.values[i++] = p.units.Count(u => u.IsLandUnit() && u.unitType.bombard > 0 && u.unitType.attack == 0);
			m.values[i++] = riverAttacks[p];
			List<int> contactTurns = civs.Where(o => o != p).Select(o => p.playerRelationships.TryGetValue(o.id, out PlayerRelationship r)
				&& r.firstContactTurn >= 0 ? r.firstContactTurn : turns).ToList();
			m.values[i++] = contactTurns.Count(t => t < turns);
			m.values[i++] = contactTurns.Count == 0 ? 0 : contactTurns.Average();
			m.values[i++] = barbKills[p];
			return m;
		}

		List<Measures> trial = civs.Where(trialCivs.Contains).Select(Measure).ToList();
		List<Measures> others = trialHalf < 0 ? trial : civs.Where(p => !trialCivs.Contains(p)).Select(Measure).ToList();
		return (trial, others, civs.Average(p => p.knownTechs.Count), wonders, trialCivs);
	}
}

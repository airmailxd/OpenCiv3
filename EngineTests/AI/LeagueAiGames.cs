using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7Engine.AI.UnitAI;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;

namespace EngineTests.AI;

// Headless AI games under the League's mods (MP Tournament, QC, Modern,
// Future, MDJ) and stock rules, every civ played by the AI, with what the
// League cares about counted: wars declared (and by which engine class),
// cities captured, cities per civ at turns 30 and 60, the governments
// chosen, airdrops, and gold left unspent. LeagueAiTest asserts on fixed
// seeds; LeagueAiTest.Measure prints a table across seeds.
public static class LeagueAiGames {
	public sealed class War {
		public int turn;
		public string aggressor, defender, via;
		public override string ToString() => $"t{turn} {aggressor}->{defender} ({via})";
	}

	public sealed class Capture {
		public int turn;
		public string taker, from, city;
		public override string ToString() => $"t{turn} {taker} took {city} from {from}";
	}

	public sealed class Result {
		public GameScenario scenario;
		public int seed, turnsPlayed, civs;
		public AiType aiType;
		public readonly List<War> wars = new();
		public readonly List<Capture> captures = new();
		public int citiesRazed;
		// Cities per civ at turns 30 and 60 (by civ name), and at the end.
		public readonly Dictionary<string, int> citiesAt30 = new(), citiesAt60 = new(), citiesAtEnd = new();
		// The government each civ was in at the end, and the first real one
		// it adopted.
		public readonly Dictionary<string, string> government = new(), firstGovernment = new();
		public int airdrops;
		public readonly Dictionary<string, int> maxGold = new(), goldAtEnd = new();
		public readonly List<string> eliminated = new();
		public string winner;
		public double seconds;

		public double AvgCities(Dictionary<string, int> d) => d.Count == 0 ? 0 : d.Values.Average();

		public string Summary() {
			string govs = string.Join(",", government.Values.GroupBy(g => g).OrderBy(g => g.Key).Select(g => $"{g.Key}x{g.Count()}"));
			return $"{scenario,-12} {aiType,-8} seed={seed,-3} turns={turnsPlayed,-3} wars={wars.Count} captures={captures.Count} razed={citiesRazed} "
				+ $"cities@30={AvgCities(citiesAt30):0.0} [{string.Join(" ", citiesAt30.Values)}] cities@60={AvgCities(citiesAt60):0.0} [{string.Join(" ", citiesAt60.Values)}] "
				+ $"end=[{string.Join(" ", citiesAtEnd.Values)}] govs={govs} airdrops={airdrops} maxGold={(maxGold.Count == 0 ? 0 : maxGold.Values.Max())} "
				+ $"goldEnd={(goldAtEnd.Count == 0 ? 0 : goldAtEnd.Values.Average()):0} out={eliminated.Count} {seconds:0}s";
		}
	}

	private static GameMode.Config Mode => new("civ3", ["standalone"]);

	// The game mode (behaviours and ruleset folder) a scenario plays in.
	private static GameMode.Config ModeFor(GameScenario scenario) {
		string dir = GameSetup.RulesetDirFor(scenario);
		return dir == null ? Mode : new GameMode.Config(Mode.baseModeDir, [.. Mode.addonPaths], dir);
	}

	public static int DefaultTurns(GameScenario scenario) => scenario switch {
		GameScenario.Qc or GameScenario.Modern => 64,
		_ => 90,
	};

	// A new game of the scenario for this many civs: the League's ladder
	// world (or a pangaea for stock rules and MDJ), at a world size that
	// fits the civs, so the games are quick.
	public static SaveGame NewSave(GameScenario scenario, int seed, int civs, Difficulty difficultyOverride = null) {
		SaveGame save = scenario == GameScenario.None
			? GameMode.Load(PathUtils.GameModesDir, Mode).GetSave()
			: GameSetup.LoadRulesFor(scenario, PathUtils.GameModesDir, Mode);
		GameSetup.BundledRules rules = GameSetup.BundledRulesFor(scenario);
		WorldSize size = scenario switch {
			GameScenario.Mdj => save.WorldSizes.First(w => w.numberOfCivs >= civs),
			GameScenario.Qc => save.WorldSizes.First(w => w.numberOfCivs >= civs),
			GameScenario.Modern => save.WorldSizes.First(w => w.numberOfCivs >= Math.Max(civs, 5)),
			GameScenario.Future => save.WorldSizes[0],
			_ => save.WorldSizes[0],
		};
		WorldCharacteristics wc = new(save) {
			landform = rules?.Ladder.Landform ?? WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = rules?.Ladder.Ocean ?? WorldCharacteristics.OceanCoverage.Percent_60,
			age = rules?.Ladder.Age ?? WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize {
				name = size.name, width = size.width, height = size.height, numberOfCivs = civs,
				distanceBetweenCivs = size.distanceBetweenCivs, techRate = size.techRate, optimalNumberOfCities = size.optimalNumberOfCities,
			},
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.IsIndependent && !c.isBarbarian),
			difficulty = save.Difficulties[Math.Min(3, save.Difficulties.Count - 1)],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, civs - 1).ToList(),
			victoryConditions = save.VictoryConditions,
			scenario = scenario,
		}.Populate(save);
		return save;
	}

	private static string DeclaredBy() {
		foreach (StackFrame frame in new StackTrace().GetFrames()) {
			Type type = frame.GetMethod()?.DeclaringType;
			while (type?.DeclaringType != null) {
				type = type.DeclaringType;
			}
			if (type == null || type == typeof(Player) || type == typeof(LeagueAiGames) || type.Namespace?.StartsWith("C7") != true) {
				continue;
			}
			return type.Name;
		}
		return "?";
	}

	private static string Name(Player p) => p?.civilization?.name ?? "?";

	public static Player[] Civs(C7GameData.GameData gameData) => gameData.players.Where(p => !p.IsIndependent && !p.isBarbarians).ToArray();

	// Plays a game to its turn limit (or `turns`), every civ the AI of the
	// given type. onTurn, if given, runs after each turn.
	public static async Task<Result> Play(GameScenario scenario, int seed, int civs = 4, int? turns = null,
			AiType aiType = AiType.Standard, Action<C7GameData.GameData> onTurn = null) {
		Stopwatch clock = Stopwatch.StartNew();
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		BehaviorEngine behaviors = GameMode.Load(PathUtils.GameModesDir, ModeFor(scenario)).behaviors;
		await CreateGame.createGame(NewSave(scenario, seed, civs), _ => behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;
		int limit = turns ?? Math.Min(DefaultTurns(scenario), gameData.timeOptions.turnLimit > 0 ? gameData.timeOptions.turnLimit : int.MaxValue);
		gameData.observerMode = true;
		foreach (Player player in gameData.players) {
			player.isHuman = false;
			if (!player.IsIndependent) {
				player.aiType = aiType;
			}
		}
		Result result = new() { scenario = scenario, seed = seed, aiType = aiType, civs = civs };
		Player[] all = Civs(gameData);
		Dictionary<City, Player> owners = gameData.cities.ToDictionary(c => c, c => c.owner);
		Action<Player, Player> previousWar = AiExperiments.onWarDeclared;
		Action<MapUnit, Tile> previousDrop = AirdropAI.onAirdrop;
		AiExperiments.onWarDeclared = (a, d) => {
			if (!a.IsIndependent && !d.IsIndependent) {
				result.wars.Add(new War { turn = gameData.turn, aggressor = Name(a), defender = Name(d), via = DeclaredBy() });
			}
		};
		AirdropAI.onAirdrop = (_, _) => ++result.airdrops;
		int start = gameData.turn;
		try {
			TurnHandling.OnBeginTurn();
			TurnHandling.InitTurnData();
			Task firstTurn = TurnHandling.AdvanceTurn();
			EngineWaits.WaitForStartTurnMessage(_ => { });
			await firstTurn;
			while (!gameData.gameOver && gameData.turn - start < limit) {
				new MsgEndTurn { turn = gameData.turn }.send();
				EngineWaits.WaitForStartTurnMessage(_ => { });
				foreach (City c in gameData.cities) {
					if (owners.TryGetValue(c, out Player was) && was != c.owner && !was.IsIndependent) {
						result.captures.Add(new Capture { turn = gameData.turn, taker = Name(c.owner), from = Name(was), city = c.name });
					}
					owners[c] = c.owner;
				}
				foreach (City gone in owners.Keys.Where(c => !gameData.cities.Contains(c)).ToList()) {
					++result.citiesRazed;
					owners.Remove(gone);
				}
				int played = gameData.turn - start;
				foreach (Player p in all) {
					string n = Name(p);
					result.maxGold[n] = Math.Max(result.maxGold.GetValueOrDefault(n), p.gold);
					string g = p.government?.name;
					if (g != null && !result.firstGovernment.ContainsKey(n) && g != "Anarchy" && g != "Despotism" && !g.StartsWith("!")) {
						result.firstGovernment[n] = g;
					}
					if (played == 30) {
						result.citiesAt30[n] = p.cities.Count;
					}
					if (played == 60) {
						result.citiesAt60[n] = p.cities.Count;
					}
				}
				onTurn?.Invoke(gameData);
			}
		} finally {
			gameData.observerMode = false;
			AiExperiments.onWarDeclared = previousWar;
			AirdropAI.onAirdrop = previousDrop;
		}
		result.turnsPlayed = gameData.turn - start;
		foreach (Player p in all) {
			string n = Name(p);
			result.citiesAtEnd[n] = p.cities.Count;
			result.government[n] = p.government?.name;
			result.goldAtEnd[n] = p.gold;
			if (p.defeated) {
				result.eliminated.Add(n);
			}
		}
		result.winner = Name(gameData.winner);
		result.seconds = clock.Elapsed.TotalSeconds;
		return result;
	}
}

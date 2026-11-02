using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7Engine.PlayLog;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.GameData;

// The play log (C7Engine.PlayLog.PlayRecorder) must not change the game. A
// scripted "human" plays a generated game against the AI, twice: without the
// log and with it. Every turn's whole game state (the save's hash, which
// holds the random number generator's state) must be the same both times,
// and the log must hold the human's turns, actions and the Advanced AI's
// shadow choices.
public class PlayRecorderTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;

	public PlayRecorderTest(SaveGameFixture fixture, ITestOutputHelper output) {
		this.fixture = fixture;
		this.output = output;
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	private const int Turns = 80;

	private static SaveGame NewSave(int seed) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Continents,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() {
				width = 40,
				height = 40,
				numberOfCivs = 4,
				distanceBetweenCivs = 6,
				techRate = 240,
				optimalNumberOfCities = 20,
			},
			mapSeed = seed,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian && !c.IsIndependent),
			difficulty = save.Difficulties[3],
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 3).ToList(),
			victoryConditions = new VictoryConditions(),
		}.Populate(save);
		return save;
	}

	private static string Hash(C7GameData.GameData gameData) {
		return Convert.ToHexString(SHA256.HashData(SaveGame.FromGameData(gameData).ToCompactJSON()))[..16];
	}

	[Fact]
	public async Task RecordingLeavesTheGameUnchanged() {
		using TempDirectory dir = new("play-log");
		try {
			PlayRecorder.enabled = false;
			List<string> without = await PlayScriptedGame();
			Assert.Empty(Directory.GetFiles(dir.Path));

			PlayRecorder.enabled = true;
			PlayRecorder.directory = dir.Path;
			List<string> with = await PlayScriptedGame();
			string path = PlayRecorder.CurrentPath;
			PlayRecorder.Close();

			Assert.Equal(without.Count, with.Count);
			for (int i = 0; i < without.Count; ++i) {
				Assert.True(without[i] == with[i], $"The game differs with the play log on at turn {i}: {without[i]} vs {with[i]}");
			}

			Assert.NotNull(path);
			List<JsonElement> rows = File.ReadAllLines(path).Select(l => JsonDocument.Parse(l).RootElement).ToList();
			string Type(JsonElement r) => r.TryGetProperty("type", out JsonElement t) ? t.GetString() : "state";
			output.WriteLine($"{rows.Count} rows: " + string.Join(", ", rows.GroupBy(Type).Select(g => $"{g.Key} {g.Count()}")));
			Assert.Equal("header", Type(rows[0]));

			// A state row, as the AI trials write them, for every turn.
			List<JsonElement> states = rows.Where(r => Type(r) == "state").ToList();
			Assert.True(states.Count >= Turns, $"{states.Count} state rows");
			Assert.All(states, s => Assert.Equal("Human", s.GetProperty("ai").GetString()));
			foreach (string key in new[] { "game", "seed", "run", "turn", "civ", "cities", "citizens", "units", "military", "workers",
					"settlers", "ships", "techs", "government", "gold", "score", "culture", "atWarWith", "knownLand", "openSites" }) {
				Assert.True(states[0].TryGetProperty(key, out _), key);
			}

			List<JsonElement> actions = rows.Where(r => Type(r) == "action").ToList();
			bool HasShadow(string msg) => actions.Any(a => a.GetProperty("msg").GetString() == msg && a.TryGetProperty("shadow", out _));
			Assert.True(HasShadow(nameof(MsgChooseProduction)), "production shadow");
			Assert.True(HasShadow(nameof(MsgChooseResearch)), "research shadow");
			Assert.True(HasShadow(nameof(MsgChangeSliders)), "tax shadow");
			Assert.True(HasShadow(nameof(MsgBuildCity)), "city site shadow");
			Assert.True(HasShadow(nameof(StartGovernmentTransitionMsg)), "revolution shadow");
			Assert.Contains(actions, a => a.GetProperty("msg").GetString() == nameof(MsgMoveUnit));
			Assert.Contains(actions, a => a.GetProperty("msg").GetString() == nameof(MsgEndTurn));
			foreach (JsonElement a in actions.Where(a => a.TryGetProperty("shadow", out _)).Take(6)) {
				output.WriteLine(a.ToString());
			}
		} finally {
			PlayRecorder.enabled = false;
			PlayRecorder.directory = null;
			PlayRecorder.Close();
		}
	}

	// Plays the game, the first civ by a script that depends only on the game
	// and its own random numbers, and returns the game's hash after each turn.
	private async Task<List<string>> PlayScriptedGame() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await CreateGame.createGame(NewSave(4321), (_) => fixture.behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;
		Player human = gameData.players.First(p => p.isHuman);
		Random script = new(77);
		int cityNames = 0;
		List<string> hashes = new();

		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		Task firstTurn = TurnHandling.AdvanceTurn();
		WaitForTurn(human);
		await firstTurn;

		for (int turn = 0; turn < Turns && !gameData.gameOver && !human.defeated; ++turn) {
			// Research.
			List<Tech> available = ((IEnumerable<Tech>)human.GetAvailableTechsToResearch(gameData.techs)).ToList();
			Tech monarchy = gameData.techs.FirstOrDefault(t => t.Name == "Monarchy");
			if (turn == 0 && monarchy != null) {
				// A far tech: the engine queues the way there.
				Send(new MsgChooseResearch(monarchy, MsgChooseResearch.AdvisorState.DontShow));
			} else if (available.Count > 0 && human.currentlyResearchedTech == null) {
				Send(new MsgChooseResearch(available[script.Next(available.Count)], MsgChooseResearch.AdvisorState.DontShow));
			}
			// Production.
			foreach (City city in human.cities.ToList()) {
				if ((turn + city.residents.Count) % 3 == 0) {
					List<IProducible> options = city.ListProductionOptions(gameData).ToList();
					IProducible settlers = options.FirstOrDefault(o => o is UnitPrototype { isSettler: true });
					IProducible pick = settlers != null && human.cities.Count < 6 && script.Next(2) == 0 ? settlers
						: options[script.Next(options.Count)];
					Send(new MsgChooseProduction(city.id, pick.name));
				}
			}
			// Taxes.
			if (turn % 4 == 1) {
				Send(new MsgChangeSliders(turn % 8 == 1 ? MsgChangeSliders.DomesticPolicyChoice.MoreScience
					: MsgChangeSliders.DomesticPolicyChoice.LessScience, script.Next(3, 8)));
			}
			// Government.
			// (A revolution with nothing to change to is turned down, but
			// still asks the AI whether it would revolt.)
			if (!human.government.transitionType && human.government.hasTilePenalty
					&& (human.CanStartRevolution(gameData) || turn == Turns - 10)) {
				Send(new StartGovernmentTransitionMsg());
			} else if (human.government.transitionType && gameData.turn >= human.inAnarchyUntilTurn) {
				Send(new SelectGovernmentMsg(human.GetAvailableGovernments(gameData).First(g => !g.transitionType)));
			}
			// War on the nearest civ met, to see fights.
			Player enemy = gameData.players.Where(p => p != human && !p.IsIndependent && !p.defeated && p.cities.Count > 0
					&& human.playerRelationships.ContainsKey(p.id))
				.OrderBy(p => p.cities.Min(c => human.cities.Count == 0 ? 0 : c.location.DistanceTo(human.cities[0].location))).FirstOrDefault();
			if (turn == 30 && enemy != null && !PlayerRelationship.AtWar(human, enemy)) {
				Send(new MsgDeclareWar(enemy));
			}
			// Units: settlers found cities where they may, soldiers march on
			// an enemy at war with them, the rest wander.
			foreach (MapUnit unit in human.units.ToList()) {
				if (gameData.GetUnit(unit.id) == null || !unit.movementPoints.canMove) {
					continue;
				}
				if (unit.unitType.isSettler && SettlerLocationAI.IsStillValidSite(unit.location, human)
						&& (human.cities.Count == 0 || script.Next(3) == 0)) {
					Send(new MsgBuildCity(unit, $"Scripted {++cityNames}"));
					continue;
				}
				if (unit.unitType.isWorker && !unit.unitType.isSettler) {
					continue;
				}
				City target = enemy != null && PlayerRelationship.AtWar(human, enemy) && unit.unitType.attack > 0 && !unit.unitType.isSettler
					? enemy.cities.OrderBy(c => c.location.DistanceTo(unit.location)).FirstOrDefault() : null;
				if (target != null && target.location != unit.location) {
					Send(new MsgMoveUnit(unit.id, unit.location.DirectionTo(target.location)));
				} else if (script.Next(2) == 0) {
					TileDirection dir = (TileDirection)script.Next(8);
					Send(new MsgMoveUnit(unit.id, dir));
				}
			}

			new MsgEndTurn { turn = gameData.turn }.send();
			WaitForTurn(human);
			hashes.Add($"turn {gameData.turn} {Hash(gameData)}");
		}
		return hashes;
	}

	// Handles the message and whatever follows from it.
	private static void Send(MessageToEngine msg) {
		msg.send();
		for (int i = 0; i < 10000 && EngineStorage.HasPendingMessagesToEngine(); ++i) {
			EngineStorage.ProcessNextMessageToEngine();
			while (EngineStorage.TryDequeueNextMessageToUI(out _)) {
			}
			if (EngineStorage.HasPendingMessagesToEngine()) {
				Thread.Sleep(1);
			}
		}
		Assert.False(EngineStorage.HasPendingMessagesToEngine(), $"{msg.GetType().Name} wasn't handled");
	}

	// Waits for the human's turn, closing any talks an AI opens with them
	// at once (as a human turning everything down would).
	private static void WaitForTurn(Player human) {
		EngineWaits.WaitForStartTurnMessage(_ => {
			if (EngineStorage.diplomacyPlayerID == human.id) {
				new MsgDiplomacyCompleted { playerID = human.id }.send();
			}
		});
	}
}

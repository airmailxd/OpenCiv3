using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7Engine.Lua;
using C7Engine.Pathing;
using C7GameData.AIData;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace EngineTests.AI.UnitAI;

// On an archipelago, the AIs ferry settlers to other islands by Galley, and
// their ships never end a turn in water they could sink in.
public class SettlerSailsOverseasTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;
	private readonly ITestOutputHelper output;

	public SettlerSailsOverseasTest(SaveGameFixture fixture, ITestOutputHelper output) {
		this.fixture = fixture;
		this.output = output;
		// Engine message queues are static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.messagesToUI.Clear();
		EngineStorage.pendingMessages.Clear();
	}

	private static SaveGame NewSave(WorldCharacteristics.Landform landform = WorldCharacteristics.Landform.Archipelago, int size = 70, int civs = 4) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = landform,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_70,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.None,
			worldSize = new WorldSize() {
				width = size,
				height = size,
				numberOfCivs = civs,
				distanceBetweenCivs = 10,
				techRate = 240,
				optimalNumberOfCities = 20,
			},
			mapSeed = 31,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.First(c => !c.isBarbarian),
			// Monarch, where the AI's cities grow and build at the human's
			// pace. On Chieftain their food boxes and shield costs are
			// doubled (its AI cost factor is 20 to the human's 10), and on
			// this map they don't fill their own islands in time to need
			// to sail.
			difficulty = save.Difficulties.First(d => d.Name == "Monarch"),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, civs - 1).ToList(),
			victoryConditions = new VictoryConditions(),
		}.Populate(save);
		return save;
	}

	// Every civ can build Galleys from the start, and the AI plays them all.
	private static void GiveEveryoneGalleys(C7GameData.GameData gameData) {
		List<Tech> galleyTechs = gameData.techs.Where(t => t.Name == "Alphabet" || t.Name == "Map Making").ToList();
		Assert.Equal(2, galleyTechs.Count);
		gameData.observerMode = true;
		foreach (Player player in gameData.players) {
			player.isHuman = false;
			foreach (Tech tech in galleyTechs) {
				player.knownTechs.Add(tech.id);
			}
		}
	}

	[Fact]
	public async Task AIsSettleOtherIslandsByShip() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await CreateGame.createGame(NewSave(), (_) => fixture.behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;

		GiveEveryoneGalleys(gameData);

		// Where each civ started.
		Dictionary<Player, int> homeIsland = gameData.players.Where(p => p.cities.Count > 0 || p.units.Count > 0)
			.ToDictionary(p => p, p => (p.cities.FirstOrDefault()?.location ?? p.units[0].location).continent);

		bool sawSettlerAboard = false;
		try {
			Task firstTurn = TurnHandling.AdvanceTurn();
			EngineWaits.WaitForStartTurnMessage(_ => { });
			await firstTurn;
			// When the first civ sails depends on how fast the islands
			// fill, which small changes to where cities go move by tens of
			// turns, so the game runs to 200 turns.
			for (int turn = 0; turn < 200; ++turn) {
				new MsgEndTurn { turn = gameData.turn }.send();
				EngineWaits.WaitForStartTurnMessage(_ => { });

				sawSettlerAboard |= gameData.mapUnits.Any(u => u.unitType.name == "Settler" && u.IsLoaded());
				foreach (Player player in gameData.players.Where(p => !p.isBarbarians)) {
					foreach (MapUnit ship in player.units.Where(u => u.IsWaterUnit())) {
						Assert.False(ship.IsInUnsafeWater(), $"turn {gameData.turn}: {ship} ended its turn in water it could sink in");
					}
				}

				List<City> overseas = gameData.cities.Where(c => homeIsland.TryGetValue(c.owner, out int home) && c.location.continent != home).ToList();
				if (overseas.Count > 0) {
					Assert.True(sawSettlerAboard, "a settler should have sailed there");
					output.WriteLine($"turn {gameData.turn}: {string.Join(", ", overseas.Select(c => $"{c.owner.civilization.name} founded {c.name}"))}");
					return;
				}
			}
			Assert.Fail("No civ settled another island");
		} finally {
			gameData.observerMode = false;
		}
	}

	// A settler sails along the coast when that gets it to a site on its
	// own landmass sooner than walking around a bay or a mountain range.
	[Fact]
	public async Task SettlerSailsWhenThatIsQuickerThanWalking() {
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		await CreateGame.createGame(NewSave(WorldCharacteristics.Landform.Continents, 90, 8), (_) => fixture.behaviors);
		C7GameData.GameData gameData = EngineStorage.gameData;
		GiveEveryoneGalleys(gameData);

		try {
			Task firstTurn = TurnHandling.AdvanceTurn();
			EngineWaits.WaitForStartTurnMessage(_ => { });
			await firstTurn;
			for (int turn = 0; turn < 40; ++turn) {
				new MsgEndTurn { turn = gameData.turn }.send();
				EngineWaits.WaitForStartTurnMessage(_ => { });
			}
		} finally {
			gameData.observerMode = false;
		}

		// With a Galley at hand, rather than one to build first.
		UnitPrototype settlerType = gameData.unitPrototypes.First(p => p.name == "Settler");
		UnitPrototype galley = gameData.unitPrototypes.First(p => p.name == "Galley");
		int checkedSites = 0;
		foreach (City city in gameData.cities.Where(c => c.location.NeighborsWater() && !c.owner.isBarbarians)) {
			Player player = city.owner;
			MapUnit ship = gameData.SpawnUnit(player, galley, city.location);
			MapUnit settler = settlerType.GetInstance(ID.None("settler"), settlerType, player, location: city.location);
			foreach (Tile site in city.location.GetTilesWithinRankDistance(12)) {
				if (!site.IsLand() || site.continent != city.location.continent || site.HasCity() || !site.NeighborsWater()) {
					continue;
				}
				TilePath walk = PathingAlgorithmChooser.GetAlgorithm(settler).PathFrom(settler.location, site, settler);
				if (walk == null || walk.PathLength() <= 0) {
					continue;
				}
				int walkTurns = walk.PathCost(player, settler.location, settler.MaxMovementPoints(), settler.movementPoints.remaining);
				SettlerAIData data = new() { destination = site };
				SettlerAI.ChooseRoute(settler, player, data);
				++checkedSites;
				if (data.port != null) {
					SeaRoutes.Voyage voyage = SeaRoutes.PlanVoyage(settler, site, int.MaxValue);
					Assert.True(voyage.turns + SeaRoutes.TurnsSavedToSail <= walkTurns,
						$"sailing to {site} takes {voyage.turns} turns, walking {walkTurns}");
					Assert.Equal(site.continent, data.landing.continent);
					output.WriteLine($"{player.civilization.name}: from {city.name} to {site} by sea in {voyage.turns} turns rather than {walkTurns} on foot");
					return;
				}
			}
			gameData.RemoveUnit(ship);
		}
		Assert.Fail($"No site on the same landmass was quicker to reach by sea, of {checkedSites}");
	}
}

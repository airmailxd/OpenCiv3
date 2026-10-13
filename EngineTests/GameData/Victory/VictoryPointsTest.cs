using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData.Victory;

// Victory points, Victory Point Scoring and Wonder Victory (see
// VictoryPoints), per the Mesopotamia conquest's victory point schedule and
// the Conquests Civilopedia.
public class VictoryPointsTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player attacker;
	private readonly Player defender;

	public VictoryPointsTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.victoryConditions = new VictoryConditions();

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		attacker = civs[0];
		defender = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City FoundCity(Player player, int size) {
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
			});
		}
		return city;
	}

	// "Wonder Cost (2.5 VP * build cost)", from a multiple of 25.
	[Fact]
	public void AWonderScoresItsMultipleInTenthsOfItsCost() {
		gameData.victoryConditions.WonderVictoryPoints = 25;
		Building pyramids = gameData.Buildings.First(b => b.IsGreatWonder());

		VictoryPoints.OnWonderBuilt(attacker, pyramids, gameData.victoryConditions);

		Assert.Equal(pyramids.shieldCost * 25 / 10, attacker.victoryPoints);
	}

	// "Defeating an enemy unit (5 VP * unit cost)".
	[Fact]
	public void DefeatingAUnitScoresItsCost() {
		MapUnit warrior = gameData.SpawnUnit(defender, gameData.unitPrototypes.First(p => p.name == "Warrior"), defender.units[0].location);

		VictoryPoints.OnUnitDefeated(attacker, warrior, gameData.victoryConditions);

		Assert.Equal(5 * warrior.unitType.shieldCost, attacker.victoryPoints);
	}

	// "Advancement Cost (5 VP * tech cost)".
	[Fact]
	public void AnAdvanceScoresItsCost() {
		Tech tech = gameData.techs.First(t => !attacker.knownTechs.Contains(t.id) && t.Cost > 0);

		attacker.AcquireTech(gameData, tech);

		Assert.Equal(5 * tech.Cost, attacker.victoryPoints);
	}

	// "City Conquest (100 VP * city population)", as the city was taken.
	[Fact]
	public void ConqueringACityScoresItsPopulation() {
		FoundCity(attacker, 1);
		City city = FoundCity(defender, 4);

		CityInteractions.CaptureCity(city, attacker);

		Assert.Equal(400, attacker.victoryPoints);
		Assert.Equal(0, defender.victoryPoints);
	}

	[Fact]
	public void BarbariansScoreNothing() {
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		MapUnit warrior = gameData.SpawnUnit(defender, gameData.unitPrototypes.First(p => p.name == "Warrior"), defender.units[0].location);

		VictoryPoints.OnUnitDefeated(barbarians, warrior, gameData.victoryConditions);

		Assert.Equal(0, barbarians.victoryPoints);
	}

	[Fact]
	public void TheFirstToTheLimitWins() {
		VictoryPointVictory victory = new(limit: 5500);
		attacker.victoryPoints = 5499;
		Assert.False(victory.HasVictory(victory.Evaluate(attacker, gameData)));
		attacker.victoryPoints = 5500;
		Assert.True(victory.HasVictory(victory.Evaluate(attacker, gameData)));
	}

	[Fact]
	public void VictoryPointScoringAndWonderVictoryAreOnlyCheckedWhenOn() {
		SaveGame save = fixture.saveGame;
		save.VictoryConditions = new VictoryConditions { VictoryLocations = true, AllowWonderVictory = true, VictoryPointLimit = 30000 };
		C7GameData.GameData withThem = save.ToGameData(fixture.behaviors);
		Assert.Single(withThem.victories.OfType<VictoryPointVictory>());
		Assert.Single(withThem.victories.OfType<WonderVictory>());

		save = fixture.saveGame;
		save.VictoryConditions = new VictoryConditions();
		C7GameData.GameData without = save.ToGameData(fixture.behaviors);
		Assert.Empty(without.victories.OfType<VictoryPointVictory>());
		Assert.Empty(without.victories.OfType<WonderVictory>());
	}

	// "the game ends as soon as [all] Great Wonders have been built. The
	// winner is then determined based on victory points".
	[Fact]
	public void OnceEveryWonderIsBuiltTheMostVictoryPointsWins() {
		WonderVictory victory = new();
		attacker.victoryPoints = 100;
		defender.victoryPoints = 200;
		List<Building> wonders = gameData.Buildings.Where(b => b.IsGreatWonder()).ToList();
		foreach (Building w in wonders.Skip(1)) {
			gameData.GreatWondersBuilt.Add(w.name);
		}
		Assert.False(victory.HasVictory(victory.Evaluate(defender, gameData)));

		gameData.GreatWondersBuilt.Add(wonders[0].name);
		Assert.True(victory.HasVictory(victory.Evaluate(defender, gameData)));
		Assert.False(victory.HasVictory(victory.Evaluate(attacker, gameData)));
		VictoryStatus status = victory.Evaluate(attacker, gameData);
		Assert.Equal(wonders.Count, status.WondersToBuild);
		Assert.Equal(["Total to build:", $"{wonders.Count}", "Wonders built:", "0", "Total built by rivals:", $"{wonders.Count}"],
			victory.GenerateStatusRows(status, []).Single());
	}

	// "If the game ends and no one has reached 30,000 victory points, then
	// the winner is given to the civilization that has amassed the most
	// victory points."
	[Fact]
	public void AtTheTurnLimitVictoryPointsDecide() {
		gameData.victoryConditions.VictoryLocations = true;
		gameData.victories.Clear();
		gameData.victories.Add(new TimeLimitVictory(1));
		gameData.turn = 1;
		gameData.gameOver = false;
		foreach (Player p in gameData.players.Where(p => !p.isBarbarians)) {
			gameData.history[p.id.ToString()] = [new HistTurnRecord { Score = p == attacker ? 1000 : 0 }];
		}
		defender.victoryPoints = 10;

		TurnHandling.CheckVictory(gameData);

		Assert.Equal(defender, gameData.winner);
	}

	// "To score these points a unit with a defensive military stat must be
	// in that tile at the end of all players' turns ... the controlling
	// player gets 25 victory points."
	[Fact]
	public void HoldingAVictoryLocationWithADefenderScores() {
		gameData.victoryConditions.VictoryLocations = true;
		Tile location = gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity());
		location.isVictoryLocation = true;
		MapUnit worker = gameData.SpawnUnit(attacker, gameData.unitPrototypes.First(p => p.isWorker && p.defense == 0), location);

		VictoryPoints.ScoreVictoryLocations(gameData);
		Assert.Equal(0, attacker.victoryPoints);

		gameData.SpawnUnit(attacker, gameData.unitPrototypes.First(p => p.name == "Warrior"), location);
		VictoryPoints.ScoreVictoryLocations(gameData);
		Assert.Equal(25, attacker.victoryPoints);

		// Without Victory Point Scoring, nothing.
		gameData.victoryConditions.VictoryLocations = false;
		VictoryPoints.ScoreVictoryLocations(gameData);
		Assert.Equal(25, attacker.victoryPoints);
		Assert.NotNull(worker);
	}

	// "In single player mode, the starting location of each player is
	// designated a Victory Location."
	[Fact]
	public void ANewGameWithVictoryPointScoringMakesEachStartAVictoryLocation() {
		SaveGame save = SaveGameFixture.LoadGameMode(new C7Engine.Lua.GameMode.Config("civ3")).GetSave();
		WorldCharacteristics world = new(save) {
			landform = WorldCharacteristics.Landform.Pangaea,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.None,
			worldSize = new WorldSize() {
				width = 40,
				height = 40,
				numberOfCivs = 2,
				distanceBetweenCivs = 6,
				techRate = 240,
				optimalNumberOfCities = 10,
			},
			mapSeed = 78,
		};
		new GameSetup {
			playerCivilization = save.Civilizations.Single(c => c.name == "America"),
			difficulty = save.Difficulties.First(),
			worldCharacteristics = world,
			opponents = [new SelectedOpponent { Name = "Rome" }],
			victoryConditions = new VictoryConditions { VictoryLocations = true },
		}.Populate(save);

		List<SaveTile> locations = save.Map.tiles.Where(t => t.features.Contains(SaveTile.VictoryLocationFeature)).ToList();
		Assert.Equal(2, locations.Count);
		foreach (SavePlayer player in save.Players.Where(p => p.civilization is "America" or "Rome")) {
			TileLocation start = save.Units.First(u => u.owner == player.id).currentLocation;
			Assert.Contains(locations, t => t.X == start.X && t.Y == start.Y);
		}
	}

	[Fact]
	public void VictoryLocationsAreSaved() {
		Tile location = gameData.map.tiles.First(t => t.IsLand());
		location.isVictoryLocation = true;

		SaveGame loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		C7GameData.GameData reloaded = loaded.ToGameData(fixture.behaviors);

		Assert.True(reloaded.map.tileAt(location.XCoordinate, location.YCoordinate).isVictoryLocation);
		Assert.Single(reloaded.map.tiles, t => t.isVictoryLocation);
		Assert.True(new SaveTile(location).Describes(location));
	}

	[Fact]
	public void VictoryPointsAreSaved() {
		attacker.victoryPoints = 1234;
		gameData.victoryConditions = new VictoryConditions { VictoryLocations = true, VictoryPointLimit = 30000, WonderVictoryPoints = 25 };

		SaveGame loaded = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());

		Assert.Equal(1234, loaded.Players.Single(p => p.id == attacker.id).victoryPoints);
		Assert.Equal(30000, loaded.VictoryConditions.VictoryPointLimit);
		Assert.Equal(25, loaded.VictoryConditions.WonderVictoryPoints);
	}

	[Fact]
	public void TheSetupChoicesAreRemembered() {
		NewGameChoices choices = new();
		choices.SetVictoryConditions(new VictoryConditions { AllowWonderVictory = true, VictoryLocations = true });
		Dictionary<string, string> values = [];
		choices.Write((key, value) => values[key] = value);

		VictoryConditions chosen = NewGameChoices.Read(key => values.GetValueOrDefault(key))
			.ChosenVictoryConditions(new VictoryConditions { VictoryPointLimit = 30000 });

		Assert.True(chosen.AllowWonderVictory);
		Assert.True(chosen.VictoryLocations);
		Assert.Equal(30000, chosen.VictoryPointLimit);
	}
}

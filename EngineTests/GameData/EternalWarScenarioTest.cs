using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.Lua;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class EternalWarScenarioTest {
	private static (SaveGame, C7GameData.GameData) NewGame(int seed, int? citiesPerCiv = 4, List<string> civNames = null,
			WorldCharacteristics.OceanCoverage ocean = WorldCharacteristics.OceanCoverage.Percent_60) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Continents,
			oceanCoverage = ocean,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = 100, height = 100, numberOfCivs = 6, distanceBetweenCivs = 12, techRate = 240, optimalNumberOfCities = 20 },
			mapSeed = seed,
		};
		Civilization player = civNames == null
			? save.Civilizations.First(c => !c.isBarbarian && c.traits.Contains(Civilization.Trait.Expansionist))
			: save.Civilizations.First(c => c.name == civNames[0]);
		List<SelectedOpponent> opponents = civNames == null
			? Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 5).ToList()
			: civNames.Skip(1).Select(n => new SelectedOpponent { Name = n }).ToList();
		new GameSetup {
			playerCivilization = player,
			difficulty = save.Difficulties.First(d => d.Name == "Regent"),
			worldCharacteristics = wc,
			opponents = opponents,
			victoryConditions = new VictoryConditions(),
			scenario = GameScenario.EternalWar,
			eternalWarCitiesPerCiv = citiesPerCiv,
		}.Populate(save);
		return (save, SaveGameFixture.HydrateSaveGame(save));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public void SetsUpTheScenario(int seed) {
		(SaveGame save, C7GameData.GameData gameData) = NewGame(seed);

		// One landmass, whatever landform was picked, and no goody huts.
		gameData.map.recomputeContinents();
		Assert.Single(gameData.map.continents, c => c.First().IsLand());
		Assert.DoesNotContain(gameData.map.tiles, t => t.hasGoodyHut);

		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		Assert.Equal(6, civs.Count);
		List<ID> ancientTechs = gameData.techs.Where(t => t.EraCivilopediaName == EraUtils.ANCIENT_TIMES_CVLPD).Select(t => t.id).ToList();
		Assert.NotEmpty(ancientTechs);
		foreach (Player civ in civs) {
			Assert.Equal(EraUtils.MIDDLE_AGES_CVLPD, civ.eraCivilopediaName);
			Assert.Equal("Monarchy", civ.government.name);
			Assert.All(ancientTechs, id => Assert.Contains(id, civ.knownTechs));

			Assert.Equal(4, civ.cities.Count);
			Assert.Single(civ.cities, c => c.IsCapital());
			foreach (City city in civ.cities) {
				Assert.Equal(GameSetup.EternalWarCitySize, city.residents.Count);
				Assert.Contains(city.GetBuildings(), b => b.building.name == "Library");
				Assert.Contains(city.location.unitsOnTile, u => u.owner == civ && u.unitType.name == "Pikeman");
				Assert.Contains(city.location.unitsOnTile, u => u.owner == civ && u.unitType.name == "Worker");
				Assert.DoesNotContain(city.ListProductionOptions(gameData), p => p is UnitPrototype u && u.isSettler);
			}
			int scouts = civ.units.Count(u => u.unitType.name == "Scout");
			Assert.Equal(civ.civilization.traits.Contains(Civilization.Trait.Expansionist) ? 1 : 0, scouts);
			Assert.DoesNotContain(civ.units, u => u.unitType.isSettler);

			foreach (Player other in civs.Where(o => o != civ)) {
				Assert.True(PlayerRelationship.AtWar(civ, other));
				Assert.True(gameData.AreInLockedWar(civ, other));
				Assert.False(civ.WillAcceptCommunicationFrom(other, gameData.turn));
			}
		}

		// The cities are spread out, and on a 60% water map no two share a
		// tile of Civ3's city radius (2; citizens here work further, see
		// CitizensWorkAsFarAsTheSecondBorderExpansion).
		List<Tile> cityTiles = gameData.cities.Select(c => c.location).ToList();
		foreach (Tile t in cityTiles) {
			Assert.All(cityTiles.Where(o => o != t), o => Assert.True(t.DistanceTo(o) >= 4));
		}
		List<Tile> radii = cityTiles.SelectMany(t => t.GetTilesWithinRankDistance(2)).ToList();
		Assert.Equal(radii.Count, radii.Distinct().Count());

		// Only the first capital has a granary and an aqueduct.
		foreach (City city in gameData.cities) {
			bool has = city.GetBuildings().Any(b => b.building.name == "Granary") && city.GetBuildings().Any(b => b.building.name == "Aqueduct");
			Assert.Equal(city.IsCapital(), has);
		}

		// Workers defend but can't attack; libraries cost nothing to keep;
		// governments support three times the units.
		UnitPrototype worker = gameData.unitPrototypes.First(p => p.name == "Worker");
		Assert.Equal(0, worker.attack);
		Assert.Equal(2, worker.defense);
		Assert.Equal(0, gameData.Buildings.Find(b => b.name == "Library").maintenanceCost);
		SaveGame rules = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		foreach (Government government in gameData.governments) {
			Government original = rules.Governments.Find(g => g.name == government.name);
			Assert.Equal(3 * original.freeUnitsPerTown, government.freeUnitsPerTown);
			Assert.Equal(3 * original.freeUnitsPerCity, government.freeUnitsPerCity);
			Assert.Equal(3 * original.freeUnitsPerMetropolis, government.freeUnitsPerMetropolis);
		}

		Assert.Equal("Knight", gameData.barbarianInfo.basicBarbarian.name);
		Assert.Equal("Knight", gameData.barbarianInfo.advancedBarbarian.name);
		Assert.Null(gameData.barbarianInfo.barbarianSeaUnitProto);
		Assert.True(gameData.barbarianInfo.middleAgesUprisingHappened);
	}

	[Fact]
	public void CitiesHaveNoCorruption() {
		(_, C7GameData.GameData gameData) = NewGame(3);
		foreach (Player civ in gameData.players.Where(p => !p.isBarbarians)) {
			civ.DoCorruptionCalculations(gameData);
		}
		Assert.All(gameData.cities, city => Assert.Equal(0, city.corruption));
	}

	// A quarter of the citizens who'd be born unhappy are born content.
	[Fact]
	public void FewerCitizensAreBornUnhappy() {
		(_, C7GameData.GameData gameData) = NewGame(4);
		City city = gameData.cities.First(c => c.owner.isHuman);
		CitizenType citizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
		while (city.residents.Count < 12) {
			city.AddCitizen(new CityResident { city = city, nationality = city.owner.civilization, citizenType = citizen });
		}
		int bornContent = gameData.gameDifficulty.NumberOfCitizensBornContent;
		int expectedUnhappy = (12 - bornContent) * GameSetup.EternalWarBornUnhappyPercent / 100;

		gameData.rules.BornUnhappyPercent = 100;
		city.RecalculateCitizenMoods(gameData);
		int normalUnhappy = city.residents.Count(r => r.mood == CityResident.Mood.Unhappy);

		gameData.rules.BornUnhappyPercent = GameSetup.EternalWarBornUnhappyPercent;
		city.RecalculateCitizenMoods(gameData);
		int scenarioUnhappy = city.residents.Count(r => r.mood == CityResident.Mood.Unhappy);

		Assert.True(scenarioUnhappy < normalUnhappy, $"{scenarioUnhappy} unhappy under the scenario, {normalUnhappy} normally");
		Assert.True(scenarioUnhappy <= expectedUnhappy, $"{scenarioUnhappy} unhappy, at most {expectedUnhappy} expected");
	}

	// The cities each civ starts with grow with the world by default.
	[Fact]
	public void DefaultCitiesGrowWithTheWorld() {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		Dictionary<string, int> expected = new() { ["Tiny"] = 2, ["Small"] = 4, ["Standard"] = 6, ["Large"] = 8, ["Huge"] = 10 };
		foreach (WorldSize size in save.WorldSizes) {
			Assert.Equal(expected[size.name], GameSetup.DefaultEternalWarCitiesPerCiv(save.WorldSizes, size));
		}
	}

	// The game's creator may choose how many cities each civ starts with.
	[Fact]
	public void StartsWithTheCitiesChosen() {
		(_, C7GameData.GameData gameData) = NewGame(5, citiesPerCiv: 7);
		Assert.All(gameData.players.Where(p => !p.isBarbarians), p => Assert.Equal(7, p.cities.Count));
	}

	// A seafaring civ's capital is on the coast, even when it's dealt no
	// coastal site of its own.
	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	public void SeafaringCapitalsAreCoastal(int seed) {
		List<string> civs = ["England", "Spain", "Carthage", "Rome", "Egypt", "Babylon"];
		(_, C7GameData.GameData gameData) = NewGame(seed, citiesPerCiv: 2, civNames: civs);
		foreach (Player p in gameData.players.Where(p => !p.isBarbarians && p.civilization.traits.Contains(Civilization.Trait.Seafaring))) {
			City capital = p.cities.Single(c => c.IsCapital());
			Assert.True(capital.location.NeighborsOcean(), $"seed {seed}: seafaring {p.civilization.name}'s capital is inland");
		}
	}

	// A captured city keeps its library, though it has culture.
	[Fact]
	public void CapturedCitiesKeepTheirLibrary() {
		(_, C7GameData.GameData gameData) = NewGame(6);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		for (int i = 0; i < 2; ++i) {
			City city = civs[0].cities.First(c => !c.IsCapital());
			CityInteractions.CaptureCity(city, civs[1]);
			Assert.Equal(civs[1], city.owner);
			Assert.Contains(city.GetBuildings(), b => b.building.name == "Library");
			CityInteractions.CaptureCity(city, civs[0]);
		}
	}

	// When the cities chosen don't all fit, every civ gets the same smaller
	// number.
	[Fact]
	public void CivsGetFewerCitiesWhenTheyDontFit() {
		(_, C7GameData.GameData gameData) = NewGame(7, citiesPerCiv: GameSetup.MaxEternalWarCitiesPerCiv);
		List<int> counts = gameData.players.Where(p => !p.isBarbarians).Select(p => p.cities.Count).Distinct().ToList();
		Assert.Single(counts);
		Assert.InRange(counts[0], 1, GameSetup.MaxEternalWarCitiesPerCiv - 1);
	}

	// Citizens work tiles as far as a city's borders reach after their
	// second expansion: radius 3, where they're the civ's.
	[Fact]
	public void CitizensWorkAsFarAsTheSecondBorderExpansion() {
		(_, C7GameData.GameData gameData) = NewGame(8);
		EngineStorage.InitializeGameDataForTests(gameData);
		Assert.Equal(GameSetup.EternalWarWorkRadius, gameData.rules.MaxRankOfWorkableTiles);
		City city = gameData.cities.First();
		Tile far = city.location.GetTilesWithinRankDistance(3)
			.First(t => t != Tile.NONE && t.RankDistanceTo(city.location) == 3 && t.IsLand() && !t.HasCity());
		far.owningCity = city;
		Assert.Contains(far, city.GetWorkableTiles());
	}

	// Cities grow to 12 without fresh water or an aqueduct, to 24 with one,
	// and past that with a hospital.
	[Fact]
	public void CitiesGrowToTwelveWithoutAnAqueduct() {
		(_, C7GameData.GameData gameData) = NewGame(9);
		EngineStorage.InitializeGameDataForTests(gameData);
		City city = gameData.cities.First(c => !c.IsCapital() && !c.location.BordersRiver() && !c.location.NeighborsFreshWater());
		CitizenType citizen = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
		void GrowTo(int size) {
			while (city.residents.Count < size) {
				city.AddCitizen(new CityResident { city = city, nationality = city.owner.civilization, citizenType = citizen });
			}
		}

		GrowTo(11);
		Assert.True(city.CanGrowPopulationByOne(gameData));
		GrowTo(12);
		Assert.False(city.CanGrowPopulationByOne(gameData));

		city.AddBuilding(gameData.Buildings.Find(b => b.name == "Aqueduct"));
		Assert.True(city.CanGrowPopulationByOne(gameData));
		GrowTo(24);
		Assert.False(city.CanGrowPopulationByOne(gameData));

		city.AddBuilding(gameData.Buildings.Find(b => b.name == "Hospital"));
		Assert.True(city.CanGrowPopulationByOne(gameData));
	}

	// Every tile makes a commerce more, city centers too, forest on tundra a
	// food more, and desert, but not flood plains, a shield more.
	[Fact]
	public void TilesMakeMoreCommerceAndTundraForestMoreFood() {
		(_, C7GameData.GameData gameData) = NewGame(10);
		EngineStorage.InitializeGameDataForTests(gameData);
		City city = gameData.cities.First();
		Player owner = city.owner;
		Tile land = city.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity());

		int Commerce(Tile t) => t.CommerceYield(owner).yield;
		int withBonus = Commerce(land), centerWithBonus = Commerce(city.location);
		gameData.rules.TileCommerceBonus = 0;
		Assert.Equal(Commerce(land) + 1, withBonus);
		Assert.Equal(Commerce(city.location) + 1, centerWithBonus);

		land.baseTerrainType = gameData.terrainTypes.Find(t => t.IsTundra);
		land.overlayTerrainType = gameData.terrainTypes.Find(t => t.IsForest);
		int food = land.FoodYield(owner).yield;
		gameData.rules.TundraForestFoodBonus = 0;
		Assert.Equal(land.FoodYield(owner).yield + 1, food);

		TerrainType desert = gameData.terrainTypes.Find(t => t.IsDesert);
		land.baseTerrainType = desert;
		land.overlayTerrainType = desert;
		int desertShields = land.ProductionYield(owner).yield;
		land.overlayTerrainType = gameData.terrainTypes.Find(t => t.IsFloodPlain);
		int floodPlainShields = land.ProductionYield(owner).yield;
		gameData.rules.DesertShieldBonus = 0;
		Assert.Equal(floodPlainShields, land.ProductionYield(owner).yield);
		land.overlayTerrainType = desert;
		Assert.Equal(land.ProductionYield(owner).yield + 1, desertShields);
	}

	// A worker beaten in battle becomes a slave worker in the winner's
	// nearest city.
	[Fact]
	public async Task ABeatenWorkerBecomesASlaveInTheWinnersNearestCity() {
		(_, C7GameData.GameData gameData) = NewGame(11);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		(Player winner, Player loser) = (civs[0], civs[1]);

		bool Empty(Tile t) => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0 && !t.IsImpassable();
		Tile to = gameData.map.tiles.First(t => Empty(t) && t.neighbors.Values.Any(Empty));
		(TileDirection dir, Tile from) = to.neighbors.First(n => Empty(n.Value));
		MapUnit knight = gameData.SpawnUnit(winner, gameData.unitPrototypes.First(p => p.name == "Knight"), from);
		MapUnit worker = gameData.SpawnUnit(loser, gameData.unitPrototypes.First(p => p.name == "Worker"), to);
		worker.hitPointsRemaining = 1;
		City nearest = winner.cities.OrderBy(c => c.location.DistanceTo(to)).First();
		int slavesBefore = nearest.location.unitsOnTile.Count(u => u.owner == winner && u.unitType.isWorker);

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ZeroRandom();
		try {
			Assert.True(await knight.Move(dir.Reversed()));
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.DoesNotContain(worker, gameData.mapUnits);
		List<MapUnit> slaves = nearest.location.unitsOnTile.Where(u => u.owner == winner && u.unitType.isWorker).ToList();
		Assert.Equal(slavesBefore + 1, slaves.Count);
		Assert.Contains(slaves, s => s.nationality == loser.civilization);
	}

	// Staging gives units of the leading civ's age, but never a unique unit,
	// though it may give a unit some civ's unique unit replaces.
	[Fact]
	public void StagingGivesUnitsOfTheLeadingAgeButNoUniqueUnits() {
		(_, C7GameData.GameData gameData) = NewGame(12);
		EngineStorage.InitializeGameDataForTests(gameData);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();

		List<UnitPrototype> pool = Staging.Pool(gameData);
		Assert.NotEmpty(pool);
		Assert.All(pool, u => Assert.Equal(1, EraUtils.GetEraIndex(u.requiredTech.EraCivilopediaName)));
		Assert.Contains(pool, u => u.name == "Knight");
		Assert.Contains(pool, u => u.name == "Musketman");
		Assert.DoesNotContain(pool, u => u.name == "Musketeer" || u.name == "Man-O-War");
		Assert.DoesNotContain(pool, u => u.isSettler || u.isWorker || u.isLeader);
		// A unique unit of the age, such as Japan's Samurai, isn't given.
		Assert.Contains(gameData.unitPrototypes, u => u.requiredTech != null
			&& EraUtils.GetEraIndex(u.requiredTech.EraCivilopediaName) == 1 && !civs.All(c => u.IsProducibleBy(c.civilization)));

		civs[2].eraCivilopediaName = EraUtils.INDUSTRIAL_AGE_CVLPD;
		Assert.All(Staging.Pool(gameData), u => Assert.Equal(2, EraUtils.GetEraIndex(u.requiredTech.EraCivilopediaName)));
	}

	// Land units are given more often than ships.
	[Fact]
	public void StagingGivesLandUnitsMoreOftenThanShips() {
		(_, C7GameData.GameData gameData) = NewGame(12);
		EngineStorage.InitializeGameDataForTests(gameData);
		List<UnitPrototype> pool = Staging.Pool(gameData);
		int land = pool.Count(u => !u.IsSeaUnit()), sea = pool.Count(u => u.IsSeaUnit());
		Assert.True(land > 0 && sea > 0);

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new System.Random(1);
		try {
			int ships = Enumerable.Range(0, 4000).Count(_ => Staging.Pick(pool).IsSeaUnit());
			double expected = 4000.0 * sea / (sea + Staging.LandUnitWeight * land);
			Assert.InRange(ships, expected * 0.85, expected * 1.15);
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	// A ship may be sent to any city; the AI sends one to the coast.
	[Fact]
	public void AStagedShipMayGoToAnyCity() {
		(_, C7GameData.GameData gameData) = NewGame(15);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player owner = gameData.cities.First(c => !c.location.NeighborsOcean()
			&& c.owner.cities.Any(o => o.location.NeighborsOcean())).owner;
		UnitPrototype ship = gameData.unitPrototypes.First(p => p.IsSeaUnit() && p.attack > 0);
		Assert.Equal(owner.cities.Count, Staging.Choices(owner, ship).Count);
		Assert.True(Staging.AiChoice(gameData, owner, ship).location.NeighborsOcean());
	}

	// The AIs' units are placed at once; the humans choose a city, and no
	// one moves until they have.
	[Fact]
	public void StagingWaitsForTheHumansToChooseACity() {
		(_, C7GameData.GameData gameData) = NewGame(13);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.activePlayerID = human.id;
		List<Player> ais = gameData.players.Where(p => !p.isHuman && !p.isBarbarians).ToList();
		Dictionary<Player, int> unitsBefore = gameData.players.ToDictionary(p => p, p => p.units.Count);
		Assert.True(TurnHandling.IsPlayersTurn(gameData, human.id));

		Staging.BeginTurn(gameData);
		UnitPrototype unit = Staging.UnitType(gameData);
		Assert.NotNull(unit);
		Assert.All(ais, ai => {
			Assert.Equal(unitsBefore[ai] + 1, ai.units.Count);
			Assert.Equal(unit, ai.units.Last().unitType);
		});
		Assert.True(Staging.InProgress(gameData));
		Assert.Equal(unitsBefore[human], human.units.Count);
		Assert.False(TurnHandling.IsPlayersTurn(gameData, human.id));

		// Only one of their own cities.
		City theirs = ais[0].cities.First();
		Assert.False(Staging.Answer(gameData, human, theirs));
		City city = Staging.Choices(human, unit).Last();
		Assert.True(Staging.Answer(gameData, human, city));
		Assert.False(Staging.InProgress(gameData));
		Assert.Contains(human.units, u => u.unitType == unit && (u.location == city.location || !unit.IsLandUnit()));
		Assert.True(TurnHandling.IsPlayersTurn(gameData, human.id));
		Assert.False(Staging.Answer(gameData, human, city));
	}

	// Through the turn loop: once everyone has played a turn, the human is
	// asked where their unit goes before the next turn starts, and the next
	// starts once they've chosen. The question and answer cross a LAN too.
	[Fact]
	public async Task StagingComesBetweenTurnsInTheTurnLoop() {
		(_, C7GameData.GameData gameData) = NewGame(17);
		EngineStorage.InitializeGameDataForTests(gameData);
		new MsgSetAnimationsEnabled(false).send();
		EngineStorage.ProcessNextMessageToEngine();
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.activePlayerID = human.id;
		EngineStorage.uiControllerID = human.id;

		TurnHandling.OnBeginTurn();
		TurnHandling.InitTurnData();
		Task firstTurn = TurnHandling.AdvanceTurn();
		EngineWaits.WaitForStartTurnMessage(_ => { });
		await firstTurn;

		new MsgEndTurn { turn = gameData.turn }.send();
		MsgShowStaging ask = EngineWaits.WaitForMessageToUI<MsgShowStaging>(m => Assert.IsNotType<MsgStartTurn>(m));
		Assert.Equal(human, ask.recipient);
		Assert.Equal(Staging.UnitType(gameData).name, ask.unitType);
		Assert.False(TurnHandling.IsPlayersTurn(gameData, human.id));
		MsgShowStaging sent = (MsgShowStaging)C7Engine.Network.NetSerialization.DeserializeMessageToUI(C7Engine.Network.NetSerialization.Serialize(ask));
		Assert.Equal(ask.cities.Count, sent.cities.Count);

		int unitsBefore = human.units.Count;
		MessageToEngine answer = C7Engine.Network.NetSerialization.DeserializeMessageToEngine(
			C7Engine.Network.NetSerialization.Serialize(new MsgChooseStagingCity(ask.cities[0]) { playerID = human.id }));
		EngineStorage.SendToEngine(answer);
		EngineWaits.WaitForStartTurnMessage(_ => { });
		Assert.Equal(unitsBefore + 1, human.units.Count);
		Assert.Equal(ask.unitType, human.units.Last().unitType.name);
		Assert.True(TurnHandling.IsPlayersTurn(gameData, human.id));
	}

	// Rules without Staging have none.
	[Fact]
	public void StagingIsOnlyInTheScenario() {
		(_, C7GameData.GameData gameData) = NewGame(14);
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.rules.StagingUnits = false;
		Staging.BeginTurn(gameData);
		Assert.False(Staging.InProgress(gameData));
		Assert.Null(Staging.UnitType(gameData));
	}

	// A ship sent to an inland city puts to sea at the coast nearest it.
	[Fact]
	public void AStagedShipWithoutACoastalCityGoesToTheNearestSea() {
		(_, C7GameData.GameData gameData) = NewGame(15);
		EngineStorage.InitializeGameDataForTests(gameData);
		City inland = gameData.cities.First(c => !c.location.NeighborsOcean());
		Player owner = inland.owner;
		UnitPrototype ship = gameData.unitPrototypes.First(p => p.IsSeaUnit() && !p.sinksInSea && !p.sinksInOcean && p.attack > 0);

		MapUnit placed = Staging.Place(gameData, owner, ship, inland);
		Assert.NotNull(placed);
		Assert.True(placed.location.baseTerrainType.IsCoast);
		Assert.False(placed.location.isFreshWater);
		int distance = inland.location.DistanceTo(placed.location);
		Assert.DoesNotContain(gameData.map.tiles, t => t.baseTerrainType.IsCoast && !t.isFreshWater
			&& inland.location.DistanceTo(t) < distance && t.unitsOnTile.All(u => u.owner == owner));
	}

	// Coastal buildings, like the harbor and the Colossus, can be built
	// within 3 tiles of the coast, not just next to it.
	[Fact]
	public void CoastalBuildingsCanBeBuiltWithinThreeTilesOfTheCoast() {
		(_, C7GameData.GameData gameData) = NewGame(16);
		EngineStorage.InitializeGameDataForTests(gameData);
		Assert.Equal(GameSetup.EternalWarCoastalBuildingRange, gameData.rules.CoastalBuildingRange);
		City city = gameData.cities.First(c => !c.location.NeighborsOcean());
		// Some ocean two tiles away, where the city has none nearer.
		TerrainType coast = gameData.terrainTypes.First(t => t.IsWater && !t.IsSea && !t.IsOcean);
		Tile near = city.location.GetTilesWithinRankDistance(2)
			.First(t => t != Tile.NONE && t.RankDistanceTo(city.location) == 2 && !t.HasCity() && t.unitsOnTile.Count == 0);
		near.baseTerrainType = coast;
		near.overlayTerrainType = coast;
		near.isFreshWater = false;

		bool CanBuild(string name) => city.ListProductionOptions(gameData).Any(p => p is Building b && b.name == name);
		Assert.True(CanBuild("Harbor"));
		Assert.True(CanBuild("The Colossus"));

		gameData.rules.CoastalBuildingRange = 1;
		Assert.False(CanBuild("Harbor"));
		Assert.False(CanBuild("The Colossus"));
	}

	private class ZeroRandom : System.Random {
		protected override double Sample() => 0;
	}
}

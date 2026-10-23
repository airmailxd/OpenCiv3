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

	// Staging gives units of the leading civ's age and the ancient times,
	// but never a unique unit, though it may give a unit some civ's unique
	// unit replaces.
	[Fact]
	public void StagingGivesUnitsOfTheLeadingAgeButNoUniqueUnits() {
		(_, C7GameData.GameData gameData) = NewGame(12);
		EngineStorage.InitializeGameDataForTests(gameData);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();

		List<UnitPrototype> pool = Staging.Pool(gameData);
		Assert.NotEmpty(pool);
		Assert.All(pool, u => Assert.Contains(EraUtils.GetEraIndex(u.requiredTech.EraCivilopediaName), new[] { 0, 1 }));
		Assert.Contains(pool, u => u.name == "Swordsman");
		Assert.Contains(pool, u => u.name == "Knight");
		Assert.Contains(pool, u => u.name == "Musketman");
		Assert.DoesNotContain(pool, u => u.name == "Musketeer" || u.name == "Man-O-War");
		Assert.DoesNotContain(pool, u => u.isSettler || u.isWorker || u.isLeader);
		// A unique unit of the age, such as Japan's Samurai, isn't given.
		Assert.Contains(gameData.unitPrototypes, u => u.requiredTech != null
			&& EraUtils.GetEraIndex(u.requiredTech.EraCivilopediaName) == 1 && !civs.All(c => u.IsProducibleBy(c.civilization)));

		civs[2].eraCivilopediaName = EraUtils.INDUSTRIAL_AGE_CVLPD;
		Assert.All(Staging.Pool(gameData), u => Assert.Contains(EraUtils.GetEraIndex(u.requiredTech.EraCivilopediaName), new[] { 0, 2 }));
	}

	// The AIs get no extra starting units for the difficulty.
	[Fact]
	public void TheAIsGetNoExtraStartingUnits() {
		(_, C7GameData.GameData gameData) = NewGame(18);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player ai = gameData.players.First(p => !p.isHuman && !p.isBarbarians);
		int before = ai.units.Count;
		ai.MaybeSpawnBonusUnits(gameData);
		Assert.Equal(before, ai.units.Count);
	}

	// Ships are given only one time in twenty.
	[Fact]
	public void StagingRarelyGivesShips() {
		(_, C7GameData.GameData gameData) = NewGame(12);
		EngineStorage.InitializeGameDataForTests(gameData);
		List<UnitPrototype> pool = Staging.Pool(gameData);
		int land = pool.Count(u => !u.IsSeaUnit()), sea = pool.Count(u => u.IsSeaUnit());
		Assert.True(land > 0 && sea > 0);

		System.Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new System.Random(1);
		try {
			int ships = Enumerable.Range(0, 4000).Count(_ => Staging.Pick(pool).IsSeaUnit());
			double expected = 4000.0 / Staging.ShipOneIn;
			Assert.InRange(ships, expected * 0.75, expected * 1.25);
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

	// The highest bid for each unit wins it and pays; civs tied for the
	// highest each get one; a unit no one bid on goes unsold. The AIs
	// place theirs at once, and nobody moves until the humans have bid.
	[Fact]
	public void TheMarketSellsEachUnitToTheHighestBidders() {
		(_, C7GameData.GameData gameData) = NewGame(13);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.activePlayerID = human.id;
		List<Player> ais = gameData.players.Where(p => !p.isHuman && !p.isBarbarians).ToList();
		foreach (Player p in gameData.players) {
			p.gold = 100;
		}
		Assert.True(TurnHandling.IsPlayersTurn(gameData, human.id));

		Staging.BeginTurn(gameData);
		Assert.Equal(Staging.StandardOffers, gameData.staging.offers.Count);
		Assert.True(Staging.InProgress(gameData));
		Assert.False(TurnHandling.IsPlayersTurn(gameData, human.id));
		Assert.True(Staging.IsBidding(gameData, human));

		// Two AIs tie for the first unit, one wins the third, and no one
		// bids on the second.
		foreach (Player ai in ais) {
			gameData.staging.bids[ai.id.ToString()] = [0, 0, 0];
		}
		gameData.staging.bids[ais[0].id.ToString()] = [10, 0, 5];
		gameData.staging.bids[ais[1].id.ToString()] = [10, 0, 7];
		Dictionary<Player, int> unitsBefore = gameData.players.ToDictionary(p => p, p => p.units.Count);
		List<UnitPrototype> offers = Staging.Offers(gameData);

		Assert.True(Staging.AnswerBids(gameData, human, [3, 0, 0]));
		Assert.Equal(90, ais[0].gold);
		Assert.Equal(83, ais[1].gold);
		Assert.Equal(100, human.gold);
		Assert.Equal(unitsBefore[ais[0]] + 1, ais[0].units.Count);
		Assert.Equal(unitsBefore[ais[1]] + 2, ais[1].units.Count);
		Assert.Contains(ais[0].units, u => u.unitType == offers[0]);
		Assert.Contains(ais[1].units, u => u.unitType == offers[2]);
		Assert.All(ais.Skip(2), ai => Assert.Equal(unitsBefore[ai], ai.units.Count));
		Assert.False(Staging.InProgress(gameData));
		Assert.True(TurnHandling.IsPlayersTurn(gameData, human.id));
	}

	// Bids can't come to more than the bidder's gold: the last are cut. A
	// human who wins chooses a city for the unit before anyone moves.
	[Fact]
	public void AHumanWhoWinsChoosesACity() {
		(_, C7GameData.GameData gameData) = NewGame(13);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player human = gameData.players.First(p => p.isHuman);
		EngineStorage.activePlayerID = human.id;
		foreach (Player p in gameData.players) {
			p.gold = 0;
		}
		human.gold = 50;
		Staging.BeginTurn(gameData);
		List<UnitPrototype> offers = Staging.Offers(gameData);

		Staging.AnswerBids(gameData, human, [40, 30, 0]);
		Assert.Equal([40, 10, 0], gameData.staging.bids[human.id.ToString()]);
		Assert.Equal(0, human.gold);
		Assert.True(Staging.InProgress(gameData));
		Assert.Equal(offers[0], Staging.NextToPlace(gameData, human));

		City theirs = gameData.cities.First(c => c.owner != human);
		Assert.False(Staging.Answer(gameData, human, theirs));
		City city = human.cities.Last();
		Assert.True(Staging.Answer(gameData, human, city));
		Assert.Equal(offers[1], Staging.NextToPlace(gameData, human));
		Assert.True(Staging.Answer(gameData, human, city));
		Assert.False(Staging.InProgress(gameData));
		Assert.Contains(human.units, u => u.unitType == offers[0]);
		Assert.Contains(human.units, u => u.unitType == offers[1]);
		Assert.True(TurnHandling.IsPlayersTurn(gameData, human.id));
	}

	// One more unit is offered for each world size above standard, and one
	// fewer for each below, but at least one.
	[Fact]
	public void TheMarketOffersMoreUnitsOnBiggerWorlds() {
		Assert.Equal(3, Staging.OffersForWorldSize(2, 2));
		Assert.Equal(5, Staging.OffersForWorldSize(4, 2));
		Assert.Equal(1, Staging.OffersForWorldSize(0, 2));
		Assert.Equal(1, Staging.OffersForWorldSize(0, 5));
	}

	// Through the turn loop: once everyone has played a turn, the human is
	// shown the market before the next turn starts, then who won, then
	// asked where their unit goes, and the next turn starts once they've
	// chosen. The messages cross a LAN too.
	[Fact]
	public async Task TheMarketComesBetweenTurnsInTheTurnLoop() {
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
		foreach (Player p in gameData.players) {
			p.gold = 0;
		}
		human.gold = 1000;

		new MsgEndTurn { turn = gameData.turn }.send();
		MsgShowMarket market = EngineWaits.WaitForMessageToUI<MsgShowMarket>(m => Assert.IsNotType<MsgStartTurn>(m));
		Assert.Equal(human, market.recipient);
		Assert.Equal(Staging.BidSeconds, market.seconds);
		Assert.False(TurnHandling.IsPlayersTurn(gameData, human.id));

		int unitsBefore = human.units.Count;
		EngineStorage.SendToEngine(C7Engine.Network.NetSerialization.DeserializeMessageToEngine(
			C7Engine.Network.NetSerialization.Serialize(new MsgPlaceMarketBids([gameData.players.Max(p => p.gold), 0, 0]) { playerID = human.id })));
		MsgShowMarketResults results = EngineWaits.WaitForMessageToUI<MsgShowMarketResults>(m => Assert.IsNotType<MsgStartTurn>(m));
		MsgShowMarketResults sentResults = (MsgShowMarketResults)C7Engine.Network.NetSerialization.DeserializeMessageToUI(C7Engine.Network.NetSerialization.Serialize(results));
		Assert.Equal([human], sentResults.results[0].winners);
		Assert.Empty(sentResults.results[1].winners);

		MsgShowStaging ask = EngineWaits.WaitForMessageToUI<MsgShowStaging>(m => Assert.IsNotType<MsgStartTurn>(m));
		Assert.Equal(market.offers[0], ask.unitType);
		EngineStorage.SendToEngine(C7Engine.Network.NetSerialization.DeserializeMessageToEngine(
			C7Engine.Network.NetSerialization.Serialize(new MsgChooseStagingCity(ask.cities[0]) { playerID = human.id })));
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
		Assert.Empty(Staging.Offers(gameData));
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
		// The owner sees around it.
		Assert.All(placed.location.neighbors.Values.Where(t => t != Tile.NONE).Append(placed.location),
			t => Assert.True(owner.tileKnowledge.isTileKnown(t)));
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

using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7Engine.Lua;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class KingsAndNomadsScenarioTest {
	internal static (SaveGame, C7GameData.GameData) NewGame(int seed) {
		SaveGame save = SaveGameFixture.LoadGameMode(new GameMode.Config("civ3")).GetSave();
		WorldCharacteristics wc = new(save) {
			landform = WorldCharacteristics.Landform.Continents,
			oceanCoverage = WorldCharacteristics.OceanCoverage.Percent_60,
			age = WorldCharacteristics.Age.Billion_4,
			climate = WorldCharacteristics.Climate.Normal,
			temperature = WorldCharacteristics.Temperature.Temperate,
			barbarianActivity = BarbarianActivity.Roaming,
			worldSize = new WorldSize() { width = 80, height = 80, numberOfCivs = 4, distanceBetweenCivs = 12, techRate = 240, optimalNumberOfCities = 16 },
			mapSeed = seed,
		};
		Civilization player = save.Civilizations.First(c => !c.IsIndependent && c.traits.Contains(Civilization.Trait.Expansionist));
		new GameSetup {
			playerCivilization = player,
			difficulty = save.Difficulties.First(d => d.Name == "Regent"),
			worldCharacteristics = wc,
			opponents = Enumerable.Repeat(new SelectedOpponent { isRandom = true }, 3).ToList(),
			victoryConditions = new VictoryConditions(),
			scenario = GameScenario.KingsAndNomads,
		}.Populate(save);
		return (save, SaveGameFixture.HydrateSaveGame(save));
	}

	// Each civ starts with a King and its horde, and no city or settler,
	// under Monarchy for good; garrisoned city-states belong to the
	// independents.
	[Fact]
	public void SetsUpTheScenario() {
		(_, C7GameData.GameData gameData) = NewGame(1);
		EngineStorage.InitializeGameDataForTests(gameData);
		Assert.True(gameData.victoryConditions.Regicide);
		Player independents = gameData.players.Single(p => p.isCityStates);
		List<Player> civs = gameData.players.Where(p => !p.IsIndependent).ToList();
		Assert.Equal(civs.Count * GameSetup.CityStatesPerCiv, independents.cities.Count);
		// The city-states are a player of their own, not the barbarians: at
		// war with every civ, at peace with the barbarians, and without the
		// barbarians' hit points.
		Player barbarians = gameData.players.Single(p => p.isBarbarians);
		Assert.NotSame(barbarians, independents);
		Assert.Empty(barbarians.cities);
		Assert.All(civs, c => Assert.True(PlayerRelationship.AtWar(c, independents)));
		Assert.False(PlayerRelationship.AtWar(barbarians, independents));
		Assert.All(independents.units, u => Assert.Equal(gameData.defaultExperienceLevel, u.experienceLevel));
		Assert.All(independents.cities, c => {
			Assert.Equal(GameSetup.CityStateSize, c.residents.Count);
			Assert.DoesNotContain(c.constructed_buildings, b => b.building.name == "Walls");
			Assert.Equal(3, c.location.unitsOnTile.Count(u => u.owner == independents));
		});
		foreach (Player civ in civs) {
			Assert.Equal("Monarchy", civ.government.name);
			foreach (Tech tech in gameData.techs) {
				civ.knownTechs.Add(tech.id);
			}
			Assert.Equal(["Monarchy"], civ.GetAvailableGovernments(gameData).Select(g => g.name));
			Assert.False(civ.CanStartRevolution(gameData));
			Assert.Empty(civ.cities);
			Assert.Single(civ.units, u => u.unitType.isKing);
			Assert.DoesNotContain(civ.units, u => u.unitType.isSettler);
			Assert.Equal(2, civ.units.Count(u => u.unitType.name == "Horseman"));
			Assert.Equal(2, civ.units.Count(u => u.unitType.name == "Archer"));
			Assert.Single(civ.units, u => u.unitType.name == "Swordsman");
		}
	}

	// A save from before the city-states had their own player, held by the
	// barbarians, gives them one when loaded.
	[Fact]
	public void OldSavesGiveTheCityStatesTheirOwnPlayer() {
		(SaveGame save, _) = NewGame(2);
		SavePlayer barbarians = save.Players.Single(p => save.Civilizations.Single(c => c.isBarbarian).name == p.civilization);
		SavePlayer cityStates = save.Players.Single(p => p.civilization == GameSetup.CityStatesName);
		int held = save.Cities.Count(c => c.owner == cityStates.id);
		foreach (SaveCity c in save.Cities.Where(c => c.owner == cityStates.id)) {
			c.owner = barbarians.id;
		}
		foreach (SaveUnit u in save.Units.Where(u => u.owner == cityStates.id)) {
			u.owner = barbarians.id;
		}
		save.Players.Remove(cityStates);
		save.Civilizations.RemoveAll(c => c.isCityStates);

		C7GameData.GameData gameData = SaveGameFixture.HydrateSaveGame(save);
		Player independents = gameData.players.Single(p => p.isCityStates);
		Assert.Equal(held, independents.cities.Count);
		Assert.Empty(gameData.players.Single(p => p.isBarbarians).cities);
		Assert.All(independents.cities, c => Assert.Contains(c.location.unitsOnTile, u => u.owner == independents));
	}

	// Every civ starts on a landmass with city-states of its own to take.
	[Theory]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(7)]
	public void EveryCivHasCityStatesOnItsLandmass(int seed) {
		(_, C7GameData.GameData gameData) = NewGame(seed);
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.map.recomputeContinents();
		Player independents = gameData.players.Single(p => p.isCityStates);
		foreach (Player civ in gameData.players.Where(p => !p.IsIndependent)) {
			int home = civ.units[0].location.continent;
			Assert.True(independents.cities.Count(c => c.location.continent == home) >= 2, $"{civ} has too few city-states at home");
		}
	}

	// A road from a civ's city to a city-state is a trade route: the
	// civ gets the resources inside the city-state's borders, gold a turn,
	// shields in the city that nothing wastes, and now and then a unit
	// (see CityStateTrade).
	[Fact]
	public void ARoadToACityStateIsATradeRoute() {
		(_, C7GameData.GameData gameData) = NewGame(8);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		City cityState = gameData.players.Single(p => p.isCityStates).cities[0];
		Tile cs = cityState.location;
		(Tile site, Tile middle) = cs.GetTilesWithinRankDistance(2)
			.Where(t => t != Tile.NONE && t.IsLand() && t.IsAllowCities() && !t.HasCity() && t.DistanceTo(cs) == 2)
			.SelectMany(t => t.neighbors.Values.Where(m => m != Tile.NONE && m.IsLand() && cs.neighbors.ContainsValue(m)).Select(m => (t, m)))
			.First();
		City city = CityInteractions.BuildCity(site, civ, "Trader");
		gameData.UpdateTileOwners();
		Tile resourceTile = cs.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && t.owningCity == cityState && t != middle);
		Resource luxury = gameData.Resources.First(r => r.Category == ResourceCategory.LUXURY);
		resourceTile.Resource = luxury;
		gameData.InvalidateCachedTradeNetwork();
		int before = civ.CalculateGoldPerTurn();
		int shieldsBefore = city.CurrentProductionYield().useful;
		Assert.Empty(gameData.GetTradeNetwork().ConnectedCityStates(civ));

		middle.overlays.Add(gameData.terrainImprovements.Single(ti => ti.key == C7GameData.Tile.TileOverlays.ROAD));
		gameData.InvalidateCachedTradeNetwork();
		Assert.Contains(cityState, gameData.GetTradeNetwork().ConnectedCityStates(civ));
		Assert.Equal(before + CityStateTrade.IncomePerCityState, civ.CalculateGoldPerTurn());
		if (civ.KnowsAboutResource(luxury)) {
			Assert.Contains(luxury, gameData.GetTradeNetwork().GetResourcesAvailableToCity(civ, city).Keys);
		}
		Assert.Equal(shieldsBefore + CityStateTrade.ShieldsPerCityState, city.CurrentProductionYield().useful);

		// When the city-state's chance comes up, it sends the civ a unit.
		gameData.turn = 1;
		gameData.random = new AlwaysZero();
		int units = city.location.unitsOnTile.Count(u => u.owner == civ);
		CityStateTrade.GiveGifts(gameData);
		Assert.Equal(units + 1, city.location.unitsOnTile.Count(u => u.owner == civ));
	}

	// A city-state's citizens are all happy and work only the tiles around
	// it; its center makes 5 food more and 2 more a citizen; those who find
	// no tile become specialists, each making 4 shields for it and 2 gold
	// and 2 beakers for each civ trading with it; its shields build its
	// defenders; and it never flips to a civ's culture.
	[Fact]
	public void CityStatesGrowRichAndTheirSpecialistsEnrichTheirPartners() {
		(_, C7GameData.GameData gameData) = NewGame(8);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player independents = gameData.players.Single(p => p.isCityStates);
		City cityState = independents.cities[0];
		Assert.All(cityState.residents.Where(r => r.tileWorked != Tile.NONE), r => Assert.Equal(1, r.tileWorked.DistanceTo(cityState.location)));
		int baseFood = cityState.location.overlayTerrainType.baseFoodProduction;
		Assert.Equal(2 + CityStates.CenterFood + CityStates.CenterFoodPerCitizen * cityState.residents.Count,
			cityState.location.FoodYield(cityState).yield);

		CitizenType laborer = gameData.citizenTypes.Find(c => c.IsDefaultCitizen);
		while (cityState.residents.Count < 12) {
			CityResident r = new() { nationality = independents.civilization, city = cityState, citizenType = laborer };
			cityState.AddCitizen(r);
			C7Engine.AI.CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
		}
		CityStates.ArrangeCitizens(gameData, cityState);
		// It grows without limit, with no aqueduct or hospital.
		Assert.True(cityState.CanGrowPopulationByOne(gameData));
		int worked = cityState.residents.Count(r => r.tileWorked != Tile.NONE);
		Assert.True(worked <= 8);
		int specialists = CityStates.Specialists(cityState);
		Assert.Equal(12 - worked, specialists);
		Assert.True(specialists >= 4);
		Assert.Equal(City.Mood.Happy, cityState.RecalculateCitizenMoods(gameData));
		Assert.All(cityState.residents.Where(r => r.citizenType.IsDefaultCitizen), r => Assert.Equal(CityResident.Mood.Happy, r.mood));
		int shields = cityState.CurrentProductionYield().useful;
		Assert.True(shields >= specialists * CityStates.SpecialistShields);

		// A partner's gold and beakers.
		Player civ = gameData.players.First(p => !p.IsIndependent);
		Tile cs = cityState.location;
		(Tile site, Tile middle) = cs.GetTilesWithinRankDistance(2)
			.Where(t => t != Tile.NONE && t.IsLand() && t.IsAllowCities() && !t.HasCity() && t.DistanceTo(cs) == 2)
			.SelectMany(t => t.neighbors.Values.Where(m => m != Tile.NONE && m.IsLand() && cs.neighbors.ContainsValue(m)).Select(m => (t, m)))
			.First();
		CityInteractions.BuildCity(site, civ, "Trader");
		middle.overlays.Add(gameData.terrainImprovements.Single(ti => ti.key == C7GameData.Tile.TileOverlays.ROAD));
		gameData.InvalidateCachedTradeNetwork();
		Assert.Contains(cityState, gameData.GetTradeNetwork().ConnectedCityStates(civ));
		Assert.Equal(CityStateTrade.IncomePerCityState + specialists * CityStates.SpecialistGold, CityStateTrade.Income(gameData, civ));
		Assert.Equal(specialists * CityStates.SpecialistBeakers, CityStateTrade.Beakers(gameData, civ));

		// Its shields raise a defender once they pay for one.
		UnitPrototype defender = CityStates.Defender(gameData);
		while (CityStates.Garrison(cityState) > 1) {
			cityState.location.unitsOnTile.First(u => u.owner == independents && u.unitType.defense > 0).RemoveFromPlay();
		}
		cityState.SetStoredShields(independents.ShieldCost(defender) - 1);
		gameData.turn = 1;
		gameData.random = new AlwaysOne();
		CityStates.BeginTurn(gameData);
		Assert.Equal(2, CityStates.Garrison(cityState));

		// And it never flips.
		Assert.Null(CultureFlip.BestChance(gameData, cityState));
	}

	// A city-state builds the best units the most advanced civ knows, that
	// the resources on its roads allow: two defenders, an attacker and a
	// bombard unit, over and over.
	[Fact]
	public void CityStatesBuildDefendersAttackersAndBombardUnitsInTurn() {
		(_, C7GameData.GameData gameData) = NewGame(9);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player independents = gameData.players.Single(p => p.isCityStates);
		Player leader = gameData.players.First(p => !p.IsIndependent);
		foreach (Tech tech in gameData.techs.Where(t => t.EraCivilopediaName == gameData.techs.First().EraCivilopediaName)) {
			leader.knownTechs.Add(tech.id);
		}
		City cityState = independents.cities[0];
		foreach (MapUnit u in cityState.location.unitsOnTile.Where(u => u.owner == independents).ToList()) {
			u.RemoveFromPlay();
		}
		List<CityStates.Role> built = new();
		for (int i = 0; i < 4; ++i) {
			UnitPrototype next = CityStates.NextUnit(gameData, cityState);
			Assert.NotNull(next);
			Assert.True(next.requiredResources.All(CityStates.Resources(cityState).Contains), $"{next.name} needs resources the city-state lacks");
			Assert.Equal(CityStates.Best(gameData, cityState, CityStates.RoleOf(next)), next);
			built.Add(CityStates.RoleOf(next));
			gameData.SpawnUnit(independents, next, cityState.location);
		}
		Assert.Equal([CityStates.Role.Defender, CityStates.Role.Defender, CityStates.Role.Attacker, CityStates.Role.Bombard], built);
		Assert.Equal(CityStates.Role.Defender, CityStates.NextRole(gameData, cityState));
	}

	// A city-state's attackers strike an enemy next to it when the odds are
	// good (here a knight against a warrior).
	[Fact]
	public async System.Threading.Tasks.Task CityStatesStrikeEnemiesNextToThem() {
		(_, C7GameData.GameData gameData) = NewGame(10);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		Player independents = gameData.players.Single(p => p.isCityStates);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		City cityState = independents.cities[0];
		Tile next = cityState.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity());
		gameData.SpawnUnit(civ, gameData.unitPrototypes.First(p => p.name == "Worker"), next);
		gameData.SpawnUnit(civ, gameData.unitPrototypes.First(p => p.name == "Warrior"), next);
		MapUnit knight = gameData.SpawnUnit(independents, gameData.unitPrototypes.First(p => p.name == "Knight"), cityState.location);
		MapUnit warrior = next.unitsOnTile.First(u => u.unitType.name == "Warrior");
		int knightHp = knight.hitPointsRemaining, warriorHp = warrior.hitPointsRemaining;
		await C7Engine.AI.CityStateDefenseAI.StrikeBack(independents, cityState);
		// It fought: one of them was hurt or killed.
		Assert.True(!gameData.mapUnits.Contains(knight) || !gameData.mapUnits.Contains(warrior)
			|| knight.hitPointsRemaining < knightHp || warrior.hitPointsRemaining < warriorHp);
	}

	private class AlwaysOne : System.Random {
		public override int Next(int maxValue) => maxValue - 1;
	}

	private class AlwaysZero : System.Random {
		public override int Next(int maxValue) => 0;
	}

	// The city-states' garrisons grow every 10 turns, up to 6, and they
	// gain walls on turn 40.
	[Fact]
	public void CityStatesGrowStronger() {
		(_, C7GameData.GameData gameData) = NewGame(9);
		EngineStorage.InitializeGameDataForTests(gameData);
		City city = gameData.players.Single(p => p.isCityStates).cities[0];
		int start = CityStates.Garrison(city);
		gameData.turn = CityStates.GarrisonGrowthTurns;
		CityStates.BeginTurn(gameData);
		Assert.Equal(start + 1, CityStates.Garrison(city));
		gameData.turn = CityStates.GarrisonGrowthTurns + 1;
		CityStates.BeginTurn(gameData);
		Assert.Equal(start + 1, CityStates.Garrison(city));
		for (int turn = 2 * CityStates.GarrisonGrowthTurns; turn <= 100; turn += CityStates.GarrisonGrowthTurns) {
			gameData.turn = turn;
			CityStates.BeginTurn(gameData);
		}
		Assert.Equal(CityStates.MaxGarrison, CityStates.Garrison(city));
		Assert.Contains(city.constructed_buildings, b => b.building.name == "Walls");
	}

	// The AIs get none of the difficulty's extra starting units, not even
	// once they take their first city.
	[Theory]
	[InlineData("Regent")]
	[InlineData("Deity")]
	public void TheAIsGetNoExtraStartingUnits(string difficulty) {
		(_, C7GameData.GameData gameData) = NewGame(10);
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.gameDifficulty = gameData.difficulties.First(d => d.Name == difficulty);
		Player ai = gameData.players.First(p => !p.isHuman && !p.IsIndependent);
		CityInteractions.CaptureCity(gameData.players.Single(p => p.isCityStates).cities[0], ai);
		int before = ai.units.Count;
		ai.MaybeSpawnBonusUnits(gameData);
		Assert.Equal(before, ai.units.Count);
	}

	// The civ's units on the King's tile or next to it fight at +100%;
	// farther away, they don't.
	[Fact]
	public void TheKingsPresenceStrengthensUnitsNearHim() {
		(_, C7GameData.GameData gameData) = NewGame(11);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		MapUnit king = civ.units.Single(u => u.unitType.isKing);
		MapUnit swordsman = civ.units.Single(u => u.unitType.name == "Swordsman");
		double Attack() => swordsman.StrengthVersus(null, CombatRole.Attack, null);
		double near = Attack();
		Assert.Equal(swordsman.unitType.attack * 2.0, near, 3);

		Tile far = gameData.map.tiles.First(t => t.IsLand() && t.DistanceTo(king.location) > 3 && t.unitsOnTile.Count == 0);
		king.location.unitsOnTile.Remove(king);
		king.location = far;
		far.unitsOnTile.Add(king);
		Assert.Equal(swordsman.unitType.attack, Attack(), 3);
	}

	// A city-state's units fight at +50% in their own city.
	[Fact]
	public void CityStateUnitsAreStrongerAtHome() {
		(_, C7GameData.GameData gameData) = NewGame(12);
		EngineStorage.InitializeGameDataForTests(gameData);
		City city = gameData.players.Single(p => p.isCityStates).cities[0];
		MapUnit defender = city.location.unitsOnTile.First(u => u.owner == city.owner);
		Assert.Contains(defender.ListStrengthBonusesVersus(null, CombatRole.Defense, null), b => b.description == "Defending their city-state" && b.amount == 0.5);
		Assert.Contains(defender.ListStrengthBonusesVersus(null, CombatRole.Attack, null), b => b.description == "Defending their city-state");
	}

	// Settlers are built only in a city taken from someone else.
	[Fact]
	public void SettlersOnlyInConqueredCities() {
		(_, C7GameData.GameData gameData) = NewGame(2);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		City city = gameData.players.Single(p => p.isCityStates).cities[0];
		CityInteractions.CaptureCity(city, civ);
		void Grow(City c) {
			while (c.residents.Count < 6) {
				c.AddCitizen(new CityResident { city = c, nationality = civ.civilization, citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen) });
			}
		}
		Grow(city);
		bool CanBuildSettler(City c) => c.ListProductionOptions(gameData).Any(p => p is UnitPrototype u && u.isSettler);
		Assert.True(CanBuildSettler(city));
		Tile site = gameData.map.tiles.First(t => t.IsLand() && t.IsAllowCities() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& gameData.cities.All(c => c.location.DistanceTo(t) > 3));
		City founded = CityInteractions.BuildCity(site, civ, "Newtown");
		Grow(founded);
		Assert.False(CanBuildSettler(founded));
	}

	// A civ whose King dies falls, and its cities go to the independents.
	[Fact]
	public void AFallenCivsCitiesGoIndependent() {
		(_, C7GameData.GameData gameData) = NewGame(3);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player independents = gameData.players.Single(p => p.isCityStates);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		City city = independents.cities[0];
		CityInteractions.CaptureCity(city, civ);
		Assert.Equal(civ, city.owner);
		MapUnit king = civ.units.Single(u => u.unitType.isKing);
		gameData.RemoveUnit(king);
		Assert.True(civ.defeated);
		Assert.Equal(independents, city.owner);
		Assert.Contains(city, gameData.cities);
	}

	// A fallen civ's cities become city-states only where their nine tiles
	// wouldn't touch another city-state's; the rest are disbanded.
	[Fact]
	public void AFallenCivsCitiesNearCityStatesAreDisbanded() {
		(_, C7GameData.GameData gameData) = NewGame(3);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player independents = gameData.players.Single(p => p.isCityStates);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		City cityState = independents.cities[0];
		// A city three tiles from a city-state, whose land would touch it,
		// and one far from them all.
		Tile near = gameData.map.tiles.First(t => t.IsAllowCities() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& t.DistanceTo(cityState.location) == 3 && CityStates.WouldTouch(t, cityState.location));
		Tile far = gameData.map.tiles.First(t => t.IsAllowCities() && !t.HasCity() && t.unitsOnTile.Count == 0
			&& independents.cities.All(c => !CityStates.WouldTouch(t, c.location)) && t.DistanceTo(near) > 4);
		City doomed = CityInteractions.BuildCity(near, civ, "Doomed");
		City freed = CityInteractions.BuildCity(far, civ, "Freed");
		gameData.RemoveUnit(civ.units.Single(u => u.unitType.isKing));
		Assert.True(civ.defeated);
		Assert.DoesNotContain(doomed, gameData.cities);
		Assert.Equal(independents, freed.owner);
	}

	// A city-state taken comes down to size 5, needs an aqueduct to grow
	// past 6 even by fresh water. A civ's units fight the city-states at
	// double strength before it has taken one, at 1.5 for its second, and
	// a tenth less for each one after. A city-state's land is its nine
	// tiles.
	[Fact]
	public void TakingACityStateCostsTheCaptor() {
		(_, C7GameData.GameData gameData) = NewGame(6);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player independents = gameData.players.Single(p => p.isCityStates);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		gameData.UpdateTileOwners();
		foreach (City c in independents.cities) {
			Assert.All(gameData.map.tiles.Where(t => t.owningCity == c), t => Assert.True(t.RankDistanceTo(c.location) <= 1));
		}

		City cityState = independents.cities[0];
		CitizenType laborer = gameData.citizenTypes.Find(c => c.IsDefaultCitizen);
		while (cityState.residents.Count < 12) {
			cityState.AddCitizen(new CityResident { nationality = independents.civilization, city = cityState, citizenType = laborer });
		}
		MapUnit soldier = civ.units.First(u => u.unitType.name == "Swordsman");
		MapUnit enemy = cityState.location.unitsOnTile.First(u => u.owner == independents);
		double before = soldier.StrengthVersus(enemy, CombatRole.Attack, null);

		CityInteractions.CaptureCity(cityState, civ);
		Assert.Equal(CityStates.CapturedSize, cityState.residents.Count);
		Assert.True(cityState.wasCityState);
		Assert.Equal(1, civ.cityStatesCaptured);
		cityState.AddCitizen(new CityResident { nationality = civ.civilization, city = cityState, citizenType = laborer });
		Assert.False(cityState.CanGrowPopulationByOne(gameData));

		MapUnit other = independents.units.First(u => u.location.cityAtTile != null);
		Assert.Equal(before * 1.5 / 2.0, soldier.StrengthVersus(other, CombatRole.Attack, null), 6);
		civ.cityStatesCaptured = 2;
		Assert.Equal(before * 1.35 / 2.0, soldier.StrengthVersus(other, CombatRole.Attack, null), 6);
	}

	// Holding half the world's cities wins.
	[Fact]
	public void HoldingHalfTheCitiesWins() {
		(_, C7GameData.GameData gameData) = NewGame(4);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player civ = gameData.players.First(p => !p.IsIndependent);
		EmpireVictory empire = Assert.Single(gameData.victories.OfType<EmpireVictory>());
		List<City> all = gameData.cities.ToList();
		foreach (City c in all.Take((all.Count + 1) / 2 - 1)) {
			CityInteractions.CaptureCity(c, civ);
		}
		Assert.False(empire.HasVictory(empire.Evaluate(civ, gameData)));
		CityInteractions.CaptureCity(gameData.cities.First(c => c.owner != civ), civ);
		Assert.True(empire.HasVictory(empire.Evaluate(civ, gameData)));
	}
}

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
	private static (SaveGame, C7GameData.GameData) NewGame(int seed) {
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
		Civilization player = save.Civilizations.First(c => !c.isBarbarian && c.traits.Contains(Civilization.Trait.Expansionist));
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

	// Each civ starts with a King and its horde, and no city or settler;
	// walled, garrisoned city-states belong to the independents.
	[Fact]
	public void SetsUpTheScenario() {
		(_, C7GameData.GameData gameData) = NewGame(1);
		EngineStorage.InitializeGameDataForTests(gameData);
		Assert.True(gameData.victoryConditions.Regicide);
		Player independents = gameData.players.Single(p => p.isBarbarians);
		List<Player> civs = gameData.players.Where(p => !p.isBarbarians).ToList();
		Assert.Equal(civs.Count * GameSetup.CityStatesPerCiv, independents.cities.Count);
		Assert.All(independents.cities, c => {
			Assert.Equal(GameSetup.CityStateSize, c.residents.Count);
			Assert.Contains(c.constructed_buildings, b => b.building.name == "Walls");
			Assert.Equal(3, c.location.unitsOnTile.Count(u => u.owner == independents));
		});
		foreach (Player civ in civs) {
			Assert.Empty(civ.cities);
			Assert.Single(civ.units, u => u.unitType.isKing);
			Assert.DoesNotContain(civ.units, u => u.unitType.isSettler);
			Assert.Equal(2, civ.units.Count(u => u.unitType.name == "Horseman"));
			Assert.Equal(2, civ.units.Count(u => u.unitType.name == "Archer"));
		}
	}

	// Settlers are built only in a city taken from someone else.
	[Fact]
	public void SettlersOnlyInConqueredCities() {
		(_, C7GameData.GameData gameData) = NewGame(2);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player civ = gameData.players.First(p => !p.isBarbarians);
		City city = gameData.players.Single(p => p.isBarbarians).cities[0];
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
		Player independents = gameData.players.Single(p => p.isBarbarians);
		Player civ = gameData.players.First(p => !p.isBarbarians);
		City city = independents.cities[0];
		CityInteractions.CaptureCity(city, civ);
		Assert.Equal(civ, city.owner);
		MapUnit king = civ.units.Single(u => u.unitType.isKing);
		gameData.RemoveUnit(king);
		Assert.True(civ.defeated);
		Assert.Equal(independents, city.owner);
		Assert.Contains(city, gameData.cities);
	}

	// Holding half the world's cities wins.
	[Fact]
	public void HoldingHalfTheCitiesWins() {
		(_, C7GameData.GameData gameData) = NewGame(4);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player civ = gameData.players.First(p => !p.isBarbarians);
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

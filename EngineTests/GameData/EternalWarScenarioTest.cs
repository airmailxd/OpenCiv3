using System.Collections.Generic;
using System.Linq;
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
		// tile of their city radius.
		List<Tile> cityTiles = gameData.cities.Select(c => c.location).ToList();
		foreach (Tile t in cityTiles) {
			Assert.All(cityTiles.Where(o => o != t), o => Assert.True(t.DistanceTo(o) >= 4));
		}
		int rank = gameData.rules.MaxRankOfWorkableTiles;
		List<Tile> radii = cityTiles.SelectMany(t => t.GetTilesWithinRankDistance(rank)).ToList();
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
}

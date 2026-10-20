using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Rules from the compendium's sections on map, terrain and workers (07) and
// on science and wonders (04), checked against the engine.
public class CompendiumTerrainScienceTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public CompendiumTerrainScienceTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	private UnitPrototype Prototype(string name) {
		return gameData.unitPrototypes.Single(p => p.name == name);
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	// ---- Workers ----

	// Each worker's points a turn are rounded down, never below one.
	[Fact]
	public void WorkerPointsAreWholeNumbersRoundedDown() {
		Tile tile = gameData.map.tiles.First(IsEmptyLand);
		MapUnit slave = gameData.SpawnUnit(us, Prototype("Worker"), tile);
		slave.nationality = them.civilization;
		Assert.True(slave.IsCaptive());
		us.civilization.traits.Add(Civilization.Trait.Industrious);
		us.government.workerRate = 2;

		// 2 x 1.5 x 0.5 = 1.5, rounded down.
		Assert.Equal(1f, slave.workerSpeed());

		// Democracy: 3 x 1.5 = 4.5 for a native worker.
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), tile);
		us.government.workerRate = 3;
		Assert.Equal(4f, worker.workerSpeed());

		// Anarchy: a slave's 0.5 (without the trait) is raised to one.
		us.civilization.traits.Remove(Civilization.Trait.Industrious);
		us.government.workerRate = 1;
		Assert.Equal(1f, slave.workerSpeed());

		// After Replaceable Parts an Industrious civ's slave makes 3.
		us.civilization.traits.Add(Civilization.Trait.Industrious);
		us.government.workerRate = 2;
		Tech tech = gameData.techs.First(t => !us.knownTechs.Contains(t.id));
		tech.DoublesWorkerRate = true;
		us.knownTechs.Add(tech.id);
		Assert.Equal(3f, slave.workerSpeed());
	}

	// Chopped shields pass over cities building a wonder, the Palace or a
	// spaceship part.
	[Fact]
	public void ForestShieldsSkipThePalaceAndSpaceshipParts() {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		City city = CityInteractions.BuildCity(tile, us, "Chopper");
		city.SetItemBeingProduced(gameData.Buildings.Single(b => b.name == "Temple"));
		Assert.True(city.CanReceiveForestShields());
		city.SetItemBeingProduced(gameData.Buildings.First(b => b.isCenterOfEmpire));
		Assert.False(city.CanReceiveForestShields());
		city.SetItemBeingProduced(gameData.Buildings.First(b => b.IsSpaceshipPart));
		Assert.False(city.CanReceiveForestShields());
		city.SetItemBeingProduced(gameData.Buildings.Single(b => b.name == "Heroic Epic"));
		Assert.False(city.CanReceiveForestShields());
	}

	// A barbarian city builds nothing and gains no culture.
	[Fact]
	public void BarbarianCitiesBuildNothingAndGainNoCulture() {
		Player barbarians = gameData.players.First(p => p.isBarbarians);
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		City city = CityInteractions.BuildCity(tile, barbarians, "Barbaria");
		city.AddBuilding(gameData.Buildings.Single(b => b.name == "Temple"));
		UnitPrototype warrior = Prototype("Warrior");
		city.SetItemBeingProduced(warrior);
		city.SetStoredShields(barbarians.ShieldCost(warrior));
		int units = barbarians.units.Count;
		int culture = city.GetCulture();

		barbarians.HandleCityUpdates(gameData);

		Assert.Equal(units, barbarians.units.Count);
		Assert.Equal(culture, city.GetCulture());
	}

	// ---- Research ----

	// Switching research loses the stored beakers, unless switched back the
	// same turn.
	[Fact]
	public void SwitchingResearchLosesTheBeakersUnlessUndoneThatTurn() {
		Tech[] techs = us.GetAvailableTechsToResearch(gameData.techs).Take(2).ToArray();
		Tech a = techs[0], b = techs[1];
		us.SetCurrentlyResearchedTech(a.id);
		us.beakers = 30;
		us.turnsResearched = 3;

		us.SetCurrentlyResearchedTech(b.id);
		Assert.Equal(0, us.beakers);
		us.SetCurrentlyResearchedTech(a.id);
		Assert.Equal(30, us.beakers);
		Assert.Equal(3, us.turnsResearched);

		// A turn later the abandoned beakers are gone.
		us.SetCurrentlyResearchedTech(b.id);
		gameData.turn += 1;
		us.SetCurrentlyResearchedTech(a.id);
		Assert.Equal(0, us.beakers);
	}

	// Picking the tech already being researched, or a goal it leads to, is
	// no switch.
	[Fact]
	public void ChoosingTheCurrentResearchAgainKeepsTheBeakers() {
		// A goal one step past a researchable tech.
		Tech goal = gameData.techs.First(t => !us.knownTechs.Contains(t.id)
			&& t.Prerequisites.Count(p => !us.knownTechs.Contains(p.id)) == 1
			&& us.GetAvailableTechsToResearch(gameData.techs).Contains(t.Prerequisites.Single(p => !us.knownTechs.Contains(p.id))));
		Tech current = goal.Prerequisites.Single(p => !us.knownTechs.Contains(p.id));
		us.SetCurrentlyResearchedTech(current.id);
		us.beakers = 30;
		us.SetCurrentlyResearchedTech(current.id);
		Assert.Equal(30, us.beakers);

		us.CalculateFreshTechQueueAndAssignNewCurrent(goal);
		Assert.Equal(current.id, us.currentlyResearchedTech);
		Assert.Equal(30, us.beakers);
	}
}

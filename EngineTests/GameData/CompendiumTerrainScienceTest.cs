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

	// A railroad needs Iron and Coal access and terrain that allows it on
	// every tile it is built on (owner's decision, 2026-10-21: no "railroad
	// to" that skips the resource check, which the shipped game's does).
	// The engine has no separate railroad-to command: a human's worker and
	// the automated ones (WorkerAI, which checks each tile with
	// CanPerformTerraformAction before starting a job there) build a
	// railroad one tile at a time, through the same check as a single
	// build.
	[Fact]
	public void EveryRailroadStepNeedsIronCoalAndTerrainThatAllowsIt() {
		Terraform railroad = gameData.Terraforms.Single(t => t.UIAction == C7Action.UnitBuildRailroad);
		Assert.Contains(railroad.RequiredResources, r => r.Key.Contains("iron", System.StringComparison.OrdinalIgnoreCase)
			|| r.Name == "Iron");
		Assert.Contains(railroad.RequiredResources, r => r.Key.Contains("coal", System.StringComparison.OrdinalIgnoreCase)
			|| r.Name == "Coal");
		us.knownTechs.Add(railroad.RequiredTech);

		// Roaded flat land with no Iron or Coal anywhere on our network.
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.OwningPlayer() == null
			&& (t.overlayTerrainType.Key == "grassland" || t.overlayTerrainType.Key == "plains"));
		tile.overlays.Add(gameData.terrainImprovements.Single(i => i.key == Tile.TileOverlays.ROAD));
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), tile);
		Assert.False(worker.CanPerformTerraformAction(railroad, tile));
		worker.PerformTerraformAction(railroad);
		Assert.Null(worker.WorkerJob);

		// The resources are what stops it there: without them, the tile
		// would take a railroad.
		System.Collections.Generic.List<Resource> needed = railroad.RequiredResources;
		railroad.RequiredResources = [];
		try {
			Assert.True(worker.CanPerformTerraformAction(railroad, tile));
			// Terrain that allows no railroad (here water) never does.
			Tile water = gameData.map.tiles.First(t => t.IsWater());
			Assert.False(worker.CanPerformTerraformAction(railroad, water));
		} finally {
			railroad.RequiredResources = needed;
		}
	}

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

	// Workers plant forests on plains, grassland and tundra (Engineering).
	[Fact]
	public void WorkersPlantForestsOnPlainsGrasslandAndTundra() {
		Terraform plant = gameData.Terraforms.Single(t => t.Name == "Plant Forest");
		us.knownTechs.Add(plant.RequiredTech);
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.owningCity == null
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		MapUnit worker = gameData.SpawnUnit(us, Prototype("Worker"), tile);
		TerrainType Terrain(string key) => gameData.terrainTypes.Single(t => t.Key == key);

		foreach (string key in new[] { "plains", "grassland", "tundra" }) {
			tile.baseTerrainType = tile.overlayTerrainType = Terrain(key);
			Assert.True(worker.CanPerformTerraformAction(plant), key);
		}
		foreach (string key in new[] { "hills", "desert", "forest" }) {
			tile.overlayTerrainType = Terrain(key);
			Assert.False(worker.CanPerformTerraformAction(plant), key);
		}

		tile.baseTerrainType = tile.overlayTerrainType = Terrain("grassland");
		TerrainImprovement irrigation = gameData.terrainImprovements.Single(i => i.key == Tile.TileOverlays.IRRIGATION);
		tile.overlays.Add(irrigation);
		plant.OnComplete(us, tile);
		Assert.Equal("forest", tile.overlayTerrainType.Key);
		Assert.Equal("grassland", tile.baseTerrainType.Key);
		Assert.False(tile.overlays.HasImprovement(irrigation));
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

	// A camp a unit destroys moves to a new site.
	[Fact]
	public void ADestroyedCampMovesToANewSite() {
		gameData.barbarianInfo.barbarianActivity = BarbarianActivity.Roaming;
		gameData.map.barbarianCamps.ToList().ForEach(t => BarbarianInteractions.DisperseCamp(gameData, t, us));
		Tile camp = BarbarianInteractions.PossibleNewCampSites(gameData).First();
		camp.hasBarbarianCamp = true;
		gameData.map.barbarianCamps.Add(camp);
		Assert.NotEmpty(BarbarianInteractions.PossibleNewCampSites(gameData));

		MapUnit warrior = gameData.SpawnUnit(us, Prototype("Warrior"), camp);
		warrior.OnEnterTile(camp, true);

		Assert.False(camp.hasBarbarianCamp);
		Tile moved = Assert.Single(gameData.map.barbarianCamps);
		Assert.NotSame(camp, moved);
		Assert.True(moved.hasBarbarianCamp);
	}

	// Barbarians search every tile within 6 steps all round, the intended
	// rule rather than the shipped NW/SE diagonal bug (owner's decision,
	// 2026-10-20).
	[Fact]
	public void BarbariansSearchEveryTileWithinSixSteps() {
		Tile here = gameData.map.tileAt(30, 30);
		System.Collections.Generic.HashSet<Tile> searched = C7Engine.AI.UnitAI.CombatAI.BarbarianSearchTiles(here).ToHashSet();
		// A 13 by 13 square of steps, less the barbarian's own tile.
		Assert.Equal(13 * 13 - 1, searched.Count);
		Assert.DoesNotContain(here, searched);
		Assert.All(here.neighbors.Values, n => Assert.Contains(n, searched));
		// Six steps E, N, NE and SW are searched.
		Assert.Contains(gameData.map.tileAt(42, 30), searched);
		Assert.Contains(gameData.map.tileAt(30, 18), searched);
		Assert.Contains(gameData.map.tileAt(36, 24), searched);
		Assert.Contains(gameData.map.tileAt(24, 36), searched);
		// Seven steps isn't, nor the far NW/SE diagonal of the stock bug,
		// which is searched separately (BarbarianDiagonalTiles).
		Assert.DoesNotContain(gameData.map.tileAt(44, 30), searched);
		Assert.DoesNotContain(gameData.map.tileAt(37, 37), searched);
		Assert.DoesNotContain(gameData.map.tileAt(42, 42), searched);
		Assert.DoesNotContain(gameData.map.tileAt(18, 18), searched);
	}

	// Barbarians also keep the stock game's reach along the NW/SE diagonal,
	// 12 steps each way, and a target there comes before any other (owner's
	// decision, 2026-10-21).
	[Fact]
	public void BarbariansAlsoSearchTheirNwSeDiagonalAndPreferIt() {
		Tile here = gameData.map.tileAt(30, 30);
		System.Collections.Generic.List<Tile> diagonal = C7Engine.AI.UnitAI.CombatAI.BarbarianDiagonalTiles(here).ToList();
		Assert.Equal(24, diagonal.Count);
		Assert.Equal(gameData.map.tileAt(31, 31), diagonal[0]);
		Assert.Equal(gameData.map.tileAt(29, 29), diagonal[1]);
		Assert.Contains(gameData.map.tileAt(42, 42), diagonal);
		Assert.Contains(gameData.map.tileAt(18, 18), diagonal);
		Assert.DoesNotContain(gameData.map.tileAt(43, 43), diagonal);

		Player barbarians = gameData.players.Single(p => p.isBarbarians);
		static bool Open(Tile t) => t != Tile.NONE && IsEmptyLand(t) && t.OwningPlayer() == null
			&& (t.overlayTerrainType.Key == "grassland" || t.overlayTerrainType.Key == "plains")
			&& t.GetTilesWithinTileSquare(1).All(n => n != Tile.NONE && n.IsLand() && !n.IsImpassable() && !n.HasCity());
		Tile start = gameData.map.tiles.FirstOrDefault(t => Open(t)
			&& Open(gameData.map.tileAt(t.XCoordinate + 6, t.YCoordinate))
			&& Open(gameData.map.tileAt(t.XCoordinate + 4, t.YCoordinate + 4))
			&& Open(gameData.map.tileAt(t.XCoordinate + 9, t.YCoordinate + 9))
			&& Open(gameData.map.tileAt(t.XCoordinate + 5, t.YCoordinate + 5)));
		Assert.NotNull(start);
		Tile east = gameData.map.tileAt(start.XCoordinate + 6, start.YCoordinate);
		Tile nearDiagonal = gameData.map.tileAt(start.XCoordinate + 4, start.YCoordinate + 4);
		Tile farDiagonal = gameData.map.tileAt(start.XCoordinate + 9, start.YCoordinate + 9);
		foreach (Tile t in gameData.map.tiles) {
			barbarians.tileKnowledge.knownTiles.Add(t);
		}

		bool fog = EngineStorage.aiFogOfWar;
		EngineStorage.aiFogOfWar = false;
		try {
			MapUnit horse = gameData.SpawnUnit(barbarians, Prototype("Horseman"), start);
			// Nine steps out on the diagonal, beyond the 6 all round, is
			// still a target.
			gameData.SpawnUnit(us, Prototype("Warrior"), farDiagonal);
			Assert.Equal(farDiagonal, C7Engine.AI.UnitAI.CombatAI.MakeAiData(horse, barbarians)?.destination);

			// A unit three steps east scores better (it's nearer) than one
			// four steps down the diagonal, yet the diagonal comes first.
			gameData.SpawnUnit(us, Prototype("Warrior"), east);
			gameData.SpawnUnit(us, Prototype("Warrior"), nearDiagonal);
			Assert.Equal(nearDiagonal, C7Engine.AI.UnitAI.CombatAI.MakeAiData(horse, barbarians)?.destination);

			// With the diagonal empty, the all-round search finds the east one.
			foreach (Tile t in new[] { nearDiagonal, farDiagonal }) {
				foreach (MapUnit u in t.unitsOnTile.ToList()) {
					gameData.RemoveUnit(u);
				}
			}
			Assert.Equal(east, C7Engine.AI.UnitAI.CombatAI.MakeAiData(horse, barbarians)?.destination);
		} finally {
			EngineStorage.aiFogOfWar = fog;
		}
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

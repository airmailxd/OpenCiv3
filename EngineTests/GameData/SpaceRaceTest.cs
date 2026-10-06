using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// The Apollo Program, spaceship parts and the space race victory.
public class SpaceRaceTest : IClassFixture<SaveGameFixture>, System.IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public SpaceRaceTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		gameData.victoryConditions = VictoryConditions.NewGameDefaults();

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private Building BuildingNamed(string name) => gameData.Buildings.Single(b => b.name == name);

	private Building Apollo => gameData.Buildings.Single(b => b.buildSpaceshipParts);

	private List<Building> Parts => SpaceRace.PartTypes(gameData);

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity()));
		return CityInteractions.BuildCity(tile, owner, owner.GetNextCityName());
	}

	private void LearnAll(Player player) {
		foreach (Tech t in gameData.techs) {
			player.knownTechs.Add(t.id);
		}
	}

	private HashSet<Resource> AllResources => gameData.Resources.ToHashSet();

	private void BuildAllParts(Player player) {
		foreach (Building part in Parts) {
			for (int i = 0; i < SpaceRace.PartsNeeded(gameData, part.spaceshipPart); ++i) {
				SpaceRace.OnPartCompleted(gameData, player.cities[0], part);
			}
		}
	}

	[Fact]
	public void RulesetHasTenPartsNeedingOneEach() {
		List<Building> parts = Parts;
		Assert.Equal(10, parts.Count);
		Assert.Equal(Enumerable.Range(0, 10), parts.Select(p => p.spaceshipPart));
		Assert.Equal(0, BuildingNamed("SS Thrusters").spaceshipPart);
		Assert.Equal(9, BuildingNamed("SS Exterior Casing").spaceshipPart);
		Assert.All(parts, p => Assert.Equal(1, gameData.rules.SpaceshipPartsNeeded(p.spaceshipPart)));
		Assert.False(BuildingNamed("Factory").IsSpaceshipPart);
		Assert.Equal("Apollo Program", Apollo.name);
		Assert.True(Apollo.isSmallWonder);
	}

	[Fact]
	public void PartsNeedTheApolloProgram() {
		LearnAll(us);
		City city = BuildCity(us);
		Building engine = BuildingNamed("SS Engine");

		Assert.False(engine.CanProduce(city, AllResources));

		// Apollo built by another civ lets everyone build parts.
		LearnAll(them);
		City theirCity = BuildCity(them);
		theirCity.AddBuilding(Apollo);
		Assert.True(engine.CanProduce(city, AllResources));
		Assert.True(engine.CanProduce(theirCity, AllResources));

		// Parts still need their techs and resources.
		Assert.False(engine.CanProduce(city, []));
	}

	[Fact]
	public void CompletingApolloIsRememberedAndRevealsTheMap() {
		LearnAll(us);
		City city = BuildCity(us);
		Assert.True(Apollo.CanProduce(city, AllResources));
		Assert.Contains(gameData.map.tiles, t => !them.tileKnowledge.isTileKnown(t));

		SpaceRace.OnApolloCompleted(gameData, city, Apollo);

		foreach (Player p in gameData.players.Where(p => !p.isBarbarians)) {
			Assert.All(gameData.map.tiles, t => Assert.True(p.tileKnowledge.isTileKnown(t)));
		}
		// Recorded for the world, so it outlasts the Apollo city.
		Assert.True(SpaceRace.ApolloProgramBuilt(gameData));
		Assert.Contains(EngineStorage.messagesToUI, m => m is MsgShowScienceAdvisorPopup);
	}

	[Fact]
	public void ApolloCompletedThroughProduction() {
		LearnAll(us);
		City city = BuildCity(us);
		city.SetItemBeingProduced(Apollo);
		city.SetStoredShields(us.ShieldCost(Apollo));

		city.HandleCityProduction(gameData);

		Assert.Contains(city.constructed_buildings, cb => cb.building == Apollo);
		Assert.Contains(Apollo.name, gameData.GreatWondersBuilt);
		Assert.True(gameData.map.tiles.All(t => them.tileKnowledge.isTileKnown(t)));
	}

	[Fact]
	public void PartsAreLimitedToTheNumberTheShipNeeds() {
		LearnAll(us);
		City first = BuildCity(us);
		City second = BuildCity(us);
		gameData.GreatWondersBuilt.Add(Apollo.name);
		Building cockpit = BuildingNamed("SS Cockpit");

		Assert.True(cockpit.CanProduce(first, AllResources));
		Assert.True(cockpit.CanProduce(second, AllResources));

		// The one cockpit needed is being built elsewhere.
		first.SetItemBeingProduced(cockpit);
		Assert.True(cockpit.CanProduce(first, AllResources));
		Assert.False(cockpit.CanProduce(second, AllResources));

		// And once built, no more are needed.
		first.SetItemBeingProduced(BuildingNamed("Barracks"));
		SpaceRace.OnPartCompleted(gameData, first, cockpit);
		Assert.Equal(1, SpaceRace.PartsBuilt(us, cockpit.spaceshipPart));
		Assert.False(cockpit.CanProduce(first, AllResources));
		Assert.False(cockpit.CanProduce(second, AllResources));
		// Other parts are unaffected.
		Assert.True(BuildingNamed("SS Engine").CanProduce(second, AllResources));
	}

	[Fact]
	public void BuildingAPartAddsItToTheShipNotTheCity() {
		LearnAll(us);
		City city = BuildCity(us);
		gameData.GreatWondersBuilt.Add(Apollo.name);
		Building thrusters = BuildingNamed("SS Thrusters");
		city.SetItemBeingProduced(thrusters);
		city.SetStoredShields(us.ShieldCost(thrusters));

		city.HandleCityProduction(gameData);

		Assert.Equal(1, SpaceRace.PartsBuilt(us, thrusters.spaceshipPart));
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == thrusters);
		Assert.NotEqual<IProducible>(thrusters, city.itemBeingProduced);
		Assert.Equal((1, 10), SpaceRace.Progress(gameData, us));
	}

	[Fact]
	public void NoPartsWhenTheSpaceRaceIsDisabled() {
		LearnAll(us);
		City city = BuildCity(us);
		gameData.GreatWondersBuilt.Add(Apollo.name);
		gameData.victoryConditions.AllowSpaceRaceVictory = false;

		Assert.False(BuildingNamed("SS Engine").CanProduce(city, AllResources));
		// The Apollo Program itself is still available.
		Assert.True(Apollo.CanProduce(city, AllResources));
	}

	[Fact]
	public void CompletingTheShipWinsTheSpaceRace() {
		BuildCity(us);
		SpaceRaceVictory victory = new();

		Assert.False(victory.HasVictory(victory.Evaluate(us, gameData)));
		BuildAllParts(us);
		VictoryStatus status = victory.Evaluate(us, gameData);
		Assert.Equal(10, status.SpaceshipPartsBuilt);
		Assert.Equal(10, status.SpaceshipPartsNeeded);
		Assert.True(victory.HasVictory(status));
		Assert.False(victory.HasVictory(victory.Evaluate(them, gameData)));
		Assert.Single(victory.GenerateStatusRows(status, [victory.Evaluate(them, gameData)]));
	}

	[Fact]
	public void SpaceRaceVictoryEndsTheGame() {
		BuildCity(us);
		gameData.victories.Clear();
		gameData.victories.Add(new SpaceRaceVictory());
		BuildAllParts(us);

		TurnHandling.CheckVictory(gameData);

		Assert.True(gameData.gameOver);
		Assert.Equal(us, gameData.winner);
	}

	[Fact]
	public void SpaceRaceVictoryIsAddedWhenAllowed() {
		SaveGame save = fixture.saveGame;
		save.VictoryConditions = VictoryConditions.NewGameDefaults();
		Assert.Contains(save.ToGameData(fixture.behaviors).victories, v => v is SpaceRaceVictory);

		save = fixture.saveGame;
		save.VictoryConditions = new VictoryConditions { AllowSpaceRaceVictory = false };
		Assert.DoesNotContain(save.ToGameData(fixture.behaviors).victories, v => v is SpaceRaceVictory);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[Fact]
	public void LosingTheCapitalDestroysTheShip() {
		BuildCity(them);
		City capital = BuildCity(us);
		Assert.True(capital.IsCapital());
		BuildCity(us);
		while (capital.residents.Count < 3) {
			capital.AddCitizen(new CityResident() {
				city = capital,
				citizenType = capital.residents[0].citizenType,
				tileWorked = capital.residents[0].tileWorked,
			});
		}
		SpaceRace.OnPartCompleted(gameData, capital, BuildingNamed("SS Engine"));
		Assert.Equal(1, SpaceRace.Progress(gameData, us).built);

		CityInteractions.CaptureCity(capital, them);

		Assert.Equal(0, SpaceRace.Progress(gameData, us).built);
	}

	[Fact]
	public void LosingAnotherCityKeepsTheShip() {
		BuildCity(them);
		BuildCity(us);
		City other = BuildCity(us);
		SpaceRace.OnPartCompleted(gameData, other, BuildingNamed("SS Engine"));

		CityInteractions.CaptureCity(other, them);

		Assert.Equal(1, SpaceRace.Progress(gameData, us).built);
	}

	[Fact]
	public void ShipIsSavedAndLoaded() {
		BuildCity(us);
		SpaceRace.OnPartCompleted(gameData, us.cities[0], BuildingNamed("SS Engine"));
		SpaceRace.OnPartCompleted(gameData, us.cities[0], BuildingNamed("SS Exterior Casing"));

		SaveGame save = SaveGame.FromGameData(gameData);
		SavePlayer savedUs = save.Players.Single(p => p.id == us.id);
		Player loaded = savedUs.ToPlayer(gameData.map, gameData.civilizations, gameData.governments, gameData.techs,
			gameData.rules, gameData.alliances, gameData.terrainImprovements);

		Assert.Equal(1, SpaceRace.PartsBuilt(loaded, BuildingNamed("SS Engine").spaceshipPart));
		Assert.Equal(1, SpaceRace.PartsBuilt(loaded, BuildingNamed("SS Exterior Casing").spaceshipPart));
		Assert.Equal(0, SpaceRace.PartsBuilt(loaded, BuildingNamed("SS Cockpit").spaceshipPart));
	}

	[Fact]
	public void BuildingTakesItsPartIndexFromTheSave() {
		SaveBuilding part = new() { name = "Part", spaceshipPart = 3 };
		Building b = new(part, gameData);
		Assert.Equal(3, b.spaceshipPart);
		Building ordinary = new(new SaveBuilding { name = "Ordinary" }, gameData);
		Assert.Equal(-1, ordinary.spaceshipPart);
		Assert.False(ordinary.IsSpaceshipPart);
	}
}

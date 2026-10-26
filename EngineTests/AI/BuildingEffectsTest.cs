using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.AI;

// The AI values city improvements by what their effects are worth in the
// city (see BuildingEffects).
public sealed class BuildingEffectsTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly Player enemy;
	private readonly City city;

	public BuildingEffectsTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		EngineStorage.aiFogOfWar = false;
		player = gameData.players.First(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler));
		enemy = gameData.players.First(p => !p.isBarbarians && p != player);
		player.EnsureRelationshipExists(enemy);
		city = CityInteractions.BuildCity(player.units.First(u => u.unitType.isSettler).location, player, player.GetNextCityName());
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private Building Named(string name) => gameData.Buildings.First(b => b.name == name);

	private void GrowTo(int size) {
		while (city.residents.Count < size) {
			CityResident r = new() {
				city = city,
				nationality = player.civilization,
				citizenType = gameData.citizenTypes.Find(x => x.IsDefaultCitizen),
			};
			city.AddCitizen(r);
			CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
		}
	}

	private ChooseProducible.ProducibleStats Stats() {
		List<IProducible> options = city.ListProductionOptions(gameData).ToList();
		return ChooseProducible.CalculateStats(city, player, options);
	}

	private float Score(string building) {
		return ChooseProducible.ScoreBuilding(Stats(), city, player, Named(building));
	}

	private float Effect(string building) {
		return BuildingEffects.Score(Stats(), city, player, Named(building)) ?? float.NegativeInfinity;
	}

	private MapUnit SpawnEnemy(string unitName, Func<Tile, bool> where, int distance) {
		Tile tile = city.location.GetTilesWithinTileSquare(distance)
			.First(t => t != Tile.NONE && !t.HasCity() && t.unitsOnTile.Count == 0 && where(t));
		return gameData.SpawnUnit(enemy, gameData.unitPrototypes.First(p => p.name == unitName), tile);
	}

	[Fact]
	public void APeacefulCityNeverBuildsSamBatteriesNorACoastalFortress() {
		Assert.True(Score("SAM Missile Battery") <= -1000);
		Assert.True(Score("Coastal Fortress") <= -1000);
	}

	[Fact]
	public void EnemyAircraftNearbyMakeSamBatteriesWorthBuilding() {
		SpawnEnemy("Bomber", t => t.IsLand(), 4);
		Assert.True(Score("SAM Missile Battery") <= -1000);

		player.DeclareWarOn(enemy, gameData.turn);
		Assert.True(Score("SAM Missile Battery") > -1000);
	}

	[Fact]
	public void EnemyWarshipsNearbyMakeACoastalFortressWorthBuilding() {
		player.DeclareWarOn(enemy, gameData.turn);
		Assert.True(Score("Coastal Fortress") <= -1000);

		SpawnEnemy("Frigate", t => t.IsWater(), 8);
		Assert.True(Score("Coastal Fortress") > -1000);
	}

	[Fact]
	public void APollutingCityBuildsMassTransitBeforeSamBatteries() {
		Assert.True(Score("Mass Transit System") <= -1000);

		GrowTo(gameData.rules.MaximumLevel2CitySize + 4);
		Assert.True(city.PollutionIcons(gameData.rules) > 1);
		Assert.True(Score("Mass Transit System") > -1000);
		Assert.True(Score("Mass Transit System") > Score("SAM Missile Battery"));
	}

	[Fact]
	public void ARecyclingCenterOnlyWhereBuildingsPollute() {
		Assert.True(Score("Recycling Center") <= -1000);
		city.AddBuilding(Named("Factory"));
		Assert.True(Score("Recycling Center") > -1000);
	}

	[Fact]
	public void AnAirportOnlyForACivWithAircraft() {
		Assert.True(Score("Airport") <= -1000);
		gameData.SpawnUnit(player, gameData.unitPrototypes.First(p => p.name == "Fighter"), city.location);
		Assert.True(Score("Airport") > -1000);
	}

	[Fact]
	public void AGranaryIsWorthNothingToACityThatCantGrow() {
		GrowTo(gameData.rules.MaximumLevel1CitySize - 2);
		float growing = Effect("Granary");

		city.AddBuilding(Named("Aqueduct"));
		GrowTo(gameData.rules.MaximumLevel2CitySize);
		Assert.False(city.CanGrowPopulationByOne(gameData));
		float atTheCap = Effect("Granary");
		Assert.True(atTheCap <= growing);
		Assert.True(atTheCap < 0);
	}

	[Fact]
	public void ACommercialCityAtItsSizeCapPrefersALibraryToAGranary() {
		player.scienceRate = 10;
		player.luxuryRate = 0;
		city.AddBuilding(Named("Aqueduct"));
		GrowTo(gameData.rules.MaximumLevel2CitySize);
		Assert.True(city.CurrentCommerceYieldRaw().beakers > 0);

		Assert.True(Effect("Library") > Effect("Granary"));
		Assert.True(Score("Library") > Score("Granary"));
	}

	[Fact]
	public void AFactoryIsWorthMoreInAMoreProductiveCity() {
		GrowTo(2);
		float small = Effect("Factory");
		GrowTo(gameData.rules.MaximumLevel1CitySize);
		Assert.True(Effect("Factory") >= small);
	}

	// A city runs one power plant: a second adds only what it adds over
	// the first.
	[Fact]
	public void ASecondPowerPlantIsWorthLess() {
		GrowTo(gameData.rules.MaximumLevel1CitySize);
		city.AddBuilding(Named("Factory"));
		float first = Effect("Solar Plant");
		city.AddBuilding(Named("Coal Plant"));
		Assert.True(Effect("Solar Plant") < first);
	}

	// The unit builders are the civ's most productive cities of size 3 or
	// more that lose little to corruption.
	[Fact]
	public void UnitBuildersAreProductiveUncorruptCities() {
		GrowTo(4);
		Assert.Contains(city, ChooseProducible.UnitBuilders(player));

		// A corrupt city isn't one, nor a small one.
		city.corruption = 0.5f;
		gameData.turn += 1;
		Assert.DoesNotContain(city, ChooseProducible.UnitBuilders(player));
	}
}

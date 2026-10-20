using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// City pollution (see City.PollutionIcons and City.MaybePollute).
public class PollutionTest : IClassFixture<SaveGameFixture> {
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;

	public PollutionTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
	}

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

	private Building Named(string name) {
		return gameData.Buildings.First(b => b.name == name);
	}

	[Fact]
	public void TheBiqFlagsTheMassTransitAndRecyclingCenter() {
		Assert.True(Named("Mass Transit System").removesPopulationPollution);
		Assert.True(Named("Recycling Center").reducesBuildingPollution);
		Assert.False(Named("Factory").reducesBuildingPollution);
	}

	[Fact]
	public void EachCitizenAboveTwelvePollutes() {
		int threshold = gameData.rules.MaximumLevel2CitySize;
		Assert.Equal(12, threshold);
		GrowTo(threshold);
		Assert.Equal(0, city.PollutionIcons(gameData.rules));
		GrowTo(threshold + 3);
		Assert.Equal(3, city.PollutionIcons(gameData.rules));
	}

	[Fact]
	public void MassTransitLeavesOneIconForPopulation() {
		GrowTo(16);
		city.AddBuilding(Named("Mass Transit System"));
		Assert.Equal(1, city.PollutionIcons(gameData.rules));
	}

	[Fact]
	public void BuildingsAddTheirPollutionAndARecyclingCenterLeavesOne() {
		city.AddBuilding(Named("Factory"));
		int factory = Named("Factory").pollution;
		Assert.True(factory > 1);
		Assert.Equal(factory, city.PollutionIcons(gameData.rules));

		city.AddBuilding(Named("Recycling Center"));
		Assert.Equal(1, city.PollutionIcons(gameData.rules));
	}

	// The citizen working a tile that becomes polluted is made a specialist
	// (C3X's notes on the shipped game).
	[Fact]
	public void PollutionTurnsTheTilesWorkerIntoASpecialist() {
		GrowTo(12 + 5);
		C7GameData.GameData.rng = new Random(99);
		for (int turn = 0; turn < 5000; ++turn) {
			System.Collections.Generic.Dictionary<Tile, CityResident> worked = city.residents
				.Where(r => r.tileWorked != Tile.NONE && r.tileWorked != city.location)
				.ToDictionary(r => r.tileWorked);
			Tile polluted = city.MaybePollute(gameData);
			if (polluted == null || !worked.TryGetValue(polluted, out CityResident worker)) {
				continue;
			}
			Assert.Null(polluted.personWorkingTile);
			Assert.Equal(Tile.NONE, worker.tileWorked);
			Assert.False(worker.citizenType.IsDefaultCitizen);
			return;
		}
		Assert.Fail("Pollution never struck a worked tile");
	}

	[Fact]
	public void ACleanCityNeverPollutesNorDrawsARandomNumber() {
		C7GameData.GameData.rng = new Random(7);
		int expected = new Random(7).Next();
		Assert.Null(city.MaybePollute(gameData));
		Assert.Equal(expected, C7GameData.GameData.rng.Next());
	}

	[Fact]
	public void PollutionStrikesAboutOncePerHundredTurnsPerIcon() {
		GrowTo(12 + 5);
		Assert.Equal(5, city.PollutionIcons(gameData.rules));
		C7GameData.GameData.rng = new Random(1234);
		int strikes = 0;
		for (int turn = 0; turn < 2000; ++turn) {
			Tile polluted = city.MaybePollute(gameData);
			if (polluted != null) {
				++strikes;
				Assert.True(polluted.HasPollution());
				Assert.NotEqual(city.location, polluted);
				Assert.False(polluted.IsWater());
				polluted.ClearDamage();
			}
		}
		// 5% of 2000 turns is 100.
		Assert.InRange(strikes, 70, 130);
	}
}

using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
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
		// Over the threshold, not at it (owner's decision, 2026-10-21).
		GrowTo(threshold);
		Assert.Equal(0, city.PollutionIcons(gameData.rules));
		GrowTo(threshold + 1);
		Assert.Equal(1, city.PollutionIcons(gameData.rules));
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

	// Returns the given numbers from Next(int) in order.
	private class ScriptedRandom(params int[] values) : Random {
		private readonly System.Collections.Generic.Queue<int> queue = new(values);
		public override int Next(int maxValue) => queue.Dequeue();
	}

	// The search for the tile starts at a random one of the 20 around the
	// city ("Normally, it passes 20 as the limit here", C3X
	// patch_rand_int_to_place_pollution) and takes the first suitable one
	// from there, in the city-tile spiral.
	[Fact]
	public void PollutionSearchesOnFromARandomOneOfTheTwentyTiles() {
		GrowTo(12 + 1);
		TileLocation here = new(city.location);
		Tile Spiral(int i) {
			TileLocation at = ImportCiv3.GetTileFromSpiral(here, i);
			return gameData.map.tileAt(at.X, at.Y);
		}
		// Spiral tiles 6 and 7 (W and NW) are land; 6 is already polluted.
		foreach (int i in new[] { 6, 7 }) {
			Spiral(i).baseTerrainType = city.location.baseTerrainType;
			Spiral(i).ClearDamage();
		}
		Assert.True(Tile.TryAddPollution(Spiral(6)));

		Random original = C7GameData.GameData.rng;
		try {
			// Pollution strikes (0 of 100), and the search starts at index 5
			// of 20, spiral tile 6.
			C7GameData.GameData.rng = new ScriptedRandom(0, 5);
			Assert.Same(Spiral(7), city.MaybePollute(gameData));
		} finally {
			C7GameData.GameData.rng = original;
		}
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

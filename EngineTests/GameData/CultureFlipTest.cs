using System;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Culture flipping: cities defecting to a civ whose culture outweighs their
// owner's (see CultureFlip).
public class CultureFlipTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	public CultureFlipTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		gameData.turn = 10;

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman).Take(2).ToArray();
		us = civs[0];
		them = civs[1];
		// Only our cities and theirs are in play.
		foreach (Player p in gameData.players) {
			foreach (City c in p.cities.ToList()) {
				CityInteractions.DestroyCity(c);
			}
		}
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private static bool IsEmptyLand(Tile t) {
		return t.IsLand() && t.unitsOnTile.Count == 0 && !t.HasCity() && !t.hasBarbarianCamp && !t.IsImpassable();
	}

	private City BuildCity(Player owner, Func<Tile, bool> where = null) {
		Tile tile = gameData.map.tiles.First(t => IsEmptyLand(t) && t.IsAllowCities()
			&& t.neighbors.Values.All(n => n == Tile.NONE || !n.HasCity())
			&& (where == null || where(t)));
		City city = CityInteractions.BuildCity(tile, owner, owner.GetNextCityName());
		city.ownerChangedTurn = -1;
		return city;
	}

	private void AddResidents(City city, Civilization nationality, int count) {
		CitizenType worker = gameData.citizenTypes.First(c => c.IsDefaultCitizen);
		for (int i = 0; i < count; ++i) {
			city.residents.Add(new CityResident { city = city, nationality = nationality, citizenType = worker });
		}
	}

	// Our capital, their capital, and one of our cities, with foreign
	// citizens of theirs, that they have much more culture than us.
	private (City ourCapital, City theirCapital, City city) Setup() {
		City ourCapital = BuildCity(us);
		City theirCapital = BuildCity(them, t => t.DistanceTo(ourCapital.location) > 6);
		City city = BuildCity(us, t => t.DistanceTo(ourCapital.location) > 3 && t.DistanceTo(theirCapital.location) > 3);
		ourCapital.perPlayerCulture[us] = 10;
		city.perPlayerCulture[us] = 0;
		theirCapital.perPlayerCulture[them] = 200;
		AddResidents(city, them.civilization, 6);
		return (ourCapital, theirCapital, city);
	}

	[Fact]
	public void TheChanceFollowsTheFormula() {
		(_, _, City city) = Setup();
		city.perPlayerCulture[them] = 50;
		city.isInCivilDisorder = true;

		CultureFlip.Chance chance = CultureFlip.ChanceOfFlippingTo(gameData, city, them);
		Assert.Equal(city.residents.Count(r => r.nationality == them.civilization), chance.foreigners);
		Assert.Equal(2, chance.memoryFactor);
		Assert.Equal(2, chance.moodFactor);
		Assert.Equal(0, chance.garrison);
		double ratio = (double)CultureReport.TotalCulture(them) / CultureReport.TotalCulture(us);
		Assert.Equal(ratio, chance.cultureRatio, 6);
		double expected = (chance.foreigners + chance.tiles) * 2 * 2 * ratio / (CultureFlip.DistanceFactor * chance.distanceRatio);
		Assert.Equal(Math.Clamp(expected, 0, 1), chance.probability, 6);
	}

	[Fact]
	public void ResistersCountTwiceAndCelebrationsHalveTheChance() {
		(_, _, City city) = Setup();
		int foreigners = CultureFlip.ChanceOfFlippingTo(gameData, city, them).foreigners;
		city.resisters = 2;
		Assert.Equal(foreigners + 2, CultureFlip.ChanceOfFlippingTo(gameData, city, them).foreigners);

		city.celebrating = true;
		Assert.Equal(0.5, CultureFlip.ChanceOfFlippingTo(gameData, city, them).moodFactor);
	}

	[Fact]
	public void TheDistanceRatioIsCapped() {
		(City ourCapital, City theirCapital, City city) = Setup();
		double d = CultureFlip.DistanceRatio(city, us, them);
		Assert.InRange(d, CultureFlip.MinDistanceRatio, CultureFlip.MaxDistanceRatio);
		// A capital is as close to its owner as can be.
		Assert.Equal(CultureFlip.MaxDistanceRatio, CultureFlip.DistanceRatio(ourCapital, us, them));
		Assert.Equal(CultureFlip.MinDistanceRatio, CultureFlip.DistanceRatio(ourCapital, them, us));
		Assert.Equal(0, CultureFlip.DistanceToCapital(theirCapital, them));
	}

	[Fact]
	public void AGarrisonCanStopAFlip() {
		(_, _, City city) = Setup();
		Assert.True(CultureFlip.ChanceOfFlippingTo(gameData, city, them).probability > 0);

		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.IsLandUnit() && p.defense > 0 && p.attack > 0);
		for (int i = 0; i < 400; ++i) {
			city.AddUnit(warrior, gameData);
		}
		Assert.Equal(0, CultureFlip.ChanceOfFlippingTo(gameData, city, them).probability);
		Assert.Null(CultureFlip.BestChance(gameData, city));
	}

	[Fact]
	public void ACityDoesNotFlipOnTheTurnItChangedHands() {
		(_, _, City city) = Setup();
		Assert.NotNull(CultureFlip.BestChance(gameData, city));
		city.ownerChangedTurn = gameData.turn;
		Assert.Null(CultureFlip.BestChance(gameData, city));
	}

	[Fact]
	public void ChangingHandsRecordsTheTurn() {
		(_, _, City city) = Setup();
		CityInteractions.TransferCity(city, them);
		Assert.Equal(gameData.turn, city.ownerChangedTurn);
		Assert.Null(CultureFlip.BestChance(gameData, city));
	}

	[Fact]
	public void FlippingKillsTheGarrisonAndGivesTheNewOwnerADefender() {
		(_, _, City city) = Setup();
		int population = city.residents.Count;
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.IsLandUnit() && p.defense > 0 && p.attack > 0);
		city.AddUnit(warrior, gameData);
		MapUnit garrison = city.location.unitsOnTile.Single();

		CultureFlip.Flip(gameData, city, them);

		Assert.Equal(them, city.owner);
		Assert.Contains(city, them.cities);
		Assert.DoesNotContain(city, us.cities);
		Assert.Equal(population, city.residents.Count);
		Assert.DoesNotContain(garrison, us.units);
		Assert.DoesNotContain(garrison, them.units);
		Assert.All(city.location.unitsOnTile, u => Assert.Equal(them, u.owner));
		Assert.Single(city.location.unitsOnTile);
		Assert.False(city.IsInResistance);
	}

	[Fact]
	public void NoFlipsWhenTheyAreTurnedOff() {
		(_, _, City city) = Setup();
		// Certain to flip, but for the rule.
		gameData.rules.AllowCultureFlips = false;
		AddResidents(city, them.civilization, 2000);
		CultureFlip.ProcessEndOfRound(gameData);
		Assert.Equal(us, city.owner);

		gameData.rules.AllowCultureFlips = true;
		Assert.Equal(1, CultureFlip.BestChance(gameData, city).probability);
		CultureFlip.ProcessEndOfRound(gameData);
		Assert.Equal(them, city.owner);
	}

	[Fact]
	public void TheTurnACityChangedHandsIsSaved() {
		(_, _, City city) = Setup();
		city.ownerChangedTurn = 7;
		Assert.Equal(7, new SaveCity(city).ownerChangedTurn);
		city.ownerChangedTurn = -1;
		Assert.Null(new SaveCity(city).ownerChangedTurn);
	}
}

using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class BarbarianCampTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public BarbarianCampTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Fact]
	public void EveryCampStartsWithABasicBarbarianDefender() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Assert.NotEmpty(gameData.map.barbarianCamps);

		foreach (Tile camp in gameData.map.barbarianCamps) {
			MapUnit defender = Assert.Single(camp.unitsOnTile);
			Assert.True(defender.owner.isBarbarians);
			Assert.Equal(gameData.barbarianInfo.basicBarbarian, defender.unitType);
			Assert.Equal(gameData.barbarianInfo.maxHitpoints, defender.hitPointsRemaining);
		}
	}

	[Fact]
	public void UnguardedCampsGetAnAdvancedBarbarianDefender() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		Player barbarians = gameData.players.Find(p => p.isBarbarians);
		Tile emptyCamp = gameData.map.barbarianCamps[0];
		Tile guardedCamp = gameData.map.barbarianCamps[1];

		// Empty one camp, as if its defender had been killed without the camp
		// being taken.
		foreach (MapUnit unit in emptyCamp.unitsOnTile.ToList()) {
			gameData.mapUnits.Remove(unit);
			unit.owner.units.Remove(unit);
			emptyCamp.unitsOnTile.Remove(unit);
		}

		BarbarianInteractions.GarrisonUnguardedCamps(gameData, barbarians);

		MapUnit newDefender = Assert.Single(emptyCamp.unitsOnTile);
		Assert.Equal(gameData.barbarianInfo.advancedBarbarian, newDefender.unitType);
		Assert.Contains(newDefender, barbarians.units);
		// Camps that already had a defender are left alone.
		Assert.Single(guardedCamp.unitsOnTile);
	}
}

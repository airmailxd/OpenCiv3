using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

public class CivDestructionTest : IClassFixture<SaveGameFixture> {
	private readonly SaveGameFixture fixture;

	public CivDestructionTest(SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	private C7GameData.GameData Load(SaveGame save) {
		C7GameData.GameData gameData = save.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		return gameData;
	}

	private static bool IsSettler(SaveGame save, SaveUnit unit) {
		return save.UnitPrototypes.First(p => p.name == unit.prototype).actions.Contains(UnitAction.BuildCity);
	}

	// A scenario civ can start with units but no cities or settlers. It
	// isn't destroyed at the end of the turn for that, nor when it loses a
	// unit, while it still has units.
	[Fact]
	public void AScenarioCivWithOnlyUnitsSurvives() {
		SaveGame save = fixture.saveGame;
		ID civ = save.Players.First(p => !p.human && save.Units.Any(u => u.owner == p.id && IsSettler(save, u))
			&& save.Units.Count(u => u.owner == p.id) >= 2).id;
		string warrior = save.UnitPrototypes.First(p => !p.actions.Contains(UnitAction.BuildCity) && p.categories.Contains("Land") && p.attack > 0).name;
		foreach (SaveUnit unit in save.Units.Where(u => u.owner == civ && IsSettler(save, u))) {
			unit.prototype = warrior;
		}
		C7GameData.GameData gameData = Load(save);
		Player player = gameData.players.First(p => p.id == civ);
		Assert.True(player.neverHadCityOrSettler);

		gameData.DestroyDefeatedCivs();
		Assert.False(player.defeated);

		gameData.RemoveUnit(player.units[0]);
		Assert.False(player.defeated);
		Assert.NotEmpty(player.units);
	}

	// A civ that started with a settler and lost it before founding a city
	// is destroyed, as before.
	[Fact]
	public void ACivThatLostItsSettlersIsDestroyed() {
		C7GameData.GameData gameData = Load(fixture.saveGame);
		Player player = gameData.players.First(p => !p.isBarbarians && !p.isHuman
			&& p.units.Any(u => u.unitType.isSettler) && p.units.Any(u => !u.unitType.isSettler));
		Assert.False(player.neverHadCityOrSettler);

		foreach (MapUnit settler in player.units.Where(u => u.unitType.isSettler).ToList()) {
			gameData.RemoveUnit(settler);
		}

		Assert.True(player.defeated);
	}

	// A civ that has founded a city, and has lost its cities and settlers
	// but kept other units (from before such a civ was destroyed at once),
	// isn't taken for one that started without cities.
	[Fact]
	public void ACivThatFoundedACityIsntTakenForAScenarioCiv() {
		SaveGame save = fixture.saveGame;
		SavePlayer saved = save.Players.First(p => !p.human && save.Units.Any(u => u.owner == p.id && IsSettler(save, u))
			&& save.Units.Count(u => u.owner == p.id) >= 2);
		string warrior = save.UnitPrototypes.First(p => !p.actions.Contains(UnitAction.BuildCity) && p.categories.Contains("Land") && p.attack > 0).name;
		foreach (SaveUnit unit in save.Units.Where(u => u.owner == saved.id && IsSettler(save, u))) {
			unit.prototype = warrior;
		}
		saved.citiesFounded = 1;
		C7GameData.GameData gameData = Load(save);
		Player player = gameData.players.First(p => p.id == saved.id);
		Assert.False(player.neverHadCityOrSettler);

		gameData.DestroyDefeatedCivs();

		Assert.True(player.defeated);
	}
}

using System;
using System.IO;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using QueryCiv3;
using Xunit;

namespace EngineTests.GameData;

// Civ3's debug mode and its editing commands (see C7Engine.DebugMode).
public class DebugModeTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly SaveGameFixture fixture;
	private readonly C7GameData.GameData gameData;
	private readonly Player player;
	private readonly City city;
	private readonly ID previousActivePlayer;

	public DebugModeTest(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		player = gameData.players.First(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler));
		Tile tile = player.units.First(u => u.unitType.isSettler).location;
		city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		player.hasPlayedThisTurn = false;
		previousActivePlayer = EngineStorage.activePlayerID;
		EngineStorage.activePlayerID = player.id;
	}

	public void Dispose() {
		EngineStorage.activePlayerID = previousActivePlayer;
		EngineStorage.uiMessageRouter = null;
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private UnitPrototype Unit(string name) => gameData.unitPrototypes.First(p => p.name == name);

	private Tile EmptyLandTile() {
		return gameData.map.tiles.First(t => t.IsLand() && t.unitsOnTile.Count == 0 && t.cityAtTile == null);
	}

	[Fact]
	public void DebugModeIsOffUnlessTheScenarioTurnsItOn() {
		Assert.False(gameData.debugMode);
		Assert.False(DebugMode.IsOn(gameData));
		gameData.debugMode = true;
		Assert.True(DebugMode.IsOn(gameData));
	}

	[Fact]
	public void DebugModeIsKeptInTheSave() {
		gameData.debugMode = true;
		SaveGame save = SaveGame.FromJSON(SaveGame.FromGameData(gameData).ToCompactJSON());
		Assert.True(save.DebugMode);
		Assert.True(save.ToGameData(fixture.behaviors).debugMode);
		EngineStorage.InitializeGameDataForTests(gameData);
	}

	[Fact]
	public void TheEditorMakesAUnitOfAnyTypeForAnyPlayerAtTheChosenExperience() {
		Player other = gameData.players.First(p => p != player && !p.isBarbarians);
		Tile tile = EmptyLandTile();
		ExperienceLevel elite = gameData.experienceLevels.Last();

		MapUnit unit = DebugMode.CreateUnit(gameData, other, Unit("Knight"), tile, elite);

		Assert.NotNull(unit);
		Assert.Equal(other, unit.owner);
		Assert.Equal(tile, unit.location);
		Assert.Contains(unit, tile.unitsOnTile);
		Assert.Contains(unit, other.units);
		Assert.Equal(elite, unit.experienceLevel);
		Assert.Equal(unit.maxHitPoints, unit.hitPointsRemaining);
	}

	// EDITORUNIT starts at the second experience level, Regular.
	[Fact]
	public void TheEditorStartsAtRegular() {
		Assert.Equal("Regular", DebugMode.DefaultExperience(gameData).displayName);
	}

	[Fact]
	public void NoUnitGoesWhereAnotherPlayersUnitsAre() {
		Player other = gameData.players.First(p => p != player && !p.isBarbarians);
		Tile tile = EmptyLandTile();
		DebugMode.CreateUnit(gameData, other, Unit("Warrior"), tile, null);

		Assert.NotNull(DebugMode.WhyCannotCreateUnit(player, Unit("Warrior"), tile));
		Assert.Null(DebugMode.CreateUnit(gameData, player, Unit("Warrior"), tile, null));
		Assert.Null(DebugMode.WhyCannotCreateUnit(other, Unit("Warrior"), tile));
	}

	[Fact]
	public void DollarAddsAHundredGold() {
		int gold = player.gold;
		DebugMode.AddGold(player);
		Assert.Equal(gold + 100, player.gold);
	}

	[Fact]
	public void SAddsAHundredShieldsUpToTheCost() {
		city.SetItemBeingProduced(gameData.Buildings.First(b => b.name == "Temple"));
		city.SetStoredShields(0);
		int cost = player.ShieldCost(city.itemBeingProduced);
		Assert.True(cost > 0);

		DebugMode.AddShields(city);
		Assert.Equal(Math.Min(100, cost), city.shieldsStored);

		city.SetStoredShields(cost - 1);
		DebugMode.AddShields(city);
		Assert.Equal(cost, city.shieldsStored);
	}

	[Fact]
	public void AAddsACitizenWithoutAnAqueduct() {
		while (city.residents.Count < gameData.rules.MaximumLevel1CitySize) {
			Assert.True(DebugMode.AddCitizen(gameData, city));
		}
		int size = city.residents.Count;

		Assert.True(DebugMode.AddCitizen(gameData, city));

		Assert.Equal(size + 1, city.residents.Count);
		Assert.Equal(player.civilization, city.residents.Last().nationality);
	}

	[Fact]
	public void BAddsAnImprovementOrWonderTheCityLacks() {
		Building temple = gameData.Buildings.First(b => b.name == "Temple");
		Building pyramids = gameData.Buildings.First(b => b.greatWonderProperties != null);
		Assert.Contains(temple, DebugMode.BuildingsToAdd(gameData, city));

		Assert.True(DebugMode.AddBuilding(gameData, city, temple));
		Assert.True(DebugMode.AddBuilding(gameData, city, pyramids));

		Assert.Contains(city.constructed_buildings, cb => cb.building == temple);
		Assert.DoesNotContain(temple, DebugMode.BuildingsToAdd(gameData, city));
		Assert.False(DebugMode.AddBuilding(gameData, city, temple));
		Assert.Contains(pyramids.name, gameData.GreatWondersBuilt);
		Assert.DoesNotContain(DebugMode.BuildingsToAdd(gameData, city), b => b.IsSpaceshipPart);
	}

	[Fact]
	public void TheMessagesAreRefusedOutsideDebugMode() {
		int gold = player.gold;
		Assert.False(new MsgDebugAddGold { playerID = player.id }.process());
		Assert.Equal(gold, player.gold);

		gameData.debugMode = true;
		Assert.True(new MsgDebugAddGold { playerID = player.id }.process());
		Assert.Equal(gold + 100, player.gold);
	}

	// Civ3 turns debug mode off in a multiplayer game.
	[Fact]
	public void TheMessagesAreRefusedInALanGame() {
		gameData.debugMode = true;
		EngineStorage.uiMessageRouter = _ => { };
		Assert.False(DebugMode.IsOn(gameData));

		int size = city.residents.Count;
		Assert.False(new MsgDebugAddCitizen(city) { playerID = player.id }.process());
		Assert.Equal(size, city.residents.Count);
	}

	[Fact]
	public void TheMessagesWorkOnlyInTheSendersCities() {
		gameData.debugMode = true;
		Player other = gameData.players.First(p => p != player && !p.isBarbarians);
		int size = city.residents.Count;

		EngineStorage.activePlayerID = other.id;
		new MsgDebugAddCitizen(city) { playerID = other.id }.process();
		Assert.Equal(size, city.residents.Count);

		EngineStorage.activePlayerID = player.id;
		new MsgDebugAddCitizen(city) { playerID = player.id }.process();
		Assert.Equal(size + 1, city.residents.Count);
	}

	[Fact]
	public void TheEditorMessageMakesTheUnit() {
		gameData.debugMode = true;
		Tile tile = EmptyLandTile();
		ExperienceLevel veteran = gameData.experienceLevels[2];

		Assert.True(new MsgDebugCreateUnit(player, "Horseman", veteran.key, tile) { playerID = player.id }.process());

		MapUnit unit = Assert.Single(tile.unitsOnTile);
		Assert.Equal("Horseman", unit.unitType.name);
		Assert.Equal(veteran, unit.experienceLevel);
	}

	// Civ3's own rules don't turn debug mode on.
	[SkippableFact]
	public void Civ3sScenariosAreNotInDebugMode() {
		Skip.If(Civ3TestData.ShouldSkipCiv3DependentTests(), "No Civ3 install found.");

		BiqData biq = BiqData.LoadFile(PathUtils.defaultBicPath);
		Assert.Equal(0, biq.Game[0].DebugMode);
		string conquests = Path.Join(Civ3Location.GetCiv3Path(), "Conquests");
		SaveGame game = ImportCiv3.ImportBiq(Path.Join(conquests, "Scenarios", "No Civ Traits.biq"), PathUtils.defaultBicPath,
			_ => Path.GetFullPath(Path.Combine(conquests, "Text", "PediaIcons.txt")));
		Assert.False(game.DebugMode);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;

namespace EngineTests.GameData;

// Regression tests for fixes to tiles, the map, IDs and unit prototypes that
// don't need a full game.
public class FixEngineCoreTests {
	private static readonly TerrainType Land = new() { Key = "grassland" };
	private static readonly TerrainType Water = new() { Key = "coast" };

	private static GameMap MakeMap(int width, int height, bool wrapHorizontally, bool wrapVertically = false, TerrainType terrain = null) {
		GameMap map = new() { numTilesWide = width, numTilesTall = height, wrapHorizontally = wrapHorizontally, wrapVertically = wrapVertically };
		for (int i = 0; i < width * height / 2; i++) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y, map = map, baseTerrainType = terrain ?? Land });
		}
		map.computeNeighbors();
		return map;
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public void TileRangesNeverContainTheNoneTile(bool wrapHorizontally, bool wrapVertically) {
		GameMap map = MakeMap(16, 12, wrapHorizontally, wrapVertically);
		Tile topLeft = map.tileAt(0, 0);
		Tile topRight = map.tileAt(14, 0);
		Tile bottomRight = map.tileAt(15, 11);
		foreach (Tile tile in new[] { topLeft, topRight, bottomRight, map.tileAt(8, 6) }) {
			Assert.NotEqual(Tile.NONE, tile);
			for (int rank = 0; rank <= 4; rank++) {
				List<Tile> withinRank = tile.GetTilesWithinRankDistance(rank);
				List<Tile> square = tile.GetTilesWithinTileSquare(rank);
				Assert.DoesNotContain(Tile.NONE, withinRank);
				Assert.DoesNotContain(Tile.NONE, square);
				Assert.Equal(tile, withinRank[0]);
				Assert.Equal(tile, square[0]);
				// What callers do with the tiles must work.
				Assert.All(withinRank, t => Assert.True(tile.RankDistanceTo(t) <= rank));
			}
			Assert.DoesNotContain(Tile.NONE, tile.GetEdgeNeighbors());
			Assert.False(tile.AnyEdgeNeighbor(t => t == Tile.NONE));
		}

		// A corner tile on a map that doesn't wrap vertically has fewer tiles
		// around it than one in the middle.
		if (!wrapVertically) {
			Assert.True(topLeft.GetTilesWithinRankDistance(2).Count < map.tileAt(8, 6).GetTilesWithinRankDistance(2).Count);
		}
	}

	[Fact]
	public void SingleOffMapNeighborIndexIsStillTheNoneTile() {
		GameMap map = MakeMap(16, 12, wrapHorizontally: false);
		// GetTileAtNeighborIndex asks about one place, so NONE still answers
		// "off the map" there.
		Assert.Equal(Tile.NONE, map.tileAt(0, 0).GetTileAtNeighborIndex(1));
	}

	[Fact]
	public void ContinentsDoNotIncludeTheNoneTile() {
		int noneContinent = Tile.NONE.continent;
		GameMap map = MakeMap(10, 8, wrapHorizontally: false);
		map.recomputeContinents();

		// All land, so one continent of exactly the map's tiles.
		Assert.Equal(map.tiles.Count, map.ContinentSize(map.tiles[0].continent));
		Assert.Equal(map.tiles.Count, map.continents.Sum(c => c.Count));
		Assert.DoesNotContain(map.continents, c => c.Contains(Tile.NONE));
		Assert.Equal(noneContinent, Tile.NONE.continent);
	}

	[Fact]
	public void OffMapTilesDoNotMakeLandStrips() {
		// Land everywhere except two water tiles side by side on the top row.
		// Their only shared corner tiles are one land tile to the south and
		// the edge of the map to the north, so this is not a land strip.
		GameMap map = MakeMap(10, 8, wrapHorizontally: false);
		Tile west = map.tileAt(0, 0);
		Tile east = map.tileAt(2, 0);
		west.baseTerrainType = Water;
		east.baseTerrainType = Water;

		Assert.False(GameMap.IsLandStrip(east, west));
		Assert.False(GameMap.IsLandStrip(west, east));

		map.recomputeContinents();
		Assert.Equal(west.continent, east.continent);
		Assert.Equal(2, map.ContinentSize(west.continent));
		Assert.Equal(map.tiles.Count, map.continents.Sum(c => c.Count));

		// A real strip, with land on both corners, still splits the water.
		Tile west2 = map.tileAt(0, 4);
		Tile east2 = map.tileAt(2, 4);
		west2.baseTerrainType = Water;
		east2.baseTerrainType = Water;
		Assert.True(GameMap.IsLandStrip(east2, west2));
	}

	[Fact]
	public void IdsParseTheirOwnStrings() {
		Assert.Equal(ID.None("tile"), ID.FromString("tile-none"));
		Assert.Equal(ID.None("Man-O-War"), ID.FromString("Man-O-War-none"));
		Assert.Equal("tile-none", ID.FromString(ID.None("tile").ToString()).ToString());
		Assert.Equal("Man-O-War-12", ID.FromString("Man-O-War-12").ToString());
	}

	[Theory]
	[InlineData("warrior")]
	[InlineData("warrior-")]
	[InlineData("warrior-x")]
	[InlineData("warrior-99999999999")]
	[InlineData("")]
	[InlineData(null)]
	public void MalformedIdsThrowFormatException(string? str) {
		Assert.Throws<FormatException>(() => ID.FromString(str));
	}

	[Fact]
	public void ToStringOverridesAreUsedThroughObject() {
		TradeOffer offer = new() { gold = 5, partOfPeaceTreaty = true };
		Assert.Equal(offer.ToString(), ((object)offer).ToString());
		Assert.NotEqual(typeof(TradeOffer).FullName, ((object)offer).ToString());
	}

	[Fact]
	public void PrototypeActionsAreCopiedFromTheSave() {
		SaveUnitPrototype save = new() { name = "Catapult" };
		save.actions.Add(UnitAction.Bombard);
		UnitPrototype proto = new(save, []);

		proto.actions.Add(UnitAction.Fortify);
		Assert.DoesNotContain(UnitAction.Fortify, save.actions);
		Assert.Contains(UnitAction.Bombard, proto.actions);
		Assert.NotSame(save.actions, proto.actions);
	}

	private static (C7GameData.GameData, Civilization, City) MakeUpgradeGame() {
		C7GameData.GameData gameData = new() { gameDifficulty = new Difficulty(), rules = new Rules() };
		EngineStorage.InitializeGameDataForTests(gameData);
		Civilization civ = new() { name = "Testers" };
		Player player = new() { civilization = civ, government = new Government() };
		gameData.players.Add(player);
		Tile tile = new(ID.None("tile"));
		City city = new(tile, player, "Testville", ID.None("city"));
		return (gameData, civ, city);
	}

	private static UnitPrototype AddPrototype(C7GameData.GameData gameData, Civilization civ, string name) {
		UnitPrototype p = new() { name = name };
		p.categories.Add("Land");
		p.producibleBy.Add(civ);
		gameData.unitPrototypes.Add(p);
		return p;
	}

	[Fact]
	public async Task CyclicUpgradesDoNotLoopForever() {
		(C7GameData.GameData gameData, Civilization civ, City city) = MakeUpgradeGame();
		UnitPrototype a = AddPrototype(gameData, civ, "A");
		UnitPrototype b = AddPrototype(gameData, civ, "B");
		a.upgradesTo.Add(b);
		b.upgradesTo.Add(a);

		Task<UnitPrototype> task = Task.Run(() => a.GetProducibleUpgrade(city, []));
		Assert.True(await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))) == task, "GetProducibleUpgrade looped");
		Assert.Equal(b, await task);
		Assert.Equal(a, b.GetProducibleUpgrade(city, []));
	}

	[Fact]
	public void NullUpgradeListsAreTolerated() {
		(C7GameData.GameData gameData, Civilization civ, City city) = MakeUpgradeGame();
		UnitPrototype a = AddPrototype(gameData, civ, "A");
		UnitPrototype b = AddPrototype(gameData, civ, "B");
		a.upgradesTo = null;
		b.upgradesTo.Add(a);

		Assert.Null(a.GetProducibleUpgrade(city, []));
		Assert.Equal(a, b.GetProducibleUpgrade(city, []));
	}

	[Fact]
	public void UpgradeCacheSeesChangesAfterCachedCalls() {
		(C7GameData.GameData gameData, Civilization civ, City city) = MakeUpgradeGame();
		UnitPrototype warrior = AddPrototype(gameData, civ, "Warrior");
		UnitPrototype swordsman = AddPrototype(gameData, civ, "Swordsman");
		UnitPrototype medieval = AddPrototype(gameData, civ, "Medieval Infantry");
		warrior.upgradesTo.Add(swordsman);

		// Repeated calls in the same state take the fast path and agree.
		for (int i = 0; i < 3; i++) {
			Assert.Equal(swordsman, warrior.GetProducibleUpgrade(city, []));
			Assert.Null(swordsman.GetProducibleUpgrade(city, []));
		}

		swordsman.upgradesTo.Add(medieval);
		Assert.Equal(medieval, warrior.GetProducibleUpgrade(city, []));
		Assert.Equal(medieval, swordsman.GetProducibleUpgrade(city, []));

		// The best upgrade is the one furthest along, whichever order the
		// candidates come in.
		warrior.upgradesTo = [medieval, swordsman];
		Assert.Equal(medieval, warrior.GetProducibleUpgrade(city, []));

		// Taking a civ off a set the cache already saw is noticed.
		medieval.producibleBy.Remove(civ);
		Assert.Equal(swordsman, warrior.GetProducibleUpgrade(city, []));

		// So is a new prototype list.
		gameData.unitPrototypes = [warrior];
		warrior.upgradesTo = [];
		Assert.Null(warrior.GetProducibleUpgrade(city, []));
	}
}

// Fixes that need a real game: bombardment, forest clearing, terraforms and
// cities on the map edges.
public class FixEngineCoreGameTests : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player us;
	private readonly Player them;

	private readonly SaveGameFixture fixture;

	public FixEngineCoreGameTests(SaveGameFixture fixture) {
		this.fixture = fixture;
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		C7GameData.GameData.rng = new Random(1234);

		Player[] civs = gameData.players.Where(p => !p.isBarbarians && p.units.Any(u => u.unitType.isSettler)).ToArray();
		us = civs[0];
		them = civs[1];
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	private City FoundCity(Player player, Tile tile, int size = 1) {
		City city = CityInteractions.BuildCity(tile, player, player.GetNextCityName());
		while (city.residents.Count < size) {
			city.AddCitizen(new CityResident() {
				city = city,
				citizenType = city.residents[0].citizenType,
				tileWorked = city.residents[0].tileWorked,
			});
		}
		return city;
	}

	private Tile SettlerTile(Player player) {
		return player.units.First(u => u.unitType.isSettler).location;
	}

	private MapUnit Spawn(Player owner, string prototype, Tile tile) {
		gameData.SpawnUnit(owner, gameData.unitPrototypes.Single(p => p.name == prototype), tile);
		return tile.unitsOnTile.Last();
	}

	[Fact]
	public void CitiesInTheTopCornersHaveBorders() {
		GameMap map = gameData.map;
		Tile topLeft = map.tiles.First(t => t.YCoordinate == 0);
		Tile topRight = map.tiles.Last(t => t.YCoordinate == 0);
		foreach (Tile tile in new[] { topLeft, topRight }) {
			tile.baseTerrainType = SettlerTile(us).baseTerrainType;
			City city = FoundCity(us, tile);
			List<Tile> borders = city.GetTilesWithinBorders();
			Assert.Contains(tile, borders);
			Assert.DoesNotContain(Tile.NONE, borders);
		}
	}

	// A city of theirs next to a bombarding unit of ours. The bombarder never
	// misses.
	private (City, MapUnit) SetUpBombard(int size) {
		Tile site = SettlerTile(them);
		City city = FoundCity(them, site, size);
		Tile from = site.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity());
		MapUnit catapult = Spawn(us, "Catapult", from);
		catapult.unitType.bombard = 1_000_000;
		us.DeclareWarOn(them, gameData.turn);
		return (city, catapult);
	}

	private async Task BombardOnce(MapUnit unit, Tile tile) {
		unit.hasAttackedThisTurn = false;
		unit.movementPoints.reset(unit.unitType.movement);
		await unit.Bombard(tile);
	}

	private Building Walls() {
		return gameData.Buildings.First(b => b.providesWalls && b.greatWonderProperties == null && !b.isSmallWonder);
	}

	// A great wonder that gives walls in every city, like the Great Wall
	// in some rulesets.
	private Building GreatWall() {
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder() && b.greatWonderProperties.buildingGainedInEveryCityOnContinent == null);
		wonder.greatWonderProperties.buildingGainedInEveryCity = Walls();
		return wonder;
	}

	[Fact]
	public async Task WallsGrantedByAWonderDoNotStopBombardment() {
		(City city, MapUnit catapult) = SetUpBombard(size: 4);
		Building greatWall = GreatWall();
		city.AddBuilding(greatWall);
		Assert.Contains(city.GetBuildings(), cb => cb.building.providesWalls);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building.providesWalls);

		// With only indestructible walls, the units in the city are hit.
		MapUnit defender = Spawn(them, "Spearman", city.location);
		int hitPoints = defender.hitPointsRemaining;
		await BombardOnce(catapult, city.location);
		Assert.True(defender.hitPointsRemaining < hitPoints);
		Assert.Contains(city.GetBuildings(), cb => cb.building.providesWalls);

		// With no units, the city itself is.
		defender.RemoveFromPlay();
		int buildings = city.constructed_buildings.Count;
		int population = city.residents.Count;
		await BombardOnce(catapult, city.location);
		Assert.True(city.constructed_buildings.Count < buildings || city.residents.Count < population);
		Assert.Contains(city.constructed_buildings, cb => cb.building == greatWall);
	}

	[Fact]
	public async Task BuiltWallsAreBombardedFirstByArtillery() {
		(City city, MapUnit catapult) = SetUpBombard(size: 2);
		Building walls = Walls();
		city.AddBuilding(walls);
		MapUnit defender = Spawn(them, "Spearman", city.location);
		int hitPoints = defender.hitPointsRemaining;

		await BombardOnce(catapult, city.location);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == walls);
		Assert.Equal(hitPoints, defender.hitPointsRemaining);
	}

	// Per the project owner, what a wonder provides can't be bombarded, so
	// walls built in a city the Great Wall also gives walls to stay.
	[Fact]
	public async Task WallsAWonderAlsoProvidesAreNotBombarded() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		Building walls = Walls();
		city.AddBuilding(walls);
		city.AddBuilding(GreatWall());
		Assert.True(city.IsProvidedByWonders(walls));
		// Industrial, so the wonder itself can't be bombarded away either
		// (see BombardmentDestroysGreatWondersBeforeTheIndustrialAge).
		them.eraCivilopediaName = EraUtils.INDUSTRIAL_AGE_CVLPD;

		// Size 1 with nothing else to hit: nothing is destroyed.
		for (int i = 0; i < 20; i++) {
			await BombardOnce(catapult, city.location);
		}
		Assert.Contains(city.constructed_buildings, cb => cb.building == walls);
		Assert.Contains(city.GetBuildings(), cb => cb.building.providesWalls);
	}

	// Ships don't hit walls first: "A coastal fortress is hit first.", then
	// the units, and the walls are like any other building (justanick, C3C
	// 1.22; owner's decision, 2026-10-20, see MapUnit.BombardCityInOrder).
	[Fact]
	public async Task ShipsHitWallsLikeAnyOtherBuilding() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		Building walls = Walls();
		city.AddBuilding(walls);
		MapUnit ship = SpawnBombardingShip(catapult);
		Assert.False(ship.IsLandUnit());

		MapUnit defender = Spawn(them, "Spearman", city.location);
		int hitPoints = defender.hitPointsRemaining;
		await BombardOnce(ship, city.location);
		Assert.True(defender.hitPointsRemaining < hitPoints);
		Assert.Contains(city.constructed_buildings, cb => cb.building == walls);

		// With no units, the walls are the only building it can hit, and
		// the city's only citizen can't be.
		defender.RemoveFromPlay();
		await BombardOnce(ship, city.location);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == walls);
		Assert.Single(city.residents);
	}

	// Per the project owner, bombardment that isn't lethal never kills a
	// unit: units are hit down to their last hit point, and then the city
	// is. Lethal sea bombardment doesn't make it lethal against land units.
	[Fact]
	public async Task NonLethalBombardmentNeverKillsUnits() {
		(City city, MapUnit catapult) = SetUpBombard(size: 3);
		catapult.unitType.isLandBombardmentLethal = false;
		catapult.unitType.isSeaBombardmentLethal = true;
		catapult.unitType.rateOfFire = 10;
		MapUnit defender = Spawn(them, "Spearman", city.location);

		// With its rate of fire, one bombardment takes the defender down to
		// its last hit point, but no further, and leaves the city alone.
		Assert.True(defender.hitPointsRemaining <= catapult.unitType.rateOfFire);
		await BombardOnce(catapult, city.location);
		Assert.Contains(defender, city.location.unitsOnTile);
		Assert.Equal(1, defender.hitPointsRemaining);
		Assert.Equal(3, city.residents.Count);

		// Nothing left to hit but the city: with one shot, a citizen goes
		// (there is no building it can hit).
		catapult.unitType.rateOfFire = 1;
		await BombardOnce(catapult, city.location);
		Assert.Equal(1, defender.hitPointsRemaining);
		Assert.Equal(2, city.residents.Count);

		// Population never drops below 1, however many shots are fired.
		catapult.unitType.rateOfFire = 10;
		for (int i = 0; i < 10; i++) {
			await BombardOnce(catapult, city.location);
		}
		Assert.Single(city.residents);
	}

	// Per the project owner, a bombarder with lethal land bombardment can
	// kill land units, and only then hits the city.
	[Fact]
	public async Task LethalGroundBombardmentKillsGroundUnits() {
		(City city, MapUnit catapult) = SetUpBombard(size: 3);
		catapult.unitType.isLandBombardmentLethal = true;
		catapult.unitType.isSeaBombardmentLethal = false;
		catapult.unitType.rateOfFire = 10;
		MapUnit defender = Spawn(them, "Spearman", city.location);
		Assert.True(defender.hitPointsRemaining <= catapult.unitType.rateOfFire);

		await BombardOnce(catapult, city.location);
		Assert.DoesNotContain(defender, city.location.unitsOnTile);
		Assert.Equal(3, city.residents.Count);

		// With the defender gone, the city is hit.
		catapult.unitType.rateOfFire = 1;
		await BombardOnce(catapult, city.location);
		Assert.Equal(2, city.residents.Count);
	}

	// A bombarding ship of ours next to their city, never missing.
	private MapUnit SpawnBombardingShip(MapUnit catapult) {
		UnitPrototype shipType = gameData.unitPrototypes.First(p => p.IsSeaUnit() && p.bombard > 0);
		shipType.bombard = 1_000_000;
		gameData.SpawnUnit(us, shipType, catapult.location);
		return catapult.location.unitsOnTile.Last();
	}

	// Per the project owner, lethal land bombardment doesn't kill ships;
	// lethal sea bombardment does. Land artillery can't hit ships in a city
	// at all, so a ship bombards them here.
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task OnlyLethalSeaBombardmentKillsShips(bool lethalSea) {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		MapUnit bombarder = SpawnBombardingShip(catapult);
		bombarder.unitType.isLandBombardmentLethal = true;
		bombarder.unitType.isSeaBombardmentLethal = lethalSea;
		bombarder.unitType.rateOfFire = 1_000;
		UnitPrototype shipType = gameData.unitPrototypes.First(p => p.IsSeaUnit() && p.defense > 0 && p != bombarder.unitType);
		gameData.SpawnUnit(them, shipType, city.location);
		MapUnit ship = city.location.unitsOnTile.Last();
		Assert.True(ship.IsCombatUnit());
		Assert.True(bombarder.IsBombardmentLethalAgainst(ship) == lethalSea);

		await BombardOnce(bombarder, city.location);
		if (lethalSea) {
			Assert.DoesNotContain(ship, city.location.unitsOnTile);
		} else {
			Assert.Contains(ship, city.location.unitsOnTile);
			Assert.Equal(1, ship.hitPointsRemaining);
		}
	}

	// Once the owner is industrial its great wonders are immune (see
	// BombardmentDestroysGreatWondersBeforeTheIndustrialAge); small wonders
	// and the palace always are.
	[Fact]
	public async Task BombardmentDoesNotDestroyWonders() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		them.eraCivilopediaName = EraUtils.INDUSTRIAL_AGE_CVLPD;
		Building greatWonder = gameData.Buildings.First(b => b.IsGreatWonder() && !(b.greatWonderProperties.buildingGainedInEveryCity?.providesWalls ?? false));
		Building smallWonder = gameData.Buildings.First(b => b.isSmallWonder && !b.isCenterOfEmpire);
		city.AddBuilding(greatWonder);
		city.AddBuilding(smallWonder);
		Assert.Contains(city.constructed_buildings, cb => cb.building.isCenterOfEmpire);
		int buildings = city.constructed_buildings.Count;

		// Size 1, so population can't be hit either: nothing is destroyed.
		for (int i = 0; i < 20; i++) {
			await BombardOnce(catapult, city.location);
		}
		Assert.Equal(buildings, city.constructed_buildings.Count);
		Assert.Contains(city.constructed_buildings, cb => cb.building == greatWonder);
		Assert.Contains(city.constructed_buildings, cb => cb.building == smallWonder);
	}

	// "If the bombardement occurs against a settlement of a civilization that
	// has reached the industrial age," / "then any world wonder in this
	// settlement is immune against any bombardment." / "Before the target
	// civilization has reached the industrial age this is not the case"
	// (justanick, C3C 1.22,
	// https://forums.civfanatics.com/threads/bombardment-of-settlements-in-c3c-1-22.647207/, #1).
	[Fact]
	public async Task BombardmentDestroysGreatWondersBeforeTheIndustrialAge() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		them.eraCivilopediaName = EraUtils.MIDDLE_AGES_CVLPD;
		Building greatWonder = gameData.Buildings.First(b => b.IsGreatWonder() && b.greatWonderProperties.buildingGainedInEveryCity == null
			&& b.greatWonderProperties.buildingGainedInEveryCityOnContinent == null);
		city.AddBuilding(greatWonder);

		// Size 1, so the wonder is the only thing left to hit.
		await BombardOnce(catapult, city.location);

		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == greatWonder);
	}

	// "If a regular building(meaning neither a coastal fortress nor city
	// walls) is hit and a civil defence is in the settlement," / "then it is
	// the civil defence that is hit." (justanick, #1).
	[Fact]
	public async Task CivilDefenseTakesTheHitsMeantForOtherBuildings() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		Building civilDefense = gameData.Buildings.Single(b => b.name == "Civil Defense");
		Building library = gameData.Buildings.Single(b => b.name == "Library");
		city.AddBuilding(library);
		city.AddBuilding(civilDefense);

		await BombardOnce(catapult, city.location);

		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == civilDefense);
		Assert.Contains(city.constructed_buildings, cb => cb.building == library);

		await BombardOnce(catapult, city.location);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == library);
	}

	// Walls shield only a town: "This happens only if the city is size 6 or
	// smaller." (alexman, https://codehappy.net/apolyton/threads/84569-1.htm,
	// #1). In a city the land bombardment goes on to its units.
	[Fact]
	public async Task WallsDoNotShieldACity() {
		(City city, MapUnit catapult) = SetUpBombard(size: 7);
		Building walls = Walls();
		city.AddBuilding(walls);
		MapUnit defender = Spawn(them, "Spearman", city.location);
		int hitPoints = defender.hitPointsRemaining;

		await WithRateOfFire(catapult, 1, () => BombardOnce(catapult, city.location));

		Assert.Contains(city.constructed_buildings, cb => cb.building == walls);
		Assert.Equal(hitPoints - 1, defender.hitPointsRemaining);
	}

	// "Air units can be destroyed by bombardment." / "There is no such
	// attribute as "lethal air bombardment"." (justanick, #1). A ship
	// without lethal bombardment finishes off a fighter at its last hit
	// point (land artillery can't hit aircraft at all).
	[Fact]
	public async Task AnyBombardmentKillsAircraftInACity() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		MapUnit ship = SpawnBombardingShip(catapult);
		ship.unitType.isLandBombardmentLethal = false;
		ship.unitType.isSeaBombardmentLethal = false;
		MapUnit fighter = Spawn(them, "Fighter", city.location);
		fighter.hitPointsRemaining = 1;

		await WithRateOfFire(ship, 1, () => BombardOnce(ship, city.location));

		Assert.DoesNotContain(fighter, city.location.unitsOnTile);
		Assert.DoesNotContain(fighter, them.units);
	}

	// ---- The order in which each kind of bombarder hits a city ----
	// Conquests 1.22, per justanick's tests (owner's decision, 2026-10-20;
	// see MapUnit.BombardCityInOrder).

	private MapUnit SpawnEnemyShip(City city) {
		UnitPrototype shipType = gameData.unitPrototypes.First(p => p.IsSeaUnit() && p.defense > 0);
		return gameData.SpawnUnit(them, shipType, city.location);
	}

	// One of our bombarders of the prototype next to their city, firing
	// once and never missing, without lethal bombardment.
	private MapUnit SpawnBombarder(string prototype, MapUnit catapult) {
		MapUnit unit = Spawn(us, prototype, catapult.location);
		unit.unitType.bombard = 1_000_000;
		unit.unitType.rateOfFire = 1;
		unit.unitType.isLandBombardmentLethal = false;
		unit.unitType.isSeaBombardmentLethal = false;
		return unit;
	}

	private async Task WithRandom(Random rng, Func<Task> action) {
		Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = rng;
		try {
			await action();
		} finally {
			C7GameData.GameData.rng = original;
		}
	}

	// "Ships and air units cannot be hit with land based artillery."
	[Fact]
	public async Task ArtilleryNeverHitsShipsOrAircraftInACity() {
		(City city, MapUnit catapult) = SetUpBombard(size: 3);
		MapUnit fighter = Spawn(them, "Fighter", city.location);
		MapUnit ship = SpawnEnemyShip(city);
		int fighterHitPoints = fighter.hitPointsRemaining;
		int shipHitPoints = ship.hitPointsRemaining;

		await WithRateOfFire(catapult, 1, () => BombardOnce(catapult, city.location));

		Assert.Equal(fighterHitPoints, fighter.hitPointsRemaining);
		Assert.Equal(shipHitPoints, ship.hitPointsRemaining);
		Assert.Equal(2, city.residents.Count);
	}

	// "A coastal fortress is hit first.", in a city too, then ships, air
	// units, land units, and then citizens or buildings.
	[Fact]
	public async Task ShipsHitACoastalFortressThenShipsAircraftAndGroundUnits() {
		(City city, MapUnit catapult) = SetUpBombard(size: 8);
		MapUnit bombarder = SpawnBombardingShip(catapult);
		bombarder.unitType.rateOfFire = 1;
		bombarder.unitType.isLandBombardmentLethal = false;
		bombarder.unitType.isSeaBombardmentLethal = false;
		Building fortress = gameData.Buildings.Single(b => b.name == "Coastal Fortress");
		city.AddBuilding(fortress);
		MapUnit spearman = Spawn(them, "Spearman", city.location);
		MapUnit fighter = Spawn(them, "Fighter", city.location);
		MapUnit ship = SpawnEnemyShip(city);
		int spearmanHitPoints = spearman.hitPointsRemaining;
		int fighterHitPoints = fighter.hitPointsRemaining;
		int shipHitPoints = ship.hitPointsRemaining;

		await BombardOnce(bombarder, city.location);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == fortress);
		Assert.Equal(shipHitPoints, ship.hitPointsRemaining);
		Assert.Equal(fighterHitPoints, fighter.hitPointsRemaining);

		await BombardOnce(bombarder, city.location);
		Assert.Equal(shipHitPoints - 1, ship.hitPointsRemaining);
		Assert.Equal(fighterHitPoints, fighter.hitPointsRemaining);
		Assert.Equal(spearmanHitPoints, spearman.hitPointsRemaining);

		ship.RemoveFromPlay();
		await BombardOnce(bombarder, city.location);
		Assert.Equal(fighterHitPoints - 1, fighter.hitPointsRemaining);
		Assert.Equal(spearmanHitPoints, spearman.hitPointsRemaining);

		fighter.RemoveFromPlay();
		await BombardOnce(bombarder, city.location);
		Assert.Equal(spearmanHitPoints - 1, spearman.hitPointsRemaining);
		Assert.Equal(8, city.residents.Count);

		spearman.RemoveFromPlay();
		await BombardOnce(bombarder, city.location);
		Assert.Equal(7, city.residents.Count);
	}

	// "Ships are hits first.", then air units, then land units.
	[Fact]
	public async Task CruiseMissilesHitShipsThenAircraftThenGroundUnits() {
		(City city, MapUnit catapult) = SetUpBombard(size: 3);
		MapUnit spearman = Spawn(them, "Spearman", city.location);
		MapUnit fighter = Spawn(them, "Fighter", city.location);
		MapUnit ship = SpawnEnemyShip(city);
		int spearmanHitPoints = spearman.hitPointsRemaining;
		int fighterHitPoints = fighter.hitPointsRemaining;
		int shipHitPoints = ship.hitPointsRemaining;

		MapUnit missile = SpawnBombarder("Cruise Missile", catapult);
		Assert.True(missile.unitType.isCruiseMissile);
		await BombardOnce(missile, city.location);
		Assert.Equal(shipHitPoints - 1, ship.hitPointsRemaining);
		Assert.Equal(fighterHitPoints, fighter.hitPointsRemaining);
		Assert.Equal(spearmanHitPoints, spearman.hitPointsRemaining);

		ship.RemoveFromPlay();
		await BombardOnce(SpawnBombarder("Cruise Missile", catapult), city.location);
		Assert.Equal(fighterHitPoints - 1, fighter.hitPointsRemaining);
		Assert.Equal(spearmanHitPoints, spearman.hitPointsRemaining);

		fighter.RemoveFromPlay();
		await BombardOnce(SpawnBombarder("Cruise Missile", catapult), city.location);
		Assert.Equal(spearmanHitPoints - 1, spearman.hitPointsRemaining);
		Assert.Equal(3, city.residents.Count);
	}

	// "If air units are left in the settlement, then air units or citizens
	// are hit." / "If no air units are left in the settlement, then ships,
	// citizens or buildings are hit.", then land units, citizens or
	// buildings. Which of those is hit is a roll: the lowest picks the unit.
	[Fact]
	public async Task AircraftHitAircraftOrCitizensThenShipsThenGroundUnits() {
		(City city, MapUnit catapult) = SetUpBombard(size: 3);
		Building library = gameData.Buildings.Single(b => b.name == "Library");
		city.AddBuilding(library);
		MapUnit spearman = Spawn(them, "Spearman", city.location);
		MapUnit fighter = Spawn(them, "Fighter", city.location);
		MapUnit ship = SpawnEnemyShip(city);
		int spearmanHitPoints = spearman.hitPointsRemaining;
		int fighterHitPoints = fighter.hitPointsRemaining;
		int shipHitPoints = ship.hitPointsRemaining;
		MapUnit bomber = SpawnBombarder("Bomber", catapult);
		Assert.True(bomber.IsAirUnit());

		await WithRandom(new ScriptedRandom([], 0.0), () => BombardOnce(bomber, city.location));
		Assert.Equal(fighterHitPoints - 1, fighter.hitPointsRemaining);
		Assert.Equal(shipHitPoints, ship.hitPointsRemaining);
		Assert.Equal(3, city.residents.Count);

		// The other choice is the citizens, never the buildings.
		await WithRandom(new ScriptedRandom([], 0.0, highest: true), () => BombardOnce(bomber, city.location));
		Assert.Equal(fighterHitPoints - 1, fighter.hitPointsRemaining);
		Assert.Equal(2, city.residents.Count);
		Assert.Contains(city.constructed_buildings, cb => cb.building == library);

		fighter.RemoveFromPlay();
		await WithRandom(new ScriptedRandom([], 0.0), () => BombardOnce(bomber, city.location));
		Assert.Equal(shipHitPoints - 1, ship.hitPointsRemaining);
		Assert.Equal(spearmanHitPoints, spearman.hitPointsRemaining);

		ship.RemoveFromPlay();
		await WithRandom(new ScriptedRandom([], 0.0), () => BombardOnce(bomber, city.location));
		Assert.Equal(spearmanHitPoints - 1, spearman.hitPointsRemaining);

		// Now the buildings can be hit too, before the land units.
		await WithRandom(new ScriptedRandom([], 0.0, highest: true), () => BombardOnce(bomber, city.location));
		Assert.Equal(spearmanHitPoints - 1, spearman.hitPointsRemaining);
		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == library);
	}

	// ---- Rate of fire against cities, walls and improvements ----

	// "Rate of fire defines how many times a bombarding unit will attack per
	// turn" (Civ3 editor help), and "Each shot is tallied up individually"
	// (https://forums.civfanatics.com/threads/bombard-question.19648). The
	// prototype is shared, so its rate of fire is put back afterwards.
	private async Task WithRateOfFire(MapUnit unit, int rateOfFire, Func<Task> action) {
		int original = unit.unitType.rateOfFire;
		unit.unitType.rateOfFire = rateOfFire;
		try {
			await action();
		} finally {
			unit.unitType.rateOfFire = original;
		}
	}

	private static int BombardableBuildings(City city) {
		return city.constructed_buildings.Count(cb => !cb.building.isCenterOfEmpire && !cb.building.isSmallWonder && !cb.building.IsGreatWonder()
			&& !city.IsProvidedByWonders(cb.building));
	}

	[Fact]
	public async Task EachShotAtACityHitsABuildingOrACitizen() {
		(City city, MapUnit catapult) = SetUpBombard(size: 6);
		foreach (Building b in gameData.Buildings.Where(b => !b.providesWalls && !b.isCenterOfEmpire && !b.isSmallWonder && !b.IsGreatWonder()).Take(3)) {
			city.AddBuilding(b);
		}
		int before = city.residents.Count + BombardableBuildings(city);

		await WithRateOfFire(catapult, 3, () => BombardOnce(catapult, city.location));

		// The bombarder never misses, so each of the three shots destroys
		// something.
		Assert.Equal(before - 3, city.residents.Count + BombardableBuildings(city));
		Assert.Equal(catapult.unitType.movement - 1, catapult.movementPoints.remaining);
	}

	[Fact]
	public async Task ShotsAtACityNeverKillItsLastCitizen() {
		(City city, MapUnit catapult) = SetUpBombard(size: 2);
		Assert.Equal(0, BombardableBuildings(city));

		await WithRateOfFire(catapult, 3, () => BombardOnce(catapult, city.location));

		Assert.Single(city.residents);
	}

	// Each shot at walls is rolled on its own; once they are down, the
	// rest of the bombardment doesn't go on to the city.
	[Fact]
	public async Task ShotsAtWallsStopOnceTheyAreDown() {
		(City city, MapUnit catapult) = SetUpBombard(size: 4);
		Building walls = Walls();
		city.AddBuilding(walls);

		// The first two shots miss, the third hits.
		Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ScriptedRandom([0.9999999, 0.9999999], 0.0);
		try {
			await WithRateOfFire(catapult, 3, () => BombardOnce(catapult, city.location));
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.DoesNotContain(city.constructed_buildings, cb => cb.building == walls);
		Assert.Equal(4, city.residents.Count);
	}

	[Fact]
	public async Task WallsTakeOnlyOneShotWithoutRateOfFire() {
		(City city, MapUnit catapult) = SetUpBombard(size: 4);
		Building walls = Walls();
		city.AddBuilding(walls);

		Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ScriptedRandom([0.9999999, 0.9999999], 0.0);
		try {
			await WithRateOfFire(catapult, 1, () => BombardOnce(catapult, city.location));
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.Contains(city.constructed_buildings, cb => cb.building == walls);
	}

	// The bombarder rolls up to its rate of fire times, and the first roll
	// that hits damages the tile once (Flintlock, reading the 1.22 code; see
	// MapUnit.BombardTileImprovements).
	[Fact]
	public async Task ABombardmentDamagesATileOnceWhateverItsRateOfFire() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		Tile field = catapult.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0);
		TerrainImprovement railroad = gameData.terrainImprovements.Single(i => i.key == Tile.TileOverlays.RAILROAD);
		TerrainImprovement mine = gameData.terrainImprovements.Single(i => i.key == Tile.TileOverlays.MINE);
		Assert.NotNull(railroad.upgradesFrom);
		field.overlays.Add(railroad);
		field.overlays.Add(mine);

		// The first two rolls miss, the third hits, and that is all.
		Random original = C7GameData.GameData.rng;
		C7GameData.GameData.rng = new ScriptedRandom([0.9999999, 0.9999999], 0.0);
		try {
			await WithRateOfFire(catapult, 3, () => BombardOnce(catapult, field));
		} finally {
			C7GameData.GameData.rng = original;
		}

		Assert.Equal(2, field.overlays.GetManMadeImprovements().Count());
	}

	// "the chance to do damage on each roll equals 1 - X/(S + X)", X =
	// "floor((100 + D) * 16 / 100)" (Flintlock, C3X thread post-16584231),
	// with the game's 1024 scale clamped to 1..1023 (C3X,
	// get_tile_bombard_defense_threshold).
	[Theory]
	[InlineData(12, 0, (1024 - 585) / 1024.0)]   // X = 16, 1024 * 16 / 28 = 585
	[InlineData(12, 50, (1024 - 682) / 1024.0)]  // a fortress: X = 24, 1024 * 24 / 36 = 682
	[InlineData(8, 25, (1024 - 731) / 1024.0)]   // X = 20, 1024 * 20 / 28 = 731
	[InlineData(1_000_000, 0, 1023 / 1024.0)]    // never certain
	public void TileBombardOddsFollowTheGame(double strength, int defencePercent, double expected) {
		Assert.Equal(expected, MapUnit.TileBombardHitChance(strength, defencePercent), 9);
	}

	// D is only the tile's terrain and fortress or barricade (Flintlock,
	// post-16583488).
	[Fact]
	public void TileBombardDefenceIsTheTerrainsAndTheFortresss() {
		Tile field = SettlerTile(us).neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity());
		int terrain = (int)Math.Round(field.overlayTerrainType.defenseBonus.amount * 100);
		Assert.Equal(terrain, MapUnit.TileBombardDefencePercent(field));

		TerrainImprovement fortress = gameData.terrainImprovements.Single(i => i.key == Tile.TileOverlays.FORTRESS);
		field.overlays.Add(fortress);
		Assert.Equal(terrain + (int)Math.Round(fortress.defenseBonus.Value.amount * 100), MapUnit.TileBombardDefencePercent(field));
	}

	[Fact]
	public async Task OneShotAtATileHitsOneImprovement() {
		(City city, MapUnit catapult) = SetUpBombard(size: 1);
		Tile field = catapult.location.neighbors.Values.First(t => t != Tile.NONE && t.IsLand() && !t.HasCity() && t.unitsOnTile.Count == 0);
		TerrainImprovement railroad = gameData.terrainImprovements.Single(i => i.key == Tile.TileOverlays.RAILROAD);
		TerrainImprovement mine = gameData.terrainImprovements.Single(i => i.key == Tile.TileOverlays.MINE);
		field.overlays.Add(railroad);
		field.overlays.Add(mine);

		await WithRateOfFire(catapult, 1, () => BombardOnce(catapult, field));

		List<TerrainImprovement> left = field.overlays.GetManMadeImprovements().ToList();
		Assert.True(left.SequenceEqual([railroad.upgradesFrom, mine]) || left.SequenceEqual([mine, railroad.upgradesFrom])
			|| (left.Count == 1 && left[0] == railroad));
	}

	// Returns the given rolls in order, then `rest` forever.
	// With `highest`, Next(min, max) gives the highest value, else the
	// lowest.
	private class ScriptedRandom(double[] rolls, double rest, bool highest = false) : Random {
		private readonly Queue<double> queue = new(rolls);
		protected override double Sample() => queue.Count > 0 ? queue.Dequeue() : rest;
		public override double NextDouble() => Sample();
		public override int Next(int minValue, int maxValue) => highest ? maxValue - 1 : minValue;
		public override int Next() => 0;
	}

	// The shields go to the clearer's cities only, the forest need not be
	// in the city's borders (CivWiki, Wald roden (Civ3); owner's decision,
	// 2026-10-20).
	[Fact]
	public void ForestShieldsGoOnlyToTheClearersCities() {
		// Our city and theirs, with a forest between them that we own.
		City ours = FoundCity(us, SettlerTile(us));
		Tile forest = ours.location.neighbors[TileDirection.EAST];
		Tile theirSite = forest.neighbors[TileDirection.EAST];
		theirSite.baseTerrainType = ours.location.baseTerrainType;
		City theirs = FoundCity(them, theirSite);
		forest.owningCity = ours;

		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");
		foreach (City c in new[] { ours, theirs }) {
			c.SetItemBeingProduced(warrior);
			c.SetStoredShields(0);
		}

		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields();
		Assert.True(ours.shieldsStored > 0 || gameData.rules.ForestValueInShields == 0);
		Assert.Equal(0, theirs.shieldsStored);

		// A city building a wonder gets nothing, and no other civ's city
		// does either.
		ours.SetStoredShields(0);
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder());
		ours.SetItemBeingProduced(wonder);
		ours.SetStoredShields(0);
		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields(us);
		Assert.Equal(0, ours.shieldsStored);
		Assert.Equal(0, theirs.shieldsStored);

		// They clear the forest in our territory: their city gets them,
		// though the forest isn't inside its borders.
		ours.SetItemBeingProduced(warrior);
		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields(them);
		Assert.Equal(0, ours.shieldsStored);
		Assert.True(theirs.shieldsStored > 0 || gameData.rules.ForestValueInShields == 0);
	}

	// The city on the lowest-numbered tile of Civ3's clockwise city-tile
	// spiral around the forest gets the shields, not the city whose
	// territory holds it nor the nearest (CivWiki, Wald roden (Civ3);
	// owner's decision, 2026-10-20). NE is first, W sixth; two tiles out
	// (N of NE) comes after all eight neighbours.
	[Fact]
	public void ForestShieldsGoToTheLowestNumberedTileAroundTheForest() {
		City west = FoundCity(us, SettlerTile(us));
		Tile forest = west.location.neighbors[TileDirection.EAST];
		Tile northeastSite = forest.neighbors[TileDirection.NORTHEAST];
		northeastSite.baseTerrainType = west.location.baseTerrainType;
		City northeast = FoundCity(us, northeastSite);
		forest.owningCity = west;

		Building temple = gameData.Buildings.First(b => b.name == "Temple");
		foreach (City c in new[] { west, northeast }) {
			c.SetItemBeingProduced(temple);
			c.SetStoredShields(0);
		}
		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields(us);
		Assert.Equal(0, west.shieldsStored);
		Assert.True(northeast.shieldsStored > 0 || gameData.rules.ForestValueInShields == 0);

		// With the NE city building the Palace, the W one is next.
		northeast.SetItemBeingProduced(gameData.Buildings.First(b => b.isCenterOfEmpire));
		northeast.SetStoredShields(0);
		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields(us);
		Assert.True(west.shieldsStored > 0 || gameData.rules.ForestValueInShields == 0);
		Assert.Equal(0, northeast.shieldsStored);

		// Spiral numbers 1, 6 and 9 are NE, W, and N of NE.
		TileLocation at = new(forest);
		Tile Spiral(int i) {
			TileLocation l = ImportCiv3.GetTileFromSpiral(at, i);
			return gameData.map.tileAt(l.X, l.Y);
		}
		Assert.Same(northeastSite, Spiral(1));
		Assert.Same(west.location, Spiral(6));
		Assert.Same(northeastSite.neighbors[TileDirection.NORTH], Spiral(9));
	}

	// Per the project owner, chopped shields never go to a wonder: they go
	// to the next city in range that isn't building one, which then can't
	// switch to a wonder until it has completed something.
	[Fact]
	public void ForestShieldsGoToTheNextCityNotBuildingAWonder() {
		City ours = FoundCity(us, SettlerTile(us));
		Tile forest = ours.location.neighbors[TileDirection.EAST];
		Tile secondSite = forest.neighbors[TileDirection.NORTHEAST];
		secondSite.baseTerrainType = ours.location.baseTerrainType;
		City second = FoundCity(us, secondSite);
		forest.owningCity = ours;
		Assert.True(second.location.RankDistanceTo(forest) <= gameData.rules.MaxRankOfWorkableTiles);

		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");
		Building temple = gameData.Buildings.First(b => b.name == "Temple");
		Building wonder = gameData.Buildings.First(b => b.IsGreatWonder());
		ours.SetItemBeingProduced(wonder);
		second.SetItemBeingProduced(temple);
		ours.SetStoredShields(0);
		second.SetStoredShields(0);

		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields(us);
		int awarded = System.Math.Min(gameData.rules.ForestValueInShields, us.ShieldCost(temple));
		Assert.Equal(0, ours.shieldsStored);
		Assert.Equal(awarded, second.shieldsStored);
		Assert.False(ours.receivedForestShields);
		Assert.True(second.receivedForestShields);

		// The city given them can't switch to a wonder, but can to
		// anything else.
		Assert.False(second.CanChangeProduction(wonder));
		Assert.NotNull(second.ProductionChangeWarning(wonder));
		Assert.True(second.CanChangeProduction(warrior));

		// The flag survives a save.
		C7GameData.GameData loaded = SaveGame.FromGameData(gameData).ToGameData(fixture.behaviors);
		Assert.True(loaded.cities.Single(c => c.id == second.id).receivedForestShields);
		Assert.False(loaded.cities.Single(c => c.id == ours.id).receivedForestShields);

		// Completing something lifts it.
		second.SetStoredShields(us.ShieldCost(temple));
		second.HandleCityProduction(gameData);
		Assert.False(second.receivedForestShields);
		Assert.True(second.CanChangeProduction(wonder));

		// With every city in range building a wonder, the shields are lost.
		second.SetItemBeingProduced(wonder);
		second.SetStoredShields(0);
		forest.hasHadForestCleared = false;
		forest.MaybeAwardForestClearingShields(us);
		Assert.Equal(0, ours.shieldsStored);
		Assert.Equal(0, second.shieldsStored);
	}

	// Per the project owner, shields carry over in full when switching,
	// with no penalty between categories, but those beyond the new item's
	// cost are lost, and the picker says how many.
	[Fact]
	public void SwitchingProductionWarnsOfWastedShields() {
		City city = FoundCity(us, SettlerTile(us));
		UnitPrototype warrior = gameData.unitPrototypes.First(p => p.name == "Warrior");
		Building temple = gameData.Buildings.First(b => b.name == "Temple");
		city.SetItemBeingProduced(temple);
		int stored = us.ShieldCost(warrior) + 3;
		city.SetStoredShields(stored);

		Assert.Equal(3, city.ShieldsLostByChangingTo(warrior));
		Assert.Equal("3 shields will be wasted", city.ProductionChangeWarning(warrior));
		Assert.Null(city.ProductionChangeWarning(temple));

		// A building to a unit: everything up to the unit's cost carries
		// over.
		Assert.True(city.ChangeProduction(warrior));
		Assert.Equal(us.ShieldCost(warrior), city.shieldsStored);
		// And back: nothing more is lost.
		Assert.Equal(0, city.ShieldsLostByChangingTo(temple));
		Assert.True(city.ChangeProduction(temple));
		Assert.Equal(us.ShieldCost(warrior), city.shieldsStored);
	}

	[Fact]
	public void ToTerraformSkipsTerraformsWithoutAnImprovement() {
		Assert.Contains(gameData.Terraforms, tf => tf.Improvement == null);
		Assert.Null(TerrainImprovement.ToTerraform("no such improvement"));
		Terraform road = gameData.Terraforms.First(tf => tf.Improvement != null);
		Assert.Same(road, TerrainImprovement.ToTerraform(road.Improvement.key.ToUpperInvariant()));
		Assert.Equal(road.Name, ((object)road).ToString());
	}
}

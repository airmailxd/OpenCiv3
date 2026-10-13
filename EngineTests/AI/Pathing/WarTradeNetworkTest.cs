using System.Collections.Generic;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.AI.Pathing;

// War cutting the trade network (Civilopedia, GCON_Trade): "two cities are
// not connected if the only road/rail that connects them passes through
// enemy territory, or if a harbor is blockaded by enemy naval forces". See
// TradeNetwork.
public class WarTradeNetworkTest {
	private static readonly TerrainImprovement road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	private readonly TerrainType grassland = new() { Key = "grassland" };
	private readonly TerrainType coast = new() { Key = "coast" };

	private readonly Resource iron = new() { Key = "Iron", Name = "Iron", Category = ResourceCategory.STRATEGIC };
	private readonly Building harbor;

	private readonly C7GameData.GameData gameData;
	private GameMap map;
	private readonly Player us, them;

	public WarTradeNetworkTest() {
		gameData = new C7GameData.GameData() { gameDifficulty = new Difficulty() };
		gameData.Resources.Add(iron);
		EngineStorage.InitializeGameDataForTests(gameData);
		SaveBuilding saveHarbor = new() { name = "Harbor" };
		saveHarbor.flags.Add(SaveBuilding.Flag.AllowsWaterTrade);
		harbor = new Building(saveHarbor, gameData);

		us = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1"), government = new Government(), isHuman = true };
		them = new() { civilization = new Civilization("Carthage"), id = ID.FromString("player-2"), government = new Government() };
		gameData.players.Add(us);
		gameData.players.Add(them);
		MakePeace();
	}

	private void MakePeace() {
		us.playerRelationships[them.id] = new PlayerRelationship { multiTurnDeals = { MultiTurnDeal.DEFAULT_PEACE } };
		them.playerRelationships[us.id] = new PlayerRelationship { multiTurnDeals = { MultiTurnDeal.DEFAULT_PEACE } };
	}

	private void DeclareWar() {
		PlayerRelationship.DeclareWar(them, us, sneakAttack: false, refuseContactUntilTurn: 10, currentTurn: 0, honourPacts: false);
	}

	// Land at x <= 1 and x >= 14, the given terrain between.
	private void MakeMap(TerrainType middle) {
		map = new() { numTilesWide = 32, numTilesTall = 4, tiles = new List<Tile>() };
		for (int i = 0; i < 64; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			TerrainType terrain = x <= 1 || x >= 14 ? grassland : middle;
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y, baseTerrainType = terrain });
		}
		map.computeNeighbors();
		gameData.map = map;
		foreach (Tile t in map.tiles) {
			us.tileKnowledge.knownTiles.Add(t);
		}
	}

	private static City MakeCity(Player owner, Tile tile, bool capital) {
		City city = new(tile, owner, owner.civilization.name + owner.cities.Count, ID.None("city")) { capital = capital };
		tile.cityAtTile = city;
		owner.cities.Add(city);
		return city;
	}

	// Iron on a road next to the colony.
	private void IronBy(City colony, Tile tile) {
		tile.overlays.Add(road);
		tile.Resource = iron;
		tile.owningCity = colony;
	}

	private bool CapitalHasIron(City capital) => gameData.GetTradeNetwork().GetResourcesAvailableToCity(us, capital).ContainsKey(iron);

	// Our capital and a colony joined by a road along y = 1, whose middle
	// runs through Carthage's land.
	private (City capital, City colony, List<Tile> theirLand) RoadThroughTheirLand() {
		MakeMap(grassland);
		City capital = MakeCity(us, map.tileAt(1, 1), true);
		City colony = MakeCity(us, map.tileAt(15, 1), false);
		City theirs = MakeCity(them, map.tileAt(8, 2), true);
		// Cities have roads.
		for (int x = 1; x <= 15; x += 2) {
			map.tileAt(x, 1).overlays.Add(road);
		}
		List<Tile> theirLand = new() { map.tileAt(7, 1), map.tileAt(9, 1) };
		foreach (Tile t in theirLand) {
			t.owningCity = theirs;
		}
		IronBy(colony, map.tileAt(17, 1));
		gameData.InvalidateCachedTradeNetwork();
		return (capital, colony, theirLand);
	}

	[Fact]
	public void ARoadThroughTheLandOfACivAtPeaceConnects() {
		(City capital, City colony, _) = RoadThroughTheirLand();
		Assert.True(CapitalHasIron(capital));
		Assert.True(gameData.GetTradeNetwork().ConnectedToCapital(us, colony));
	}

	[Fact]
	public void DeclaringWarCutsARoadThroughTheEnemysLand() {
		(City capital, City colony, _) = RoadThroughTheirLand();
		Assert.True(CapitalHasIron(capital));

		DeclareWar();
		Assert.False(CapitalHasIron(capital));
		Assert.False(gameData.GetTradeNetwork().ConnectedToCapital(us, colony));

		// Peace opens the road again.
		MakePeace();
		Assert.True(CapitalHasIron(capital));
	}

	[Fact]
	public void AtWarARoadAroundTheEnemysLandStillConnects() {
		(City capital, _, _) = RoadThroughTheirLand();
		// A detour along y = 3, outside their land.
		for (int x = 3; x <= 13; x += 2) {
			map.tileAt(x, 3).overlays.Add(road);
		}
		gameData.InvalidateCachedTradeNetwork();
		DeclareWar();
		Assert.True(CapitalHasIron(capital));
	}

	// Our capital on the west island, the colony on the east one, both with
	// harbors on the coast between.
	private (City capital, City colony) HarborsAcrossTheCoast() {
		MakeMap(coast);
		City capital = MakeCity(us, map.tileAt(1, 1), true);
		City colony = MakeCity(us, map.tileAt(14, 0), false);
		IronBy(colony, map.tileAt(15, 1));
		capital.AddBuilding(harbor);
		colony.AddBuilding(harbor);
		gameData.InvalidateCachedTradeNetwork();
		return (capital, colony);
	}

	private MapUnit Ship(Player owner, Tile tile, int attack) {
		MapUnit ship = new(ID.FromString("unit-1")) {
			owner = owner,
			unitType = new UnitPrototype { attack = attack, defense = 1, categories = new HashSet<string> { "Sea" } },
			location = tile,
			hitPointsRemaining = 3,
		};
		owner.units.Add(ship);
		tile.unitsOnTile.Add(ship);
		return ship;
	}

	[Fact]
	public void AnEnemyWarshipOffTheHarborBlockadesIt() {
		(City capital, City colony) = HarborsAcrossTheCoast();
		DeclareWar();
		Assert.True(CapitalHasIron(capital));

		// Next to the colony, its zone of control covers all the colony's
		// coast.
		Ship(them, map.tileAt(12, 0), attack: 2);
		// Ships move without telling the network: the blockade is seen the
		// next turn.
		gameData.turn++;
		Assert.False(CapitalHasIron(capital));
		Assert.False(gameData.GetTradeNetwork().ConnectedToCapital(us, colony));
	}

	[Fact]
	public void AShipOfACivAtPeaceDoesNotBlockade() {
		(City capital, _) = HarborsAcrossTheCoast();
		Ship(them, map.tileAt(12, 0), attack: 2);
		gameData.InvalidateCachedTradeNetwork();
		Assert.True(CapitalHasIron(capital));
	}

	[Fact]
	public void AnEnemyShipThatCannotAttackDoesNotBlockade() {
		(City capital, _) = HarborsAcrossTheCoast();
		DeclareWar();
		Ship(them, map.tileAt(12, 0), attack: 0);
		gameData.InvalidateCachedTradeNetwork();
		Assert.True(CapitalHasIron(capital));
	}

	[Fact]
	public void AnEnemyShipFarFromTheRouteDoesNotBlockade() {
		(City capital, _) = HarborsAcrossTheCoast();
		DeclareWar();
		// The water is four rows tall; a ship at the top of the middle
		// leaves the bottom rows open.
		Ship(them, map.tileAt(8, 0), attack: 2);
		gameData.InvalidateCachedTradeNetwork();
		Assert.True(CapitalHasIron(capital));
	}
}

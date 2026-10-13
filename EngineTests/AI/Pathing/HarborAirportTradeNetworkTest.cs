using System.Collections.Generic;
using C7Engine;
using C7Engine.Pathing;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.AI.Pathing;

// Harbors and airports as links in the trade network (Civilopedia,
// GCON_Trade): harbors connect over the water the civ can trade over (coast,
// sea with Astronomy, ocean with Navigation or Magnetism), airports connect
// to each other.
public class HarborAirportTradeNetworkTest {
	private static readonly TerrainImprovement road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	private readonly TerrainType grassland = new() { Key = "grassland" };
	private readonly TerrainType coast = new() { Key = "coast" };
	private readonly TerrainType sea = new() { Key = "sea" };
	private readonly TerrainType ocean = new() { Key = "ocean" };

	private readonly Resource iron = new() { Key = "Iron", Name = "Iron", Category = ResourceCategory.STRATEGIC };
	private readonly Tech astronomy;
	private readonly Tech navigation;
	private readonly Building harbor;
	private readonly Building airport;

	private C7GameData.GameData gameData;
	private GameMap map;
	private Player us, them;

	public HarborAirportTradeNetworkTest() {
		astronomy = MakeTech("tech-1", "Astronomy", SaveTech.Flag.EnablesTradeOverSea);
		navigation = MakeTech("tech-2", "Navigation", SaveTech.Flag.EnablesTradeOverOcean);
		gameData = new C7GameData.GameData() { gameDifficulty = new Difficulty(), techs = [astronomy, navigation] };
		gameData.Resources.Add(iron);
		EngineStorage.InitializeGameDataForTests(gameData);
		harbor = MakeBuilding("Harbor", SaveBuilding.Flag.AllowsWaterTrade);
		airport = MakeBuilding("Airport", SaveBuilding.Flag.AllowsAirTrade);

		us = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1"), government = new Government(), isHuman = true };
		them = new() { civilization = new Civilization("Greece"), id = ID.FromString("player-2"), government = new Government() };
		gameData.players.Add(us);
		gameData.players.Add(them);
		us.playerRelationships[them.id] = new PlayerRelationship { multiTurnDeals = { MultiTurnDeal.DEFAULT_PEACE } };
		them.playerRelationships[us.id] = new PlayerRelationship { multiTurnDeals = { MultiTurnDeal.DEFAULT_PEACE } };
	}

	private static Tech MakeTech(string id, string name, SaveTech.Flag flag) {
		SaveTech tech = new() { id = ID.FromString(id), Name = name };
		tech.flags.Add(flag);
		return tech.ToTechWithoutPrereqs();
	}

	private Building MakeBuilding(string name, SaveBuilding.Flag flag) {
		SaveBuilding building = new() { name = name };
		building.flags.Add(flag);
		return new Building(building, gameData);
	}

	// Two islands, x <= 1 and x >= 14, with coast next to each and the
	// given water in the middle (6 <= x <= 9).
	private void MakeMap(TerrainType middle) {
		map = new() { numTilesWide = 32, numTilesTall = 4, tiles = new List<Tile>() };
		for (int i = 0; i < 64; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			TerrainType terrain = x <= 1 || x >= 14 ? grassland : x >= 6 && x <= 9 ? middle : coast;
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y, baseTerrainType = terrain });
		}
		map.computeNeighbors();
		gameData.map = map;
	}

	private static City MakeCity(Player owner, Tile tile, bool capital) {
		City city = new(tile, owner, owner.civilization.name + owner.cities.Count, ID.None("city")) { capital = capital };
		tile.cityAtTile = city;
		owner.cities.Add(city);
		return city;
	}

	private void SeeEverything(Player player) {
		foreach (Tile t in map.tiles) {
			player.tileKnowledge.knownTiles.Add(t);
		}
	}

	// Our capital on the west island, and a second city on the east island
	// with iron on a road next to it.
	private (City capital, City colony) TwoIslandEmpire(TerrainType middle) {
		MakeMap(middle);
		City capital = MakeCity(us, map.tileAt(1, 1), true);
		City colony = MakeCity(us, map.tileAt(14, 0), false);
		Tile ironTile = map.tileAt(15, 1);
		ironTile.overlays.Add(road);
		ironTile.Resource = iron;
		ironTile.owningCity = colony;
		SeeEverything(us);
		gameData.InvalidateCachedTradeNetwork();
		return (capital, colony);
	}

	private bool CapitalHasIron(City capital) => gameData.GetTradeNetwork().GetResourcesAvailableToCity(us, capital).ContainsKey(iron);

	[Fact]
	public void HarborsShareAResourceOnlyOnceTheCivCanTradeOverSea() {
		(City capital, City colony) = TwoIslandEmpire(sea);
		Assert.False(CapitalHasIron(capital));

		capital.AddBuilding(harbor);
		colony.AddBuilding(harbor);
		Assert.False(CapitalHasIron(capital));
		Assert.False(gameData.GetTradeNetwork().ConnectedToCapital(us, colony));

		us.knownTechs.Add(astronomy.id);
		Assert.True(CapitalHasIron(capital));
		Assert.True(gameData.GetTradeNetwork().ConnectedToCapital(us, colony));
	}

	[Fact]
	public void OceanNeedsTheOceanTechNotJustTheSeaOne() {
		(City capital, City colony) = TwoIslandEmpire(ocean);
		capital.AddBuilding(harbor);
		colony.AddBuilding(harbor);

		us.knownTechs.Add(astronomy.id);
		Assert.False(CapitalHasIron(capital));

		us.knownTechs.Add(navigation.id);
		Assert.True(CapitalHasIron(capital));
	}

	[Fact]
	public void CoastAloneNeedsNoTech() {
		(City capital, City colony) = TwoIslandEmpire(coast);
		capital.AddBuilding(harbor);
		colony.AddBuilding(harbor);
		Assert.True(CapitalHasIron(capital));
	}

	[Fact]
	public void BothCitiesNeedAHarbor() {
		(City capital, City _) = TwoIslandEmpire(coast);
		capital.AddBuilding(harbor);
		Assert.False(CapitalHasIron(capital));
	}

	[Fact]
	public void TheWaterRouteMustBeVisible() {
		(City capital, City colony) = TwoIslandEmpire(coast);
		capital.AddBuilding(harbor);
		colony.AddBuilding(harbor);
		foreach (Tile t in map.tiles) {
			if (t.XCoordinate == 6 || t.XCoordinate == 7) {
				us.tileKnowledge.knownTiles.Remove(t);
			}
		}
		gameData.InvalidateCachedTradeNetwork();
		Assert.False(CapitalHasIron(capital));
	}

	[Fact]
	public void AirportsConnect() {
		(City capital, City colony) = TwoIslandEmpire(ocean);
		capital.AddBuilding(airport);
		Assert.False(CapitalHasIron(capital));
		colony.AddBuilding(airport);
		Assert.True(CapitalHasIron(capital));
	}

	[Fact]
	public void CapitalsOnDifferentIslandsAreNotConnectedWithoutHarbors() {
		MakeMap(sea);
		City ourCapital = MakeCity(us, map.tileAt(1, 1), true);
		City theirCapital = MakeCity(them, map.tileAt(14, 0), true);
		SeeEverything(us);
		SeeEverything(them);
		us.knownTechs.Add(astronomy.id);
		them.knownTechs.Add(astronomy.id);
		gameData.InvalidateCachedTradeNetwork();
		TradeNetwork network = gameData.GetTradeNetwork();
		Assert.False(network.CapitalsConnected(us, them));

		ourCapital.AddBuilding(harbor);
		Assert.False(network.CapitalsConnected(us, them));
		theirCapital.AddBuilding(harbor);
		Assert.True(network.CapitalsConnected(us, them));
		Assert.True(network.CapitalsConnected(them, us));
	}

	[Fact]
	public void BothCivsMustBeAbleToTradeOverTheWaterBetweenThem() {
		MakeMap(sea);
		MakeCity(us, map.tileAt(1, 1), true).AddBuilding(harbor);
		MakeCity(them, map.tileAt(14, 0), true).AddBuilding(harbor);
		SeeEverything(us);
		SeeEverything(them);
		us.knownTechs.Add(astronomy.id);
		gameData.InvalidateCachedTradeNetwork();
		Assert.False(gameData.GetTradeNetwork().CapitalsConnected(us, them));

		them.knownTechs.Add(astronomy.id);
		Assert.True(gameData.GetTradeNetwork().CapitalsConnected(us, them));
	}

	[Fact]
	public void AirportsConnectCapitalsOfDifferentCivs() {
		MakeMap(ocean);
		MakeCity(us, map.tileAt(1, 1), true).AddBuilding(airport);
		MakeCity(them, map.tileAt(14, 0), true).AddBuilding(airport);
		gameData.InvalidateCachedTradeNetwork();
		Assert.True(gameData.GetTradeNetwork().CapitalsConnected(us, them));
	}

	[Fact]
	public void TheRulesetsHarborAndAirportLinkTheNetwork() {
		using System.Text.Json.JsonDocument ruleset = EngineTests.Utils.JsonUtils.LoadBaseRuleset();
		List<string> water = new(), air = new();
		foreach (System.Text.Json.JsonElement b in ruleset.RootElement.GetProperty("buildings").EnumerateArray()) {
			if (!b.TryGetProperty("flags", out System.Text.Json.JsonElement flags)) {
				continue;
			}
			foreach (System.Text.Json.JsonElement f in flags.EnumerateArray()) {
				if (f.GetString() == "allowsWaterTrade") {
					water.Add(b.GetProperty("name").GetString());
				} else if (f.GetString() == "allowsAirTrade") {
					air.Add(b.GetProperty("name").GetString());
				}
			}
		}
		Assert.Equal(["Harbor"], water);
		Assert.Equal(["Airport"], air);
	}
}

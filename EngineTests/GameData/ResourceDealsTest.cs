using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using Xunit;

namespace EngineTests.GameData;

// Resource deals on a map with roads: imports count for terraforming, and
// deals are cancelled when the exporter can no longer keep them.
public class ResourceDealsTest {
	private static readonly TerrainImprovement road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	private readonly ID uraniumTech = ID.FromString("tech-9");
	private readonly Resource iron = new() { Key = "Iron", Name = "Iron", Category = ResourceCategory.STRATEGIC };
	private readonly Resource uranium;

	private readonly C7GameData.GameData gameData;
	private readonly GameMap map;
	private readonly Player us, them;
	private readonly City ourCapital, theirCapital;
	private readonly Tile ironTile;

	public ResourceDealsTest() {
		uranium = new() { Key = "Uranium", Name = "Uranium", Category = ResourceCategory.STRATEGIC, Prerequisite = uraniumTech };
		gameData = new C7GameData.GameData() { gameDifficulty = new Difficulty() };
		gameData.Resources.AddRange(new[] { iron, uranium });
		EngineStorage.InitializeGameDataForTests(gameData);

		// Two rows of tiles. Only the top row, from x = 0 to x = 8, has roads.
		map = new() { numTilesWide = 20, numTilesTall = 2, tiles = new List<Tile>() };
		for (int i = 0; i < 20; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y });
		}
		map.computeNeighbors();

		us = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1"), government = new Government(), isHuman = true };
		them = new() { civilization = new Civilization("Greece"), id = ID.FromString("player-2"), government = new Government() };
		gameData.players.Add(us);
		gameData.players.Add(them);
		us.playerRelationships[them.id] = new PlayerRelationship { multiTurnDeals = { MultiTurnDeal.DEFAULT_PEACE } };
		them.playerRelationships[us.id] = new PlayerRelationship { multiTurnDeals = { MultiTurnDeal.DEFAULT_PEACE } };

		ourCapital = MakeCapital(us, map.tileAt(0, 0));
		theirCapital = MakeCapital(them, map.tileAt(8, 0));

		// After founding the cities, which clears their tiles.
		for (int x = 0; x <= 8; x += 2) {
			map.tileAt(x, 0).overlays.Add(road);
		}

		// Their iron is on the road next to their capital.
		ironTile = map.tileAt(6, 0);
		ironTile.Resource = iron;
		ironTile.owningCity = theirCapital;
		gameData.InvalidateCachedTradeNetwork();
	}

	private static City MakeCapital(Player owner, Tile tile) {
		City city = new(tile, owner, owner.civilization.name, ID.None("city")) { capital = true };
		tile.cityAtTile = city;
		owner.cities.Add(city);
		return city;
	}

	private void Trade(Player exporter, Player importer, Resource resource, int start = 0) {
		PlayerRelationship.RegisterMultiTurnDeal(exporter, importer, new MultiTurnDeal(DealType.Resource,
			DealSubType.ResourcePerTurn, DealDetails.Outbound, resourcePerTurn: resource.Key, turnStartDeal: start));
	}

	private static bool HasResourceDeal(Player player, Player other) =>
		player.playerRelationships[other.id].multiTurnDeals.Any(d => d.dealSubType == DealSubType.ResourcePerTurn);

	[Fact]
	public void ImportedResourcesCountForTerraformingOnTheCapitalsNetwork() {
		Tile nextToOurCapital = map.tileAt(2, 0);
		Tile offTheNetwork = map.tileAt(1, 1);
		Assert.False(gameData.GetTradeNetwork().HasResourceAccess(gameData, nextToOurCapital, us, iron));

		Trade(them, us, iron);

		Assert.True(gameData.GetTradeNetwork().HasResourceAccess(gameData, nextToOurCapital, us, iron));
		Assert.False(gameData.GetTradeNetwork().HasResourceAccess(gameData, offTheNetwork, us, iron));
	}

	[Fact]
	public void ExportedResourcesDontCountForTerraforming() {
		Tile nextToTheirCapital = map.tileAt(6, 0);
		Assert.True(gameData.GetTradeNetwork().HasResourceAccess(gameData, nextToTheirCapital, them, iron));

		Trade(them, us, iron);

		Assert.False(gameData.GetTradeNetwork().HasResourceAccess(gameData, nextToTheirCapital, them, iron));
	}

	[Fact]
	public void DealsLastWhileTheExporterCanKeepThem() {
		Trade(them, us, iron);

		PlayerRelationship.CheckForObsoleteDeals(us, gameData.players, 1);

		Assert.True(HasResourceDeal(us, them));
		Assert.True(HasResourceDeal(them, us));
	}

	[Fact]
	public void CuttingTheRoadBetweenCapitalsCancelsTheDeal() {
		Trade(them, us, iron);

		map.tileAt(4, 0).overlays.Remove(road);
		PlayerRelationship.CheckForObsoleteDeals(us, gameData.players, 1);

		Assert.False(HasResourceDeal(us, them));
		Assert.False(HasResourceDeal(them, us));
		Assert.Empty(ourCapital.GetStrategicResources(gameData));
	}

	[Fact]
	public void LosingTheResourceCancelsTheDeal() {
		Trade(them, us, iron);

		// The iron tile goes to another city, as when it is captured.
		ironTile.owningCity = null;
		gameData.InvalidateCachedTradeNetwork();
		PlayerRelationship.CheckForObsoleteDeals(them, gameData.players, 1);

		Assert.False(HasResourceDeal(us, them));
		Assert.False(HasResourceDeal(them, us));
	}

	[Fact]
	public void AnExporterShortOfTheResourceLosesItsNewestDeal() {
		Trade(them, us, iron, start: 0);
		Trade(them, us, iron, start: 5);

		PlayerRelationship.CheckForObsoleteDeals(us, gameData.players, 6);

		List<MultiTurnDeal> ours = us.playerRelationships[them.id].multiTurnDeals
			.Where(d => d.dealSubType == DealSubType.ResourcePerTurn).ToList();
		Assert.Single(ours);
		Assert.Equal(0, ours[0].turnStartDeal);
		Assert.Single(them.playerRelationships[us.id].multiTurnDeals, d => d.dealSubType == DealSubType.ResourcePerTurn);
	}

	[Fact]
	public void ExpiredDealsEndForBothSidesAtOnce() {
		Trade(them, us, iron, start: 0);

		// The deal runs for 20 turns. Only the exporter's turn has started.
		PlayerRelationship.CheckForObsoleteDeals(them, gameData.players, 20);

		Assert.False(HasResourceDeal(them, us));
		Assert.False(HasResourceDeal(us, them));
	}

	[Fact]
	public void SpareResourcesOnlyShowThoseTheViewerKnowsAbout() {
		// They have two Uranium, which only they know about.
		them.knownTechs.Add(uraniumTech);
		foreach (int x in new[] { 2, 4 }) {
			map.tileAt(x, 0).Resource = uranium;
			map.tileAt(x, 0).owningCity = theirCapital;
		}
		gameData.InvalidateCachedTradeNetwork();

		Assert.Contains(TradeReport.ExcessResources(gameData, them, them), r => r.resource == uranium);
		Assert.DoesNotContain(TradeReport.ExcessResources(gameData, them, us), r => r.resource == uranium);

		us.knownTechs.Add(uraniumTech);
		Assert.Contains(TradeReport.ExcessResources(gameData, them, us), r => r.resource == uranium);
	}

	[Fact]
	public void CapitalResourcesCountImports() {
		Assert.Empty(TradeReport.CapitalResources(gameData, us));

		Trade(them, us, iron);

		Assert.Equal(new[] { iron }, TradeReport.CapitalResources(gameData, us).Keys);
	}
}

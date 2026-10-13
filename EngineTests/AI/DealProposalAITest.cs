using System.Collections.Generic;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using Xunit;

namespace EngineTests.AI;

// The resource deals and pacts the AI proposes (see DealProposalAI), on a
// map like GoldPerTurnAndResourceDealsTest's: our capital in the west,
// theirs in the east, joined by a road, with two wines by theirs.
public class DealProposalAITest {
	private static readonly TerrainImprovement road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	private readonly Resource wines = new() { Key = "Wines", Name = "Wines", Category = ResourceCategory.LUXURY };

	private readonly C7GameData.GameData gameData;
	private readonly GameMap map;
	private readonly Player us, them, third;
	private readonly City theirCapital;

	public DealProposalAITest() {
		gameData = new C7GameData.GameData() { gameDifficulty = new Difficulty(), rules = new Rules() { DefaultDealDuration = 20 } };
		gameData.Resources.Add(wines);
		EngineStorage.InitializeGameDataForTests(gameData);

		map = new() { numTilesWide = 20, numTilesTall = 2, tiles = new List<Tile>() };
		for (int i = 0; i < 20; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y });
		}
		map.computeNeighbors();
		gameData.map = map;

		us = MakeCiv("player-1", "Rome");
		them = MakeCiv("player-2", "Greece");
		third = MakeCiv("player-3", "Egypt");
		us.EnsureRelationshipExists(them);
		us.EnsureRelationshipExists(third);
		them.EnsureRelationshipExists(third);

		MakeCapital(us, map.tileAt(0, 0));
		theirCapital = MakeCapital(them, map.tileAt(8, 0));
		MakeCapital(third, map.tileAt(18, 0));
		for (int x = 0; x <= 8; x += 2) {
			map.tileAt(x, 0).overlays.Add(road);
		}
		foreach (Tile t in new[] { map.tileAt(6, 0), map.tileAt(4, 0) }) {
			t.Resource = wines;
			t.owningCity = theirCapital;
		}
		us.gold = 500;
		them.gold = 500;
		gameData.InvalidateCachedTradeNetwork();
	}

	private Player MakeCiv(string id, string name) {
		Player p = new() { civilization = new Civilization(name), id = ID.FromString(id), government = new Government(), rules = gameData.rules };
		gameData.players.Add(p);
		return p;
	}

	private static City MakeCapital(Player owner, Tile tile) {
		City city = new(tile, owner, owner.civilization.name, ID.None("city")) { capital = true };
		tile.cityAtTile = city;
		owner.cities.Add(city);
		return city;
	}

	[Fact]
	public void TheAIBuysALuxuryItLacksAtTheSellersPrice() {
		(TradeOffer weGive, TradeOffer weWant)? deal = DealProposalAI.ResourceDeal(gameData, us, them);

		Assert.NotNull(deal);
		Assert.Equal([wines.Key], deal.Value.weWant.resources);
		int price = TradeOffer.ResourceWorth(gameData, them, wines, given: true);
		Assert.True(deal.Value.weGive.goldPerTurn == price || deal.Value.weGive.gold == price * 20);
		// And the seller would take it.
		Assert.True(them.WouldAcceptDealFrom(gameData, us, deal.Value.weGive, deal.Value.weWant));
	}

	[Fact]
	public void TheAISellsASpareLuxury() {
		(TradeOffer weGive, TradeOffer weWant)? deal = DealProposalAI.ResourceDeal(gameData, them, us);

		Assert.NotNull(deal);
		Assert.Equal([wines.Key], deal.Value.weGive.resources);
		Assert.True(deal.Value.weWant.goldPerTurn >= TradeOffer.ResourceWorth(gameData, them, wines, given: true));
		Assert.True(deal.Value.weWant.goldPerTurn <= TradeOffer.ResourceWorth(gameData, us, wines, given: false));
		Assert.True(us.WouldAcceptDealFrom(gameData, them, deal.Value.weGive, deal.Value.weWant));
	}

	[Fact]
	public void NoResourceDealsWithACivThatBrokeOne() {
		us.playerRelationships[them.id].brokeTradeDeal = true;
		Assert.Null(DealProposalAI.ResourceDeal(gameData, us, them));
	}

	[Fact]
	public void NoResourceDealsWithAnEmbargoedCiv() {
		PlayerRelationship.RegisterMultiTurnDeal(us, third, new MultiTurnDeal(DealType.Embargo, DealSubType.TradeEmbargo,
			DealDetails.Outbound, turnStartDeal: gameData.turn, againstPlayer: them.id));
		Assert.True(Pacts.Embargoes(us, them));
		Assert.Null(DealProposalAI.ResourceDeal(gameData, us, them));
	}

	[Fact]
	public void TheAIProposesAPactOnlyToACivItIsGraciousToward() {
		foreach (Player a in gameData.players) {
			foreach (PlayerRelationship r in a.playerRelationships.Values) {
				r.hasEmbassy = true;
			}
		}
		for (int i = 0; i < 50; ++i) {
			Assert.Null(DealProposalAI.PactDeal(gameData, us, them));
		}

		Attitudes.RecordGift(us.playerRelationships[them.id], 10000, gameData.turn);
		Assert.Equal(AttitudeLevel.Gracious, Attitudes.Level(gameData, us, them));
		(TradeOffer weGive, TradeOffer weWant)? deal = null;
		for (int i = 0; i < 200 && deal == null; ++i) {
			deal = DealProposalAI.PactDeal(gameData, us, them);
		}
		Assert.NotNull(deal);
		Assert.True(TradeOffer.IncludesMutualProtectionPact(deal.Value.weGive, deal.Value.weWant));

		// Not while either is at war.
		them.DeclareWarOn(third, gameData.turn);
		for (int i = 0; i < 50; ++i) {
			Assert.Null(DealProposalAI.PactDeal(gameData, us, them));
		}
	}
}

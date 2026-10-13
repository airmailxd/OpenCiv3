using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using Xunit;

namespace EngineTests.AI;

// The resource deals and pacts the AI proposes (see DealProposalAI), on a
// map like GoldPerTurnAndResourceDealsTest's: our capital in the west,
// theirs in the east, joined by a road, with two wines by theirs.
public class DealProposalAITest : System.IDisposable {
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

	public void Dispose() {
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
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

	private void GiveEmbassies() {
		foreach (Player a in gameData.players) {
			foreach (PlayerRelationship r in a.playerRelationships.Values) {
				r.hasEmbassy = true;
			}
		}
	}

	// Calls propose until it makes a deal (it only does some of the time).
	private static (TradeOffer weGive, TradeOffer weWant)? Eventually(System.Func<(TradeOffer, TradeOffer)?> propose) {
		for (int i = 0; i < 200; ++i) {
			if (propose() is (TradeOffer, TradeOffer) deal) {
				return deal;
			}
		}
		return null;
	}

	[Fact]
	public void AtWarTheAIPaysACivAnnoyedWithTheEnemyToJoin() {
		GiveEmbassies();
		us.gold = 500;
		// Third declared war on them once, which they remember.
		them.playerRelationships[third.id].warDeclarationCount = 1;
		Assert.True(Attitudes.Level(gameData, them, third) is AttitudeLevel.Annoyed or AttitudeLevel.Furious);
		// Not at war, no alliance.
		Assert.Null(Eventually(() => DealProposalAI.AllianceDeal(gameData, us, them)));

		us.DeclareWarOn(third, gameData.turn);
		(TradeOffer weGive, TradeOffer weWant)? deal = Eventually(() => DealProposalAI.AllianceDeal(gameData, us, them));
		Assert.NotNull(deal);
		Assert.Equal(third.id, TradeOffer.AllianceTarget(deal.Value.weGive, deal.Value.weWant));
		Assert.Equal(PactAI.JoinWarGold + PactAI.JoinWarGoldPerCity * third.cities.Count, deal.Value.weGive.gold);
		Assert.Contains("Join our crusade", DealProposalAI.Greeting(gameData, deal.Value.weGive, deal.Value.weWant));
		Assert.True(them.WouldAcceptDealFrom(gameData, us, deal.Value.weGive, deal.Value.weWant));
		// The same civ isn't asked again soon.
		Assert.Null(Eventually(() => DealProposalAI.AllianceDeal(gameData, us, them)));

		Assert.True(us.ExecuteDeal(gameData, them, deal.Value.weWant, deal.Value.weGive));
		Assert.True(PlayerRelationship.AtWar(them, third));
	}

	[Fact]
	public void NoAllianceFromACivThatBrokeADeal() {
		GiveEmbassies();
		us.gold = 500;
		them.playerRelationships[third.id].warDeclarationCount = 1;
		us.DeclareWarOn(third, gameData.turn);
		us.playerRelationships[them.id].brokeTradeDeal = true;
		Assert.Null(Eventually(() => DealProposalAI.AllianceDeal(gameData, us, them)));
	}

	[Fact]
	public void TheAIAsksForAnEmbargoAgainstACivItIsFuriousWith() {
		GiveEmbassies();
		// We have reason to be furious with third, and they to be annoyed.
		us.playerRelationships[third.id].warDeclarationCount = 3;
		them.playerRelationships[third.id].warDeclarationCount = 1;
		Assert.Equal(AttitudeLevel.Furious, Attitudes.Level(gameData, us, third));

		(TradeOffer weGive, TradeOffer weWant)? deal = Eventually(() => DealProposalAI.EmbargoDeal(gameData, us, them));
		Assert.NotNull(deal);
		Assert.Equal(third.id, TradeOffer.EmbargoTarget(deal.Value.weGive, deal.Value.weWant));
		Assert.Contains("trade embargo", DealProposalAI.Greeting(gameData, deal.Value.weGive, deal.Value.weWant));
		Assert.True(them.WouldAcceptDealFrom(gameData, us, deal.Value.weGive, deal.Value.weWant));
	}

	[Fact]
	public void NoEmbargoAgainstACivWeDontMind() {
		GiveEmbassies();
		them.playerRelationships[third.id].warDeclarationCount = 1;
		Assert.Null(Eventually(() => DealProposalAI.EmbargoDeal(gameData, us, them)));
	}

	// They send us wines for a gold a turn, from turn 0.
	private void BuyWines() {
		Assert.True(them.ExecuteDeal(gameData, us, new TradeOffer { goldPerTurn = 1 }, new TradeOffer { resources = { wines.Key } }));
	}

	private bool HasWinesDeal(Player a, Player b) =>
		a.playerRelationships[b.id].multiTurnDeals.Any(d => d.dealSubType == DealSubType.LuxuryPerTurn);

	[Fact]
	public async Task AnExpiredDealIsRenewedBetweenAIsThatStillWantIt() {
		BuyWines();
		gameData.turn = 20;
		PlayerRelationship.CheckForObsoleteDeals(us, gameData.players, gameData.turn);
		Assert.False(HasWinesDeal(us, them));

		await PlayerAI.RenewExpiredDeals(us, gameData);

		Assert.True(HasWinesDeal(us, them));
		MultiTurnDeal renewed = us.playerRelationships[them.id].multiTurnDeals.Single(d => d.dealSubType == DealSubType.LuxuryPerTurn);
		Assert.Equal(20, renewed.TurnsRemaining(gameData.turn));
		Assert.Contains(us.playerRelationships[them.id].multiTurnDeals, d => d.dealSubType == DealSubType.GoldPerTurn && d.goldPerTurn == 1);
		// Only once.
		Assert.Empty(DealRenewal.TakeFor(gameData, them));
	}

	[Fact]
	public void AHumanIsToldAndOfferedTheOldTerms() {
		us.isHuman = true;
		BuyWines();
		gameData.turn = 20;
		PlayerRelationship.CheckForObsoleteDeals(us, gameData.players, gameData.turn);

		Assert.Contains(EngineStorage.messagesToUI,
			m => m is MsgShowTemporaryPopup p && p.recipient == us && p.message.Contains("expired"));
		// On their turn, the AI offers the same terms.
		var renewal = Assert.Single(DealRenewal.TakeFor(gameData, them));
		Assert.Equal(us, renewal.other);
		Assert.Equal([wines.Key], renewal.aiGives.resources);
		Assert.Equal(1, renewal.otherGives.goldPerTurn);
		Assert.True(them.WouldAcceptDealFrom(gameData, us, renewal.otherGives, renewal.aiGives));
	}

	[Fact]
	public async Task AnAIThatNoLongerWantsTheDealDoesNotRenewIt() {
		BuyWines();
		gameData.turn = 20;
		// We have wines of our own now, so don't want theirs.
		Tile ours = map.tileAt(2, 0);
		ours.Resource = wines;
		ours.owningCity = us.cities[0];
		gameData.InvalidateCachedTradeNetwork();
		PlayerRelationship.CheckForObsoleteDeals(us, gameData.players, gameData.turn);

		await PlayerAI.RenewExpiredDeals(us, gameData);

		Assert.False(HasWinesDeal(us, them));
	}
}

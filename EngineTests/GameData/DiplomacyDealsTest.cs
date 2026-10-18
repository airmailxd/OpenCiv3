using System;
using System.Linq;
using C7Engine;
using C7GameData;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.GameData;

// Deals made over time: trading reputation, gold per turn, resources, and
// the pacts (see PlayerRelationship and TradeOffer).
public class DiplomacyDealsTest : IDisposable {
	private readonly C7GameData.GameData gameData = new() { rules = new Rules() { DefaultDealDuration = 20 } };
	private readonly Player us = MakeCiv("player-1");
	private readonly Player victim = MakeCiv("player-2");
	private readonly Player third = MakeCiv("player-3");

	public DiplomacyDealsTest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.players.Add(us);
		gameData.players.Add(victim);
		gameData.players.Add(third);
		foreach (Player a in gameData.players) {
			foreach (Player b in gameData.players) {
				if (a != b) {
					a.EnsureRelationshipExists(b);
				}
			}
		}
		us.isHuman = true;
		us.gold = 1000;
	}

	public void Dispose() {
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(id), government = new Government() };
	}

	private void PayEachTurn(Player payer, Player payee, int gold) {
		RegisterMultiTurnDeal(payer, payee, new MultiTurnDeal(DealType.Gold, DealSubType.GoldPerTurn,
			DealDetails.Outbound, goldPerTurn: gold, turnStartDeal: gameData.turn));
	}

	private bool AcceptsGoldPerTurnFromUs(Player ai) {
		return ai.WouldAcceptDealFrom(gameData, us, new TradeOffer { goldPerTurn = 1 }, new TradeOffer());
	}

	[Fact]
	public void BreakingAGoldPerTurnDealByWarRuinsOurReputationWithOtherAIs() {
		PayEachTurn(us, victim, 5);
		Assert.True(AcceptsGoldPerTurnFromUs(third));

		DeclareWar(us, victim, false, gameData.turn + 10);

		Assert.True(victim.playerRelationships[us.id].brokeTradeDeal);
		Assert.True(TradingReputationBroken(victim, us, gameData.players));
		Assert.False(AcceptsGoldPerTurnFromUs(third));
		// Other deals are still fine.
		Assert.True(third.WouldAcceptDealFrom(gameData, us, new TradeOffer { gold = 10 }, new TradeOffer()));
	}

	[Fact]
	public void AnAIAtWarWithTheVictimDoesNotHearOfIt() {
		PayEachTurn(us, victim, 5);
		DeclareWar(third, victim, false, gameData.turn + 10);
		DeclareWar(us, victim, false, gameData.turn + 10);

		Assert.True(AcceptsGoldPerTurnFromUs(third));
	}

	[Fact]
	public void BeingAttackedDoesNotRuinOurReputation() {
		PayEachTurn(us, victim, 5);

		DeclareWar(victim, us, false, gameData.turn + 10);

		Assert.False(TradingReputationBroken(third, us, gameData.players));
		Assert.False(TradingReputationBroken(victim, us, gameData.players));
		Assert.True(AcceptsGoldPerTurnFromUs(third));
	}

	private void GiveEmbassies() {
		foreach (Player a in gameData.players) {
			foreach (ID b in a.playerRelationships.Keys) {
				a.playerRelationships[b].hasEmbassy = true;
			}
		}
	}

	private static TradeOffer Pact() => new() { mutualProtectionPact = true };

	[Fact]
	public void AMutualProtectionPactCallsThePartnerToWar() {
		GiveEmbassies();
		Assert.True(third.ExecuteDeal(gameData, victim, Pact(), Pact()));
		Assert.True(Pacts.HasDeal(third, victim, DealSubType.MutualProtectionPact));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, third, victim, Pact(), Pact()));

		us.DeclareWarOn(victim, gameData.turn);

		Assert.True(AtWar(third, us));
		Assert.True(third.playerRelationships[us.id].otherStartedCurrentWar == false);
		// The pact holds.
		Assert.True(Pacts.HasDeal(third, victim, DealSubType.MutualProtectionPact));
	}

	[Fact]
	public void AMutualProtectionPactDoesNotFollowItsPartnerIntoWar() {
		GiveEmbassies();
		Assert.True(us.ExecuteDeal(gameData, third, Pact(), Pact()));

		us.DeclareWarOn(victim, gameData.turn);

		Assert.False(AtWar(third, victim));
	}

	[Fact]
	public void PactsNeedAnEmbassyAndTheirTech() {
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, third, Pact(), Pact()));
		GiveEmbassies();
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, us, third, Pact(), Pact()));

		Tech nationalism = new() { id = ID.FromString("tech-1"), Name = "Nationalism" };
		nationalism.Flags.Add(C7GameData.Save.SaveTech.Flag.EnablesMutualProtection);
		gameData.techs.Add(nationalism);
		us.knownTechs.Add(nationalism.id);
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, third, Pact(), Pact()));
		third.knownTechs.Add(nationalism.id);
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, us, third, Pact(), Pact()));
	}

	[Fact]
	public void AMilitaryAllianceBringsTheAllyIntoTheWar() {
		GiveEmbassies();
		TradeOffer alliance() => new() { militaryAllianceAgainst = victim.id };
		// Neither is at war with the victim yet.
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, third, alliance(), alliance()));

		us.DeclareWarOn(victim, gameData.turn);
		Assert.True(us.ExecuteDeal(gameData, third, alliance(), alliance()));

		Assert.True(AtWar(third, victim));
		Assert.True(Pacts.HasDeal(us, third, DealSubType.MilitaryAlliance, victim.id));
		Assert.Contains(ActiveDeals.Between(gameData, us, third).agreements, a => a.StartsWith("Military Alliance against"));

		// Making peace with the enemy breaks the alliance, and the ally
		// remembers.
		SignPeaceAfterWar(third, victim, gameData);
		Assert.False(Pacts.HasDeal(us, third, DealSubType.MilitaryAlliance));
		Assert.False(Pacts.HasDeal(third, us, DealSubType.MilitaryAlliance));
		Assert.True(us.playerRelationships[third.id].brokeTradeDeal);
	}

	[Fact]
	public void TheAIJoinsAWarItIsAlreadyFighting() {
		GiveEmbassies();
		TradeOffer alliance() => new() { militaryAllianceAgainst = victim.id };
		us.DeclareWarOn(victim, gameData.turn);
		// Not at war with the victim, and not annoyed with it.
		Assert.False(third.WouldAcceptDealFrom(gameData, us, alliance(), alliance()));

		third.DeclareWarOn(victim, gameData.turn);
		Assert.True(third.WouldAcceptDealFrom(gameData, us, alliance(), alliance()));

		// But not with a civ that broke a deal.
		third.playerRelationships[us.id].brokeTradeDeal = true;
		Assert.False(third.WouldAcceptDealFrom(gameData, us, alliance(), alliance()));
	}

	[Fact]
	public void TheAIOnlySignsAPactWithACivItLikes() {
		GiveEmbassies();
		Assert.Equal(AttitudeLevel.Polite, Attitudes.Level(gameData, third, us));
		Assert.True(third.WouldAcceptDealFrom(gameData, us, Pact(), Pact()));

		// Not with a civ at war.
		us.DeclareWarOn(victim, gameData.turn);
		Assert.False(third.WouldAcceptDealFrom(gameData, us, Pact(), Pact()));
	}

	[Fact]
	public void AnEmbargoStopsTradeWithItsTarget() {
		GiveEmbassies();
		third.gold = 1000;
		TradeOffer embargo() => new() { tradeEmbargoAgainst = victim.id };
		PayEachTurn(third, victim, 1);
		// Not while one of them still trades with it.
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, third, embargo(), embargo()));
		third.playerRelationships[victim.id].multiTurnDeals.RemoveAll(d => d.dealSubType == DealSubType.GoldPerTurn);
		victim.playerRelationships[third.id].multiTurnDeals.RemoveAll(d => d.dealSubType == DealSubType.GoldPerTurn);

		Assert.True(us.ExecuteDeal(gameData, third, embargo(), embargo()));
		Assert.True(Pacts.Embargoes(third, victim));

		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, third, victim, new TradeOffer { goldPerTurn = 5 }, new TradeOffer()));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, victim, third, new TradeOffer(), new TradeOffer { goldPerTurn = 5 }));
		// Other trade goes on.
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, third, victim, new TradeOffer { gold = 5 }, new TradeOffer()));
		// Both parties are bound.
		victim.gold = 1000;
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, victim, us, new TradeOffer { goldPerTurn = 5 }, new TradeOffer()));
	}

	[Fact]
	public void WarWithoutAPerTurnDealDoesNotRuinOurReputation() {
		// The other civ paying us isn't our deal to keep.
		PayEachTurn(victim, us, 5);

		DeclareWar(us, victim, false, gameData.turn + 10);

		Assert.False(TradingReputationBroken(third, us, gameData.players));
	}
}

// Trading reputation is kept in saves.
public class DiplomacyDealsSaveTest : IClassFixture<EngineTests.Utils.SaveGameFixture> {
	private readonly EngineTests.Utils.SaveGameFixture fixture;

	public DiplomacyDealsSaveTest(EngineTests.Utils.SaveGameFixture fixture) {
		this.fixture = fixture;
	}

	[Fact]
	public void BrokenDealsAreRememberedAfterLoading() {
		C7GameData.GameData gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		Player[] civs = gameData.players.Where(p => !p.isBarbarians).Take(3).ToArray();
		civs[0].EnsureRelationshipExists(civs[1]);
		civs[0].EnsureRelationshipExists(civs[2]);
		Player player = civs[0];
		ID otherId = civs[1].id;
		player.playerRelationships[otherId].brokeTradeDeal = true;

		byte[] json = C7GameData.Save.SaveGame.FromGameData(gameData).ToCompactJSON();
		C7GameData.GameData loaded = C7GameData.Save.SaveGame.FromJSON(json).ToGameData(fixture.behaviors);

		Player again = loaded.GetPlayer(player.id);
		Assert.True(again.playerRelationships[otherId].brokeTradeDeal);
		Assert.All(again.playerRelationships.Where(r => r.Key != otherId), r => Assert.False(r.Value.brokeTradeDeal));
	}
}

// Negotiated gold per turn and resources, on a map with roads like
// ResourceDealsTest's: our capital in the west, theirs in the east, joined
// by a road along the top row.
public class GoldPerTurnAndResourceDealsTest {
	private static readonly TerrainImprovement road = new("road", TerrainImprovement.Layer.Roads, movementCost: 1.0f / 3);

	private readonly Resource wines = new() { Key = "Wines", Name = "Wines", Category = ResourceCategory.LUXURY };
	private readonly Resource iron = new() { Key = "Iron", Name = "Iron", Category = ResourceCategory.STRATEGIC };

	private readonly C7GameData.GameData gameData;
	private readonly GameMap map;
	private readonly Player us, them;
	private readonly City ourCapital, theirCapital;

	public GoldPerTurnAndResourceDealsTest() {
		gameData = new C7GameData.GameData() { gameDifficulty = new Difficulty(), rules = new Rules() { DefaultDealDuration = 20 } };
		gameData.Resources.AddRange(new[] { wines, iron });
		EngineStorage.InitializeGameDataForTests(gameData);

		map = new() { numTilesWide = 20, numTilesTall = 2, tiles = new System.Collections.Generic.List<Tile>() };
		for (int i = 0; i < 20; ++i) {
			map.tileIndexToCoords(i, out int x, out int y);
			map.tiles.Add(new Tile(ID.None("tile")) { XCoordinate = x, YCoordinate = y });
		}
		map.computeNeighbors();
		gameData.map = map;

		us = new() { civilization = new Civilization("Rome"), id = ID.FromString("player-1"), government = new Government(), isHuman = true, rules = gameData.rules };
		them = new() { civilization = new Civilization("Greece"), id = ID.FromString("player-2"), government = new Government(), rules = gameData.rules };
		gameData.players.Add(us);
		gameData.players.Add(them);
		us.EnsureRelationshipExists(them);

		ourCapital = MakeCapital(us, map.tileAt(0, 0));
		theirCapital = MakeCapital(them, map.tileAt(8, 0));
		for (int x = 0; x <= 8; x += 2) {
			map.tileAt(x, 0).overlays.Add(road);
		}

		// Two wines and one iron by their capital, on the road.
		Place(wines, map.tileAt(6, 0));
		Place(wines, map.tileAt(4, 0));
		Place(iron, map.tileAt(2, 0));
		map.tileAt(2, 0).owningCity = theirCapital;
		gameData.InvalidateCachedTradeNetwork();
	}

	private void Place(Resource resource, Tile tile) {
		tile.Resource = resource;
		tile.owningCity = theirCapital;
	}

	private static City MakeCapital(Player owner, Tile tile) {
		City city = new(tile, owner, owner.civilization.name, ID.None("city")) { capital = true };
		tile.cityAtTile = city;
		owner.cities.Add(city);
		return city;
	}

	[Fact]
	public void ALuxuryDealSendsTheLuxury() {
		Assert.Empty(ourCapital.GetLuxuries(gameData));
		TradeOffer theyGive = new() { resources = { wines.Key } };

		Assert.True(us.ExecuteDeal(gameData, them, theyGive, new TradeOffer()));

		MultiTurnDeal inbound = Assert.Single(us.playerRelationships[them.id].multiTurnDeals, d => d.dealSubType == DealSubType.LuxuryPerTurn);
		Assert.Equal(DealDetails.Inbound, inbound.dealDetails);
		Assert.Equal(wines.Key, inbound.resourcePerTurn);
		Assert.Equal(20, inbound.TurnsRemaining(gameData.turn));
		MultiTurnDeal outbound = Assert.Single(them.playerRelationships[us.id].multiTurnDeals, d => d.dealSubType == DealSubType.LuxuryPerTurn);
		Assert.Equal(DealDetails.Outbound, outbound.dealDetails);
		Assert.Contains(wines, ourCapital.GetLuxuries(gameData).Keys);

		// They had only one to spare.
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, them, us, theyGive, new TradeOffer()));
	}

	[Fact]
	public void OnlySpareConnectedResourcesCanBeTraded() {
		// Their only iron.
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, them, us, new TradeOffer { resources = { iron.Key } }, new TradeOffer()));
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, them, us, new TradeOffer { resources = { wines.Key } }, new TradeOffer()));

		// Cut the road between the capitals.
		map.tileAt(2, 0).overlays.Remove(road);
		map.tileAt(2, 0).Resource = Resource.NONE;
		gameData.InvalidateCachedTradeNetwork();
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, them, us, new TradeOffer { resources = { wines.Key } }, new TradeOffer()));
	}

	[Fact]
	public void GoldPerTurnIsPaidEachTurnUntilTheDealEnds() {
		us.gold = 100;
		// Our income and a tenth of the treasury.
		int max = us.CalculateGoldPerTurn() + 10;
		Assert.Equal(max, TradeOffer.MaxGoldPerTurn(us));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { goldPerTurn = max + 1 }, new TradeOffer()));

		Assert.True(them.ExecuteDeal(gameData, us, new TradeOffer { goldPerTurn = 3 }, new TradeOffer()));

		int paid = 0;
		for (int turn = 0; turn < 30; ++turn) {
			gameData.turn = turn;
			PlayerRelationship.CheckForObsoleteDeals(us, gameData.players, turn);
			int received = them.AggregateFlows().fromOtherCivs;
			Assert.Equal(received, us.AggregateFlows().toOtherCivs);
			Assert.Equal(turn < 20 ? 3 : 0, received);
			paid += received;
			int expected = us.gold + us.CalculateGoldPerTurn();
			us.DoPerTurnFinanceUpdates(gameData);
			Assert.Equal(expected, us.gold);
		}
		Assert.Equal(60, paid);
	}

	[Fact]
	public void TheAIWeighsResourcesByNeed() {
		TradeOffer luxury = new() { resources = { wines.Key } };
		// We have no wines, so they'd please us; they have spare.
		Assert.True(luxury.GoldEquivalentFor(gameData, us) > luxury.GoldEquivalentFor(gameData, them, given: true, counterpart: us));
		Assert.Equal(0, luxury.GoldEquivalentFor(gameData, them));
		Assert.Equal(20 * 5, new TradeOffer { goldPerTurn = 5 }.GoldEquivalentFor(gameData, them));

		// The AI sells a luxury it has to spare for enough gold.
		TradeOffer price = new() { gold = luxury.GoldEquivalentFor(gameData, them, given: true, counterpart: us) };
		us.gold = price.gold.Value;
		Assert.True(them.WouldAcceptDealFrom(gameData, us, price, luxury));
		Assert.False(them.WouldAcceptDealFrom(gameData, us, new TradeOffer { gold = price.gold - 1 }, luxury));
	}
}

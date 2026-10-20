using System;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.AI;

// How AIs price techs they trade with each other (see AiTechPricing), and
// the rules every AI-AI deal keeps: no trade at war but peace, and no gold
// per turn it can't pay.
public class AiTechPricingTest : IDisposable {
	private readonly C7GameData.GameData gameData = new() { rules = new Rules() { DefaultDealDuration = 20 } };
	private readonly Player seller = MakeCiv("player-1");
	private readonly Player buyer = MakeCiv("player-2");
	// Knows the tech, but only the seller has met it.
	private readonly Player stranger = MakeCiv("player-3");
	private readonly Tech alphabet = new() { id = ID.FromString("tech-1"), Name = "Alphabet", Cost = 10 };
	private readonly Tech wheel = new() { id = ID.FromString("tech-2"), Name = "The Wheel", Cost = 20 };

	public AiTechPricingTest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.difficulties.Add(new Difficulty() { Name = "Sid", AiCostFactor = 10, AIToAITradeRate = 200 });
		gameData.gameDifficulty = gameData.difficulties[0];
		gameData.map.techRate = 240;
		gameData.techs.AddRange([alphabet, wheel]);
		gameData.players.AddRange([seller, buyer, stranger]);
		seller.EnsureRelationshipExists(buyer);
		seller.EnsureRelationshipExists(stranger);
		seller.knownTechs.UnionWith([alphabet.id, wheel.id]);
		stranger.knownTechs.UnionWith([alphabet.id, wheel.id]);
	}

	public void Dispose() {
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(id), government = new Government() };
	}

	private int SellingPrice(Tech tech) {
		return new TradeOffer() { techs = { tech } }.GoldEquivalentFor(gameData, seller, given: true, counterpart: buyer);
	}

	// "it is still full value to that last civ if it is still alone": the
	// stranger knows the tech, but until the buyer meets it, it doesn't
	// cheapen the tech for the buyer.
	[Fact]
	public void TheSellerAsksTheBuyersCostCountingOnlyCivsTheBuyerKnows() {
		int unmet = SellingPrice(alphabet);
		Assert.Equal(gameData.TechCostFor(alphabet, buyer), unmet);

		buyer.EnsureRelationshipExists(stranger);
		Assert.True(SellingPrice(alphabet) < unmet);
		Assert.Equal(gameData.TechCostFor(alphabet, buyer), SellingPrice(alphabet));
	}

	// A tech the buyer is "close to discovering" is "not worth very much
	// via trade".
	[Fact]
	public void TheBuyersResearchLowersThePrice() {
		int full = SellingPrice(alphabet);
		buyer.SetCurrentlyResearchedTech(alphabet.id);
		buyer.beakers = full - 5;

		Assert.Equal(5, SellingPrice(alphabet));
	}

	// To a human, the AI still asks the human's cost, with its markup on
	// top (see TradeMarkup).
	[Fact]
	public void AHumanBuyerIsAskedTheHumansCost() {
		buyer.isHuman = true;
		Assert.Equal(gameData.TechCostFor(alphabet, buyer), SellingPrice(alphabet));
		Assert.False(AiTechPricing.PricesAtBuyerCost(seller, buyer));
	}

	// "down to half-value, but only if the other AI has nothing else to
	// give for it": at Sid's trade rate of 200, half.
	[Fact]
	public void AnAiSellsForAsLittleAsHalfToAnAiWithNothingMore() {
		TradeOffer sold = new() { techs = { alphabet } };
		int full = SellingPrice(alphabet);
		Assert.Equal(50, AiTechPricing.LeastSharePercent(gameData));

		buyer.gold = (full + 1) / 2;
		Assert.True(seller.WouldAcceptDealFrom(gameData, buyer, new TradeOffer { gold = buyer.gold }, sold));
		// Not if it holds some gold back.
		buyer.gold += 10;
		Assert.False(seller.WouldAcceptDealFrom(gameData, buyer, new TradeOffer { gold = buyer.gold - 10 }, sold));
		// Nor for less than half.
		buyer.gold = full / 2 - 1;
		Assert.False(seller.WouldAcceptDealFrom(gameData, buyer, new TradeOffer { gold = buyer.gold }, sold));
		// Full value is taken from anyone.
		buyer.gold = full + 100;
		Assert.True(seller.WouldAcceptDealFrom(gameData, buyer, new TradeOffer { gold = full }, sold));
	}

	[Fact]
	public void TheLeastShareFollowsTheTradeRate() {
		gameData.gameDifficulty.AIToAITradeRate = 140;
		Assert.Equal(72, AiTechPricing.LeastSharePercent(gameData));
		gameData.gameDifficulty.AIToAITradeRate = 100;
		Assert.Equal(100, AiTechPricing.LeastSharePercent(gameData));
	}

	// "AIs don't trade with each other when they are at war" (ShuShu,
	// Apolyton 42905): at war only peace can be agreed.
	[Fact]
	public void AisAtWarTradeNothingButPeace() {
		buyer.gold = 10000;
		DeclareWar(buyer, seller, false, gameData.turn);

		TradeOffer sold = new() { techs = { alphabet } };
		Assert.False(seller.WouldAcceptDealFrom(gameData, buyer, new TradeOffer { gold = 10000 }, sold));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, buyer, seller, new TradeOffer { gold = 10000 }, sold));
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, buyer, seller,
			new TradeOffer { partOfPeaceTreaty = true }, new TradeOffer { partOfPeaceTreaty = true }));
	}

	// "They aren't allowed to sign per-turn deals they can't pay" (Soren,
	// Apolyton 42905): with no income, the buyer won't pay per turn.
	[Fact]
	public void AnAiWontPayAnotherAiGoldPerTurnItCantAfford() {
		Assert.Equal(0, buyer.CalculateGoldPerTurn());
		Assert.False(buyer.WouldAcceptDealFrom(gameData, seller, new TradeOffer { techs = { alphabet } }, new TradeOffer { goldPerTurn = 1 }));
	}
}

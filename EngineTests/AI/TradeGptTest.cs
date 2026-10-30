using System;
using System.Linq;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using EngineTests.Utils;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.AI;

// The "trade-gpt" trial switch: AIs sell each other techs for gold per
// turn, at once on meeting or learning a tech, and with the buyer's
// science rate lowered while the deal is made.
public sealed class TradeGptTest : IClassFixture<SaveGameFixture>, IDisposable {
	private readonly C7GameData.GameData gameData;
	private readonly Player buyer;
	private readonly Player seller;
	private readonly Tech tech;

	public TradeGptTest(SaveGameFixture fixture) {
		gameData = fixture.saveGame.ToGameData(fixture.behaviors);
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.animationsEnabled = false;
		var civs = gameData.players.Where(p => !p.isBarbarians && !p.isHuman && p.units.Any(u => u.unitType.isSettler)).Take(2).ToList();
		(buyer, seller) = (civs[0], civs[1]);
		foreach (Player p in civs) {
			p.isHuman = false;
			CityInteractions.BuildCity(p.units.First(u => u.unitType.isSettler).location, p, p.GetNextCityName());
		}
		buyer.EnsureRelationshipExists(seller);
		seller.EnsureRelationshipExists(buyer);
		tech = gameData.techs.First(t => t.Prerequisites.Count == 0 && !t.CannotBeTraded && !buyer.knownTechs.Contains(t.id) && !seller.knownTechs.Contains(t.id));
		seller.knownTechs.Add(tech.id);
		buyer.gold = 0;
		// An income to pay from: another civ sends it 20 gold a turn.
		RegisterMultiTurnDeal(seller, buyer, new MultiTurnDeal(DealType.Gold, DealSubType.GoldPerTurn,
			DealDetails.Outbound, goldPerTurn: 20, turnStartDeal: gameData.turn));
	}

	public void Dispose() {
		while (EngineStorage.TryDequeueNextMessageToUI(out _)) { }
	}

	[Fact]
	public void WithTheSwitchOnATechIsBoughtForGoldPerTurnAndTheRatesComeBack() {
		AiExperiments.Enable(buyer, TradeGpt.Switch);
		AiExperiments.Enable(seller, TradeGpt.Switch);
		buyer.scienceRate = buyer.maxScienceRate;
		buyer.taxRate = 10 - buyer.scienceRate;
		buyer.luxuryRate = 0;
		(int sci, int tax) = (buyer.scienceRate, buyer.taxRate);

		Assert.True(TradeGpt.On(buyer, seller));
		Assert.True(TradeGpt.TradeTechs(gameData, buyer, seller) >= 1);

		Assert.Contains(tech.id, buyer.knownTechs);
		Assert.Equal((sci, tax), (buyer.scienceRate, buyer.taxRate));
		Assert.Equal(0, buyer.gold);
		Assert.Contains(buyer.playerRelationships[seller.id].multiTurnDeals, d => d.dealSubType == DealSubType.GoldPerTurn);
	}

	[Fact]
	public void WithTheSwitchOffNothingChanges() {
		Assert.False(TradeGpt.On(buyer, seller));
		Assert.False(TradeGpt.WantsToTradeNow(buyer));
		AiExperiments.Enable(buyer, TradeGpt.Switch);
		Assert.True(TradeGpt.WantsToTradeNow(buyer));
		// Nothing new since the last look.
		Assert.False(TradeGpt.WantsToTradeNow(buyer));
		buyer.knownTechs.Add(tech.id);
		Assert.True(TradeGpt.WantsToTradeNow(buyer));
	}
}

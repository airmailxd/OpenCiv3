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

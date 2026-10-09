using System;
using System.Linq;
using System.Threading.Tasks;
using C7Engine;
using C7GameData;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.GameData;

// The engine's checks on who may act when, and on the deals and wars they
// propose.
public class TurnAndTradeRulesTest : IDisposable {
	private readonly C7GameData.GameData gameData = new();
	private readonly Player us = MakeCiv("player-1");
	private readonly Player them = MakeCiv("player-2");
	// A civ nobody has met.
	private readonly Player stranger = MakeCiv("player-3");

	public TurnAndTradeRulesTest() {
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
		EngineStorage.ResetNetworking();
		gameData.players.Add(us);
		gameData.players.Add(them);
		gameData.players.Add(stranger);
		us.EnsureRelationshipExists(them);
		us.isHuman = true;
		EngineStorage.activePlayerID = us.id;
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
		EngineStorage.activePlayerID = null;
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(), government = new Government() };
	}

	private static void ProcessAll() {
		while (EngineStorage.HasPendingMessagesToEngine()) {
			EngineStorage.ProcessNextMessageToEngine();
		}
	}

	[Fact]
	public void APlayerWhoEndedTheirTurnCantActUntilTheNext() {
		Assert.True(TurnHandling.IsPlayersTurn(gameData, us.id));
		us.hasPlayedThisTurn = true;
		Assert.False(TurnHandling.IsPlayersTurn(gameData, us.id));

		// Nor end it again.
		new MsgEndTurn { playerID = us.id }.send();
		ProcessAll();
		Assert.False(TurnHandling.TurnInProgress);
	}

	[Fact]
	public async Task OnlyAnswersAreAcceptedFromAPlayerAnAIWaitsOn() {
		us.hasPlayedThisTurn = true;
		EngineStorage.diplomacyPlayerID = us.id;
		EngineStorage.diplomacyAIPlayerID = them.id;

		new MsgDeclareWar(them) { playerID = us.id }.send();
		Task<MsgDiplomacyCompleted> closed = EngineStorage.WaitForMessageToEngine<MsgDiplomacyCompleted>();
		new MsgDiplomacyCompleted { playerID = us.id }.send();
		ProcessAll();

		Assert.True(AtPeace(us, them));
		await closed.WaitAsync(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public async Task EveryoneWaitingForAMessageGetsIt() {
		Task<MsgDiplomacyCompleted> first = EngineStorage.WaitForMessageToEngine<MsgDiplomacyCompleted>();
		Task<MsgDiplomacyCompleted> second = EngineStorage.WaitForMessageToEngine<MsgDiplomacyCompleted>();
		new MsgDiplomacyCompleted { playerID = us.id }.send();
		ProcessAll();

		await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void TradesNeedTheGoldOnOffer() {
		us.gold = 10;
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { gold = -5 }, new TradeOffer()));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { gold = 20 }, new TradeOffer()));
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer(), new TradeOffer { gold = 1 }));
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { gold = 10 }, new TradeOffer()));
	}

	[Fact]
	public void OnlyPlayersWhoHaveMetCanTrade() {
		us.gold = 10;
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, stranger, new TradeOffer { gold = 5 }, new TradeOffer()));
	}

	[Fact]
	public void PeaceCanOnlyBeMadeAtWar() {
		TradeOffer peace = new() { partOfPeaceTreaty = true };
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, peace, new TradeOffer { partOfPeaceTreaty = true }));
		Assert.False(them.ExecuteDeal(gameData, us, peace, new TradeOffer { partOfPeaceTreaty = true }));

		// At war, only peace can be made.
		us.DeclareWarOn(them, gameData.turn);
		us.gold = 10;
		Assert.NotNull(TradeOffer.ProblemWithDeal(gameData, us, them, new TradeOffer { gold = 5 }, new TradeOffer()));
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, us, them, peace, new TradeOffer { partOfPeaceTreaty = true }));
	}

	[Fact]
	public void ADealThatCanNoLongerBeMadeIsNotCarriedOut() {
		us.gold = 50;
		TradeOffer ourGold = new() { gold = 40 };
		Assert.Null(TradeOffer.ProblemWithDeal(gameData, us, them, ourGold, new TradeOffer()));

		// We spent the gold while they thought it over.
		us.gold = 10;
		Assert.False(them.ExecuteDeal(gameData, us, ourGold, new TradeOffer()));
		Assert.Equal(10, us.gold);
		Assert.Equal(0, them.gold);
	}

	[Fact]
	public void AnAIRefusingContactWontDeal() {
		them.DeclareWarOn(us, gameData.turn);
		us.playerRelationships[them.id].refuseContactUntilTurn = gameData.turn + 5;
		them.playerRelationships[us.id].refuseContactUntilTurn = gameData.turn + 5;

		TradeOffer peace = new() { partOfPeaceTreaty = true };
		Assert.False(them.WouldAcceptDealFrom(gameData, us, peace, new TradeOffer { partOfPeaceTreaty = true }));
	}

	[Fact]
	public void WarCantBeDeclaredOnACivNotMet() {
		new MsgDeclareWar(stranger) { playerID = us.id }.send();
		ProcessAll();
		Assert.False(us.playerRelationships.ContainsKey(stranger.id));
	}

	[Fact]
	public void TheCivWarIsDeclaredOnHearsOfIt() {
		them.isHuman = true;
		new MsgDeclareWar(them) { playerID = us.id }.send();
		ProcessAll();

		Assert.True(AtWar(us, them));
		MsgWarDeclaration declaration = Assert.Single(EngineStorage.messagesToUI.OfType<MsgWarDeclaration>());
		Assert.Same(them, declaration.recipient);
	}

	[Fact]
	public void OnlyTheOwnerMayAbandonACity() {
		City capital = new(Tile.NONE, us, "Capital", ID.None("city"));
		us.cities.Add(capital);
		Assert.True(CityInteractions.MayAbandon(us, capital, gameData));
		Assert.False(CityInteractions.MayAbandon(them, capital, gameData));
	}
}

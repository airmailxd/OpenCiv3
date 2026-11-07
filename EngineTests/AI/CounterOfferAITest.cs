using System;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.AI;

// The counter-offers an AI makes when a human asks "What do you want for
// $GIVE0?" or "Would you care to offer something in exchange for $GIVE0?",
// and how it takes a deal on the table (see CounterOfferAI).
public class CounterOfferAITest : IDisposable {
	private readonly C7GameData.GameData gameData = new() { rules = new Rules() { DefaultDealDuration = 20 } };
	private readonly Player ai = MakeCiv("player-1");
	private readonly Player human = MakeCiv("player-2");
	private readonly Tech alphabet = new() { id = ID.FromString("tech-1"), Name = "Alphabet", Cost = 10 };
	private readonly Tech wheel = new() { id = ID.FromString("tech-2"), Name = "The Wheel", Cost = 20 };
	private readonly Tech pottery = new() { id = ID.FromString("tech-3"), Name = "Pottery", Cost = 15 };

	public CounterOfferAITest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.difficulties.Add(new Difficulty() { Name = "Regent", AiCostFactor = 10, AIToAITradeRate = 100 });
		gameData.gameDifficulty = gameData.difficulties[0];
		gameData.map.techRate = 240;
		gameData.techs.AddRange([alphabet, wheel, pottery]);
		gameData.players.AddRange([ai, human]);
		ai.EnsureRelationshipExists(human);
		human.EnsureRelationshipExists(ai);
		human.isHuman = true;
		ai.knownTechs.UnionWith([alphabet.id, wheel.id]);
		human.knownTechs.Add(pottery.id);
	}

	public void Dispose() {
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(id), government = new Government() };
	}

	private TradeOffer Alphabet() => new() { techs = { alphabet } };

	// The least gold the AI takes for alphabet.
	private int LeastGoldForAlphabet() {
		for (int gold = 0; gold <= human.gold; gold++) {
			if (ai.WouldAcceptDealFrom(gameData, human, new TradeOffer { gold = gold }, Alphabet())) {
				return gold;
			}
		}
		throw new InvalidOperationException("No price");
	}

	// "Usually they give 10%-15% more of what they offer": asked what it
	// wants, the AI asks more gold than it would take, by about an eighth.
	[Fact]
	public void AskedWhatItWantsTheAIAsksMoreGoldThanItWouldTake() {
		human.gold = 10000;
		int least = LeastGoldForAlphabet();

		(TradeOffer humanGives, TradeOffer humanWants)? counter = CounterOfferAI.Counter(gameData, ai, human, new TradeOffer(), Alphabet());

		Assert.NotNull(counter);
		int asked = counter.Value.humanGives.gold ?? 0;
		Assert.True(asked > least, $"asked {asked}, would take {least}");
		Assert.InRange(asked, least * 110 / 100, least * 115 / 100 + 1);
		Assert.Contains(alphabet, counter.Value.humanWants.techs);
		Assert.True(ai.WouldAcceptDealFrom(gameData, human, counter.Value.humanGives, counter.Value.humanWants));
	}

	// Without the gold, it asks for a tech as well.
	[Fact]
	public void AskedWhatItWantsTheAIAsksForATechWhenThereIsNotEnoughGold() {
		human.gold = 0;

		(TradeOffer humanGives, TradeOffer humanWants)? counter = CounterOfferAI.Counter(gameData, ai, human, new TradeOffer(), Alphabet());

		Assert.NotNull(counter);
		Assert.Contains(pottery, counter.Value.humanGives.techs);
		Assert.True(ai.WouldAcceptDealFrom(gameData, human, counter.Value.humanGives, counter.Value.humanWants));
	}

	[Fact]
	public void AskedWhatItWantsTheAISeesNoDealWhenTheHumanHasNothing() {
		human.gold = 0;
		human.knownTechs.Clear();

		Assert.Null(CounterOfferAI.Counter(gameData, ai, human, new TradeOffer(), Alphabet()));
	}

	// Asked what it would give for pottery, the AI offers gold first, less
	// than the most it would pay.
	[Fact]
	public void AskedForAnExchangeTheAIOffersLessGoldThanItWouldPay() {
		ai.gold = 10000;
		TradeOffer pottery = new() { techs = { this.pottery } };
		int most = 0;
		while (ai.WouldAcceptDealFrom(gameData, human, pottery, new TradeOffer { gold = most + 1 })) {
			most++;
		}

		(TradeOffer humanGives, TradeOffer humanWants)? exchange = CounterOfferAI.Exchange(gameData, ai, human, pottery, new TradeOffer());

		Assert.NotNull(exchange);
		int offered = exchange.Value.humanWants.gold ?? 0;
		Assert.True(offered > 0 && offered < most, $"offered {offered}, would pay {most}");
		Assert.Empty(exchange.Value.humanWants.techs);
		Assert.True(ai.WouldAcceptDealFrom(gameData, human, exchange.Value.humanGives, exchange.Value.humanWants));
	}

	// With no gold, it offers a tech of its own.
	[Fact]
	public void AskedForAnExchangeAnAIWithoutGoldOffersATech() {
		ai.gold = 0;
		human.knownTechs.UnionWith([alphabet.id]);
		gameData.techs.Add(new Tech { id = ID.FromString("tech-4"), Name = "Bronze Working", Cost = 5 });
		ai.knownTechs.Add(ID.FromString("tech-4"));
		TradeOffer valuable = new() { techs = { pottery }, gold = 0 };

		(TradeOffer humanGives, TradeOffer humanWants)? exchange = CounterOfferAI.Exchange(gameData, ai, human, valuable, new TradeOffer());

		Assert.NotNull(exchange);
		Assert.Single(exchange.Value.humanWants.techs);
		Assert.True(ai.WouldAcceptDealFrom(gameData, human, exchange.Value.humanGives, exchange.Value.humanWants));
	}

	[Fact]
	public void AtWarThereIsNoHaggling() {
		human.gold = 10000;
		DeclareWar(human, ai, false, gameData.turn + 10);

		Assert.Null(CounterOfferAI.Counter(gameData, ai, human, new TradeOffer(), Alphabet()));
		Assert.Null(CounterOfferAI.Exchange(gameData, ai, human, new TradeOffer { gold = 100 }, new TradeOffer()));
	}

	// The AI's words go from "This offer seems fair." to "Unfortunately, this
	// does not work for us" as the deal gets further from one it would take.
	[Fact]
	public void TheFurtherFromADealItWouldTakeTheStrongerTheAIsNo() {
		human.gold = 10000;
		int least = LeastGoldForAlphabet();
		DealReaction ReactionTo(int gold) => CounterOfferAI.Reaction(gameData, ai, human, new TradeOffer { gold = gold }, Alphabet());

		Assert.Equal(DealReaction.Accept, ReactionTo(least));
		Assert.Equal(DealReaction.WeakReject, ReactionTo(least * 90 / 100));
		Assert.Equal(DealReaction.NeutralReject, ReactionTo(least * 60 / 100));
		Assert.Equal(DealReaction.StrongReject, ReactionTo(least * 30 / 100));
		Assert.Equal(DealReaction.TotalReject, ReactionTo(least * 20 / 100));
		// Nothing for something can never be enough.
		Assert.Equal(DealReaction.TotalReject, CounterOfferAI.Reaction(gameData, ai, human, new TradeOffer(), Alphabet()));
	}
}

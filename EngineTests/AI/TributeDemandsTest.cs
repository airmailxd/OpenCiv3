using System;
using System.Threading.Tasks;
using C7Engine;
using C7Engine.AI;
using C7GameData;
using C7GameData.Save;
using EngineTests.Utils;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.AI;

public class TributeDemandsTest : MapBase, IDisposable {
	private readonly C7GameData.GameData gameData = new();
	private readonly Player us = MakeCiv("player-1");
	private readonly Player them = MakeCiv("player-2");

	public TributeDemandsTest() {
		// Engine state is static; parallelization is disabled repo-wide (XunitSettings.cs)
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.pendingMessages.Clear();
		EngineStorage.messagesToUI.Clear();
		EngineStorage.ResetNetworking();
		gameData.players.Add(us);
		gameData.players.Add(them);
		us.EnsureRelationshipExists(them);
		them.EnsureRelationshipExists(us);
		them.isHuman = true;
		gameData.turn = 50;

		InitilizeStartTile(MakePlainsTile(), new TileLocation(50, 50));
		us.cities.Add(new City(startTile, us, "Capital", ID.None("city")));
	}

	public void Dispose() {
		EngineStorage.ResetNetworking();
		EngineStorage.activePlayerID = null;
	}

	private static Player MakeCiv(string id) {
		return new Player() { id = ID.FromString(id), civilization = new Civilization(), government = new Government() };
	}

	private void AddWarriors(Player owner, int count) {
		for (int i = 0; i < count; ++i) {
			MapUnit unit = new(ID.None("unit")) {
				owner = owner,
				unitType = new UnitPrototype() { attack = 1, defense = 1 },
				experienceLevel = new ExperienceLevel("regular", "Regular", 3, 0, 0),
				location = startTile,
			};
			unit.unitType.categories.Add("Land");
			startTile.unitsOnTile.Add(unit);
			owner.units.Add(unit);
		}
	}

	// We demand the tribute of the human, them, who answers as given.
	private async Task<MsgShowTributeDemand> Demand(TradeOffer tribute, params MessageToEngine[] answers) {
		Task demand = TributeDemands.DemandFrom(us, them, tribute, gameData);

		MsgShowTributeDemand shown = null;
		while (EngineStorage.TryDequeueNextMessageToUI(out MessageToUI msg)) {
			shown ??= msg as MsgShowTributeDemand;
		}
		Assert.NotNull(shown);
		foreach (MessageToEngine answer in answers) {
			answer.playerID = them.id;
			answer.send();
			EngineStorage.ProcessNextMessageToEngine();
		}
		await demand;
		return shown;
	}

	[Fact]
	public void AStrongerAiDemandsTributeOfAWeakerHuman() {
		AddWarriors(us, 4);
		AddWarriors(them, 1);

		Assert.True(TributeDemands.WouldDemandFrom(us, them, gameData));
	}

	[Fact]
	public void NoTributeIsDemandedEarlyInTheGame() {
		AddWarriors(us, 4);
		AddWarriors(them, 1);
		gameData.turn = TributeDemands.FIRST_DEMAND_TURN - 1;

		Assert.False(TributeDemands.WouldDemandFrom(us, them, gameData));

		gameData.turn = TributeDemands.FIRST_DEMAND_TURN;
		Assert.True(TributeDemands.WouldDemandFrom(us, them, gameData));
	}

	[Fact]
	public void NoTributeIsDemandedSoonAfterMeeting() {
		AddWarriors(us, 4);
		AddWarriors(them, 1);
		us.playerRelationships[them.id].firstContactTurn = 45;

		gameData.turn = 45 + TributeDemands.TURNS_AFTER_CONTACT_BEFORE_DEMANDS - 1;
		Assert.False(TributeDemands.WouldDemandFrom(us, them, gameData));

		gameData.turn = 45 + TributeDemands.TURNS_AFTER_CONTACT_BEFORE_DEMANDS;
		Assert.True(TributeDemands.WouldDemandFrom(us, them, gameData));
	}

	[Fact]
	public void MeetingRecordsTheTurn() {
		Player other = MakeCiv("player-3");
		gameData.players.Add(other);

		us.EnsureRelationshipExists(other);

		Assert.Equal(50, us.playerRelationships[other.id].firstContactTurn);
		Assert.Equal(50, other.playerRelationships[us.id].firstContactTurn);
	}

	// "if (32 - this->power_rank > 2 * (32 - recipient->power_rank))" the
	// AI makes no demand (C3X's injected_code.c): third in power may ask
	// of the second, but not of the first.
	[Fact]
	public void TributeIsDemandedOnlyOfACivAtLeastHalfOurPowerPlace() {
		Player strongest = MakeCiv("player-3");
		gameData.players.Add(strongest);
		AddWarriors(strongest, 6);
		AddWarriors(them, 4);
		AddWarriors(us, 2);
		Assert.Equal(3, TributeDemands.PowerPlace(gameData, us));

		Assert.True(TributeDemands.PowerPlaceAllows(gameData, us, them));
		Assert.True(TributeDemands.WouldDemandFrom(us, them, gameData));

		// Now they are first.
		AddWarriors(them, 4);
		Assert.Equal(1, TributeDemands.PowerPlace(gameData, them));
		Assert.False(TributeDemands.WouldDemandFrom(us, them, gameData));
	}

	private int DemandRollsPassed(int tries) {
		int passed = 0;
		for (int i = 0; i < tries; ++i) {
			if (TributeDemands.RollsToDemand(us, them, gameData)) {
				++passed;
			}
		}
		return passed;
	}

	// The strongest civ, Cautious or worse, at aggression +1 or +2 always
	// passes the stock rolls; Polite (here -10 from a gift) passes one in
	// ten; at aggression 0, one in two.
	[Fact]
	public void TheDemandRollsFollowPowerAttitudeAndAggression() {
		AddWarriors(us, 4);
		us.civilization.aggressionLevel = 2;
		Assert.Equal(0, Attitudes.Points(gameData, us, them));
		Assert.Equal(1000, DemandRollsPassed(1000));

		Attitudes.RecordGift(us.playerRelationships[them.id], 100, gameData.turn);
		Assert.Equal(-10, Attitudes.Points(gameData, us, them));
		Assert.InRange(DemandRollsPassed(4000), 300, 500);

		us.playerRelationships[them.id].giftGold = 0;
		us.civilization.aggressionLevel = 0;
		Assert.InRange(DemandRollsPassed(4000), 1800, 2200);
	}

	// A one-in-(power place) roll: second in power passes about half the
	// time.
	[Fact]
	public void ALesserPowerDemandsLessOften() {
		AddWarriors(them, 4);
		AddWarriors(us, 2);
		us.civilization.aggressionLevel = 2;
		Assert.Equal(2, TributeDemands.PowerPlace(gameData, us));

		Assert.InRange(DemandRollsPassed(4000), 1800, 2200);
	}

	[Fact]
	public void NoTributeIsDemandedOfAnAi() {
		them.isHuman = false;
		AddWarriors(us, 4);

		Assert.False(TributeDemands.WouldDemandFrom(us, them, gameData));
	}

	[Fact]
	public void NoTributeIsDemandedAtWar() {
		AddWarriors(us, 4);
		us.DeclareWarOn(them, gameData.turn);

		Assert.False(TributeDemands.WouldDemandFrom(us, them, gameData));
	}

	[Fact]
	public async Task TributeIsDemandedAgainOnlyAfterAWhile() {
		AddWarriors(us, 4);
		them.gold = 100;
		await Demand(TributeDemands.ChooseTribute(us, them, gameData),
			new MsgRespondToTributeDemand(true), new MsgDiplomacyCompleted());

		gameData.turn += TributeDemands.MIN_TURNS_BETWEEN_DEMANDS - 1;
		Assert.False(TributeDemands.WouldDemandFrom(us, them, gameData));
		gameData.turn += 1;
		Assert.True(TributeDemands.WouldDemandFrom(us, them, gameData));
	}

	[Fact]
	public void AFifthOfTheTreasuryIsDemanded() {
		them.gold = 157;

		TradeOffer tribute = TributeDemands.ChooseTribute(us, them, gameData, preferGold: true);

		Assert.Equal(30, tribute.gold);
	}

	[Fact]
	public void GoldIsDemandedWhenThereIsNoTechToGive() {
		them.gold = 157;

		TradeOffer tribute = TributeDemands.ChooseTribute(us, them, gameData, preferGold: false);

		Assert.Equal(30, tribute.gold);
	}

	[Fact]
	public void NoOtherAiDemandsTributeSoonAfterOneHas() {
		Player other = MakeCiv("player-3");
		gameData.players.Add(other);
		other.EnsureRelationshipExists(them);
		AddWarriors(us, 4);
		other.playerRelationships[them.id].lastTributeDemandTurn = gameData.turn - 1;

		Assert.False(TributeDemands.WouldDemandFrom(us, them, gameData));

		other.playerRelationships[them.id].lastTributeDemandTurn = gameData.turn - TributeDemands.MIN_TURNS_BETWEEN_ANY_DEMANDS;
		Assert.True(TributeDemands.WouldDemandFrom(us, them, gameData));
	}

	[Fact]
	public void NothingIsDemandedOfAHumanWithNoGoldOrTechsToGive() {
		them.gold = TributeDemands.MIN_GOLD_DEMANDED;

		Assert.Null(TributeDemands.ChooseTribute(us, them, gameData));
	}

	[Fact]
	public async Task AHumanWhoPaysKeepsThePeace() {
		AddWarriors(us, 4);
		us.gold = 10;
		them.gold = 100;

		MsgShowTributeDemand shown = await Demand(new TradeOffer() { gold = 50 },
			new MsgRespondToTributeDemand(true), new MsgDiplomacyCompleted());

		Assert.Same(us, shown.aiPlayer);
		Assert.Same(them, shown.humanPlayer);
		Assert.Equal(50, shown.gold);
		Assert.Equal(60, us.gold);
		Assert.Equal(50, them.gold);
		Assert.True(AtPeace(us, them));
	}

	[Fact]
	public async Task AHumanWhoRefusesIsAttacked() {
		AddWarriors(us, 4);
		them.gold = 100;

		await Demand(new TradeOffer() { gold = 50 },
			new MsgRespondToTributeDemand(false), new MsgDiplomacyCompleted());

		Assert.Equal(100, them.gold);
		Assert.True(AtWar(us, them));
		// They were warned, so it's no sneak attack.
		Assert.False(them.playerRelationships[us.id].wasSneakAttacked);
	}

	[Fact]
	public async Task AHumanWhoCanNoLongerPayIsAttacked() {
		AddWarriors(us, 4);
		them.gold = 10;

		await Demand(new TradeOffer() { gold = 50 },
			new MsgRespondToTributeDemand(true), new MsgDiplomacyCompleted());

		Assert.True(AtWar(us, them));
	}

	[Fact]
	public async Task PaidTributeIsRemembered() {
		AddWarriors(us, 4);
		them.gold = 100;

		await Demand(new TradeOffer() { gold = 50 },
			new MsgRespondToTributeDemand(true), new MsgDiplomacyCompleted());

		Assert.Equal(gameData.turn, us.playerRelationships[them.id].lastTributePaidTurn);
		Assert.Equal(-1, Attitudes.Points(gameData, us, them));
	}

	private void PlayOn(string difficulty) {
		foreach (string name in new[] { "Chieftain", "Warlord", "Regent", "Monarch" }) {
			gameData.difficulties.Add(new Difficulty() { Name = name });
		}
		gameData.gameDifficulty = gameData.difficulties.Find(d => d.Name == difficulty);
	}

	[Fact]
	public async Task BelowRegentACalmAiBacksDownButHoldsItAgainstThem() {
		PlayOn("Warlord");
		AddWarriors(us, 4);
		them.gold = 100;

		await Demand(new TradeOffer() { gold = 50 },
			new MsgRespondToTributeDemand(false), new MsgDiplomacyCompleted());

		Assert.True(AtPeace(us, them));
		Assert.Equal(1, us.playerRelationships[them.id].tributeBluffsCalled);
	}

	[Fact]
	public async Task FromRegentUpARefusalAlwaysMeansWar() {
		PlayOn("Regent");
		AddWarriors(us, 4);
		them.gold = 100;

		await Demand(new TradeOffer() { gold = 50 },
			new MsgRespondToTributeDemand(false), new MsgDiplomacyCompleted());

		Assert.True(AtWar(us, them));
	}

	// No war over a refused demand while the two have a mutual protection
	// pact (C3X's injected_code.c: "Relation_Treaties ... & 4").
	[Fact]
	public async Task NoWarOverARefusalWithAMutualProtectionPact() {
		PlayOn("Regent");
		AddWarriors(us, 4);
		them.gold = 100;
		Pacts.SignMutualProtectionPact(gameData, us, them);

		await Demand(new TradeOffer() { gold = 50 },
			new MsgRespondToTributeDemand(false), new MsgDiplomacyCompleted());

		Assert.True(AtPeace(us, them));
		Assert.Equal(1, us.playerRelationships[them.id].tributeBluffsCalled);
	}

	[Fact]
	public async Task ClosingTheDemandWithoutAnsweringIsPaying() {
		AddWarriors(us, 4);
		them.gold = 100;

		await Demand(new TradeOffer() { gold = 50 }, new MsgDiplomacyCompleted());

		Assert.Equal(50, them.gold);
		Assert.True(AtPeace(us, them));
	}
}

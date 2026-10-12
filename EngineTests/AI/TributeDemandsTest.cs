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

	[Fact]
	public void NoTributeIsDemandedOfAnEqual() {
		AddWarriors(us, 2);
		AddWarriors(them, 2);

		Assert.False(TributeDemands.WouldDemandFrom(us, them, gameData));
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
	public void HalfTheTreasuryIsDemanded() {
		them.gold = 157;

		TradeOffer tribute = TributeDemands.ChooseTribute(us, them, gameData);

		Assert.Equal(70, tribute.gold);
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

	[Fact]
	public async Task ClosingTheDemandWithoutAnsweringIsPaying() {
		AddWarriors(us, 4);
		them.gold = 100;

		await Demand(new TradeOffer() { gold = 50 }, new MsgDiplomacyCompleted());

		Assert.Equal(50, them.gold);
		Assert.True(AtPeace(us, them));
	}
}

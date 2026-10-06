using C7Engine;
using C7GameData;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.GameData;

public class ActiveDealsTest {
	private readonly C7GameData.GameData gameData = new();
	private readonly Player us = MakeCiv("player-1", "Roman");
	private readonly Player them = MakeCiv("player-2", "Greek");
	private readonly Player third = MakeCiv("player-3", "Zulu");

	public ActiveDealsTest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		gameData.rules = new Rules() { DefaultDealDuration = 20 };
		gameData.players.Add(us);
		gameData.players.Add(them);
		gameData.players.Add(third);
		gameData.Resources.Add(new Resource() { Key = "wines", Name = "Wines" });
		gameData.Resources.Add(new Resource() { Key = "iron", Name = "Iron" });
		gameData.turn = 10;
	}

	private static Player MakeCiv(string id, string noun) {
		return new Player() {
			id = ID.FromString(id),
			civilization = new Civilization() { noun = noun },
			government = new Government(),
		};
	}

	[Fact]
	public void PeaceFromFirstContactHasNoCountdown() {
		us.EnsureRelationshipExists(them);

		ActiveDeals deals = ActiveDeals.Between(gameData, us, them);

		Assert.Equal(["Peace Treaty"], deals.agreements);
		Assert.Empty(deals.theyGive);
		Assert.Empty(deals.weGive);
	}

	[Fact]
	public void PeaceAfterWarCountsDownThenHasNoCountdown() {
		us.EnsureRelationshipExists(them);
		DeclareWar(us, them, false, 0);
		SignPeaceAfterWar(us, them, gameData);

		Assert.Equal(["Peace Treaty (20 turns)"], ActiveDeals.Between(gameData, us, them).agreements);

		gameData.turn = 29;
		Assert.Equal(["Peace Treaty (1 turn)"], ActiveDeals.Between(gameData, them, us).agreements);

		gameData.turn = 30;
		Assert.Equal(["Peace Treaty"], ActiveDeals.Between(gameData, us, them).agreements);
	}

	[Fact]
	public void AtWarThereAreNoDeals() {
		us.EnsureRelationshipExists(them);
		DeclareWar(us, them, false, 0);

		Assert.True(ActiveDeals.Between(gameData, us, them).IsEmpty);
	}

	[Fact]
	public void ResourcesAndGoldAreSplitByWhoGivesThem() {
		us.EnsureRelationshipExists(them);
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.Luxury, DealSubType.LuxuryPerTurn,
			DealDetails.Outbound, 0, "wines", 20, 5));
		RegisterMultiTurnDeal(them, us, new MultiTurnDeal(DealType.Resource, DealSubType.ResourcePerTurn,
			DealDetails.Outbound, 0, "iron", 20, 10));
		RegisterMultiTurnDeal(them, us, new MultiTurnDeal(DealType.Gold, DealSubType.GoldPerTurn,
			DealDetails.Outbound, 3, null, 20, 10));

		ActiveDeals ours = ActiveDeals.Between(gameData, us, them);
		Assert.Equal(["Wines (15 turns)"], ours.weGive);
		Assert.Equal(["Iron (20 turns)", "3 gold per turn (20 turns)"], ours.theyGive);

		ActiveDeals theirs = ActiveDeals.Between(gameData, them, us);
		Assert.Equal(["Wines (15 turns)"], theirs.theyGive);
	}

	[Fact]
	public void AgreementsAgainstACivNameIt() {
		us.EnsureRelationshipExists(them);
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.Alliance, DealSubType.MilitaryAlliance,
			DealDetails.Exchange, 0, null, 20, 10, third.id));
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.RightOfPassage,
			DealDetails.Exchange, 0, null, 20, 10));

		Assert.Equal(["Peace Treaty", "Military Alliance against the Zulu (20 turns)", "Right of Passage (20 turns)"],
			ActiveDeals.Between(gameData, us, them).agreements);
	}
}

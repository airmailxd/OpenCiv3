using System.Collections.Generic;
using C7Engine;
using C7GameData;
using Xunit;
using static C7GameData.PlayerRelationship;

namespace EngineTests.GameData;

// What Civ3's hidden Soren advisor lists: every other civ in the game, eight
// at a time, with its government and ties.
public class SorenReportTest {
	private readonly C7GameData.GameData gameData = new() {
		gameDifficulty = new Difficulty(),
		rules = new Rules() { DefaultDealDuration = 20 },
		turn = 10,
	};
	private readonly Player us;
	private readonly Player them;
	private readonly Player third;
	private readonly Player unmet;

	public SorenReportTest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		us = MakeCiv("player-1", "Romans");
		us.isHuman = true;
		them = MakeCiv("player-2", "Greeks");
		third = MakeCiv("player-3", "Zulus");
		unmet = MakeCiv("player-4", "Aztecs");
		us.EnsureRelationshipExists(them);
		them.EnsureRelationshipExists(third);
	}

	private Player MakeCiv(string id, string noun, string government = "Despotism") {
		Player player = new() {
			id = ID.FromString(id),
			civilization = new Civilization(noun) { noun = noun, adjective = noun },
			government = government == null ? null : new Government() { name = government },
			rules = gameData.rules,
		};
		gameData.players.Add(player);
		return player;
	}

	[Fact]
	public void ListsEveryOtherCivMetOrNot() {
		Player barbarians = MakeCiv("player-5", "Barbarians");
		barbarians.civilization.isBarbarian = true;
		Player dead = MakeCiv("player-6", "Celts");
		dead.defeated = true;

		Assert.Equal([them, third, unmet], SorenReport.Civs(gameData, us));
	}

	[Fact]
	public void ShowsEightCivsAtATime() {
		Assert.False(SorenReport.HasPrevious(0));
		Assert.True(SorenReport.HasPrevious(1));
		Assert.False(SorenReport.HasNext(0, 8));
		Assert.True(SorenReport.HasNext(0, 9));
		Assert.False(SorenReport.HasNext(1, 9));
		Assert.Equal(0, SorenReport.ClampFirst(3, 5));
		Assert.Equal(2, SorenReport.ClampFirst(5, 10));
		Assert.Equal(0, SorenReport.ClampFirst(-1, 10));
	}

	[Fact]
	public void GovernmentIsUnknownWithoutOne() {
		Player none = MakeCiv("player-7", "Incas", government: null);
		Assert.Equal("Government Despotism", SorenReport.GovernmentLine(them));
		Assert.Equal("Government Unknown", SorenReport.GovernmentLine(none));
		Assert.Equal("The Greeks", SorenReport.Heading(them));
	}

	[Fact]
	public void LinesShowOurTieThenTheirsWithoutAnEmbassy() {
		DeclareWar(them, third, false, 0, honourPacts: false);
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.DiplomaticAgreement, DealSubType.MutualProtectionPact,
			DealDetails.Exchange, 0, null, 20, 10));
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.Alliance, DealSubType.MilitaryAlliance,
			DealDetails.Exchange, 0, null, 20, 10, third.id));
		RegisterMultiTurnDeal(us, them, new MultiTurnDeal(DealType.Embargo, DealSubType.TradeEmbargo,
			DealDetails.Exchange, 0, null, 20, 10, third.id));

		List<string> lines = SorenReport.Lines(gameData, us, them);
		Assert.Equal([
			"Government Despotism",
			"We are at peace with the Greeks",
			"We have a mutual protection pact with the Greeks",
			"We have a military alliance with the Greeks against the Zulus",
			"We have a trade embargo with the Greeks against the Zulus",
			"They are at war with the Zulus",
		], lines);
	}

	[Fact]
	public void UnmetCivHasOnlyItsGovernment() {
		Assert.Equal(["Government Despotism"], SorenReport.Lines(gameData, us, unmet));
	}
}

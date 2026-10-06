using System.Collections.Generic;
using System.Linq;
using C7Engine;
using C7GameData;
using C7GameData.Save;
using Xunit;

namespace EngineTests.GameData.Victory;

public class UnitedNationsTest : System.IDisposable {
	private readonly C7GameData.GameData gameData = new() {
		gameDifficulty = new Difficulty(),
		rules = new Rules(),
		history = new Dictionary<string, List<HistTurnRecord>>(),
		timeOptions = new TimeOptions(),
		victoryConditions = new VictoryConditions { AllowDiplomaticVictory = true },
		turn = 50,
	};
	private int nextId = 1;

	public UnitedNationsTest() {
		EngineStorage.InitializeGameDataForTests(gameData);
		EngineStorage.messagesToUI.Clear();
	}

	public void Dispose() {
		EngineStorage.messagesToUI.Clear();
	}

	private Player MakePlayer(string civName, int population, bool human = false) {
		Player player = new() {
			id = ID.FromString($"player-{nextId++}"),
			isHuman = human,
			civilization = new Civilization(civName) { noun = civName },
			government = new Government(),
			rules = gameData.rules,
		};
		gameData.players.Add(player);
		gameData.history[player.id.ToString()] = new List<HistTurnRecord> { new HistTurnRecord() };
		if (population > 0) {
			Tile tile = new(ID.None("tile")) { continent = 1 };
			City city = new(tile, player, $"{civName}ville", ID.FromString($"city-{nextId++}"));
			tile.cityAtTile = city;
			player.cities.Add(city);
			gameData.cities.Add(city);
			for (int i = 0; i < population; i++) {
				city.residents.Add(new CityResident { city = city });
			}
		}
		return player;
	}

	private void BuildUnitedNations(Player owner) {
		SaveBuilding sb = new() { name = "The United Nations", greatWonderProperties = new() };
		sb.flags.Add(SaveBuilding.Flag.AllowDiplomaticVictory);
		owner.cities[0].AddBuilding(new Building(sb, gameData));
	}

	private static void Meet(params Player[] players) {
		foreach (Player a in players) {
			foreach (Player b in players) {
				if (a != b) {
					a.EnsureRelationshipExists(b);
				}
			}
		}
	}

	[Fact]
	public void NoUnitedNationsMeansNoCandidates() {
		MakePlayer("Romans", 3);
		MakePlayer("Greeks", 2);
		Assert.Null(UnitedNations.Candidates(gameData));
		Assert.Null(UnitedNations.ProcessEndOfRound(gameData));
		Assert.Equal(-1, gameData.unitedNations.votingTurn);
	}

	[Fact]
	public void CandidatesAreTheOwnerAndTheMostPopulousRival() {
		Player rome = MakePlayer("Romans", 3);
		MakePlayer("Greeks", 2);
		Player egypt = MakePlayer("Egyptians", 7);
		BuildUnitedNations(rome);

		var candidates = UnitedNations.Candidates(gameData);
		Assert.NotNull(candidates);
		Assert.Equal(rome, candidates.Value.owner);
		Assert.Equal(egypt, candidates.Value.rival);
	}

	[Fact]
	public void AIVotesForTheCandidateItIsAtPeaceWith() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player greece = MakePlayer("Greeks", 2);
		Meet(rome, egypt, greece);
		PlayerRelationship.DeclareWar(greece, egypt, false, 0);

		Assert.Equal(rome, UnitedNations.AIVote(greece, rome, egypt));
	}

	[Fact]
	public void AIAbstainsWhenItHasMetNeitherCandidate() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player greece = MakePlayer("Greeks", 2);

		Assert.Null(UnitedNations.AIVote(greece, rome, egypt));
	}

	[Fact]
	public void AIAbstainsWhenAtWarWithEveryCandidate() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player greece = MakePlayer("Greeks", 2);
		Meet(rome, egypt, greece);
		PlayerRelationship.DeclareWar(greece, egypt, false, 0);
		PlayerRelationship.DeclareWar(greece, rome, false, 0);

		Assert.Null(UnitedNations.AIVote(greece, rome, egypt));
	}

	[Fact]
	public void AIPrefersAnAllyAndBreaksTiesForTheSmallerCandidate() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player greece = MakePlayer("Greeks", 2);
		Meet(rome, egypt, greece);

		// Equally liked: the smaller civ gets the vote.
		Assert.Equal(rome, UnitedNations.AIVote(greece, rome, egypt));

		PlayerRelationship.RegisterMultiTurnDeal(greece, egypt, MultiTurnDeal.DEFAULT_MUTUAL_PROTECTION_PACT);
		Assert.Equal(egypt, UnitedNations.AIVote(greece, rome, egypt));

		// Caught spies count against a candidate.
		greece.playerRelationships[egypt.id].espionageIncidents = 10;
		Assert.Equal(rome, UnitedNations.AIVote(greece, rome, egypt));
	}

	[Fact]
	public void VotesAreWeightedByPopulationAndNeedAMajority() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player greece = MakePlayer("Greeks", 6);
		BuildUnitedNations(rome);
		Meet(rome, egypt, greece);
		PlayerRelationship.DeclareWar(greece, egypt, false, 0);

		UnitedNations.ElectionResult result = UnitedNations.HoldElection(gameData);
		Assert.Equal(rome, result.candidateA);
		Assert.Equal(egypt, result.candidateB);
		Assert.Equal(9, result.votesForA);
		Assert.Equal(7, result.votesForB);
		Assert.Equal(0, result.abstentions);
		Assert.Equal(rome, result.winner);
	}

	[Fact]
	public void AbstentionsCanDenyAMajority() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 4);
		Player greece = MakePlayer("Greeks", 2);
		BuildUnitedNations(rome);
		Meet(rome, egypt);

		// 3 for Rome, 4 for Egypt, 2 abstaining: no majority of 9.
		UnitedNations.ElectionResult result = UnitedNations.HoldElection(gameData);
		Assert.Equal(2, result.abstentions);
		Assert.Null(result.ballots[greece]);
		Assert.Null(result.winner);
	}

	[Fact]
	public void HumansVoteAsTheyChoseAndOtherwiseAbstain() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player human = MakePlayer("Greeks", 6, human: true);
		BuildUnitedNations(rome);
		Meet(rome, egypt, human);
		gameData.unitedNations.votingTurn = gameData.turn;

		Assert.True(UnitedNations.HumanShouldVote(gameData, human));
		Assert.Null(UnitedNations.Vote(gameData, human, rome, egypt));

		Assert.True(UnitedNations.CastHumanVote(gameData, human, rome));
		Assert.False(UnitedNations.HumanShouldVote(gameData, human));
		Assert.Equal(rome, UnitedNations.Vote(gameData, human, rome, egypt));

		// Only candidates can be voted for.
		Assert.False(UnitedNations.CastHumanVote(gameData, human, human));
	}

	[Fact]
	public void HumanIsAskedToVoteOnlyOnTheVotingTurn() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player human = MakePlayer("Greeks", 6, human: true);
		BuildUnitedNations(rome);
		Meet(rome, egypt, human);
		gameData.unitedNations.votingTurn = gameData.turn + 1;

		UnitedNations.AskHumanToVote(gameData, human);
		Assert.Empty(EngineStorage.messagesToUI.OfType<MsgShowUnitedNationsVote>());

		gameData.unitedNations.votingTurn = gameData.turn;
		UnitedNations.AskHumanToVote(gameData, human);
		MsgShowUnitedNationsVote msg = Assert.Single(EngineStorage.messagesToUI.OfType<MsgShowUnitedNationsVote>());
		Assert.Equal(human, msg.recipient);
		Assert.Equal(rome, msg.candidateA);
		Assert.Equal(egypt, msg.candidateB);
	}

	[Fact]
	public void ElectionIsScheduledThenHeldAndTheWinnerGetsADiplomaticVictory() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player greece = MakePlayer("Greeks", 6);
		BuildUnitedNations(rome);
		Meet(rome, egypt, greece);
		PlayerRelationship.DeclareWar(greece, egypt, false, 0);
		gameData.victories.Add(new DiplomaticVictory());

		// The round that finds the UN schedules a vote for the coming turn.
		Assert.Null(UnitedNations.ProcessEndOfRound(gameData));
		Assert.Equal(50, gameData.unitedNations.votingTurn);
		TurnHandling.CheckVictory(gameData);
		Assert.False(gameData.gameOver);

		// Once that turn has been played, the votes are counted.
		gameData.turn = 51;
		UnitedNations.ElectionResult result = UnitedNations.ProcessEndOfRound(gameData);
		Assert.Equal(rome, result.winner);
		Assert.Equal(rome.id.ToString(), gameData.unitedNations.secretaryGeneral);
		Assert.Single(EngineStorage.messagesToUI.OfType<MsgUnitedNationsElectionResult>());

		TurnHandling.CheckVictory(gameData);
		Assert.True(gameData.gameOver);
		Assert.Equal(rome, gameData.winner);
		Assert.IsType<DiplomaticVictory>(EngineStorage.messagesToUI.OfType<MsgVictory>().Single().victory);
	}

	[Fact]
	public void VotesAreCountedForTheCandidatesTheHumanWasAskedAbout() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player greece = MakePlayer("Greeks", 6);
		Player human = MakePlayer("Persians", 2, human: true);
		BuildUnitedNations(rome);
		Meet(rome, egypt, greece, human);

		// The vote is called as the voting turn begins.
		Assert.Null(UnitedNations.ProcessEndOfRound(gameData));
		Assert.Equal(egypt.id.ToString(), gameData.unitedNations.candidateB);
		UnitedNations.AskHumanToVote(gameData, human);
		MsgShowUnitedNationsVote msg = Assert.Single(EngineStorage.messagesToUI.OfType<MsgShowUnitedNationsVote>());
		Assert.Equal(egypt, msg.candidateB);
		Assert.True(UnitedNations.CastHumanVote(gameData, human, egypt));

		// Greece overtakes Egypt during the voting turn, but the human's
		// vote still counts.
		for (int i = 0; i < 4; i++) {
			greece.cities[0].residents.Add(new CityResident { city = greece.cities[0] });
		}
		Assert.Equal(greece, UnitedNations.Candidates(gameData).Value.rival);
		gameData.turn = 51;
		UnitedNations.ElectionResult result = UnitedNations.ProcessEndOfRound(gameData);
		Assert.Equal(egypt, result.candidateB);
		Assert.Equal(egypt, result.ballots[human]);
		Assert.Null(gameData.unitedNations.candidateA);
		Assert.Null(gameData.unitedNations.candidateB);
	}

	[Fact]
	public void CandidatesAreStoredWhenFirstNeededInOlderSaves() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 7);
		Player human = MakePlayer("Greeks", 2, human: true);
		BuildUnitedNations(rome);
		Meet(rome, egypt, human);
		gameData.unitedNations.votingTurn = gameData.turn;

		Assert.True(UnitedNations.CastHumanVote(gameData, human, egypt));
		Assert.Equal(rome.id.ToString(), gameData.unitedNations.candidateA);
		Assert.Equal(egypt.id.ToString(), gameData.unitedNations.candidateB);
	}

	[Fact]
	public void FailedVoteSchedulesTheNextOne() {
		Player rome = MakePlayer("Romans", 3);
		Player egypt = MakePlayer("Egyptians", 4);
		MakePlayer("Greeks", 2);
		BuildUnitedNations(rome);
		Meet(rome, egypt);
		gameData.unitedNations.votingTurn = 49;

		UnitedNations.ElectionResult result = UnitedNations.ProcessEndOfRound(gameData);
		Assert.Null(result.winner);
		Assert.Null(gameData.unitedNations.secretaryGeneral);
		Assert.Equal(49 + UnitedNations.ElectionInterval, gameData.unitedNations.votingTurn);
	}

	[Fact]
	public void NoElectionsWithoutDiplomaticVictory() {
		gameData.victoryConditions.AllowDiplomaticVictory = false;
		Player rome = MakePlayer("Romans", 3);
		MakePlayer("Egyptians", 4);
		BuildUnitedNations(rome);

		Assert.Null(UnitedNations.ProcessEndOfRound(gameData));
		Assert.Equal(-1, gameData.unitedNations.votingTurn);
	}

	[Fact]
	public void DiplomaticVictoryIsRegisteredWhenAllowed() {
		SaveGame save = new() { VictoryConditions = new VictoryConditions { AllowDiplomaticVictory = true } };
		Assert.True(VictoryConditions.NewGameDefaults().AllowDiplomaticVictory);
		Assert.True(new DiplomaticVictory().HasVictory(new VictoryStatus { ElectedSecretaryGeneral = true }));
		Assert.False(new DiplomaticVictory().HasVictory(new VictoryStatus()));
		Assert.NotNull(save.UnitedNations);
	}
}

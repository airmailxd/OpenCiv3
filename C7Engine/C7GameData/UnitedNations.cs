using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7GameData {
	// The saved state of the United Nations elections. Players are stored by
	// their ID strings so the state serializes as it is.
	public class UnitedNationsState {
		// The turn during which the next vote is cast: humans are asked for
		// their vote at the start of that turn, and the votes are counted
		// once every player has played it. -1 if no vote is scheduled.
		public int votingTurn = -1;

		// The votes human players cast for the coming election: the voter's
		// ID to the ID of the candidate they voted for, or "" to abstain.
		public Dictionary<string, string> humanVotes = new();

		// The IDs of the two candidates in the coming election, stored when
		// the voting turn begins so the humans are asked about, and the votes
		// counted for, the same two civs. Null outside a voting turn, and in
		// saves from before they were stored, which store them when first
		// needed.
		public string candidateA;
		public string candidateB;

		// The player elected Secretary General, who has won a diplomatic
		// victory, or null.
		public string secretaryGeneral;
	}
}

namespace C7Engine {
	// Civ3's diplomatic victory. Once the United Nations (a great wonder with
	// the "allow diplomatic victory" flag) is built, and the game allows a
	// diplomatic victory, the civs vote every few turns for a Secretary
	// General. The candidates are the owner of the UN and the most populous
	// other civ. Every civ votes, weighted by its population, and a candidate
	// elected by a majority of the world's population wins.
	//
	// Assumptions where Civ3's rules are uncertain:
	// - Votes are held every ElectionInterval (10) turns, the first one on the
	//   turn after the UN is completed.
	// - The second candidate is the civ with the most citizens other than the
	//   UN owner (ties go to the civ listed first).
	// - Each civ's vote is weighted by its population (citizens), and a
	//   candidate needs more than half the votes of every living civ,
	//   abstentions included, to win.
	// - A civ may only vote for a candidate it has met; one that has met
	//   neither abstains. Candidates vote for themselves.
	public static class UnitedNations {
		private static readonly ILogger log = Log.ForContext(typeof(UnitedNations));

		public const int ElectionInterval = 10;

		public class ElectionResult {
			public Player candidateA;
			public Player candidateB;
			public int votesForA;
			public int votesForB;
			public int abstentions;
			// Every voter, and the candidate they voted for (null to abstain).
			public Dictionary<Player, Player> ballots = new();
			public Player winner;

			public int TotalVotes => votesForA + votesForB + abstentions;
		}

		public static bool DiplomaticVictoryAllowed(GameData gameData) {
			return gameData.victoryConditions?.AllowDiplomaticVictory == true;
		}

		// The city with the United Nations, or null if it hasn't been built.
		public static City City(GameData gameData) {
			foreach (City city in gameData.cities) {
				if (city.owner == null || city.owner.defeated || city.owner.isBarbarians) {
					continue;
				}
				foreach (CityBuilding cb in city.constructed_buildings) {
					if (cb.building.allowDiplomaticVictory) {
						return city;
					}
				}
			}
			return null;
		}

		public static Player Owner(GameData gameData) {
			return City(gameData)?.owner;
		}

		public static int Population(Player player) {
			int result = 0;
			foreach (City c in player.cities) {
				result += c.residents.Count;
			}
			return result;
		}

		private static IEnumerable<Player> Voters(GameData gameData) {
			return gameData.players.Where(p => !p.isBarbarians && !p.defeated && p.isIncludedInGame);
		}

		// The two candidates: the UN owner, and the most populous other civ.
		// Null if there is no UN or no rival.
		public static (Player owner, Player rival)? Candidates(GameData gameData) {
			Player owner = Owner(gameData);
			if (owner == null) {
				return null;
			}
			Player rival = null;
			int rivalPopulation = -1;
			foreach (Player p in Voters(gameData)) {
				if (p == owner) {
					continue;
				}
				int population = Population(p);
				if (population > rivalPopulation) {
					rival = p;
					rivalPopulation = population;
				}
			}
			return rival == null ? null : (owner, rival);
		}

		// The candidates in the election under way: the ones stored when the
		// vote was called, or else the current ones, which are stored if this
		// is the voting turn. A stored candidate who has since been destroyed
		// is replaced. Null if there is no UN or no rival.
		public static (Player a, Player b)? BallotCandidates(GameData gameData) {
			UnitedNationsState state = gameData.unitedNations;
			if (state?.candidateA != null && state.candidateB != null) {
				Player a = gameData.players.Find(p => p.id.ToString() == state.candidateA);
				Player b = gameData.players.Find(p => p.id.ToString() == state.candidateB);
				if (a != null && b != null && !a.defeated && !b.defeated) {
					return (a, b);
				}
			}
			var candidates = Candidates(gameData);
			if (state != null && state.votingTurn == gameData.turn) {
				StoreCandidates(state, candidates);
			}
			return candidates;
		}

		private static void StoreCandidates(UnitedNationsState state, (Player a, Player b)? candidates) {
			state.candidateA = candidates?.a.id.ToString();
			state.candidateB = candidates?.b.id.ToString();
		}

		// Whether a human player is asked for a vote this turn: a vote is due
		// at the end of it, and the human isn't a candidate (who vote for
		// themselves) and hasn't voted yet.
		public static bool HumanShouldVote(GameData gameData, Player player) {
			UnitedNationsState state = gameData.unitedNations;
			if (state == null || !player.isHuman || player.defeated || !DiplomaticVictoryAllowed(gameData)
				|| state.votingTurn != gameData.turn || state.humanVotes.ContainsKey(player.id.ToString())) {
				return false;
			}
			var candidates = BallotCandidates(gameData);
			if (candidates == null) {
				return false;
			}
			var (a, b) = candidates.Value;
			return player != a && player != b && (HasMet(player, a) || HasMet(player, b));
		}

		// Asks a human for their vote if one is due.
		public static void AskHumanToVote(GameData gameData, Player player) {
			if (!HumanShouldVote(gameData, player)) {
				return;
			}
			var (a, b) = BallotCandidates(gameData).Value;
			new MsgShowUnitedNationsVote(player, HasMet(player, a) ? a : null, HasMet(player, b) ? b : null).send();
		}

		// Records a human's vote; a null candidate abstains. Votes for anyone
		// but a candidate they have met are ignored.
		public static bool CastHumanVote(GameData gameData, Player voter, Player candidate) {
			UnitedNationsState state = gameData.unitedNations;
			if (state == null || state.votingTurn != gameData.turn) {
				return false;
			}
			var candidates = BallotCandidates(gameData);
			if (candidates == null) {
				return false;
			}
			var (a, b) = candidates.Value;
			if (candidate != null && ((candidate != a && candidate != b) || !HasMet(voter, candidate))) {
				return false;
			}
			state.humanVotes[voter.id.ToString()] = candidate?.id.ToString() ?? "";
			return true;
		}

		private static bool HasMet(Player voter, Player candidate) {
			return PlayerRelationship.TryGetRelationship(voter, candidate, out _);
		}

		// Called once every player has played a turn, after the turn counter
		// has moved on. Schedules the first vote once the UN exists, and counts
		// the votes when one is due.
		public static ElectionResult ProcessEndOfRound(GameData gameData) {
			UnitedNationsState state = gameData.unitedNations ??= new UnitedNationsState();
			if (!DiplomaticVictoryAllowed(gameData) || state.secretaryGeneral != null) {
				return null;
			}

			Player owner = Owner(gameData);
			if (owner == null) {
				state.votingTurn = -1;
				state.humanVotes.Clear();
				StoreCandidates(state, null);
				return null;
			}

			if (state.votingTurn < 0) {
				state.votingTurn = gameData.turn;
				log.Information("The United Nations, owned by {Owner}, will vote on turn {Turn}", owner, state.votingTurn);
			}

			// The voting turn is beginning, so call the vote.
			if (gameData.turn == state.votingTurn) {
				StoreCandidates(state, Candidates(gameData));
				return null;
			}
			if (gameData.turn < state.votingTurn) {
				return null;
			}

			ElectionResult result = HoldElection(gameData);
			state.humanVotes.Clear();
			StoreCandidates(state, null);
			state.votingTurn = gameData.turn + ElectionInterval - 1;
			if (result == null) {
				return null;
			}

			if (result.winner != null) {
				state.secretaryGeneral = result.winner.id.ToString();
			}
			log.Information("United Nations vote: {A} {VotesA}, {B} {VotesB}, {Abstentions} abstaining; winner {Winner}",
				result.candidateA, result.votesForA, result.candidateB, result.votesForB, result.abstentions, result.winner);
			new MsgUnitedNationsElectionResult(result.candidateA, result.candidateB, result.votesForA, result.votesForB,
				result.abstentions, result.winner).send();
			return result;
		}

		// Counts the votes of every civ for the candidates of the vote that
		// was called. Doesn't change any state once the voting turn is over.
		public static ElectionResult HoldElection(GameData gameData) {
			var candidates = BallotCandidates(gameData);
			if (candidates == null) {
				return null;
			}
			var (a, b) = candidates.Value;
			ElectionResult result = new() { candidateA = a, candidateB = b };
			foreach (Player voter in Voters(gameData)) {
				Player choice = Vote(gameData, voter, a, b);
				int weight = Population(voter);
				result.ballots[voter] = choice;
				if (choice == a) {
					result.votesForA += weight;
				} else if (choice == b) {
					result.votesForB += weight;
				} else {
					result.abstentions += weight;
				}
			}
			int total = result.TotalVotes;
			if (total > 0) {
				if (2 * result.votesForA > total) {
					result.winner = a;
				} else if (2 * result.votesForB > total) {
					result.winner = b;
				}
			}
			return result;
		}

		// Who the voter votes for, or null to abstain.
		public static Player Vote(GameData gameData, Player voter, Player a, Player b) {
			if (voter == a || voter == b) {
				return voter;
			}
			if (voter.isHuman) {
				UnitedNationsState state = gameData.unitedNations;
				if (state != null && state.humanVotes.TryGetValue(voter.id.ToString(), out string vote)) {
					if (vote == a.id.ToString() && HasMet(voter, a)) return a;
					if (vote == b.id.ToString() && HasMet(voter, b)) return b;
				}
				return null;
			}
			return AIVote(voter, a, b);
		}

		// An AI votes for the candidate it likes better, judged by war, treaties
		// and grievances. If it is at war with every candidate it has met, it
		// abstains. Between equally liked candidates it votes for the smaller
		// one, so as not to hand the world to the strongest civ.
		public static Player AIVote(Player voter, Player a, Player b) {
			int? scoreA = Opinion(voter, a);
			int? scoreB = Opinion(voter, b);
			if (scoreA == null && scoreB == null) {
				return null;
			}
			if (scoreB == null || (scoreA != null && scoreA > scoreB)) {
				return scoreA <= AtWarOpinion ? null : a;
			}
			if (scoreA == null || scoreB > scoreA) {
				return scoreB <= AtWarOpinion ? null : b;
			}
			// A tie.
			if (scoreA <= AtWarOpinion) {
				return null;
			}
			return Population(a) <= Population(b) ? a : b;
		}

		// An opinion at or below this is no better than being at war.
		private const int AtWarOpinion = 0;

		// How much the voter likes the candidate; higher is better, and at or
		// below zero means at war. Null if they haven't met.
		internal static int? Opinion(Player voter, Player candidate) {
			if (!PlayerRelationship.TryGetRelationship(voter, candidate, out PlayerRelationship pr)) {
				return null;
			}
			if (pr.AtWar()) {
				return -100 - 10 * pr.warDeclarationCount;
			}
			int opinion = 100;
			foreach (MultiTurnDeal deal in pr.multiTurnDeals) {
				opinion += deal.dealSubType switch {
					DealSubType.MilitaryAlliance => 40,
					DealSubType.MutualProtectionPact => 30,
					DealSubType.RightOfPassage => 10,
					DealSubType.GoldPerTurn or DealSubType.ResourcePerTurn or DealSubType.LuxuryPerTurn => 5,
					_ => 0,
				};
			}
			if (voter.alliance != null && voter.alliance == candidate.alliance) {
				opinion += 50;
			}
			string cultureGroup = voter.civilization?.cultureGroup?.name;
			if (cultureGroup != null && cultureGroup == candidate.civilization?.cultureGroup?.name) {
				opinion += 5;
			}
			opinion -= 10 * pr.warDeclarationCount;
			opinion -= 10 * pr.warDeclarationWithRoPActiveCount;
			opinion -= pr.wasSneakAttacked ? 20 : 0;
			opinion -= 5 * pr.espionageIncidents;
			opinion -= 15 * pr.nuclearAtrocityCount;
			// Peace still counts for something, however bad the history.
			return System.Math.Max(opinion, AtWarOpinion + 1);
		}
	}
}

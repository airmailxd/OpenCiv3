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

		// The IDs of the (two or three) candidates in the coming election,
		// stored when the voting turn begins so the humans are asked about,
		// and the votes counted for, the same civs. Null outside a voting
		// turn, and in saves from before they were stored, which store them
		// when first needed. candidateC is null when there are only two
		// candidates, and in saves from before there could be three.
		public string candidateA;
		public string candidateB;
		public string candidateC;

		// The player elected Secretary General, who has won a diplomatic
		// victory, or null.
		public string secretaryGeneral;
	}
}

namespace C7Engine {
	// Civ3's diplomatic victory. Once the United Nations (a great wonder with
	// the "allow diplomatic victory" flag) is built, and the game allows a
	// diplomatic victory, the civs vote every few turns for a Secretary
	// General.
	//
	// The CivFanatics Civ3 FAQ (https://civfanatics.com/civ3/faq/):
	//   "Each civilization gets 1 vote, and can vote for one of the
	//   candidates. At maximum, there can be three candidates: the
	//   civilization that builds the UN is always a candidate, and then if
	//   civilization(s) have 25% of the land OR population, they will also be
	//   eligible. If there are no civilizations with 25% land or population,
	//   then the civilization with the highest score becomes the second
	//   candidate. If a civilization gains a majority of the votes in the
	//   election, they become Secretary General and win a diplomatic victory!
	//   If not, the election is inconclusive. The choice for the founder of
	//   the UN to have elections comes around every 11 turns"
	//
	// Choices the FAQ doesn't settle (UNVERIFIED):
	// - A vote is held every ElectionInterval (11) turns, the first one on the
	//   turn after the UN is completed; the founder isn't asked whether to
	//   hold it.
	// - "Land" is the share of the world's land tiles counted for domination
	//   inside a civ's borders, and "population" its share of the world's
	//   citizens. Should more than two other civs reach 25%, the two with the
	//   largest share (of land or population, whichever is larger) stand.
	// - Score is the civ's accumulated score; ties go to the more populous
	//   civ, then to the civ listed first. The UN owner never counts as the
	//   highest-scoring civ, as it is a candidate already.
	// - A majority means more than half of the votes of every living civ,
	//   abstentions included.
	// - A civ may only vote for a candidate it has met; one that has met none
	//   abstains. Candidates vote for themselves.
	public static class UnitedNations {
		private static readonly ILogger log = Log.ForContext(typeof(UnitedNations));

		public const int ElectionInterval = 11;

		// The most candidates in an election, and the share of the world's
		// land or population that makes a civ one.
		public const int MaxCandidates = 3;
		public const double CandidateShare = 0.25;

		public class ElectionResult {
			public List<Player> candidates = new();
			// The votes for each candidate, in the order of candidates.
			public List<int> votes = new();
			public int abstentions;
			// Every voter, and the candidate they voted for (null to abstain).
			public Dictionary<Player, Player> ballots = new();
			public Player winner;

			public int VotesFor(Player candidate) {
				int i = candidates.IndexOf(candidate);
				return i < 0 ? 0 : votes[i];
			}

			public int TotalVotes => votes.Sum() + abstentions;
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
			// A LAN guest may not be sent the city, so the host says.
			if (gameData.hostFacts != null) {
				string owner = gameData.hostFacts.unitedNationsOwner;
				return owner == null ? null : gameData.players.Find(p => p.id.ToString() == owner);
			}
			return City(gameData)?.owner;
		}

		public static int Population(Player player) {
			int result = 0;
			foreach (City c in player.cities) {
				result += c.residents.Count;
			}
			return result;
		}

		// The tiles of the civ's territory counted as its land.
		private static int Land(Player player) {
			return player.tileKnowledge.DominationTiles().Count;
		}

		private static int Score(GameData gameData, Player player) {
			return gameData.history != null && gameData.history.TryGetValue(player.HistoryKey, out List<HistTurnRecord> turns)
				? turns.LastOrDefault()?.Score ?? 0
				: 0;
		}

		private static IEnumerable<Player> Voters(GameData gameData) {
			return gameData.players.Where(p => !p.isBarbarians && !p.defeated && p.isIncludedInGame);
		}

		// The candidates: the UN owner first, then up to two civs with 25% of
		// the world's land or population, or if there are none, the
		// highest-scoring other civ. Null if there is no UN or no rival.
		public static List<Player> Candidates(GameData gameData) {
			Player owner = Owner(gameData);
			if (owner == null) {
				return null;
			}
			List<Player> rivals = Voters(gameData).Where(p => p != owner).ToList();
			if (rivals.Count == 0) {
				return null;
			}

			int worldLand = 0;
			foreach (Tile t in gameData.map?.tiles ?? []) {
				if (t.IsCountedForDomination()) {
					++worldLand;
				}
			}
			int worldPopulation = Voters(gameData).Sum(Population);
			double Share(int part, int whole) => whole == 0 ? 0 : (double)part / whole;

			List<Player> result = [owner];
			// OrderBy is stable, so ties go to the civ listed first.
			result.AddRange(rivals
				.Select(p => (player: p, share: System.Math.Max(Share(Land(p), worldLand), Share(Population(p), worldPopulation))))
				.Where(ps => ps.share >= CandidateShare)
				.OrderByDescending(ps => ps.share)
				.Take(MaxCandidates - 1)
				.Select(ps => ps.player));
			if (result.Count == 1) {
				result.Add(rivals.OrderByDescending(p => Score(gameData, p)).ThenByDescending(Population).First());
			}
			return result;
		}

		// The candidates in the election under way: the ones stored when the
		// vote was called, or else the current ones, which are stored if this
		// is the voting turn. If a stored candidate has since been destroyed,
		// the candidates are chosen again. Null if there is no UN or no rival.
		public static List<Player> BallotCandidates(GameData gameData) {
			UnitedNationsState state = gameData.unitedNations;
			if (state?.candidateA != null && state.candidateB != null) {
				List<Player> stored = new();
				foreach (string id in new[] { state.candidateA, state.candidateB, state.candidateC }) {
					if (id != null) {
						stored.Add(gameData.players.Find(p => p.id.ToString() == id));
					}
				}
				if (stored.All(p => p != null && !p.defeated)) {
					return stored;
				}
			}
			List<Player> candidates = Candidates(gameData);
			if (state != null && state.votingTurn == gameData.turn) {
				StoreCandidates(state, candidates);
			}
			return candidates;
		}

		private static void StoreCandidates(UnitedNationsState state, List<Player> candidates) {
			state.candidateA = candidates?.ElementAtOrDefault(0)?.id.ToString();
			state.candidateB = candidates?.ElementAtOrDefault(1)?.id.ToString();
			state.candidateC = candidates?.ElementAtOrDefault(2)?.id.ToString();
		}

		// Whether a human player is asked for a vote this turn: a vote is due
		// at the end of it, and the human isn't a candidate (who vote for
		// themselves), has met a candidate, and hasn't voted yet.
		public static bool HumanShouldVote(GameData gameData, Player player) {
			UnitedNationsState state = gameData.unitedNations;
			if (state == null || !player.isHuman || player.defeated || !DiplomaticVictoryAllowed(gameData)
				|| state.votingTurn != gameData.turn || state.humanVotes.ContainsKey(player.id.ToString())) {
				return false;
			}
			List<Player> candidates = BallotCandidates(gameData);
			return candidates != null && !candidates.Contains(player) && candidates.Any(c => HasMet(player, c));
		}

		// Asks a human for their vote if one is due, offering the candidates
		// they have met.
		public static void AskHumanToVote(GameData gameData, Player player) {
			if (!HumanShouldVote(gameData, player)) {
				return;
			}
			new MsgShowUnitedNationsVote(player, BallotCandidates(gameData).Where(c => HasMet(player, c)).ToArray()).send();
		}

		// Records a human's vote; a null candidate abstains. Votes for anyone
		// but a candidate they have met are ignored.
		public static bool CastHumanVote(GameData gameData, Player voter, Player candidate) {
			UnitedNationsState state = gameData.unitedNations;
			if (state == null || state.votingTurn != gameData.turn) {
				return false;
			}
			List<Player> candidates = BallotCandidates(gameData);
			if (candidates == null) {
				return false;
			}
			if (candidate != null && (!candidates.Contains(candidate) || !HasMet(voter, candidate))) {
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
			log.Information("United Nations vote: {Candidates} got {Votes}, {Abstentions} abstaining; winner {Winner}",
				result.candidates, result.votes, result.abstentions, result.winner);
			new MsgUnitedNationsElectionResult(result.candidates, result.votes, result.abstentions, result.winner).send();
			return result;
		}

		// Counts the votes of every civ, one each, for the candidates of the
		// vote that was called. Doesn't change any state once the voting turn
		// is over.
		public static ElectionResult HoldElection(GameData gameData) {
			List<Player> candidates = BallotCandidates(gameData);
			if (candidates == null) {
				return null;
			}
			ElectionResult result = new() { candidates = candidates, votes = candidates.Select(_ => 0).ToList() };
			Player[] ballot = candidates.ToArray();
			foreach (Player voter in Voters(gameData)) {
				Player choice = Vote(gameData, voter, ballot);
				result.ballots[voter] = choice;
				int i = choice == null ? -1 : candidates.IndexOf(choice);
				if (i >= 0) {
					++result.votes[i];
				} else {
					++result.abstentions;
				}
			}
			int total = result.TotalVotes;
			for (int i = 0; i < candidates.Count; i++) {
				if (total > 0 && 2 * result.votes[i] > total) {
					result.winner = candidates[i];
				}
			}
			return result;
		}

		// Who the voter votes for, or null to abstain.
		public static Player Vote(GameData gameData, Player voter, params Player[] candidates) {
			if (candidates.Contains(voter)) {
				return voter;
			}
			if (voter.isHuman) {
				UnitedNationsState state = gameData.unitedNations;
				if (state != null && state.humanVotes.TryGetValue(voter.id.ToString(), out string vote)) {
					return candidates.FirstOrDefault(c => vote == c.id.ToString() && HasMet(voter, c));
				}
				return null;
			}
			return AIVote(voter, candidates);
		}

		// An AI votes for the candidate it likes best, judged by war, treaties
		// and grievances. If it is at war with every candidate it has met, it
		// abstains. Between equally liked candidates it votes for the smallest
		// one, so as not to hand the world to the strongest civ (further ties
		// go to the candidate listed first).
		public static Player AIVote(Player voter, params Player[] candidates) {
			Player best = null;
			int bestOpinion = int.MinValue;
			foreach (Player candidate in candidates) {
				int? opinion = Opinion(voter, candidate);
				if (opinion == null) {
					continue;
				}
				if (best == null || opinion > bestOpinion
					|| (opinion == bestOpinion && Population(candidate) < Population(best))) {
					best = candidate;
					bestOpinion = opinion.Value;
				}
			}
			return best == null || bestOpinion <= AtWarOpinion ? null : best;
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

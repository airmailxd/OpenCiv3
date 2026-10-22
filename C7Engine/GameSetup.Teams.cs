using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine;

// Teams, not Civ3 (owner's design, 2026-10-21): when a game is set up, each
// civ may be put on a team, numbered from 1 up to the number of civs, unless
// the game is a free-for-all. Teammates are locked allies, as in a Civ3
// scenario's alliances: they have met, are at peace with a mutual protection
// pact, and can't go to war with each other, even in Eternal War. A team wins
// together: conquest is won when only one team is left, and any victory a
// civ wins is its teammates' too (Rules.TeamVictory). A civ alone on its team
// plays for itself.
//
// By default the human players are dealt out over two teams, or four on a
// large or huge world, in turn order, and the computer players after them,
// so the teams come out even.
public partial class GameSetup {
	// The teams players are dealt out over by default on each world size;
	// two on any size not named (owner's choice, 2026-10-21).
	private static readonly Dictionary<string, int> DefaultTeamsByWorldSize = new(StringComparer.OrdinalIgnoreCase) {
		["Large"] = 4,
		["Huge"] = 4,
	};

	public static int DefaultTeamCount(WorldSize size) {
		return size?.name != null && DefaultTeamsByWorldSize.TryGetValue(size.name, out int teams) ? teams : 2;
	}

	// The default team of the player in the given place in turn order (0
	// for the first human, the computer players after the humans), with
	// that many teams to deal them out over.
	public static int DefaultTeam(int place, int teamCount) {
		return place % Math.Max(1, teamCount) + 1;
	}

	public static string TeamName(int team) => $"Team {team}";

	// Puts the players with a team on it. A team with one civ is no
	// alliance, so its civ plays as if it had none.
	private static void SetUpTeams(SaveGame save, List<(SavePlayer player, int team)> players) {
		List<IGrouping<int, SavePlayer>> teams = players.Where(p => p.team > 0)
			.GroupBy(p => p.team, p => p.player)
			.Where(g => g.Count() > 1)
			.OrderBy(g => g.Key)
			.ToList();
		if (teams.Count == 0) {
			return;
		}
		int sides = teams.Count + players.Count(p => !teams.Any(t => t.Contains(p.player)));
		if (sides < 2) {
			throw new ArgumentException("Every civilization is on the same team, so there is nobody to play against. Put some on another team.");
		}

		foreach (IGrouping<int, SavePlayer> team in teams) {
			Alliance alliance = new(team.Key, TeamName(team.Key));
			save.Alliances.Add(alliance);
			foreach (SavePlayer player in team) {
				player.alliance = alliance.name;
				foreach (SavePlayer other in team.Where(o => o != player)) {
					player.playerRelationships[other.id.ToString()] = new PlayerRelationship {
						firstContactTurn = 0,
						multiTurnDeals = [MultiTurnDeal.DEFAULT_PEACE, MultiTurnDeal.DEFAULT_MUTUAL_PROTECTION_PACT],
					};
				}
			}
		}
		save.Rules.TeamVictory = true;
	}
}

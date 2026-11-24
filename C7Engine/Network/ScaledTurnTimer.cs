using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using C7GameData;

namespace C7Engine.Network;

// Civ3's scaled multiplayer turn timer, by the rules' MultiplayerTurnTimer
// values (the BIQ's GAME section): a turn lasts the base time, and so many
// seconds more for each city and each unit.
//
// How Civ3 applies it, from the Conquests manual (Steam's manual.pdf for
// app 3910, "The Effects of Game Speed", p. 194, and "Multiplayer Game
// Types", pp. 190-191):
//   "Turn-Based: Game Speed determines the amount of time each player has
//    to complete his or her turn. When time elapses, the next player's turn
//    begins."
//   "Simultaneous Movement: Game Speed determines the amount of time all
//    players have to complete each turn. When time elapses, the next turn
//    begins for all players."
//   "As your civilization grows, the game automatically increases the
//    amount of time available to you."
//   "When the turn timer runs out, your turn ends and the next player's
//    turn starts." (Turn-Based); "Each turn lasts until the turn timer has
//    finished counting down." (Simultaneous Movement)
// So in turns one by one, each player is timed by their own cities and
// units; with simultaneous moves one clock times everyone's round. The
// manual doesn't say whose size sets that shared clock; OpenCiv gives it
// the largest of the players' times, so that nobody has less than their
// own empire would get them (our call, 2026-11-22). Running out ends the
// turn, as OpenCiv's fixed timers do (see LanHost.UpdateTurnClock).
//
// Each turn's time is worked out as the humans start moving it, and holds
// for the turn.
public static class ScaledTurnTimer {
	// How many seconds the player has for a turn.
	public static int SecondsFor(Rules rules, Player player) {
		long seconds = (long)rules.MultiplayerTurnTimerBase
			+ (long)rules.MultiplayerTurnTimerPerCity * player.cities.Count
			+ (long)rules.MultiplayerTurnTimerPerUnit * player.units.Count;
		return (int)Math.Clamp(seconds, 0, int.MaxValue);
	}

	// How long the turn is for the players about to move: each one's own
	// time when they move one by one, the largest of theirs when they move
	// at once. Null if they have none (rules with no timer).
	public static TimeSpan? LimitFor(GameData gameData, IEnumerable<ID> playersToMove) {
		int seconds = 0;
		foreach (ID id in playersToMove) {
			if (gameData.GetPlayer(id) is Player player) {
				seconds = Math.Max(seconds, SecondsFor(gameData.rules, player));
			}
		}
		return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
	}

	// The formula, as the setup screens show it: "16 s + 2 s per city + 1 s
	// per unit". A part the rules give no time is left out.
	public static string Describe(Rules rules) {
		if (rules == null) {
			return "the rules' timer";
		}
		List<string> parts = [];
		if (rules.MultiplayerTurnTimerBase != 0 || (rules.MultiplayerTurnTimerPerCity == 0 && rules.MultiplayerTurnTimerPerUnit == 0)) {
			parts.Add($"{Seconds(rules.MultiplayerTurnTimerBase)} s");
		}
		if (rules.MultiplayerTurnTimerPerCity != 0) {
			parts.Add($"{Seconds(rules.MultiplayerTurnTimerPerCity)} s per city");
		}
		if (rules.MultiplayerTurnTimerPerUnit != 0) {
			parts.Add($"{Seconds(rules.MultiplayerTurnTimerPerUnit)} s per unit");
		}
		return string.Join(" + ", parts);
	}

	// The timer's choice as the setup lists show it.
	public static string ChoiceText(Rules rules) => $"Scaled ({Describe(rules)})";

	private static string Seconds(int seconds) => seconds.ToString(CultureInfo.InvariantCulture);
}

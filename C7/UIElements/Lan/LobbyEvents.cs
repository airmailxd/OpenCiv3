using System;
using System.Collections.Generic;
using System.Linq;
using C7Engine.Network;
using C7GameData;

// Who has come and gone in a game's lobby, told as messages: each time the
// seats are shown, what changed since they last were.
public class LobbyEvents {
	// Who was in each seat when last shown, with whether they had lost the
	// connection, and who was watching; null before the first time.
	private Dictionary<ID, (string name, bool disconnected)> occupants;
	private HashSet<string> spectators;

	public void Reset() {
		occupants = null;
		spectators = null;
	}

	public void Note(IEnumerable<SeatInfo> seats, IEnumerable<string> watching, Action<string> say) {
		Dictionary<ID, (string name, bool disconnected)> now = seats.Where(s => !s.isHost)
			.ToDictionary(s => s.playerID, s => (s.takenBy, s.disconnected));
		HashSet<string> watchingNow = watching.ToHashSet();
		if (occupants != null) {
			// Seats may have been renumbered, so who is in some seat is what
			// counts, not which.
			HashSet<string> before = occupants.Values.Where(o => o.name != null).Select(o => o.name).ToHashSet();
			HashSet<string> after = now.Values.Where(o => o.name != null).Select(o => o.name).ToHashSet();
			// A player who renamed their slot is the same player.
			foreach ((ID id, (string name, bool _)) in now) {
				if (name != null && occupants.TryGetValue(id, out var was) && was.name != null && was.name != name
					&& !after.Contains(was.name) && !before.Contains(name)) {
					say($"{was.name} is now {name}.");
					before.Remove(was.name);
					after.Remove(name);
				}
			}
			foreach (string name in after.Except(before)) {
				say($"{name} joined.");
			}
			foreach (string name in before.Except(after).Where(n => !watchingNow.Contains(n))) {
				say($"{name} left.");
			}
			foreach ((ID id, (string name, bool disconnected)) in now) {
				if (name == null || !occupants.TryGetValue(id, out var was) || was.name != name || was.disconnected == disconnected) {
					continue;
				}
				say(disconnected ? $"{name} lost the connection; their slot is held for them." : $"{name} is back.");
			}
			foreach (string name in watchingNow.Except(spectators)) {
				say(before.Contains(name) && !after.Contains(name) ? $"{name} has no slot now, so watches the game." : $"{name} is watching.");
			}
			foreach (string name in spectators.Except(watchingNow)) {
				say($"{name} stopped watching.");
			}
		}
		occupants = now;
		spectators = watchingNow;
	}
}

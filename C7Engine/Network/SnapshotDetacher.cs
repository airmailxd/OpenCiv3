using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using C7GameData.Save;

namespace C7Engine.Network;

// SaveGame.FromGameData builds the map, units, cities and players afresh, but
// hands over much of the rest as the game's own objects: the history, the
// great wonders built, each player's known techs and relationships, the
// rules, and so on. The game goes on changing some of them, so a snapshot
// encoded on another thread could see them change halfway through, or throw.
//
// Detach replaces everything in a snapshot that isn't built afresh with a copy,
// made by a round trip through the save's own compact JSON, which writes it
// exactly as the original would have been written. Those parts are small; the
// large ones (the map, units, cities and what players know of the map) are
// the fresh ones, left as they are.
internal static class SnapshotDetacher {
	// The parts of a save and of its players that FromGameData builds afresh
	// for each save. Anything not named here is copied, so a part added
	// later is safe, if a little slower, until it's added here.
	private static readonly HashSet<string> FreshSaveMembers = ["Map", "Units", "Players", "Cities"];
	// knownTileIndices is a string, which can't change, so it's shared as is.
	// outdatedTiles is built from what the player remembers of the map, like
	// tileKnowledge.
	private static readonly HashSet<string> FreshPlayerMembers = ["tileKnowledge", "knownTileIndices", "outdatedTiles"];

	private static readonly List<Member> SharedSaveMembers = SharedMembers(typeof(SaveGame), FreshSaveMembers);
	private static readonly List<Member> SharedPlayerMembers = SharedMembers(typeof(SavePlayer), FreshPlayerMembers);

	public static void Detach(SaveGame save) {
		SaveGame shared = new();
		foreach (Member member in SharedSaveMembers) {
			member.Set(shared, member.Get(save));
		}
		shared.Players = save.Players.ConvertAll(player => {
			SavePlayer sharedPlayer = new();
			foreach (Member member in SharedPlayerMembers) {
				member.Set(sharedPlayer, member.Get(player));
			}
			return sharedPlayer;
		});

		// Clone would write indented JSON, which takes longer for the same copy.
		SaveGame copy = SaveGame.FromJSON(shared.ToCompactJSON());

		foreach (Member member in SharedSaveMembers) {
			member.Set(save, member.Get(copy));
		}
		for (int i = 0; i < save.Players.Count; ++i) {
			foreach (Member member in SharedPlayerMembers) {
				member.Set(save.Players[i], member.Get(copy.Players[i]));
			}
		}
	}

	private record Member(Func<object, object> Get, Action<object, object> Set);

	private static List<Member> SharedMembers(Type type, HashSet<string> fresh) {
		IEnumerable<Member> fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
			.Where(f => !f.IsInitOnly && !f.IsLiteral && !fresh.Contains(f.Name))
			.Select(f => new Member(f.GetValue, f.SetValue));
		IEnumerable<Member> properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
				&& p.GetSetMethod() != null && !fresh.Contains(p.Name))
			.Select(p => new Member(p.GetValue, p.SetValue));
		return fields.Concat(properties).ToList();
	}
}

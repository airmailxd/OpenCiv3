using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using C7GameData;

namespace C7Engine.AI {
	// Switches for AI changes still on trial. AiTrialTest turns one on for
	// half the civs of a game and compares how they fare against the rest;
	// a change that proves itself becomes how every AI plays, and its switch
	// goes away. Nothing turns these on in a real game, and they aren't
	// saved.
	public static class AiExperiments {
		private static readonly ConditionalWeakTable<Player, HashSet<string>> enabled = new();

		public static bool Has(Player player, string name) {
			return player != null && enabled.TryGetValue(player, out HashSet<string> names) && names.Contains(name);
		}

		public static void Enable(Player player, string name) {
			enabled.GetOrCreateValue(player).Add(name);
		}

		// Lets AiTrialTest count battles and war declarations. Null outside
		// a trial.
		internal static Action<MapUnit, MapUnit, CombatResult> onCombat;
		internal static Action<Player, Player> onWarDeclared;
		// And the AI's hurried production: the city and the gold it paid.
		internal static Action<City, int> onHurry;
		// And culture flips: the city (already the new owner's), the civ
		// it left, and the chance it flipped with.
		internal static Action<City, Player, CultureFlip.Chance> onCultureFlip;
		// And defenceless units taken by an enemy walking onto them
		// (captured, or destroyed by barbarians): the unit, still its
		// owner's, and the taker.
		internal static Action<MapUnit, Player> onUnitTaken;
	}
}

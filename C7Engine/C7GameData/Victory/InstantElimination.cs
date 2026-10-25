using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// Conquests' "instant elimination" rules, Regicide, Mass Regicide and
// Elimination, which take a civ out of the game early. From the Conquests
// Civilopedia (GCON_Short_Game_Modes):
//   Regicide: "each player has a "King" unit. ... if the "King" unit is
//   killed, the controlling player is eliminated from the game. Once the
//   "King" has been eliminated, all units, colonies, airbases, and cities
//   belonging to that player are destroyed, becoming ruins until something
//   new is built on it."
//   Mass Regicide: "each player has several "King" units. ... if all of a
//   player's "King" units are killed, the controlling player is eliminated
//   from the game." (and destroyed in the same way)
//   Elimination: "any civilization that loses a city to an enemy is
//   eliminated from the game." (and destroyed in the same way)
// The BIQ's GAME section sets how many cities a civ may lose (its "city
// elimination count", 1 in conquests.biq), which Civ3's victory status
// screen shows as "Lose cities:" and "Cities lost:" (labels.txt).
//
// Each civ's King is the unit its RACE section names ("unit type for
// king"; in conquests.biq a 1/1/2 unit named after its leader, Lincoln for
// the Americans), and is placed at its starting location when the game
// begins if the GAME section's "autoplace kings" is set.
public static class InstantElimination {
	// How many Kings each civ starts with under Mass Regicide: "you start
	// with 7 kings in mass regicide"
	// (https://forums.civfanatics.com/threads/questions-about-regicide-games.177926/),
	// also "This allows you to begin with 7 (I think) kings"
	// (https://forums.civfanatics.com/threads/mass-regicide.76473/).
	// UNVERIFIED: one poster in the first thread thought it was 6.
	public const int MassRegicideKings = 7;

	public static bool RegicideOn(VictoryConditions conditions) {
		return conditions != null && (conditions.Regicide || conditions.MassRegicide);
	}

	// How many Kings each civ is given when the game begins, or 0 if none.
	public static int KingsToPlace(VictoryConditions conditions) {
		if (!RegicideOn(conditions) || !conditions.AutoPlaceKings) {
			return 0;
		}
		return conditions.MassRegicide ? MassRegicideKings : 1;
	}

	// The unit the civ's Kings are, from those given: the civ's own King, or
	// failing that the first King unit, or null if there is none.
	public static T KingUnitFor<T>(Civilization civ, IEnumerable<T> prototypes, System.Func<T, string> name, System.Func<T, bool> isKing) {
		List<T> kings = prototypes.Where(isKing).ToList();
		if (civ?.kingUnit != null) {
			foreach (T king in kings) {
				if (name(king) == civ.kingUnit) {
					return king;
				}
			}
		}
		return kings.FirstOrDefault();
	}

	public static int KingsLeft(Player player) {
		return player.units.Count(u => u.unitType.isKing);
	}

	// Whether the civ has lost its King (Regicide), all of its Kings (Mass
	// Regicide), or the cities it may lose (Elimination).
	// UNVERIFIED (no Civ3 source found): with both Regicide and Mass
	// Regicide on, losing any King eliminates a civ, as under Regicide. A civ
	// that has lost no King, as when the rules have no King units, isn't
	// eliminated by Regicide.
	public static bool IsEliminated(Player player, VictoryConditions conditions) {
		if (player.IsIndependent || conditions == null) {
			return false;
		}
		if (conditions.Regicide && player.kingsLost > 0) {
			return true;
		}
		if (conditions.MassRegicide && player.kingsLost > 0 && KingsLeft(player) == 0) {
			return true;
		}
		return conditions.CityElimination && player.citiesLost >= System.Math.Max(1, conditions.CityEliminationCount);
	}

	// Notes that the unit, which the player has lost, was one of their
	// Kings.
	public static void OnUnitLost(MapUnit unit, Player owner) {
		if (unit.unitType.isKing && !owner.defeated) {
			++owner.kingsLost;
			// Civ3 FUN_005ca720 records a King lost in a Regicide game, by its
			// name or its unit type's, where it was.
			GameData gameData = EngineStorage.gameData;
			VictoryConditions conditions = gameData?.victoryConditions;
			if (conditions != null && (conditions.Regicide || conditions.MassRegicide) && gameData.gameHistory != null) {
				string name = string.IsNullOrEmpty(unit.name) ? unit.unitType.name : unit.name;
				gameData.gameHistory.Record(gameData.turn, ReplayEventType.KingKilled, owner, unit.location, name);
			}
		}
	}

	// Notes that an enemy has taken (or destroyed in taking) one of the
	// player's cities.
	// UNVERIFIED: only cities taken by force count as "lost to an enemy";
	// cities that flip, are traded away or are abandoned don't. Cities taken
	// by barbarians count.
	public static void OnCityLost(Player owner, Player captor) {
		if (owner != captor && !owner.IsIndependent) {
			++owner.citiesLost;
		}
	}
}

// Regicide on Civ3's victory status screen: "Lose kings: 1" or "Lose
// kings: All", and "Kings left:" (labels.txt). It never wins a game, but
// shows how near a civ is to losing it.
public class RegicideCondition : IVictory {
	private readonly bool all;

	public RegicideCondition(bool all) {
		this.all = all;
	}

	public string Header() => "Regicide";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		return new VictoryStatus {
			Player = player,
			KingsLeft = InstantElimination.KingsLeft(player),
		};
	}

	public bool HasVictory(VictoryStatus status) => false;

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return [
			all ? "Lose kings: All" : "Lose kings: 1",
			"",
			"Kings left:",
			$"{status.KingsLeft}",
			"",
			"",
		];
	}
}

// Elimination on Civ3's victory status screen: "Lose cities:" and "Cities
// lost:" (labels.txt). Like Regicide, it never wins a game.
public class EliminationCondition : IVictory {
	private readonly int citiesToLose;

	public EliminationCondition(int citiesToLose) {
		this.citiesToLose = System.Math.Max(1, citiesToLose);
	}

	public string Header() => "Elimination";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		return new VictoryStatus {
			Player = player,
			CitiesLost = player.citiesLost,
		};
	}

	public bool HasVictory(VictoryStatus status) => false;

	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return [
			"Lose cities:",
			$"{citiesToLose}",
			"Cities lost:",
			$"{status.CitiesLost}",
			"",
			"",
		];
	}
}

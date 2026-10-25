using System.Collections.Generic;
using System.Linq;
using C7GameData;

namespace C7Engine;

// Conquests' victory points. The Mesopotamia conquest's Civilopedia
// (GCON_Victory) gives the schedule:
//   "Wonder Cost (2.5 VP * build cost)
//    Defeating an enemy unit (5 VP * unit cost)
//    Advancement Cost (5 VP * tech cost)
//    City Conquest (100 VP * city population)"
// and its BIQ's GAME section has a wonder multiple of 25, a defeat multiple
// of 5, an advance multiple of 5 and a city conquest multiple of 100, so
// the wonder multiple is in tenths and the others are whole. Elsewhere:
// "When you discover an advance the Cost in the Technology page in the
// editor is multiplied by this value"
// (https://forums.civfanatics.com/threads/the-advance-multiple-vs-tech-rate.163149/),
// and killing barbarians scores too (Ancient Treasures' Civilopedia gives
// its barbarians no build cost "to prevent the player from accruing large
// amounts of victory points by concentrating on killing barbarians").
//
// Holding a Victory Location scores too (see ScoreVictoryLocations).
//
// The points are always kept; they win a game only with Victory Point
// Scoring (the first to the limit wins, as in "If you can amass 30,000
// victory points, you win immediately", Middle Ages) or a Wonder Victory,
// and settle a game that reaches its time limit under Victory Point
// Scoring ("If the game ends and no one has reached 30,000 victory points,
// then the winner is given to the civilization that has amassed the most
// victory points").
public static class VictoryPoints {
	// The BIQ's multiples in conquests.biq.
	public const int DefaultLimit = 50000;
	public const int DefaultWonderMultiple = 10;
	public const int DefaultDefeatMultiple = 5;
	public const int DefaultAdvanceMultiple = 5;
	public const int DefaultCityConquestMultiple = 100;
	public const int DefaultLocationPoints = 25;

	private static void Add(Player player, long points) {
		if (player == null || player.IsIndependent || points <= 0) {
			return;
		}
		player.victoryPoints = (int)System.Math.Min(int.MaxValue, player.victoryPoints + points);
	}

	public static void OnWonderBuilt(Player owner, Building wonder, VictoryConditions conditions) {
		if (conditions != null) {
			Add(owner, (long)conditions.WonderVictoryPoints * wonder.shieldCost / 10);
		}
	}

	// UNVERIFIED: units killed by bombardment or by other means than
	// combat score nothing; the schedule only speaks of defeating them.
	public static void OnUnitDefeated(Player victor, MapUnit defeated, VictoryConditions conditions) {
		if (conditions != null && victor != defeated.owner) {
			Add(victor, (long)conditions.DefeatVictoryPoints * defeated.unitType.shieldCost);
		}
	}

	// UNVERIFIED: advances gained other than by research (traded, stolen,
	// free) score too.
	public static void OnAdvance(Player player, Tech tech, VictoryConditions conditions) {
		if (conditions != null) {
			Add(player, (long)conditions.AdvanceVictoryPoints * tech.Cost);
		}
	}

	// Scored for the city's population as it was taken.
	public static void OnCityConquered(Player captor, City city, VictoryConditions conditions) {
		if (conditions != null && captor != city.owner) {
			Add(captor, (long)conditions.CityConquestVictoryPoints * city.residents.Count);
		}
	}

	// Victory Locations, from the Conquests Civilopedia
	// (DESC_GCON_Short_Game_Modes): "Players score points by capturing and
	// holding Victory Location tiles. To score these points a unit with a
	// defensive military stat must be in that tile at the end of all
	// players' turns for that turn. Each turn a Victory Location is
	// controlled, the controlling player gets 25 victory points."
	// UNVERIFIED: barbarians hold a location without scoring for it.
	public static void ScoreVictoryLocations(GameData gameData) {
		VictoryConditions conditions = gameData.victoryConditions;
		if (conditions == null || !conditions.VictoryLocations || conditions.VictoryLocationPoints <= 0) {
			return;
		}
		foreach (Tile tile in gameData.map.tiles) {
			if (!tile.isVictoryLocation) {
				continue;
			}
			MapUnit holder = tile.unitsOnTile.FirstOrDefault(u => u.unitType.defense > 0 && u.hitPointsRemaining > 0);
			if (holder != null) {
				Add(holder.owner, conditions.VictoryLocationPoints);
			}
		}
	}

	public static int TopRivalPoints(Player player, GameData gameData) {
		int top = 0;
		foreach (Player p in gameData.players) {
			if (p != player && !p.IsIndependent && !p.defeated) {
				top = System.Math.Max(top, p.victoryPoints);
			}
		}
		return top;
	}
}

// Victory Point Scoring: the first civ to the limit wins ("a Victory Point
// Victory", labels.txt).
public class VictoryPointVictory : IVictory {
	private readonly int limit;

	public VictoryPointVictory(int limit) {
		this.limit = limit;
	}

	public string Header() => "Victory Point";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		return new VictoryStatus {
			Player = player,
			VictoryPoints = player.victoryPoints,
			TopRivalVictoryPoints = VictoryPoints.TopRivalPoints(player, gameData),
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return limit > 0 && status.VictoryPoints >= limit;
	}

	// As Civ3's victory status screen words it (labels.txt).
	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return [
			"Limit",
			$"{limit:N0}",
			"Current victory points:",
			$"{status.VictoryPoints:N0}",
			"",
			rivalStatuses.Count > 0 ? $"{rivalStatuses.Max(r => r.VictoryPoints):N0}" : "",
		];
	}
}

// Wonder Victory, from the Conquests Civilopedia (GCON_Short_Game_Modes):
// "If this condition is enabled the game ends as soon as [all] Great
// Wonders have been built. The winner is then determined based on victory
// points -- not necessarily on building the greatest number of wonders, nor
// a specific wonder."
// UNVERIFIED: a tie on victory points goes to the higher score, as other
// ties do (see TurnHandling.CheckVictory).
public class WonderVictory : IVictory {
	public string Header() => "Wonder";

	public VictoryStatus Evaluate(Player player, GameData gameData) {
		List<Building> wonders = gameData.Buildings.Where(b => b.IsGreatWonder()).ToList();
		int mine = player.cities.Sum(c => c.constructed_buildings.Count(cb => cb.building.IsGreatWonder()));
		int built = wonders.Count(w => gameData.GreatWondersBuilt.Contains(w.name));
		return new VictoryStatus {
			Player = player,
			WondersToBuild = wonders.Count,
			WondersBuilt = mine,
			WondersBuiltByRivals = System.Math.Max(0, built - mine),
			AllWondersBuilt = wonders.Count > 0 && built == wonders.Count,
			VictoryPoints = player.victoryPoints,
			TopRivalVictoryPoints = VictoryPoints.TopRivalPoints(player, gameData),
		};
	}

	public bool HasVictory(VictoryStatus status) {
		return status.AllWondersBuilt && status.VictoryPoints >= status.TopRivalVictoryPoints;
	}

	// As Civ3's victory status screen words it (labels.txt).
	public IEnumerable<string[]> GenerateStatusRows(VictoryStatus status, List<VictoryStatus> rivalStatuses) {
		yield return [
			"Total to build:",
			$"{status.WondersToBuild}",
			"Wonders built:",
			$"{status.WondersBuilt}",
			"Total built by rivals:",
			$"{status.WondersBuiltByRivals}",
		];
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// Trials of how fast the AI develops its empire: what its cities build,
	// what it researches, how it hurries production and works its tiles. Each
	// idea is behind its own AiExperiments switch, and all of them are AI
	// preferences, not rules.
	public static class DevelopmentAI {
		private static readonly ILogger log = Log.ForContext(typeof(DevelopmentAI));

		// Once its units cost upkeep or it has stopped expanding, a civ at
		// peace stops adding soldiers when it has a defender and a half per
		// city (three near a rival's city), and builds no more than one scout,
		// nor walls or barracks. Without this the AI kept some 80 land units
		// for 11 cities by turn 200, and paid for most of them.
		public const string LeanMilitary = "lean-military";

		// Research heads for Monarchy, then picks the cheapest tech as
		// before. Cheapest first left Monarchy, among the dearest ancient
		// techs, until nearly all the others were known: the AI spent some
		// 175 of its first 200 turns under Despotism's tile penalty.
		public const string ResearchMonarchy = "research-monarchy";

		// Spends gold above a reserve to finish settlers, workers, defenders
		// of unguarded cities and useful buildings, under governments that
		// hurry with gold. The AI didn't hurry production at all.
		public const string RushBuy = "rush-buy";

		// Cities work their food tiles harder while they are small, or while
		// they build a settler or worker.
		public const string FoodFirst = "food-first";

		////////////////////////////////////////////////////////////////////
		// lean-military

		internal static bool IsMilitaryLandUnit(UnitPrototype unit) {
			return unit.IsLandUnit() && !unit.isWorker && !unit.actions.Contains(UnitAction.BuildCity)
				&& (unit.attack > 0 || unit.defense > 0) && !unit.isArmy;
		}

		// The soldiers we have, or are building, and how many we want in
		// peacetime: a defender and a half per city (three near a rival's
		// city), an escort for each settler, and a couple to spare (e.g.
		// against barbarians).
		internal static (int have, int want) MilitaryNeed(Player player) {
			int have = 0;
			int settlers = 0;
			foreach (MapUnit u in player.units) {
				if (u.unitType.actions.Contains(UnitAction.BuildCity)) {
					++settlers;
				} else if (IsMilitaryLandUnit(u.unitType)) {
					++have;
				}
			}
			int borderCities = 0;
			foreach (City c in player.cities) {
				if (c.itemBeingProduced is UnitPrototype p && IsMilitaryLandUnit(p)) {
					++have;
				} else if (c.itemBeingProduced is UnitPrototype s && s.actions.Contains(UnitAction.BuildCity)) {
					++settlers;
				}
				// On a crowded continent, the cap without this cost cities to
				// the neighbors.
				if (NearRivalCity(player, c)) {
					++borderCities;
				}
			}
			int want = (int)Math.Ceiling((player.cities.Count + borderCities) * 1.5) + settlers + 2;
			return (have, want);
		}

		private const int BorderDistance = 6;

		private static bool NearRivalCity(Player player, City city) {
			foreach (Player other in EngineStorage.gameData.players) {
				if (other == player || other.isBarbarians || other.defeated) {
					continue;
				}
				foreach (City theirs in other.cities) {
					if (theirs.location.map == city.location.map && city.location.DistanceTo(theirs.location) <= BorderDistance
							&& AIFogOfWar.KnowsTile(player, theirs.location)) {
						return true;
					}
				}
			}
			return false;
		}

		internal static float LeanMilitaryAdjustment(ChooseProducible.ProducibleStats stats, UnitPrototype unit) {
			if (stats.atWar) {
				return 0;
			}
			// Scouts and explorers: one is enough. (Capping soldiers alone
			// sent the shields to them instead.)
			if (unit.IsLandUnit() && unit.attack == 0 && unit.defense == 0 && !unit.isWorker
					&& !unit.actions.Contains(UnitAction.BuildCity)) {
				return stats.player.units.Any(u => u.unitType.IsLandUnit() && u.unitType.attack == 0 && u.unitType.defense == 0
					&& !u.unitType.isWorker && !u.unitType.actions.Contains(UnitAction.BuildCity)) ? -15 : 0;
			}
			// A city's own guard and a settler's escort are always wanted.
			if (!IsMilitaryLandUnit(unit) || !stats.CityGuarded || stats.HasUnescortedSettler) {
				return 0;
			}
			// While the empire is still expanding and its units cost nothing
			// to support, soldiers are the cheapest use of shields a small
			// city has: the alternatives cost citizens (workers) or upkeep
			// (temples, walls), and capping soldiers then held back the
			// expansion by some 10 turns.
			if (stats.InExpansionPhase && stats.UnitSupportCost <= 0) {
				return 0;
			}
			(int have, int want) = stats.MilitaryNeed;
			if (have < want) {
				return 0;
			}
			return -10 - 5 * (have - want);
		}

		// Nor walls and barracks in peacetime, which the shields also went to.
		internal static float LeanMilitaryAdjustment(ChooseProducible.ProducibleStats stats, Building building) {
			if (stats.atWar) {
				return 0;
			}
			float score = 0;
			if (building.providesVeteranGroundUnits) {
				score -= 10;
			}
			if (building.providesWalls) {
				score -= 8;
			}
			return score;
		}

		////////////////////////////////////////////////////////////////////
		// research-monarchy

		private static readonly string[] MonarchyGoals = ["Monarchy"];

		// The tech to research next, or null to research as before.
		public static Tech ChooseResearch(Player player, List<Tech> techs, IEnumerable<Tech> available) {
			if (!AiExperiments.Has(player, ResearchMonarchy)) {
				return null;
			}
			List<Tech> availableList = available.ToList();
			foreach (string goalName in MonarchyGoals) {
				Tech goal = techs.Find(t => t.Name == goalName);
				if (goal == null || player.knownTechs.Contains(goal.id)) {
					continue;
				}
				// The cheapest tech we can research now on the way to the goal.
				HashSet<Tech> path = new();
				CollectUnknownPrerequisites(player, goal, path);
				Tech next = availableList.Where(path.Contains).OrderBy(t => t.Cost).FirstOrDefault();
				if (next != null) {
					return next;
				}
			}
			return null;
		}

		private static void CollectUnknownPrerequisites(Player player, Tech tech, HashSet<Tech> result) {
			if (player.knownTechs.Contains(tech.id) || !result.Add(tech)) {
				return;
			}
			foreach (Tech t in tech.Prerequisites) {
				CollectUnknownPrerequisites(player, t, result);
			}
		}

		////////////////////////////////////////////////////////////////////
		// rush-buy

		// Hurries the most valuable items the treasury can pay for, keeping a
		// reserve for upkeep and emergencies.
		public static void HurryProduction(Player player) {
			if (!AiExperiments.Has(player, RushBuy) || player.government.hurryingType != Government.HurryProductionType.PaidLabor) {
				return;
			}
			int reserve = 20 + 3 * player.cities.Count;
			bool atWar = PlayerRelationship.IsInAnyWar(player, EngineStorage.gameData.players);
			List<(City city, float value, int cost)> candidates = new();
			foreach (City city in player.cities) {
				// From an empty box hurrying costs double; wait a turn.
				if (city.hurriedThisTurn || city.itemBeingProduced == null || city.shieldsStored == 0) {
					continue;
				}
				float value = RushValue(city, atWar);
				if (value <= 0) {
					continue;
				}
				// Done next turn anyway.
				if (city.TurnsUntilProductionFinished() <= 1) {
					continue;
				}
				// A settler or worker the city isn't big enough for would only wait.
				if (city.itemBeingProduced.populationCost >= city.residents.Count) {
					continue;
				}
				City.HurryProductionDetails details = city.GetHurryProductionDetails();
				if (details.errorMessage != null || details.goldCost <= 0) {
					continue;
				}
				candidates.Add((city, value, details.goldCost));
			}
			foreach ((City city, float value, int cost) in candidates.OrderByDescending(c => c.value / c.cost)) {
				if (player.gold - cost < reserve) {
					continue;
				}
				log.Debug("{Player} hurries {Item} in {City} for {Cost}", player, city.itemBeingProduced, city, cost);
				city.HurryProduction();
			}
		}

		// How much it is worth to finish the city's item now, or 0 if it
		// isn't worth gold.
		private static float RushValue(City city, bool atWar) {
			if (city.itemBeingProduced is UnitPrototype unit) {
				if (unit.actions.Contains(UnitAction.BuildCity)) {
					return 3;
				}
				if (unit.isWorker) {
					return 2;
				}
				bool guarded = city.location.unitsOnTile.Any(u => u.CanDefendOnLand());
				if (!guarded && unit.defense > 0) {
					return 4;
				}
				return atWar && IsMilitaryLandUnit(unit) ? 1 : 0;
			}
			if (city.itemBeingProduced is Building b) {
				if (b.IsGreatWonder() || b.isSmallWonder) {
					return 0;
				}
				if (b.contentFacesInCity > 0 || b.increasesResearch || b.increasesTax || b.increasesLuxury
						|| b.reducesCorruption || b.allowsCitySize2 || b.tileModifier != null) {
					return 1.5f;
				}
				return 0.5f;
			}
			return 0;
		}

		////////////////////////////////////////////////////////////////////
		// food-first

		// The food surplus the city's citizens aim for, from the usual one.
		public static int DesiredFoodSurplus(City city, int usual) {
			if (!AiExperiments.Has(city.owner, FoodFirst)) {
				return usual;
			}
			if (city.itemBeingProduced is UnitPrototype u && u.populationCost > 0) {
				return usual + 2;
			}
			// Without Despotism's tile penalty, food tiles make more, so it
			// pays to work them a little longer.
			if (city.residents.Count < (city.owner.government.hasTilePenalty ? 3 : 4)) {
				return usual + 1;
			}
			return usual;
		}
	}
}

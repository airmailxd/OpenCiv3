using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine.AI {
	// How fast the AI develops its empire: what it researches, how its
	// cities work their tiles, which units it hurries with gold, and, still
	// on trial behind an AiExperiments switch, what its cities build. All of
	// them are AI preferences, not rules.
	public static class DevelopmentAI {
		private static readonly ILogger log = Log.ForContext(typeof(DevelopmentAI));

		// Once its units cost upkeep or it has stopped expanding, a civ at
		// peace stops adding soldiers when it has a defender and a half per
		// city (three near a rival's city), and builds no more than one scout,
		// nor walls or barracks. Without this the AI kept some 80 land units
		// for 11 cities by turn 200, and paid for most of them.
		public const string LeanMilitary = "lean-military";

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

		internal static bool NearRivalCity(Player player, City city) {
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
		// Research

		// Research heads for Monarchy, then picks the cheapest tech as
		// before. Cheapest first left Monarchy, among the dearest ancient
		// techs, until nearly all the others were known: the AI spent some
		// 175 of its first 200 turns under Despotism's tile penalty. In AI
		// trials (AiTrialTest), civs doing this had about 20% more score
		// after 200 turns, along with the war campaigns and food first.
		private static readonly string[] MonarchyGoals = ["Monarchy"];

		// The tech to research next, or null to research as before.
		public static Tech ChooseResearch(Player player, List<Tech> techs, IEnumerable<Tech> available) {
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
		// Hurrying production

		// Why a unit is worth hurrying, least urgent first.
		private enum RushReason {
			None,
			Worker,
			Settler,
			Escort,
			Campaign,
			Defense,
		}

		// Under governments that hurry with gold, buys the units that matter
		// now, most urgent first: defenders for unguarded or threatened
		// cities, attackers and siege engines for a war campaign, escorts for
		// waiting settlers, settlers, and workers while short of them.
		//
		// Not hurried, as AI trials (AiTrialTest) found no gain or a loss:
		// buildings (the gold went to units first anyway, and with the taxes
		// below they cost beakers); any soldier at war (it mostly bought
		// obsolete warriors and archers); and units bought with citizens
		// under Despotism and the like (fewer units and lower scores).
		// Hurrying as it is, with the taxes at war, civs had some 66 more
		// score after 200 turns over 64 paired games on Continents and
		// Pangaea (+-47; ahead on each map and seed set), most on Pangaea,
		// where they also lost fewer cities.
		public static void HurryProduction(Player player) {
			if (player.isHuman || player.isBarbarians || player.cities.Count == 0
					|| player.government.hurryingType != Government.HurryProductionType.PaidLabor) {
				return;
			}
			City staging = WarCampaign.CampaignStagingCity(player);
			bool atWar = staging != null || PlayerRelationship.IsInAnyWar(player, EngineStorage.gameData.players);
			// The reserve also covers ten turns of any deficit, so that the
			// sliders (PlayerAI.AdjustSliders, which tolerates a deficit of a
			// tenth of the treasury) needn't take beakers for taxes.
			int deficit = Math.Max(0, -player.CalculateGoldPerTurn());
			int reserve = Math.Max(BaseReserve(player), ReserveDeficitTurns * deficit);
			// At war, or gathering for one, gold kept back is worth less than
			// soldiers in the field.
			int warReserve = atWar ? reserve / 2 : reserve;
			int workers = player.units.Count(u => u.unitType.isWorker);

			List<(City city, RushReason reason, int cost)> candidates = new();
			foreach (City city in player.cities) {
				if (city.hurriedThisTurn || city.itemBeingProduced == null) {
					continue;
				}
				RushReason reason = UnitRushReason(player, city, staging, workers);
				if (reason == RushReason.None) {
					continue;
				}
				// From an empty box hurrying costs double; wait a turn unless
				// the city is in danger.
				if (city.shieldsStored == 0 && reason != RushReason.Defense) {
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
				candidates.Add((city, reason, details.goldCost));
			}
			// The most urgent first, and among equals the cheapest (the
			// nearest to done), so the gold buys the most.
			foreach ((City city, RushReason reason, int cost) in candidates.OrderByDescending(c => c.reason).ThenBy(c => c.cost)) {
				int keep = reason switch {
					RushReason.Defense => 0,
					RushReason.Campaign => warReserve,
					_ => reserve,
				};
				if (player.gold - cost < keep) {
					continue;
				}
				log.Debug("{Player} hurries {Item} in {City} for {Cost} ({Reason})", player, city.itemBeingProduced, city, cost, reason);
				city.HurryProduction();
				AiExperiments.onHurry?.Invoke(city, cost);
			}
		}

		// HEURISTIC: the gold kept back from hurrying: a base and so much per
		// city, or ten turns of deficit if that is more. A reserve of 30 and
		// 5 a city left the AI's small treasuries nearly untouched.
		private const int ReserveBase = 10;
		private const int ReservePerCity = 2;
		private const int ReserveDeficitTurns = 10;

		private static int BaseReserve(Player player) {
			return ReserveBase + ReservePerCity * player.cities.Count;
		}

		// The gold per turn the sliders should at least bring in (see
		// PlayerAI.AdjustSliders), or int.MinValue for no more than usual:
		// a gold per city while at war or gathering for one, under
		// governments that hurry with gold, until the treasury holds three
		// times the reserve. The AI otherwise ran its treasury at about
		// nothing a turn, so had little to hurry with when it mattered.
		public static int WantedGoldPerTurn(Player player) {
			if (player.isHuman || player.isBarbarians
					|| player.government.hurryingType != Government.HurryProductionType.PaidLabor
					|| player.gold > 3 * BaseReserve(player)) {
				return int.MinValue;
			}
			if (WarCampaign.CampaignStagingCity(player) == null && !PlayerRelationship.IsInAnyWar(player, EngineStorage.gameData.players)) {
				return int.MinValue;
			}
			return player.cities.Count;
		}

		// HEURISTIC: how many tiles from a campaign's staging city a city may
		// be for the attackers it buys to join the campaign in time.
		private const int CampaignRushDistance = 8;

		private static RushReason UnitRushReason(Player player, City city, City staging, int workers) {
			if (city.itemBeingProduced is not UnitPrototype unit || !unit.IsLandUnit() || unit.isArmy) {
				return RushReason.None;
			}
			if (unit.defense > 0 && (!city.location.unitsOnTile.Any(u => u.owner == player && u.CanDefendOnLand()) || Threatened(player, city))) {
				return RushReason.Defense;
			}
			if (staging != null && city.location.continent == staging.location.continent
					&& city.location.DistanceTo(staging.location) <= CampaignRushDistance) {
				if ((unit.attack > unit.defense && WarCampaign.WantsAttackers(player))
						|| (unit.bombard > 0 && unit.attack == 0 && WarCampaign.WantsBombarders(player))) {
					return RushReason.Campaign;
				}
			}
			if (unit.defense > 0 && unit.defense >= unit.attack
					&& city.location.unitsOnTile.Any(u => u.owner == player && u.currentAI is SettlerAI s && s.data.escort == null)) {
				return RushReason.Escort;
			}
			if (unit.actions.Contains(UnitAction.BuildCity)) {
				return RushReason.Settler;
			}
			if (unit.isWorker && workers < player.cities.Count) {
				return RushReason.Worker;
			}
			return RushReason.None;
		}

		// HEURISTIC: how near an enemy unit must be for its city to be in
		// danger.
		private const int ThreatDistance = 2;

		// A threatened city with fewer real defenders (defense at least
		// attack) than the enemy units near it, or fewer than two, switches
		// to the best defender it can build, unless it's building one, so
		// that hurrying (see HurryProduction's Defense reason) can buy it
		// this turn. Without this a city kept building whatever it had
		// started, say a 20-turn cathedral, with the enemy at its walls.
		// Shields carry over from a building to a unit only partly in Civ3,
		// but a lost city loses them all.
		public static void DefendThreatenedCities(Player player) {
			GameData gameData = EngineStorage.gameData;
			if (gameData == null || player.isHuman || player.isBarbarians) {
				return;
			}
			foreach (City city in player.cities) {
				int enemies = EnemyAttackersNear(player, city);
				if (enemies == 0) {
					continue;
				}
				int defenders = city.location.unitsOnTile.Count(u => u.owner == player && u.CanDefendOnLand()
					&& u.unitType.defense >= u.unitType.attack);
				if (defenders >= Math.Max(2, enemies)) {
					continue;
				}
				if (city.itemBeingProduced is UnitPrototype current && IsDefender(current)) {
					continue;
				}
				UnitPrototype best = city.ListProductionOptions(gameData).OfType<UnitPrototype>().Where(IsDefender)
					.OrderByDescending(p => p.defense).ThenBy(p => p.shieldCost).FirstOrDefault();
				if (best != null) {
					log.Information("{Player} switches {City} to {Unit}, with {Enemies} enemies near and {Defenders} defenders", player, city, best, enemies, defenders);
					city.ChooseProduction(best);
				}
			}
		}

		private static bool IsDefender(UnitPrototype p) {
			return p.IsLandUnit() && !p.isArmy && p.defense > 0 && p.defense >= p.attack && !p.isWorker
				&& !p.actions.Contains(UnitAction.BuildCity);
		}

		// The enemy land units that can attack, which we can see, near the
		// city.
		private static int EnemyAttackersNear(Player player, City city) {
			int count = 0;
			foreach (Tile t in city.location.GetTilesWithinRankDistance(ThreatDistance)) {
				if (!AIFogOfWar.SeesUnitsOn(player, t)) {
					continue;
				}
				foreach (MapUnit u in t.unitsOnTile) {
					if (u.owner != player && u.IsLandUnit() && u.unitType.attack > 0 && !u.owner.IsAtPeaceWith(player)) {
						++count;
					}
				}
			}
			return count;
		}

		// Whether an enemy land unit that can attack stands, as far as we
		// can see, near the city.
		private static bool Threatened(Player player, City city) {
			foreach (Tile t in city.location.GetTilesWithinRankDistance(ThreatDistance)) {
				if (!AIFogOfWar.SeesUnitsOn(player, t)) {
					continue;
				}
				foreach (MapUnit u in t.unitsOnTile) {
					if (u.owner != player && u.IsLandUnit() && u.unitType.attack > 0 && !u.owner.IsAtPeaceWith(player)) {
						return true;
					}
				}
			}
			return false;
		}

		////////////////////////////////////////////////////////////////////
		// Food first

		// The food surplus the citizens of an AI's city aim for, from the
		// usual one: they work their food tiles harder while the city is
		// small, or while it builds a settler or worker. Humans' cities are
		// left as they were.
		public static int DesiredFoodSurplus(City city, int usual) {
			if (city.owner == null || city.owner.isHuman || city.owner.isBarbarians) {
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

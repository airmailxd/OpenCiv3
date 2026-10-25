using System;
using System.Collections.Generic;
using System.Linq;
using C7GameData;
using C7GameData.Save;

namespace C7Engine.AI {
	// HEURISTIC: what a city improvement's effect is worth in this city,
	// added to its score in ChooseProducible.ScoreBuilding. Without it most
	// improvements scored alike (a flat bonus less their cost and upkeep),
	// so a Library went up in a city with no commerce and a SAM Missile
	// Battery in a city no aircraft could reach.
	//
	// Each effect is worth what it adds to the city each turn, in shields,
	// beakers, gold or food, which count about alike (as in
	// WonderPlanner.EffectWorth, where a commerce point counts like a
	// shield). Improvements that only help in some situations (against
	// pollution, aircraft, ships, or with aircraft of our own) aren't built
	// at all outside them. Wonders are valued elsewhere (see WonderPlanner
	// and ChooseProducible.ScoreSmallWonder), and so are the effects
	// ScoreBuilding already looks at (happiness, culture, corruption, city
	// size, walls and barracks).
	//
	// In AI trials (AiTrialTest, 32 paired games of 250 turns on each map)
	// civs valuing improvements this way did no worse and perhaps a little
	// better: score +66 +- 87 on Continents, +18 +- 51 on Archipelago and
	// +68 +- 108 on Pangaea (25 games), where they also lost fewer cities
	// (-2.7 +- 0.8 each).
	public static class BuildingEffects {
		// HEURISTIC: the score of an improvement goes up by this much for
		// each shield (or beaker, gold or food) it adds a turn ...
		private const float PointsPerYield = 3;
		// ... less this much, so that an improvement adding next to nothing
		// scores below the plain peacetime bonus ScoreBuilding gives every
		// improvement, and one adding a lot well above it.
		private const float Offset = 10;
		private const float MinScore = -15;
		private const float MaxScore = 45;

		// HEURISTIC: each pollution icon is a 1% chance a turn of a polluted
		// tile, which yields less until a worker cleans it, and adds to
		// global pollution.
		private const float PollutionIconWorth = 2;

		// HEURISTIC: how near (in tiles) enemy aircraft or warships must be
		// for a city to want SAM batteries or a coastal fortress.
		private const int AirThreatDistance = 12;
		private const int NavalThreatDistance = 8;

		// The change to the building's score for what its effect is worth in
		// this city, or null if it isn't worth building here at all. Zero
		// for wonders and for improvements whose effects aren't valued here.
		internal static float? Score(ChooseProducible.ProducibleStats stats, City city, Player player, Building building) {
			if (building.IsGreatWonder() || building.isSmallWonder) {
				return 0;
			}
			float gain = 0;
			bool valued = false;
			HashSet<SaveBuilding.Flag> flags = building.ToSaveBuilding().flags;
			List<CityBuilding> have = city.EffectiveBuildings();

			// Science, taxes and luxuries: half (or all) again the city's
			// share of each before the bonuses it already has.
			if (building.increasesResearch || building.doublesResearch) {
				valued = true;
				int percent = have.Sum(cb => (cb.building.increasesResearch ? 50 : 0) + (cb.building.doublesResearch ? 100 : 0));
				float baseBeakers = stats.Commerce.beakers * 100f / (100 + percent);
				gain += baseBeakers * (building.doublesResearch ? 1f : 0.5f);
			}
			if (building.increasesTax) {
				valued = true;
				int count = have.Count(cb => cb.building.increasesTax);
				float baseTaxes = stats.Commerce.taxes / (1 + count / 2f);
				gain += baseTaxes * 0.5f;
			}
			if (building.increasesLuxury) {
				valued = true;
				int count = have.Count(cb => cb.building.increasesLuxury);
				float baseLuxury = stats.Commerce.happiness / (1 + count / 2f);
				// Luxuries only matter as far as they make citizens content.
				gain += baseLuxury * 0.25f;
			}

			// Gold the buildings bring in by themselves (see City.BuildingGold).
			if (building.goldPerTurn > 0 || building.goldPerWorkedCoastTile > 0 || building.doublesBuildingGold) {
				valued = true;
				gain += city.BuildingGold(new(have) { new CityBuilding { building = building } }) - city.BuildingGold(have);
			}

			// Shields: factories and power plants multiply the useful shields
			// (a city runs only one power plant; see City.ProductionBonusPercent).
			if (building.productionBonusPercent > 0) {
				valued = true;
				int before = City.ProductionBonusPercent(have);
				List<CityBuilding> with = new(have) { new CityBuilding { building = building } };
				int after = City.ProductionBonusPercent(with);
				float baseShields = stats.CurrentProductionYield.useful * 100f / (100 + before);
				gain += baseShields * (after - before) / 100f;
			}

			// Growth: a granary only helps a city with food to spare and room
			// to grow.
			bool canGrow = city.CanGrowPopulationByOne(EngineStorage.gameData);
			if (building.doublesCityGrowthRate) {
				valued = true;
				if (canGrow) {
					gain += Math.Max(0, stats.FoodGrowthPerTurn) * 0.5f;
				}
			}

			// Ocean tiles: a harbor adds a food, an offshore platform a shield
			// and a commercial dock a commerce on each water tile worked, and
			// a bit for those the city may work later. Food is worth less to a
			// city that can't grow.
			bool food = flags.Contains(SaveBuilding.Flag.IncreasesFoodInWater);
			bool shields = flags.Contains(SaveBuilding.Flag.IncreasesShieldsInWater);
			bool trade = flags.Contains(SaveBuilding.Flag.IncreasesTradeInWater);
			if (food || shields || trade) {
				valued = true;
				(int worked, int unworked) = WaterTiles(city);
				float perTile = (food ? (canGrow ? 1f : 0.5f) : 0) + (shields ? 1 : 0) + (trade ? 1 : 0);
				gain += perTile * (worked + Math.Min(unworked, 4) * 0.25f);
			}

			// A harbor also links the city by sea to our cities on other
			// landmasses, for their resources and luxuries (see TradeNetwork).
			if (building.allowsWaterTrade) {
				valued = true;
				if (player.cities.Any(c => c.location.continent != city.location.continent)) {
					gain += 2;
				}
			}

			// Pollution: mass transit and recycling only where they remove
			// some, and polluting buildings cost what they add.
			if (building.removesPopulationPollution || building.reducesBuildingPollution) {
				valued = true;
				int removed = PollutionRemoved(city, player, building);
				if (removed <= 0) {
					return null;
				}
				gain += removed * PollutionIconWorth;
			}
			if (building.pollution > 0) {
				bool recycling = have.Any(cb => cb.building.reducesBuildingPollution);
				if (!recycling) {
					gain -= building.pollution * PollutionIconWorth;
				}
			}

			// Defenses against aircraft and ships: only where they come.
			if (building.airPower > 0) {
				valued = true;
				stats.enemyAircraftNear ??= EnemyUnitsNear(player, city, AirThreatDistance, u => u.IsAirUnit());
				if (!stats.enemyAircraftNear.Value) {
					return null;
				}
				gain += 3 + city.residents.Count / 3f;
			}
			if (building.navalDefenseBonus != null && !building.providesWalls) {
				valued = true;
				stats.enemyWarshipsNear ??= EnemyUnitsNear(player, city, NavalThreatDistance,
					u => u.IsWaterUnit() && (u.unitType.attack > 0 || u.unitType.bombard > 0));
				if (!stats.enemyWarshipsNear.Value) {
					return null;
				}
				gain += 3 + city.residents.Count / 3f;
			}

			// An airport is for air units: veterans, and airlifting them.
			if (building.allowsAirTrade || building.providesVeteranAirUnits) {
				valued = true;
				int aircraft = player.units.Count(u => u.IsAirUnit());
				if (aircraft == 0) {
					return null;
				}
				gain += Math.Min(aircraft, 6) * 0.5f;
			}

			if (!valued) {
				return 0;
			}
			float score = Math.Clamp(gain * PointsPerYield - Offset, MinScore, MaxScore);

			// Upkeep weighs more on a treasury that barely covers it.
			if (stats.GoldPerTurn < building.maintenanceCost * 2) {
				score -= building.maintenanceCost * 2;
			}
			return score;
		}

		// The water tiles in the city's radius that the city works, and those
		// it doesn't.
		private static (int worked, int unworked) WaterTiles(City city) {
			HashSet<Tile> working = new();
			foreach (CityResident r in city.residents) {
				if (r.tileWorked != null && r.tileWorked != Tile.NONE) {
					working.Add(r.tileWorked);
				}
			}
			int worked = 0, unworked = 0;
			foreach (Tile t in city.location.GetTilesWithinRankDistance(EngineStorage.gameData.rules.MaxRankOfWorkableTiles)) {
				if (t == Tile.NONE || t == city.location || !t.IsWater()) {
					continue;
				}
				if (working.Contains(t)) {
					++worked;
				} else {
					++unworked;
				}
			}
			return (worked, unworked);
		}

		// The pollution icons the building would take away from the city.
		private static int PollutionRemoved(City city, Player player, Building building) {
			int before = city.PollutionIcons(player.rules);
			if (before <= 0) {
				return 0;
			}
			int population = Math.Max(0, city.residents.Count - player.rules.MaximumLevel2CitySize);
			int buildings = Math.Max(0, city.BuildingPollution());
			int removed = 0;
			if (building.removesPopulationPollution) {
				removed += Math.Max(0, population - 1);
			}
			if (building.reducesBuildingPollution) {
				removed += Math.Max(0, buildings - 1);
			}
			return Math.Min(removed, before);
		}

		// Whether a unit of a civ at war with us (barbarians aside) that
		// passes the test is within the distance of the city, where we can
		// see it.
		private static bool EnemyUnitsNear(Player player, City city, int distance, Func<MapUnit, bool> test) {
			foreach (Player other in EngineStorage.gameData.players) {
				if (other == player || other.IsIndependent || other.defeated || !PlayerRelationship.AtWar(player, other)) {
					continue;
				}
				foreach (MapUnit u in other.units) {
					if (Tile.IsTileValid(u.location) && u.location.map == city.location.map
							&& u.location.DistanceTo(city.location) <= distance && test(u)
							&& AIFogOfWar.SeesUnitsOn(player, u.location)) {
						return true;
					}
				}
			}
			return false;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Serilog.Events;
using C7GameData;
using static C7GameData.PlayerRelationship;

namespace C7Engine {
	public class ChooseProducible {
		private static ILogger log = Log.ForContext<ChooseProducible>();

		// Facts about the city and player that every option is scored
		// against. Nothing changes while the options of one decision are
		// scored, so each is computed at most once per decision, and only if
		// an option needs it.
		internal sealed class ProducibleStats {
			public readonly City city;
			public readonly Player player;

			public float bestAttack;
			public float bestDefense;
			public readonly bool atWar;

			// Finding the open city spots scores every known tile, so only do
			// it if a decision needs it: when scoring a settler, or to tell if
			// a player with 5 or more cities is still expanding.
			private int? numberOfReachableOpenCitySpots;
			public int NumberOfReachableOpenCitySpots => numberOfReachableOpenCitySpots ??= ChooseProducible.NumberOfReachableOpenCitySpots(city);

			private bool? inExpansionPhase;
			public bool InExpansionPhase => inExpansionPhase ??= player.cities.Count < 5 || NumberOfReachableOpenCitySpots > player.cities.Count * 2;

			public ProducibleStats(City city, Player player) {
				this.city = city;
				this.player = player;
				atWar = IsInAnyWar(player, EngineStorage.gameData.players);
			}

			private bool? cityGuarded;
			public bool CityGuarded => cityGuarded ??= city.location.unitsOnTile.Count(u => u.CanDefendOnLand()) > 0;

			private bool? hasUnescortedSettler;
			public bool HasUnescortedSettler => hasUnescortedSettler ??= ChooseProducible.HasUnescortedSettler(city);

			private int? unitSupportCost;
			public int UnitSupportCost => unitSupportCost ??= player.TotalUnitsAllowedUnitsAndSupportCost().Item3;

			private int? settlersUnderConstruction;
			public int SettlersUnderConstruction => settlersUnderConstruction ??= ChooseProducible.SettlersUnderConstruction(player);

			private int? numWorkers;
			public int NumWorkers => numWorkers ??= player.units.Count(x => x.unitType.isWorker);

			private int? numUnworkedTiles;
			public int NumUnworkedTiles => numUnworkedTiles ??= ChooseProducible.NumUnworkedTiles(city);

			private bool? connectedToCapital;
			public bool ConnectedToCapital => connectedToCapital ??= EngineStorage.gameData.GetTradeNetwork().ConnectedToCapital(player, city);

			private bool? neighborsOcean;
			public bool NeighborsOcean => neighborsOcean ??= city.location.NeighborsOcean();

			private int? luxuryCount;
			public int LuxuryCount => luxuryCount ??= city.GetLuxuries(EngineStorage.gameData).Keys.Count;

			private int? foodGrowthPerTurn;
			public int FoodGrowthPerTurn => foodGrowthPerTurn ??= city.FoodGrowthPerTurn();

			private CorruptableValue? currentProductionYield;
			public CorruptableValue CurrentProductionYield => currentProductionYield ??= city.CurrentProductionYield();

			private int? culturePerTurn;
			public int CulturePerTurn => culturePerTurn ??= city.GetCulturePerTurn();

			private (int unclaimed, int enemy)? outerRingTiles;
			public (int unclaimed, int enemy) OuterRingTiles => outerRingTiles ??= CountOuterRingTiles(city);

			private (int unhappy, int entertainers)? unhappyAndEntertainers;
			public (int unhappy, int entertainers) UnhappyAndEntertainers => unhappyAndEntertainers ??= CountUnhappyAndEntertainers(city);

			private int? turnsUntilGrowth;
			public int TurnsUntilGrowth => turnsUntilGrowth ??= city.TurnsUntilGrowth();

			// Whether a naval unit of each type built here would explore.
			private readonly Dictionary<UnitPrototype, bool> wouldExplore = new();
			public bool WouldExplore(UnitPrototype unit) {
				if (!wouldExplore.TryGetValue(unit, out bool result)) {
					// A unit we might build, used only to ask what we would do
					// with it. It isn't put into play, so it doesn't need a
					// real ID.
					MapUnit temp = unit.GetInstance(ID.None(unit.name), unit, player, location: city.location);
					result = PlayerAI.WouldExplore(temp, player, hypothetical: true);
					wouldExplore[unit] = result;
				}
				return result;
			}
		}

		public static IProducible Choose(City city, Player player) {
			List<IProducible> options = city.ListProductionOptions(EngineStorage.gameData).ToList();
			ProducibleStats stats = CalculateStats(city, player, options);

			IProducible best = null;
			float bestScore = int.MinValue;

			bool debugLogging = log.IsEnabled(LogEventLevel.Debug);
			if (debugLogging) {
				log.Debug("{Civilization}: {City}---- {InExpansionPhase} {OpenCitySpots}", player.civilization.name, city, stats.InExpansionPhase, stats.NumberOfReachableOpenCitySpots);
			}
			foreach (IProducible option in options) {
				// Get the item score, with a +/- 10% random adjustment to make
				// things seem appropriately random.
				float score = ScoreProducible(stats, city, player, option) * (GameData.rng.Next(90, 110) / 100.0f);
				if (score > bestScore) {
					bestScore = score;
					best = option;
				}
				if (debugLogging) {
					log.Debug("\t{Option}: {Score}", option, score);
				}
			}
			if (debugLogging) {
				log.Debug("\t\tchose {Best}", best);
			}
			return best;
		}

		private static float ScoreProducible(ProducibleStats stats, City city, Player player, IProducible option) {
			if (option is UnitPrototype unit) {
				return ScoreUnit(stats, city, player, unit);
			} else if (option is Building building) {
				return ScoreBuilding(stats, city, player, building);
			} else if (option is Inflow inflow) {
				return ScoreInflow(stats, city, player, inflow);
			} else {
				throw new Exception($"Unexpected producible: {option}");
			}
		}

		private static float ScoreInflow(ProducibleStats stats, City city, Player player, Inflow inflow) {
			// TODO: score this properly, right now we give a very low score to avoid auto picking this
			// I am not sure how civ III does this, although I guess we should consider stuff like
			// no more buildings to build, reached unit cap, very bad economy with no other options,
			// the WealthNever and WealthOften flags from RACE, etc

			if (HasWeakEconomy(player)) {
				return 100 - player.gold;
			}

			return -1000f;
		}

		internal static float ScoreUnit(ProducibleStats stats, City city, Player player, UnitPrototype unit) {
			// The AI doesn't know how to use armies yet, so it doesn't build them.
			if (unit.isArmy) {
				return -1000f;
			}

			bool isSettler = unit.actions.Contains(UnitAction.BuildCity);
			bool isWorker = unit.isWorker;
			bool atWar = stats.atWar;

			float attackWeight = atWar ? 15 : 10;
			float defenseWeight = atWar ? 15 : 10;
			float populationWeight = 10;
			float populationCostPenalty = populationWeight * unit.populationCost;
			float noTradeAccessBoost = 10;
			float unitSupportCapPenalty = 10;

			float score = 0;

			////////////////////////////////////////////////////////////////////
			///
			/// Raw score adjustments based on unit stats.
			///

			// Weight the unit's attack and defense score by comparing it to the
			// best stat available. If no option has any attack (or defense),
			// the stat doesn't tell options apart (and 0 / 0 would make every
			// unit's score NaN, so none could be chosen).
			if (stats.bestAttack > 0) {
				score += unit.attack / stats.bestAttack * attackWeight;
			}
			if (stats.bestDefense > 0) {
				score += unit.defense / stats.bestDefense * defenseWeight;
			}

			// Penalize more expensive units.
			score -= city.TurnsToProduce(unit);

			// Penalize units that consume population.
			score -= populationCostPenalty;

			////////////////////////////////////////////////////////////////////
			///
			/// Naval units
			///

			if (unit.categories.Contains("Sea")) {
				// Don't bother building naval units unless we border the ocean.
				// This does mean that inland seas are excluded.
				if (!stats.NeighborsOcean) {
					return int.MinValue;
				}

				// Don't built a naval unit if we wouldn't explore with it. We
				// don't yet handle using ships as transports or naval warfare.
				if (!stats.WouldExplore(unit)) {
					return int.MinValue;
				}
			}

			////////////////////////////////////////////////////////////////////
			///
			/// Adjustments based on the city situation.
			///

			// Prioritize defending the city if it is unguarded.
			if (!stats.CityGuarded && unit.defense == 0) {
				return int.MinValue;
			}

			// Prioritize building settler escorts if we don't have one.
			if (stats.HasUnescortedSettler && unit.defense == 0) {
				return int.MinValue;
			}

			// Penalize going over the unit support cap unless we're at war or
			// still in the expansion phase.
			if (!atWar && !stats.InExpansionPhase) {
				int unitSupportCost = stats.UnitSupportCost;
				if (unitSupportCost > 0) {
					score -= unitSupportCost / 2;

					if (HasWeakEconomy(player)) {
						score -= unitSupportCost * 2;
					}
				}
			}

			////////////////////////////////////////////////////////////////////
			///
			/// Adjustments for settlers and workers.
			///

			// Don't built a worker or settler if we don't have enough population.
			if (CityIsTooSmall(stats, city, unit)) {
				return int.MinValue;
			}

			if (isSettler) {
				// Don't build settlers if we don't have anywhere to go or if we
				// already have enough settlers under construction to fill all
				// the spots.
				if (stats.NumberOfReachableOpenCitySpots <= stats.SettlersUnderConstruction) {
					return int.MinValue;
				}

				// If there are more open spots than we have cities we are still
				// aggressively expanding. Negate the population penalty, and
				// weight settlers more heavily if there are way more open spots.
				if (stats.NumberOfReachableOpenCitySpots > player.cities.Count) {
					score += populationCostPenalty * Math.Min(3.0f, stats.NumberOfReachableOpenCitySpots / player.cities.Count);
				}

				// Slightly penalize going over the optimal number of cities.
				if (player.cities.Count > EngineStorage.gameData.map.optimalNumberOfCities) {
					score *= 2.0f / 3.0f;
				}
			}

			if (isWorker) {
				// If we have unworked tiles and fewer workers than cities, boost
				// the odds of producing a worker.
				int numUnworkedTiles = stats.NumUnworkedTiles;
				if (numUnworkedTiles > 0 && stats.NumWorkers < player.cities.Count * 1.5f) {
					score += populationCostPenalty * Math.Min(3.0f, 1 + numUnworkedTiles);
				}

				// If we have a few cities but don't have trade access to the
				// capital, boost the odds of a worker.
				if (player.cities.Count > 4 && !stats.ConnectedToCapital) {
					score += noTradeAccessBoost;
				}
			}

			return score;
		}

		private static float ScoreBuilding(ProducibleStats stats, City city, Player player, Building building) {
			bool atWar = stats.atWar;

			float score = 0;

			// The AI doesn't move its palace on purpose; losing the capital
			// rebuilds it for free.
			if (building.isCenterOfEmpire) {
				return int.MinValue;
			}

			if (building.isSmallWonder) {
				float? smallWonderScore = ScoreSmallWonder(stats, city, player, building);
				if (smallWonderScore == null) {
					return -1000f;
				}
				score += smallWonderScore.Value;
			}

			// Marketplaces should be prioritized in cities with high population
			// that have a decent number of luxuries. Otherwise they don't do
			// much.
			if (building.increasesLuxuryTrade) {
				int luxuryCount = stats.LuxuryCount;
				score += Math.Max(city.residents.Count - luxuryCount, 0) * (luxuryCount / 2);
			}

			// Only build an aqueduct if we need one.
			if (building.allowsCitySize2 &&
				city.residents.Count == player.rules.MaximumLevel1CitySize && stats.FoodGrowthPerTurn > 0) {
				score += 50;
			}

			// Ditto with hospitals.
			if (building.allowsCitySize3 &&
				city.residents.Count == player.rules.MaximumLevel2CitySize && stats.FoodGrowthPerTurn > 0) {
				score += 70;
			}

			// A town that is at war might consider building walls, otherwise it
			// probably shouldn't.
			if (building.providesWalls) {
				score += atWar ? 20 : 8;
			}

			// If a city has a decent number of corrupt shields, prioritize
			// building a courthouse based on how many corrupt shields are in
			// play.
			//
			// Ensure we don't build a courthouse in the capital though!
			if (building.reducesCorruption) {
				if (city.capital) {
					return int.MinValue;
				}

				CorruptableValue prod = stats.CurrentProductionYield;
				if (prod.useful > 0 && ((float)prod.corrupt) / prod.useful > .15) {
					score += prod.corrupt * 8;
				}
			}

			// Boost buildings that help with happiness if we have some unhappy
			// citizens in the city or if we have a decently large value on the
			// luxury slider.
			if (building.contentFacesInCity > 0) {
				(int unhappyCount, int entertainerCount) = stats.UnhappyAndEntertainers;

				if (unhappyCount > 0 || (player.luxuryRate > 3 && city.residents.Count > 4)) {
					score += Math.Min(unhappyCount, building.contentFacesInCity) * 10;
					score += Math.Min(entertainerCount, building.contentFacesInCity) * 10;
				}
			}

			// Boost buildings that provide culture, especially if could claim
			// additional tiles by expanding our borders, or if we are next to
			// an enemy city.
			if (building.culturePerTurn > 0) {
				(int unclaimedTilesInOuterRing, int enemyTilesInOuterRing) = stats.OuterRingTiles;

				if (stats.CulturePerTurn == 0) {
					score += building.culturePerTurn * (unclaimedTilesInOuterRing + enemyTilesInOuterRing * 3);
				} else {
					score += building.culturePerTurn * (unclaimedTilesInOuterRing + enemyTilesInOuterRing * 3) / 2.0f;
				}
			}

			// Prioritize barracks in larger cities, and especially if we are at
			// war.
			if (building.providesVeteranGroundUnits) {
				score += (city.residents.Count / 5) * (atWar ? 3 : 1);
			}

			// The space race: the Apollo Program opens it, and every
			// spaceship part brings the AI closer to winning. Parts are only
			// offered while the ship still needs them, so favor them strongly,
			// a bit less while at war.
			if (SpaceRace.SpaceRaceAllowed(EngineStorage.gameData)) {
				if (building.IsSpaceshipPart) {
					score += atWar ? 40 : 70;
				} else if (building.buildSpaceshipParts && !SpaceRace.ApolloProgramBuilt(EngineStorage.gameData)) {
					score += atWar ? 20 : 40;
				}
			}

			// Penalize more expensive buildings.
			score -= city.TurnsToProduce(building) / (stats.InExpansionPhase ? 1 : 2);

			// Penalize buildings with higher maintenance costs.
			score -= building.maintenanceCost;

			// Boost buildings a bit if we aren't at war and aren't expanding.
			if (!stats.InExpansionPhase && !atWar) {
				score += 20;
			}

			// Penalize buildings if economy is weak
			if (HasWeakEconomy(player)) {
				score -= 10 * building.maintenanceCost;
			}

			return score;
		}

		// The extra score of a small wonder for its effect, or null if it isn't
		// worth building here at all.
		private static float? ScoreSmallWonder(ProducibleStats stats, City city, Player player, Building building) {
			bool atWar = stats.atWar;
			float score = 0;

			// The AI doesn't use armies yet, so the army wonders are wasted
			// on it.
			if (building.allowsBuildArmy || building.allowsLargerArmies) {
				return null;
			}

			// A Forbidden Palace (or Secret Police HQ) pays off in a large
			// empire, in a city that would become the center for several
			// cities now closer to it than to the palace or another one.
			if (building.isForbiddenPalace) {
				List<City> centers = player.citiesWithCorruptionWonders;
				if (player.cities.Count < 6 || centers.Count == 0) {
					return null;
				}
				int served = 0;
				foreach (City c in player.cities) {
					int toCenter = centers.Min(x => c.location.RankDistanceTo(x.location));
					if (c.location.RankDistanceTo(city.location) < toCenter) {
						++served;
					}
				}
				if (served < 3) {
					return null;
				}
				score += served * 6;
			}

			// Wall Street: 5% interest on the treasury, up to 50 gold.
			if (building.treasuryEarnsInterest) {
				score += Math.Min(player.gold / 20, 50) * 2;
			}

			// The Iron Works: a big production boost in an already productive
			// city.
			if (building.productionBonusPercent > 0) {
				score += stats.CurrentProductionYield.useful * building.productionBonusPercent / 100f;
			}

			if (building.increasesLeaderChance) {
				score += atWar ? 15 : 5;
			}
			if (building.allowsEnemyTerritoryHealing) {
				score += atWar ? 20 : 0;
			}
			if (building.decreasesMissileSuccess) {
				score += atWar ? 15 : 0;
			}
			if (building.buildSpaceshipParts) {
				score += 15;
			}
			if (building.allowsSpyMissions) {
				score += 5;
			}

			return score;
		}

		internal static ProducibleStats CalculateStats(City city, Player player, List<IProducible> options) {
			ProducibleStats stats = new(city, player);

			foreach (IProducible option in options) {
				if (option is UnitPrototype unit) {
					stats.bestAttack = Math.Max(stats.bestAttack, unit.attack);
					stats.bestDefense = Math.Max(stats.bestDefense, unit.defense);
				}
			}

			return stats;
		}

		private static (int unclaimed, int enemy) CountOuterRingTiles(City city) {
			int unclaimedTilesInOuterRing = 0;
			int enemyTilesInOuterRing = 0;
			foreach (Tile t in city.location.GetTilesWithinRankDistance(2)) {
				Player owner = t.OwningPlayer();
				if (owner == null) {
					++unclaimedTilesInOuterRing;
				} else if (owner != city.owner) {
					++enemyTilesInOuterRing;
				}
			}
			return (unclaimedTilesInOuterRing, enemyTilesInOuterRing);
		}

		private static (int unhappy, int entertainers) CountUnhappyAndEntertainers(City city) {
			int unhappyCount = 0;
			int entertainerCount = 0;
			foreach (CityResident cr in city.residents) {
				// Specialists' moods are stale and don't count.
				if (cr.citizenType.IsDefaultCitizen && cr.mood == CityResident.Mood.Unhappy) { ++unhappyCount; }
				if (cr.citizenType.Luxuries > 0) { ++entertainerCount; }
			}
			return (unhappyCount, entertainerCount);
		}

		private static bool HasUnescortedSettler(City city) {
			foreach (MapUnit u in city.location.unitsOnTile) {
				if (u.currentAI is SettlerAI settlerAi && settlerAi.data.escort == null) {
					return true;
				}
			}
			return false;
		}

		internal static int NumberOfReachableOpenCitySpots(City city) {
			int result = 0;

			// Note: GetScoredSettlerCandidates already excludes tiles with
			// settlers moving towards them.
			Dictionary<Tile, float> scoredLocations = SettlerLocationAI.GetScoredSettlerCandidates(city.location, city.owner);
			List<KeyValuePair<Tile, float>> orderedScores = scoredLocations.OrderByDescending(t => t.Value).ToList();

			foreach ((Tile tile, float score) in orderedScores) {
				if (scoredLocations.ContainsKey(tile)) {
					++result;

					// Remove all the spots that would become invalid locations
					// if we built this city: like SettlerLocationAI, cities
					// can't be founded within two steps of another city.
					foreach (Tile n in tile.neighbors.Values) {
						if (n == Tile.NONE) {
							continue;
						}
						scoredLocations.Remove(n);
						foreach (Tile nn in n.neighbors.Values) {
							scoredLocations.Remove(nn);
						}
					}
				}
			}

			return result;
		}

		private static int SettlersUnderConstruction(Player player) {
			int result = 0;
			foreach (City c in player.cities) {
				if (c.itemBeingProduced is UnitPrototype unit && unit.actions.Contains(UnitAction.BuildCity)) {
					++result;
				}
			}
			return result;
		}

		private static bool CityIsTooSmall(ProducibleStats stats, City city, UnitPrototype unit) {
			if (unit.populationCost < city.residents.Count) {
				return false;
			}

			// If we would grow before the city finishes producing the unit, we
			// can build the unit.
			if (unit.populationCost == city.residents.Count && city.TurnsToProduce(unit) >= stats.TurnsUntilGrowth) {
				return false;
			}

			return true;
		}

		private static int NumUnworkedTiles(City city) {
			int result = 0;
			foreach (CityResident cr in city.residents) {
				if (cr.tileWorked != Tile.NONE && !cr.tileWorked.overlays.HasBeenImproved()) {
					++result;
				}
			}
			return result;
		}

		public static bool HasWeakEconomy(Player player) {
			return player.luxuryRate == 0 && player.scienceRate == 0 && player.gold < 100;
		}
	}
}

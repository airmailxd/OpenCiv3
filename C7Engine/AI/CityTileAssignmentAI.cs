using System;
using System.Collections.Generic;
using C7GameData;
using System.Linq;
using Serilog;
using Serilog.Events;

namespace C7Engine.AI {
	public class CityTileAssignmentAI {
		public static int DesiredFoodSurplusPerTurn = 2;
		private static readonly int FOOD_PER_CITIZEN = 2;   //eventually will be configured by rules


		private static int foodPriorityRate = 40;
		private static int productionPriorityRate = 50;
		private static int commercePriorityRate = 30;

		// How much more a yield the city's governor emphasizes counts.
		// UNVERIFIED: Civ3 doesn't say how strongly its governor favours an
		// emphasized yield, so it simply counts twice.
		private const int EmphasisMultiplier = 2;

		private static ILogger log = Log.ForContext<CityTileAssignmentAI>();

		// Values that stay the same while several citizens of one city are
		// assigned in a row, as long as nothing else changes in between: the
		// city's workable tiles and their yields, and the known specialists.
		// Only which tiles are worked changes, and that is always read fresh.
		internal sealed class AssignmentContext {
			public readonly City city;
			public readonly List<Tile> workableTiles;
			private readonly GameData gameData;
			private readonly Dictionary<Tile, (int food, int production, int commerce)> playerYields = new();
			private readonly Dictionary<Tile, int> cityFoodYields = new();
			private List<CitizenType> knownSpecialists;

			// Whether the city's food beyond what its citizens eat is wasted
			// (see AssignNewCitizenToTile), decided before its citizens were
			// taken off their tiles to be placed again, when its size still
			// showed whether it can grow.
			public readonly bool? foodIsWasted;

			public AssignmentContext(GameData gameData, City city, bool? foodIsWasted = null) {
				this.gameData = gameData;
				this.city = city;
				this.foodIsWasted = foodIsWasted;
				workableTiles = city.GetWorkableTiles();
			}

			public (int food, int production, int commerce) PlayerYields(Tile t) {
				if (!playerYields.TryGetValue(t, out var yields)) {
					yields = (t.FoodYield(city.owner).yield, t.ProductionYield(city.owner).yield, t.CommerceYield(city.owner).yield);
					playerYields[t] = yields;
				}
				return yields;
			}

			// Same as City.CurrentFoodYield.
			public int CurrentFoodYield() {
				int yield = city.location.FoodYield(city).yield;
				foreach (CityResident r in city.residents) {
					if (!cityFoodYields.TryGetValue(r.tileWorked, out int food)) {
						food = r.tileWorked.FoodYield(city).yield;
						cityFoodYields[r.tileWorked] = food;
					}
					yield += food;
				}
				return yield;
			}

			public List<CitizenType> KnownSpecialists() {
				return knownSpecialists ??= city.owner.GetKnownSpecialists(gameData);
			}
		}

		// Assigns a citizen, which is alredy part of a city, to a tile, if possible.
		public static void AssignNewCitizenToTile(GameData gameData, CityResident newResident, bool manageMoods = false) {
			AssignNewCitizenToTile(gameData, newResident, manageMoods, context: null);
		}

		// As above. A context may be passed in when assigning several citizens
		// of the same city in a row, to avoid recomputing tile yields.
		internal static void AssignNewCitizenToTile(GameData gameData, CityResident newResident, bool manageMoods, AssignmentContext context) {
			City city = newResident.city;
			if (context != null && context.city != city) {
				throw new ArgumentException($"Assignment context is for {context.city}, not {city}");
			}

			int foodYield = context?.CurrentFoodYield() ?? city.CurrentFoodYield();

			// A computer player's city that can't grow (it needs an
			// aqueduct or hospital) wants only the food its citizens eat:
			// more would be wasted, so its citizens work the tiles with the
			// most shields and trade instead. Owner's decision, 2026-10-12:
			// a size 12 French capital worked flood plains for 1 shield a
			// turn and could barely build anything.
			bool foodIsWasted = context?.foodIsWasted ?? FoodIsWasted(gameData, city);
			int desiredFoodRate = city.residents.Count * FOOD_PER_CITIZEN
				+ (foodIsWasted ? 0 : DevelopmentAI.DesiredFoodSurplus(city, DesiredFoodSurplusPerTurn));
			int targetTileFoodAmount = desiredFoodRate - foodYield;

			bool debugLogging = log.IsEnabled(LogEventLevel.Debug);
			double maxScore = 0;
			Tile preferredTile = Tile.NONE;
			foreach (Tile t in context?.workableTiles ?? city.GetWorkableTiles()) {
				if (t.personWorkingTile == null) {
					double score;
					if (context != null) {
						var (food, production, commerce) = context.PlayerYields(t);
						score = CalculateTileYieldScore(food, production, commerce, targetTileFoodAmount, foodIsWasted, city.governor);
					} else {
						score = CalculateTileYieldScore(t.FoodYield(city.owner).yield, t.ProductionYield(city.owner).yield, t.CommerceYield(city.owner).yield,
							targetTileFoodAmount, foodIsWasted, city.governor);
					}
					if (debugLogging) {
						log.Debug("Tile {Tile} scored {Score}", t, score);
					}
					if (score > maxScore) {
						maxScore = score;
						preferredTile = t;
					}
				}
			}

			if (debugLogging) {
				log.Debug("Assigning new citizen of {City} to tile {Tile} with yield {Yield}", city.name, preferredTile, city.location.YieldString(city.owner));
			}

			if (preferredTile == Tile.NONE) {
				newResident.citizenType = (context?.KnownSpecialists() ?? city.owner.GetKnownSpecialists(gameData))[0];
			} else {
				newResident.tileWorked = preferredTile;
				preferredTile.personWorkingTile = newResident;
			}

			// Check to see if this citizen working a tile would cause the city
			// to be unhappy. If so, make them an entertainer.
			City.Mood cityMood = city.RecalculateCitizenMoods(gameData);

			if (cityMood == City.Mood.Unhappy && manageMoods) {
				newResident.citizenType = (context?.KnownSpecialists() ?? city.owner.GetKnownSpecialists(gameData)).MaxBy(x => x.Luxuries);
				if (newResident.tileWorked != Tile.NONE) {
					newResident.tileWorked = Tile.NONE;
					preferredTile.personWorkingTile = null;
				}
			}
		}

		private static int WeightedYield(int food, int production, int commerce, CityGovernor governor) {
			return food * foodPriorityRate * (governor?.emphasizeFood == true ? EmphasisMultiplier : 1)
				+ production * productionPriorityRate * (governor?.emphasizeProduction == true ? EmphasisMultiplier : 1)
				+ commerce * commercePriorityRate * (governor?.emphasizeCommerce == true ? EmphasisMultiplier : 1);
		}

		// What a city's governor does when it manages citizen moods (Civ3's
		// "Manage citizen moods"): if the city would riot, its laborers
		// on the least valuable tiles become entertainers until it wouldn't;
		// otherwise its entertainers go back to work, one at a time, as long
		// as the city stays in order. Other specialists are the player's
		// choice and left alone, as are resisters. Returns whether any
		// citizen changed.
		//
		// UNVERIFIED: how Civ3's governor picks which citizens to move isn't
		// documented; this is the simplest behaviour that keeps the city out
		// of disorder without idling more citizens than it needs to.
		public static bool GovernMoods(GameData gameData, City city) {
			if (!city.governor.manageMoods || city.residents.Count == 0) {
				return false;
			}
			List<CitizenType> specialists = city.owner.GetKnownSpecialists(gameData);
			if (specialists.Count == 0) {
				return false;
			}
			CitizenType entertainer = specialists.MaxBy(x => x.Luxuries);
			// The resisters are the last citizens (see City.StartResistance).
			List<CityResident> governed = city.residents.Take(city.residents.Count - city.resisters).ToList();
			bool changed = false;

			if (city.RecalculateCitizenMoods(gameData) == City.Mood.Unhappy) {
				while (true) {
					CityResident worst = governed
						.Where(r => r.citizenType.IsDefaultCitizen && r.tileWorked != Tile.NONE)
						.MinBy(r => WeightedYield(r.tileWorked.FoodYield(city.owner).yield, r.tileWorked.ProductionYield(city.owner).yield,
							r.tileWorked.CommerceYield(city.owner).yield, city.governor));
					if (worst == null) {
						break;
					}
					worst.tileWorked.personWorkingTile = null;
					worst.tileWorked = Tile.NONE;
					worst.citizenType = entertainer;
					changed = true;
					if (city.RecalculateCitizenMoods(gameData) != City.Mood.Unhappy) {
						break;
					}
				}
				return changed;
			}

			CitizenType laborer = gameData.citizenTypes.Find(x => x.IsDefaultCitizen);
			foreach (CityResident r in governed.Where(r => !r.citizenType.IsDefaultCitizen && r.citizenType.Luxuries > 0).ToList()) {
				CitizenType wasType = r.citizenType;
				r.citizenType = laborer;
				AssignNewCitizenToTile(gameData, r, manageMoods: true);
				if (!r.citizenType.IsDefaultCitizen) {
					// No free tile, or working one would make the city riot.
					r.citizenType = wasType;
					break;
				}
				changed = true;
			}
			city.RecalculateCitizenMoods(gameData);
			return changed;
		}

		internal static bool FoodIsWasted(GameData gameData, City city) {
			return !city.owner.isHuman && !city.CanGrowPopulationByOne(gameData);
		}

		public static double CalculateTileYieldScore(Tile t, int targetFoodAmount, Player player) {
			return CalculateTileYieldScore(t.FoodYield(player).yield, t.ProductionYield(player).yield, t.CommerceYield(player).yield, targetFoodAmount);
		}

		// With foodIsWasted, food beyond what the city still needs counts
		// for nothing; falling short of what it needs is still penalized,
		// or a city at its size cap worked mines and starved (the target
		// used to be set to the capped food, which made the penalty 0).
		// The yields the city's governor emphasizes, if any, count for more.
		private static double CalculateTileYieldScore(int food, int production, int commerce, int targetFoodAmount, bool foodIsWasted = false,
				CityGovernor governor = null) {
			if (foodIsWasted) {
				food = Math.Min(food, Math.Max(0, targetFoodAmount));
			}
			int score = WeightedYield(food, production, commerce, governor);
			int penalty = (targetFoodAmount - food);
			if (penalty <= 0) {
				return score;
			}
			return score / (Math.Pow(2, penalty));
		}
	}
}

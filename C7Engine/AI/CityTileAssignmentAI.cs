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

			public AssignmentContext(GameData gameData, City city) {
				this.gameData = gameData;
				this.city = city;
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
				return yield * city.YieldMultiplier();
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

			int desiredFoodRate = city.residents.Count * FOOD_PER_CITIZEN + DesiredFoodSurplusPerTurn;
			int targetTileFoodAmount = desiredFoodRate - foodYield;

			bool debugLogging = log.IsEnabled(LogEventLevel.Debug);
			double maxScore = 0;
			Tile preferredTile = Tile.NONE;
			foreach (Tile t in context?.workableTiles ?? city.GetWorkableTiles()) {
				if (t.personWorkingTile == null) {
					double score;
					if (context != null) {
						var (food, production, commerce) = context.PlayerYields(t);
						score = CalculateTileYieldScore(food, production, commerce, targetTileFoodAmount);
					} else {
						score = CalculateTileYieldScore(t, targetTileFoodAmount, city.owner);
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

		public static double CalculateTileYieldScore(Tile t, int targetFoodAmount, Player player) {
			return CalculateTileYieldScore(t.FoodYield(player).yield, t.ProductionYield(player).yield, t.CommerceYield(player).yield, targetFoodAmount);
		}

		private static double CalculateTileYieldScore(int food, int production, int commerce, int targetFoodAmount) {
			int score = food * foodPriorityRate
				+ production * productionPriorityRate
				+ commerce * commercePriorityRate;
			int penalty = (targetFoodAmount - food);
			if (penalty <= 0) {
				return score;
			}
			return score / (Math.Pow(2, penalty));
		}
	}
}

using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine {
	// Kings & Nomads' independent city-states grow stronger as the game
	// goes on, so they last into the middle game (owner's design,
	// 2026-10-23): every GarrisonGrowthTurns turns each gains a defender, up
	// to MaxGarrison, the best defender the leading civs know, and on turn
	// WallsTurn they all gain walls. They build nothing themselves (no
	// barbarian city does).
	public static class CityStates {
		private static readonly ILogger log = Log.ForContext(typeof(CityStates));

		public const int GarrisonGrowthTurns = 10;
		public const int MaxGarrison = 6;
		public const int WallsTurn = 40;
		private const string WallsName = "Walls";

		// Called as each turn begins.
		public static void BeginTurn(GameData gameData) {
			if (!CityStateTrade.IsOn(gameData) || gameData.turn <= 0) {
				return;
			}
			Player independents = gameData.players.Find(p => p.isBarbarians);
			if (independents == null) {
				return;
			}
			if (gameData.turn == WallsTurn && gameData.Buildings.Find(b => b.name == WallsName) is Building walls) {
				foreach (City city in independents.cities.Where(c => !c.constructed_buildings.Any(b => b.building == walls))) {
					city.AddBuilding(walls);
				}
				log.Information("The city-states raise their walls on turn {Turn}", gameData.turn);
			}
			if (gameData.turn % GarrisonGrowthTurns == 0 && Defender(gameData) is UnitPrototype defender) {
				foreach (City city in independents.cities.ToList()) {
					if (Garrison(city) < MaxGarrison) {
						gameData.SpawnUnit(independents, defender, city.location);
					}
				}
			}
			CityStateTrade.GiveGifts(gameData);
		}

		public static int Garrison(City city) {
			return city.location.unitsOnTile.Count(u => u.owner == city.owner && u.unitType.defense > 0 && !u.unitType.isWorker);
		}

		// HEURISTIC: the best defender (by defence) of the common units
		// (not unique ones) whose advance some civ knows, or that need none.
		public static UnitPrototype Defender(GameData gameData) {
			var civs = gameData.players.Where(p => !p.isBarbarians && !p.defeated).ToList();
			int civilizations = gameData.civilizations.Count(c => !c.isBarbarian);
			return gameData.unitPrototypes
				.Where(u => u.IsLandUnit() && !u.unproducible && u.defense > 0 && u.defense >= u.attack
					&& !u.isWorker && !u.isSettler && !u.isKing && !u.isLeader && !u.isArmy
					&& u.producibleBy.Count(c => !c.isBarbarian) * 2 > civilizations
					&& (u.requiredTech == null || civs.Any(p => p.knownTechs.Contains(u.requiredTech.id))))
				.OrderByDescending(u => u.defense).ThenBy(u => u.shieldCost)
				.FirstOrDefault();
		}
	}
}

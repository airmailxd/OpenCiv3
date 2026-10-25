using System.Collections.Generic;
using System.Linq;
using C7GameData;
using Serilog;

namespace C7Engine {
	// Kings & Nomads' independent city-states grow stronger as the game
	// goes on, so they last into the middle game (owner's design,
	// 2026-10-23): every GarrisonGrowthTurns turns each gains a defender, up
	// to MaxGarrison, the best defender the leading civs know, and on turn
	// WallsTurn they all gain walls.
	//
	// Owner's rework, 2026-10-25: a city-state's citizens are all happy, and
	// it grows without limit (see City.CanGrowPopulationByOne);
	// its center makes CenterFood food more, and CenterFoodPerCitizen more
	// for each citizen; its citizens work only the eight tiles around it
	// (see City.GetWorkableTiles), and those beyond become specialists, each
	// making SpecialistShields shields for the city-state and
	// SpecialistBeakers beakers and SpecialistGold gold for each civ trading
	// with it (see CityStateTrade). Its shields build units while its
	// garrison is short of MaxGarrison (barbarian cities build nothing
	// otherwise): the best the most advanced civ knows that the resources
	// joined to it by road allow, in the order of BuildOrder (owner's
	// ruling, 2026-10-25), and its attackers and bombard units strike
	// enemies next to it (see CityStateDefenseAI).
	public static class CityStates {
		private static readonly ILogger log = Log.ForContext(typeof(CityStates));

		public const int CenterFood = 5;
		public const int CenterFoodPerCitizen = 2;
		public const int SpecialistShields = 4;
		public const int SpecialistBeakers = 2;
		public const int SpecialistGold = 2;
		public const int GarrisonGrowthTurns = 10;

		// What a city-state a civ takes comes down to (owner's ruling,
		// 2026-10-25).
		public const int CapturedSize = 5;

		// What each city-state a civ has taken leaves of its units'
		// strength against the rest: soldiers are loath to fight their
		// brethren (owner's ruling, 2026-10-25; HEURISTIC: the 0.9).
		public const double BrethrenStrengthPerCapture = 0.9;

		// Whether two city-states' nine tiles would touch: one of A's tiles
		// is one of B's, or next to one.
		public static bool WouldTouch(Tile a, Tile b) {
			HashSet<Tile> near = new();
			foreach (Tile t in a.GetTilesWithinRankDistance(1)) {
				if (t == Tile.NONE) {
					continue;
				}
				near.Add(t);
				foreach (Tile n in t.neighbors.Values) {
					near.Add(n);
				}
			}
			return b.GetTilesWithinRankDistance(1).Any(t => t != Tile.NONE && near.Contains(t));
		}

		// What a city-state's garrison is made of, and the order it builds
		// them in, over and over.
		public enum Role { Defender, Attacker, Bombard }
		public static readonly Role[] BuildOrder = [Role.Defender, Role.Defender, Role.Attacker, Role.Bombard];
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
			foreach (City city in independents.cities) {
				ArrangeCitizens(gameData, city);
			}
			BuildUnits(gameData, independents);
			CityStateTrade.GiveGifts(gameData);
		}

		// Puts the city-state's idle citizens to work: on a free tile around
		// it, or else as specialists (see CityTileAssignmentAI).
		public static void ArrangeCitizens(GameData gameData, City city) {
			foreach (CityResident r in city.residents.Where(r => r.citizenType.IsDefaultCitizen && r.tileWorked == Tile.NONE).ToList()) {
				AI.CityTileAssignmentAI.AssignNewCitizenToTile(gameData, r);
			}
		}

		// The city-state's specialists: its citizens who work no tile.
		public static int Specialists(City city) {
			return city.residents.Count(r => !r.citizenType.IsDefaultCitizen);
		}

		// Each city-state puts its shields towards the next unit of its
		// build order, which it raises once they pay for it, while its
		// garrison is short.
		private static void BuildUnits(GameData gameData, Player independents) {
			foreach (City city in independents.cities.ToList()) {
				if (Garrison(city) >= MaxGarrison || NextUnit(gameData, city) is not UnitPrototype unit) {
					city.SetStoredShields(0);
					continue;
				}
				int stored = city.shieldsStored + city.CurrentProductionYield().useful;
				int cost = independents.ShieldCost(unit);
				if (stored >= cost) {
					gameData.SpawnUnit(independents, unit, city.location);
					stored -= cost;
				}
				city.SetStoredShields(stored);
			}
		}

		// The unit's place in a garrison.
		public static Role RoleOf(UnitPrototype unit) {
			if (unit.bombard > 0 && unit.actions.Contains(UnitAction.Bombard)) {
				return Role.Bombard;
			}
			return unit.attack > unit.defense ? Role.Attacker : Role.Defender;
		}

		// The role the city-state builds next: the first in its build order,
		// gone through again and again, that its garrison has fewer of than
		// the order has come to. A role it can build no unit for is skipped.
		public static Role? NextRole(GameData gameData, City city) {
			Dictionary<Role, int> have = city.location.unitsOnTile
				.Where(u => u.owner == city.owner && IsGarrison(u))
				.GroupBy(u => RoleOf(u.unitType)).ToDictionary(g => g.Key, g => g.Count());
			Dictionary<Role, int> wanted = new();
			for (int i = 0; i < MaxGarrison * BuildOrder.Length; ++i) {
				Role role = BuildOrder[i % BuildOrder.Length];
				wanted[role] = wanted.GetValueOrDefault(role) + 1;
				if (wanted[role] > have.GetValueOrDefault(role) && Best(gameData, city, role) != null) {
					return role;
				}
			}
			return null;
		}

		public static UnitPrototype NextUnit(GameData gameData, City city) {
			return NextRole(gameData, city) is Role role ? Best(gameData, city, role) : null;
		}

		// The civ that knows the most advances.
		private static Player MostAdvanced(GameData gameData) {
			return gameData.players.Where(p => !p.isBarbarians && !p.defeated).MaxBy(p => p.knownTechs.Count);
		}

		// The common units (not unique ones) of the role the most advanced
		// civ could build, whose resources the city-state has, if a
		// city-state is given.
		private static IEnumerable<UnitPrototype> Available(GameData gameData, City cityState) {
			Player leader = MostAdvanced(gameData);
			int civilizations = gameData.civilizations.Count(c => !c.isBarbarian);
			HashSet<Resource> resources = cityState == null ? null : Resources(cityState);
			return gameData.unitPrototypes
				.Where(u => u.IsLandUnit() && !u.unproducible && (u.attack > 0 || u.defense > 0 || u.bombard > 0)
					&& !u.isWorker && !u.isSettler && !u.isKing && !u.isLeader && !u.isArmy
					&& u.producibleBy.Count(c => !c.isBarbarian) * 2 > civilizations
					&& (u.requiredTech == null || leader?.knownTechs.Contains(u.requiredTech.id) == true)
					&& (resources == null || u.requiredResources.All(resources.Contains)));
		}

		// The best unit of the role the city-state can build: the highest
		// defence, attack or bombard strength, the cheapest of equals.
		public static UnitPrototype Best(GameData gameData, City cityState, Role role) {
			IEnumerable<UnitPrototype> units = Available(gameData, cityState).Where(u => RoleOf(u) == role);
			return (role switch {
				Role.Defender => units.OrderByDescending(u => u.defense),
				Role.Attacker => units.OrderByDescending(u => u.attack),
				_ => units.OrderByDescending(u => u.bombard),
			}).ThenBy(u => u.shieldCost).FirstOrDefault();
		}

		// The resources the city-state has: those in its borders, and any on
		// the roads joined to it, whoever's land they cross (owner's ruling,
		// 2026-10-25).
		public static HashSet<Resource> Resources(City city) {
			HashSet<Resource> result = new();
			HashSet<Tile> seen = [city.location];
			Queue<Tile> roads = new([city.location]);
			while (roads.Count > 0) {
				Tile t = roads.Dequeue();
				if (t.Resource != Resource.NONE) {
					result.Add(t.Resource);
				}
				foreach (Tile n in t.neighbors.Values) {
					if (n != Tile.NONE && n.IsLand() && n.HasRoad() && seen.Add(n)) {
						roads.Enqueue(n);
					}
				}
			}
			foreach (Tile t in city.location.GetTilesWithinRankDistance(1)) {
				if (t != Tile.NONE && t.owningCity == city && t.Resource != Resource.NONE) {
					result.Add(t.Resource);
				}
			}
			return result;
		}

		public static int Garrison(City city) {
			return city.location.unitsOnTile.Count(u => u.owner == city.owner && IsGarrison(u));
		}

		// Whether the unit is one of a garrison's fighting units.
		private static bool IsGarrison(MapUnit u) {
			UnitPrototype t = u.unitType;
			return !t.isWorker && !t.isSettler && !t.isKing && !t.isLeader && (t.attack > 0 || t.defense > 0 || t.bombard > 0);
		}

		// The best defender (by defence) of the common units (not unique
		// ones) the most advanced civ knows, or that need no advance,
		// whatever resources they need: the city-states' garrisons' growth,
		// and their gifts.
		public static UnitPrototype Defender(GameData gameData) {
			return Best(gameData, null, Role.Defender);
		}
	}
}
